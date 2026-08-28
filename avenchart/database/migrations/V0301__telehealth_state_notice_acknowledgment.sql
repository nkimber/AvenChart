-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0027: one applicant-owned, state-specific synthetic telehealth
-- notice acknowledgment after patient-shell promotion. This is not legal or
-- clinician-obtained consent and creates no portal, request, queue, or care.

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
                'SyntheticTelehealthNoticeAcknowledged')
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
               'prospective-telehealth-notice-acknowledged'));

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
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_notice_acknowledgments (
  acknowledgment_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  safety_triage_evaluation_id uuid not null unique
    references telehealth_applicant_safety_triage_evaluations(evaluation_id),
  promotion_id uuid not null unique
    references telehealth_applicant_synthetic_promotions(promotion_id),
  canonical_patient_id text not null unique references patients(canonical_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  current_location_state_code character(2) not null,
  notice_key text not null,
  notice_version integer not null,
  notice_source_title text not null,
  notice_source_url text not null,
  current_location_confirmed boolean not null,
  mode_of_care_acknowledged boolean not null,
  privacy_limitations_acknowledged boolean not null,
  emergency_instructions_acknowledged boolean not null,
  in_person_option_acknowledged boolean not null,
  clinician_reconfirmation_required_acknowledged boolean not null,
  synthetic_data_confirmed boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  legal_review_status text not null,
  applicant_expires_at timestamptz not null,
  acknowledged_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  legal_consent_established boolean not null default false,
  clinician_consent_documented boolean not null default false,
  electronic_signature_created boolean not null default false,
  portal_account_created boolean not null default false,
  portal_session_created boolean not null default false,
  external_identity_mapping_created boolean not null default false,
  chart_content_created boolean not null default false,
  intake_completed boolean not null default false,
  practice_accepted boolean not null default false,
  insurance_created boolean not null default false,
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
  constraint uq_telehealth_applicant_notice_acknowledgment_idempotency
    unique(applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_notice_acknowledgment_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_telehealth_applicant_notice_acknowledgment_result check (
    resulting_applicant_version >= 13
    and resulting_applicant_status='SyntheticTelehealthNoticeAcknowledged'),
  constraint chk_telehealth_applicant_notice_acknowledgment_state_notice check (
    (current_location_state_code='GA'
      and notice_key='GA_TELEHEALTH_NOTICE_V1'
      and notice_source_title='Georgia Composite Medical Board Rule 360-3-.07'
      and notice_source_url='https://rules.sos.ga.gov/gac/360-3-.07')
    or
    (current_location_state_code='CA'
      and notice_key='CA_TELEHEALTH_NOTICE_V1'
      and notice_source_title='California Business and Professions Code § 2290.5'
      and notice_source_url='https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=BPC&sectionNum=2290.5.')
    or
    (current_location_state_code='FL'
      and notice_key='FL_TELEHEALTH_NOTICE_V1'
      and notice_source_title='Florida Statutes § 456.47'
      and notice_source_url='https://leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499/0456/Sections/0456.47.html')),
  constraint chk_telehealth_applicant_notice_acknowledgment_version check (
    notice_version=1),
  constraint chk_telehealth_applicant_notice_acknowledgment_affirmations check (
    current_location_confirmed and mode_of_care_acknowledged
    and privacy_limitations_acknowledged
    and emergency_instructions_acknowledged
    and in_person_option_acknowledged
    and clinician_reconfirmation_required_acknowledged
    and synthetic_data_confirmed),
  constraint chk_telehealth_applicant_notice_acknowledgment_policy check (
    policy_key='SYNTHETIC_TELEHEALTH_NOTICE_ACKNOWLEDGMENT'
    and policy_version=1
    and evidence_type='STATE_NOTICE_FIXTURE_AND_PATIENT_ACKNOWLEDGMENTS_ONLY'
    and legal_review_status='PendingIndependentReview'),
  constraint chk_telehealth_applicant_notice_acknowledgment_freshness check (
    acknowledged_at < applicant_expires_at),
  constraint chk_telehealth_applicant_notice_acknowledgment_idempotency check (
    length(idempotency_key) between 8 and 200),
  constraint chk_telehealth_applicant_notice_acknowledgment_fingerprint check (
    command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_notice_acknowledgment_no_consequence check (
    not legal_consent_established
    and not clinician_consent_documented
    and not electronic_signature_created
    and not portal_account_created
    and not portal_session_created
    and not external_identity_mapping_created
    and not chart_content_created
    and not intake_completed
    and not practice_accepted
    and not insurance_created
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
    and not external_call_performed));

create or replace function enforce_telehealth_applicant_notice_acknowledgment()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  safety_row telehealth_applicant_safety_triage_evaluations%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants
  where applicant_id=new.applicant_id;
  select * into safety_row
  from telehealth_applicant_safety_triage_evaluations
  where evaluation_id=new.safety_triage_evaluation_id;
  select * into promotion_row
  from telehealth_applicant_synthetic_promotions
  where promotion_id=new.promotion_id;
  select * into patient_row
  from patients
  where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null
     or safety_row.evaluation_id is null
     or promotion_row.promotion_id is null
     or patient_row.canonical_id is null
     or safety_row.applicant_id<>new.applicant_id
     or safety_row.current_location_state_code<>new.current_location_state_code
     or safety_row.outcome<>'TelehealthEligible'
     or promotion_row.applicant_id<>new.applicant_id
     or promotion_row.practice_id<>new.practice_id
     or promotion_row.facility_id<>new.facility_id
     or promotion_row.outcome<>'SyntheticPatientCreated'
     or not promotion_row.canonical_patient_created
     or promotion_row.canonical_patient_id<>new.canonical_patient_id
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.expires_at<>new.applicant_expires_at
     or patient_row.facility_id<>new.facility_id
     or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null then
    raise exception using
      errcode='23514',
      message='telehealth_applicant_notice_acknowledgment_provenance_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_notice_acknowledgment_guard
  on telehealth_applicant_notice_acknowledgments;
create trigger trg_telehealth_applicant_notice_acknowledgment_guard
before insert on telehealth_applicant_notice_acknowledgments
for each row execute function enforce_telehealth_applicant_notice_acknowledgment();

drop trigger if exists trg_telehealth_applicant_notice_acknowledgments_append_only
  on telehealth_applicant_notice_acknowledgments;
create trigger trg_telehealth_applicant_notice_acknowledgments_append_only
before update or delete on telehealth_applicant_notice_acknowledgments
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_notice_acknowledgment_next
  on telehealth_prospective_applicants(practice_id,facility_id,status,updated_at,applicant_id)
  where status='SyntheticTelehealthNoticeAcknowledged';
