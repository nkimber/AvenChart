-- INT-12: External laboratory traffic must have an explicitly governed source
-- identity. Raw credentials are never retained; only a salted PBKDF2 verifier
-- can be used to authenticate a synthetic or future partner laboratory.
create table if not exists external_laboratory_sources (
  source_id text primary key check (
    source_id = lower(source_id)
    and length(source_id) between 3 and 80
    and source_id ~ '^[a-z0-9][a-z0-9-]*[a-z0-9]$'
  ),
  display_name text not null check (length(btrim(display_name)) between 1 and 160),
  api_key_salt bytea not null check (octet_length(api_key_salt) = 16),
  api_key_hash bytea not null check (octet_length(api_key_hash) = 32),
  api_key_iterations integer not null check (api_key_iterations between 100000 and 2000000),
  active boolean not null default true,
  created_at timestamptz not null default now(),
  created_by text not null check (length(btrim(created_by)) between 1 and 120),
  deactivated_at timestamptz,
  deactivated_by text,
  deactivation_reason text,
  check (
    (active and deactivated_at is null and deactivated_by is null and deactivation_reason is null)
    or (not active and deactivated_at is not null and deactivated_by is not null and deactivation_reason is not null)
  )
);

create table if not exists external_laboratory_source_events (
  event_id uuid primary key,
  source_id text not null references external_laboratory_sources(source_id) on delete restrict,
  action text not null check (action in ('created', 'deactivated')),
  actor text not null check (length(btrim(actor)) between 1 and 120),
  reason text,
  occurred_at timestamptz not null default now()
);

create index if not exists idx_external_laboratory_source_events_source_time
  on external_laboratory_source_events(source_id, occurred_at desc, event_id desc);

create or replace function avenchart_prevent_external_laboratory_source_event_mutation()
returns trigger
language plpgsql
as $$
begin
  raise exception using
    errcode = '55000',
    message = 'External laboratory source events are immutable and cannot be altered or deleted.';
end;
$$;

drop trigger if exists trg_external_laboratory_source_events_immutable on external_laboratory_source_events;
create trigger trg_external_laboratory_source_events_immutable
before update or delete on external_laboratory_source_events
for each row execute function avenchart_prevent_external_laboratory_source_event_mutation();
