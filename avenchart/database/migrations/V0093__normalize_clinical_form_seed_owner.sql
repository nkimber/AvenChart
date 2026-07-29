update clinical_form_revisions r
set schema_json = jsonb_set(
      r.schema_json,
      '{owningService}',
      to_jsonb('clinical_operations'::text),
      false
    ),
    schema_hash = 'a77c82aa74498d8e8ff79485b09834b7804f689348ff33edbb6d733f0d7f1e95',
    updated_at = now(),
    updated_by = 'migration'
from clinical_form_definitions d
where d.definition_id = r.definition_id
  and d.stable_key = 'clinical.observation'
  and r.revision = 1
  and r.schema_json->>'owningService' = 'clinical-operations';

update clinical_form_definition_events e
set snapshot_hash =
      'a77c82aa74498d8e8ff79485b09834b7804f689348ff33edbb6d733f0d7f1e95'
from clinical_form_definitions d
where d.definition_id = e.definition_id
  and d.stable_key = 'clinical.observation'
  and e.revision = 1
  and e.action = 'seeded-effective';
