create table if not exists coding_catalog_revisions (
  revision_id bigint generated always as identity primary key,
  catalog_key text not null references coding_catalogs(catalog_key) on delete restrict,
  display_name text not null,
  sequence integer not null,
  active boolean not null,
  claim_enabled boolean not null,
  fee_enabled boolean not null,
  modifier_length integer not null,
  action text not null check (action in ('baseline','created','updated','rolled-back')),
  restored_from_revision_id bigint references coding_catalog_revisions(revision_id) on delete restrict,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_coding_catalog_revisions_key_time on coding_catalog_revisions(catalog_key,occurred_at desc,revision_id desc);
alter table coding_catalog_audit_events drop constraint if exists coding_catalog_audit_events_action_check;
alter table coding_catalog_audit_events add constraint coding_catalog_audit_events_action_check check (action in ('created','updated','rolled-back'));
insert into coding_catalog_revisions(catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,action,occurred_at,username)
select catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,'baseline',updated_at,updated_by
from coding_catalogs catalog
where not exists (select 1 from coding_catalog_revisions revision where revision.catalog_key=catalog.catalog_key);
