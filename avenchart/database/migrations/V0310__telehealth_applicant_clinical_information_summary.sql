-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Adds one immutable no-edit confirmation over the existing bounded synthetic
-- medication, allergy, and health-history receipts. It creates no canonical
-- clinical record, reconciliation, intake completion, request, queue, or care.

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
                'SyntheticClinicalInformationSummaryConfirmed')
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
               'prospective-clinical-information-summary-confirmed'));

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
      'SyntheticClinicalInformationSummaryConfirmed','VerificationLocked','Expired'))
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
      'SyntheticClinicalInformationSummaryConfirmed','VerificationLocked','Expired'));

create table if not exists telehealth_applicant_clinical_information_summary_confirmations (
  confirmation_id uuid primary key,
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  promotion_id uuid not null unique references telehealth_applicant_synthetic_promotions(promotion_id),
  canonical_patient_id text not null unique references patients(canonical_id),
  clinical_inventory_id uuid not null unique
    references telehealth_applicant_clinical_information_inventories(inventory_id),
  medication_information_id uuid not null unique
    references telehealth_applicant_medication_information_receipts(receipt_id),
  allergy_information_id uuid not null unique
    references telehealth_applicant_allergy_information_receipts(receipt_id),
  health_history_information_id uuid not null unique
    references telehealth_applicant_health_history_information_receipts(receipt_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  clinical_information_summary_snapshot_fingerprint character(64) not null,
  medication_information_snapshot_fingerprint character(64) not null,
  allergy_information_snapshot_fingerprint character(64) not null,
  health_history_information_snapshot_fingerprint character(64) not null,
  medications_status text not null,
  allergies_or_intolerances_status text not null,
  other_health_history_status text not null,
  medication_item_count integer not null,
  allergy_item_count integer not null,
  health_history_topic_count integer not null,
  additional_medication_items_reported boolean not null,
  additional_allergy_items_reported boolean not null,
  additional_health_history_topics_reported boolean not null,
  medication_review_route text not null,
  allergy_review_route text not null,
  health_history_review_route text not null,
  summary_route text not null,
  patient_reported_may_be_incomplete_acknowledged boolean not null,
  not_clinically_verified_or_reconciled_acknowledged boolean not null,
  no_intake_completion_or_eligibility_acknowledged boolean not null,
  correction_requires_separate_workflow_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  confirmed_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  questionnaire_response_created boolean not null default false,
  medication_list_reconciled boolean not null default false,
  allergy_list_reconciled boolean not null default false,
  health_history_reconciled boolean not null default false,
  confirmed_negative_established boolean not null default false,
  clinician_review_created boolean not null default false,
  clinical_intake_completed boolean not null default false,
  clinical_eligibility_established boolean not null default false,
  clinical_triage_changed boolean not null default false,
  patient_record_changed boolean not null default false,
  practice_accepted boolean not null default false,
  request_created boolean not null default false,
  queue_entered boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  constraint uq_telehealth_clinical_information_summary_idempotency
    unique(applicant_id,idempotency_key),
  constraint chk_telehealth_clinical_information_summary_versions check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticClinicalInformationSummaryConfirmed'
    and clinical_information_summary_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and medication_information_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and allergy_information_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and health_history_information_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_clinical_information_summary_statuses check (
    medications_status in ('PatientReportsNone','ItemsToReview','Unsure')
    and allergies_or_intolerances_status in ('PatientReportsNone','ItemsToReview','Unsure')
    and other_health_history_status in ('PatientReportsNone','ItemsToReview','Unsure')),
  constraint chk_telehealth_clinical_information_summary_counts check (
    medication_item_count between 0 and 6
    and allergy_item_count between 0 and 6
    and health_history_topic_count between 0 and 6),
  constraint chk_telehealth_clinical_information_summary_route check (
    summary_route = case
      when additional_medication_items_reported
        or additional_allergy_items_reported
        or additional_health_history_topics_reported
        then 'AdditionalClinicalInformationCollectionRequired'
      when 'Unsure' in (medications_status,allergies_or_intolerances_status,other_health_history_status)
        then 'AssistedClinicalInformationReviewRequired'
      when 'ItemsToReview' in (medications_status,allergies_or_intolerances_status,other_health_history_status)
        then 'ClinicianClinicalInformationReviewRequired'
      else 'PendingClinicianReconciliationOfPatientReportedNone'
    end),
  constraint chk_telehealth_clinical_information_summary_acknowledgments check (
    patient_reported_may_be_incomplete_acknowledged
    and not_clinically_verified_or_reconciled_acknowledged
    and no_intake_completion_or_eligibility_acknowledged
    and correction_requires_separate_workflow_acknowledged),
  constraint chk_telehealth_clinical_information_summary_policy check (
    policy_key='SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_CLINICAL_INFORMATION_SUMMARY_CONFIRMATION_RECEIPT'),
  constraint chk_telehealth_clinical_information_summary_expiry check (
    confirmed_at <= applicant_expires_at),
  constraint chk_telehealth_clinical_information_summary_no_consequence check (
    not questionnaire_response_created
    and not medication_list_reconciled
    and not allergy_list_reconciled
    and not health_history_reconciled
    and not confirmed_negative_established
    and not clinician_review_created
    and not clinical_intake_completed
    and not clinical_eligibility_established
    and not clinical_triage_changed
    and not patient_record_changed
    and not practice_accepted
    and not request_created
    and not queue_entered
    and not care_authorized
    and not prescribing_enabled)
);

create or replace function enforce_telehealth_applicant_clinical_information_summary()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  patient_row patients%rowtype;
  inventory_row telehealth_applicant_clinical_information_inventories%rowtype;
  medication_row telehealth_applicant_medication_information_receipts%rowtype;
  allergy_row telehealth_applicant_allergy_information_receipts%rowtype;
  history_row telehealth_applicant_health_history_information_receipts%rowtype;
  medication_count integer;
  allergy_count integer;
  history_count integer;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id;
  select * into promotion_row from telehealth_applicant_synthetic_promotions
  where promotion_id=new.promotion_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;
  select * into inventory_row from telehealth_applicant_clinical_information_inventories
  where inventory_id=new.clinical_inventory_id;
  select * into medication_row from telehealth_applicant_medication_information_receipts
  where receipt_id=new.medication_information_id;
  select * into allergy_row from telehealth_applicant_allergy_information_receipts
  where receipt_id=new.allergy_information_id;
  select * into history_row from telehealth_applicant_health_history_information_receipts
  where receipt_id=new.health_history_information_id;
  select count(*) into medication_count from telehealth_applicant_reported_medication_items
  where receipt_id=new.medication_information_id;
  select count(*) into allergy_count from telehealth_applicant_reported_allergy_items
  where receipt_id=new.allergy_information_id;
  select count(*) into history_count from telehealth_applicant_reported_health_history_topics
  where receipt_id=new.health_history_information_id;

  if applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at
     or applicant_row.expires_at<=now() then
    raise exception using errcode='23514',
      message='telehealth_clinical_information_summary_applicant_mismatch';
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
      message='telehealth_clinical_information_summary_patient_mismatch';
  end if;

  if history_row.receipt_id is null
     or history_row.applicant_id<>new.applicant_id
     or history_row.practice_id<>new.practice_id
     or history_row.facility_id<>new.facility_id
     or history_row.promotion_id<>new.promotion_id
     or history_row.canonical_patient_id<>new.canonical_patient_id
     or history_row.clinical_inventory_id<>new.clinical_inventory_id
     or history_row.medication_information_id<>new.medication_information_id
     or history_row.allergy_information_id<>new.allergy_information_id
     or history_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or history_row.resulting_applicant_status<>'SyntheticHealthHistoryInformationRecorded'
     or history_row.health_history_information_snapshot_fingerprint<>new.health_history_information_snapshot_fingerprint
     or history_row.medication_information_snapshot_fingerprint<>new.medication_information_snapshot_fingerprint
     or history_row.allergy_information_snapshot_fingerprint<>new.allergy_information_snapshot_fingerprint
     or history_row.inventory_other_health_history_status<>new.other_health_history_status
     or history_row.selected_topic_count<>new.health_history_topic_count
     or history_count<>new.health_history_topic_count
     or history_row.additional_or_unlisted_topics_reported<>new.additional_health_history_topics_reported
     or history_row.review_route<>new.health_history_review_route
     or history_row.policy_key<>'SYNTHETIC_APPLICANT_HEALTH_HISTORY_INFORMATION'
     or history_row.policy_version<>1
     or history_row.evidence_type<>'PROMOTED_PATIENT_HEALTH_HISTORY_INFORMATION_RECEIPT'
     or not history_row.patient_reported_may_be_incomplete_acknowledged
     or not history_row.topic_selection_is_not_diagnosis_acknowledged
     or not history_row.no_status_or_timing_captured_acknowledged
     or not history_row.clinician_verification_required_acknowledged
     or history_row.condition_created or history_row.procedure_created
     or history_row.observation_created or history_row.family_member_history_created
     or history_row.questionnaire_response_created or history_row.health_history_reconciled
     or history_row.risk_modifier_evaluated or history_row.clinical_triage_changed
     or history_row.clinician_review_created or history_row.clinical_intake_completed
     or history_row.clinical_eligibility_established or history_row.patient_record_changed
     or history_row.request_created or history_row.queue_entered
     or history_row.care_authorized or history_row.prescribing_enabled then
    raise exception using errcode='23514',
      message='telehealth_clinical_information_summary_history_mismatch';
  end if;

  if medication_row.receipt_id is null
     or medication_row.clinical_inventory_id<>new.clinical_inventory_id
     or medication_row.medication_information_snapshot_fingerprint<>new.medication_information_snapshot_fingerprint
     or medication_row.selected_item_count<>new.medication_item_count
     or medication_count<>new.medication_item_count
     or medication_row.additional_or_unlisted_items_reported<>new.additional_medication_items_reported
     or medication_row.review_route<>new.medication_review_route
     or medication_row.patient_record_changed or medication_row.clinical_intake_completed
     or medication_row.clinical_eligibility_established or medication_row.request_created
     or medication_row.queue_entered or medication_row.care_authorized
     or medication_row.prescribing_enabled then
    raise exception using errcode='23514',
      message='telehealth_clinical_information_summary_medication_mismatch';
  end if;

  if allergy_row.receipt_id is null
     or allergy_row.clinical_inventory_id<>new.clinical_inventory_id
     or allergy_row.medication_information_id<>new.medication_information_id
     or allergy_row.allergy_information_snapshot_fingerprint<>new.allergy_information_snapshot_fingerprint
     or allergy_row.selected_item_count<>new.allergy_item_count
     or allergy_count<>new.allergy_item_count
     or allergy_row.additional_or_unlisted_items_reported<>new.additional_allergy_items_reported
     or allergy_row.review_route<>new.allergy_review_route
     or allergy_row.patient_record_changed or allergy_row.clinical_intake_completed
     or allergy_row.clinical_eligibility_established or allergy_row.request_created
     or allergy_row.queue_entered or allergy_row.care_authorized
     or allergy_row.prescribing_enabled then
    raise exception using errcode='23514',
      message='telehealth_clinical_information_summary_allergy_mismatch';
  end if;

  if inventory_row.inventory_id is null
     or inventory_row.applicant_id<>new.applicant_id
     or inventory_row.practice_id<>new.practice_id
     or inventory_row.facility_id<>new.facility_id
     or inventory_row.medications_status<>new.medications_status
     or inventory_row.allergies_or_intolerances_status<>new.allergies_or_intolerances_status
     or inventory_row.other_health_history_status<>new.other_health_history_status
     or inventory_row.patient_record_changed or inventory_row.clinical_intake_completed
     or inventory_row.clinical_eligibility_established or inventory_row.request_created
     or inventory_row.queue_entered or inventory_row.care_authorized
     or inventory_row.prescribing_enabled then
    raise exception using errcode='23514',
      message='telehealth_clinical_information_summary_inventory_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_clinical_information_summary_guard
  on telehealth_applicant_clinical_information_summary_confirmations;
create trigger trg_telehealth_clinical_information_summary_guard
before insert on telehealth_applicant_clinical_information_summary_confirmations
for each row execute function enforce_telehealth_applicant_clinical_information_summary();

drop trigger if exists trg_telehealth_clinical_information_summary_append_only
  on telehealth_applicant_clinical_information_summary_confirmations;
create trigger trg_telehealth_clinical_information_summary_append_only
before update or delete on telehealth_applicant_clinical_information_summary_confirmations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_clinical_information_summary_practice
  on telehealth_applicant_clinical_information_summary_confirmations(
    practice_id,facility_id,confirmed_at);
