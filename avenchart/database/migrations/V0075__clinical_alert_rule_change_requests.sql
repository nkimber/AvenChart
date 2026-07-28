-- Clinical alert behavior is activated only from a complete, reviewed rule
-- definition so alert semantics cannot change through partial direct edits.
create table if not exists clinical_alert_rule_change_requests (
  request_id uuid primary key,
  rule_key text not null,
  change_kind text not null check (change_kind in ('create', 'update')),
  proposed_definition jsonb not null,
  baseline_definition jsonb,
  baseline_updated_at timestamptz,
  reason text not null,
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null, created_by text not null,
  updated_at timestamptz not null, updated_by text not null,
  check ((change_kind = 'create' and baseline_definition is null and baseline_updated_at is null) or (change_kind = 'update' and baseline_definition is not null and baseline_updated_at is not null))
);
create unique index if not exists ux_clinical_alert_rule_change_requests_open_key on clinical_alert_rule_change_requests(rule_key) where status in ('draft', 'submitted', 'approved');
create index if not exists ix_clinical_alert_rule_change_requests_status_updated on clinical_alert_rule_change_requests(status, updated_at desc, request_id desc);
create table if not exists clinical_alert_rule_change_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references clinical_alert_rule_change_requests(request_id) on delete cascade,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  note text, occurred_at timestamptz not null, username text not null
);
create index if not exists ix_clinical_alert_rule_change_request_events_request_time on clinical_alert_rule_change_request_events(request_id, occurred_at desc, event_id desc);
alter table clinical_alert_rule_revisions drop constraint if exists clinical_alert_rule_revisions_action_check;
alter table clinical_alert_rule_revisions add constraint clinical_alert_rule_revisions_action_check check (action in ('baseline', 'updated', 'activated', 'rolled-back'));
