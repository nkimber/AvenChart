create schema if not exists operations;

create table if not exists operations.operator_credentials (
    singleton boolean primary key default true check (singleton),
    password_hash text not null,
    bootstrap_version text not null,
    credential_version integer not null default 1,
    initialized_at timestamptz not null default now(),
    rotated_at timestamptz not null default now(),
    failed_attempt_count integer not null default 0,
    locked_until timestamptz
);

create table if not exists operations.sessions (
    id uuid primary key,
    token_hash bytea not null unique,
    csrf_hash bytea not null,
    staff_session_hash bytea not null,
    credential_version integer not null,
    created_at timestamptz not null default now(),
    last_seen_at timestamptz not null default now(),
    idle_expires_at timestamptz not null,
    absolute_expires_at timestamptz not null,
    ended_at timestamptz,
    source_hash text not null
);

create index if not exists idx_operations_sessions_active
    on operations.sessions (absolute_expires_at desc)
    where ended_at is null;

create table if not exists operations.audit_events (
    id bigserial primary key,
    occurred_at timestamptz not null default now(),
    event_type text not null,
    success boolean not null,
    source_hash text not null,
    detail_code text not null
);

create index if not exists idx_operations_audit_events_occurred
    on operations.audit_events (occurred_at desc, id desc);

create table if not exists operations.usage_events (
    id bigserial primary key,
    occurred_at timestamptz not null default now(),
    event_type text not null check (event_type in ('login_success', 'login_failure', 'api_activity')),
    browser_hash text not null,
    network_hash text not null,
    session_hash text not null,
    device_family text not null,
    category text not null
);

create index if not exists idx_operations_usage_events_occurred
    on operations.usage_events (occurred_at desc, id desc);
create index if not exists idx_operations_usage_events_browser
    on operations.usage_events (browser_hash, occurred_at desc);
create index if not exists idx_operations_usage_events_network
    on operations.usage_events (network_hash, occurred_at desc);
create index if not exists idx_operations_usage_events_session
    on operations.usage_events (session_hash, occurred_at desc);

create table if not exists operations.runtime_state (
    singleton boolean primary key default true check (singleton),
    last_dataset_reset_at timestamptz,
    dataset_id text,
    dataset_version text,
    deployment_id text,
    revision text,
    updated_at timestamptz not null default now()
);

-- Raw network and browser values from the original local-only audit implementation are
-- deliberately removed. Operations telemetry stores only keyed, non-reversible values.
update auth_sessions set source_ip = null, user_agent = null
where source_ip is not null or user_agent is not null;
update auth_audit_events set source_ip = null
where source_ip is not null;
