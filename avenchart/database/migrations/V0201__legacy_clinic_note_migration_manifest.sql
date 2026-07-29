-- Defines a non-executing Clinic Note migration manifest. The contract is
-- deliberately draft-only: it can be reviewed and reconciled, but it cannot
-- create governed instances or represent production migration approval.
create table if not exists clinical_form_migration_manifests (
  manifest_id uuid primary key,
  stable_key text not null,
  source_system text not null,
  source_baseline_version text not null,
  extraction_revision text not null,
  source_schema text not null,
  source_table text not null,
  target_definition_revision integer not null check (target_definition_revision > 0),
  manifest_revision integer not null check (manifest_revision > 0),
  status text not null check (status in ('draft', 'in-review', 'locally-approved', 'rejected')),
  contract_json jsonb not null check (jsonb_typeof(contract_json) = 'object'),
  blockers_json jsonb not null check (jsonb_typeof(blockers_json) = 'array'),
  manifest_sha256 text not null check (manifest_sha256 ~ '^[0-9a-f]{64}$'),
  production_approved boolean not null default false,
  execution_enabled boolean not null default false,
  reviewed_by text,
  reviewed_at timestamptz,
  approved_by text,
  approved_at timestamptz,
  decision_reason text,
  created_at timestamptz not null,
  check (production_approved = false),
  check (execution_enabled = false),
  check (
    (reviewed_by is null and reviewed_at is null)
    or (reviewed_by is not null and reviewed_at is not null)
  ),
  check (
    (approved_by is null and approved_at is null)
    or (approved_by is not null and approved_at is not null)
  ),
  unique (
    stable_key,
    source_system,
    source_schema,
    source_table,
    extraction_revision,
    manifest_revision
  )
);

with manifest as (
  select jsonb_build_object(
    'contractRevision', 'local-clinical-form-migration-manifest-v1',
    'mappingRules', jsonb_build_array(
      jsonb_build_object(
        'sourceField', 'history',
        'targetField', 'history',
        'transform', 'exact-text'
      ),
      jsonb_build_object(
        'sourceField', 'examination',
        'targetField', 'examination',
        'transform', 'exact-text'
      ),
      jsonb_build_object(
        'sourceField', 'plan',
        'targetField', 'plan',
        'transform', 'exact-text'
      ),
      jsonb_build_object(
        'sourceField', 'followup_required',
        'targetField', 'follow_up_status',
        'transform', 'bounded-code-normalization',
        'knownCodes', jsonb_build_object(
          '0', 'none_required',
          '1', 'required_in',
          '2', 'pending_investigation'
        )
      ),
      jsonb_build_object(
        'sourceField', 'followup_timing',
        'targetField', 'follow_up_timing',
        'transform', 'exact-text'
      )
    ),
    'changedSemantics', jsonb_build_array(
      'Legacy follow-up integers become bounded target option identifiers.',
      'Modern target narratives have a 4,000-character limit.',
      'Any future converted record must enter the immutable governed instance lifecycle.'
    ),
    'errorDisposition', jsonb_build_array(
      'Missing expected fields block the source row.',
      'Unknown follow-up codes block the source row and retain the raw value.',
      'Extra source fields block the source row until the manifest is superseded.',
      'Inactive source rows remain visible but are not eligible for conversion.'
    ),
    'reconciliationRequired', jsonb_build_array(
      'Source, active, inactive, fully mapped, unmapped, eligible, and blocked row totals.',
      'Sorted source snapshot digest and exact target schema SHA-256.',
      'Zero governed instances before an approved executor exists.',
      'Per-row disposition with source row and raw-value evidence.'
    ),
    'compensationRollback', jsonb_build_array(
      'Never modify or delete the captured source snapshot.',
      'A future executor must record manifest, source snapshot, and target instance lineage.',
      'Only unsigned migration drafts may be compensated; signed clinical content is amended, never deleted.',
      'Stop on reconciliation drift and require a new manifest revision before retry.'
    ),
    'requiredApprovals', jsonb_build_array(
      'Clinical owner field and semantic acceptance.',
      'Health information management retention and disclosure approval.',
      'Data governance source completeness and reconciliation approval.',
      'Security authorization and minimum-necessary approval.',
      'Operations rollout, rollback, monitoring, and incident acceptance.'
    )
  ) as contract_json
)
insert into clinical_form_migration_manifests (
  manifest_id,
  stable_key,
  source_system,
  source_baseline_version,
  extraction_revision,
  source_schema,
  source_table,
  target_definition_revision,
  manifest_revision,
  status,
  contract_json,
  blockers_json,
  manifest_sha256,
  production_approved,
  execution_enabled,
  reviewed_by,
  reviewed_at,
  approved_by,
  approved_at,
  decision_reason,
  created_at
)
select
  '90f00000-0000-4000-a000-000000000001'::uuid,
  'legacy.clinicnote',
  'legacy-legacy-ehr',
  'Legacy EHR 8.1.0',
  'legacy-ehr-shared-synthetic-v1',
  'legacy-ehr',
  'form_clinic_note',
  1,
  1,
  'draft',
  manifest.contract_json,
  jsonb_build_array(
    'Production legacy extraction completeness has not been established.',
    'No accountable owner has approved the field semantics or migration population.',
    'No conversion executor, idempotency ledger, reconciliation sign-off, or compensation runbook exists.',
    'Retention, disclosure, rollout, monitoring, incident, and rollback policy remain unapproved.'
  ),
  encode(sha256(convert_to(manifest.contract_json::text, 'utf8')), 'hex'),
  false,
  false,
  null,
  null,
  null,
  null,
  null,
  '2026-07-29T16:30:00Z'::timestamptz
from manifest
on conflict (
  stable_key,
  source_system,
  source_schema,
  source_table,
  extraction_revision,
  manifest_revision
) do nothing;
