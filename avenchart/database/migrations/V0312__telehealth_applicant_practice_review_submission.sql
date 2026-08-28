-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Creates one synthetic practice-intake review work item. It is deliberately
-- separate from telehealth requests and patient/clinician care queues.

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
               'SyntheticPracticeReviewSubmitted',
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
                'SyntheticPreRequestReadinessAcknowledged',
                'SyntheticPracticeReviewSubmitted')
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
               'prospective-pre-request-readiness-acknowledged',
               'prospective-practice-review-submitted'));

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
      'SyntheticPracticeReviewSubmitted','VerificationLocked','Expired'))
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
      'SyntheticPracticeReviewSubmitted','VerificationLocked','Expired'));

create table if not exists telehealth_prospective_practice_review_cases (
  case_id uuid primary key,
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  readiness_acknowledgment_id uuid not null unique
    references telehealth_applicant_pre_request_readiness_acknowledgments(acknowledgment_id),
  readiness_snapshot_fingerprint character(64) not null,
  review_route text not null,
  case_status text not null,
  applicant_expires_at timestamptz not null,
  created_at timestamptz not null default now(),
  constraint chk_telehealth_practice_review_case_hash check (
    readiness_snapshot_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_practice_review_case_route check (
    review_route in ('AdditionalClinicalInformationRequired',
                     'AssistedPreRequestSupportRequired',
                     'PendingPracticePreRequestReview')),
  constraint chk_telehealth_practice_review_case_status check (
    case_status='PendingPracticeReview'),
  constraint chk_telehealth_practice_review_case_expiry check (
    created_at <= applicant_expires_at)
);

create table if not exists telehealth_applicant_practice_review_submissions (
  submission_id uuid primary key,
  case_id uuid not null unique references telehealth_prospective_practice_review_cases(case_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  readiness_acknowledgment_id uuid not null unique
    references telehealth_applicant_pre_request_readiness_acknowledgments(acknowledgment_id),
  readiness_snapshot_fingerprint character(64) not null,
  review_route text not null,
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  practice_review_snapshot_fingerprint character(64) not null,
  patient_reported_information_acknowledged boolean not null,
  practice_may_request_information_or_decline_acknowledged boolean not null,
  no_telehealth_request_or_care_queue_acknowledged boolean not null,
  worsening_symptoms_require_immediate_action_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  submitted_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  staff_review_created boolean not null default true,
  clinician_review_created boolean not null default false,
  practice_accepted boolean not null default false,
  patient_record_changed boolean not null default false,
  telehealth_request_created boolean not null default false,
  patient_care_queue_entered boolean not null default false,
  clinician_queue_entered boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  billing_enabled boolean not null default false,
  claim_created boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  constraint uq_telehealth_practice_review_submission_idempotency
    unique(applicant_id,idempotency_key),
  constraint chk_telehealth_practice_review_submission_result check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticPracticeReviewSubmitted'),
  constraint chk_telehealth_practice_review_submission_hashes check (
    readiness_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and practice_review_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_practice_review_submission_route check (
    review_route in ('AdditionalClinicalInformationRequired',
                     'AssistedPreRequestSupportRequired',
                     'PendingPracticePreRequestReview')),
  constraint chk_telehealth_practice_review_submission_acknowledgments check (
    patient_reported_information_acknowledged
    and practice_may_request_information_or_decline_acknowledged
    and no_telehealth_request_or_care_queue_acknowledged
    and worsening_symptoms_require_immediate_action_acknowledged),
  constraint chk_telehealth_practice_review_submission_policy check (
    policy_key='SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_PRACTICE_REVIEW_SUBMISSION_RECEIPT'),
  constraint chk_telehealth_practice_review_submission_expiry check (
    submitted_at <= applicant_expires_at),
  constraint chk_telehealth_practice_review_submission_consequences check (
    staff_review_created
    and not clinician_review_created
    and not practice_accepted
    and not patient_record_changed
    and not telehealth_request_created
    and not patient_care_queue_entered
    and not clinician_queue_entered
    and not appointment_created
    and not encounter_created
    and not care_authorized
    and not prescribing_enabled
    and not billing_enabled
    and not claim_created
    and not integration_enabled
    and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_practice_review_submission()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  readiness_row telehealth_applicant_pre_request_readiness_acknowledgments%rowtype;
  case_row telehealth_prospective_practice_review_cases%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id;
  select * into readiness_row from telehealth_applicant_pre_request_readiness_acknowledgments
  where acknowledgment_id=new.readiness_acknowledgment_id;
  select * into case_row from telehealth_prospective_practice_review_cases
  where case_id=new.case_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at
     or applicant_row.expires_at<=now() then
    raise exception using errcode='23514',
      message='telehealth_practice_review_applicant_mismatch';
  end if;

  if readiness_row.acknowledgment_id is null
     or readiness_row.applicant_id<>new.applicant_id
     or readiness_row.practice_id<>new.practice_id
     or readiness_row.facility_id<>new.facility_id
     or readiness_row.canonical_patient_id<>new.canonical_patient_id
     or readiness_row.resulting_applicant_status<>'SyntheticPreRequestReadinessAcknowledged'
     or readiness_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or readiness_row.pre_request_readiness_snapshot_fingerprint<>new.readiness_snapshot_fingerprint
     or readiness_row.overall_route<>new.review_route then
    raise exception using errcode='23514',
      message='telehealth_practice_review_readiness_mismatch';
  end if;

  if case_row.case_id is null
     or case_row.applicant_id<>new.applicant_id
     or case_row.practice_id<>new.practice_id
     or case_row.facility_id<>new.facility_id
     or case_row.canonical_patient_id<>new.canonical_patient_id
     or case_row.readiness_acknowledgment_id<>new.readiness_acknowledgment_id
     or case_row.readiness_snapshot_fingerprint<>new.readiness_snapshot_fingerprint
     or case_row.review_route<>new.review_route
     or case_row.case_status<>'PendingPracticeReview'
     or case_row.applicant_expires_at<>new.applicant_expires_at then
    raise exception using errcode='23514',
      message='telehealth_practice_review_case_mismatch';
  end if;

  if patient_row.canonical_id is null
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
      message='telehealth_practice_review_patient_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_enforce_telehealth_applicant_practice_review_submission
  on telehealth_applicant_practice_review_submissions;
create trigger trg_enforce_telehealth_applicant_practice_review_submission
before insert on telehealth_applicant_practice_review_submissions
for each row execute function enforce_telehealth_applicant_practice_review_submission();

drop trigger if exists trg_telehealth_practice_review_cases_append_only
  on telehealth_prospective_practice_review_cases;
create trigger trg_telehealth_practice_review_cases_append_only
before update or delete on telehealth_prospective_practice_review_cases
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_practice_review_submissions_append_only
  on telehealth_applicant_practice_review_submissions;
create trigger trg_telehealth_practice_review_submissions_append_only
before update or delete on telehealth_applicant_practice_review_submissions
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_practice_review_cases_scope_status_created
  on telehealth_prospective_practice_review_cases(practice_id,facility_id,case_status,created_at,case_id);
