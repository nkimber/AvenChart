create table if not exists patient_sdoh_assessments (
  assessment_id uuid primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  assessment_date date not null,
  screening_tool text,
  assessor text not null,
  instrument_score integer not null,
  domains jsonb not null default '{}'::jsonb,
  interventions text,
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);

create index if not exists ix_patient_sdoh_assessments_patient_history
  on patient_sdoh_assessments(patient_id, assessment_date desc, updated_at desc);
