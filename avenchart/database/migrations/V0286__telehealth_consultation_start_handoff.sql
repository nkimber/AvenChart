-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default synthetic consultation-start linkage authorized by TH-DEC-0009.
-- This migration adds no clinical content, prescription, billing, claim, or live-media path.

alter table telehealth_requests
  drop constraint if exists chk_telehealth_requests_status;
alter table telehealth_requests
  add constraint chk_telehealth_requests_status
  check (status in ('Draft','LocationConfirmed','Intake','Verification','OperationalReview','Redirected','Queued','Reserved','Connecting','InConsultation'));

alter table telehealth_requests
  add column if not exists appointment_id text;
alter table telehealth_requests
  drop constraint if exists fk_telehealth_request_appointment;
alter table telehealth_requests
  add constraint fk_telehealth_request_appointment
  foreign key (appointment_id) references appointments(id);
create unique index if not exists uq_telehealth_request_appointment
  on telehealth_requests(appointment_id) where appointment_id is not null;

alter table telehealth_clinician_shifts
  drop constraint if exists chk_telehealth_shift_status;
alter table telehealth_clinician_shifts
  add constraint chk_telehealth_shift_status check (status in ('Active','Busy','Ended'));

drop index if exists uq_telehealth_active_shift_clinician;
create unique index uq_telehealth_active_shift_clinician
  on telehealth_clinician_shifts(practice_id, clinician_staff_id)
  where status in ('Active','Busy');

create table if not exists telehealth_consultation_contexts (
  consultation_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  reservation_id uuid not null unique,
  shift_id uuid not null references telehealth_clinician_shifts(shift_id),
  session_id uuid not null unique references telehealth_video_sessions(session_id),
  appointment_id text not null unique references appointments(id),
  encounter_id integer not null unique references encounters(encounter),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  physician_staff_id integer not null references staff(id),
  patient_location_state character(2) not null,
  modality text not null,
  status text not null,
  patient_identity_discussed boolean not null,
  callback_confirmed boolean not null,
  privacy_confirmed boolean not null,
  consent_discussed boolean not null,
  no_concerning_symptom_change boolean not null,
  emergency_plan_confirmed boolean not null,
  communication_sufficient boolean not null,
  synthetic_data_confirmed boolean not null,
  legal_effect boolean not null default false,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  version bigint not null default 1,
  started_at timestamptz not null default now(),
  constraint fk_telehealth_consultation_reservation_request
    foreign key (reservation_id, request_id)
    references telehealth_reservations(reservation_id, request_id),
  constraint chk_telehealth_consultation_practice check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_consultation_state check (patient_location_state in ('GA','CA','FL')),
  constraint chk_telehealth_consultation_modality check (modality = 'SYNTHETIC_VIDEO'),
  constraint chk_telehealth_consultation_status check (status = 'Started'),
  constraint chk_telehealth_consultation_start_gate check (
    patient_identity_discussed and callback_confirmed and privacy_confirmed and consent_discussed
    and no_concerning_symptom_change and emergency_plan_confirmed
    and communication_sufficient and synthetic_data_confirmed and legal_effect = false),
  constraint chk_telehealth_consultation_idempotency check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_consultation_version check (version >= 1)
);

-- A clinician shift owns a sequence of consultations. The patient request,
-- reservation, room, appointment, and encounter remain one-to-one, but a
-- completed consultation must not prevent the same shift serving the next
-- queued patient.
alter table telehealth_consultation_contexts
  drop constraint if exists telehealth_consultation_contexts_shift_id_key;

create index if not exists ix_telehealth_consultation_contexts_shift
  on telehealth_consultation_contexts(shift_id, started_at);

create unique index if not exists uq_telehealth_consultation_idempotency
  on telehealth_consultation_contexts(physician_staff_id, idempotency_key);

create table if not exists telehealth_consultation_events (
  event_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  request_id uuid not null references telehealth_requests(request_id),
  aggregate_version bigint not null,
  action text not null,
  actor_type text not null,
  actor_subject_hash character(64) not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint chk_telehealth_consultation_event_version check (aggregate_version >= 1),
  constraint chk_telehealth_consultation_event_actor check (actor_type in ('physician','system')),
  constraint uq_telehealth_consultation_event_version unique (consultation_id, aggregate_version),
  constraint uq_telehealth_consultation_event_idempotency unique (consultation_id, actor_type, actor_subject_hash, idempotency_key)
);

drop trigger if exists trg_telehealth_consultation_events_append_only on telehealth_consultation_events;
create trigger trg_telehealth_consultation_events_append_only
before update or delete on telehealth_consultation_events
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_consultation_contexts_append_only on telehealth_consultation_contexts;
create trigger trg_telehealth_consultation_contexts_append_only
before update or delete on telehealth_consultation_contexts
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_consultation_practice_request
  on telehealth_consultation_contexts(practice_id, facility_id, request_id);
