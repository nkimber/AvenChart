-- V0091 seeded the bounded clinical observation form before the runtime was
-- exercised end-to-end. Correct its schema rather than leaving an ineffective
-- form in the catalog: units belong only to measurement fields, owning-service
-- keys use the approved underscore grammar, and the high-pain warning targets
-- the related disposition field instead of creating a self-referential rule.
update clinical_form_revisions
set schema_json = jsonb_set(
        jsonb_set(
          jsonb_set(schema_json, '{owningService}', '"clinical_operations"'::jsonb),
          '{fields,1,unit}', 'null'::jsonb),
        '{rules,1,targetFieldKey}', '"disposition"'::jsonb),
    schema_hash = 'ad3cdf7278aae393ff48a4c7e528c053be963f6c5fa13f79e534931d894c206c',
    version = version + 1,
    updated_at = now(),
    updated_by = 'migration-v0092'
where definition_id = '90f00000-0000-4000-8000-000000000001'
  and revision = 1;

update clinical_form_definitions
set updated_at = now(),
    updated_by = 'migration-v0092'
where definition_id = '90f00000-0000-4000-8000-000000000001';

insert into clinical_form_definition_events (
  definition_id, revision, action, from_status, to_status,
  actor, reason, occurred_at, snapshot_hash
)
select
  definition_id,
  revision,
  'seed-schema-corrected',
  status,
  status,
  'migration-v0092',
  'Correct the ineffective seeded schema before clinical use.',
  now(),
  schema_hash
from clinical_form_revisions
where definition_id = '90f00000-0000-4000-8000-000000000001'
  and revision = 1
  and not exists (
    select 1
    from clinical_form_definition_events
    where definition_id = '90f00000-0000-4000-8000-000000000001'
      and revision = 1
      and action = 'seed-schema-corrected'
  );
