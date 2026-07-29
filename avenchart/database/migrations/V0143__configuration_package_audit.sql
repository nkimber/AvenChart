create table if not exists configuration_package_events (
  event_id bigint generated always as identity primary key,
  event_type text not null check (event_type in ('exported', 'dry-run-validated', 'dry-run-rejected')),
  package_sha256 text,
  practice_setting_count integer not null check (practice_setting_count >= 0),
  occurred_at timestamptz not null,
  username text not null
);

create index if not exists ix_configuration_package_events_occurred_at
  on configuration_package_events(occurred_at desc, event_id desc);
