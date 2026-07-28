-- Only locally governed modules can be activated. Partner-gated and
-- decision-required entries remain catalog facts, not local proposals.
create table if not exists module_change_requests (
  request_id uuid primary key, module_key text not null,
  proposed_status text not null check (proposed_status in ('enabled','disabled')),
  baseline_status text not null check (baseline_status in ('enabled','disabled')),
  baseline_updated_at timestamptz not null,
  reason text not null,
  status text not null check (status in ('draft','submitted','approved','rejected','activated','cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null, created_by text not null, updated_at timestamptz not null, updated_by text not null
);
create unique index if not exists ux_module_change_requests_open_key on module_change_requests(module_key) where status in ('draft','submitted','approved');
create table if not exists module_change_request_events (
  event_id bigint generated always as identity primary key, request_id uuid not null references module_change_requests(request_id) on delete cascade,
  action text not null check (action in ('created','submitted','approved','rejected','activated','cancelled')), note text, occurred_at timestamptz not null, username text not null
);
alter table module_catalog_revisions drop constraint if exists module_catalog_revisions_action_check;
alter table module_catalog_revisions add constraint module_catalog_revisions_action_check check (action in ('baseline','updated','activated','rolled-back'));
