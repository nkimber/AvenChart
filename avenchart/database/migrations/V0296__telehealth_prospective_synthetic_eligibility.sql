-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0022: one normalized NON_PRODUCTION prospective eligibility result.
-- No proprietary X12 transaction, payer call, exact network, canonical coverage, or care.

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
                'SyntheticEligibilityRecorded')
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
               'prospective-synthetic-eligibility-recorded'));
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
      'VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
      'MemberInsuranceDetailsRecorded','SyntheticEligibilityRecorded',
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_eligibility_results (
  eligibility_result_id uuid primary key,
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
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  location_state_code character(2) not null,
  purpose_category text not null,
  plan_key text not null,
  payer_display_name text not null,
  product_display_name text not null,
  practice_network_status text not null,
  member_id_last4 character(4) not null,
  group_number_present boolean not null,
  group_number_last4 character(4),
  subscriber_relationship text not null,
  coverage_priority text not null,
  date_of_service date not null,
  service_category text not null,
  adapter_mode text not null,
  compatibility_target text not null,
  dataset_key text not null,
  dataset_version integer not null,
  dataset_effective_from timestamptz not null,
  dataset_effective_through timestamptz not null,
  inquiry_trace_token uuid not null unique,
  response_trace_token uuid not null unique,
  transport_outcome text not null,
  member_match_status text not null,
  eligibility_status text not null,
  benefit_information_status text not null,
  business_outcome text not null,
  member_matched boolean not null,
  member_eligibility_checked boolean not null,
  member_benefits_checked boolean not null,
  checked_at timestamptz not null,
  expires_at timestamptz not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  raw_transaction_created boolean not null default false,
  exact_network_confirmed boolean not null default false,
  coverage_verified boolean not null default false,
  canonical_patient_created boolean not null default false,
  identity_proofed boolean not null default false,
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
  recorded_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_eligibility_idempotency
    unique (applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_eligibility_practice
    check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_applicant_eligibility_version
    check (resulting_applicant_version >= 8),
  constraint chk_telehealth_applicant_eligibility_status
    check (resulting_applicant_status='SyntheticEligibilityRecorded'),
  constraint chk_telehealth_applicant_eligibility_location
    check (location_state_code in ('GA','CA','FL')),
  constraint chk_telehealth_applicant_eligibility_purpose
    check (purpose_category in ('migraine','sleep')),
  constraint chk_telehealth_applicant_eligibility_plan_status check (
    (plan_key='harbor-mutual-hd'
      and payer_display_name='Harbor Mutual'
      and product_display_name='High Deductible'
      and practice_network_status='PracticeNetworkConfirmedFixture')
    or
    (plan_key='blue-valley-standard'
      and payer_display_name='Blue Valley Health'
      and product_display_name='Standard'
      and practice_network_status='NetworkUnknown')
    or
    (plan_key='pine-state-choice'
      and payer_display_name='Pine State Choice'
      and product_display_name='Choice'
      and practice_network_status='PracticeOutOfNetworkFixture')),
  constraint chk_telehealth_applicant_eligibility_masks check (
    member_id_last4 ~ '^[A-Z0-9-]{4}$'
    and ((group_number_present and group_number_last4 ~ '^[A-Z0-9-]{4}$')
      or (not group_number_present and group_number_last4 is null))),
  constraint chk_telehealth_applicant_eligibility_subscriber check (
    subscriber_relationship in ('Self','Spouse','Parent','Other')
    and coverage_priority='Primary'),
  constraint chk_telehealth_applicant_eligibility_inquiry check (
    service_category='ProfessionalTelehealthConsultation'
    and adapter_mode='NON_PRODUCTION'
    and compatibility_target='ASC_X12N_270_271_005010X279A1'
    and dataset_key='avenchart-synthetic-prospective-eligibility-2026-08'
    and dataset_version=1
    and dataset_effective_from='2026-08-27T00:00:00Z'::timestamptz
    and dataset_effective_through='2026-10-31T23:59:59Z'::timestamptz
    and date_of_service between
      (dataset_effective_from at time zone 'UTC')::date
      and (dataset_effective_through at time zone 'UTC')::date),
  constraint chk_telehealth_applicant_eligibility_outcome_vocabulary check (
    transport_outcome in ('SimulatedAccepted','SimulatedUnavailable')
    and member_match_status in ('Matched','NotMatched','Unknown')
    and eligibility_status in ('Active','Inactive','Unknown')
    and benefit_information_status in ('Reported','NotReported','Unknown')
    and business_outcome in ('EligibleBenefitsReported','CoverageInactive',
                             'SubscriberNotFound','UnableToDetermine')),
  constraint chk_telehealth_applicant_eligibility_outcome_mapping check (
    (business_outcome='EligibleBenefitsReported'
      and transport_outcome='SimulatedAccepted'
      and member_match_status='Matched'
      and eligibility_status='Active'
      and benefit_information_status='Reported'
      and member_matched and member_eligibility_checked and member_benefits_checked)
    or
    (business_outcome='CoverageInactive'
      and transport_outcome='SimulatedAccepted'
      and member_match_status='Matched'
      and eligibility_status='Inactive'
      and benefit_information_status='NotReported'
      and member_matched and member_eligibility_checked and not member_benefits_checked)
    or
    (business_outcome='SubscriberNotFound'
      and transport_outcome='SimulatedAccepted'
      and member_match_status='NotMatched'
      and eligibility_status='Unknown'
      and benefit_information_status='NotReported'
      and not member_matched and member_eligibility_checked and not member_benefits_checked)
    or
    (business_outcome='UnableToDetermine'
      and transport_outcome='SimulatedUnavailable'
      and member_match_status='Unknown'
      and eligibility_status='Unknown'
      and benefit_information_status='Unknown'
      and not member_matched and not member_eligibility_checked and not member_benefits_checked)),
  constraint chk_telehealth_applicant_eligibility_freshness
    check (checked_at <= recorded_at and expires_at > checked_at
      and expires_at <= checked_at + interval '15 minutes'),
  constraint chk_telehealth_applicant_eligibility_idempotency
    check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_eligibility_fingerprint
    check (command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_eligibility_no_consequence check (
    not raw_transaction_created and not exact_network_confirmed
    and not coverage_verified and not canonical_patient_created
    and not identity_proofed and not chart_linked
    and not portal_account_created and not prospective_intake_completed
    and not consent_created and not practice_accepted
    and not coverage_record_created and not estimate_created
    and not financial_acknowledgment_created and not request_created
    and not queue_enabled and not appointment_created
    and not encounter_created and not care_enabled
    and not prescribing_enabled and not billing_enabled
    and not claim_created and not communication_enabled
    and not integration_enabled and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_eligibility_result()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  review_row telehealth_applicant_identity_review_decisions%rowtype;
  safety_row telehealth_applicant_safety_triage_evaluations%rowtype;
  purpose_row telehealth_applicant_visit_purposes%rowtype;
  precheck_row telehealth_applicant_practice_network_prechecks%rowtype;
  details_row telehealth_applicant_member_insurance_details%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into review_row from telehealth_applicant_identity_review_decisions
  where decision_id=new.identity_review_decision_id and applicant_id=new.applicant_id;
  select * into safety_row from telehealth_applicant_safety_triage_evaluations
  where evaluation_id=new.safety_triage_evaluation_id and applicant_id=new.applicant_id;
  select * into purpose_row from telehealth_applicant_visit_purposes
  where purpose_id=new.visit_purpose_id and applicant_id=new.applicant_id;
  select * into precheck_row from telehealth_applicant_practice_network_prechecks
  where precheck_id=new.practice_network_precheck_id and applicant_id=new.applicant_id;
  select * into details_row from telehealth_applicant_member_insurance_details
  where details_id=new.member_insurance_details_id and applicant_id=new.applicant_id;

  if applicant_row.applicant_id is null
     or review_row.decision_id is null
     or safety_row.evaluation_id is null
     or purpose_row.purpose_id is null
     or precheck_row.precheck_id is null
     or details_row.details_id is null
     or review_row.decision <> 'ApprovedForProspectiveIntake'
     or review_row.identity_proofed or review_row.canonical_patient_created
     or safety_row.outcome <> 'TelehealthEligible'
     or safety_row.resulting_applicant_status <> 'SafetyScreenPassed'
     or safety_row.current_location_state_code <> new.location_state_code
     or purpose_row.safety_triage_evaluation_id <> safety_row.evaluation_id
     or purpose_row.identity_review_decision_id <> review_row.decision_id
     or purpose_row.purpose_category <> new.purpose_category
     or purpose_row.resulting_applicant_status <> 'VisitPurposeRecorded'
     or precheck_row.identity_review_decision_id <> review_row.decision_id
     or precheck_row.safety_triage_evaluation_id <> safety_row.evaluation_id
     or precheck_row.visit_purpose_id <> purpose_row.purpose_id
     or precheck_row.resulting_applicant_status <> 'PracticeNetworkPrecheckRecorded'
     or details_row.identity_review_decision_id <> review_row.decision_id
     or details_row.safety_triage_evaluation_id <> safety_row.evaluation_id
     or details_row.visit_purpose_id <> purpose_row.purpose_id
     or details_row.practice_network_precheck_id <> precheck_row.precheck_id
     or details_row.resulting_applicant_status <> 'MemberInsuranceDetailsRecorded'
     or details_row.location_state_code <> new.location_state_code
     or details_row.purpose_category <> new.purpose_category
     or details_row.plan_key <> new.plan_key
     or details_row.payer_display_name <> new.payer_display_name
     or details_row.product_display_name <> new.product_display_name
     or details_row.practice_network_status <> new.practice_network_status
     or details_row.member_id_last4 <> new.member_id_last4
     or details_row.group_number_present <> new.group_number_present
     or details_row.group_number_last4 is distinct from new.group_number_last4
     or details_row.subscriber_relationship <> new.subscriber_relationship
     or details_row.coverage_priority <> new.coverage_priority
     or applicant_row.practice_id <> new.practice_id
     or applicant_row.facility_id <> new.facility_id
     or applicant_row.version <> new.resulting_applicant_version
     or applicant_row.status <> new.resulting_applicant_status
     or applicant_row.duplicate_disposition <> 'NoCandidate'
     or applicant_row.contact_verified_at is null then
    raise exception using
      errcode='P0001',
      message='telehealth_applicant_eligibility_result_snapshot_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_eligibility_result_guard
  on telehealth_applicant_eligibility_results;
create trigger trg_telehealth_applicant_eligibility_result_guard
before insert on telehealth_applicant_eligibility_results
for each row execute function enforce_telehealth_applicant_eligibility_result();

drop trigger if exists trg_telehealth_applicant_eligibility_result_append_only
  on telehealth_applicant_eligibility_results;
create trigger trg_telehealth_applicant_eligibility_result_append_only
before update or delete on telehealth_applicant_eligibility_results
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_eligibility_recorded
  on telehealth_applicant_eligibility_results(
    practice_id,facility_id,recorded_at,applicant_id);
