insert into clinical_form_definitions (
  definition_id, stable_key, latest_revision, effective_revision,
  created_at, created_by, updated_at, updated_by
)
values (
  '90f00000-0000-4000-8000-000000000003',
  'legacy.clinicalinstructions',
  1,
  null,
  now(),
  'legacy-adoption-seed',
  now(),
  'legacy-adoption-seed'
)
on conflict (stable_key) do nothing;

insert into clinical_form_revisions (
  definition_id, revision, status, version, schema_json, renderer_version,
  schema_hash, author, reviewed_by, approved_by, effective_from,
  predecessor_revision, created_at, updated_at, updated_by
)
select
  d.definition_id,
  1,
  'effective',
  3,
  '{
    "stableKey":"legacy.clinicalinstructions",
    "name":"Clinical Instructions",
    "purpose":"Legacy Clinical Instructions compatibility capture for encounter-specific patient instructions.",
    "contextScope":"encounter",
    "owningService":"clinical_operations",
    "capability":"encounters.auth_a",
    "signaturePolicy":"author-only",
    "sections":[
      {"key":"instructions","title":"Instructions","sequence":10,"description":"Mapped from the legacy Clinical Instructions encounter form."}
    ],
    "fields":[
      {"key":"instruction","sectionKey":"instructions","label":"Instructions","type":"multiline","sequence":10,"required":false,"accessibilityLabel":"Instructions","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false}
    ],
    "rules":[]
  }'::jsonb,
  'local-clinical-form-renderer-v1',
  '7d71a407153830b04d1d7234dfc2932e170f18acbddcdd742d0c9953ee8782ba',
  'legacy-adoption-seed',
  'legacy-adoption-seed',
  'legacy-adoption-seed',
  now(),
  null,
  now(),
  now(),
  'legacy-adoption-seed'
from clinical_form_definitions d
where d.stable_key = 'legacy.clinicalinstructions'
on conflict (definition_id, revision) do nothing;

update clinical_form_definitions d
set effective_revision = 1,
    updated_at = now(),
    updated_by = 'legacy-adoption-seed'
where d.stable_key = 'legacy.clinicalinstructions'
  and d.effective_revision is null
  and exists (
    select 1
    from clinical_form_revisions r
    where r.definition_id = d.definition_id
      and r.revision = 1
      and r.status = 'effective'
  );

insert into clinical_form_definition_events (
  definition_id, revision, action, from_status, to_status,
  actor, reason, occurred_at, snapshot_hash
)
select
  d.definition_id,
  1,
  'legacy-adopted-effective',
  null,
  'effective',
  'legacy-adoption-seed',
  'Adopt the bounded legacy Clinical Instructions field as a local compatibility form.',
  now(),
  r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r
  on r.definition_id = d.definition_id and r.revision = 1
where d.stable_key = 'legacy.clinicalinstructions'
  and not exists (
    select 1
    from clinical_form_definition_events e
    where e.definition_id = d.definition_id
      and e.revision = 1
      and e.action = 'legacy-adopted-effective'
  );
