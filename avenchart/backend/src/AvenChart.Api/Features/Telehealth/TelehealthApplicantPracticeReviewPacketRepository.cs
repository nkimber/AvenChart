// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPracticeReviewPacketRecord(
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
    DateTimeOffset SubmittedAt,
    DateTimeOffset AssignmentExpiresAt,
    string PayerDisplayName,
    string ProductDisplayName,
    string MemberIdLast4,
    string? GroupNumberLast4,
    string SubscriberRelationship,
    string CoveragePriority,
    string EligibilityBusinessOutcome,
    DateTimeOffset EligibilityCheckedAt,
    DateTimeOffset EligibilityExpiresAt,
    string PracticeNetworkBusinessOutcome,
    DateTimeOffset PracticeNetworkCheckedAt,
    DateTimeOffset PracticeNetworkExpiresAt,
    bool RenderingPhysicianNetworkChecked,
    string PreferredSpokenLanguage,
    bool InterpreterRequested,
    bool AccessibilitySupportRequested,
    bool SafePrivateCommunicationConfirmed,
    bool BrowserSupported,
    bool CameraAvailable,
    bool MicrophoneAvailable,
    bool SpeakerAvailable,
    string NetworkQuality,
    string ClinicalInformationSummaryRoute,
    DateTimeOffset RegistrationConfirmedAt,
    DateTimeOffset InsuranceConfirmedAt,
    DateTimeOffset CommunicationRecordedAt,
    DateTimeOffset DeviceRecordedAt,
    DateTimeOffset ClinicalSummaryConfirmedAt,
    DateTimeOffset DatabaseNow);

public sealed class TelehealthApplicantPracticeReviewPacketRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantPracticeReviewPacketRecord?> GetAsync(
        string practiceId,
        int facilityId,
        string actorId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select c.case_id,a.version,a.status,c.case_status,
                   a.legal_first_name,a.legal_last_name,a.date_of_birth,a.email,a.phone,
                   a.residence_state_code,a.postal_code,
                   purpose.purpose_category,purpose.purpose_display_label,safety.outcome,
                   c.review_route,submission.submitted_at,active_claim.lease_expires_at,
                   insurance.payer_display_name,insurance.product_display_name,
                   insurance.member_id_last4,insurance.group_number_last4,
                   insurance.subscriber_relationship,insurance.coverage_priority,
                   insurance.eligibility_business_outcome,insurance.eligibility_checked_at,
                   insurance.eligibility_expires_at,insurance.practice_network_business_outcome,
                   insurance.practice_network_checked_at,insurance.practice_network_expires_at,
                   insurance.rendering_physician_network_checked,
                   communication.preferred_spoken_language,communication.interpreter_requested,
                   communication.accessibility_support_requested,
                   communication.safe_private_communication_confirmed,
                   device.browser_supported,device.camera_available,device.microphone_available,
                   device.speaker_available,device.network_quality,summary.summary_route,
                   registration.confirmed_at,insurance.confirmed_at,communication.recorded_at,
                   device.recorded_at,summary.confirmed_at,now()
            from telehealth_prospective_practice_review_cases c
            join telehealth_applicant_practice_review_submissions submission
              on submission.case_id=c.case_id and submission.applicant_id=c.applicant_id
             and submission.practice_id=c.practice_id and submission.facility_id=c.facility_id
             and submission.canonical_patient_id=c.canonical_patient_id
             and submission.readiness_acknowledgment_id=c.readiness_acknowledgment_id
             and submission.readiness_snapshot_fingerprint=c.readiness_snapshot_fingerprint
             and submission.review_route=c.review_route
            join telehealth_prospective_applicants a
              on a.applicant_id=c.applicant_id and a.practice_id=c.practice_id
             and a.facility_id=c.facility_id and a.version=submission.resulting_applicant_version
             and a.status=submission.resulting_applicant_status
            join telehealth_applicant_pre_request_readiness_acknowledgments readiness
              on readiness.acknowledgment_id=c.readiness_acknowledgment_id
             and readiness.applicant_id=c.applicant_id and readiness.practice_id=c.practice_id
             and readiness.facility_id=c.facility_id
             and readiness.canonical_patient_id=c.canonical_patient_id
             and readiness.pre_request_readiness_snapshot_fingerprint=c.readiness_snapshot_fingerprint
             and readiness.overall_route=c.review_route
            join telehealth_applicant_synthetic_promotions promotion
              on promotion.promotion_id=readiness.promotion_id
             and promotion.applicant_id=c.applicant_id and promotion.practice_id=c.practice_id
             and promotion.facility_id=c.facility_id
             and promotion.canonical_patient_id=c.canonical_patient_id
             and promotion.canonical_patient_created
            join patients patient
              on patient.canonical_id=c.canonical_patient_id and patient.facility_id=c.facility_id
             and not patient.portal_enabled and patient.merged_into_patient_id is null
             and patient.first_name=a.legal_first_name and patient.last_name=a.legal_last_name
             and patient.date_of_birth=a.date_of_birth and patient.email=a.email
             and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
             and patient.state=a.residence_state_code and patient.postal_code=a.postal_code
            join telehealth_applicant_visit_purposes purpose
              on purpose.applicant_id=c.applicant_id and purpose.practice_id=c.practice_id
             and purpose.facility_id=c.facility_id
            join telehealth_applicant_safety_triage_evaluations safety
              on safety.evaluation_id=purpose.safety_triage_evaluation_id
             and safety.applicant_id=c.applicant_id and safety.practice_id=c.practice_id
             and safety.facility_id=c.facility_id and safety.outcome=purpose.source_safety_outcome
            join telehealth_applicant_registration_details_confirmations registration
              on registration.confirmation_id=readiness.registration_details_confirmation_id
             and registration.applicant_id=c.applicant_id and registration.practice_id=c.practice_id
             and registration.facility_id=c.facility_id
             and registration.promotion_id=readiness.promotion_id
             and registration.canonical_patient_id=c.canonical_patient_id
             and registration.details_fingerprint=readiness.registration_details_fingerprint
            join telehealth_applicant_insurance_handoff_confirmations insurance
              on insurance.confirmation_id=readiness.insurance_handoff_confirmation_id
             and insurance.applicant_id=c.applicant_id and insurance.practice_id=c.practice_id
             and insurance.facility_id=c.facility_id
             and insurance.promotion_id=readiness.promotion_id
             and insurance.canonical_patient_id=c.canonical_patient_id
             and insurance.registration_details_confirmation_id=registration.confirmation_id
             and insurance.insurance_snapshot_fingerprint=readiness.insurance_snapshot_fingerprint
            join telehealth_applicant_communication_access_readiness communication
              on communication.readiness_id=readiness.communication_access_readiness_id
             and communication.applicant_id=c.applicant_id and communication.practice_id=c.practice_id
             and communication.facility_id=c.facility_id
             and communication.promotion_id=readiness.promotion_id
             and communication.canonical_patient_id=c.canonical_patient_id
             and communication.registration_details_confirmation_id=registration.confirmation_id
             and communication.insurance_handoff_confirmation_id=insurance.confirmation_id
             and communication.context_snapshot_fingerprint=readiness.communication_context_fingerprint
            join telehealth_applicant_device_preparations device
              on device.preparation_id=readiness.device_preparation_id
             and device.applicant_id=c.applicant_id and device.practice_id=c.practice_id
             and device.facility_id=c.facility_id and device.promotion_id=readiness.promotion_id
             and device.canonical_patient_id=c.canonical_patient_id
             and device.registration_details_confirmation_id=registration.confirmation_id
             and device.insurance_handoff_confirmation_id=insurance.confirmation_id
             and device.communication_access_readiness_id=communication.readiness_id
             and device.preparation_snapshot_fingerprint=readiness.preparation_snapshot_fingerprint
            join telehealth_applicant_clinical_information_summary_confirmations summary
              on summary.confirmation_id=readiness.clinical_information_summary_confirmation_id
             and summary.applicant_id=c.applicant_id and summary.practice_id=c.practice_id
             and summary.facility_id=c.facility_id and summary.promotion_id=readiness.promotion_id
             and summary.canonical_patient_id=c.canonical_patient_id
             and summary.clinical_inventory_id=readiness.clinical_inventory_id
             and summary.clinical_information_summary_snapshot_fingerprint=
                 readiness.clinical_information_summary_snapshot_fingerprint
             and summary.summary_route=readiness.clinical_information_summary_route
            join lateral (
              select claim.lease_expires_at
              from telehealth_practice_review_claims claim
              where claim.case_id=c.case_id and claim.practice_id=c.practice_id
                and claim.facility_id=c.facility_id
                and claim.assigned_to_actor_id=@actorId and claim.lease_expires_at>now()
              order by claim.assigned_at desc,claim.claim_id desc limit 1
            ) active_claim on true
            where c.case_id=@caseId and c.practice_id=@practiceId and c.facility_id=@facilityId
              and c.case_status='PendingPracticeReview'
              and a.status='SyntheticPracticeReviewSubmitted'
              and c.applicant_expires_at>now() and a.expires_at=c.applicant_expires_at
              and submission.applicant_expires_at=c.applicant_expires_at
              and submission.resulting_applicant_status='SyntheticPracticeReviewSubmitted'
              and submission.policy_key='SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION'
              and submission.policy_version=1 and submission.staff_review_created
              and not submission.clinician_review_created and not submission.practice_accepted
              and not submission.patient_record_changed and not submission.telehealth_request_created
              and not submission.patient_care_queue_entered and not submission.clinician_queue_entered
              and not submission.appointment_created and not submission.encounter_created
              and not submission.care_authorized and not submission.prescribing_enabled
              and not submission.billing_enabled and not submission.claim_created
              and not submission.integration_enabled and not submission.external_call_performed
              and readiness.resulting_applicant_status='SyntheticPreRequestReadinessAcknowledged'
              and readiness.resulting_applicant_version=submission.resulting_applicant_version-1
              and readiness.policy_key='SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS'
              and readiness.policy_version=1
              and registration.resulting_applicant_status='SyntheticMinimumRegistrationDetailsConfirmed'
              and registration.policy_key='SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION'
              and registration.policy_version=1 and not registration.identity_assurance_established
              and not registration.patient_record_changed and not registration.correction_completed
              and not registration.intake_completed and not registration.legal_consent_established
              and not registration.practice_accepted and not registration.insurance_confirmed
              and not registration.coverage_created and not registration.request_created
              and not registration.queue_enabled and not registration.care_enabled
              and insurance.resulting_applicant_status='SyntheticInsuranceDetailsConfirmed'
              and insurance.policy_key='SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
              and insurance.policy_version=1 and not insurance.coverage_verified
              and not insurance.exact_network_confirmed and not insurance.canonical_coverage_created
              and not insurance.patient_record_changed and not insurance.portal_access_enabled
              and not insurance.intake_completed and not insurance.legal_consent_established
              and not insurance.practice_accepted and not insurance.financial_record_created
              and not insurance.request_created and not insurance.queue_enabled
              and not insurance.appointment_created and not insurance.encounter_created
              and not insurance.care_enabled and not insurance.prescribing_enabled
              and not insurance.billing_enabled and not insurance.claim_created
              and not insurance.communication_enabled and not insurance.integration_enabled
              and not insurance.external_call_performed
              and communication.resulting_applicant_status='SyntheticCommunicationAccessReadinessRecorded'
              and communication.policy_key='SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
              and communication.policy_version=1 and not communication.interpreter_assigned
              and not communication.accessibility_accommodation_arranged
              and not communication.communication_arrangement_completed
              and not communication.support_request_created
              and not communication.technology_readiness_completed
              and not communication.patient_record_changed and not communication.portal_access_enabled
              and not communication.intake_completed and not communication.legal_consent_established
              and not communication.practice_accepted and not communication.financial_record_created
              and not communication.request_created and not communication.queue_enabled
              and not communication.appointment_created and not communication.encounter_created
              and not communication.care_enabled and not communication.prescribing_enabled
              and not communication.billing_enabled and not communication.claim_created
              and not communication.communication_enabled and not communication.integration_enabled
              and not communication.external_call_performed
              and device.resulting_applicant_status='SyntheticDevicePreparationRecorded'
              and device.policy_key='SYNTHETIC_APPLICANT_DEVICE_PREPARATION'
              and device.policy_version=1 and not device.technology_ready
              and not device.waiting_room_created and not device.media_session_created
              and not device.communication_started and not device.support_arrangement_completed
              and not device.patient_record_changed and not device.portal_access_enabled
              and not device.intake_completed and not device.legal_consent_established
              and not device.practice_accepted and not device.financial_record_created
              and not device.request_created and not device.queue_entered
              and not device.appointment_created and not device.encounter_created
              and not device.care_authorized and not device.prescribing_enabled
              and not device.billing_enabled and not device.claim_created
              and not device.integration_enabled and not device.external_call_performed
              and summary.resulting_applicant_status='SyntheticClinicalInformationSummaryConfirmed'
              and summary.policy_key='SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY'
              and summary.policy_version=1 and not summary.questionnaire_response_created
              and not summary.medication_list_reconciled and not summary.allergy_list_reconciled
              and not summary.health_history_reconciled and not summary.confirmed_negative_established
              and not summary.clinician_review_created and not summary.clinical_intake_completed
              and not summary.clinical_eligibility_established and not summary.clinical_triage_changed
              and not summary.patient_record_changed and not summary.practice_accepted
              and not summary.request_created and not summary.queue_entered
              and not summary.care_authorized and not summary.prescribing_enabled
              and purpose.purpose_category in ('migraine','sleep') and safety.outcome='TelehealthEligible'
              and not exists(select 1 from insurance_records x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from medications x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from prescriptions x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from allergies x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from problems x where lower(x.patient_id)=lower(c.canonical_patient_id));
            """;
        command.Parameters.AddWithValue("caseId", caseId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("actorId", actorId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new(
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
            reader.GetFieldValue<DateTimeOffset>(15),
            reader.GetFieldValue<DateTimeOffset>(16),
            reader.GetString(17),
            reader.GetString(18),
            reader.GetString(19),
            reader.IsDBNull(20) ? null : reader.GetString(20),
            reader.GetString(21),
            reader.GetString(22),
            reader.GetString(23),
            reader.GetFieldValue<DateTimeOffset>(24),
            reader.GetFieldValue<DateTimeOffset>(25),
            reader.GetString(26),
            reader.GetFieldValue<DateTimeOffset>(27),
            reader.GetFieldValue<DateTimeOffset>(28),
            reader.GetBoolean(29),
            reader.GetString(30),
            reader.GetBoolean(31),
            reader.GetBoolean(32),
            reader.GetBoolean(33),
            reader.GetBoolean(34),
            reader.GetBoolean(35),
            reader.GetBoolean(36),
            reader.GetBoolean(37),
            reader.GetString(38),
            reader.GetString(39),
            reader.GetFieldValue<DateTimeOffset>(40),
            reader.GetFieldValue<DateTimeOffset>(41),
            reader.GetFieldValue<DateTimeOffset>(42),
            reader.GetFieldValue<DateTimeOffset>(43),
            reader.GetFieldValue<DateTimeOffset>(44),
            reader.GetFieldValue<DateTimeOffset>(45));
    }
}
