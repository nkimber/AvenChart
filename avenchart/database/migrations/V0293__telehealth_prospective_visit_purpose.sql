-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0019: one controlled synthetic visit-purpose classification after a
-- passing universal applicant safety screen. This is not complaint-specific
-- triage and cannot create a patient, request, queue entry, or care capability.

alter table telehealth_prospective_applicants
  drop constraint chk_telehealth_applicant_status;
alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_status check (
    status in ('ContactVerificationPending','IdentityReviewPending',
               'IdentityReviewApproved','ManualReviewRequired',
               'SafetyScreenPassed','SafetyClinicalReviewRequired',
               'SafetyInPersonRequired','SafetyEmergencyRedirect',
               'VisitPurposeRecorded','VerificationLocked','Expired'));

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
                'SafetyEmergencyRedirect','VisitPurposeRecorded')
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
               'prospective-visit-purpose-recorded'));
alter table telehealth_applicant_events
  drop constraint chk_telehealth_applicant_event_status;
alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_status check (
    (from_status is null or from_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VisitPurposeRecorded','VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VisitPurposeRecorded','VerificationLocked','Expired'));

create table if not exists telehealth_applicant_visit_purposes (
  purpose_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  identity_review_decision_id uuid not null
    references telehealth_applicant_identity_review_decisions(decision_id),
  safety_triage_evaluation_id uuid not null unique
    references telehealth_applicant_safety_triage_evaluations(evaluation_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  purpose_category text not null,
  purpose_display_label text not null,
  source_safety_outcome text not null,
  source_safety_protocol_key text not null,
  source_safety_protocol_version integer not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  clinical_protocol_published boolean not null default false,
  clinical_eligibility_determined boolean not null default false,
  identity_proofed boolean not null default false,
  canonical_patient_created boolean not null default false,
  chart_linked boolean not null default false,
  prospective_intake_completed boolean not null default false,
  coverage_checked boolean not null default false,
  request_created boolean not null default false,
  queue_enabled boolean not null default false,
  care_enabled boolean not null default false,
  recorded_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_visit_purpose_idempotency
    unique (applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_visit_purpose_practice
    check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_applicant_visit_purpose_version
    check (resulting_applicant_version >= 4),
  constraint chk_telehealth_applicant_visit_purpose_status
    check (resulting_applicant_status='VisitPurposeRecorded'),
  constraint chk_telehealth_applicant_visit_purpose_category check (
    (purpose_category='migraine'
      and purpose_display_label='Headache or known migraine pattern')
    or
    (purpose_category='sleep'
      and purpose_display_label='Sleep difficulty')),
  constraint chk_telehealth_applicant_visit_purpose_source check (
    source_safety_outcome='TelehealthEligible'
    and source_safety_protocol_key='synthetic-universal-safety'
    and source_safety_protocol_version=1),
  constraint chk_telehealth_applicant_visit_purpose_idempotency
    check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_visit_purpose_fingerprint
    check (command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_visit_purpose_no_consequence check (
    not clinical_protocol_published and not clinical_eligibility_determined
    and not identity_proofed and not canonical_patient_created
    and not chart_linked and not prospective_intake_completed
    and not coverage_checked and not request_created
    and not queue_enabled and not care_enabled)
);

create or replace function enforce_telehealth_applicant_visit_purpose()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  review_row telehealth_applicant_identity_review_decisions%rowtype;
  safety_row telehealth_applicant_safety_triage_evaluations%rowtype;
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

  if not found
     or review_row.decision <> 'ApprovedForProspectiveIntake'
     or review_row.identity_proofed
     or review_row.canonical_patient_created
     or safety_row.outcome <> 'TelehealthEligible'
     or safety_row.resulting_applicant_status <> 'SafetyScreenPassed'
     or safety_row.protocol_key <> new.source_safety_protocol_key
     or safety_row.protocol_version <> new.source_safety_protocol_version
     or applicant_row.practice_id <> new.practice_id
     or applicant_row.facility_id <> new.facility_id
     or applicant_row.version <> new.resulting_applicant_version
     or applicant_row.status <> new.resulting_applicant_status
     or applicant_row.duplicate_disposition <> 'NoCandidate'
     or applicant_row.contact_verified_at is null then
    raise exception using
      errcode='P0001',
      message='telehealth_applicant_visit_purpose_snapshot_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_visit_purpose_guard
  on telehealth_applicant_visit_purposes;
create trigger trg_telehealth_applicant_visit_purpose_guard
before insert on telehealth_applicant_visit_purposes
for each row execute function enforce_telehealth_applicant_visit_purpose();

drop trigger if exists trg_telehealth_applicant_visit_purpose_append_only
  on telehealth_applicant_visit_purposes;
create trigger trg_telehealth_applicant_visit_purpose_append_only
before update or delete on telehealth_applicant_visit_purposes
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_visit_purpose_category
  on telehealth_applicant_visit_purposes(
    practice_id,facility_id,purpose_category,recorded_at,applicant_id);
