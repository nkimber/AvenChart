-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0030: one applicant-owned synthetic communication/access-readiness
-- receipt. Preferences are not arrangements and create no patient, support,
-- communication, request, queue, or care capability.

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
                'SyntheticCommunicationAccessReadinessRecorded')
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
               'prospective-communication-access-readiness-recorded'));

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
      'SyntheticInsuranceDetailsConfirmed',
      'SyntheticCommunicationAccessReadinessRecorded',
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_communication_access_readiness (
  readiness_id uuid primary key,
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
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  context_snapshot_fingerprint character(64) not null,
  current_location_state_code character(2) not null,
  callback_phone_last4 character(4) not null,
  preferred_spoken_language text not null,
  interpreter_requested boolean not null,
  accessibility_support_requested boolean not null,
  current_location_confirmed boolean not null,
  callback_number_confirmed boolean not null,
  safe_private_communication_confirmed boolean not null,
  disconnection_emergency_plan_acknowledged boolean not null,
  synthetic_data_confirmed boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  interpreter_assigned boolean not null default false,
  accessibility_accommodation_arranged boolean not null default false,
  communication_arrangement_completed boolean not null default false,
  support_request_created boolean not null default false,
  technology_readiness_completed boolean not null default false,
  patient_record_changed boolean not null default false,
  portal_access_enabled boolean not null default false,
  intake_completed boolean not null default false,
  legal_consent_established boolean not null default false,
  practice_accepted boolean not null default false,
  financial_record_created boolean not null default false,
  request_created boolean not null default false,
  queue_enabled boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  care_enabled boolean not null default false,
  prescribing_enabled boolean not null default false,
  billing_enabled boolean not null default false,
  claim_created boolean not null default false,
  communication_enabled boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  constraint uq_telehealth_communication_access_practice_key
    unique(practice_id,idempotency_key),
  constraint chk_telehealth_communication_access_result check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticCommunicationAccessReadinessRecorded'),
  constraint chk_telehealth_communication_access_hashes check (
    context_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_communication_access_context check (
    current_location_state_code in ('GA','CA','FL')
    and callback_phone_last4 ~ '^[0-9]{4}$'),
  constraint chk_telehealth_communication_access_language check (
    preferred_spoken_language in ('English','Spanish')),
  constraint chk_telehealth_communication_access_affirmations check (
    current_location_confirmed
    and callback_number_confirmed
    and safe_private_communication_confirmed
    and disconnection_emergency_plan_acknowledged
    and synthetic_data_confirmed),
  constraint chk_telehealth_communication_access_policy check (
    policy_key='SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_COMMUNICATION_ACCESS_READINESS_RECEIPT'),
  constraint chk_telehealth_communication_access_expiry check (
    recorded_at <= applicant_expires_at),
  constraint chk_telehealth_communication_access_no_consequence check (
    not interpreter_assigned
    and not accessibility_accommodation_arranged
    and not communication_arrangement_completed
    and not support_request_created
    and not technology_readiness_completed
    and not patient_record_changed
    and not portal_access_enabled
    and not intake_completed
    and not legal_consent_established
    and not practice_accepted
    and not financial_record_created
    and not request_created
    and not queue_enabled
    and not appointment_created
    and not encounter_created
    and not care_enabled
    and not prescribing_enabled
    and not billing_enabled
    and not claim_created
    and not communication_enabled
    and not integration_enabled
    and not external_call_performed)
);

create or replace function enforce_telehealth_communication_access_readiness()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  patient_row patients%rowtype;
  registration_row telehealth_applicant_registration_details_confirmations%rowtype;
  handoff_row telehealth_applicant_insurance_handoff_confirmations%rowtype;
  safety_row telehealth_applicant_safety_triage_evaluations%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants where applicant_id=new.applicant_id;
  select * into promotion_row
  from telehealth_applicant_synthetic_promotions where promotion_id=new.promotion_id;
  select * into patient_row
  from patients where canonical_id=new.canonical_patient_id;
  select * into registration_row
  from telehealth_applicant_registration_details_confirmations
  where confirmation_id=new.registration_details_confirmation_id;
  select * into handoff_row
  from telehealth_applicant_insurance_handoff_confirmations
  where confirmation_id=new.insurance_handoff_confirmation_id;
  select * into safety_row
  from telehealth_applicant_safety_triage_evaluations
  where evaluation_id=new.safety_evaluation_id;

  if applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at
     or applicant_row.expires_at<=now() then
    raise exception using errcode='23514',message='telehealth_communication_access_applicant_mismatch';
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
    raise exception using errcode='23514',message='telehealth_communication_access_patient_mismatch';
  end if;

  if registration_row.applicant_id<>new.applicant_id
     or registration_row.practice_id<>new.practice_id
     or registration_row.facility_id<>new.facility_id
     or registration_row.promotion_id<>new.promotion_id
     or registration_row.canonical_patient_id<>new.canonical_patient_id
     or handoff_row.applicant_id<>new.applicant_id
     or handoff_row.practice_id<>new.practice_id
     or handoff_row.facility_id<>new.facility_id
     or handoff_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or handoff_row.promotion_id<>new.promotion_id
     or handoff_row.canonical_patient_id<>new.canonical_patient_id
     or handoff_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or handoff_row.resulting_applicant_status<>'SyntheticInsuranceDetailsConfirmed'
     or handoff_row.policy_key<>'SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
     or handoff_row.policy_version<>1
     or handoff_row.coverage_verified
     or handoff_row.exact_network_confirmed
     or handoff_row.canonical_coverage_created
     or handoff_row.patient_record_changed
     or handoff_row.portal_access_enabled
     or handoff_row.intake_completed
     or handoff_row.legal_consent_established
     or handoff_row.practice_accepted
     or handoff_row.request_created
     or handoff_row.queue_enabled
     or handoff_row.care_enabled then
    raise exception using errcode='23514',message='telehealth_communication_access_handoff_mismatch';
  end if;

  if safety_row.applicant_id<>new.applicant_id
     or safety_row.practice_id<>new.practice_id
     or safety_row.facility_id<>new.facility_id
     or safety_row.outcome<>'TelehealthEligible'
     or safety_row.resulting_applicant_status<>'SafetyScreenPassed'
     or not safety_row.current_location_confirmed
     or safety_row.current_location_state_code<>new.current_location_state_code
     or right(regexp_replace(applicant_row.phone,'[^0-9]','','g'),4)<>new.callback_phone_last4 then
    raise exception using errcode='23514',message='telehealth_communication_access_context_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_communication_access_readiness_guard
  on telehealth_applicant_communication_access_readiness;
create trigger trg_telehealth_communication_access_readiness_guard
before insert on telehealth_applicant_communication_access_readiness
for each row execute function enforce_telehealth_communication_access_readiness();

drop trigger if exists trg_telehealth_communication_access_readiness_append_only
  on telehealth_applicant_communication_access_readiness;
create trigger trg_telehealth_communication_access_readiness_append_only
before update or delete on telehealth_applicant_communication_access_readiness
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_communication_access_readiness_practice
  on telehealth_applicant_communication_access_readiness(practice_id,facility_id,recorded_at);
