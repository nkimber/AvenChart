-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- These rooms have no relationship to patients, staff, appointments or charts.
-- An invitation grants only a synthetic media persona, never clinical access.
create table telehealth_demo_runs (
  run_id uuid primary key,
  practice_id text not null,
  facility_id integer not null references facilities(id),
  observer_key_hash character(64) not null,
  command_id text not null,
  status text not null default 'Active' check (status in ('Active','Ended')),
  transport text not null check (transport in ('NON_PRODUCTION_LOCAL_WEBRTC_POC','NON_PRODUCTION_INTERNET_ACS_CALLING_POC')),
  created_at timestamptz not null default now(),
  expires_at timestamptz not null default now() + interval '30 minutes',
  ended_at timestamptz,
  synthetic_only boolean not null default true check (synthetic_only),
  unique(practice_id, facility_id, command_id),
  check (expires_at > created_at and expires_at <= created_at + interval '30 minutes'),
  check ((status='Active' and ended_at is null) or (status='Ended' and ended_at is not null))
);
create table telehealth_demo_participants (
  participant_id uuid primary key,
  run_id uuid not null references telehealth_demo_runs(run_id),
  role text not null check (role in ('patient','physician')),
  invitation_hash character(64) not null,
  session_key_hash character(64),
  redeemed_at timestamptz,
  stage text not null default 'Invited' check (stage in ('Invited','Opened','DevicesReady','Connected')),
  last_seen_at timestamptz,
  unique(run_id,role),
  check ((session_key_hash is null and redeemed_at is null) or (session_key_hash is not null and redeemed_at is not null))
);
create index ix_telehealth_demo_runs_expiry on telehealth_demo_runs(practice_id,facility_id,expires_at) where status='Active';
