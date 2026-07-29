alter table practice_setting_change_requests
  add column if not exists facility_id integer references facilities(id) on delete restrict;

create index if not exists ix_practice_setting_change_requests_scope_updated
  on practice_setting_change_requests(facility_id, setting_key, updated_at desc, request_id desc);

create table if not exists practice_setting_facility_override_revisions (
  revision_id bigint generated always as identity primary key,
  setting_key text not null,
  facility_id integer not null,
  value text not null,
  prior_effective_value text not null,
  action text not null check (action = 'activated'),
  occurred_at timestamptz not null,
  username text not null,
  foreign key (setting_key, facility_id)
    references practice_setting_facility_overrides(setting_key, facility_id)
    on delete restrict
);

create index if not exists ix_practice_setting_facility_override_revisions_scope_time
  on practice_setting_facility_override_revisions(setting_key, facility_id, occurred_at desc, revision_id desc);

comment on table practice_setting_facility_override_revisions is
  'Immutable audit of facility-scoped practice-setting activations. System-scoped activations remain in practice_setting_revisions.';
