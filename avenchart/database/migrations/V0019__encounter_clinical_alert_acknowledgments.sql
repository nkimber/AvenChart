create table if not exists encounter_clinical_alert_acknowledgments (
  encounter integer not null,
  rule_key text not null references clinical_alert_rules(rule_key),
  acknowledged_at timestamptz not null,
  acknowledged_by text not null,
  reopened_at timestamptz,
  reopened_by text,
  primary key (encounter, rule_key)
);

create index if not exists ix_encounter_clinical_alert_acknowledgments_open
  on encounter_clinical_alert_acknowledgments(encounter, rule_key)
  where reopened_at is null;
