-- API client registrations are governed metadata. Secrets, signing keys, and
-- token issuance remain deployment-owned and are intentionally not stored here.
create table if not exists api_client_change_requests (
  request_id uuid primary key,
  client_key text not null,
  change_kind text not null check (change_kind in ('create','update')),
  proposed_definition jsonb not null,
  baseline_definition jsonb,
  baseline_updated_at timestamptz,
  reason text not null,
  status text not null check (status in ('draft','submitted','approved','rejected','activated','cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);
create unique index if not exists ux_api_client_change_requests_open_key
  on api_client_change_requests(client_key)
  where status in ('draft','submitted','approved');

create table if not exists api_client_change_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references api_client_change_requests(request_id) on delete cascade,
  action text not null check (action in ('created','submitted','approved','rejected','activated','cancelled')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);

alter table api_client_registry_revisions drop constraint if exists api_client_registry_revisions_action_check;
alter table api_client_registry_revisions add constraint api_client_registry_revisions_action_check
  check (action in ('baseline','updated','activated','rolled-back'));
