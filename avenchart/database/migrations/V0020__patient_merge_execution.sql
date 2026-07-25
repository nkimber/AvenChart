alter table patients add column if not exists merged_into_patient_id text references patients(canonical_id);
alter table patients add column if not exists merged_at timestamptz;
alter table patients add column if not exists merged_by text;

create table if not exists patient_merge_audit_plans (
  audit_id uuid primary key,
  target_patient_id text not null,
  source_patient_id text not null,
  target_legacy_pid integer not null,
  source_legacy_pid integer not null,
  match_score integer not null,
  match_reasons text[] not null,
  rationale text,
  planned_by text not null,
  planned_at timestamptz not null,
  status text not null
);

create table if not exists patient_merge_executions (
  execution_id uuid primary key,
  audit_id uuid not null references patient_merge_audit_plans(audit_id),
  target_patient_id text not null references patients(canonical_id),
  source_patient_id text not null references patients(canonical_id),
  executed_by text not null,
  executed_at timestamptz not null,
  rolled_back_by text,
  rolled_back_at timestamptz,
  status text not null
);

create table if not exists patient_merge_execution_manifest_rows (
  execution_id uuid not null references patient_merge_executions(execution_id),
  table_name text not null,
  record_id text not null,
  primary key (execution_id, table_name, record_id)
);

create index if not exists ix_patient_merge_executions_source_patient
  on patient_merge_executions(source_patient_id);

create index if not exists ix_patient_merge_executions_target_patient
  on patient_merge_executions(target_patient_id);
