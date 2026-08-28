-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default connection-room evidence authorized by TH-DEC-0008.
-- The NON_PRODUCTION adapter transports no media and contacts no vendor.

alter table telehealth_requests
  drop constraint if exists chk_telehealth_requests_status;
alter table telehealth_requests
  add constraint chk_telehealth_requests_status
  check (status in ('Draft','LocationConfirmed','Intake','Verification','OperationalReview','Redirected','Queued','Reserved','Connecting'));

create unique index if not exists uq_telehealth_reservation_request_pair
  on telehealth_reservations(reservation_id, request_id);

create table if not exists telehealth_video_sessions (
  session_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  reservation_id uuid not null unique,
  practice_id text not null,
  facility_id integer not null references facilities(id),
  adapter_mode text not null,
  provider_session_reference character(64) not null,
  status text not null,
  recording_enabled boolean not null default false,
  transcription_enabled boolean not null default false,
  media_transport_enabled boolean not null default false,
  version bigint not null default 1,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null,
  constraint fk_telehealth_video_session_reservation_request
    foreign key (reservation_id, request_id)
    references telehealth_reservations(reservation_id, request_id),
  constraint chk_telehealth_video_session_practice check (practice_id ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
  constraint chk_telehealth_video_session_adapter check (adapter_mode = 'NON_PRODUCTION'),
  constraint chk_telehealth_video_session_status check (status in ('Prepared','WaitingRoom','Ended','Expired')),
  constraint chk_telehealth_video_session_no_capture check (
    recording_enabled = false and transcription_enabled = false and media_transport_enabled = false),
  constraint chk_telehealth_video_session_expiry check (expires_at > created_at and expires_at <= created_at + interval '30 minutes'),
  constraint chk_telehealth_video_session_version check (version >= 1)
);

create table if not exists telehealth_video_preflights (
  preflight_id uuid primary key,
  session_id uuid not null references telehealth_video_sessions(session_id),
  participant_role text not null,
  participant_subject_hash character(64) not null,
  browser_supported boolean not null,
  camera_available boolean not null,
  microphone_available boolean not null,
  speaker_available boolean not null,
  network_quality text not null,
  synthetic_data_confirmed boolean not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint chk_telehealth_video_preflight_role check (participant_role in ('patient','physician')),
  constraint chk_telehealth_video_preflight_passed check (
    browser_supported and camera_available and microphone_available and speaker_available and synthetic_data_confirmed),
  constraint chk_telehealth_video_preflight_network check (network_quality in ('unknown','limited','good')),
  constraint chk_telehealth_video_preflight_idempotency check (length(idempotency_key) between 8 and 128),
  constraint uq_telehealth_video_preflight_idempotency
    unique (session_id, participant_role, participant_subject_hash, idempotency_key)
);

create table if not exists telehealth_video_participant_grants (
  grant_id uuid primary key,
  session_id uuid not null references telehealth_video_sessions(session_id),
  preflight_id uuid not null unique references telehealth_video_preflights(preflight_id),
  participant_role text not null,
  participant_subject_hash character(64) not null,
  provider_instance_id character(64) not null,
  credential_hash character(64) not null,
  status text not null,
  issued_at timestamptz not null default now(),
  expires_at timestamptz not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  constraint chk_telehealth_video_grant_role check (participant_role in ('patient','physician')),
  constraint chk_telehealth_video_grant_status check (status in ('Issued','Revoked','Expired')),
  constraint chk_telehealth_video_grant_expiry check (expires_at > issued_at and expires_at <= issued_at + interval '5 minutes'),
  constraint chk_telehealth_video_grant_idempotency check (length(idempotency_key) between 8 and 128),
  constraint uq_telehealth_video_grant_idempotency
    unique (session_id, participant_role, participant_subject_hash, idempotency_key)
);

create unique index if not exists uq_telehealth_video_active_participant_grant
  on telehealth_video_participant_grants(session_id, participant_role, participant_subject_hash)
  where status='Issued';

create table if not exists telehealth_video_events (
  event_id uuid primary key,
  session_id uuid not null references telehealth_video_sessions(session_id),
  aggregate_version bigint not null,
  action text not null,
  actor_type text not null,
  actor_subject_hash character(64) not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint chk_telehealth_video_event_version check (aggregate_version >= 1),
  constraint chk_telehealth_video_event_actor check (actor_type in ('patient','physician','system')),
  constraint uq_telehealth_video_event_version unique (session_id, aggregate_version),
  constraint uq_telehealth_video_event_idempotency unique (session_id, actor_type, actor_subject_hash, idempotency_key)
);

drop trigger if exists trg_telehealth_video_preflights_append_only on telehealth_video_preflights;
create trigger trg_telehealth_video_preflights_append_only
before update or delete on telehealth_video_preflights
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_video_events_append_only on telehealth_video_events;
create trigger trg_telehealth_video_events_append_only
before update or delete on telehealth_video_events
for each row execute function reject_telehealth_evidence_mutation();

create or replace function reject_telehealth_video_aggregate_delete() returns trigger
language plpgsql as $$
begin
  raise exception 'telehealth video aggregates cannot be deleted';
end;
$$;

drop trigger if exists trg_telehealth_video_sessions_no_delete on telehealth_video_sessions;
create trigger trg_telehealth_video_sessions_no_delete
before delete on telehealth_video_sessions
for each row execute function reject_telehealth_video_aggregate_delete();

drop trigger if exists trg_telehealth_video_grants_no_delete on telehealth_video_participant_grants;
create trigger trg_telehealth_video_grants_no_delete
before delete on telehealth_video_participant_grants
for each row execute function reject_telehealth_video_aggregate_delete();

create index if not exists ix_telehealth_video_session_request
  on telehealth_video_sessions(practice_id, facility_id, request_id);
create index if not exists ix_telehealth_video_grant_expiry
  on telehealth_video_participant_grants(expires_at)
  where status='Issued';

