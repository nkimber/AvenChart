-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default synthetic foundation authorized by TH-DEC-0003.
-- This is additive and contains no clinical protocol content or patient rows.

create table if not exists telehealth_requests (
  request_id uuid primary key,
  practice_id text not null,
  facility_id integer not null references facilities(id),
  patient_id text not null references patients(canonical_id),
  status text not null,
  complaint_category text not null,
  triage_outcome text,
  version bigint not null default 1,
  create_idempotency_key text not null,
  create_fingerprint character(64) not null,
  ready_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint chk_telehealth_requests_practice check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_requests_status check (status in ('Draft','LocationConfirmed','OperationalReview','Redirected','Queued','Reserved')),
  constraint chk_telehealth_requests_complaint check (complaint_category in ('migraine','sleep')),
  constraint chk_telehealth_requests_triage check (triage_outcome is null or triage_outcome in ('Emergency','UrgentInPerson','InPersonRequired','ClinicalReview','TelehealthEligible')),
  constraint chk_telehealth_requests_version check (version >= 1),
  constraint chk_telehealth_requests_idempotency check (length(create_idempotency_key) between 8 and 128),
  constraint uq_telehealth_requests_create_idempotency unique (practice_id, patient_id, create_idempotency_key)
);

create table if not exists telehealth_protocol_versions (
  protocol_id uuid primary key,
  protocol_key text not null,
  protocol_version integer not null,
  content_hash character(64) not null,
  is_synthetic boolean not null,
  published_at timestamptz not null,
  constraint uq_telehealth_protocol_version unique (protocol_key, protocol_version),
  constraint chk_telehealth_protocol_synthetic_only check (is_synthetic = true),
  constraint chk_telehealth_protocol_version check (protocol_version > 0)
);

create table if not exists telehealth_patient_locations (
  location_id uuid primary key,
  request_id uuid not null references telehealth_requests(request_id),
  state_code character(2) not null,
  attested_at timestamptz not null default now(),
  request_version bigint not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  constraint chk_telehealth_location_state check (state_code in ('GA','CA','FL')),
  constraint chk_telehealth_location_version check (request_version >= 2),
  constraint uq_telehealth_location_idempotency unique (request_id, idempotency_key)
);

create table if not exists telehealth_triage_assessments (
  assessment_id uuid primary key,
  request_id uuid not null references telehealth_requests(request_id),
  protocol_id uuid not null references telehealth_protocol_versions(protocol_id),
  answer_fingerprint character(64) not null,
  outcome text not null,
  evaluated_at timestamptz not null default now(),
  request_version bigint not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  constraint chk_telehealth_assessment_outcome check (outcome in ('Emergency','UrgentInPerson','InPersonRequired','ClinicalReview','TelehealthEligible')),
  constraint chk_telehealth_assessment_version check (request_version >= 3),
  constraint uq_telehealth_assessment_idempotency unique (request_id, idempotency_key)
);

create table if not exists telehealth_request_events (
  event_id uuid primary key,
  request_id uuid not null references telehealth_requests(request_id),
  aggregate_version bigint not null,
  action text not null,
  from_status text,
  to_status text not null,
  actor_type text not null,
  actor_id text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint chk_telehealth_event_version check (aggregate_version >= 1),
  constraint chk_telehealth_event_actor check (actor_type in ('patient','administrator','physician','system')),
  constraint uq_telehealth_event_version unique (request_id, aggregate_version),
  constraint uq_telehealth_event_idempotency unique (request_id, idempotency_key)
);

create table if not exists telehealth_queue_entries (
  queue_entry_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  status text not null,
  ready_at timestamptz not null,
  authorized_by_actor_id text not null,
  version bigint not null default 1,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint chk_telehealth_queue_status check (status in ('Ready','Reserved','Removed')),
  constraint chk_telehealth_queue_version check (version >= 1),
  constraint chk_telehealth_queue_authorizer check (length(trim(authorized_by_actor_id)) between 1 and 128)
);

create table if not exists telehealth_clinician_shifts (
  shift_id uuid primary key,
  practice_id text not null,
  facility_id integer not null references facilities(id),
  clinician_staff_id integer not null references staff(id),
  status text not null,
  started_at timestamptz not null default now(),
  ended_at timestamptz,
  start_idempotency_key text not null,
  start_fingerprint character(64) not null,
  version bigint not null default 1,
  constraint chk_telehealth_shift_status check (status in ('Active','Ended')),
  constraint chk_telehealth_shift_version check (version >= 1)
);

create unique index if not exists uq_telehealth_active_shift_clinician
  on telehealth_clinician_shifts(practice_id, facility_id, clinician_staff_id)
  where status = 'Active';
create unique index if not exists uq_telehealth_shift_start_idempotency
  on telehealth_clinician_shifts(practice_id, clinician_staff_id, start_idempotency_key);

create table if not exists telehealth_reservations (
  reservation_id uuid primary key,
  request_id uuid not null references telehealth_requests(request_id),
  queue_entry_id uuid not null references telehealth_queue_entries(queue_entry_id),
  shift_id uuid not null references telehealth_clinician_shifts(shift_id),
  clinician_staff_id integer not null references staff(id),
  status text not null,
  reserved_at timestamptz not null default now(),
  lease_expires_at timestamptz not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  version bigint not null default 1,
  constraint chk_telehealth_reservation_status check (status in ('Active','Released','Expired')),
  constraint chk_telehealth_reservation_lease check (lease_expires_at > reserved_at),
  constraint chk_telehealth_reservation_version check (version >= 1)
);

create unique index if not exists uq_telehealth_active_reservation_request
  on telehealth_reservations(request_id) where status = 'Active';
create unique index if not exists uq_telehealth_active_reservation_clinician
  on telehealth_reservations(clinician_staff_id) where status = 'Active';
create unique index if not exists uq_telehealth_reservation_idempotency
  on telehealth_reservations(clinician_staff_id, idempotency_key);
create index if not exists ix_telehealth_patient_requests
  on telehealth_requests(practice_id, patient_id, created_at desc);
create index if not exists ix_telehealth_operational_review
  on telehealth_requests(practice_id, facility_id, created_at, request_id)
  where status = 'OperationalReview';
create index if not exists ix_telehealth_ready_queue
  on telehealth_queue_entries(practice_id, facility_id, ready_at, queue_entry_id)
  where status = 'Ready';

create or replace function reject_telehealth_evidence_mutation() returns trigger
language plpgsql as $$
begin
  raise exception 'telehealth evidence is append-only';
end;
$$;

drop trigger if exists trg_telehealth_protocol_versions_append_only on telehealth_protocol_versions;
create trigger trg_telehealth_protocol_versions_append_only
before update or delete on telehealth_protocol_versions
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_patient_locations_append_only on telehealth_patient_locations;
create trigger trg_telehealth_patient_locations_append_only
before update or delete on telehealth_patient_locations
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_triage_assessments_append_only on telehealth_triage_assessments;
create trigger trg_telehealth_triage_assessments_append_only
before update or delete on telehealth_triage_assessments
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_request_events_append_only on telehealth_request_events;
create trigger trg_telehealth_request_events_append_only
before update or delete on telehealth_request_events
for each row execute function reject_telehealth_evidence_mutation();
