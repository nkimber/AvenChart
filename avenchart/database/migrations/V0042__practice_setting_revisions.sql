create table if not exists practice_setting_revisions (
  revision_id bigint generated always as identity primary key,
  setting_key text not null references practice_settings(setting_key) on delete restrict,
  value text not null,
  prior_value text,
  action text not null check (action in ('baseline','updated','rolled-back')),
  restored_from_revision_id bigint references practice_setting_revisions(revision_id) on delete restrict,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_practice_setting_revisions_key_time on practice_setting_revisions(setting_key, occurred_at desc, revision_id desc);
insert into practice_setting_revisions(setting_key,value,prior_value,action,occurred_at,username)
select setting_key,setting_value,null,'baseline',updated_at,updated_by
from practice_settings setting
where not exists (select 1 from practice_setting_revisions revision where revision.setting_key=setting.setting_key);
