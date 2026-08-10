-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

create table if not exists azure_deployment_profiles (
  profile_id uuid primary key,
  name varchar(120) not null,
  document jsonb not null,
  version integer not null default 1 check (version > 0),
  created_by varchar(255) not null,
  created_at timestamptz not null default now(),
  updated_by varchar(255) not null,
  updated_at timestamptz not null default now(),
  archived_at timestamptz
);

create unique index if not exists ux_azure_deployment_profiles_active_name
  on azure_deployment_profiles (lower(name))
  where archived_at is null;

create table if not exists azure_deployment_profile_revisions (
  revision_id bigint generated always as identity primary key,
  profile_id uuid not null references azure_deployment_profiles(profile_id),
  version integer not null check (version > 0),
  action varchar(40) not null,
  snapshot jsonb not null,
  changed_by varchar(255) not null,
  changed_at timestamptz not null default now(),
  unique (profile_id, version)
);

create index if not exists ix_azure_deployment_profile_revisions_profile_time
  on azure_deployment_profile_revisions(profile_id, changed_at desc, revision_id desc);

create table if not exists azure_deployment_executions (
  execution_id uuid primary key,
  profile_id uuid not null references azure_deployment_profiles(profile_id),
  profile_version integer not null check (profile_version > 0),
  execution_kind varchar(24) not null check (execution_kind in ('plan', 'deploy', 'rollback', 'verify')),
  status varchar(24) not null check (status in ('queued', 'running', 'cancelling', 'cancelled', 'succeeded', 'failed')),
  phase varchar(80) not null,
  requested_by varchar(255) not null,
  requested_at timestamptz not null default now(),
  started_at timestamptz,
  completed_at timestamptz,
  summary text,
  error text,
  application_url text,
  azure_deployment_name text,
  cancellation_requested_at timestamptz,
  profile_snapshot jsonb not null
);

create unique index if not exists ux_azure_deployment_executions_active_profile
  on azure_deployment_executions(profile_id)
  where status in ('queued', 'running', 'cancelling');

create index if not exists ix_azure_deployment_executions_profile_time
  on azure_deployment_executions(profile_id, requested_at desc);

create table if not exists azure_deployment_execution_events (
  event_id bigint generated always as identity primary key,
  execution_id uuid not null references azure_deployment_executions(execution_id) on delete cascade,
  level varchar(16) not null check (level in ('information', 'warning', 'error')),
  phase varchar(80) not null,
  message text not null,
  occurred_at timestamptz not null default now()
);

create index if not exists ix_azure_deployment_execution_events_execution_time
  on azure_deployment_execution_events(execution_id, event_id);
