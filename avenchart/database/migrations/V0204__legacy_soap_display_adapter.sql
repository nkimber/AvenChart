-- Adds a read-only, source-labeled display adapter and deterministic extraction
-- fixtures for the bounded legacy SOAP form. These rows are not converted
-- governed instances and carry no migration approval.
with fixture (
  snapshot_id,
  source_row_id,
  source_active,
  source_recorded_at,
  raw_values
) as (
  values
    (
      '90f00000-0000-4000-9000-000000000005'::uuid,
      '882001',
      true,
      '2026-06-08T15:15:00Z'::timestamptz,
      '{
        "subjective":"Reports improved exertional tolerance and no pain at rest.",
        "objective":"Alert, comfortable, and in no acute distress.",
        "assessment":"Symptoms are stable on the current regimen.",
        "plan":"Continue medications and return in two weeks."
      }'::jsonb
    ),
    (
      '90f00000-0000-4000-9000-000000000006'::uuid,
      '882002',
      false,
      '2026-06-08T15:30:00Z'::timestamptz,
      '{
        "subjective":"Inactive historical SOAP narrative retained for source display.",
        "objective":"",
        "assessment":"Superseded legacy assessment.",
        "plan":"No current action; retained as inactive source evidence."
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
  'legacy-legacy-ehr',
  'Legacy EHR 8.1.0',
  'legacy-ehr-shared-synthetic-v1',
  'legacy-ehr',
  'form_soap',
  fixture.source_row_id,
  'legacy-ehr-8.1.0-form_soap-v1',
  'legacy.soap',
  'MOD-PAT-0001',
  1000013,
  fixture.source_active,
  fixture.source_recorded_at,
  '2026-07-29T18:00:00Z'::timestamptz,
  'local-legacy-soap-display-v1',
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
