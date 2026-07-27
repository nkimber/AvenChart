create table if not exists form_option_list_revisions (
  revision_id bigint generated always as identity primary key,
  list_key text not null references form_option_lists(list_key) on delete restrict,
  title text not null,
  active boolean not null,
  options jsonb not null,
  action text not null check (action in ('baseline','updated','rolled-back')),
  restored_from_revision_id bigint references form_option_list_revisions(revision_id) on delete restrict,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_form_option_list_revisions_key_time on form_option_list_revisions(list_key,occurred_at desc,revision_id desc);
insert into form_option_list_revisions(list_key,title,active,options,action,occurred_at,username)
select l.list_key,l.title,l.active,coalesce((select jsonb_agg(jsonb_build_object('key',v.option_key,'title',v.title,'sequence',v.sequence,'isDefault',v.is_default,'active',v.active,'value',v.option_value) order by v.sequence,v.option_key) from form_option_values v where v.list_key=l.list_key),'[]'::jsonb),'baseline',l.updated_at,l.updated_by
from form_option_lists l where not exists (select 1 from form_option_list_revisions r where r.list_key=l.list_key);
