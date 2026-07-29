insert into clinical_form_definitions (
  definition_id, stable_key, latest_revision, effective_revision,
  created_at, created_by, updated_at, updated_by
)
values (
  '90f00000-0000-4000-8000-000000000004',
  'legacy.soap',
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
    "stableKey":"legacy.soap",
    "name":"SOAP",
    "purpose":"Legacy SOAP compatibility capture for subjective, objective, assessment, and plan.",
    "contextScope":"encounter",
    "owningService":"clinical_operations",
    "capability":"encounters.auth_a",
    "signaturePolicy":"author-only",
    "sections":[
      {"key":"soap","title":"SOAP","sequence":10,"description":"Mapped from the legacy SOAP encounter form."}
    ],
    "fields":[
      {"key":"subjective","sectionKey":"soap","label":"Subjective","type":"multiline","sequence":10,"required":false,"accessibilityLabel":"Subjective","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"objective","sectionKey":"soap","label":"Objective","type":"multiline","sequence":20,"required":false,"accessibilityLabel":"Objective","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"assessment","sectionKey":"soap","label":"Assessment","type":"multiline","sequence":30,"required":false,"accessibilityLabel":"Assessment","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"plan","sectionKey":"soap","label":"Plan","type":"multiline","sequence":40,"required":false,"accessibilityLabel":"Plan","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false}
    ],
    "rules":[]
  }'::jsonb,
  'local-clinical-form-renderer-v1',
  'dead7d95ea9efc8a9a4800aac321b53143a34f8d5663958935290184924a90a0',
  'legacy-adoption-seed',
  'legacy-adoption-seed',
  'legacy-adoption-seed',
  now(),
  null,
  now(),
  now(),
  'legacy-adoption-seed'
from clinical_form_definitions d
where d.stable_key = 'legacy.soap'
on conflict (definition_id, revision) do nothing;

update clinical_form_definitions d
set effective_revision = 1,
    updated_at = now(),
    updated_by = 'legacy-adoption-seed'
where d.stable_key = 'legacy.soap'
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
  'Adopt the bounded legacy SOAP fields as a local compatibility form.',
  now(),
  r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r
  on r.definition_id = d.definition_id and r.revision = 1
where d.stable_key = 'legacy.soap'
  and not exists (
    select 1
    from clinical_form_definition_events e
    where e.definition_id = d.definition_id
      and e.revision = 1
      and e.action = 'legacy-adopted-effective'
  );
