-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Immutable, short-lived staff claims prevent duplicate synthetic review work.
-- They do not change the applicant, case, request, queue, or care lifecycle.

create table if not exists telehealth_practice_review_claims (
  claim_id uuid primary key,
  case_id uuid not null references telehealth_prospective_practice_review_cases(case_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  expected_applicant_version bigint not null,
  assigned_to_staff_id integer,
  assigned_to_actor_id text not null,
  assigned_to_role text not null,
  assigned_at timestamptz not null default now(),
  lease_expires_at timestamptz not null,
  no_decision_acknowledged boolean not null,
  no_patient_contact_acknowledged boolean not null,
  no_request_or_care_queue_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  staff_review_work_item_exists boolean not null default true,
  staff_action_taken boolean not null default true,
  assigned boolean not null default true,
  priority_assigned boolean not null default false,
  practice_accepted boolean not null default false,
  practice_declined boolean not null default false,
  patient_contacted boolean not null default false,
  clinician_review_created boolean not null default false,
  telehealth_request_created boolean not null default false,
  patient_care_queue_entered boolean not null default false,
  clinician_queue_entered boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  billing_enabled boolean not null default false,
  claim_created boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  constraint uq_telehealth_practice_review_claim_idempotency
    unique(case_id,idempotency_key),
  constraint chk_telehealth_practice_review_claim_actor check (
    length(trim(assigned_to_actor_id)) between 1 and 200
    and assigned_to_role in ('administrator','frontdesk')
    and (assigned_to_role<>'frontdesk' or assigned_to_staff_id is not null)),
  constraint chk_telehealth_practice_review_claim_lease check (
    expected_applicant_version > 0
    and lease_expires_at=assigned_at+interval '120 seconds'),
  constraint chk_telehealth_practice_review_claim_acknowledgments check (
    no_decision_acknowledged
    and no_patient_contact_acknowledged
    and no_request_or_care_queue_acknowledged),
  constraint chk_telehealth_practice_review_claim_policy check (
    policy_key='SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM'
    and policy_version=1
    and evidence_type='PENDING_PRACTICE_REVIEW_SHORT_LEASE_RECEIPT'),
  constraint chk_telehealth_practice_review_claim_hash check (
    command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_practice_review_claim_consequences check (
    staff_review_work_item_exists and staff_action_taken and assigned
    and not priority_assigned
    and not practice_accepted and not practice_declined
    and not patient_contacted and not clinician_review_created
    and not telehealth_request_created
    and not patient_care_queue_entered and not clinician_queue_entered
    and not appointment_created and not encounter_created
    and not care_authorized and not prescribing_enabled
    and not billing_enabled and not claim_created
    and not integration_enabled and not external_call_performed)
);

create or replace function enforce_telehealth_practice_review_claim()
returns trigger
language plpgsql
as $$
declare
  case_row telehealth_prospective_practice_review_cases%rowtype;
  applicant_row telehealth_prospective_applicants%rowtype;
begin
  select * into case_row
  from telehealth_prospective_practice_review_cases
  where case_id=new.case_id;
  select * into applicant_row
  from telehealth_prospective_applicants
  where applicant_id=case_row.applicant_id;

  if case_row.case_id is null
     or case_row.practice_id<>new.practice_id
     or case_row.facility_id<>new.facility_id
     or case_row.case_status<>'PendingPracticeReview'
     or case_row.applicant_expires_at<=new.assigned_at
     or applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticPracticeReviewSubmitted'
     or applicant_row.version<>new.expected_applicant_version
     or applicant_row.expires_at<>case_row.applicant_expires_at
     or applicant_row.expires_at<=new.assigned_at then
    raise exception using errcode='23514',
      message='telehealth_practice_review_claim_provenance_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_enforce_telehealth_practice_review_claim
  on telehealth_practice_review_claims;
create trigger trg_enforce_telehealth_practice_review_claim
before insert on telehealth_practice_review_claims
for each row execute function enforce_telehealth_practice_review_claim();

drop trigger if exists trg_telehealth_practice_review_claims_append_only
  on telehealth_practice_review_claims;
create trigger trg_telehealth_practice_review_claims_append_only
before update or delete on telehealth_practice_review_claims
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_practice_review_claims_case_lease
  on telehealth_practice_review_claims(case_id,lease_expires_at desc,assigned_at desc,claim_id);
