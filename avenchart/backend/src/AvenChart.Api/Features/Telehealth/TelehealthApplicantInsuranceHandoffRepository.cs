// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantInsuranceHandoffContext(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string AccessKeyHash,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    int ApplicantFacilityId,
    Guid? PromotionId,
    string? PromotionOutcome,
    bool? CanonicalPatientCreated,
    string? CanonicalPatientId,
    bool? PatientPortalEnabled,
    int? PatientFacilityId,
    string? MergedIntoPatientId,
    Guid? RegistrationDetailsConfirmationId,
    Guid? MemberInsuranceDetailsId,
    string? PayerDisplayName,
    string? ProductDisplayName,
    string? MemberIdLast4,
    bool? GroupNumberPresent,
    string? GroupNumberLast4,
    string? SubscriberRelationship,
    string? CoveragePriority,
    Guid? EligibilityResultId,
    string? EligibilityBusinessOutcome,
    DateTimeOffset? EligibilityCheckedAt,
    DateTimeOffset? EligibilityExpiresAt,
    Guid? PracticeNetworkDeterminationId,
    string? PracticeNetworkBusinessOutcome,
    DateTimeOffset? PracticeNetworkCheckedAt,
    DateTimeOffset? PracticeNetworkExpiresAt,
    bool? RenderingPhysicianNetworkChecked,
    bool SourceProvenanceValid,
    long CanonicalInsuranceRecordCount,
    Guid? ConfirmationId,
    DateTimeOffset? ConfirmedAt);

public sealed record TelehealthApplicantInsuranceHandoffConfirmationRecord(
    Guid ConfirmationId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string InsuranceSnapshotFingerprint,
    DateTimeOffset ConfirmedAt);

public sealed class TelehealthApplicantInsuranceHandoffRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select
          a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now(),a.facility_id,
          promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
          promotion.canonical_patient_id,patient.portal_enabled,patient.facility_id,
          patient.merged_into_patient_id,
          registration.confirmation_id,
          member.details_id,member.payer_display_name,member.product_display_name,
          member.member_id_last4,member.group_number_present,member.group_number_last4,
          member.subscriber_relationship,member.coverage_priority,
          eligibility.eligibility_result_id,eligibility.business_outcome,
          eligibility.checked_at,eligibility.expires_at,
          network.network_determination_id,network.business_outcome,
          network.checked_at,network.expires_at,network.rendering_physician_network_checked,
          coalesce(
            promotion.outcome='SyntheticPatientCreated'
            and promotion.canonical_patient_created
            and promotion.practice_id=a.practice_id
            and promotion.facility_id=a.facility_id
            and patient.canonical_id=promotion.canonical_patient_id
            and patient.facility_id=a.facility_id
            and not patient.portal_enabled
            and patient.merged_into_patient_id is null
            and patient.first_name=a.legal_first_name
            and patient.last_name=a.legal_last_name
            and patient.date_of_birth=a.date_of_birth
            and patient.email=a.email
            and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
            and patient.state=a.residence_state_code
            and patient.postal_code=a.postal_code
            and registration.practice_id=a.practice_id
            and registration.facility_id=a.facility_id
            and registration.promotion_id=promotion.promotion_id
            and registration.canonical_patient_id=promotion.canonical_patient_id
            and registration.resulting_applicant_status='SyntheticMinimumRegistrationDetailsConfirmed'
            and member.practice_id=a.practice_id
            and member.facility_id=a.facility_id
            and member.resulting_applicant_status='MemberInsuranceDetailsRecorded'
            and member.details_confirmed and member.synthetic_data_confirmed
            and eligibility.practice_id=a.practice_id
            and eligibility.facility_id=a.facility_id
            and eligibility.member_insurance_details_id=member.details_id
            and eligibility.resulting_applicant_status='SyntheticEligibilityRecorded'
            and eligibility.plan_key=member.plan_key
            and eligibility.payer_display_name=member.payer_display_name
            and eligibility.product_display_name=member.product_display_name
            and eligibility.member_id_last4=member.member_id_last4
            and eligibility.group_number_present=member.group_number_present
            and eligibility.group_number_last4 is not distinct from member.group_number_last4
            and eligibility.subscriber_relationship=member.subscriber_relationship
            and eligibility.coverage_priority=member.coverage_priority
            and eligibility.business_outcome='EligibleBenefitsReported'
            and eligibility.member_matched
            and eligibility.member_eligibility_checked
            and eligibility.member_benefits_checked
            and not eligibility.coverage_verified
            and not eligibility.exact_network_confirmed
            and network.practice_id=a.practice_id
            and network.facility_id=a.facility_id
            and network.member_insurance_details_id=member.details_id
            and network.eligibility_result_id=eligibility.eligibility_result_id
            and network.resulting_applicant_status='SyntheticPracticeNetworkRecorded'
            and network.plan_key=member.plan_key
            and network.payer_display_name=member.payer_display_name
            and network.product_display_name=member.product_display_name
            and network.eligibility_business_outcome=eligibility.business_outcome
            and network.eligibility_checked_at=eligibility.checked_at
            and network.eligibility_expires_at=eligibility.expires_at
            and network.business_outcome='PracticeInNetworkAcceptingNewPatients'
            and network.practice_network_checked
            and network.practice_in_network
            and network.new_patients_accepted
            and not network.rendering_physician_network_checked
            and not network.exact_network_confirmed
            and not network.coverage_verified,
            false) as source_provenance_valid,
          (select count(*) from insurance_records insurance
             where lower(insurance.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_insurance_record_count,
          confirmation.confirmation_id,confirmation.confirmed_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.applicant_id=a.applicant_id
        left join patients patient
          on patient.canonical_id=promotion.canonical_patient_id
        left join telehealth_applicant_registration_details_confirmations registration
          on registration.applicant_id=a.applicant_id
        left join telehealth_applicant_member_insurance_details member
          on member.applicant_id=a.applicant_id
        left join telehealth_applicant_eligibility_results eligibility
          on eligibility.applicant_id=a.applicant_id
        left join telehealth_applicant_practice_network_determinations network
          on network.applicant_id=a.applicant_id
        left join telehealth_applicant_insurance_handoff_confirmations confirmation
          on confirmation.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantInsuranceHandoffContext> GetAuthorizedAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var context = await LoadAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);
        RequireEligible(context, facilityId, allowConfirmed: true);
        return context;
    }

    public async Task<TelehealthApplicantInsuranceHandoffConfirmationRecord> ConfirmAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantInsuranceHandoffConfirmation confirmation,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await LoadAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
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

        RequireEligible(context, facilityId, allowConfirmed: false);
        if (context.ApplicantVersion != confirmation.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_insurance_handoff_version_conflict",
                "The applicant changed. Reload the insurance handoff before retrying.");
        }
        var snapshot = Snapshot(context);
        if (!string.Equals(
                snapshot.Fingerprint,
                confirmation.InsuranceSnapshotFingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_insurance_handoff_snapshot_conflict",
                "The insurance handoff changed. Reload it before confirming.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticMinimumRegistrationDetailsConfirmed';
                """;
            update.Parameters.AddWithValue("status", TelehealthApplicantInsuranceHandoffPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", confirmation.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_insurance_handoff_version_conflict",
                    "The applicant changed. Reload the insurance handoff before retrying.");
            }
        }

        var confirmationId = Guid.NewGuid();
        DateTimeOffset confirmedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_insurance_handoff_confirmations(
                  confirmation_id,applicant_id,practice_id,facility_id,
                  registration_details_confirmation_id,promotion_id,canonical_patient_id,
                  member_insurance_details_id,eligibility_result_id,network_determination_id,
                  resulting_applicant_version,resulting_applicant_status,
                  insurance_snapshot_fingerprint,payer_display_name,product_display_name,
                  member_id_last4,group_number_present,group_number_last4,
                  subscriber_relationship,coverage_priority,
                  eligibility_business_outcome,eligibility_checked_at,eligibility_expires_at,
                  practice_network_business_outcome,practice_network_checked_at,
                  practice_network_expires_at,rendering_physician_network_checked,
                  payer_product_confirmed,masked_member_details_confirmed,
                  subscriber_relationship_confirmed,evidence_limitations_acknowledged,
                  synthetic_data_confirmed,policy_key,policy_version,evidence_type,
                  applicant_expires_at,idempotency_key,command_fingerprint)
                values(
                  @confirmationId,@applicantId,@practiceId,@facilityId,
                  @registrationId,@promotionId,@patientId,
                  @memberDetailsId,@eligibilityId,@networkId,
                  @nextVersion,@nextStatus,@snapshotFingerprint,@payer,@product,
                  @memberLast4,@groupPresent,@groupLast4,@relationship,@priority,
                  @eligibilityOutcome,@eligibilityCheckedAt,@eligibilityExpiresAt,
                  @networkOutcome,@networkCheckedAt,@networkExpiresAt,false,
                  true,true,true,true,true,@policyKey,@policyVersion,@evidenceType,
                  @applicantExpiresAt,@idempotencyKey,@commandFingerprint)
                returning confirmed_at;
                """;
            insert.Parameters.AddWithValue("confirmationId", confirmationId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("registrationId", context.RegistrationDetailsConfirmationId!.Value);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId!.Value);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("memberDetailsId", context.MemberInsuranceDetailsId!.Value);
            insert.Parameters.AddWithValue("eligibilityId", context.EligibilityResultId!.Value);
            insert.Parameters.AddWithValue("networkId", context.PracticeNetworkDeterminationId!.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", TelehealthApplicantInsuranceHandoffPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("payer", snapshot.PayerDisplayName);
            insert.Parameters.AddWithValue("product", snapshot.ProductDisplayName);
            insert.Parameters.AddWithValue("memberLast4", context.MemberIdLast4!);
            insert.Parameters.AddWithValue("groupPresent", context.GroupNumberPresent!.Value);
            insert.Parameters.AddWithValue("groupLast4", (object?)context.GroupNumberLast4 ?? DBNull.Value);
            insert.Parameters.AddWithValue("relationship", snapshot.SubscriberRelationship);
            insert.Parameters.AddWithValue("priority", snapshot.CoveragePriority);
            insert.Parameters.AddWithValue("eligibilityOutcome", snapshot.EligibilityBusinessOutcome);
            insert.Parameters.AddWithValue("eligibilityCheckedAt", snapshot.EligibilityCheckedAt);
            insert.Parameters.AddWithValue("eligibilityExpiresAt", snapshot.EligibilityExpiresAt);
            insert.Parameters.AddWithValue("networkOutcome", snapshot.PracticeNetworkBusinessOutcome);
            insert.Parameters.AddWithValue("networkCheckedAt", snapshot.PracticeNetworkCheckedAt);
            insert.Parameters.AddWithValue("networkExpiresAt", snapshot.PracticeNetworkExpiresAt);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantInsuranceHandoffPolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantInsuranceHandoffPolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantInsuranceHandoffPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic insurance handoff confirmation time is unavailable.");
            }
            confirmedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-insurance-handoff-confirmed',
                       'SyntheticMinimumRegistrationDetailsConfirmed',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", TelehealthApplicantInsuranceHandoffPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "insurance-handoff-confirmation:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            confirmationId,
            applicantId,
            nextVersion,
            TelehealthApplicantInsuranceHandoffPolicy.ResultingStatus,
            snapshot.Fingerprint,
            confirmedAt);
    }

    public static TelehealthApplicantInsuranceHandoffSnapshot Snapshot(
        TelehealthApplicantInsuranceHandoffContext context) =>
        TelehealthApplicantInsuranceHandoffPolicy.Snapshot(
            context.MemberInsuranceDetailsId!.Value,
            context.EligibilityResultId!.Value,
            context.PracticeNetworkDeterminationId!.Value,
            context.PayerDisplayName!,
            context.ProductDisplayName!,
            context.MemberIdLast4!,
            context.GroupNumberLast4,
            context.SubscriberRelationship!,
            context.CoveragePriority!,
            context.EligibilityBusinessOutcome!,
            context.EligibilityCheckedAt!.Value,
            context.EligibilityExpiresAt!.Value,
            context.PracticeNetworkBusinessOutcome!,
            context.PracticeNetworkCheckedAt!.Value,
            context.PracticeNetworkExpiresAt!.Value,
            context.RenderingPhysicianNetworkChecked!.Value);

    private static async Task<TelehealthApplicantInsuranceHandoffContext?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ContextProjection + "\n" + """
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.applicant_id=@applicantId
            """ + (forUpdate ? "\nfor update of a;" : ";");
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new(
            reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
            reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5), reader.GetInt32(6),
            NullableGuid(reader, 7), NullableString(reader, 8), NullableBoolean(reader, 9),
            NullableString(reader, 10), NullableBoolean(reader, 11), NullableInt32(reader, 12),
            NullableString(reader, 13), NullableGuid(reader, 14), NullableGuid(reader, 15),
            NullableString(reader, 16), NullableString(reader, 17), NullableString(reader, 18),
            NullableBoolean(reader, 19), NullableString(reader, 20), NullableString(reader, 21),
            NullableString(reader, 22), NullableGuid(reader, 23), NullableString(reader, 24),
            NullableDateTimeOffset(reader, 25), NullableDateTimeOffset(reader, 26),
            NullableGuid(reader, 27), NullableString(reader, 28),
            NullableDateTimeOffset(reader, 29), NullableDateTimeOffset(reader, 30),
            NullableBoolean(reader, 31), reader.GetBoolean(32), reader.GetInt64(33),
            NullableGuid(reader, 34), NullableDateTimeOffset(reader, 35));
    }

    private static async Task<(TelehealthApplicantInsuranceHandoffConfirmationRecord Record,
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
            select confirmation_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,insurance_snapshot_fingerprint,
                   confirmed_at,command_fingerprint
            from telehealth_applicant_insurance_handoff_confirmations
            where practice_id=@practiceId and facility_id=@facilityId
              and applicant_id=@applicantId and idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return (new(
            reader.GetGuid(0), reader.GetGuid(1), Convert.ToInt32(reader.GetInt64(2)),
            reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5)),
            reader.GetString(6));
    }

    private static void RequireEligible(
        TelehealthApplicantInsuranceHandoffContext context,
        int facilityId,
        bool allowConfirmed)
    {
        var statusAllowed = context.ApplicantStatus == "SyntheticMinimumRegistrationDetailsConfirmed"
            || (allowConfirmed
                && context.ApplicantStatus == TelehealthApplicantInsuranceHandoffPolicy.ResultingStatus
                && context.ConfirmationId is not null);
        var evidenceFresh = context.EligibilityExpiresAt > context.DatabaseNow
            && context.PracticeNetworkExpiresAt > context.DatabaseNow;
        if (!statusAllowed
            || context.ApplicantExpiresAt <= context.DatabaseNow
            || context.ApplicantFacilityId != facilityId
            || context.PromotionOutcome != "SyntheticPatientCreated"
            || context.CanonicalPatientCreated is not true
            || context.PromotionId is null
            || string.IsNullOrWhiteSpace(context.CanonicalPatientId)
            || context.PatientPortalEnabled is not false
            || context.PatientFacilityId != facilityId
            || context.MergedIntoPatientId is not null
            || context.RegistrationDetailsConfirmationId is null
            || context.MemberInsuranceDetailsId is null
            || context.EligibilityResultId is null
            || context.PracticeNetworkDeterminationId is null
            || !context.SourceProvenanceValid
            || context.CanonicalInsuranceRecordCount != 0
            || (!allowConfirmed && !evidenceFresh))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_insurance_handoff_state_conflict",
                "The applicant is not eligible for this bounded synthetic insurance handoff confirmation.");
        }
    }

    private static Guid? NullableGuid(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

    private static string? NullableString(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static bool? NullableBoolean(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);

    private static int? NullableInt32(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static DateTimeOffset? NullableDateTimeOffset(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);

    private static void RequireAccess(string existingHash, string suppliedHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(existingHash, suppliedHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireReplayFingerprint(string existing, string commandFingerprint)
    {
        if (!string.Equals(existing, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_insurance_handoff_idempotency_conflict",
                "The insurance handoff idempotency key was already used with different content.");
        }
    }
}
