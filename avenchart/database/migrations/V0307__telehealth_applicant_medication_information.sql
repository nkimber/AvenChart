-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Adds one immutable, patient-reported synthetic medication-information
-- receipt and zero or more immutable selections from a fixed local catalog.
-- This is not a MedicationStatement, MedicationRequest, canonical medication
-- list, reconciliation, interaction check, clinical task, or care capability.

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
                'SyntheticMedicationInformationRecorded')
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
               'prospective-medication-information-recorded'));

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
      'SyntheticClinicalInformationInventoryRecorded',
      'SyntheticMedicationInformationRecorded','VerificationLocked','Expired'))
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
      'SyntheticClinicalInformationInventoryRecorded',
      'SyntheticMedicationInformationRecorded','VerificationLocked','Expired'));

create table if not exists telehealth_applicant_medication_information_receipts (
  receipt_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  promotion_id uuid not null unique
    references telehealth_applicant_synthetic_promotions(promotion_id),
  canonical_patient_id text not null unique references patients(canonical_id),
  clinical_inventory_id uuid not null unique
    references telehealth_applicant_clinical_information_inventories(inventory_id),
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
  medication_information_snapshot_fingerprint character(64) not null,
  inventory_snapshot_fingerprint character(64) not null,
  inventory_medications_status text not null,
  selected_item_count integer not null,
  additional_or_unlisted_items_reported boolean not null,
  review_route text not null,
  patient_reported_may_be_incomplete_acknowledged boolean not null,
  synthetic_catalog_incomplete_acknowledged boolean not null,
  no_dose_or_directions_captured_acknowledged boolean not null,
  clinician_reconciliation_required_acknowledged boolean not null,
  catalog_key text not null,
  catalog_version integer not null,
  coding_system text not null,
  catalog_complete boolean not null default false,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  medication_statement_created boolean not null default false,
  medication_request_created boolean not null default false,
  medication_list_reconciled boolean not null default false,
  interaction_check_performed boolean not null default false,
  clinician_review_created boolean not null default false,
  clinical_intake_completed boolean not null default false,
  clinical_eligibility_established boolean not null default false,
  patient_record_changed boolean not null default false,
  request_created boolean not null default false,
  queue_entered boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  constraint uq_telehealth_medication_information_practice_key
    unique(practice_id,idempotency_key),
  constraint chk_telehealth_medication_information_result check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticMedicationInformationRecorded'),
  constraint chk_telehealth_medication_information_hashes check (
    medication_information_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and inventory_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_medication_information_inventory_status check (
    inventory_medications_status in ('PatientReportsNone','ItemsToReview','Unsure')),
  constraint chk_telehealth_medication_information_count check (
    selected_item_count between 0 and 6),
  constraint chk_telehealth_medication_information_branch check (
    (inventory_medications_status='ItemsToReview')
    or (selected_item_count=0 and not additional_or_unlisted_items_reported)),
  constraint chk_telehealth_medication_information_route check (
    review_route = case
      when additional_or_unlisted_items_reported
        then 'AdditionalMedicationCollectionRequired'
      when inventory_medications_status='ItemsToReview'
        then 'ClinicianMedicationReviewRequired'
      when inventory_medications_status='Unsure'
        then 'AssistedMedicationReviewRequired'
      else 'PendingClinicianConfirmationOfNone'
    end),
  constraint chk_telehealth_medication_information_acknowledgments check (
    patient_reported_may_be_incomplete_acknowledged
    and synthetic_catalog_incomplete_acknowledged
    and no_dose_or_directions_captured_acknowledged
    and clinician_reconciliation_required_acknowledged),
  constraint chk_telehealth_medication_information_catalog check (
    catalog_key='avenchart-synthetic-applicant-medication-ingredients-2026-08'
    and catalog_version=1
    and coding_system='LOCAL_SYNTHETIC_ONLY'
    and not catalog_complete),
  constraint chk_telehealth_medication_information_policy check (
    policy_key='SYNTHETIC_APPLICANT_MEDICATION_INFORMATION'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_MEDICATION_INFORMATION_RECEIPT'),
  constraint chk_telehealth_medication_information_expiry check (
    recorded_at <= applicant_expires_at),
  constraint chk_telehealth_medication_information_no_consequence check (
    not medication_statement_created
    and not medication_request_created
    and not medication_list_reconciled
    and not interaction_check_performed
    and not clinician_review_created
    and not clinical_intake_completed
    and not clinical_eligibility_established
    and not patient_record_changed
    and not request_created
    and not queue_entered
    and not care_authorized
    and not prescribing_enabled)
);

create table if not exists telehealth_applicant_reported_medication_items (
  item_id uuid primary key,
  receipt_id uuid not null
    references telehealth_applicant_medication_information_receipts(receipt_id),
  applicant_id uuid not null references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  item_ordinal integer not null,
  catalog_key text not null,
  display_name text not null,
  catalog_version integer not null,
  coding_system text not null,
  rxnorm_mapped boolean not null default false,
  reported_use_status text not null,
  recorded_at timestamptz not null default now(),
  constraint uq_telehealth_reported_medication_item_ordinal
    unique(receipt_id,item_ordinal),
  constraint uq_telehealth_reported_medication_item_catalog
    unique(receipt_id,catalog_key),
  constraint chk_telehealth_reported_medication_item_ordinal check (
    item_ordinal between 1 and 6),
  constraint chk_telehealth_reported_medication_item_catalog check (
    (catalog_key,display_name) in (
      ('acetaminophen','Acetaminophen'),
      ('ibuprofen','Ibuprofen'),
      ('sumatriptan','Sumatriptan'),
      ('melatonin','Melatonin'),
      ('lisinopril','Lisinopril'),
      ('metformin','Metformin'))
    and catalog_version=1
    and coding_system='LOCAL_SYNTHETIC_ONLY'
    and not rxnorm_mapped),
  constraint chk_telehealth_reported_medication_item_use_status check (
    reported_use_status in ('Taking','NotTaking','Unsure'))
);

create or replace function enforce_telehealth_applicant_medication_information()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  patient_row patients%rowtype;
  inventory_row telehealth_applicant_clinical_information_inventories%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants where applicant_id=new.applicant_id;
  select * into promotion_row
  from telehealth_applicant_synthetic_promotions where promotion_id=new.promotion_id;
  select * into patient_row
  from patients where canonical_id=new.canonical_patient_id;
  select * into inventory_row
  from telehealth_applicant_clinical_information_inventories
  where inventory_id=new.clinical_inventory_id;

  if applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at
     or applicant_row.expires_at<=now() then
    raise exception using errcode='23514',
      message='telehealth_medication_information_applicant_mismatch';
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
               where lower(insurance.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from medications medication
               where lower(medication.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from prescriptions prescription
               where lower(prescription.patient_id)=lower(new.canonical_patient_id)) then
    raise exception using errcode='23514',
      message='telehealth_medication_information_patient_mismatch';
  end if;

  if inventory_row.applicant_id<>new.applicant_id
     or inventory_row.practice_id<>new.practice_id
     or inventory_row.facility_id<>new.facility_id
     or inventory_row.promotion_id<>new.promotion_id
     or inventory_row.canonical_patient_id<>new.canonical_patient_id
     or inventory_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or inventory_row.insurance_handoff_confirmation_id<>new.insurance_handoff_confirmation_id
     or inventory_row.safety_evaluation_id<>new.safety_evaluation_id
     or inventory_row.communication_access_readiness_id<>new.communication_access_readiness_id
     or inventory_row.device_preparation_id<>new.device_preparation_id
     or inventory_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or inventory_row.resulting_applicant_status<>'SyntheticClinicalInformationInventoryRecorded'
     or inventory_row.inventory_snapshot_fingerprint<>new.inventory_snapshot_fingerprint
     or inventory_row.medications_status<>new.inventory_medications_status
     or inventory_row.medications_status not in ('PatientReportsNone','ItemsToReview','Unsure')
     or inventory_row.policy_key<>'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY'
     or inventory_row.policy_version<>1
     or inventory_row.evidence_type<>'PROMOTED_PATIENT_CLINICAL_INFORMATION_INVENTORY_RECEIPT'
     or not inventory_row.patient_reported_may_be_incomplete_acknowledged
     or not inventory_row.no_clinical_details_captured_acknowledged
     or not inventory_row.clinician_reconciliation_required_acknowledged
     or inventory_row.medication_list_reconciled
     or inventory_row.clinical_intake_completed
     or inventory_row.clinical_eligibility_established
     or inventory_row.clinician_review_created
     or inventory_row.patient_record_changed
     or inventory_row.request_created
     or inventory_row.queue_entered
     or inventory_row.care_authorized
     or inventory_row.prescribing_enabled then
    raise exception using errcode='23514',
      message='telehealth_medication_information_inventory_mismatch';
  end if;

  return new;
end;
$$;

create or replace function enforce_telehealth_reported_medication_item_provenance()
returns trigger
language plpgsql
as $$
declare
  receipt_row telehealth_applicant_medication_information_receipts%rowtype;
begin
  select * into receipt_row
  from telehealth_applicant_medication_information_receipts
  where receipt_id=new.receipt_id;
  if receipt_row.receipt_id is null
     or receipt_row.applicant_id<>new.applicant_id
     or receipt_row.practice_id<>new.practice_id
     or receipt_row.facility_id<>new.facility_id then
    raise exception using errcode='23514',
      message='telehealth_reported_medication_item_provenance_mismatch';
  end if;
  return new;
end;
$$;

create or replace function enforce_telehealth_medication_information_item_count()
returns trigger
language plpgsql
as $$
declare
  target_receipt_id uuid := coalesce(new.receipt_id,old.receipt_id);
  receipt_row telehealth_applicant_medication_information_receipts%rowtype;
  actual_count integer;
begin
  select * into receipt_row
  from telehealth_applicant_medication_information_receipts
  where receipt_id=target_receipt_id;
  if receipt_row.receipt_id is null then
    return null;
  end if;
  select count(*) into actual_count
  from telehealth_applicant_reported_medication_items
  where receipt_id=target_receipt_id;
  if actual_count<>receipt_row.selected_item_count
     or (receipt_row.inventory_medications_status='ItemsToReview'
         and actual_count=0
         and not receipt_row.additional_or_unlisted_items_reported)
     or (receipt_row.inventory_medications_status<>'ItemsToReview'
         and actual_count<>0) then
    raise exception using errcode='23514',
      message='telehealth_medication_information_item_count_mismatch';
  end if;
  return null;
end;
$$;

drop trigger if exists trg_telehealth_medication_information_guard
  on telehealth_applicant_medication_information_receipts;
create trigger trg_telehealth_medication_information_guard
before insert on telehealth_applicant_medication_information_receipts
for each row execute function enforce_telehealth_applicant_medication_information();

drop trigger if exists trg_telehealth_reported_medication_item_guard
  on telehealth_applicant_reported_medication_items;
create trigger trg_telehealth_reported_medication_item_guard
before insert on telehealth_applicant_reported_medication_items
for each row execute function enforce_telehealth_reported_medication_item_provenance();

drop trigger if exists trg_telehealth_medication_information_count_parent
  on telehealth_applicant_medication_information_receipts;
create constraint trigger trg_telehealth_medication_information_count_parent
after insert on telehealth_applicant_medication_information_receipts
deferrable initially deferred
for each row execute function enforce_telehealth_medication_information_item_count();

drop trigger if exists trg_telehealth_medication_information_count_items
  on telehealth_applicant_reported_medication_items;
create constraint trigger trg_telehealth_medication_information_count_items
after insert or update or delete on telehealth_applicant_reported_medication_items
deferrable initially deferred
for each row execute function enforce_telehealth_medication_information_item_count();

drop trigger if exists trg_telehealth_medication_information_append_only
  on telehealth_applicant_medication_information_receipts;
create trigger trg_telehealth_medication_information_append_only
before update or delete on telehealth_applicant_medication_information_receipts
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_reported_medication_item_append_only
  on telehealth_applicant_reported_medication_items;
create trigger trg_telehealth_reported_medication_item_append_only
before update or delete on telehealth_applicant_reported_medication_items
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_medication_information_practice
  on telehealth_applicant_medication_information_receipts(
    practice_id,facility_id,recorded_at);

create index if not exists ix_telehealth_reported_medication_items_receipt
  on telehealth_applicant_reported_medication_items(receipt_id,item_ordinal);
