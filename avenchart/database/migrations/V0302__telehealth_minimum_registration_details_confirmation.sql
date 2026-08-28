-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0028: one applicant-owned, no-edit confirmation of the minimum
-- registration details copied into a promoted portal-disabled synthetic patient
-- shell. This creates no identity assurance, complete intake, insurance
-- confirmation, request, queue, or care capability.

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
                'SyntheticMinimumRegistrationDetailsConfirmed')
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
               'prospective-minimum-registration-details-confirmed'));

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
      'VerificationLocked','Expired'))
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
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_registration_details_confirmations (
  confirmation_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  notice_acknowledgment_id uuid not null unique
    references telehealth_applicant_notice_acknowledgments(acknowledgment_id),
  promotion_id uuid not null unique
    references telehealth_applicant_synthetic_promotions(promotion_id),
  canonical_patient_id text not null unique references patients(canonical_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  details_fingerprint character(64) not null,
  legal_name_birth_date_confirmed boolean not null,
  contact_channels_confirmed boolean not null,
  residence_region_confirmed boolean not null,
  no_corrections_needed_confirmed boolean not null,
  synthetic_data_confirmed boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  confirmed_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  identity_assurance_established boolean not null default false,
  patient_record_changed boolean not null default false,
  correction_completed boolean not null default false,
  intake_completed boolean not null default false,
  legal_consent_established boolean not null default false,
  practice_accepted boolean not null default false,
  insurance_confirmed boolean not null default false,
  coverage_created boolean not null default false,
  financial_record_created boolean not null default false,
  request_created boolean not null default false,
  queue_enabled boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  care_enabled boolean not null default false,
  prescribing_enabled boolean not null default false,
  claim_created boolean not null default false,
  communication_enabled boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  constraint uq_telehealth_registration_details_practice_key
    unique(practice_id,idempotency_key),
  constraint chk_telehealth_registration_details_result check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticMinimumRegistrationDetailsConfirmed'),
  constraint chk_telehealth_registration_details_fingerprints check (
    details_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_registration_details_affirmations check (
    legal_name_birth_date_confirmed
    and contact_channels_confirmed
    and residence_region_confirmed
    and no_corrections_needed_confirmed
    and synthetic_data_confirmed),
  constraint chk_telehealth_registration_details_policy check (
    policy_key='SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_MINIMUM_DETAILS_NO_EDIT_CONFIRMATION'),
  constraint chk_telehealth_registration_details_expiry check (
    confirmed_at < applicant_expires_at),
  constraint chk_telehealth_registration_details_no_consequence check (
    not identity_assurance_established
    and not patient_record_changed
    and not correction_completed
    and not intake_completed
    and not legal_consent_established
    and not practice_accepted
    and not insurance_confirmed
    and not coverage_created
    and not financial_record_created
    and not request_created
    and not queue_enabled
    and not appointment_created
    and not encounter_created
    and not care_enabled
    and not prescribing_enabled
    and not claim_created
    and not communication_enabled
    and not integration_enabled
    and not external_call_performed)
);

create or replace function enforce_telehealth_registration_details_confirmation()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  notice_row telehealth_applicant_notice_acknowledgments%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants
  where applicant_id=new.applicant_id;

  if applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at then
    raise exception using
      errcode='23514',
      message='telehealth_registration_details_applicant_mismatch';
  end if;

  select * into promotion_row
  from telehealth_applicant_synthetic_promotions
  where promotion_id=new.promotion_id
    and applicant_id=new.applicant_id;

  if promotion_row.promotion_id is null
     or promotion_row.practice_id<>new.practice_id
     or promotion_row.facility_id<>new.facility_id
     or promotion_row.outcome<>'SyntheticPatientCreated'
     or not promotion_row.canonical_patient_created
     or promotion_row.canonical_patient_id<>new.canonical_patient_id then
    raise exception using
      errcode='23514',
      message='telehealth_registration_details_promotion_mismatch';
  end if;

  select * into notice_row
  from telehealth_applicant_notice_acknowledgments
  where acknowledgment_id=new.notice_acknowledgment_id
    and applicant_id=new.applicant_id;

  if notice_row.acknowledgment_id is null
     or notice_row.practice_id<>new.practice_id
     or notice_row.facility_id<>new.facility_id
     or notice_row.promotion_id<>new.promotion_id
     or notice_row.canonical_patient_id<>new.canonical_patient_id
     or notice_row.resulting_applicant_status<>'SyntheticTelehealthNoticeAcknowledged'
     or notice_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or notice_row.acknowledged_at>new.confirmed_at then
    raise exception using
      errcode='23514',
      message='telehealth_registration_details_notice_mismatch';
  end if;

  select * into patient_row
  from patients
  where canonical_id=new.canonical_patient_id;

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
     or patient_row.postal_code<>applicant_row.postal_code then
    raise exception using
      errcode='23514',
      message='telehealth_registration_details_patient_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_registration_details_confirmation_guard
  on telehealth_applicant_registration_details_confirmations;
create trigger trg_telehealth_registration_details_confirmation_guard
before insert on telehealth_applicant_registration_details_confirmations
for each row execute function enforce_telehealth_registration_details_confirmation();

drop trigger if exists trg_telehealth_registration_details_confirmations_append_only
  on telehealth_applicant_registration_details_confirmations;
create trigger trg_telehealth_registration_details_confirmations_append_only
before update or delete on telehealth_applicant_registration_details_confirmations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_registration_details_queue
  on telehealth_prospective_applicants(practice_id,facility_id,status,updated_at,applicant_id)
  where status='SyntheticMinimumRegistrationDetailsConfirmed';
