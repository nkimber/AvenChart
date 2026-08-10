-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

create table if not exists azure_operations_access_config (
  config_id smallint primary key check (config_id = 1),
  code_salt bytea not null check (octet_length(code_salt) >= 16),
  code_hash bytea not null check (octet_length(code_hash) = 32),
  hash_iterations integer not null check (hash_iterations >= 100000),
  code_version integer not null default 1 check (code_version > 0),
  requires_change boolean not null default true,
  changed_by varchar(255) not null,
  changed_at timestamptz not null default now()
);

-- PBKDF2-HMAC-SHA256, 310,000 iterations. The bootstrap code itself is never
-- stored in the database; operators are prompted to replace it after unlock.
insert into azure_operations_access_config
  (config_id, code_salt, code_hash, hash_iterations, code_version, requires_change, changed_by)
values
  (1,
   decode('d720afa58f65af292c6ce6394f8904c1', 'hex'),
   decode('92378400ba08c8584aa89fcc77ae082a0054789ab67d81700852e8a483bca094', 'hex'),
   310000,
   1,
   true,
   'system-bootstrap')
on conflict (config_id) do nothing;

create table if not exists azure_operations_access_grants (
  grant_id uuid primary key,
  token_hash bytea not null unique check (octet_length(token_hash) = 32),
  session_id uuid not null references auth_sessions(id) on delete cascade,
  username varchar(255) not null,
  code_version integer not null check (code_version > 0),
  created_at timestamptz not null default now(),
  expires_at timestamptz not null,
  last_used_at timestamptz not null default now(),
  revoked_at timestamptz,
  revoke_reason varchar(80),
  check (expires_at > created_at)
);

create index if not exists ix_azure_operations_access_grants_active_session
  on azure_operations_access_grants(session_id, expires_at desc)
  where revoked_at is null;

create table if not exists azure_operations_unlock_attempts (
  session_id uuid primary key references auth_sessions(id) on delete cascade,
  username varchar(255) not null,
  failure_count integer not null default 0 check (failure_count >= 0),
  window_started_at timestamptz not null default now(),
  locked_until timestamptz,
  updated_at timestamptz not null default now()
);

create table if not exists azure_operations_access_audit (
  event_id bigint generated always as identity primary key,
  event_type varchar(40) not null check (event_type in (
    'unlock_succeeded', 'unlock_failed', 'unlock_locked',
    'grant_rejected', 'grant_locked', 'code_changed'
  )),
  username varchar(255) not null,
  session_id uuid,
  success boolean not null,
  source_ip text,
  user_agent text,
  detail varchar(255) not null,
  occurred_at timestamptz not null default now()
);

create index if not exists ix_azure_operations_access_audit_time
  on azure_operations_access_audit(occurred_at desc, event_id desc);
