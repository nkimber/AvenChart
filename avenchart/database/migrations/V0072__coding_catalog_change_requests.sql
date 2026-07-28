-- Coding catalog edits can affect billing and clinical-code selection.  Keep
-- proposals separate from the active catalog until they pass the local
-- governance lifecycle.
create table if not exists coding_catalog_change_requests (
  request_id uuid primary key,
  catalog_key text not null,
  change_kind text not null check (change_kind in ('create', 'update')),
  proposed_display_name text not null,
  proposed_sequence integer not null check (proposed_sequence >= 0),
  proposed_active boolean not null,
  proposed_claim_enabled boolean not null,
  proposed_fee_enabled boolean not null,
  proposed_modifier_length integer not null check (proposed_modifier_length between 0 and 12),
  baseline_display_name text,
  baseline_sequence integer,
  baseline_active boolean,
  baseline_claim_enabled boolean,
  baseline_fee_enabled boolean,
  baseline_modifier_length integer,
  baseline_updated_at timestamptz,
  reason text not null,
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null,
  check ((change_kind = 'create' and baseline_display_name is null and baseline_sequence is null and baseline_active is null and baseline_claim_enabled is null and baseline_fee_enabled is null and baseline_modifier_length is null and baseline_updated_at is null) or (change_kind = 'update' and baseline_display_name is not null and baseline_sequence is not null and baseline_active is not null and baseline_claim_enabled is not null and baseline_fee_enabled is not null and baseline_modifier_length is not null and baseline_updated_at is not null))
);

create unique index if not exists ux_coding_catalog_change_requests_open_key
  on coding_catalog_change_requests(catalog_key)
  where status in ('draft', 'submitted', 'approved');
create index if not exists ix_coding_catalog_change_requests_status_updated
  on coding_catalog_change_requests(status, updated_at desc, request_id desc);

create table if not exists coding_catalog_change_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references coding_catalog_change_requests(request_id) on delete cascade,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);

create index if not exists ix_coding_catalog_change_request_events_request_time
  on coding_catalog_change_request_events(request_id, occurred_at desc, event_id desc);
