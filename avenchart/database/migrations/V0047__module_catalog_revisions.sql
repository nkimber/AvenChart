create table if not exists module_catalog_revisions (
  revision_id bigint generated always as identity primary key,
  module_key text not null references module_catalog(module_key) on delete restrict,
  display_name text not null,
  category text not null,
  status text not null,
  description text not null,
  action text not null check (action in ('baseline','updated','rolled-back')),
  restored_from_revision_id bigint references module_catalog_revisions(revision_id) on delete restrict,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_module_catalog_revisions_key_time on module_catalog_revisions(module_key,occurred_at desc,revision_id desc);
insert into module_catalog_revisions(module_key,display_name,category,status,description,action,occurred_at,username)
select module_key,display_name,category,status,description,'baseline',updated_at,updated_by
from module_catalog module
where not exists(select 1 from module_catalog_revisions revision where revision.module_key=module.module_key);
