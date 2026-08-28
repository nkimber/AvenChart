-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0029: one applicant-owned, no-edit confirmation of a masked
-- synthetic insurance handoff. This creates no canonical coverage, exact
-- network result, financial record, request, queue, or care capability.

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
                'SyntheticInsuranceDetailsConfirmed')
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
               'prospective-insurance-handoff-confirmed'));

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
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_insurance_handoff_confirmations (
  confirmation_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  registration_details_confirmation_id uuid not null unique
    references telehealth_applicant_registration_details_confirmations(confirmation_id),
  promotion_id uuid not null unique
    references telehealth_applicant_synthetic_promotions(promotion_id),
  canonical_patient_id text not null unique references patients(canonical_id),
  member_insurance_details_id uuid not null unique
    references telehealth_applicant_member_insurance_details(details_id),
  eligibility_result_id uuid not null unique
    references telehealth_applicant_eligibility_results(eligibility_result_id),
  network_determination_id uuid not null unique
    references telehealth_applicant_practice_network_determinations(network_determination_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  insurance_snapshot_fingerprint character(64) not null,
  payer_display_name text not null,
  product_display_name text not null,
  member_id_last4 character(4) not null,
  group_number_present boolean not null,
  group_number_last4 character(4),
  subscriber_relationship text not null,
  coverage_priority text not null,
  eligibility_business_outcome text not null,
  eligibility_checked_at timestamptz not null,
  eligibility_expires_at timestamptz not null,
  practice_network_business_outcome text not null,
  practice_network_checked_at timestamptz not null,
  practice_network_expires_at timestamptz not null,
  rendering_physician_network_checked boolean not null,
  payer_product_confirmed boolean not null,
  masked_member_details_confirmed boolean not null,
  subscriber_relationship_confirmed boolean not null,
  evidence_limitations_acknowledged boolean not null,
  synthetic_data_confirmed boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  confirmed_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  coverage_verified boolean not null default false,
  exact_network_confirmed boolean not null default false,
  canonical_coverage_created boolean not null default false,
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
  constraint uq_telehealth_insurance_handoff_practice_key
    unique(practice_id,idempotency_key),
  constraint chk_telehealth_insurance_handoff_result check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticInsuranceDetailsConfirmed'),
  constraint chk_telehealth_insurance_handoff_fingerprints check (
    insurance_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_insurance_handoff_masks check (
    member_id_last4 ~ '^[A-Z0-9-]{4}$'
    and ((group_number_present and group_number_last4 ~ '^[A-Z0-9-]{4}$')
      or (not group_number_present and group_number_last4 is null))),
  constraint chk_telehealth_insurance_handoff_relationship check (
    subscriber_relationship in ('Self','Spouse','Parent','Other')
    and coverage_priority='Primary'),
  constraint chk_telehealth_insurance_handoff_evidence check (
    eligibility_business_outcome='EligibleBenefitsReported'
    and practice_network_business_outcome='PracticeInNetworkAcceptingNewPatients'
    and not rendering_physician_network_checked
    and eligibility_checked_at < eligibility_expires_at
    and practice_network_checked_at < practice_network_expires_at
    and eligibility_checked_at <= practice_network_checked_at),
  constraint chk_telehealth_insurance_handoff_affirmations check (
    payer_product_confirmed
    and masked_member_details_confirmed
    and subscriber_relationship_confirmed
    and evidence_limitations_acknowledged
    and synthetic_data_confirmed),
  constraint chk_telehealth_insurance_handoff_policy check (
    policy_key='SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_INSURANCE_HANDOFF_NO_EDIT_CONFIRMATION'),
  constraint chk_telehealth_insurance_handoff_expiry check (
    confirmed_at < applicant_expires_at
    and confirmed_at < eligibility_expires_at
    and confirmed_at < practice_network_expires_at),
  constraint chk_telehealth_insurance_handoff_no_consequence check (
    not coverage_verified
    and not exact_network_confirmed
    and not canonical_coverage_created
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

create or replace function enforce_telehealth_insurance_handoff_confirmation()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  patient_row patients%rowtype;
  registration_row telehealth_applicant_registration_details_confirmations%rowtype;
  member_row telehealth_applicant_member_insurance_details%rowtype;
  eligibility_row telehealth_applicant_eligibility_results%rowtype;
  network_row telehealth_applicant_practice_network_determinations%rowtype;
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
    raise exception using errcode='23514',message='telehealth_insurance_handoff_applicant_mismatch';
  end if;

  select * into promotion_row
  from telehealth_applicant_synthetic_promotions
  where promotion_id=new.promotion_id and applicant_id=new.applicant_id;
  select * into patient_row
  from patients where canonical_id=new.canonical_patient_id;
  select * into registration_row
  from telehealth_applicant_registration_details_confirmations
  where confirmation_id=new.registration_details_confirmation_id
    and applicant_id=new.applicant_id;

  if promotion_row.promotion_id is null
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
     or registration_row.confirmation_id is null
     or registration_row.practice_id<>new.practice_id
     or registration_row.facility_id<>new.facility_id
     or registration_row.promotion_id<>new.promotion_id
     or registration_row.canonical_patient_id<>new.canonical_patient_id
     or registration_row.resulting_applicant_status<>'SyntheticMinimumRegistrationDetailsConfirmed'
     or registration_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or registration_row.confirmed_at>new.confirmed_at
     or exists(select 1 from insurance_records insurance
               where lower(insurance.patient_id)=lower(new.canonical_patient_id)) then
    raise exception using errcode='23514',message='telehealth_insurance_handoff_patient_mismatch';
  end if;

  select * into member_row
  from telehealth_applicant_member_insurance_details
  where details_id=new.member_insurance_details_id and applicant_id=new.applicant_id;
  select * into eligibility_row
  from telehealth_applicant_eligibility_results
  where eligibility_result_id=new.eligibility_result_id and applicant_id=new.applicant_id;
  select * into network_row
  from telehealth_applicant_practice_network_determinations
  where network_determination_id=new.network_determination_id and applicant_id=new.applicant_id;

  if member_row.details_id is null
     or eligibility_row.eligibility_result_id is null
     or network_row.network_determination_id is null
     or member_row.practice_id<>new.practice_id
     or member_row.facility_id<>new.facility_id
     or member_row.resulting_applicant_status<>'MemberInsuranceDetailsRecorded'
     or not member_row.details_confirmed
     or not member_row.synthetic_data_confirmed
     or eligibility_row.member_insurance_details_id<>member_row.details_id
     or eligibility_row.resulting_applicant_status<>'SyntheticEligibilityRecorded'
     or eligibility_row.business_outcome<>'EligibleBenefitsReported'
     or not eligibility_row.member_matched
     or not eligibility_row.member_eligibility_checked
     or not eligibility_row.member_benefits_checked
     or eligibility_row.coverage_verified
     or eligibility_row.exact_network_confirmed
     or network_row.member_insurance_details_id<>member_row.details_id
     or network_row.eligibility_result_id<>eligibility_row.eligibility_result_id
     or network_row.resulting_applicant_status<>'SyntheticPracticeNetworkRecorded'
     or network_row.business_outcome<>'PracticeInNetworkAcceptingNewPatients'
     or not network_row.practice_network_checked
     or not network_row.practice_in_network
     or not network_row.new_patients_accepted
     or network_row.rendering_physician_network_checked
     or network_row.exact_network_confirmed
     or network_row.coverage_verified
     or member_row.payer_display_name<>new.payer_display_name
     or member_row.product_display_name<>new.product_display_name
     or member_row.member_id_last4<>new.member_id_last4
     or member_row.group_number_present<>new.group_number_present
     or member_row.group_number_last4 is distinct from new.group_number_last4
     or member_row.subscriber_relationship<>new.subscriber_relationship
     or member_row.coverage_priority<>new.coverage_priority
     or eligibility_row.plan_key<>member_row.plan_key
     or eligibility_row.payer_display_name<>member_row.payer_display_name
     or eligibility_row.product_display_name<>member_row.product_display_name
     or eligibility_row.member_id_last4<>member_row.member_id_last4
     or eligibility_row.group_number_present<>member_row.group_number_present
     or eligibility_row.group_number_last4 is distinct from member_row.group_number_last4
     or eligibility_row.subscriber_relationship<>member_row.subscriber_relationship
     or eligibility_row.coverage_priority<>member_row.coverage_priority
     or eligibility_row.business_outcome<>new.eligibility_business_outcome
     or eligibility_row.checked_at<>new.eligibility_checked_at
     or eligibility_row.expires_at<>new.eligibility_expires_at
     or network_row.plan_key<>member_row.plan_key
     or network_row.payer_display_name<>member_row.payer_display_name
     or network_row.product_display_name<>member_row.product_display_name
     or network_row.eligibility_business_outcome<>eligibility_row.business_outcome
     or network_row.eligibility_checked_at<>eligibility_row.checked_at
     or network_row.eligibility_expires_at<>eligibility_row.expires_at
     or network_row.business_outcome<>new.practice_network_business_outcome
     or network_row.checked_at<>new.practice_network_checked_at
     or network_row.expires_at<>new.practice_network_expires_at
     or network_row.rendering_physician_network_checked<>new.rendering_physician_network_checked then
    raise exception using errcode='23514',message='telehealth_insurance_handoff_evidence_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_insurance_handoff_confirmation_guard
  on telehealth_applicant_insurance_handoff_confirmations;
create trigger trg_telehealth_insurance_handoff_confirmation_guard
before insert on telehealth_applicant_insurance_handoff_confirmations
for each row execute function enforce_telehealth_insurance_handoff_confirmation();

drop trigger if exists trg_telehealth_insurance_handoff_confirmations_append_only
  on telehealth_applicant_insurance_handoff_confirmations;
create trigger trg_telehealth_insurance_handoff_confirmations_append_only
before update or delete on telehealth_applicant_insurance_handoff_confirmations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_insurance_handoff_queue
  on telehealth_prospective_applicants(practice_id,facility_id,status,updated_at,applicant_id)
  where status='SyntheticInsuranceDetailsConfirmed';
