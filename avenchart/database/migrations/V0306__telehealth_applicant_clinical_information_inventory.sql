-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Adds one immutable, coarse, patient-reported synthetic clinical-information
-- inventory receipt. It stores no medication, substance, reaction, dose,
-- diagnosis, symptom, procedure, narrative, date, identifier, or free text and
-- creates no clinical review, intake, eligibility, request, queue, prescribing,
-- or care capability.

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
                'SyntheticClinicalInformationInventoryRecorded')
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
               'prospective-clinical-information-inventory-recorded'));

alter table telehealth_applicant_events
  drop constraint if exists chk_telehealth_applicant_event_status;

alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_status check (
    (from_status is null or from_status in (
      'ContactVerificationPending','IdentityReviewPending',
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
      'SyntheticClinicalInformationInventoryRecorded','VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending',
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
      'SyntheticClinicalInformationInventoryRecorded','VerificationLocked','Expired'));

create table if not exists telehealth_applicant_clinical_information_inventories (
  inventory_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  promotion_id uuid not null unique
    references telehealth_applicant_synthetic_promotions(promotion_id),
  canonical_patient_id text not null unique references patients(canonical_id),
  registration_details_confirmation_id uuid not null unique
    references telehealth_applicant_registration_details_confirmations(confirmation_id),
  insurance_handoff_confirmation_id uuid not null unique
    references telehealth_applicant_insurance_handoff_confirmations(confirmation_id),
  safety_evaluation_id uuid not null unique
    references telehealth_applicant_safety_triage_evaluations(evaluation_id),
  communication_access_readiness_id uuid not null unique
    references telehealth_applicant_communication_access_readiness(readiness_id),
  device_preparation_id uuid not null unique
    references telehealth_applicant_device_preparations(preparation_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  inventory_snapshot_fingerprint character(64) not null,
  preparation_snapshot_fingerprint character(64) not null,
  medications_status text not null,
  allergies_or_intolerances_status text not null,
  other_health_history_status text not null,
  review_route text not null,
  patient_reported_may_be_incomplete_acknowledged boolean not null,
  no_clinical_details_captured_acknowledged boolean not null,
  clinician_reconciliation_required_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  medication_list_reconciled boolean not null default false,
  allergy_list_reconciled boolean not null default false,
  health_history_reconciled boolean not null default false,
  clinical_intake_completed boolean not null default false,
  clinical_eligibility_established boolean not null default false,
  clinician_review_created boolean not null default false,
  patient_record_changed boolean not null default false,
  request_created boolean not null default false,
  queue_entered boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  constraint uq_telehealth_clinical_inventory_practice_key
    unique(practice_id,idempotency_key),
  constraint chk_telehealth_clinical_inventory_result check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticClinicalInformationInventoryRecorded'),
  constraint chk_telehealth_clinical_inventory_hashes check (
    inventory_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and preparation_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_clinical_inventory_statuses check (
    medications_status in ('PatientReportsNone','ItemsToReview','Unsure')
    and allergies_or_intolerances_status in ('PatientReportsNone','ItemsToReview','Unsure')
    and other_health_history_status in ('PatientReportsNone','ItemsToReview','Unsure')),
  constraint chk_telehealth_clinical_inventory_route check (
    review_route = case
      when 'ItemsToReview' in (
        medications_status,allergies_or_intolerances_status,other_health_history_status)
        then 'DetailedCollectionRequired'
      when 'Unsure' in (
        medications_status,allergies_or_intolerances_status,other_health_history_status)
        then 'AssistedReviewRequired'
      else 'PendingClinicianReconciliation'
    end),
  constraint chk_telehealth_clinical_inventory_acknowledgments check (
    patient_reported_may_be_incomplete_acknowledged
    and no_clinical_details_captured_acknowledged
    and clinician_reconciliation_required_acknowledged),
  constraint chk_telehealth_clinical_inventory_policy check (
    policy_key='SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_CLINICAL_INFORMATION_INVENTORY_RECEIPT'),
  constraint chk_telehealth_clinical_inventory_expiry check (
    recorded_at <= applicant_expires_at),
  constraint chk_telehealth_clinical_inventory_no_consequence check (
    not medication_list_reconciled
    and not allergy_list_reconciled
    and not health_history_reconciled
    and not clinical_intake_completed
    and not clinical_eligibility_established
    and not clinician_review_created
    and not patient_record_changed
    and not request_created
    and not queue_entered
    and not care_authorized
    and not prescribing_enabled)
);

create or replace function enforce_telehealth_applicant_clinical_information_inventory()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  patient_row patients%rowtype;
  preparation_row telehealth_applicant_device_preparations%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants where applicant_id=new.applicant_id;
  select * into promotion_row
  from telehealth_applicant_synthetic_promotions where promotion_id=new.promotion_id;
  select * into patient_row
  from patients where canonical_id=new.canonical_patient_id;
  select * into preparation_row
  from telehealth_applicant_device_preparations
  where preparation_id=new.device_preparation_id;

  if applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at
     or applicant_row.expires_at<=now() then
    raise exception using errcode='23514',
      message='telehealth_clinical_inventory_applicant_mismatch';
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
     or exists(select 1 from insurance_records insurance
               where lower(insurance.patient_id)=lower(new.canonical_patient_id)) then
    raise exception using errcode='23514',
      message='telehealth_clinical_inventory_patient_mismatch';
  end if;

  if preparation_row.applicant_id<>new.applicant_id
     or preparation_row.practice_id<>new.practice_id
     or preparation_row.facility_id<>new.facility_id
     or preparation_row.promotion_id<>new.promotion_id
     or preparation_row.canonical_patient_id<>new.canonical_patient_id
     or preparation_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or preparation_row.insurance_handoff_confirmation_id<>new.insurance_handoff_confirmation_id
     or preparation_row.safety_evaluation_id<>new.safety_evaluation_id
     or preparation_row.communication_access_readiness_id<>new.communication_access_readiness_id
     or preparation_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or preparation_row.resulting_applicant_status<>'SyntheticDevicePreparationRecorded'
     or preparation_row.preparation_snapshot_fingerprint<>new.preparation_snapshot_fingerprint
     or not preparation_row.browser_supported
     or not preparation_row.camera_available
     or not preparation_row.microphone_available
     or not preparation_row.speaker_available
     or preparation_row.network_quality not in ('Unknown','Good')
     or not preparation_row.client_reported_result_acknowledged
     or not preparation_row.no_readiness_guarantee_acknowledged
     or not preparation_row.recheck_before_consultation_acknowledged
     or preparation_row.policy_key<>'SYNTHETIC_APPLICANT_DEVICE_PREPARATION'
     or preparation_row.policy_version<>1
     or preparation_row.evidence_type<>'PROMOTED_PATIENT_DEVICE_PREPARATION_RECEIPT'
     or preparation_row.technology_ready
     or preparation_row.waiting_room_created
     or preparation_row.media_session_created
     or preparation_row.communication_started
     or preparation_row.support_arrangement_completed
     or preparation_row.patient_record_changed
     or preparation_row.portal_access_enabled
     or preparation_row.intake_completed
     or preparation_row.legal_consent_established
     or preparation_row.practice_accepted
     or preparation_row.financial_record_created
     or preparation_row.request_created
     or preparation_row.queue_entered
     or preparation_row.appointment_created
     or preparation_row.encounter_created
     or preparation_row.care_authorized
     or preparation_row.prescribing_enabled
     or preparation_row.billing_enabled
     or preparation_row.claim_created
     or preparation_row.integration_enabled
     or preparation_row.external_call_performed then
    raise exception using errcode='23514',
      message='telehealth_clinical_inventory_preparation_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_clinical_inventory_guard
  on telehealth_applicant_clinical_information_inventories;
create trigger trg_telehealth_applicant_clinical_inventory_guard
before insert on telehealth_applicant_clinical_information_inventories
for each row execute function enforce_telehealth_applicant_clinical_information_inventory();

drop trigger if exists trg_telehealth_applicant_clinical_inventory_append_only
  on telehealth_applicant_clinical_information_inventories;
create trigger trg_telehealth_applicant_clinical_inventory_append_only
before update or delete on telehealth_applicant_clinical_information_inventories
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_clinical_inventory_practice
  on telehealth_applicant_clinical_information_inventories(
    practice_id,facility_id,recorded_at);
