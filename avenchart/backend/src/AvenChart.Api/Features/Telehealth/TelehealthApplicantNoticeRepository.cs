// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantNoticeContext(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string AccessKeyHash,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid SafetyEvaluationId,
    string CurrentLocationStateCode,
    string SafetyOutcome,
    Guid PromotionId,
    string PromotionOutcome,
    bool CanonicalPatientCreated,
    string? CanonicalPatientId,
    bool? PatientPortalEnabled,
    int? PatientFacilityId,
    string? MergedIntoPatientId,
    Guid? AcknowledgmentId,
    DateTimeOffset? AcknowledgedAt);

public sealed record TelehealthApplicantNoticeAcknowledgmentRecord(
    Guid AcknowledgmentId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string NoticeKey,
    int NoticeVersion,
    string CurrentLocationStateCode,
    DateTimeOffset AcknowledgedAt);

public sealed class TelehealthApplicantNoticeRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now(),
               s.evaluation_id,s.current_location_state_code,s.outcome,
               promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
               promotion.canonical_patient_id,
               patient.portal_enabled,patient.facility_id,patient.merged_into_patient_id,
               acknowledgment.acknowledgment_id,acknowledgment.acknowledged_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_safety_triage_evaluations s
          on s.applicant_id=a.applicant_id
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.applicant_id=a.applicant_id
        left join patients patient
          on patient.canonical_id=promotion.canonical_patient_id
        left join telehealth_applicant_notice_acknowledgments acknowledgment
          on acknowledgment.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantNoticeContext> GetAuthorizedAsync(
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
        RequireEligible(context, facilityId, allowAcknowledged: true);
        return context;
    }

    public async Task<TelehealthApplicantNoticeAcknowledgmentRecord> AcknowledgeAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantNoticeAcknowledgment acknowledgment,
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

        RequireEligible(context, facilityId, allowAcknowledged: false);
        if (context.ApplicantVersion != acknowledgment.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Reload the state notice before retrying.");
        }
        var expectedNotice = TelehealthApplicantNoticePolicy.ForState(context.CurrentLocationStateCode);
        if (acknowledgment.CurrentLocationStateCode != context.CurrentLocationStateCode)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_notice_location_changed",
                "Current location changed. Start a fresh safety and location process before continuing.");
        }
        if (acknowledgment.NoticeKey != expectedNotice.NoticeKey
            || acknowledgment.NoticeVersion != expectedNotice.NoticeVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_notice_version_conflict",
                "The state notice changed. Reload it before acknowledging.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticPatientPromoted';
                """;
            update.Parameters.AddWithValue("status", TelehealthApplicantNoticePolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", acknowledgment.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_version_conflict",
                    "The applicant changed. Reload the state notice before retrying.");
            }
        }

        var acknowledgmentId = Guid.NewGuid();
        DateTimeOffset acknowledgedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_notice_acknowledgments(
                  acknowledgment_id,applicant_id,practice_id,facility_id,
                  safety_triage_evaluation_id,promotion_id,canonical_patient_id,
                  resulting_applicant_version,resulting_applicant_status,
                  current_location_state_code,notice_key,notice_version,
                  notice_source_title,notice_source_url,
                  current_location_confirmed,mode_of_care_acknowledged,
                  privacy_limitations_acknowledged,emergency_instructions_acknowledged,
                  in_person_option_acknowledged,
                  clinician_reconfirmation_required_acknowledged,
                  synthetic_data_confirmed,policy_key,policy_version,evidence_type,
                  legal_review_status,applicant_expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @acknowledgmentId,@applicantId,@practiceId,@facilityId,
                  @safetyEvaluationId,@promotionId,@patientId,
                  @nextVersion,@nextStatus,@stateCode,@noticeKey,@noticeVersion,
                  @sourceTitle,@sourceUrl,true,true,true,true,true,true,true,
                  @policyKey,@policyVersion,@evidenceType,@legalReviewStatus,
                  @applicantExpiresAt,@idempotencyKey,@commandFingerprint)
                returning acknowledged_at;
                """;
            insert.Parameters.AddWithValue("acknowledgmentId", acknowledgmentId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("safetyEvaluationId", context.SafetyEvaluationId);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", TelehealthApplicantNoticePolicy.ResultingStatus);
            insert.Parameters.AddWithValue("stateCode", acknowledgment.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("noticeKey", expectedNotice.NoticeKey);
            insert.Parameters.AddWithValue("noticeVersion", expectedNotice.NoticeVersion);
            insert.Parameters.AddWithValue("sourceTitle", expectedNotice.SourceTitle);
            insert.Parameters.AddWithValue("sourceUrl", expectedNotice.SourceUrl);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantNoticePolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantNoticePolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantNoticePolicy.EvidenceType);
            insert.Parameters.AddWithValue("legalReviewStatus", TelehealthApplicantNoticePolicy.LegalReviewStatus);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic notice acknowledgment time is unavailable.");
            }
            acknowledgedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-telehealth-notice-acknowledged',
                       'SyntheticPatientPromoted',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", TelehealthApplicantNoticePolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "notice-acknowledgment:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            acknowledgmentId,
            applicantId,
            nextVersion,
            TelehealthApplicantNoticePolicy.ResultingStatus,
            expectedNotice.NoticeKey,
            expectedNotice.NoticeVersion,
            acknowledgment.CurrentLocationStateCode,
            acknowledgedAt);
    }

    private static async Task<TelehealthApplicantNoticeContext?> LoadAsync(
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
            reader.GetFieldValue<DateTimeOffset>(5), reader.GetGuid(6), reader.GetString(7),
            reader.GetString(8), reader.GetGuid(9), reader.GetString(10), reader.GetBoolean(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetBoolean(13),
            reader.IsDBNull(14) ? null : reader.GetInt32(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetGuid(16),
            reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17));
    }

    private static async Task<(TelehealthApplicantNoticeAcknowledgmentRecord Record,
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
            select acknowledgment_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,notice_key,notice_version,
                   current_location_state_code,acknowledged_at,command_fingerprint
            from telehealth_applicant_notice_acknowledgments
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
            reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(7)), reader.GetString(8));
    }

    private static void RequireEligible(
        TelehealthApplicantNoticeContext context,
        int facilityId,
        bool allowAcknowledged)
    {
        var statusAllowed = context.ApplicantStatus == "SyntheticPatientPromoted"
            || (allowAcknowledged
                && context.ApplicantStatus == TelehealthApplicantNoticePolicy.ResultingStatus
                && context.AcknowledgmentId is not null);
        if (!statusAllowed
            || context.ApplicantExpiresAt <= context.DatabaseNow
            || context.SafetyOutcome != TelehealthTriageOutcome.TelehealthEligible.ToString()
            || context.PromotionOutcome != "SyntheticPatientCreated"
            || !context.CanonicalPatientCreated
            || string.IsNullOrWhiteSpace(context.CanonicalPatientId)
            || context.PatientPortalEnabled is not false
            || context.PatientFacilityId != facilityId
            || context.MergedIntoPatientId is not null)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_notice_state_conflict",
                "The applicant is not eligible for this bounded state-notice acknowledgment.");
        }
        _ = TelehealthApplicantNoticePolicy.ForState(context.CurrentLocationStateCode);
    }

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
                "telehealth_applicant_notice_idempotency_conflict",
                "The notice-acknowledgment idempotency key was already used with different content.");
        }
    }
}
