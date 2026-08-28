-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0018: one emergency-first synthetic universal safety screen for a
-- no-candidate, staff-reviewed prospective applicant. This does not create a
-- patient, complete intake, authorize care, or create a request or queue row.

alter table telehealth_prospective_applicants
  drop constraint chk_telehealth_applicant_status;
alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_status check (
    status in ('ContactVerificationPending','IdentityReviewPending',
               'IdentityReviewApproved','ManualReviewRequired',
               'SafetyScreenPassed','SafetyClinicalReviewRequired',
               'SafetyInPersonRequired','SafetyEmergencyRedirect',
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
                'SafetyEmergencyRedirect')
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
               'prospective-safety-triage-evaluated'));
alter table telehealth_applicant_events
  drop constraint chk_telehealth_applicant_event_status;
alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_status check (
    (from_status is null or from_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_safety_triage_evaluations (
  evaluation_id uuid primary key,
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  identity_review_decision_id uuid not null
    references telehealth_applicant_identity_review_decisions(decision_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  current_location_state_code character(2) not null,
  current_location_confirmed boolean not null,
  has_emergency_warning boolean not null,
  severe_or_worsening boolean not null,
  requires_hands_on_exam boolean not null,
  unsure boolean not null,
  protocol_id uuid not null,
  protocol_key text not null,
  protocol_version integer not null,
  protocol_content_hash character(64) not null,
  answers_fingerprint character(64) not null,
  outcome text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  identity_proofed boolean not null default false,
  clinical_review_performed boolean not null default false,
  canonical_patient_created boolean not null default false,
  chart_linked boolean not null default false,
  prospective_intake_completed boolean not null default false,
  coverage_checked boolean not null default false,
  request_created boolean not null default false,
  queue_enabled boolean not null default false,
  care_enabled boolean not null default false,
  evaluated_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_safety_triage_idempotency
    unique (applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_safety_triage_practice
    check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_applicant_safety_triage_version
    check (resulting_applicant_version >= 3),
  constraint chk_telehealth_applicant_safety_triage_status_outcome check (
    (outcome='Emergency' and resulting_applicant_status='SafetyEmergencyRedirect')
    or
    (outcome in ('UrgentInPerson','InPersonRequired')
      and resulting_applicant_status='SafetyInPersonRequired')
    or
    (outcome='ClinicalReview'
      and resulting_applicant_status='SafetyClinicalReviewRequired')
    or
    (outcome='TelehealthEligible'
      and resulting_applicant_status='SafetyScreenPassed')),
  constraint chk_telehealth_applicant_safety_triage_location check (
    current_location_state_code in ('GA','CA','FL')
    and current_location_confirmed),
  constraint chk_telehealth_applicant_safety_triage_protocol check (
    protocol_key='synthetic-universal-safety'
    and protocol_version=1),
  constraint chk_telehealth_applicant_safety_triage_hashes check (
    protocol_content_hash ~ '^[0-9a-f]{64}$'
    and answers_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_safety_triage_priority check (
    (has_emergency_warning and outcome='Emergency')
    or
    (not has_emergency_warning and severe_or_worsening
      and outcome='UrgentInPerson')
    or
    (not has_emergency_warning and not severe_or_worsening
      and requires_hands_on_exam and outcome='InPersonRequired')
    or
    (not has_emergency_warning and not severe_or_worsening
      and not requires_hands_on_exam and unsure and outcome='ClinicalReview')
    or
    (not has_emergency_warning and not severe_or_worsening
      and not requires_hands_on_exam and not unsure
      and outcome='TelehealthEligible')),
  constraint chk_telehealth_applicant_safety_triage_idempotency
    check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_safety_triage_no_consequence check (
    not identity_proofed and not clinical_review_performed
    and not canonical_patient_created and not chart_linked
    and not prospective_intake_completed and not coverage_checked
    and not request_created and not queue_enabled and not care_enabled)
);

create or replace function enforce_telehealth_applicant_safety_triage_evaluation()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  review_row telehealth_applicant_identity_review_decisions%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants
  where applicant_id=new.applicant_id
  for key share;

  select * into review_row
  from telehealth_applicant_identity_review_decisions
  where decision_id=new.identity_review_decision_id
    and applicant_id=new.applicant_id;

  if not found
     or review_row.decision <> 'ApprovedForProspectiveIntake'
     or review_row.identity_proofed
     or review_row.canonical_patient_created
     or applicant_row.practice_id <> new.practice_id
     or applicant_row.facility_id <> new.facility_id
     or applicant_row.version <> new.resulting_applicant_version
     or applicant_row.status <> new.resulting_applicant_status
     or applicant_row.duplicate_disposition <> 'NoCandidate'
     or applicant_row.contact_verified_at is null then
    raise exception using
      errcode='P0001',
      message='telehealth_applicant_safety_triage_snapshot_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_safety_triage_guard
  on telehealth_applicant_safety_triage_evaluations;
create trigger trg_telehealth_applicant_safety_triage_guard
before insert on telehealth_applicant_safety_triage_evaluations
for each row execute function enforce_telehealth_applicant_safety_triage_evaluation();

drop trigger if exists trg_telehealth_applicant_safety_triage_append_only
  on telehealth_applicant_safety_triage_evaluations;
create trigger trg_telehealth_applicant_safety_triage_append_only
before update or delete on telehealth_applicant_safety_triage_evaluations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_safety_triage_outcome
  on telehealth_applicant_safety_triage_evaluations(
    practice_id,facility_id,outcome,evaluated_at,applicant_id);
