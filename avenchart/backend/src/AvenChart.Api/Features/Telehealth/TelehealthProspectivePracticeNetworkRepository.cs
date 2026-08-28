// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectivePracticeNetworkRecord(
    Guid NetworkDeterminationId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string CurrentLocationStateCode,
    string PurposeCategory,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string PracticeDisplayName,
    DateOnly DateOfService,
    string ServiceCategory,
    string EligibilityStatus,
    string BenefitInformationStatus,
    string EligibilityBusinessOutcome,
    DateTimeOffset EligibilityCheckedAt,
    DateTimeOffset EligibilityExpiresAt,
    TelehealthProspectivePracticeNetworkAdapterResult AdapterResult,
    DateTimeOffset RecordedAt);

internal sealed record TelehealthProspectivePracticeNetworkCandidate(
    Guid IdentityReviewDecisionId,
    Guid SafetyEvaluationId,
    Guid PurposeId,
    Guid PrecheckId,
    Guid DetailsId,
    Guid EligibilityResultId,
    string CurrentLocationStateCode,
    string PurposeCategory,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string PracticeNetworkPrecheckStatus,
    DateOnly DateOfService,
    string ServiceCategory,
    string EligibilityStatus,
    string BenefitInformationStatus,
    string EligibilityBusinessOutcome,
    DateTimeOffset EligibilityCheckedAt,
    DateTimeOffset EligibilityExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthProspectivePracticeNetworkContext(
    int Version,
    string Status,
    string AccessKeyHash,
    string? DuplicateDisposition,
    DateTimeOffset? ContactVerifiedAt,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid IdentityReviewDecisionId,
    Guid SafetyEvaluationId,
    Guid PurposeId,
    Guid PrecheckId,
    Guid DetailsId,
    Guid EligibilityResultId,
    int EligibilityResultingVersion,
    string EligibilityResultingStatus,
    string CurrentLocationStateCode,
    string PurposeCategory,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string PracticeNetworkPrecheckStatus,
    DateOnly DateOfService,
    string ServiceCategory,
    string EligibilityTransportOutcome,
    string MemberMatchStatus,
    string EligibilityStatus,
    string BenefitInformationStatus,
    string EligibilityBusinessOutcome,
    bool MemberEligibilityChecked,
    DateTimeOffset EligibilityCheckedAt,
    DateTimeOffset EligibilityExpiresAt);

public sealed class TelehealthProspectivePracticeNetworkRepository(NpgsqlDataSource dataSource)
{
    internal async Task<TelehealthProspectivePracticeNetworkRecord> RecordAsync(
        string practiceId,
        int facilityId,
        string practiceDisplayName,
        Guid applicantId,
        string accessKeyHash,
        int expectedVersion,
        string idempotencyKey,
        string commandFingerprint,
        Func<TelehealthProspectivePracticeNetworkCandidate, CancellationToken,
            ValueTask<TelehealthProspectivePracticeNetworkAdapterResult>> resolveNetwork,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await LoadContextAsync(
            connection, transaction, practiceId, facilityId, applicantId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);

        var replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId,
            idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligibleContext(context);
        if (context.Version != expectedVersion)
        {
            throw VersionConflict();
        }

        var candidate = new TelehealthProspectivePracticeNetworkCandidate(
            context.IdentityReviewDecisionId,
            context.SafetyEvaluationId,
            context.PurposeId,
            context.PrecheckId,
            context.DetailsId,
            context.EligibilityResultId,
            context.CurrentLocationStateCode,
            context.PurposeCategory,
            context.PlanKey,
            context.PayerDisplayName,
            context.ProductDisplayName,
            context.PracticeNetworkPrecheckStatus,
            context.DateOfService,
            context.ServiceCategory,
            context.EligibilityStatus,
            context.BenefitInformationStatus,
            context.EligibilityBusinessOutcome,
            context.EligibilityCheckedAt,
            context.EligibilityExpiresAt,
            context.DatabaseNow);
        var adapterResult = await resolveNetwork(candidate, cancellationToken);

        const string nextStatus = "SyntheticPracticeNetworkRecorded";
        var nextVersion = context.Version + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@nextStatus,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticEligibilityRecorded';
                """;
            update.Parameters.AddWithValue("nextStatus", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw VersionConflict();
            }
        }

        var determinationId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_practice_network_determinations(
                  network_determination_id,applicant_id,practice_id,facility_id,
                  identity_review_decision_id,safety_triage_evaluation_id,
                  visit_purpose_id,practice_network_precheck_id,
                  member_insurance_details_id,eligibility_result_id,
                  resulting_applicant_version,resulting_applicant_status,
                  location_state_code,purpose_category,plan_key,payer_display_name,
                  product_display_name,practice_network_precheck_status,
                  practice_display_name,date_of_service,service_category,
                  eligibility_status,benefit_information_status,
                  eligibility_business_outcome,eligibility_checked_at,
                  eligibility_expires_at,adapter_mode,compatibility_target,
                  dataset_key,dataset_version,dataset_effective_from,
                  dataset_effective_through,source_last_updated_at,
                  request_trace_token,response_trace_token,transport_outcome,
                  plan_network_match_status,practice_affiliation_status,
                  service_availability_status,new_patient_acceptance_status,
                  business_outcome,practice_network_checked,practice_in_network,
                  new_patients_accepted,network_reference,organization_reference,
                  location_reference,service_reference,checked_at,expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @determinationId,@applicantId,@practiceId,@facilityId,
                  @identityDecisionId,@safetyEvaluationId,@purposeId,@precheckId,
                  @detailsId,@eligibilityResultId,@nextVersion,@nextStatus,
                  @locationStateCode,@purposeCategory,@planKey,@payerDisplayName,
                  @productDisplayName,@precheckStatus,@practiceDisplayName,
                  @dateOfService,@serviceCategory,@eligibilityStatus,
                  @benefitInformationStatus,@eligibilityBusinessOutcome,
                  @eligibilityCheckedAt,@eligibilityExpiresAt,@adapterMode,
                  @compatibilityTarget,@datasetKey,@datasetVersion,
                  @datasetEffectiveFrom,@datasetEffectiveThrough,
                  @sourceLastUpdatedAt,@requestTraceToken,@responseTraceToken,
                  @transportOutcome,@planNetworkMatchStatus,
                  @practiceAffiliationStatus,@serviceAvailabilityStatus,
                  @newPatientAcceptanceStatus,@businessOutcome,
                  @practiceNetworkChecked,@practiceInNetwork,@newPatientsAccepted,
                  @networkReference,@organizationReference,@locationReference,
                  @serviceReference,@checkedAt,@expiresAt,@idempotencyKey,
                  @commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("determinationId", determinationId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("identityDecisionId", candidate.IdentityReviewDecisionId);
            insert.Parameters.AddWithValue("safetyEvaluationId", candidate.SafetyEvaluationId);
            insert.Parameters.AddWithValue("purposeId", candidate.PurposeId);
            insert.Parameters.AddWithValue("precheckId", candidate.PrecheckId);
            insert.Parameters.AddWithValue("detailsId", candidate.DetailsId);
            insert.Parameters.AddWithValue("eligibilityResultId", candidate.EligibilityResultId);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("locationStateCode", candidate.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("purposeCategory", candidate.PurposeCategory);
            insert.Parameters.AddWithValue("planKey", candidate.PlanKey);
            insert.Parameters.AddWithValue("payerDisplayName", candidate.PayerDisplayName);
            insert.Parameters.AddWithValue("productDisplayName", candidate.ProductDisplayName);
            insert.Parameters.AddWithValue("precheckStatus", candidate.PracticeNetworkPrecheckStatus);
            insert.Parameters.AddWithValue("practiceDisplayName", practiceDisplayName);
            insert.Parameters.AddWithValue("dateOfService", candidate.DateOfService);
            insert.Parameters.AddWithValue("serviceCategory", candidate.ServiceCategory);
            insert.Parameters.AddWithValue("eligibilityStatus", candidate.EligibilityStatus);
            insert.Parameters.AddWithValue("benefitInformationStatus", candidate.BenefitInformationStatus);
            insert.Parameters.AddWithValue("eligibilityBusinessOutcome", candidate.EligibilityBusinessOutcome);
            insert.Parameters.AddWithValue("eligibilityCheckedAt", candidate.EligibilityCheckedAt);
            insert.Parameters.AddWithValue("eligibilityExpiresAt", candidate.EligibilityExpiresAt);
            insert.Parameters.AddWithValue("adapterMode", adapterResult.AdapterMode);
            insert.Parameters.AddWithValue("compatibilityTarget", adapterResult.CompatibilityTarget);
            insert.Parameters.AddWithValue("datasetKey", adapterResult.DatasetKey);
            insert.Parameters.AddWithValue("datasetVersion", adapterResult.DatasetVersion);
            insert.Parameters.AddWithValue("datasetEffectiveFrom", adapterResult.DatasetEffectiveFrom);
            insert.Parameters.AddWithValue("datasetEffectiveThrough", adapterResult.DatasetEffectiveThrough);
            insert.Parameters.AddWithValue("sourceLastUpdatedAt", adapterResult.SourceLastUpdatedAt);
            insert.Parameters.AddWithValue("requestTraceToken", adapterResult.RequestTraceToken);
            insert.Parameters.AddWithValue("responseTraceToken", adapterResult.ResponseTraceToken);
            insert.Parameters.AddWithValue("transportOutcome", adapterResult.TransportOutcome);
            insert.Parameters.AddWithValue("planNetworkMatchStatus", adapterResult.PlanNetworkMatchStatus);
            insert.Parameters.AddWithValue("practiceAffiliationStatus", adapterResult.PracticeAffiliationStatus);
            insert.Parameters.AddWithValue("serviceAvailabilityStatus", adapterResult.ServiceAvailabilityStatus);
            insert.Parameters.AddWithValue("newPatientAcceptanceStatus", adapterResult.NewPatientAcceptanceStatus);
            insert.Parameters.AddWithValue("businessOutcome", adapterResult.BusinessOutcome);
            insert.Parameters.AddWithValue("practiceNetworkChecked", adapterResult.PracticeNetworkChecked);
            insert.Parameters.AddWithValue("practiceInNetwork", adapterResult.PracticeInNetwork);
            insert.Parameters.AddWithValue("newPatientsAccepted", adapterResult.NewPatientsAccepted);
            insert.Parameters.AddWithValue("networkReference", (object?)adapterResult.NetworkReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("organizationReference", (object?)adapterResult.OrganizationReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("locationReference", (object?)adapterResult.LocationReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("serviceReference", (object?)adapterResult.ServiceReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("checkedAt", adapterResult.CheckedAt);
            insert.Parameters.AddWithValue("expiresAt", adapterResult.ExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Practice-network determination time is unavailable.");
            }
            recordedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-synthetic-practice-network-recorded',
                       'SyntheticEligibilityRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "prospective-practice-network:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            determinationId,
            applicantId,
            nextVersion,
            nextStatus,
            candidate.CurrentLocationStateCode,
            candidate.PurposeCategory,
            candidate.PlanKey,
            candidate.PayerDisplayName,
            candidate.ProductDisplayName,
            practiceDisplayName,
            candidate.DateOfService,
            candidate.ServiceCategory,
            candidate.EligibilityStatus,
            candidate.BenefitInformationStatus,
            candidate.EligibilityBusinessOutcome,
            candidate.EligibilityCheckedAt,
            candidate.EligibilityExpiresAt,
            adapterResult,
            recordedAt);
    }

    private static async Task<TelehealthProspectivePracticeNetworkContext?> LoadContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select a.version,a.status,a.access_key_hash,a.duplicate_disposition,
                   a.contact_verified_at,a.expires_at,now(),
                   e.identity_review_decision_id,e.safety_triage_evaluation_id,
                   e.visit_purpose_id,e.practice_network_precheck_id,
                   e.member_insurance_details_id,e.eligibility_result_id,
                   e.resulting_applicant_version,e.resulting_applicant_status,
                   e.location_state_code,e.purpose_category,e.plan_key,
                   e.payer_display_name,e.product_display_name,
                   e.practice_network_status,e.date_of_service,e.service_category,
                   e.transport_outcome,e.member_match_status,e.eligibility_status,
                   e.benefit_information_status,e.business_outcome,
                   e.member_eligibility_checked,e.checked_at,e.expires_at
            from telehealth_prospective_applicants a
            join telehealth_applicant_eligibility_results e
              on e.applicant_id=a.applicant_id
            join telehealth_applicant_identity_review_decisions d
              on d.decision_id=e.identity_review_decision_id
             and d.applicant_id=a.applicant_id
            join telehealth_applicant_safety_triage_evaluations s
              on s.evaluation_id=e.safety_triage_evaluation_id
             and s.applicant_id=a.applicant_id
            join telehealth_applicant_visit_purposes p
              on p.purpose_id=e.visit_purpose_id and p.applicant_id=a.applicant_id
            join telehealth_applicant_practice_network_prechecks n
              on n.precheck_id=e.practice_network_precheck_id
             and n.applicant_id=a.applicant_id
            join telehealth_applicant_member_insurance_details m
              on m.details_id=e.member_insurance_details_id
             and m.applicant_id=a.applicant_id
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.applicant_id=@applicantId
            for update of a;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new(
            Convert.ToInt32(reader.GetInt64(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetGuid(7),
            reader.GetGuid(8),
            reader.GetGuid(9),
            reader.GetGuid(10),
            reader.GetGuid(11),
            reader.GetGuid(12),
            Convert.ToInt32(reader.GetInt64(13)),
            reader.GetString(14),
            reader.GetString(15),
            reader.GetString(16),
            reader.GetString(17),
            reader.GetString(18),
            reader.GetString(19),
            reader.GetString(20),
            reader.GetFieldValue<DateOnly>(21),
            reader.GetString(22),
            reader.GetString(23),
            reader.GetString(24),
            reader.GetString(25),
            reader.GetString(26),
            reader.GetString(27),
            reader.GetBoolean(28),
            reader.GetFieldValue<DateTimeOffset>(29),
            reader.GetFieldValue<DateTimeOffset>(30));
    }

    private static async Task<(TelehealthProspectivePracticeNetworkRecord Record,
        string CommandFingerprint)?> LoadByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select network_determination_id,applicant_id,
                   resulting_applicant_version,resulting_applicant_status,
                   location_state_code,purpose_category,plan_key,payer_display_name,
                   product_display_name,practice_display_name,date_of_service,
                   service_category,eligibility_status,benefit_information_status,
                   eligibility_business_outcome,eligibility_checked_at,
                   eligibility_expires_at,adapter_mode,compatibility_target,
                   dataset_key,dataset_version,dataset_effective_from,
                   dataset_effective_through,source_last_updated_at,
                   request_trace_token,response_trace_token,transport_outcome,
                   plan_network_match_status,practice_affiliation_status,
                   service_availability_status,new_patient_acceptance_status,
                   business_outcome,practice_network_checked,practice_in_network,
                   new_patients_accepted,network_reference,organization_reference,
                   location_reference,service_reference,checked_at,expires_at,
                   recorded_at,command_fingerprint
            from telehealth_applicant_practice_network_determinations
            where practice_id=@practiceId and facility_id=@facilityId
              and applicant_id=@applicantId and idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var adapter = new TelehealthProspectivePracticeNetworkAdapterResult(
            reader.GetString(17),
            reader.GetString(18),
            reader.GetString(19),
            reader.GetInt32(20),
            reader.GetFieldValue<DateTimeOffset>(21),
            reader.GetFieldValue<DateTimeOffset>(22),
            reader.GetFieldValue<DateTimeOffset>(23),
            reader.GetGuid(24),
            reader.GetGuid(25),
            reader.GetString(26),
            reader.GetString(27),
            reader.GetString(28),
            reader.GetString(29),
            reader.GetString(30),
            reader.GetString(31),
            reader.GetBoolean(32),
            reader.GetBoolean(33),
            reader.GetBoolean(34),
            reader.IsDBNull(35) ? null : reader.GetString(35),
            reader.IsDBNull(36) ? null : reader.GetString(36),
            reader.IsDBNull(37) ? null : reader.GetString(37),
            reader.IsDBNull(38) ? null : reader.GetString(38),
            reader.GetFieldValue<DateTimeOffset>(39),
            reader.GetFieldValue<DateTimeOffset>(40));
        return (new(
                reader.GetGuid(0),
                reader.GetGuid(1),
                Convert.ToInt32(reader.GetInt64(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetFieldValue<DateOnly>(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.GetString(14),
                reader.GetFieldValue<DateTimeOffset>(15),
                reader.GetFieldValue<DateTimeOffset>(16),
                adapter,
                reader.GetFieldValue<DateTimeOffset>(41)),
            reader.GetString(42));
    }

    private static void RequireEligibleContext(TelehealthProspectivePracticeNetworkContext context)
    {
        if (context.ApplicantExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired before the practice-network check. Start again.");
        }
        if (context.EligibilityExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_eligibility_expired",
                "The synthetic eligibility evidence expired before the practice-network check. Start again with a new synthetic applicant.");
        }
        if (context.Status != "SyntheticEligibilityRecorded"
            || context.Version != context.EligibilityResultingVersion
            || context.EligibilityResultingStatus != "SyntheticEligibilityRecorded"
            || context.DuplicateDisposition != "NoCandidate"
            || context.ContactVerifiedAt is null
            || context.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || context.PurposeCategory is not ("migraine" or "sleep")
            || context.PlanKey is not ("harbor-mutual-hd" or "blue-valley-standard" or "pine-state-choice")
            || context.PracticeNetworkPrecheckStatus is not ("PracticeNetworkConfirmedFixture" or "NetworkUnknown" or "PracticeOutOfNetworkFixture")
            || context.ServiceCategory != SyntheticTelehealthProspectiveEligibilityGateway.ServiceCategory
            || context.DateOfService != DateOnly.FromDateTime(context.EligibilityCheckedAt.UtcDateTime)
            || context.EligibilityTransportOutcome is not ("SimulatedAccepted" or "SimulatedUnavailable")
            || context.MemberMatchStatus is not ("Matched" or "NotMatched" or "Unknown")
            || context.EligibilityStatus is not ("Active" or "Inactive" or "Unknown")
            || context.BenefitInformationStatus is not ("Reported" or "NotReported" or "Unknown")
            || context.EligibilityBusinessOutcome is not ("EligibleBenefitsReported" or "CoverageInactive" or "SubscriberNotFound" or "UnableToDetermine")
            || context.EligibilityCheckedAt > context.DatabaseNow
            || context.EligibilityExpiresAt > context.EligibilityCheckedAt.AddMinutes(15))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_network_state_conflict",
                "The applicant is not eligible for this bounded synthetic practice-network determination.");
        }
    }

    private static void RequireAccess(string existingHash, string suppliedHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(existingHash, suppliedHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireReplayFingerprint(string existing, string supplied)
    {
        if (!string.Equals(existing, supplied, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_network_idempotency_conflict",
                "The practice-network idempotency key was already used with different command content.");
        }
    }

    private static TelehealthProblem VersionConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_version_conflict",
        "The applicant changed. Reload the synthetic applicant before retrying.");
}
