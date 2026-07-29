create table if not exists practice_setting_facility_overrides (
  setting_key text not null references practice_settings(setting_key) on delete cascade,
  facility_id integer not null references facilities(id) on delete cascade,
  setting_value text not null,
  updated_at timestamptz not null,
  updated_by text not null,
  primary key (setting_key, facility_id)
);

create index if not exists ix_practice_setting_facility_overrides_facility
  on practice_setting_facility_overrides(facility_id, setting_key);

comment on table practice_setting_facility_overrides is
  'ADM-02 local extension: facility-scoped configuration values. Rows are resolution-only until governed scoped mutation and delegated administration are delivered.';
