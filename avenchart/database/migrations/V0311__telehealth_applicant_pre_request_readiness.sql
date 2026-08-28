-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Adds one immutable acknowledgment over five coarse prior synthetic onboarding
-- receipt sections. It creates no completion, task, request, queue, or care.

alter table telehealth_prospective_applicants
  drop constraint if exists chk_telehealth_applicant_status;

alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_status check (
    status in ('ContactVerificationPending','IdentityReviewPending',
               'IdentityReviewApproved','ManualReviewRequired',
               'SafetyScreenPassed','SafetyClinicalReviewRequired',
               'SafetyInPersonRequired','SafetyEmergencyRedirect',
               'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
               'MemberInsuranceDetailsRecorded','SyntheticEligibilityRecorded',
               'SyntheticPracticeNetworkRecorded','SyntheticIdentityProofingRecorded',
               'SyntheticPromotionAuthorized','SyntheticPromotionDenied',
               'SyntheticPatientPromoted','SyntheticPromotionBlockedPossibleMatch',
               'SyntheticTelehealthNoticeAcknowledged',
               'SyntheticMinimumRegistrationDetailsConfirmed',
               'SyntheticInsuranceDetailsConfirmed',
               'SyntheticCommunicationAccessReadinessRecorded',
               'SyntheticDevicePreparationRecorded',
               'SyntheticClinicalInformationInventoryRecorded',
               'SyntheticMedicationInformationRecorded',
               'SyntheticAllergyInformationRecorded',
               'SyntheticHealthHistoryInformationRecorded',
               'SyntheticClinicalInformationSummaryConfirmed',
               'SyntheticPreRequestReadinessAcknowledged',
               'VerificationLocked','Expired'));

alter table telehealth_prospective_applicants
  drop constraint if exists chk_telehealth_applicant_review_state;

alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_review_state check (
    (status = 'IdentityReviewPending'
      and contact_verified_at is not null
      and duplicate_disposition in ('NoCandidate','PossibleMatchManualReview')
      and duplicate_evidence_fingerprint is not null)
    or
    (status in ('IdentityReviewApproved','SafetyScreenPassed',
                'SafetyClinicalReviewRequired','SafetyInPersonRequired',
                'SafetyEmergencyRedirect','VisitPurposeRecorded',
                'PracticeNetworkPrecheckRecorded','MemberInsuranceDetailsRecorded',
                'SyntheticEligibilityRecorded','SyntheticPracticeNetworkRecorded',
                'SyntheticIdentityProofingRecorded','SyntheticPromotionAuthorized',
                'SyntheticPromotionDenied','SyntheticPatientPromoted',
                'SyntheticPromotionBlockedPossibleMatch',
                'SyntheticTelehealthNoticeAcknowledged',
                'SyntheticMinimumRegistrationDetailsConfirmed',
                'SyntheticInsuranceDetailsConfirmed',
                'SyntheticCommunicationAccessReadinessRecorded',
                'SyntheticDevicePreparationRecorded',
                'SyntheticClinicalInformationInventoryRecorded',
                'SyntheticMedicationInformationRecorded',
                'SyntheticAllergyInformationRecorded',
                'SyntheticHealthHistoryInformationRecorded',
                'SyntheticClinicalInformationSummaryConfirmed',
                'SyntheticPreRequestReadinessAcknowledged')
      and contact_verified_at is not null
      and duplicate_disposition = 'NoCandidate'
      and duplicate_evidence_fingerprint is not null)
    or
    (status = 'ManualReviewRequired'
      and contact_verified_at is not null
      and duplicate_disposition = 'PossibleMatchManualReview'
      and duplicate_evidence_fingerprint is not null)
    or
    (status in ('ContactVerificationPending','VerificationLocked','Expired')
      and contact_verified_at is null
      and duplicate_disposition is null
      and duplicate_evidence_fingerprint is null));

alter table telehealth_applicant_events
  drop constraint if exists chk_telehealth_applicant_event_action;

alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_action check (
    action in ('applicant-created','contact-verified','verification-locked',
               'applicant-expired','identity-review-recorded',
               'prospective-safety-triage-evaluated',
               'prospective-visit-purpose-recorded',
               'prospective-practice-network-precheck-recorded',
               'prospective-member-insurance-details-recorded',
               'prospective-synthetic-eligibility-recorded',
               'prospective-synthetic-practice-network-recorded',
               'prospective-synthetic-identity-proofing-recorded',
               'prospective-synthetic-promotion-authorization-recorded',
               'prospective-synthetic-patient-promotion-recorded',
               'prospective-telehealth-notice-acknowledged',
               'prospective-minimum-registration-details-confirmed',
               'prospective-insurance-handoff-confirmed',
               'prospective-communication-access-readiness-recorded',
               'prospective-device-preparation-recorded',
               'prospective-clinical-information-inventory-recorded',
               'prospective-medication-information-recorded',
               'prospective-allergy-information-recorded',
               'prospective-health-history-information-recorded',
               'prospective-clinical-information-summary-confirmed',
               'prospective-pre-request-readiness-acknowledged'));

alter table telehealth_applicant_events
  drop constraint if exists chk_telehealth_applicant_event_status;

alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_status check (
    (from_status is null or from_status in (
      'ContactVerificationPending','IdentityReviewPending','IdentityReviewApproved',
      'ManualReviewRequired','SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect','VisitPurposeRecorded',
      'PracticeNetworkPrecheckRecorded','MemberInsuranceDetailsRecorded',
      'SyntheticEligibilityRecorded','SyntheticPracticeNetworkRecorded',
      'SyntheticIdentityProofingRecorded','SyntheticPromotionAuthorized',
      'SyntheticPromotionDenied','SyntheticPatientPromoted',
      'SyntheticPromotionBlockedPossibleMatch','SyntheticTelehealthNoticeAcknowledged',
      'SyntheticMinimumRegistrationDetailsConfirmed','SyntheticInsuranceDetailsConfirmed',
      'SyntheticCommunicationAccessReadinessRecorded','SyntheticDevicePreparationRecorded',
      'SyntheticClinicalInformationInventoryRecorded','SyntheticMedicationInformationRecorded',
      'SyntheticAllergyInformationRecorded','SyntheticHealthHistoryInformationRecorded',
      'SyntheticClinicalInformationSummaryConfirmed','SyntheticPreRequestReadinessAcknowledged',
      'VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending','IdentityReviewApproved',
      'ManualReviewRequired','SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect','VisitPurposeRecorded',
      'PracticeNetworkPrecheckRecorded','MemberInsuranceDetailsRecorded',
      'SyntheticEligibilityRecorded','SyntheticPracticeNetworkRecorded',
      'SyntheticIdentityProofingRecorded','SyntheticPromotionAuthorized',
      'SyntheticPromotionDenied','SyntheticPatientPromoted',
      'SyntheticPromotionBlockedPossibleMatch','SyntheticTelehealthNoticeAcknowledged',
      'SyntheticMinimumRegistrationDetailsConfirmed','SyntheticInsuranceDetailsConfirmed',
      'SyntheticCommunicationAccessReadinessRecorded','SyntheticDevicePreparationRecorded',
      'SyntheticClinicalInformationInventoryRecorded','SyntheticMedicationInformationRecorded',
      'SyntheticAllergyInformationRecorded','SyntheticHealthHistoryInformationRecorded',
      'SyntheticClinicalInformationSummaryConfirmed','SyntheticPreRequestReadinessAcknowledged',
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_pre_request_readiness_acknowledgments (
  acknowledgment_id uuid primary key,
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  promotion_id uuid not null unique references telehealth_applicant_synthetic_promotions(promotion_id),
  canonical_patient_id text not null unique references patients(canonical_id),
  registration_details_confirmation_id uuid not null unique
    references telehealth_applicant_registration_details_confirmations(confirmation_id),
  registration_details_fingerprint character(64) not null,
  insurance_handoff_confirmation_id uuid not null unique
    references telehealth_applicant_insurance_handoff_confirmations(confirmation_id),
  insurance_snapshot_fingerprint character(64) not null,
  communication_access_readiness_id uuid not null unique
    references telehealth_applicant_communication_access_readiness(readiness_id),
  communication_context_fingerprint character(64) not null,
  interpreter_requested boolean not null,
  accessibility_support_requested boolean not null,
  device_preparation_id uuid not null unique
    references telehealth_applicant_device_preparations(preparation_id),
  preparation_snapshot_fingerprint character(64) not null,
  clinical_inventory_id uuid not null unique
    references telehealth_applicant_clinical_information_inventories(inventory_id),
  inventory_snapshot_fingerprint character(64) not null,
  clinical_information_summary_confirmation_id uuid not null unique
    references telehealth_applicant_clinical_information_summary_confirmations(confirmation_id),
  clinical_information_summary_snapshot_fingerprint character(64) not null,
  clinical_information_summary_route text not null,
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  pre_request_readiness_snapshot_fingerprint character(64) not null,
  overall_route text not null,
  prior_sections_reviewed_acknowledged boolean not null,
  outstanding_steps_remain_acknowledged boolean not null,
  no_request_or_queue_created_acknowledged boolean not null,
  correction_requires_separate_workflow_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  acknowledged_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  identity_assurance_established boolean not null default false,
  coverage_guaranteed boolean not null default false,
  rendering_clinician_network_verified boolean not null default false,
  interpreter_or_accommodation_arranged boolean not null default false,
  technology_ready boolean not null default false,
  clinical_information_reconciled boolean not null default false,
  clinical_intake_completed boolean not null default false,
  clinical_eligibility_established boolean not null default false,
  legal_consent_established boolean not null default false,
  staff_review_created boolean not null default false,
  clinician_review_created boolean not null default false,
  practice_accepted boolean not null default false,
  patient_record_changed boolean not null default false,
  request_created boolean not null default false,
  queue_entered boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  billing_enabled boolean not null default false,
  claim_created boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  constraint uq_telehealth_pre_request_readiness_idempotency
    unique(applicant_id,idempotency_key),
  constraint chk_telehealth_pre_request_readiness_result check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticPreRequestReadinessAcknowledged'),
  constraint chk_telehealth_pre_request_readiness_hashes check (
    registration_details_fingerprint ~ '^[0-9a-f]{64}$'
    and insurance_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and communication_context_fingerprint ~ '^[0-9a-f]{64}$'
    and preparation_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and inventory_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and clinical_information_summary_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and pre_request_readiness_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_pre_request_readiness_summary_route check (
    clinical_information_summary_route in (
      'AdditionalClinicalInformationCollectionRequired',
      'AssistedClinicalInformationReviewRequired',
      'ClinicianClinicalInformationReviewRequired',
      'PendingClinicianReconciliationOfPatientReportedNone')),
  constraint chk_telehealth_pre_request_readiness_overall_route check (
    overall_route = case
      when clinical_information_summary_route='AdditionalClinicalInformationCollectionRequired'
        then 'AdditionalClinicalInformationRequired'
      when interpreter_requested or accessibility_support_requested
        or clinical_information_summary_route='AssistedClinicalInformationReviewRequired'
        then 'AssistedPreRequestSupportRequired'
      else 'PendingPracticePreRequestReview'
    end),
  constraint chk_telehealth_pre_request_readiness_acknowledgments check (
    prior_sections_reviewed_acknowledged
    and outstanding_steps_remain_acknowledged
    and no_request_or_queue_created_acknowledged
    and correction_requires_separate_workflow_acknowledged),
  constraint chk_telehealth_pre_request_readiness_policy check (
    policy_key='SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_PRE_REQUEST_READINESS_ACKNOWLEDGMENT_RECEIPT'),
  constraint chk_telehealth_pre_request_readiness_expiry check (
    acknowledged_at <= applicant_expires_at),
  constraint chk_telehealth_pre_request_readiness_no_consequence check (
    not identity_assurance_established
    and not coverage_guaranteed
    and not rendering_clinician_network_verified
    and not interpreter_or_accommodation_arranged
    and not technology_ready
    and not clinical_information_reconciled
    and not clinical_intake_completed
    and not clinical_eligibility_established
    and not legal_consent_established
    and not staff_review_created
    and not clinician_review_created
    and not practice_accepted
    and not patient_record_changed
    and not request_created
    and not queue_entered
    and not appointment_created
    and not encounter_created
    and not care_authorized
    and not prescribing_enabled
    and not billing_enabled
    and not claim_created
    and not integration_enabled
    and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_pre_request_readiness()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  patient_row patients%rowtype;
  registration_row telehealth_applicant_registration_details_confirmations%rowtype;
  insurance_row telehealth_applicant_insurance_handoff_confirmations%rowtype;
  communication_row telehealth_applicant_communication_access_readiness%rowtype;
  device_row telehealth_applicant_device_preparations%rowtype;
  inventory_row telehealth_applicant_clinical_information_inventories%rowtype;
  summary_row telehealth_applicant_clinical_information_summary_confirmations%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id;
  select * into promotion_row from telehealth_applicant_synthetic_promotions
  where promotion_id=new.promotion_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;
  select * into registration_row from telehealth_applicant_registration_details_confirmations
  where confirmation_id=new.registration_details_confirmation_id;
  select * into insurance_row from telehealth_applicant_insurance_handoff_confirmations
  where confirmation_id=new.insurance_handoff_confirmation_id;
  select * into communication_row from telehealth_applicant_communication_access_readiness
  where readiness_id=new.communication_access_readiness_id;
  select * into device_row from telehealth_applicant_device_preparations
  where preparation_id=new.device_preparation_id;
  select * into inventory_row from telehealth_applicant_clinical_information_inventories
  where inventory_id=new.clinical_inventory_id;
  select * into summary_row from telehealth_applicant_clinical_information_summary_confirmations
  where confirmation_id=new.clinical_information_summary_confirmation_id;

  if applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at
     or applicant_row.expires_at<=now() then
    raise exception using errcode='23514',
      message='telehealth_pre_request_readiness_applicant_mismatch';
  end if;

  if promotion_row.applicant_id<>new.applicant_id
     or promotion_row.practice_id<>new.practice_id
     or promotion_row.facility_id<>new.facility_id
     or promotion_row.outcome<>'SyntheticPatientCreated'
     or not promotion_row.canonical_patient_created
     or promotion_row.canonical_patient_id<>new.canonical_patient_id
     or patient_row.canonical_id is null
     or patient_row.facility_id<>new.facility_id
     or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null
     or patient_row.first_name<>applicant_row.legal_first_name
     or patient_row.last_name<>applicant_row.legal_last_name
     or patient_row.date_of_birth<>applicant_row.date_of_birth
     or patient_row.email<>applicant_row.email
     or coalesce(nullif(patient_row.phone_cell,''),nullif(patient_row.phone_home,''),patient_row.phone)<>applicant_row.phone
     or patient_row.state<>applicant_row.residence_state_code
     or patient_row.postal_code<>applicant_row.postal_code
     or exists(select 1 from insurance_records r where lower(r.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from medications r where lower(r.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from prescriptions r where lower(r.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from allergies r where lower(r.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from problems r where lower(r.patient_id)=lower(new.canonical_patient_id)) then
    raise exception using errcode='23514',
      message='telehealth_pre_request_readiness_patient_mismatch';
  end if;

  if registration_row.confirmation_id is null
     or registration_row.applicant_id<>new.applicant_id
     or registration_row.practice_id<>new.practice_id
     or registration_row.facility_id<>new.facility_id
     or registration_row.promotion_id<>new.promotion_id
     or registration_row.canonical_patient_id<>new.canonical_patient_id
     or registration_row.details_fingerprint<>new.registration_details_fingerprint
     or registration_row.resulting_applicant_status<>'SyntheticMinimumRegistrationDetailsConfirmed'
     or registration_row.policy_key<>'SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION'
     or registration_row.policy_version<>1
     or registration_row.patient_record_changed or registration_row.intake_completed
     or registration_row.practice_accepted or registration_row.request_created
     or registration_row.queue_enabled then
    raise exception using errcode='23514',
      message='telehealth_pre_request_readiness_registration_mismatch';
  end if;

  if insurance_row.confirmation_id is null
     or insurance_row.applicant_id<>new.applicant_id
     or insurance_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or insurance_row.insurance_snapshot_fingerprint<>new.insurance_snapshot_fingerprint
     or insurance_row.resulting_applicant_status<>'SyntheticInsuranceDetailsConfirmed'
     or insurance_row.policy_key<>'SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
     or insurance_row.policy_version<>1
     or insurance_row.canonical_coverage_created or insurance_row.practice_accepted
     or insurance_row.request_created or insurance_row.queue_enabled then
    raise exception using errcode='23514',
      message='telehealth_pre_request_readiness_insurance_mismatch';
  end if;

  if communication_row.readiness_id is null
     or communication_row.applicant_id<>new.applicant_id
     or communication_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or communication_row.insurance_handoff_confirmation_id<>new.insurance_handoff_confirmation_id
     or communication_row.context_snapshot_fingerprint<>new.communication_context_fingerprint
     or communication_row.interpreter_requested<>new.interpreter_requested
     or communication_row.accessibility_support_requested<>new.accessibility_support_requested
     or communication_row.resulting_applicant_status<>'SyntheticCommunicationAccessReadinessRecorded'
     or communication_row.policy_key<>'SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
     or communication_row.policy_version<>1
     or communication_row.interpreter_assigned
     or communication_row.accessibility_accommodation_arranged
     or communication_row.support_request_created or communication_row.request_created
     or communication_row.queue_enabled then
    raise exception using errcode='23514',
      message='telehealth_pre_request_readiness_communication_mismatch';
  end if;

  if device_row.preparation_id is null
     or device_row.applicant_id<>new.applicant_id
     or device_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or device_row.insurance_handoff_confirmation_id<>new.insurance_handoff_confirmation_id
     or device_row.communication_access_readiness_id<>new.communication_access_readiness_id
     or device_row.preparation_snapshot_fingerprint<>new.preparation_snapshot_fingerprint
     or device_row.resulting_applicant_status<>'SyntheticDevicePreparationRecorded'
     or device_row.policy_key<>'SYNTHETIC_APPLICANT_DEVICE_PREPARATION'
     or device_row.policy_version<>1
     or not device_row.browser_supported or not device_row.camera_available
     or not device_row.microphone_available or not device_row.speaker_available
     or device_row.technology_ready or device_row.waiting_room_created
     or device_row.request_created or device_row.queue_entered then
    raise exception using errcode='23514',
      message='telehealth_pre_request_readiness_device_mismatch';
  end if;

  if inventory_row.inventory_id is null
     or inventory_row.applicant_id<>new.applicant_id
     or inventory_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or inventory_row.insurance_handoff_confirmation_id<>new.insurance_handoff_confirmation_id
     or inventory_row.communication_access_readiness_id<>new.communication_access_readiness_id
     or inventory_row.device_preparation_id<>new.device_preparation_id
     or inventory_row.inventory_snapshot_fingerprint<>new.inventory_snapshot_fingerprint
     or inventory_row.preparation_snapshot_fingerprint<>new.preparation_snapshot_fingerprint
     or inventory_row.resulting_applicant_status<>'SyntheticClinicalInformationInventoryRecorded'
     or inventory_row.policy_key<>'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY'
     or inventory_row.policy_version<>1
     or inventory_row.clinical_intake_completed or inventory_row.clinical_eligibility_established
     or inventory_row.request_created or inventory_row.queue_entered then
    raise exception using errcode='23514',
      message='telehealth_pre_request_readiness_inventory_mismatch';
  end if;

  if summary_row.confirmation_id is null
     or summary_row.applicant_id<>new.applicant_id
     or summary_row.practice_id<>new.practice_id
     or summary_row.facility_id<>new.facility_id
     or summary_row.promotion_id<>new.promotion_id
     or summary_row.canonical_patient_id<>new.canonical_patient_id
     or summary_row.clinical_inventory_id<>new.clinical_inventory_id
     or summary_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or summary_row.resulting_applicant_status<>'SyntheticClinicalInformationSummaryConfirmed'
     or summary_row.clinical_information_summary_snapshot_fingerprint<>
        new.clinical_information_summary_snapshot_fingerprint
     or summary_row.summary_route<>new.clinical_information_summary_route
     or summary_row.policy_key<>'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY'
     or summary_row.policy_version<>1
     or summary_row.questionnaire_response_created
     or summary_row.medication_list_reconciled or summary_row.allergy_list_reconciled
     or summary_row.health_history_reconciled or summary_row.clinical_intake_completed
     or summary_row.clinical_eligibility_established or summary_row.clinician_review_created
     or summary_row.patient_record_changed or summary_row.practice_accepted
     or summary_row.request_created or summary_row.queue_entered
     or summary_row.care_authorized or summary_row.prescribing_enabled then
    raise exception using errcode='23514',
      message='telehealth_pre_request_readiness_summary_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_pre_request_readiness_guard
  on telehealth_applicant_pre_request_readiness_acknowledgments;
create trigger trg_telehealth_pre_request_readiness_guard
before insert on telehealth_applicant_pre_request_readiness_acknowledgments
for each row execute function enforce_telehealth_applicant_pre_request_readiness();

drop trigger if exists trg_telehealth_pre_request_readiness_append_only
  on telehealth_applicant_pre_request_readiness_acknowledgments;
create trigger trg_telehealth_pre_request_readiness_append_only
before update or delete on telehealth_applicant_pre_request_readiness_acknowledgments
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_pre_request_readiness_practice
  on telehealth_applicant_pre_request_readiness_acknowledgments(
    practice_id,facility_id,acknowledged_at);
