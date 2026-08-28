-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default prospective-applicant identity shell authorized by
-- Decision 0007. Applicants are deliberately not canonical patient records.

create table if not exists telehealth_prospective_applicants (
  applicant_id uuid primary key,
  practice_id text not null,
  facility_id integer not null references facilities(id),
  status text not null,
  version bigint not null default 1,
  legal_first_name text not null,
  legal_last_name text not null,
  date_of_birth date not null,
  email text not null,
  phone text not null,
  residence_state_code character(2) not null,
  postal_code text not null,
  access_key_hash character(64) not null,
  create_idempotency_key text not null,
  create_fingerprint character(64) not null,
  duplicate_disposition text,
  duplicate_evidence_fingerprint character(64),
  contact_verified_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  expires_at timestamptz not null,
  constraint chk_telehealth_applicant_practice
    check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_applicant_status
    check (status in ('ContactVerificationPending','IdentityReviewPending','VerificationLocked','Expired')),
  constraint chk_telehealth_applicant_version check (version >= 1),
  constraint chk_telehealth_applicant_names check (
    length(trim(legal_first_name)) between 1 and 100
    and length(trim(legal_last_name)) between 1 and 100),
  constraint chk_telehealth_applicant_adult check (
    date_of_birth >= date '1900-01-01'
    and date_of_birth <= ((created_at at time zone 'UTC')::date - interval '18 years')::date),
  constraint chk_telehealth_applicant_email check (
    length(email) between 3 and 254 and email = lower(trim(email)) and position('@' in email) > 1),
  constraint chk_telehealth_applicant_phone check (phone ~ '^\+[1-9][0-9]{9,14}$'),
  constraint chk_telehealth_applicant_state check (residence_state_code in ('GA','CA','FL')),
  constraint chk_telehealth_applicant_postal check (postal_code ~ '^[0-9]{5}$'),
  constraint chk_telehealth_applicant_hashes check (
    access_key_hash ~ '^[0-9a-f]{64}$'
    and create_fingerprint ~ '^[0-9a-f]{64}$'
    and (duplicate_evidence_fingerprint is null or duplicate_evidence_fingerprint ~ '^[0-9a-f]{64}$')),
  constraint chk_telehealth_applicant_idempotency check (length(create_idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_expiry check (
    expires_at > created_at and expires_at <= created_at + interval '2 hours'),
  constraint chk_telehealth_applicant_review_state check (
    (status = 'IdentityReviewPending'
      and contact_verified_at is not null
      and duplicate_disposition in ('NoCandidate','PossibleMatchManualReview')
      and duplicate_evidence_fingerprint is not null)
    or
    (status <> 'IdentityReviewPending'
      and contact_verified_at is null
      and duplicate_disposition is null
      and duplicate_evidence_fingerprint is null)),
  constraint uq_telehealth_applicant_create_idempotency
    unique (practice_id, facility_id, create_idempotency_key)
);

create table if not exists telehealth_applicant_contact_challenges (
  challenge_id uuid primary key,
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  channel text not null,
  destination_fingerprint character(64) not null,
  verifier_hash character(64) not null,
  maximum_attempts integer not null,
  issued_at timestamptz not null default now(),
  expires_at timestamptz not null,
  constraint chk_telehealth_applicant_challenge_channel check (channel = 'email'),
  constraint chk_telehealth_applicant_challenge_hashes check (
    destination_fingerprint ~ '^[0-9a-f]{64}$' and verifier_hash ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_challenge_attempts check (maximum_attempts between 1 and 5),
  constraint chk_telehealth_applicant_challenge_expiry check (
    expires_at > issued_at and expires_at <= issued_at + interval '2 hours')
);

create table if not exists telehealth_applicant_verification_attempts (
  attempt_id uuid primary key,
  applicant_id uuid not null references telehealth_prospective_applicants(applicant_id),
  attempt_ordinal integer not null,
  result text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  attempted_at timestamptz not null default now(),
  constraint chk_telehealth_applicant_attempt_ordinal check (attempt_ordinal between 1 and 5),
  constraint chk_telehealth_applicant_attempt_result check (result in ('Accepted','Rejected','Locked')),
  constraint chk_telehealth_applicant_attempt_fingerprint
    check (command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint uq_telehealth_applicant_attempt_ordinal unique (applicant_id, attempt_ordinal),
  constraint uq_telehealth_applicant_attempt_idempotency unique (applicant_id, idempotency_key)
);

create table if not exists telehealth_applicant_events (
  event_id uuid primary key,
  applicant_id uuid not null references telehealth_prospective_applicants(applicant_id),
  aggregate_version bigint not null,
  action text not null,
  from_status text,
  to_status text not null,
  actor_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint chk_telehealth_applicant_event_version check (aggregate_version >= 1),
  constraint chk_telehealth_applicant_event_action
    check (action in ('applicant-created','contact-verified','verification-locked','applicant-expired')),
  constraint chk_telehealth_applicant_event_status check (
    (from_status is null or from_status in ('ContactVerificationPending','IdentityReviewPending','VerificationLocked','Expired'))
    and to_status in ('ContactVerificationPending','IdentityReviewPending','VerificationLocked','Expired')),
  constraint chk_telehealth_applicant_event_actor check (actor_type in ('applicant','system')),
  constraint chk_telehealth_applicant_event_fingerprint
    check (command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint uq_telehealth_applicant_event_version unique (applicant_id, aggregate_version),
  constraint uq_telehealth_applicant_event_idempotency unique (applicant_id, idempotency_key)
);

create index if not exists ix_telehealth_applicant_access
  on telehealth_prospective_applicants(practice_id, facility_id, applicant_id);
create index if not exists ix_telehealth_applicant_review
  on telehealth_prospective_applicants(practice_id, facility_id, created_at, applicant_id)
  where status = 'IdentityReviewPending';
create index if not exists ix_telehealth_applicant_expiry
  on telehealth_prospective_applicants(expires_at, applicant_id)
  where status = 'ContactVerificationPending';

drop trigger if exists trg_telehealth_applicant_challenges_append_only
  on telehealth_applicant_contact_challenges;
create trigger trg_telehealth_applicant_challenges_append_only
before update or delete on telehealth_applicant_contact_challenges
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_applicant_attempts_append_only
  on telehealth_applicant_verification_attempts;
create trigger trg_telehealth_applicant_attempts_append_only
before update or delete on telehealth_applicant_verification_attempts
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_applicant_events_append_only
  on telehealth_applicant_events;
create trigger trg_telehealth_applicant_events_append_only
before update or delete on telehealth_applicant_events
for each row execute function reject_telehealth_evidence_mutation();

create or replace function reject_telehealth_applicant_delete() returns trigger
language plpgsql as $$
begin
  raise exception 'telehealth applicant aggregates cannot be deleted';
end;
$$;

drop trigger if exists trg_telehealth_applicants_no_delete on telehealth_prospective_applicants;
create trigger trg_telehealth_applicants_no_delete
before delete on telehealth_prospective_applicants
for each row execute function reject_telehealth_applicant_delete();

