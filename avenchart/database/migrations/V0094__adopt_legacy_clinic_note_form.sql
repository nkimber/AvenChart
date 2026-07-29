-- Keep the database stable-key contract aligned with the governed runtime.
alter table clinical_form_definitions
  drop constraint if exists clinical_form_definitions_stable_key_check;
alter table clinical_form_definitions
  add constraint clinical_form_definitions_stable_key_check
  check (stable_key ~ '^[a-z][a-z0-9_]*(\.[a-z0-9_]+)*$');

insert into clinical_form_definitions (
  definition_id, stable_key, latest_revision, effective_revision,
  created_at, created_by, updated_at, updated_by
)
values (
  '90f00000-0000-4000-8000-000000000002',
  'legacy.clinicnote',
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
    "stableKey":"legacy.clinicnote",
    "name":"Clinic Note",
    "purpose":"Legacy Clinic Note compatibility capture for encounter history, examination, plan, and follow-up.",
    "contextScope":"encounter",
    "owningService":"clinical_operations",
    "capability":"encounters.auth_a",
    "signaturePolicy":"author-only",
    "sections":[
      {"key":"clinical_note","title":"This Encounter","sequence":10,"description":"Mapped from the legacy Clinic Note encounter form."},
      {"key":"follow_up","title":"Follow Up","sequence":20,"description":"Mapped from the legacy Clinic Note follow-up controls."}
    ],
    "fields":[
      {"key":"history","sectionKey":"clinical_note","label":"History","type":"multiline","sequence":10,"required":false,"accessibilityLabel":"History","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"examination","sectionKey":"clinical_note","label":"Examination","type":"multiline","sequence":20,"required":false,"accessibilityLabel":"Examination","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"plan","sectionKey":"clinical_note","label":"Plan","type":"multiline","sequence":30,"required":false,"accessibilityLabel":"Plan","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"follow_up_status","sectionKey":"follow_up","label":"Follow up","type":"select","sequence":10,"required":false,"accessibilityLabel":"Follow up","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":"legacy_clinic_note_followup_v1","options":[{"code":"required_in","display":"Required in"},{"code":"pending_investigation","display":"Pending investigation"},{"code":"none_required","display":"None required"}],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"follow_up_timing","sectionKey":"follow_up","label":"When to follow up","type":"text","sequence":20,"required":false,"accessibilityLabel":"When to follow up","helpText":null,"maxLength":250,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false}
    ],
    "rules":[]
  }'::jsonb,
  'local-clinical-form-renderer-v1',
  'bac8559f3eacfee5f5cc6acfc32ceb93d2652240c0a66eda339d97fbfd2814a7',
  'legacy-adoption-seed',
  'legacy-adoption-seed',
  'legacy-adoption-seed',
  now(),
  null,
  now(),
  now(),
  'legacy-adoption-seed'
from clinical_form_definitions d
where d.stable_key = 'legacy.clinicnote'
on conflict (definition_id, revision) do nothing;

update clinical_form_definitions d
set effective_revision = 1,
    updated_at = now(),
    updated_by = 'legacy-adoption-seed'
where d.stable_key = 'legacy.clinicnote'
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
  'Adopt the bounded legacy Clinic Note fields as a local compatibility form.',
  now(),
  r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r
  on r.definition_id = d.definition_id and r.revision = 1
where d.stable_key = 'legacy.clinicnote'
  and not exists (
    select 1
    from clinical_form_definition_events e
    where e.definition_id = d.definition_id
      and e.revision = 1
      and e.action = 'legacy-adopted-effective'
  );
