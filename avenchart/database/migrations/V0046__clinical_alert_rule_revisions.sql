create table if not exists clinical_alert_rule_revisions (
  revision_id bigint generated always as identity primary key,
  rule_key text not null references clinical_alert_rules(rule_key) on delete restrict,
  title text not null,
  trigger_type text not null,
  target_type text not null,
  severity text not null,
  message text not null,
  sequence integer not null,
  active boolean not null,
  action text not null check (action in ('baseline','updated','rolled-back')),
  restored_from_revision_id bigint references clinical_alert_rule_revisions(revision_id) on delete restrict,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_clinical_alert_rule_revisions_key_time on clinical_alert_rule_revisions(rule_key,occurred_at desc,revision_id desc);
insert into clinical_alert_rule_revisions(rule_key,title,trigger_type,target_type,severity,message,sequence,active,action,occurred_at,username)
select rule_key,title,trigger_type,target_type,severity,message,sequence,active,'baseline',updated_at,updated_by
from clinical_alert_rules rule
where not exists(select 1 from clinical_alert_rule_revisions revision where revision.rule_key=rule.rule_key);
