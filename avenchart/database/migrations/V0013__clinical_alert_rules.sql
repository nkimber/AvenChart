create table if not exists clinical_alert_rules (
  rule_key text primary key,
  title text not null,
  trigger_type text not null check (trigger_type in ('patient', 'encounter', 'appointment')),
  target_type text not null check (target_type in ('banner', 'reminder')),
  severity text not null check (severity in ('info', 'warning', 'critical')),
  message text not null,
  sequence integer not null check (sequence >= 0),
  active boolean not null default true,
  updated_at timestamptz not null,
  updated_by text not null
);
create unique index if not exists ux_clinical_alert_rules_sequence on clinical_alert_rules(sequence);
insert into clinical_alert_rules(rule_key,title,trigger_type,target_type,severity,message,sequence,active,updated_at,updated_by) values
  ('APPOINTMENT_REMINDER','Upcoming appointment','appointment','reminder','info','Appointment reminder is due.',10,true,now(),'seed'),
  ('ALLERGY_REVIEW','Allergy review','encounter','banner','warning','Review documented allergies before completing the encounter.',20,true,now(),'seed')
on conflict(rule_key) do nothing;
