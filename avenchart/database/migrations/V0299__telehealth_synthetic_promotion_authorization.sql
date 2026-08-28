-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0025: one staff-governed authorization or denial for a later
-- synthetic promotion exercise. The applicant remains prospective and no
-- patient, chart, account, request, queue, external, or care action occurs.

alter table telehealth_prospective_applicants
  drop constraint chk_telehealth_applicant_status;
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
               'VerificationLocked','Expired'));

alter table telehealth_prospective_applicants
  drop constraint chk_telehealth_applicant_review_state;
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
                'SyntheticPromotionDenied')
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
  drop constraint chk_telehealth_applicant_event_action;
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
               'prospective-synthetic-promotion-authorization-recorded'));
alter table telehealth_applicant_events
  drop constraint chk_telehealth_applicant_event_status;
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
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_promotion_authorization_decisions (
  decision_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  identity_review_decision_id uuid not null
    references telehealth_applicant_identity_review_decisions(decision_id),
  safety_triage_evaluation_id uuid not null unique
    references telehealth_applicant_safety_triage_evaluations(evaluation_id),
  visit_purpose_id uuid not null unique
    references telehealth_applicant_visit_purposes(purpose_id),
  practice_network_precheck_id uuid not null unique
    references telehealth_applicant_practice_network_prechecks(precheck_id),
  member_insurance_details_id uuid not null unique
    references telehealth_applicant_member_insurance_details(details_id),
  eligibility_result_id uuid not null unique
    references telehealth_applicant_eligibility_results(eligibility_result_id),
  network_determination_id uuid not null unique
    references telehealth_applicant_practice_network_determinations(network_determination_id),
  identity_proofing_result_id uuid not null unique
    references telehealth_applicant_identity_proofing_results(identity_proofing_result_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  location_state_code character(2) not null,
  plan_key text not null,
  eligibility_business_outcome text not null,
  network_business_outcome text not null,
  proofing_business_outcome text not null,
  assurance_level_achieved text not null,
  proofing_identity_proofed boolean not null,
  proofing_checked_at timestamptz not null,
  proofing_expires_at timestamptz not null,
  applicant_expires_at timestamptz not null,
  decision text not null,
  reason text not null,
  none_assurance_acknowledged boolean not null,
  synthetic_data_confirmed boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  decided_by_staff_id integer references staff(id),
  decided_by_actor_id text not null,
  decided_by_role text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  real_identity_proofed boolean not null default false,
  canonical_patient_created boolean not null default false,
  chart_linked boolean not null default false,
  portal_account_created boolean not null default false,
  prospective_intake_completed boolean not null default false,
  consent_created boolean not null default false,
  practice_accepted boolean not null default false,
  coverage_record_created boolean not null default false,
  estimate_created boolean not null default false,
  financial_acknowledgment_created boolean not null default false,
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
  decided_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_promotion_authorization_idempotency
    unique (applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_promotion_authorization_practice
    check (practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_telehealth_applicant_promotion_authorization_version
    check (resulting_applicant_version >= 11),
  constraint chk_telehealth_applicant_promotion_authorization_scope
    check (location_state_code in ('GA','CA','FL') and plan_key='harbor-mutual-hd'),
  constraint chk_telehealth_applicant_promotion_authorization_outcome check (
    (decision='AuthorizedForSyntheticPromotion'
      and resulting_applicant_status='SyntheticPromotionAuthorized')
    or
    (decision='DeniedForSyntheticPromotion'
      and resulting_applicant_status='SyntheticPromotionDenied')),
  constraint chk_telehealth_applicant_promotion_authorization_evidence check (
    eligibility_business_outcome='EligibleBenefitsReported'
    and network_business_outcome='PracticeInNetworkAcceptingNewPatients'
    and proofing_business_outcome='SyntheticProofingPassed'
    and assurance_level_achieved='None'
    and not proofing_identity_proofed
    and proofing_checked_at < proofing_expires_at
    and decided_at < proofing_expires_at
    and decided_at < applicant_expires_at),
  constraint chk_telehealth_applicant_promotion_authorization_reason
    check (length(trim(reason)) between 10 and 1000),
  constraint chk_telehealth_applicant_promotion_authorization_acknowledgments
    check (none_assurance_acknowledged and synthetic_data_confirmed),
  constraint chk_telehealth_applicant_promotion_authorization_policy check (
    policy_key='SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION'
    and policy_version=1
    and evidence_type='COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY'),
  constraint chk_telehealth_applicant_promotion_authorization_actor
    check (length(trim(decided_by_actor_id)) between 1 and 128),
  constraint chk_telehealth_applicant_promotion_authorization_actor_role check (
    decided_by_role in ('administrator','frontdesk')
    and (decided_by_role <> 'frontdesk' or decided_by_staff_id is not null)),
  constraint chk_telehealth_applicant_promotion_authorization_idempotency
    check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_promotion_authorization_fingerprint
    check (command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_promotion_authorization_no_consequence check (
    not real_identity_proofed and not canonical_patient_created and not chart_linked
    and not portal_account_created and not prospective_intake_completed
    and not consent_created and not practice_accepted
    and not coverage_record_created and not estimate_created
    and not financial_acknowledgment_created and not request_created
    and not queue_enabled and not appointment_created and not encounter_created
    and not care_enabled and not prescribing_enabled
    and not billing_enabled and not claim_created
    and not communication_enabled and not integration_enabled
    and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_promotion_authorization()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  proofing_row telehealth_applicant_identity_proofing_results%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into proofing_row from telehealth_applicant_identity_proofing_results
  where identity_proofing_result_id=new.identity_proofing_result_id
    and applicant_id=new.applicant_id;

  if applicant_row.applicant_id is null
     or proofing_row.identity_proofing_result_id is null
     or proofing_row.identity_review_decision_id <> new.identity_review_decision_id
     or proofing_row.safety_triage_evaluation_id <> new.safety_triage_evaluation_id
     or proofing_row.visit_purpose_id <> new.visit_purpose_id
     or proofing_row.practice_network_precheck_id <> new.practice_network_precheck_id
     or proofing_row.member_insurance_details_id <> new.member_insurance_details_id
     or proofing_row.eligibility_result_id <> new.eligibility_result_id
     or proofing_row.network_determination_id <> new.network_determination_id
     or proofing_row.location_state_code <> new.location_state_code
     or proofing_row.plan_key <> new.plan_key
     or proofing_row.business_outcome <> new.proofing_business_outcome
     or proofing_row.assurance_level_achieved <> new.assurance_level_achieved
     or proofing_row.identity_proofed <> new.proofing_identity_proofed
     or proofing_row.checked_at <> new.proofing_checked_at
     or proofing_row.expires_at <> new.proofing_expires_at
     or proofing_row.identity_evidence_collected
     or proofing_row.government_identifier_collected
     or proofing_row.biometric_data_collected
     or proofing_row.authoritative_source_queried
     or proofing_row.authenticator_bound
     or proofing_row.business_outcome <> 'SyntheticProofingPassed'
     or proofing_row.assurance_level_achieved <> 'None'
     or proofing_row.identity_proofed
     or applicant_row.practice_id <> new.practice_id
     or applicant_row.facility_id <> new.facility_id
     or applicant_row.version <> new.resulting_applicant_version
     or applicant_row.status <> new.resulting_applicant_status
     or applicant_row.expires_at <> new.applicant_expires_at
     or applicant_row.duplicate_disposition <> 'NoCandidate'
     or applicant_row.contact_verified_at is null then
    raise exception using
      errcode='P0001',
      message='telehealth_applicant_promotion_authorization_snapshot_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_promotion_authorization_guard
  on telehealth_applicant_promotion_authorization_decisions;
create trigger trg_telehealth_applicant_promotion_authorization_guard
before insert on telehealth_applicant_promotion_authorization_decisions
for each row execute function enforce_telehealth_applicant_promotion_authorization();

drop trigger if exists trg_telehealth_applicant_promotion_authorizations_append_only
  on telehealth_applicant_promotion_authorization_decisions;
create trigger trg_telehealth_applicant_promotion_authorizations_append_only
before update or delete on telehealth_applicant_promotion_authorization_decisions
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_promotion_authorization_queue
  on telehealth_prospective_applicants(practice_id,facility_id,updated_at,applicant_id)
  where status='SyntheticIdentityProofingRecorded';
