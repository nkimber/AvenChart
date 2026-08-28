-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0020: one applicant-owned synthetic practice-level plan precheck.
-- This is not member eligibility, exact network confirmation, coverage, or care.

alter table telehealth_prospective_applicants
  drop constraint chk_telehealth_applicant_status;
alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_status check (
    status in ('ContactVerificationPending','IdentityReviewPending',
               'IdentityReviewApproved','ManualReviewRequired',
               'SafetyScreenPassed','SafetyClinicalReviewRequired',
               'SafetyInPersonRequired','SafetyEmergencyRedirect',
               'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
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
                'PracticeNetworkPrecheckRecorded')
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
               'prospective-practice-network-precheck-recorded'));
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
      'VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_practice_network_prechecks (
  precheck_id uuid primary key,
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
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  location_state_code character(2) not null,
  purpose_category text not null,
  plan_key text not null,
  payer_display_name text not null,
  product_display_name text not null,
  practice_network_status text not null,
  adapter_mode text not null,
  catalog_key text not null,
  catalog_version integer not null,
  catalog_effective_from timestamptz not null,
  catalog_effective_through timestamptz not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  member_eligibility_checked boolean not null default false,
  member_benefits_checked boolean not null default false,
  rendering_physician_network_checked boolean not null default false,
  coverage_verified boolean not null default false,
  exact_network_confirmed boolean not null default false,
  identity_proofed boolean not null default false,
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
  recorded_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_network_precheck_idempotency
    unique (applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_network_precheck_practice
    check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_applicant_network_precheck_version
    check (resulting_applicant_version >= 6),
  constraint chk_telehealth_applicant_network_precheck_status
    check (resulting_applicant_status='PracticeNetworkPrecheckRecorded'),
  constraint chk_telehealth_applicant_network_precheck_location
    check (location_state_code in ('GA','CA','FL')),
  constraint chk_telehealth_applicant_network_precheck_purpose
    check (purpose_category in ('migraine','sleep')),
  constraint chk_telehealth_applicant_network_precheck_plan_status check (
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
  constraint chk_telehealth_applicant_network_precheck_catalog check (
    adapter_mode='NON_PRODUCTION'
    and catalog_key='avenchart-synthetic-prospective-practice-network-2026-08'
    and catalog_version=1
    and catalog_effective_from='2026-08-27T00:00:00Z'::timestamptz
    and catalog_effective_through='2026-10-31T23:59:59Z'::timestamptz
    and catalog_effective_from <= recorded_at
    and recorded_at <= catalog_effective_through),
  constraint chk_telehealth_applicant_network_precheck_idempotency
    check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_network_precheck_fingerprint
    check (command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_network_precheck_no_consequence check (
    not member_eligibility_checked
    and not member_benefits_checked
    and not rendering_physician_network_checked
    and not coverage_verified and not exact_network_confirmed
    and not identity_proofed and not canonical_patient_created
    and not chart_linked and not portal_account_created
    and not prospective_intake_completed and not consent_created
    and not practice_accepted
    and not coverage_record_created and not estimate_created
    and not financial_acknowledgment_created and not request_created
    and not queue_enabled and not appointment_created
    and not encounter_created and not care_enabled
    and not prescribing_enabled and not billing_enabled
    and not claim_created and not communication_enabled
    and not integration_enabled and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_practice_network_precheck()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  review_row telehealth_applicant_identity_review_decisions%rowtype;
  safety_row telehealth_applicant_safety_triage_evaluations%rowtype;
  purpose_row telehealth_applicant_visit_purposes%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants
  where applicant_id=new.applicant_id
  for key share;

  select * into review_row
  from telehealth_applicant_identity_review_decisions
  where decision_id=new.identity_review_decision_id
    and applicant_id=new.applicant_id;

  select * into safety_row
  from telehealth_applicant_safety_triage_evaluations
  where evaluation_id=new.safety_triage_evaluation_id
    and applicant_id=new.applicant_id;

  select * into purpose_row
  from telehealth_applicant_visit_purposes
  where purpose_id=new.visit_purpose_id
    and applicant_id=new.applicant_id;

  if not found
     or review_row.decision <> 'ApprovedForProspectiveIntake'
     or review_row.identity_proofed or review_row.canonical_patient_created
     or safety_row.outcome <> 'TelehealthEligible'
     or safety_row.resulting_applicant_status <> 'SafetyScreenPassed'
     or safety_row.current_location_state_code <> new.location_state_code
     or purpose_row.safety_triage_evaluation_id <> safety_row.evaluation_id
     or purpose_row.identity_review_decision_id <> review_row.decision_id
     or purpose_row.purpose_category <> new.purpose_category
     or purpose_row.resulting_applicant_status <> 'VisitPurposeRecorded'
     or applicant_row.practice_id <> new.practice_id
     or applicant_row.facility_id <> new.facility_id
     or applicant_row.version <> new.resulting_applicant_version
     or applicant_row.status <> new.resulting_applicant_status
     or applicant_row.duplicate_disposition <> 'NoCandidate'
     or applicant_row.contact_verified_at is null then
    raise exception using
      errcode='P0001',
      message='telehealth_applicant_practice_network_precheck_snapshot_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_network_precheck_guard
  on telehealth_applicant_practice_network_prechecks;
create trigger trg_telehealth_applicant_network_precheck_guard
before insert on telehealth_applicant_practice_network_prechecks
for each row execute function enforce_telehealth_applicant_practice_network_precheck();

drop trigger if exists trg_telehealth_applicant_network_precheck_append_only
  on telehealth_applicant_practice_network_prechecks;
create trigger trg_telehealth_applicant_network_precheck_append_only
before update or delete on telehealth_applicant_practice_network_prechecks
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_network_precheck_status
  on telehealth_applicant_practice_network_prechecks(
    practice_id,facility_id,practice_network_status,recorded_at,applicant_id);
