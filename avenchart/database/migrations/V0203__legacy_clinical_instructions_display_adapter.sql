-- Adds a read-only, source-labeled display adapter and deterministic extraction
-- fixtures for the bounded legacy Clinical Instructions form. These rows are
-- not converted governed instances and carry no migration approval.
with fixture (
  snapshot_id,
  source_row_id,
  source_active,
  source_recorded_at,
  raw_values
) as (
  values
    (
      '90f00000-0000-4000-9000-000000000003'::uuid,
      '881001',
      true,
      '2026-06-08T14:45:00Z'::timestamptz,
      '{
        "instruction":"Continue the current regimen. Call the clinic for worsening symptoms and bring all medication bottles to follow-up."
      }'::jsonb
    ),
    (
      '90f00000-0000-4000-9000-000000000004'::uuid,
      '881002',
      false,
      '2026-06-08T15:00:00Z'::timestamptz,
      '{
        "instruction":"Inactive historical instruction retained for source-display verification."
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
  'form_clinical_instructions',
  fixture.source_row_id,
  'legacy-ehr-8.1.0-form_clinical_instructions-v1',
  'legacy.clinicalinstructions',
  'MOD-PAT-0001',
  1000013,
  fixture.source_active,
  fixture.source_recorded_at,
  '2026-07-29T17:30:00Z'::timestamptz,
  'local-legacy-clinical-instructions-display-v1',
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
