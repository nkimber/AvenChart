-- Adds a read-only, source-labeled snapshot boundary for historical Clinic Note
-- display. These rows are synthetic extraction fixtures, not converted governed
-- form instances and not an approved migration manifest.
create table if not exists legacy_clinical_form_snapshots (
  snapshot_id uuid primary key,
  source_system text not null,
  source_baseline_version text not null,
  extraction_revision text not null,
  source_schema text not null,
  source_table text not null,
  source_row_id text not null,
  source_revision text not null,
  source_form_key text not null,
  patient_id text not null references patients(canonical_id) on delete restrict,
  encounter_id integer not null references encounters(encounter) on delete restrict,
  source_active boolean not null,
  source_recorded_at timestamptz,
  captured_at timestamptz not null,
  adapter_revision text not null,
  target_definition_revision integer not null check (target_definition_revision > 0),
  raw_values jsonb not null check (jsonb_typeof(raw_values) = 'object'),
  raw_sha256 text not null check (raw_sha256 ~ '^[0-9a-f]{64}$'),
  unique (
    source_system,
    source_schema,
    source_table,
    source_row_id,
    extraction_revision
  )
);

create index if not exists idx_legacy_clinical_form_snapshots_patient_recorded
  on legacy_clinical_form_snapshots (
    patient_id,
    source_recorded_at desc,
    snapshot_id
  );

with fixture (
  snapshot_id,
  source_row_id,
  source_active,
  source_recorded_at,
  raw_values
) as (
  values
    (
      '90f00000-0000-4000-9000-000000000001'::uuid,
      '880001',
      true,
      '2026-06-08T14:15:00Z'::timestamptz,
      '{
        "history":"Legacy history: intermittent exertional chest discomfort.",
        "examination":"Legacy examination: vital signs stable; no acute distress.",
        "plan":"Legacy plan: continue current regimen and return if symptoms worsen.",
        "followup_required":1,
        "followup_timing":"2 weeks"
      }'::jsonb
    ),
    (
      '90f00000-0000-4000-9000-000000000002'::uuid,
      '880002',
      false,
      '2026-06-08T14:30:00Z'::timestamptz,
      '{
        "history":"Inactive legacy snapshot retained for display validation.",
        "examination":"",
        "plan":"Review the unmapped legacy follow-up code before any migration decision.",
        "followup_required":9,
        "followup_timing":""
      }'::jsonb
    )
)
insert into legacy_clinical_form_snapshots (
  snapshot_id,
  source_system,
  source_baseline_version,
  extraction_revision,
  source_schema,
  source_table,
  source_row_id,
  source_revision,
  source_form_key,
  patient_id,
  encounter_id,
  source_active,
  source_recorded_at,
  captured_at,
  adapter_revision,
  target_definition_revision,
  raw_values,
  raw_sha256
)
select
  fixture.snapshot_id,
  'legacy-ehr',
  'Legacy EHR 8.1.0',
  'avenchart-shared-synthetic-v1',
  'legacy-ehr',
  'form_clinic_note',
  fixture.source_row_id,
  'legacy-ehr-8.1.0-form_clinic_note-v1',
  'legacy.clinicnote',
  'MOD-PAT-0001',
  1000013,
  fixture.source_active,
  fixture.source_recorded_at,
  '2026-07-29T14:00:00Z'::timestamptz,
  'local-legacy-clinic-note-display-v1',
  1,
  fixture.raw_values,
  encode(sha256(convert_to(fixture.raw_values::text, 'utf8')), 'hex')
from fixture
on conflict (
  source_system,
  source_schema,
  source_table,
  source_row_id,
  extraction_revision
) do nothing;
