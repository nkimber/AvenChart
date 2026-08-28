-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0017: bounded staff review of contact-control and deterministic
-- duplicate-disposition evidence. This is not identity proofing, patient
-- matching, canonical patient creation/linkage, or practice acceptance.

alter table telehealth_prospective_applicants
  drop constraint chk_telehealth_applicant_status;
alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_status check (
    status in ('ContactVerificationPending','IdentityReviewPending',
               'IdentityReviewApproved','ManualReviewRequired',
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
    (status = 'IdentityReviewApproved'
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
               'applicant-expired','identity-review-recorded'));
alter table telehealth_applicant_events
  drop constraint chk_telehealth_applicant_event_status;
alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_status check (
    (from_status is null or from_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'VerificationLocked','Expired'));
alter table telehealth_applicant_events
  drop constraint chk_telehealth_applicant_event_actor;
alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_actor
    check (actor_type in ('applicant','system','administrator'));

create table if not exists telehealth_applicant_identity_review_decisions (
  decision_id uuid primary key,
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  resulting_applicant_version bigint not null,
  decision text not null,
  reason text not null,
  contact_verified_at_snapshot timestamptz not null,
  duplicate_disposition_snapshot text not null,
  duplicate_evidence_fingerprint_snapshot character(64) not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  decided_by_staff_id integer references staff(id),
  decided_by_actor_id text not null,
  decided_by_role text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  identity_proofed boolean not null default false,
  canonical_patient_created boolean not null default false,
  chart_linked boolean not null default false,
  prospective_intake_completed boolean not null default false,
  request_created boolean not null default false,
  queue_enabled boolean not null default false,
  decided_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_identity_decision_idempotency
    unique (applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_identity_decision_practice
    check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_applicant_identity_decision_version
    check (resulting_applicant_version >= 2),
  constraint chk_telehealth_applicant_identity_decision_outcome check (
    (decision='ApprovedForProspectiveIntake'
      and duplicate_disposition_snapshot='NoCandidate')
    or
    (decision='ManualReviewRequired'
      and duplicate_disposition_snapshot='PossibleMatchManualReview')),
  constraint chk_telehealth_applicant_identity_decision_reason
    check (length(trim(reason)) between 10 and 1000),
  constraint chk_telehealth_applicant_identity_decision_fingerprint check (
    duplicate_evidence_fingerprint_snapshot ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_identity_decision_policy check (
    policy_key='SYNTHETIC_STAFF_IDENTITY_REVIEW'
    and policy_version=1
    and evidence_type='CONTACT_CONTROL_AND_DUPLICATE_DISPOSITION_ONLY'),
  constraint chk_telehealth_applicant_identity_decision_actor
    check (length(trim(decided_by_actor_id)) between 1 and 128),
  constraint chk_telehealth_applicant_identity_decision_actor_role check (
    decided_by_role in ('administrator','frontdesk')
    and (decided_by_role <> 'frontdesk' or decided_by_staff_id is not null)),
  constraint chk_telehealth_applicant_identity_decision_idempotency
    check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_identity_decision_no_promotion check (
    not identity_proofed and not canonical_patient_created and not chart_linked
    and not prospective_intake_completed and not request_created and not queue_enabled)
);

create or replace function enforce_telehealth_applicant_identity_review_decision()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants
  where applicant_id=new.applicant_id
  for key share;

  if not found
     or applicant_row.practice_id <> new.practice_id
     or applicant_row.facility_id <> new.facility_id
     or applicant_row.version <> new.resulting_applicant_version
     or applicant_row.contact_verified_at is distinct from new.contact_verified_at_snapshot
     or applicant_row.duplicate_disposition is distinct from new.duplicate_disposition_snapshot
     or applicant_row.duplicate_evidence_fingerprint is distinct from new.duplicate_evidence_fingerprint_snapshot
     or (new.decision='ApprovedForProspectiveIntake'
         and applicant_row.status <> 'IdentityReviewApproved')
     or (new.decision='ManualReviewRequired'
         and applicant_row.status <> 'ManualReviewRequired') then
    raise exception using
      errcode='P0001',
      message='telehealth_applicant_identity_decision_snapshot_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_identity_decision_guard
  on telehealth_applicant_identity_review_decisions;
create trigger trg_telehealth_applicant_identity_decision_guard
before insert on telehealth_applicant_identity_review_decisions
for each row execute function enforce_telehealth_applicant_identity_review_decision();

drop trigger if exists trg_telehealth_applicant_identity_decisions_append_only
  on telehealth_applicant_identity_review_decisions;
create trigger trg_telehealth_applicant_identity_decisions_append_only
before update or delete on telehealth_applicant_identity_review_decisions
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_identity_review_queue
  on telehealth_prospective_applicants(practice_id,facility_id,contact_verified_at,applicant_id)
  where status='IdentityReviewPending';
