insert into clinical_form_definitions (
  definition_id, stable_key, latest_revision, effective_revision,
  created_at, created_by, updated_at, updated_by
)
values (
  '90f00000-0000-4000-8000-000000000005',
  'legacy.careplan',
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
    "stableKey":"legacy.careplan",
    "name":"Care Plan",
    "purpose":"Legacy Care Plan compatibility capture for bounded encounter plan items.",
    "contextScope":"encounter",
    "owningService":"clinical_operations",
    "capability":"encounters.auth_a",
    "signaturePolicy":"author-only",
    "sections":[
      {"key":"care_plan","title":"Care Plan","sequence":10,"description":"Mapped from the legacy Care Plan encounter form."}
    ],
    "fields":[
      {"key":"items","sectionKey":"care_plan","label":"Care plan entries","type":"repeat","sequence":10,"required":false,"accessibilityLabel":"Care plan entries","helpText":"Each entry maps a legacy Care Plan row. The local form permits up to 20 entries.","maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":0,"repeatMaximum":20,"children":[
        {"key":"code","sectionKey":"","label":"Code","type":"text","sequence":10,"required":false,"accessibilityLabel":"Code","helpText":null,"maxLength":240,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"code_text","sectionKey":"","label":"Code text","type":"text","sequence":20,"required":false,"accessibilityLabel":"Code text","helpText":null,"maxLength":240,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"plan_type","sectionKey":"","label":"Type","type":"select","sequence":30,"required":false,"accessibilityLabel":"Type","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":"legacy_plan_of_care_type_v1","options":[{"code":"plan_of_care","display":"Plan of Care"},{"code":"test_or_order","display":"Test/Order"},{"code":"procedure","display":"Procedure"},{"code":"appointments","display":"Appointments"},{"code":"instructions","display":"Instructions"},{"code":"goal","display":"Goal"},{"code":"health_concern","display":"Health Concern"},{"code":"medication","display":"Medication"},{"code":"intervention","display":"Intervention"},{"code":"planned_medication_activity","display":"Planned Medication Act"},{"code":"supply_order","display":"Supply Order Act"},{"code":"device_order","display":"Device Order"}],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"service_date","sectionKey":"","label":"Date","type":"datetime","sequence":40,"required":false,"accessibilityLabel":"Date","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"target_date","sectionKey":"","label":"Target date","type":"datetime","sequence":50,"required":false,"accessibilityLabel":"Target date","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"end_date","sectionKey":"","label":"End date","type":"datetime","sequence":60,"required":false,"accessibilityLabel":"End date","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"plan_status","sectionKey":"","label":"Status","type":"select","sequence":70,"required":false,"accessibilityLabel":"Status","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":"legacy_care_plan_status_v1","options":[{"code":"draft","display":"Draft"},{"code":"active","display":"Active"},{"code":"on_hold","display":"On hold"},{"code":"revoked","display":"Revoked"},{"code":"completed","display":"Completed"},{"code":"entered_in_error","display":"Entered in error"},{"code":"unknown","display":"Unknown"}],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"description","sectionKey":"","label":"Description","type":"multiline","sequence":80,"required":false,"accessibilityLabel":"Description","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"reason_code","sectionKey":"","label":"Reason code","type":"text","sequence":90,"required":false,"accessibilityLabel":"Reason code","helpText":null,"maxLength":240,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"reason_description","sectionKey":"","label":"Reason code text","type":"text","sequence":100,"required":false,"accessibilityLabel":"Reason code text","helpText":null,"maxLength":240,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"reason_status","sectionKey":"","label":"Reason status","type":"text","sequence":110,"required":false,"accessibilityLabel":"Reason status","helpText":null,"maxLength":80,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"reason_start_date","sectionKey":"","label":"Reason recording date","type":"datetime","sequence":120,"required":false,"accessibilityLabel":"Reason recording date","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
        {"key":"reason_end_date","sectionKey":"","label":"Reason end date","type":"datetime","sequence":130,"required":false,"accessibilityLabel":"Reason end date","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false}
      ],"readOnly":false}
    ],
    "rules":[]
  }'::jsonb,
  'local-clinical-form-renderer-v1',
  'fe5d0b72861330ec2da403f62910467dd035112858ee34f544b13768f6e8c535',
  'legacy-adoption-seed',
  'legacy-adoption-seed',
  'legacy-adoption-seed',
  now(),
  null,
  now(),
  now(),
  'legacy-adoption-seed'
from clinical_form_definitions d
where d.stable_key = 'legacy.careplan'
on conflict (definition_id, revision) do nothing;

update clinical_form_definitions d
set effective_revision = 1,
    updated_at = now(),
    updated_by = 'legacy-adoption-seed'
where d.stable_key = 'legacy.careplan'
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
  'Adopt the bounded legacy Care Plan rows as a local compatibility form.',
  now(),
  r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r
  on r.definition_id = d.definition_id and r.revision = 1
where d.stable_key = 'legacy.careplan'
  and not exists (
    select 1
    from clinical_form_definition_events e
    where e.definition_id = d.definition_id
      and e.revision = 1
      and e.action = 'legacy-adopted-effective'
  );
