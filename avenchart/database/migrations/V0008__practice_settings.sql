create table if not exists practice_settings (
  setting_key text primary key,
  setting_value text not null,
  value_type text not null,
  updated_at timestamptz not null,
  updated_by text not null
);
create table if not exists practice_setting_audit_events (
  event_id uuid primary key,
  setting_key text not null,
  prior_value text not null,
  new_value text not null,
  occurred_at timestamptz not null,
  username text not null
);
insert into practice_settings (setting_key, setting_value, value_type, updated_at, updated_by) values
  ('practice.name', 'AvenChart Practice', 'text', now(), 'seed'),
  ('practice.default-facility-id', '10', 'facility-id', now(), 'seed'),
  ('practice.time-zone', 'America/New_York', 'iana-time-zone', now(), 'seed')
on conflict (setting_key) do nothing;
