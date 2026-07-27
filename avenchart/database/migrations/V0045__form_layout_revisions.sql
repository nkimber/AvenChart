create table if not exists form_layout_revisions (
  revision_id bigint generated always as identity primary key,
  layout_key text not null references form_layouts(layout_key) on delete restrict,
  title text not null,
  mapping text not null,
  sequence integer not null,
  active boolean not null,
  groups jsonb not null,
  fields jsonb not null,
  action text not null check (action in ('baseline','updated','rolled-back')),
  restored_from_revision_id bigint references form_layout_revisions(revision_id) on delete restrict,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_form_layout_revisions_key_time on form_layout_revisions(layout_key,occurred_at desc,revision_id desc);
insert into form_layout_revisions(layout_key,title,mapping,sequence,active,groups,fields,action,occurred_at,username)
select l.layout_key,l.title,l.mapping,l.sequence,l.active,
coalesce((select jsonb_agg(jsonb_build_object('key',g.group_key,'title',g.title,'sequence',g.sequence,'active',g.active) order by g.sequence,g.group_key) from form_layout_groups g where g.layout_key=l.layout_key),'[]'::jsonb),
coalesce((select jsonb_agg(jsonb_build_object('key',f.field_key,'groupKey',f.group_key,'label',f.label,'fieldType',f.field_type,'sequence',f.sequence,'required',f.required,'active',f.active,'maxLength',f.max_length,'listId',f.list_id,'defaultValue',f.default_value) order by f.group_key,f.sequence,f.field_key) from form_layout_fields f where f.layout_key=l.layout_key),'[]'::jsonb),
'baseline',l.updated_at,l.updated_by from form_layouts l where not exists(select 1 from form_layout_revisions r where r.layout_key=l.layout_key);
