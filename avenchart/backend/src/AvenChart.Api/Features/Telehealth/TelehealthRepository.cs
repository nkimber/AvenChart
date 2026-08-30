// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthRepository(NpgsqlDataSource dataSource)
{
    private const string ApplicantReservationCandidatePredicate = """
        (
          r.source_applicant_id is null
          or exists (
            select 1
            from telehealth_applicant_request_queue_authorizations queue_authorization
            join staff candidate on candidate.id=queue_authorization.candidate_staff_id
            where queue_authorization.request_id=r.request_id
              and queue_authorization.applicant_id=r.source_applicant_id
              and queue_authorization.practice_id=r.practice_id
              and queue_authorization.facility_id=r.facility_id
              and queue_authorization.canonical_patient_id=r.patient_id
              and queue_authorization.candidate_staff_id=@clinician
              and queue_authorization.resulting_request_status='Queued'
              and queue_authorization.resulting_request_version=13
              and queue_authorization.policy_key='SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
              and queue_authorization.policy_version=1
              and queue_authorization.evidence_type='APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
              and queue_authorization.source_mode='NON_PRODUCTION'
              and queue_authorization.compatibility_target='AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1'
              and queue_authorization.business_outcome='SyntheticRequestAuthorizedToQueue'
              and queue_authorization.practice_accepted
              and queue_authorization.patient_care_queue_entered
              and queue_authorization.clinician_queue_entered
              and queue_authorization.doctor_search_started
              and queue_authorization.appointment_created
              and not queue_authorization.rendering_physician_assigned
              and not queue_authorization.coverage_verified
              and not queue_authorization.financial_route_created
              and not queue_authorization.queue_position_assigned
              and not queue_authorization.encounter_created
              and not queue_authorization.consent_created
              and not queue_authorization.care_authorized
              and not queue_authorization.integration_enabled
              and not queue_authorization.external_call_performed
              and queue_authorization.authorized_at < queue_authorization.result_valid_through
              and now() < queue_authorization.result_valid_through
              and candidate.active
              and candidate.role in ('physician','provider')
              and candidate.facility_id=r.facility_id
          )
        )
        """;

    public async Task<TelehealthRequestResponse> CreateAsync(
        string practiceId,
        int facilityId,
        string patientId,
        string complaintCategory,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var requestId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into telehealth_requests(
                  request_id, practice_id, facility_id, patient_id, status,
                  complaint_category, version, create_idempotency_key, create_fingerprint)
                select @request_id, @practice_id, @facility_id, patient.canonical_id, 'Draft',
                       @complaint_category, 1, @idempotency_key, @fingerprint
                from patients patient
                where patient.canonical_id = @patient_id
                  and patient.facility_id = @facility_id
                  and patient.portal_enabled = true
                  and patient.merged_into_patient_id is null
                  and patient.lifecycle_status = 'active'
                  and patient.date_of_birth between current_date - interval '120 years'
                                                and current_date - interval '18 years'
                on conflict (practice_id, patient_id, create_idempotency_key) do nothing
                returning request_id;
                """;
            command.Parameters.AddWithValue("request_id", requestId);
            command.Parameters.AddWithValue("practice_id", practiceId);
            command.Parameters.AddWithValue("facility_id", facilityId);
            command.Parameters.AddWithValue("patient_id", patientId);
            command.Parameters.AddWithValue("complaint_category", complaintCategory);
            command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
            command.Parameters.AddWithValue("fingerprint", fingerprint);
            var inserted = await command.ExecuteScalarAsync(cancellationToken);
            if (inserted is null)
            {
                var existing = await GetByCreateIdempotencyAsync(
                    connection, transaction, practiceId, patientId, idempotencyKey, cancellationToken);
                if (existing is null)
                {
                    throw TelehealthProblem.NotFound();
                }

                if (!string.Equals(existing.CreateFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw TelehealthProblem.Conflict(
                        "telehealth_idempotency_conflict",
                        "The idempotency key was already used with different request content.");
                }

                await transaction.CommitAsync(cancellationToken);
                return await GetPatientRequestAsync(practiceId, patientId, existing.RequestId, cancellationToken)
                    ?? throw TelehealthProblem.NotFound();
            }
        }

        await InsertEventAsync(
            connection, transaction, requestId, 1, "request-created", null, "Draft",
            "patient", patientId, idempotencyKey, fingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPatientRequestAsync(practiceId, patientId, requestId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public Task<TelehealthRequestResponse> ConfirmLocationAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        string stateCode,
        int expectedVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken) =>
        MutatePatientRequestAsync(
            practiceId,
            patientId,
            requestId,
            expectedVersion,
            idempotencyKey,
            fingerprint,
            [TelehealthRequestStatus.Draft],
            TelehealthRequestStatus.LocationConfirmed,
            "location-confirmed",
            async (connection, transaction, current, newVersion, token) =>
            {
                await using var evidence = connection.CreateCommand();
                evidence.Transaction = transaction;
                evidence.CommandText = """
                    insert into telehealth_patient_locations(
                      location_id, request_id, state_code, request_version,
                      idempotency_key, command_fingerprint)
                    values (@location_id, @request_id, @state_code, @version,
                            @idempotency_key, @fingerprint);
                    """;
                evidence.Parameters.AddWithValue("location_id", Guid.NewGuid());
                evidence.Parameters.AddWithValue("request_id", current.RequestId);
                evidence.Parameters.AddWithValue("state_code", stateCode);
                evidence.Parameters.AddWithValue("version", newVersion);
                evidence.Parameters.AddWithValue("idempotency_key", idempotencyKey);
                evidence.Parameters.AddWithValue("fingerprint", fingerprint);
                await evidence.ExecuteNonQueryAsync(token);
            },
            triageOutcome: null,
            cancellationToken);

    public async Task<TelehealthRequestResponse> EvaluateTriageAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        TelehealthTriageResult result,
        int expectedVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var next = result.Outcome == TelehealthTriageOutcome.TelehealthEligible
            ? TelehealthRequestStatus.Intake
            : TelehealthRequestStatus.Redirected;
        return await MutatePatientRequestAsync(
            practiceId,
            patientId,
            requestId,
            expectedVersion,
            idempotencyKey,
            fingerprint,
            [TelehealthRequestStatus.LocationConfirmed],
            next,
            "triage-evaluated",
            async (connection, transaction, current, newVersion, token) =>
            {
                await using (var protocol = connection.CreateCommand())
                {
                    protocol.Transaction = transaction;
                    protocol.CommandText = """
                        insert into telehealth_protocol_versions(
                          protocol_id, protocol_key, protocol_version, content_hash, is_synthetic, published_at)
                        values (@id, @key, @version, @hash, true, timestamptz '2026-08-26 00:00:00+00')
                        on conflict (protocol_key, protocol_version) do nothing;
                        """;
                    protocol.Parameters.AddWithValue("id", result.ProtocolId);
                    protocol.Parameters.AddWithValue("key", result.ProtocolKey);
                    protocol.Parameters.AddWithValue("version", result.ProtocolVersion);
                    protocol.Parameters.AddWithValue("hash", result.ProtocolContentHash);
                    await protocol.ExecuteNonQueryAsync(token);
                }

                await using var assessment = connection.CreateCommand();
                assessment.Transaction = transaction;
                assessment.CommandText = """
                    insert into telehealth_triage_assessments(
                      assessment_id, request_id, protocol_id, answer_fingerprint,
                      outcome, request_version, idempotency_key, command_fingerprint)
                    values (@assessment_id, @request_id, @protocol_id, @answer_fingerprint,
                            @outcome, @request_version, @idempotency_key, @fingerprint);
                    """;
                assessment.Parameters.AddWithValue("assessment_id", Guid.NewGuid());
                assessment.Parameters.AddWithValue("request_id", current.RequestId);
                assessment.Parameters.AddWithValue("protocol_id", result.ProtocolId);
                assessment.Parameters.AddWithValue("answer_fingerprint", result.AnswerFingerprint);
                assessment.Parameters.AddWithValue("outcome", result.Outcome.ToString());
                assessment.Parameters.AddWithValue("request_version", newVersion);
                assessment.Parameters.AddWithValue("idempotency_key", idempotencyKey);
                assessment.Parameters.AddWithValue("fingerprint", fingerprint);
                await assessment.ExecuteNonQueryAsync(token);
            },
            result.Outcome.ToString(),
            cancellationToken);
    }

    public async Task<TelehealthPatientReadinessResponse> GetPatientReadinessAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var snapshot = await LoadReadinessSnapshotAsync(
            connection, null, practiceId, patientId, requestId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
        return ToReadinessResponse(snapshot);
    }

    public Task<TelehealthRequestResponse> CompleteReadinessAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        CompleteTelehealthReadinessRequest request,
        string normalizedComplaintSummary,
        string normalizedSymptomDuration,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken) =>
        MutatePatientRequestAsync(
            practiceId,
            patientId,
            requestId,
            request.ExpectedVersion,
            idempotencyKey,
            fingerprint,
            [TelehealthRequestStatus.Intake, TelehealthRequestStatus.Verification, TelehealthRequestStatus.OperationalReview],
            TelehealthRequestStatus.Verification,
            "patient-readiness-completed",
            async (connection, transaction, current, newVersion, token) =>
            {
                var snapshot = await LoadReadinessSnapshotAsync(
                    connection, transaction, practiceId, patientId, requestId, token)
                    ?? throw TelehealthProblem.NotFound();
                RequireReadinessProjection(snapshot, request);
                var coverage = snapshot.CoverageOptions.SingleOrDefault(item =>
                    string.Equals(item.CoverageToken, request.CoverageToken, StringComparison.Ordinal));
                if (coverage is null)
                {
                    throw TelehealthProblem.BadRequest(
                        "telehealth_coverage_record_invalid",
                        "The selected coverage record is not available to this patient request.");
                }

                await InsertPatientConfirmationAsync(
                    connection, transaction, current, request, newVersion, idempotencyKey, fingerprint, token);
                await InsertIntakeSnapshotAsync(
                    connection, transaction, current.RequestId, normalizedComplaintSummary,
                    normalizedSymptomDuration, newVersion, idempotencyKey, fingerprint, token);
                await InsertAcknowledgmentAsync(
                    connection, transaction, current.RequestId, request, newVersion,
                    idempotencyKey, fingerprint, token);
                await InsertCoverageSelectionAsync(
                    connection, transaction, current, coverage, newVersion,
                    idempotencyKey, fingerprint, token);
            },
            triageOutcome: null,
            cancellationToken);

    public async Task<TelehealthCoverageGatewayInput> GetCoverageGatewayInputAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await LoadCoverageGatewayInputAsync(
            connection, null, practiceId, patientId, requestId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public Task<TelehealthRequestResponse> RecordCoverageVerificationAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        int expectedVersion,
        TelehealthCoverageGatewayInput gatewayInput,
        TelehealthCoverageGatewayResult result,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var expectedStatus = Enum.Parse<TelehealthRequestStatus>(gatewayInput.RequestStatus);
        if (expectedStatus is not (TelehealthRequestStatus.Verification or TelehealthRequestStatus.OperationalReview))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_invalid_transition",
                "Coverage can be verified only while the request is in Verification or OperationalReview.");
        }
        var nextStatus = result.EligibilityStatus == TelehealthEligibilityStatus.Active
            && result.NetworkStatus == TelehealthNetworkStatus.ConfirmedInNetwork
            ? TelehealthRequestStatus.OperationalReview
            : TelehealthRequestStatus.Verification;
        return MutatePatientRequestAsync(
            practiceId,
            patientId,
            requestId,
            expectedVersion,
            idempotencyKey,
            fingerprint,
            [expectedStatus],
            nextStatus,
            "coverage-verification-recorded",
            async (connection, transaction, current, newVersion, token) =>
            {
                var currentInput = await LoadCoverageGatewayInputAsync(
                    connection, transaction, practiceId, patientId, requestId, token)
                    ?? throw TelehealthProblem.NotFound();
                if (currentInput != gatewayInput)
                {
                    throw TelehealthProblem.Conflict(
                        "telehealth_coverage_projection_stale",
                        "The selected coverage or request context changed before verification could be recorded. Refresh and try again.");
                }

                await InsertCoverageVerificationAsync(
                    connection, transaction, current.RequestId, result, newVersion,
                    idempotencyKey, fingerprint, token);
            },
            triageOutcome: null,
            cancellationToken);
    }

    public Task<TelehealthRequestResponse> CancelRequestAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        int expectedVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken) =>
        MutatePatientRequestAsync(
            practiceId,
            patientId,
            requestId,
            expectedVersion,
            idempotencyKey,
            fingerprint,
            [
                TelehealthRequestStatus.Draft,
                TelehealthRequestStatus.LocationConfirmed,
                TelehealthRequestStatus.SafetyScreening,
                TelehealthRequestStatus.Intake,
                TelehealthRequestStatus.Verification,
                TelehealthRequestStatus.OperationalReview
            ],
            TelehealthRequestStatus.Cancelled,
            "synthetic-request-cancelled",
            static (_, _, _, _, _) => Task.CompletedTask,
            triageOutcome: null,
            cancellationToken);

    public async Task<TelehealthRequestResponse> AuthorizeToQueueAsync(
        string practiceId,
        int facilityId,
        string administratorActorId,
        Guid requestId,
        int expectedVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await GetRequestForUpdateAsync(connection, transaction, requestId, cancellationToken);
        if (current is null || current.PracticeId != practiceId || current.FacilityId != facilityId)
        {
            throw TelehealthProblem.NotFound();
        }

        if (await IsApplicantOriginatedAsync(connection, transaction, requestId, cancellationToken))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_dedicated_authorization_required",
                "Applicant-originated requests require the evidence-bound applicant queue-authorization route.");
        }

        if (await IsReplayAsync(connection, transaction, requestId, idempotencyKey, fingerprint, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return await GetStaffRequestAsync(practiceId, facilityId, requestId, cancellationToken)
                ?? throw TelehealthProblem.NotFound();
        }

        RequireVersion(current.Version, expectedVersion);
        TelehealthRequestStateMachine.RequireTransition(current.Status, TelehealthRequestStatus.Queued);
        if (current.TriageOutcome != TelehealthTriageOutcome.TelehealthEligible.ToString())
        {
            throw TelehealthProblem.Conflict(
                "telehealth_clinical_gate_failed",
                "Only a synthetic TelehealthEligible result can enter the clinician queue.");
        }
        var readiness = await LoadReadinessSnapshotAsync(
            connection, transaction, practiceId, current.PatientId, requestId, cancellationToken);
        if (readiness is null
            || !await HasCurrentPatientReadinessEvidenceAsync(
                connection, transaction, requestId, readiness, cancellationToken))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_readiness_gate_stale",
                "Patient details, clinical summary, intake, or acknowledgment evidence changed or is incomplete. The patient must refresh readiness.");
        }
        if (!await HasCurrentConfirmedCoverageAsync(
            connection, transaction, requestId, current.PatientId, current.Version, cancellationToken))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_coverage_gate_failed",
                "Current synthetic Active eligibility and ConfirmedInNetwork evidence are required before queue authorization.");
        }

        var newVersion = current.Version + 1;
        var queueEntryId = Guid.NewGuid();
        var appointmentId = $"TH-APPT-{requestId:N}";
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                insert into appointments(
                  id,patient_id,pid,provider_id,facility_id,billing_location_id,
                  appointment_date,start_time,duration_minutes,category_id,title,status,
                  room,comments,recurrence_type)
                select @appointment_id,patient.canonical_id,patient.legacy_pid,null,@facility_id,@facility_id,
                       current_date,localtime(0),30,9,'Immediate telehealth','-',null,null,0
                from patients patient
                where patient.canonical_id=@patient_id and patient.facility_id=@facility_id
                  and patient.merged_into_patient_id is null
                  and coalesce(lower(patient.lifecycle_status),'active')='active'
                  and patient.deceased_date is null;
                update telehealth_requests
                set status='Queued', appointment_id=@appointment_id, version=@version, ready_at=now(), updated_at=now()
                where request_id=@request_id;
                insert into telehealth_queue_entries(
                  queue_entry_id, request_id, practice_id, facility_id, status, ready_at, authorized_by_actor_id)
                values (@queue_entry_id, @request_id, @practice_id, @facility_id, 'Ready', now(), @actor_id);
                """;
            update.Parameters.AddWithValue("version", newVersion);
            update.Parameters.AddWithValue("request_id", requestId);
            update.Parameters.AddWithValue("patient_id", current.PatientId);
            update.Parameters.AddWithValue("appointment_id", appointmentId);
            update.Parameters.AddWithValue("queue_entry_id", queueEntryId);
            update.Parameters.AddWithValue("practice_id", practiceId);
            update.Parameters.AddWithValue("facility_id", facilityId);
            update.Parameters.AddWithValue("actor_id", administratorActorId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection, transaction, requestId, newVersion, "operationally-authorized",
            current.Status.ToString(), "Queued", "administrator", administratorActorId,
            idempotencyKey, fingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetStaffRequestAsync(practiceId, facilityId, requestId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthShiftResponse> StartShiftAsync(
        string practiceId,
        int facilityId,
        int clinicianStaffId,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existing = await FindShiftByIdempotencyAsync(
            connection, transaction, practiceId, clinicianStaffId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.Fingerprint != fingerprint)
            {
                throw TelehealthProblem.Conflict("telehealth_idempotency_conflict", "The shift idempotency key was reused with different content.");
            }

            await transaction.CommitAsync(cancellationToken);
            return existing.Response;
        }

        await using (var active = connection.CreateCommand())
        {
            active.Transaction = transaction;
            active.CommandText = """
                select shift_id, status, facility_id, clinician_staff_id, started_at, version
                from telehealth_clinician_shifts
                where practice_id=@practice_id and facility_id=@facility_id
                  and clinician_staff_id=@clinician and status in ('Active','Busy')
                for update;
                """;
            active.Parameters.AddWithValue("practice_id", practiceId);
            active.Parameters.AddWithValue("facility_id", facilityId);
            active.Parameters.AddWithValue("clinician", clinicianStaffId);
            await using var reader = await active.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var response = ReadShift(reader);
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return response;
            }
        }

        var shiftId = Guid.NewGuid();
        await using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = """
                insert into telehealth_clinician_shifts(
                  shift_id, practice_id, facility_id, clinician_staff_id, status,
                  start_idempotency_key, start_fingerprint)
                select @shift_id, @practice_id, @facility_id, staff.id, 'Active', @idempotency_key, @fingerprint
                from staff
                where staff.id=@clinician and staff.active=true and staff.facility_id=@facility_id
                returning shift_id, status, facility_id, clinician_staff_id, started_at, version;
                """;
            create.Parameters.AddWithValue("shift_id", shiftId);
            create.Parameters.AddWithValue("practice_id", practiceId);
            create.Parameters.AddWithValue("facility_id", facilityId);
            create.Parameters.AddWithValue("clinician", clinicianStaffId);
            create.Parameters.AddWithValue("idempotency_key", idempotencyKey);
            create.Parameters.AddWithValue("fingerprint", fingerprint);
            await using var reader = await create.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw TelehealthProblem.Forbidden("telehealth_clinician_ineligible", "The clinician is not active in the selected facility.");
            }

            var response = ReadShift(reader);
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
    }

    public async Task<TelehealthShiftResponse> EndIdleShiftAsync(
        string practiceId, int facilityId, int clinicianStaffId, Guid shiftId, int expectedVersion,
        string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await using (var replay = connection.CreateCommand())
        {
            replay.Transaction = transaction;
            replay.CommandText = """
                select shift_id,status,facility_id,clinician_staff_id,started_at,version,ended_at,end_fingerprint
                from telehealth_clinician_shifts where practice_id=@practice and facility_id=@facility and clinician_staff_id=@clinician
                  and shift_id=@shift and end_idempotency_key=@key for update;
                """;
            replay.Parameters.AddWithValue("practice", practiceId); replay.Parameters.AddWithValue("facility", facilityId);
            replay.Parameters.AddWithValue("clinician", clinicianStaffId); replay.Parameters.AddWithValue("shift", shiftId); replay.Parameters.AddWithValue("key", idempotencyKey);
            await using var reader = await replay.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (!string.Equals(reader.GetString(7), fingerprint, StringComparison.Ordinal)) throw TelehealthProblem.Conflict("telehealth_idempotency_conflict", "The shift-end idempotency key was reused with different content.");
                var result = new TelehealthShiftResponse(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetFieldValue<DateTimeOffset>(4), checked((int)reader.GetInt64(5)), reader.GetFieldValue<DateTimeOffset>(6));
                await transaction.CommitAsync(cancellationToken); return result;
            }
        }
        await using (var end = connection.CreateCommand())
        {
            end.Transaction = transaction;
            end.CommandText = """
                update telehealth_clinician_shifts shift set status='Ended',ended_at=now(),end_idempotency_key=@key,end_fingerprint=@fingerprint,version=version+1
                where shift.shift_id=@shift and shift.practice_id=@practice and shift.facility_id=@facility and shift.clinician_staff_id=@clinician
                  and shift.status='Active' and shift.version=@expected
                  and not exists(select 1 from telehealth_reservations reservation where reservation.shift_id=shift.shift_id and reservation.status='Active')
                  and not exists(select 1 from telehealth_consultation_contexts context where context.shift_id=shift.shift_id and context.status in ('Started','MediaEnded'))
                returning shift_id,status,facility_id,clinician_staff_id,started_at,version,ended_at;
                """;
            end.Parameters.AddWithValue("shift", shiftId); end.Parameters.AddWithValue("practice", practiceId); end.Parameters.AddWithValue("facility", facilityId); end.Parameters.AddWithValue("clinician", clinicianStaffId);
            end.Parameters.AddWithValue("expected", expectedVersion); end.Parameters.AddWithValue("key", idempotencyKey); end.Parameters.AddWithValue("fingerprint", fingerprint);
            await using var reader = await end.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw TelehealthProblem.Conflict("telehealth_shift_end_unavailable", "The shift is stale, no longer idle, or cannot be ended.");
            var result = new TelehealthShiftResponse(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetFieldValue<DateTimeOffset>(4), checked((int)reader.GetInt64(5)), reader.GetFieldValue<DateTimeOffset>(6));
            await transaction.CommitAsync(cancellationToken); return result;
        }
    }

    public async Task<TelehealthReservationResponse?> ReserveNextAsync(
        string practiceId,
        int facilityId,
        int clinicianStaffId,
        int leaseSeconds,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var shift = await GetActiveShiftForUpdateAsync(
            connection, transaction, practiceId, facilityId, clinicianStaffId, cancellationToken)
            ?? throw TelehealthProblem.Conflict("telehealth_active_shift_required", "Start an active telehealth shift before reserving a request.");

        var replay = await FindReservationByIdempotencyAsync(
            connection, transaction, clinicianStaffId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.Fingerprint != fingerprint)
            {
                throw TelehealthProblem.Conflict("telehealth_idempotency_conflict", "The reservation idempotency key was reused with different content.");
            }

            await transaction.CommitAsync(cancellationToken);
            return replay.Response;
        }

        await ExpireReservationsAsync(connection, transaction, practiceId, facilityId, cancellationToken);
        if (await HasActiveReservationAsync(connection, transaction, clinicianStaffId, cancellationToken))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_clinician_already_reserved",
                "The clinician already has an active request reservation.");
        }

        QueueCandidate? candidate;
        await using (var next = connection.CreateCommand())
        {
            next.Transaction = transaction;
            next.CommandText = $"""
                select q.queue_entry_id, q.request_id, r.version,
                       r.source_applicant_id is not null
                from telehealth_queue_entries q
                join telehealth_requests r on r.request_id=q.request_id
                join appointments appointment
                  on appointment.id=r.appointment_id
                 and appointment.patient_id=r.patient_id
                 and appointment.facility_id=r.facility_id
                where q.practice_id=@practice_id and q.facility_id=@facility_id
                  and q.status='Ready' and r.status='Queued'
                  and r.triage_outcome='TelehealthEligible'
                  and appointment.provider_id is null
                  and coalesce(appointment.status,'-')='-'
                  and {ApplicantReservationCandidatePredicate}
                order by q.ready_at, q.queue_entry_id
                for update of q, r, appointment skip locked
                limit 1;
                """;
            next.Parameters.AddWithValue("practice_id", practiceId);
            next.Parameters.AddWithValue("facility_id", facilityId);
            next.Parameters.AddWithValue("clinician", clinicianStaffId);
            await using var reader = await next.ExecuteReaderAsync(cancellationToken);
            candidate = await reader.ReadAsync(cancellationToken)
                ? new QueueCandidate(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    checked((int)reader.GetInt64(2)),
                    reader.GetBoolean(3))
                : null;
        }

        if (candidate is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var reservationId = Guid.NewGuid();
        var newRequestVersion = candidate.RequestVersion + 1;
        TelehealthRequestStateMachine.RequireTransition(TelehealthRequestStatus.Queued, TelehealthRequestStatus.Reserved);
        TelehealthReservationResponse response;
        await using (var reserve = connection.CreateCommand())
        {
            reserve.Transaction = transaction;
            reserve.CommandText = """
                insert into telehealth_reservations(
                  reservation_id, request_id, queue_entry_id, shift_id, clinician_staff_id,
                  status, lease_expires_at, idempotency_key, command_fingerprint)
                values (@reservation_id, @request_id, @queue_entry_id, @shift_id, @clinician,
                        'Active', now() + make_interval(secs => @lease_seconds), @idempotency_key, @fingerprint);
                update telehealth_queue_entries
                set status='Reserved', version=version+1, updated_at=now()
                where queue_entry_id=@queue_entry_id;
                update telehealth_requests
                set status='Reserved', version=@request_version, updated_at=now()
                where request_id=@request_id;
                update appointments
                set provider_id=@clinician,appointment_date=current_date,start_time=localtime(0),row_version=row_version+1
                where id=(select appointment_id from telehealth_requests where request_id=@request_id)
                  and coalesce(status,'-')='-';
                select reservation_id, request_id, queue_entry_id, shift_id, clinician_staff_id,
                       reserved_at, lease_expires_at, status
                from telehealth_reservations where reservation_id=@reservation_id;
                """;
            reserve.Parameters.AddWithValue("reservation_id", reservationId);
            reserve.Parameters.AddWithValue("request_id", candidate.RequestId);
            reserve.Parameters.AddWithValue("queue_entry_id", candidate.QueueEntryId);
            reserve.Parameters.AddWithValue("shift_id", shift.ShiftId);
            reserve.Parameters.AddWithValue("clinician", clinicianStaffId);
            reserve.Parameters.AddWithValue("lease_seconds", leaseSeconds);
            reserve.Parameters.AddWithValue("request_version", newRequestVersion);
            reserve.Parameters.AddWithValue("idempotency_key", idempotencyKey);
            reserve.Parameters.AddWithValue("fingerprint", fingerprint);
            await using var reader = await reserve.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw TelehealthProblem.Conflict("telehealth_reservation_failed", "The next request could not be reserved.");
            }
            response = ReadReservation(reader, newRequestVersion, candidate.ApplicantOriginated);
        }

        await InsertEventAsync(
            connection, transaction, candidate.RequestId, newRequestVersion, "request-reserved",
            "Queued", "Reserved", "physician", clinicianStaffId.ToString(),
            idempotencyKey, fingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<TelehealthRequestListResponse> ListPatientRequestsAsync(
        string practiceId,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = CreateRequestProjectionCommand(connection);
        command.CommandText += " where r.practice_id=@practice_id and r.patient_id=@patient_id order by r.created_at desc;";
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("patient_id", patientId);
        var items = await ReadRequestListAsync(command, cancellationToken);
        return new TelehealthRequestListResponse(items);
    }

    public async Task<TelehealthPatientQueueStatusResponse> GetPatientQueueStatusAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select r.request_id, r.status, r.version, r.updated_at, now() as snapshot_at,
                   case
                     when r.status='Queued' and current_queue.status='Ready' then (
                       select count(*)
                       from telehealth_queue_entries candidate
                       join telehealth_requests candidate_request on candidate_request.request_id=candidate.request_id
                       where candidate.practice_id=r.practice_id
                         and candidate.facility_id=r.facility_id
                         and candidate.status='Ready'
                         and candidate_request.status='Queued'
                         and (
                           candidate.ready_at < current_queue.ready_at
                           or (candidate.ready_at=current_queue.ready_at and candidate.request_id < current_queue.request_id)
                         )
                     )
                     else null
                   end as requests_ahead
            from telehealth_requests r
            left join telehealth_queue_entries current_queue on current_queue.request_id=r.request_id
            where r.request_id=@request_id
              and r.practice_id=@practice_id
              and r.patient_id=@patient_id;
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("patient_id", patientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw TelehealthProblem.NotFound();
        }

        var status = Enum.Parse<TelehealthRequestStatus>(reader.GetString(1));
        var requestsAhead = reader.IsDBNull(5) ? null : checked((int?)reader.GetInt64(5));
        return TelehealthPatientQueueStatusProjector.Create(
            reader.GetGuid(0),
            status,
            checked((int)reader.GetInt64(2)),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            requestsAhead);
    }

    public async Task<TelehealthOperationalReviewResponse> ListOperationalReviewAsync(
        string practiceId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var items = await ListQueueProjectionAsync(
            practiceId,
            facilityId,
            "OperationalReview",
            clinicianStaffId: null,
            cancellationToken);
        return new TelehealthOperationalReviewResponse(items);
    }

    public async Task<TelehealthQueueResponse> ListClinicianQueueAsync(
        string practiceId,
        int facilityId,
        int clinicianStaffId,
        CancellationToken cancellationToken)
    {
        var items = await ListQueueProjectionAsync(
            practiceId,
            facilityId,
            "Queued",
            clinicianStaffId,
            cancellationToken);
        return new TelehealthQueueResponse(items);
    }

    private static async Task<ReadinessSnapshot?> LoadReadinessSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        string patientId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select r.request_id, r.version, r.status, r.complaint_category,
                   p.first_name, p.last_name, p.date_of_birth, p.email,
                   coalesce(nullif(p.phone_cell, ''), nullif(p.phone_home, ''), nullif(p.phone, '')) as phone,
                   concat_ws(', ', nullif(p.street, ''),
                     nullif(concat_ws(' ', nullif(p.city, ''), nullif(p.state, ''), nullif(p.postal_code, '')), '')) as address,
                   (select count(*) from medications m where m.patient_id=p.canonical_id and m.activity=1) as medication_count,
                   (select count(*) from allergies a where a.patient_id=p.canonical_id and a.activity=1) as allergy_count,
                   exists(select 1 from patient_histories h where h.patient_id=p.canonical_id) as history_available,
                   coalesce((select string_agg(
                     concat_ws('|', m.id, m.activity::text, m.lifecycle_version::text,
                       coalesce(m.modified_date::text, ''), coalesce(m.end_date::text, '')),
                     chr(31) order by m.id)
                     from medications m where m.patient_id=p.canonical_id), '') as medication_source,
                   coalesce((select string_agg(
                     concat_ws('|', a.id, a.activity::text, coalesce(a.end_date::text, '')),
                     chr(31) order by a.id)
                     from allergies a where a.patient_id=p.canonical_id), '') as allergy_source,
                   coalesce((select concat_ws('|', h.recorded_at::text, h.last_physical_exam, h.additional_history)
                     from patient_histories h where h.patient_id=p.canonical_id), '') as history_source
            from telehealth_requests r
            join patients p on p.canonical_id=r.patient_id
            where r.request_id=@request_id and r.practice_id=@practice_id and r.patient_id=@patient_id
              and p.facility_id=r.facility_id and p.portal_enabled=true and p.merged_into_patient_id is null;
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("patient_id", patientId);

        ReadinessSnapshot? snapshot;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var firstName = reader.GetString(4);
            var lastName = reader.GetString(5);
            var dateOfBirth = reader.GetFieldValue<DateOnly>(6).ToString("yyyy-MM-dd");
            var email = reader.IsDBNull(7) ? null : reader.GetString(7);
            var phone = reader.IsDBNull(8) ? null : reader.GetString(8);
            var address = reader.GetString(9);
            var medicationCount = checked((int)reader.GetInt64(10));
            var allergyCount = checked((int)reader.GetInt64(11));
            var historyAvailable = reader.GetBoolean(12);
            var demographicsFingerprint = TelehealthCommandFingerprint.Create(
                firstName, lastName, dateOfBirth, email, phone, address);
            var clinicalSummaryFingerprint = TelehealthCommandFingerprint.Create(
                medicationCount, allergyCount, historyAvailable,
                reader.GetString(13), reader.GetString(14), reader.GetString(15));
            snapshot = new ReadinessSnapshot(
                reader.GetGuid(0),
                checked((int)reader.GetInt64(1)),
                Enum.Parse<TelehealthRequestStatus>(reader.GetString(2)),
                reader.GetString(3),
                firstName,
                lastName,
                dateOfBirth,
                email,
                phone,
                address,
                demographicsFingerprint,
                medicationCount,
                allergyCount,
                historyAvailable,
                clinicalSummaryFingerprint,
                []);
        }

        await using var coverageCommand = connection.CreateCommand();
        coverageCommand.Transaction = transaction;
        coverageCommand.CommandText = """
            select id, coalesce(type, ''), coalesce(provider, ''), coalesce(plan_name, ''),
                   coalesce(policy_number, ''), coalesce(group_number, ''), coalesce(relationship, ''),
                   coalesce(subscriber_first_name, ''), coalesce(subscriber_last_name, ''),
                   subscriber_date_of_birth
            from insurance_records
            where patient_id=@patient_id
            order by case type when 'primary' then 1 when 'secondary' then 2 when 'tertiary' then 3 else 4 end, id;
            """;
        coverageCommand.Parameters.AddWithValue("patient_id", patientId);
        var coverageOptions = new List<CoverageSnapshot>();
        await using (var reader = await coverageCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var recordId = reader.GetString(0);
                var coverageType = reader.GetString(1);
                var provider = reader.GetString(2);
                var planName = reader.GetString(3);
                var policyNumber = reader.GetString(4);
                var groupNumber = reader.GetString(5);
                var relationship = reader.GetString(6);
                var subscriberFirstName = reader.GetString(7);
                var subscriberLastName = reader.GetString(8);
                var subscriberDateOfBirth = reader.IsDBNull(9)
                    ? string.Empty
                    : reader.GetFieldValue<DateOnly>(9).ToString("yyyy-MM-dd");
                var coverageFingerprint = CreateCoverageFingerprint(
                    recordId, patientId, coverageType, provider, planName, policyNumber,
                    groupNumber, relationship, subscriberFirstName, subscriberLastName,
                    subscriberDateOfBirth);
                coverageOptions.Add(new CoverageSnapshot(
                    recordId,
                    TelehealthCommandFingerprint.Create("coverage-option", requestId, patientId, recordId),
                    coverageType,
                    provider,
                    planName,
                    MaskIdentifier(policyNumber),
                    MaskIdentifier(groupNumber),
                    relationship,
                    coverageFingerprint));
            }
        }

        return snapshot with { CoverageOptions = coverageOptions };
    }

    private static TelehealthPatientReadinessResponse ToReadinessResponse(ReadinessSnapshot snapshot)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(snapshot.Email)) missing.Add("email");
        if (string.IsNullOrWhiteSpace(snapshot.Phone)) missing.Add("callback phone");
        if (string.IsNullOrWhiteSpace(snapshot.Address)) missing.Add("home address");

        var blocking = missing.Select(value => $"Current {value} is missing.").ToList();
        if (snapshot.CoverageOptions.Count == 0)
        {
            blocking.Add("No existing coverage record is available for this synthetic patient.");
        }
        if (snapshot.Status is not (TelehealthRequestStatus.Intake or TelehealthRequestStatus.Verification or TelehealthRequestStatus.OperationalReview))
        {
            blocking.Add("Readiness can be submitted only during Intake, Verification, or OperationalReview.");
        }

        return new TelehealthPatientReadinessResponse(
            snapshot.RequestId,
            snapshot.Version,
            snapshot.Status.ToString(),
            new TelehealthPatientDetailsResponse(
                string.Join(' ', snapshot.FirstName, snapshot.LastName),
                snapshot.DateOfBirth,
                snapshot.Email,
                snapshot.Phone,
                snapshot.Address,
                snapshot.DemographicsFingerprint,
                missing),
            new TelehealthClinicalSummaryResponse(
                snapshot.ActiveMedicationCount,
                snapshot.ActiveAllergyCount,
                snapshot.HistoryAvailable,
                snapshot.ClinicalSummaryFingerprint),
            snapshot.CoverageOptions.Select(item => new TelehealthCoverageOptionResponse(
                item.CoverageToken,
                string.IsNullOrWhiteSpace(item.CoverageType) ? "unspecified" : item.CoverageType,
                string.IsNullOrWhiteSpace(item.Provider) ? "Provider unavailable" : item.Provider,
                string.IsNullOrWhiteSpace(item.PlanName) ? "Plan unavailable" : item.PlanName,
                item.MaskedPolicyNumber,
                item.MaskedGroupNumber,
                string.IsNullOrWhiteSpace(item.SubscriberRelationship) ? "unspecified" : item.SubscriberRelationship,
                item.Fingerprint)).ToArray(),
            SyntheticTelehealthAcknowledgment.ToResponse(),
            blocking);
    }

    private static void RequireReadinessProjection(
        ReadinessSnapshot snapshot,
        CompleteTelehealthReadinessRequest request)
    {
        if (snapshot.Status is not (TelehealthRequestStatus.Intake or TelehealthRequestStatus.Verification or TelehealthRequestStatus.OperationalReview))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_invalid_transition",
                "Patient readiness can be completed only during Intake, Verification, or OperationalReview.");
        }
        if (!request.DemographicsConfirmed || !request.ContactConfirmed || !request.ClinicalSummaryConfirmed
            || !request.CoverageConfirmed || !request.SyntheticDataConfirmed || !request.AcknowledgmentAccepted)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_readiness_confirmation_required",
                "Every current-details, clinical-summary, coverage, synthetic-data, and acknowledgment confirmation is required.");
        }
        if (!string.Equals(snapshot.DemographicsFingerprint, request.DemographicsFingerprint, StringComparison.Ordinal)
            || !string.Equals(snapshot.ClinicalSummaryFingerprint, request.ClinicalSummaryFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_readiness_projection_stale",
                "Patient or clinical summary details changed after they were loaded. Refresh before confirming them.");
        }
        if (!string.Equals(request.AcknowledgmentPackageKey, SyntheticTelehealthAcknowledgment.PackageKey, StringComparison.Ordinal)
            || request.AcknowledgmentPackageVersion != SyntheticTelehealthAcknowledgment.PackageVersion
            || !string.Equals(request.AcknowledgmentContentHash, SyntheticTelehealthAcknowledgment.ContentHash, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_acknowledgment_version_stale",
                "The synthetic acknowledgment package changed after it was loaded. Refresh before accepting it.");
        }
        if (snapshot.CoverageOptions.Count == 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_coverage_record_required",
                "An existing synthetic coverage record is required for this bounded demonstration.");
        }
        if (string.IsNullOrWhiteSpace(snapshot.Email)
            || string.IsNullOrWhiteSpace(snapshot.Phone)
            || string.IsNullOrWhiteSpace(snapshot.Address))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_patient_details_incomplete",
                "Current email, callback phone, and home address must be present before readiness can be confirmed.");
        }

        var coverage = snapshot.CoverageOptions.SingleOrDefault(item =>
            string.Equals(item.CoverageToken, request.CoverageToken, StringComparison.Ordinal));
        if (coverage is null || !string.Equals(coverage.Fingerprint, request.CoverageFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_coverage_projection_stale",
                "The selected coverage record changed after it was loaded. Refresh before confirming it.");
        }
    }

    private static async Task<TelehealthCoverageGatewayInput?> LoadCoverageGatewayInputAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        string patientId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select r.practice_id, r.facility_id, r.request_id, r.patient_id,
                   location.state_code, r.complaint_category, r.status,
                   insurance.id, coalesce(insurance.type, ''), coalesce(insurance.provider, ''),
                   coalesce(insurance.plan_name, ''), coalesce(insurance.policy_number, ''),
                   coalesce(insurance.group_number, ''), coalesce(insurance.relationship, ''),
                   coalesce(insurance.subscriber_first_name, ''), coalesce(insurance.subscriber_last_name, ''),
                   insurance.subscriber_date_of_birth, selection.source_record_fingerprint
            from telehealth_requests r
            join lateral (
              select state_code from telehealth_patient_locations l
              where l.request_id=r.request_id order by l.attested_at desc limit 1
            ) location on true
            join lateral (
              select * from telehealth_coverage_selections s
              where s.request_id=r.request_id order by s.selected_at desc limit 1
            ) selection on true
            join insurance_records insurance
              on insurance.id=selection.insurance_record_id and insurance.patient_id=r.patient_id
            where r.request_id=@request_id and r.practice_id=@practice_id and r.patient_id=@patient_id
              and r.status in ('Verification','OperationalReview');
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("patient_id", patientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        var subscriberDateOfBirth = reader.IsDBNull(16)
            ? string.Empty
            : reader.GetFieldValue<DateOnly>(16).ToString("yyyy-MM-dd");
        var currentCoverageFingerprint = CreateCoverageFingerprint(
            reader.GetString(7),
            reader.GetString(3),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetString(12),
            reader.GetString(13),
            reader.GetString(14),
            reader.GetString(15),
            subscriberDateOfBirth);
        if (!string.Equals(currentCoverageFingerprint, reader.GetString(17), StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_coverage_projection_stale",
                "The selected coverage record changed after confirmation. Return to readiness and confirm current coverage.");
        }
        return new TelehealthCoverageGatewayInput(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            currentCoverageFingerprint);
    }

    private static async Task InsertPatientConfirmationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RequestRow current,
        CompleteTelehealthReadinessRequest request,
        int newVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_patient_confirmations(
              confirmation_id, request_id, patient_id, demographics_fingerprint,
              clinical_summary_fingerprint, demographics_confirmed, contact_confirmed,
              clinical_summary_confirmed, request_version, idempotency_key, command_fingerprint)
            values (@id, @request_id, @patient_id, @demographics_fingerprint,
                    @clinical_fingerprint, true, true, true, @version, @key, @fingerprint);
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("request_id", current.RequestId);
        command.Parameters.AddWithValue("patient_id", current.PatientId);
        command.Parameters.AddWithValue("demographics_fingerprint", request.DemographicsFingerprint);
        command.Parameters.AddWithValue("clinical_fingerprint", request.ClinicalSummaryFingerprint);
        command.Parameters.AddWithValue("version", newVersion);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertIntakeSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        string complaintSummary,
        string symptomDuration,
        int newVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_intake_snapshots(
              intake_id, request_id, complaint_summary, symptom_duration,
              synthetic_data_confirmed, request_version, idempotency_key, command_fingerprint)
            values (@id, @request_id, @summary, @duration, true, @version, @key, @fingerprint);
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("summary", complaintSummary);
        command.Parameters.AddWithValue("duration", symptomDuration);
        command.Parameters.AddWithValue("version", newVersion);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAcknowledgmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        CompleteTelehealthReadinessRequest request,
        int newVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_demonstration_acknowledgments(
              acknowledgment_id, request_id, acknowledgment_kind, package_key,
              package_version, content_hash, accepted, legal_effect, request_version,
              idempotency_key, command_fingerprint)
            values (@id, @request_id, @kind, @package_key, @package_version,
                    @content_hash, true, false, @version, @key, @fingerprint);
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("kind", SyntheticTelehealthAcknowledgment.Kind);
        command.Parameters.AddWithValue("package_key", request.AcknowledgmentPackageKey);
        command.Parameters.AddWithValue("package_version", request.AcknowledgmentPackageVersion);
        command.Parameters.AddWithValue("content_hash", request.AcknowledgmentContentHash);
        command.Parameters.AddWithValue("version", newVersion);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCoverageSelectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RequestRow current,
        CoverageSnapshot coverage,
        int newVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_coverage_selections(
              selection_id, request_id, patient_id, insurance_record_id,
              source_record_fingerprint, patient_confirmed, request_version,
              idempotency_key, command_fingerprint)
            values (@id, @request_id, @patient_id, @insurance_id,
                    @source_fingerprint, true, @version, @key, @fingerprint);
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("request_id", current.RequestId);
        command.Parameters.AddWithValue("patient_id", current.PatientId);
        command.Parameters.AddWithValue("insurance_id", coverage.CoverageRecordId);
        command.Parameters.AddWithValue("source_fingerprint", coverage.Fingerprint);
        command.Parameters.AddWithValue("version", newVersion);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCoverageVerificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        TelehealthCoverageGatewayResult result,
        int newVersion,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_coverage_verifications(
              verification_id, request_id, selection_id, adapter_mode,
              eligibility_status, network_status, financial_route,
              eligibility_source, network_source, evidence_key, evidence_version,
              input_fingerprint, limitations, verified_at, expires_at, request_version,
              idempotency_key, command_fingerprint)
            select @id, @request_id, selection_id, @adapter_mode,
                   @eligibility_status, @network_status, @financial_route,
                   @eligibility_source, @network_source, @evidence_key, @evidence_version,
                   @input_fingerprint, @limitations, now(), now() + interval '15 minutes',
                   @version, @key, @fingerprint
            from telehealth_coverage_selections
            where request_id=@request_id
            order by selected_at desc limit 1;
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("adapter_mode", result.AdapterMode);
        command.Parameters.AddWithValue("eligibility_status", result.EligibilityStatus.ToString());
        command.Parameters.AddWithValue("network_status", result.NetworkStatus.ToString());
        command.Parameters.AddWithValue("financial_route", result.FinancialRoute.ToString());
        command.Parameters.AddWithValue("eligibility_source", result.EligibilitySource);
        command.Parameters.AddWithValue("network_source", result.NetworkSource);
        command.Parameters.AddWithValue("evidence_key", result.EvidenceKey);
        command.Parameters.AddWithValue("evidence_version", result.EvidenceVersion);
        command.Parameters.AddWithValue("input_fingerprint", result.InputFingerprint);
        command.Parameters.AddWithValue("limitations", result.Limitations.ToArray());
        command.Parameters.AddWithValue("version", newVersion);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_coverage_selection_required",
                "A current patient-confirmed coverage selection is required before verification.");
        }
    }

    private static string MaskIdentifier(string value)
    {
        var normalized = value.Trim();
        return normalized.Length == 0
            ? "Not available"
            : $"••••{normalized[Math.Max(0, normalized.Length - 4)..]}";
    }

    private static string CreateCoverageFingerprint(
        string recordId,
        string patientId,
        string coverageType,
        string provider,
        string planName,
        string policyNumber,
        string groupNumber,
        string relationship,
        string subscriberFirstName,
        string subscriberLastName,
        string subscriberDateOfBirth) =>
        TelehealthCommandFingerprint.Create(
            recordId, patientId, coverageType, provider, planName, policyNumber,
            groupNumber, relationship, subscriberFirstName, subscriberLastName,
            subscriberDateOfBirth);

    private async Task<TelehealthRequestResponse> MutatePatientRequestAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        int expectedVersion,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyCollection<TelehealthRequestStatus> expectedStatuses,
        TelehealthRequestStatus nextStatus,
        string action,
        Func<NpgsqlConnection, NpgsqlTransaction, RequestRow, int, CancellationToken, Task> writeEvidence,
        string? triageOutcome,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await GetRequestForUpdateAsync(connection, transaction, requestId, cancellationToken);
        if (current is null || current.PracticeId != practiceId || current.PatientId != patientId)
        {
            throw TelehealthProblem.NotFound();
        }

        if (await IsReplayAsync(connection, transaction, requestId, idempotencyKey, fingerprint, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return await GetPatientRequestAsync(practiceId, patientId, requestId, cancellationToken)
                ?? throw TelehealthProblem.NotFound();
        }

        RequireVersion(current.Version, expectedVersion);
        if (!expectedStatuses.Contains(current.Status))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_invalid_transition",
                $"The request is {current.Status} and cannot perform this command.");
        }
        TelehealthRequestStateMachine.RequireTransition(current.Status, nextStatus);
        var newVersion = current.Version + 1;
        await writeEvidence(connection, transaction, current, newVersion, cancellationToken);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests
                set status=@status, triage_outcome=coalesce(@triage_outcome, triage_outcome),
                    version=@version, updated_at=now()
                where request_id=@request_id;
                """;
            update.Parameters.AddWithValue("status", nextStatus.ToString());
            update.Parameters.AddWithValue("triage_outcome", (object?)triageOutcome ?? DBNull.Value);
            update.Parameters.AddWithValue("version", newVersion);
            update.Parameters.AddWithValue("request_id", requestId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection, transaction, requestId, newVersion, action, current.Status.ToString(), nextStatus.ToString(),
            "patient", patientId, idempotencyKey, fingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPatientRequestAsync(practiceId, patientId, requestId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    private static void RequireVersion(int actual, int expected)
    {
        if (actual != expected)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_version_conflict",
                $"The request changed after it was loaded. Current version is {actual}.");
        }
    }

    private async Task<TelehealthRequestResponse?> GetPatientRequestAsync(
        string practiceId,
        string patientId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = CreateRequestProjectionCommand(connection);
        command.CommandText += " where r.request_id=@request_id and r.practice_id=@practice_id and r.patient_id=@patient_id;";
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("patient_id", patientId);
        return (await ReadRequestListAsync(command, cancellationToken)).SingleOrDefault();
    }

    private async Task<TelehealthRequestResponse?> GetStaffRequestAsync(
        string practiceId,
        int facilityId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = CreateRequestProjectionCommand(connection);
        command.CommandText += " where r.request_id=@request_id and r.practice_id=@practice_id and r.facility_id=@facility_id;";
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("facility_id", facilityId);
        return (await ReadRequestListAsync(command, cancellationToken)).SingleOrDefault();
    }

    private static NpgsqlCommand CreateRequestProjectionCommand(NpgsqlConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            select r.request_id, r.status, r.complaint_category, r.triage_outcome,
                   r.version, location.state_code, r.created_at, r.updated_at, r.ready_at,
                   coverage.adapter_mode, coverage.eligibility_status, coverage.network_status,
                   coverage.financial_route, coverage.limitations, coverage.verified_at, coverage.expires_at
            from telehealth_requests r
            left join lateral (
              select state_code from telehealth_patient_locations l
              where l.request_id=r.request_id order by l.attested_at desc limit 1
            ) location on true
            left join lateral (
              select adapter_mode, eligibility_status, network_status, financial_route,
                     limitations, verified_at, expires_at
              from telehealth_coverage_verifications verification
              where verification.request_id=r.request_id
              order by verification.verified_at desc, verification.verification_id desc limit 1
            ) coverage on true
            """;
        return command;
    }

    private static async Task<IReadOnlyList<TelehealthRequestResponse>> ReadRequestListAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<TelehealthRequestResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = Enum.Parse<TelehealthRequestStatus>(reader.GetString(1));
            items.Add(new TelehealthRequestResponse(
                reader.GetGuid(0),
                status.ToString(),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                checked((int)reader.GetInt64(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                AllowedActions(status),
                reader.IsDBNull(9)
                    ? null
                    : new TelehealthCoverageStatusResponse(
                        reader.GetString(9),
                        reader.GetString(10),
                        reader.GetString(11),
                        reader.GetString(12),
                        reader.GetFieldValue<string[]>(13),
                        reader.GetFieldValue<DateTimeOffset>(14),
                        reader.GetFieldValue<DateTimeOffset>(15))));
        }
        return items;
    }

    private static IReadOnlyList<string> AllowedActions(TelehealthRequestStatus status) => status switch
    {
        TelehealthRequestStatus.Draft => ["confirm-location"],
        TelehealthRequestStatus.LocationConfirmed => ["evaluate-triage"],
        TelehealthRequestStatus.SafetyScreening => ["complete-complaint-specific-triage"],
        TelehealthRequestStatus.ClinicalReview => ["await-clinical-review"],
        TelehealthRequestStatus.EmergencyRedirected => ["follow-emergency-guidance"],
        TelehealthRequestStatus.InPersonRecommended => ["follow-in-person-guidance"],
        TelehealthRequestStatus.Intake => ["complete-readiness"],
        TelehealthRequestStatus.Verification => ["verify-coverage"],
        TelehealthRequestStatus.OperationalReview => ["await-operational-review", "refresh-coverage"],
        TelehealthRequestStatus.Cancelled => ["request-cancelled"],
        TelehealthRequestStatus.Queued => ["await-clinician"],
        TelehealthRequestStatus.Reserved => ["clinician-reserved"],
        TelehealthRequestStatus.Redirected => ["follow-redirect-guidance"],
        _ => []
    };

    private static async Task<RequestRow?> GetRequestForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select request_id, practice_id, facility_id, patient_id, status,
                   complaint_category, triage_outcome, version, create_fingerprint
            from telehealth_requests where request_id=@request_id for update;
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRequestRow(reader) : null;
    }

    private static async Task<RequestRow?> GetByCreateIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        string patientId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select request_id, practice_id, facility_id, patient_id, status,
                   complaint_category, triage_outcome, version, create_fingerprint
            from telehealth_requests
            where practice_id=@practice_id and patient_id=@patient_id
              and create_idempotency_key=@idempotency_key for update;
            """;
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("patient_id", patientId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRequestRow(reader) : null;
    }

    private static RequestRow ReadRequestRow(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetInt32(2),
        reader.GetString(3),
        Enum.Parse<TelehealthRequestStatus>(reader.GetString(4)),
        reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        checked((int)reader.GetInt64(7)),
        reader.GetString(8));

    private static async Task<bool> IsReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select command_fingerprint from telehealth_request_events where request_id=@request_id and idempotency_key=@key;";
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        var existing = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (existing is null) return false;
        if (!string.Equals(existing, fingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict("telehealth_idempotency_conflict", "The idempotency key was reused with different command content.");
        }
        return true;
    }

    private static async Task<bool> HasCurrentConfirmedCoverageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        string patientId,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        if (!await IsLatestCoverageSelectionCurrentAsync(
            connection, transaction, requestId, patientId, cancellationToken))
        {
            return false;
        }
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
              select 1 from telehealth_coverage_verifications verification
              where verification.request_id=@request_id
                and verification.request_version=@request_version
                and verification.adapter_mode='NON_PRODUCTION'
                and verification.eligibility_status='Active'
                and verification.network_status='ConfirmedInNetwork'
                and verification.expires_at > now());
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("request_version", requestVersion);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> HasCurrentPatientReadinessEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        ReadinessSnapshot readiness,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select confirmation.demographics_fingerprint,
                   confirmation.clinical_summary_fingerprint,
                   acknowledgment.package_key, acknowledgment.package_version,
                   acknowledgment.content_hash, acknowledgment.accepted,
                   acknowledgment.legal_effect,
                   exists(select 1 from telehealth_intake_snapshots intake where intake.request_id=@request_id)
            from lateral (
              select * from telehealth_patient_confirmations confirmation
              where confirmation.request_id=@request_id
              order by confirmation.attested_at desc, confirmation.confirmation_id desc limit 1
            ) confirmation
            cross join lateral (
              select * from telehealth_demonstration_acknowledgments acknowledgment
              where acknowledgment.request_id=@request_id
              order by acknowledgment.accepted_at desc, acknowledgment.acknowledgment_id desc limit 1
            ) acknowledgment;
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            && string.Equals(reader.GetString(0), readiness.DemographicsFingerprint, StringComparison.Ordinal)
            && string.Equals(reader.GetString(1), readiness.ClinicalSummaryFingerprint, StringComparison.Ordinal)
            && string.Equals(reader.GetString(2), SyntheticTelehealthAcknowledgment.PackageKey, StringComparison.Ordinal)
            && reader.GetInt32(3) == SyntheticTelehealthAcknowledgment.PackageVersion
            && string.Equals(reader.GetString(4), SyntheticTelehealthAcknowledgment.ContentHash, StringComparison.Ordinal)
            && reader.GetBoolean(5)
            && !reader.GetBoolean(6)
            && reader.GetBoolean(7);
    }

    private static async Task<bool> IsLatestCoverageSelectionCurrentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select insurance.id, coalesce(insurance.type, ''), coalesce(insurance.provider, ''),
                   coalesce(insurance.plan_name, ''), coalesce(insurance.policy_number, ''),
                   coalesce(insurance.group_number, ''), coalesce(insurance.relationship, ''),
                   coalesce(insurance.subscriber_first_name, ''), coalesce(insurance.subscriber_last_name, ''),
                   insurance.subscriber_date_of_birth, selection.source_record_fingerprint
            from lateral (
              select * from telehealth_coverage_selections selection
              where selection.request_id=@request_id
              order by selection.selected_at desc, selection.selection_id desc limit 1
            ) selection
            join insurance_records insurance
              on insurance.id=selection.insurance_record_id and insurance.patient_id=selection.patient_id
            where selection.patient_id=@patient_id;
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("patient_id", patientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }
        var subscriberDateOfBirth = reader.IsDBNull(9)
            ? string.Empty
            : reader.GetFieldValue<DateOnly>(9).ToString("yyyy-MM-dd");
        var currentFingerprint = CreateCoverageFingerprint(
            reader.GetString(0), patientId, reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.GetString(7), reader.GetString(8), subscriberDateOfBirth);
        return string.Equals(currentFingerprint, reader.GetString(10), StringComparison.Ordinal);
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        int aggregateVersion,
        string action,
        string? fromStatus,
        string toStatus,
        string actorType,
        string actorId,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_request_events(
              event_id, request_id, aggregate_version, action, from_status, to_status,
              actor_type, actor_id, idempotency_key, command_fingerprint)
            values (@event_id, @request_id, @version, @action, @from_status, @to_status,
                    @actor_type, @actor_id, @idempotency_key, @fingerprint);
            """;
        command.Parameters.AddWithValue("event_id", Guid.NewGuid());
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("version", aggregateVersion);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("from_status", (object?)fromStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("to_status", toStatus);
        command.Parameters.AddWithValue("actor_type", actorType);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TelehealthOperationalReviewItem>> ListQueueProjectionAsync(
        string practiceId,
        int facilityId,
        string status,
        int? clinicianStaffId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select request_id, status, complaint_category, triage_outcome, version, created_at,
                   source_applicant_id is not null
            from telehealth_requests r
            where practice_id=@practice_id and facility_id=@facility_id and status=@status
              and (@clinician_filter_disabled or {ApplicantReservationCandidatePredicate})
            order by coalesce(ready_at, created_at), request_id;
            """;
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("facility_id", facilityId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("clinician_filter_disabled", clinicianStaffId is null);
        command.Parameters.AddWithValue("clinician", clinicianStaffId ?? 0);
        var items = new List<TelehealthOperationalReviewItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TelehealthOperationalReviewItem(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), checked((int)reader.GetInt64(4)),
                reader.GetFieldValue<DateTimeOffset>(5), reader.GetBoolean(6)));
        }
        return items;
    }

    private static async Task<bool> IsApplicantOriginatedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select source_applicant_id is not null from telehealth_requests where request_id=@request_id;
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<ShiftReplay?> FindShiftByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int clinicianStaffId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select shift_id, status, facility_id, clinician_staff_id, started_at, version, start_fingerprint
            from telehealth_clinician_shifts
            where practice_id=@practice_id and clinician_staff_id=@clinician and start_idempotency_key=@key
            for update;
            """;
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("clinician", clinicianStaffId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ShiftReplay(ReadShift(reader), reader.GetString(6))
            : null;
    }

    private static TelehealthShiftResponse ReadShift(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
        reader.GetFieldValue<DateTimeOffset>(4), checked((int)reader.GetInt64(5)));

    private static async Task<TelehealthShiftResponse?> GetActiveShiftForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        int clinicianStaffId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select shift_id, status, facility_id, clinician_staff_id, started_at, version
            from telehealth_clinician_shifts
            where practice_id=@practice_id and facility_id=@facility_id
              and clinician_staff_id=@clinician and status='Active'
            for update;
            """;
        command.Parameters.AddWithValue("practice_id", practiceId);
        command.Parameters.AddWithValue("facility_id", facilityId);
        command.Parameters.AddWithValue("clinician", clinicianStaffId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadShift(reader) : null;
    }

    private static async Task<ReservationReplay?> FindReservationByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int clinicianStaffId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select reservation.reservation_id, reservation.request_id, reservation.queue_entry_id,
                   reservation.shift_id, reservation.clinician_staff_id, reservation.reserved_at,
                   reservation.lease_expires_at, reservation.status, request.version,
                   request.source_applicant_id is not null,
                   reservation.command_fingerprint
            from telehealth_reservations reservation
            join telehealth_requests request on request.request_id=reservation.request_id
            where reservation.clinician_staff_id=@clinician and reservation.idempotency_key=@key
            for update of reservation;
            """;
        command.Parameters.AddWithValue("clinician", clinicianStaffId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ReservationReplay(
                ReadReservation(reader, checked((int)reader.GetInt64(8)), reader.GetBoolean(9)),
                reader.GetString(10))
            : null;
    }

    private static TelehealthReservationResponse ReadReservation(
        NpgsqlDataReader reader,
        int requestVersion,
        bool applicantOriginated) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetInt32(4),
        reader.GetFieldValue<DateTimeOffset>(5), reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7),
        requestVersion, applicantOriginated);

    private static async Task<bool> HasActiveReservationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int clinicianStaffId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from telehealth_reservations where clinician_staff_id=@clinician and status='Active');";
        command.Parameters.AddWithValue("clinician", clinicianStaffId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task ExpireReservationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var expired = new List<(Guid ReservationId, Guid QueueEntryId, Guid RequestId, int Version, string Status)>();
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                select reservation.reservation_id, reservation.queue_entry_id,
                       reservation.request_id, request.version, request.status
                from telehealth_reservations reservation
                join telehealth_requests request on request.request_id=reservation.request_id
                join telehealth_queue_entries queue on queue.queue_entry_id=reservation.queue_entry_id
                where reservation.status='Active' and reservation.lease_expires_at <= now()
                  and request.status in ('Reserved','Connecting')
                  and queue.practice_id=@practice_id and queue.facility_id=@facility_id
                for update of reservation, queue, request;
                """;
            select.Parameters.AddWithValue("practice_id", practiceId);
            select.Parameters.AddWithValue("facility_id", facilityId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                expired.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), checked((int)reader.GetInt64(3)), reader.GetString(4)));
            }
        }

        foreach (var item in expired)
        {
            var newVersion = item.Version + 1;
            await using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    update telehealth_reservations set status='Expired', version=version+1 where reservation_id=@reservation_id;
                    update telehealth_queue_entries set status='Ready', version=version+1, updated_at=now() where queue_entry_id=@queue_entry_id;
                    update telehealth_requests set status='Queued', version=@version, updated_at=now() where request_id=@request_id;
                    update appointments set provider_id=null,status='-',row_version=row_version+1
                    where id=(select appointment_id from telehealth_requests where request_id=@request_id)
                      and coalesce(status,'-') in ('-','@');
                    update telehealth_video_participant_grants set status='Expired'
                    where session_id in (select session_id from telehealth_video_sessions where request_id=@request_id)
                      and status='Issued';
                    update telehealth_video_sessions set status='Expired',version=version+1
                    where request_id=@request_id and status in ('Prepared','WaitingRoom');
                    """;
                update.Parameters.AddWithValue("reservation_id", item.ReservationId);
                update.Parameters.AddWithValue("queue_entry_id", item.QueueEntryId);
                update.Parameters.AddWithValue("request_id", item.RequestId);
                update.Parameters.AddWithValue("version", newVersion);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            var key = $"system-expire-{item.ReservationId:N}";
            await InsertEventAsync(
                connection, transaction, item.RequestId, newVersion, "reservation-expired",
                item.Status, "Queued", "system", "telehealth-lease-clock", key,
                TelehealthCommandFingerprint.Create(key), cancellationToken);
        }
    }

    private sealed record RequestRow(
        Guid RequestId,
        string PracticeId,
        int FacilityId,
        string PatientId,
        TelehealthRequestStatus Status,
        string ComplaintCategory,
        string? TriageOutcome,
        int Version,
        string CreateFingerprint);
    private sealed record CoverageSnapshot(
        string CoverageRecordId,
        string CoverageToken,
        string CoverageType,
        string Provider,
        string PlanName,
        string MaskedPolicyNumber,
        string MaskedGroupNumber,
        string SubscriberRelationship,
        string Fingerprint);
    private sealed record ReadinessSnapshot(
        Guid RequestId,
        int Version,
        TelehealthRequestStatus Status,
        string ComplaintCategory,
        string FirstName,
        string LastName,
        string DateOfBirth,
        string? Email,
        string? Phone,
        string Address,
        string DemographicsFingerprint,
        int ActiveMedicationCount,
        int ActiveAllergyCount,
        bool HistoryAvailable,
        string ClinicalSummaryFingerprint,
        IReadOnlyList<CoverageSnapshot> CoverageOptions);
    private sealed record QueueCandidate(
        Guid QueueEntryId,
        Guid RequestId,
        int RequestVersion,
        bool ApplicantOriginated);
    private sealed record ShiftReplay(TelehealthShiftResponse Response, string Fingerprint);
    private sealed record ReservationReplay(TelehealthReservationResponse Response, string Fingerprint);
}
