// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPracticeReviewInboxRecord(
    Guid PracticeReviewCaseId,
    int ApplicantVersion,
    string ApplicantStatus,
    string CaseStatus,
    string LegalFirstName,
    string LegalLastName,
    DateOnly DateOfBirth,
    string Email,
    string Phone,
    string ResidenceStateCode,
    string PostalCode,
    string PurposeCategory,
    string PurposeDisplayLabel,
    string SafetyOutcome,
    string ReviewRoute,
    bool InterpreterRequested,
    bool AccessibilitySupportRequested,
    string ClinicalInformationSummaryRoute,
    DateTimeOffset SubmittedAt,
    string? ActiveClaimActorId,
    DateTimeOffset? ActiveClaimExpiresAt);

public sealed class TelehealthApplicantPracticeReviewInboxRepository(NpgsqlDataSource dataSource)
{
    public async Task<(IReadOnlyList<TelehealthApplicantPracticeReviewInboxRecord> Items, DateTimeOffset DatabaseNow)> ListAsync(
        string practiceId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select c.case_id,a.version,a.status,c.case_status,
                   a.legal_first_name,a.legal_last_name,a.date_of_birth,a.email,a.phone,
                   a.residence_state_code,a.postal_code,
                   purpose.purpose_category,purpose.purpose_display_label,safety.outcome,
                   c.review_route,readiness.interpreter_requested,
                   readiness.accessibility_support_requested,
                   readiness.clinical_information_summary_route,submission.submitted_at,
                   active_claim.assigned_to_actor_id,active_claim.lease_expires_at,now()
            from telehealth_prospective_practice_review_cases c
            join telehealth_applicant_practice_review_submissions submission
              on submission.case_id=c.case_id
             and submission.applicant_id=c.applicant_id
             and submission.practice_id=c.practice_id
             and submission.facility_id=c.facility_id
             and submission.canonical_patient_id=c.canonical_patient_id
             and submission.readiness_acknowledgment_id=c.readiness_acknowledgment_id
             and submission.readiness_snapshot_fingerprint=c.readiness_snapshot_fingerprint
             and submission.review_route=c.review_route
            join telehealth_prospective_applicants a
              on a.applicant_id=c.applicant_id
             and a.practice_id=c.practice_id
             and a.facility_id=c.facility_id
             and a.version=submission.resulting_applicant_version
             and a.status=submission.resulting_applicant_status
            join telehealth_applicant_pre_request_readiness_acknowledgments readiness
              on readiness.acknowledgment_id=c.readiness_acknowledgment_id
             and readiness.applicant_id=c.applicant_id
             and readiness.practice_id=c.practice_id
             and readiness.facility_id=c.facility_id
             and readiness.canonical_patient_id=c.canonical_patient_id
             and readiness.pre_request_readiness_snapshot_fingerprint=c.readiness_snapshot_fingerprint
             and readiness.overall_route=c.review_route
            join telehealth_applicant_synthetic_promotions promotion
              on promotion.promotion_id=readiness.promotion_id
             and promotion.applicant_id=c.applicant_id
             and promotion.practice_id=c.practice_id
             and promotion.facility_id=c.facility_id
             and promotion.canonical_patient_id=c.canonical_patient_id
             and promotion.canonical_patient_created
            join patients patient
              on patient.canonical_id=c.canonical_patient_id
             and patient.facility_id=c.facility_id
             and not patient.portal_enabled
             and patient.merged_into_patient_id is null
             and patient.first_name=a.legal_first_name
             and patient.last_name=a.legal_last_name
             and patient.date_of_birth=a.date_of_birth
             and patient.email=a.email
             and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
             and patient.state=a.residence_state_code
             and patient.postal_code=a.postal_code
            join telehealth_applicant_visit_purposes purpose
              on purpose.applicant_id=c.applicant_id
             and purpose.practice_id=c.practice_id
             and purpose.facility_id=c.facility_id
            join telehealth_applicant_safety_triage_evaluations safety
              on safety.evaluation_id=purpose.safety_triage_evaluation_id
             and safety.applicant_id=c.applicant_id
             and safety.practice_id=c.practice_id
             and safety.facility_id=c.facility_id
             and safety.outcome=purpose.source_safety_outcome
            left join lateral (
              select claim.assigned_to_actor_id,claim.lease_expires_at
              from telehealth_practice_review_claims claim
              where claim.case_id=c.case_id and claim.practice_id=c.practice_id
                and claim.facility_id=c.facility_id and claim.lease_expires_at>now()
              order by claim.assigned_at desc,claim.claim_id desc limit 1
            ) active_claim on true
            where c.practice_id=@practiceId and c.facility_id=@facilityId
              and c.case_status='PendingPracticeReview'
              and a.status='SyntheticPracticeReviewSubmitted'
              and c.applicant_expires_at >= now()
              and a.expires_at=c.applicant_expires_at
              and submission.applicant_expires_at=c.applicant_expires_at
              and submission.resulting_applicant_status='SyntheticPracticeReviewSubmitted'
              and readiness.resulting_applicant_status='SyntheticPreRequestReadinessAcknowledged'
              and readiness.resulting_applicant_version=submission.resulting_applicant_version-1
              and submission.staff_review_created
              and not submission.clinician_review_created
              and not submission.practice_accepted
              and not submission.patient_record_changed
              and not submission.telehealth_request_created
              and not submission.patient_care_queue_entered
              and not submission.clinician_queue_entered
              and not submission.appointment_created
              and not submission.encounter_created
              and not submission.care_authorized
              and not submission.prescribing_enabled
              and not submission.billing_enabled
              and not submission.claim_created
              and not submission.integration_enabled
              and not submission.external_call_performed
              and safety.outcome='TelehealthEligible'
              and not exists (select 1 from insurance_records x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists (select 1 from medications x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists (select 1 from prescriptions x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists (select 1 from allergies x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists (select 1 from problems x where lower(x.patient_id)=lower(c.canonical_patient_id))
            order by submission.submitted_at,c.case_id
            limit 100;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);

        var items = new List<TelehealthApplicantPracticeReviewInboxRecord>();
        var databaseNow = DateTimeOffset.UtcNow;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            databaseNow = reader.GetFieldValue<DateTimeOffset>(21);
            items.Add(new(
                reader.GetGuid(0),
                Convert.ToInt32(reader.GetInt64(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetFieldValue<DateOnly>(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.GetString(14),
                reader.GetBoolean(15),
                reader.GetBoolean(16),
                reader.GetString(17),
                reader.GetFieldValue<DateTimeOffset>(18),
                reader.IsDBNull(19) ? null : reader.GetString(19),
                reader.IsDBNull(20) ? null : reader.GetFieldValue<DateTimeOffset>(20)));
        }

        if (items.Count == 0)
        {
            await reader.DisposeAsync();
            await using var clock = connection.CreateCommand();
            clock.CommandText = "select now();";
            await using var clockReader = await clock.ExecuteReaderAsync(cancellationToken);
            if (!await clockReader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Database clock is unavailable.");
            }

            databaseNow = clockReader.GetFieldValue<DateTimeOffset>(0);
        }

        return (items, databaseNow);
    }
}
