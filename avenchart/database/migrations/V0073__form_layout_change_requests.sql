-- A form layout is only safe to publish as a complete parent/group/field
-- definition.  Draft proposals therefore live outside the active layout tables.
create table if not exists form_layout_change_requests (
  request_id uuid primary key,
  layout_key text not null,
  change_kind text not null check (change_kind in ('create', 'update')),
  proposed_definition jsonb not null,
  baseline_definition jsonb,
  baseline_updated_at timestamptz,
  reason text not null,
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null,
  check ((change_kind = 'create' and baseline_definition is null and baseline_updated_at is null) or
         (change_kind = 'update' and baseline_definition is not null and baseline_updated_at is not null))
);

create unique index if not exists ux_form_layout_change_requests_open_key
  on form_layout_change_requests(layout_key)
  where status in ('draft', 'submitted', 'approved');
create index if not exists ix_form_layout_change_requests_status_updated
  on form_layout_change_requests(status, updated_at desc, request_id desc);

create table if not exists form_layout_change_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references form_layout_change_requests(request_id) on delete cascade,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_form_layout_change_request_events_request_time
  on form_layout_change_request_events(request_id, occurred_at desc, event_id desc);

alter table form_layout_revisions drop constraint if exists form_layout_revisions_action_check;
alter table form_layout_revisions add constraint form_layout_revisions_action_check
  check (action in ('baseline', 'updated', 'activated', 'rolled-back'));
