with corrected as (
  select r.definition_id, jsonb_set(r.schema_json,'{fields,0,repeatMaximum}','20'::jsonb) as schema_json
  from clinical_form_definitions d
  join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
  where d.stable_key='legacy.physicalexam'
)
update clinical_form_revisions r
set schema_json=c.schema_json,
    schema_hash=encode(sha256(convert_to(c.schema_json::text,'utf8')),'hex'),
    updated_at=now(),
    updated_by='migration-v0115'
from corrected c
where r.definition_id=c.definition_id and r.revision=1;

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,1,'legacy-adoption-history-repaired','superseded','superseded','migration-v0115','Repair the inherited Physical Exam revision-one bound so historical definition retrieval remains safe.',now(),r.schema_hash
from clinical_form_definitions d join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
where d.stable_key='legacy.physicalexam'
  and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=1 and e.action='legacy-adoption-history-repaired');
