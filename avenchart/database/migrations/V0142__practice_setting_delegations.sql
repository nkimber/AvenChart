create table if not exists practice_setting_delegations (
  delegation_id uuid primary key,
  username text not null references auth_accounts(username) on delete restrict,
  setting_key text not null references practice_settings(setting_key) on delete restrict,
  facility_id integer not null references facilities(id) on delete restrict,
  expires_at timestamptz,
  active boolean not null default true,
  reason text not null,
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);

create unique index if not exists ux_practice_setting_delegations_active_scope
  on practice_setting_delegations(username, setting_key, facility_id)
  where active = true;

create table if not exists practice_setting_delegation_events (
  event_id bigint generated always as identity primary key,
  delegation_id uuid not null references practice_setting_delegations(delegation_id) on delete restrict,
  action text not null check (action in ('granted', 'revoked')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);

create index if not exists ix_practice_setting_delegation_events_time
  on practice_setting_delegation_events(delegation_id, occurred_at desc, event_id desc);
