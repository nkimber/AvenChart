with corrected as (
  select r.definition_id,r.revision,
    jsonb_set(r.schema_json,'{fields,0,children,0,options}',(
      select jsonb_agg(jsonb_set(option,'{code}',to_jsonb(lower(option->>'code'))))
      from jsonb_array_elements(r.schema_json->'fields'->0->'children'->0->'options') option
    )) as schema_json
  from clinical_form_definitions d
  join clinical_form_revisions r on r.definition_id=d.definition_id
  where d.stable_key='legacy.physicalexam' and r.revision in (1,2)
)
update clinical_form_revisions r
set schema_json=c.schema_json,
    schema_hash=encode(sha256(convert_to(c.schema_json::text,'utf8')),'hex'),
    updated_at=now(),
    updated_by='migration-v0116'
from corrected c
where r.definition_id=c.definition_id and r.revision=c.revision;

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,2,'legacy-adoption-corrected','effective','effective','migration-v0116','Normalize legacy Physical Exam line identifiers to the governed runtime option-code contract.',now(),r.schema_hash
from clinical_form_definitions d join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=2
where d.stable_key='legacy.physicalexam'
  and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=2 and e.action='legacy-adoption-corrected');
