alter table practice_setting_revisions
  drop constraint if exists practice_setting_revisions_action_check;

alter table practice_setting_revisions
  add constraint practice_setting_revisions_action_check
  check (action in ('baseline', 'updated', 'rolled-back', 'activated'));

create table if not exists practice_setting_change_requests (
  request_id uuid primary key,
  setting_key text not null references practice_settings(setting_key) on delete restrict,
  proposed_value text not null,
  reason text not null,
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);

create index if not exists ix_practice_setting_change_requests_setting_updated
  on practice_setting_change_requests(setting_key, updated_at desc, request_id desc);

create table if not exists practice_setting_change_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references practice_setting_change_requests(request_id) on delete restrict,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);

create index if not exists ix_practice_setting_change_request_events_request_time
  on practice_setting_change_request_events(request_id, occurred_at desc, event_id desc);
