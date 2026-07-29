with corrected as (
  select r.definition_id,r.revision,
    jsonb_set(r.schema_json,'{fields}',(
      select jsonb_agg(case when field->>'key' like 'ankle_diagnosis%' then jsonb_set(field,'{options}',(
        select jsonb_agg(jsonb_set(option,'{code}',to_jsonb(('icd9_' || (option->>'code'))::text)))
        from jsonb_array_elements(field->'options') option
      )) else field end)
      from jsonb_array_elements(r.schema_json->'fields') field
    )) as schema_json
  from clinical_form_definitions d join clinical_form_revisions r on r.definition_id=d.definition_id
  where d.stable_key='legacy.anklediagnosisplan' and r.revision=1
)
update clinical_form_revisions r
set schema_json=c.schema_json,schema_hash=encode(sha256(convert_to(c.schema_json::text,'utf8')),'hex'),updated_at=now(),updated_by='migration-v0121'
from corrected c where r.definition_id=c.definition_id and r.revision=c.revision;

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,1,'legacy-adoption-corrected','effective','effective','migration-v0121','Prefix legacy numeric ICD-9 option identifiers to the governed runtime identifier contract while retaining their display values.',now(),r.schema_hash
from clinical_form_definitions d join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
where d.stable_key='legacy.anklediagnosisplan'
  and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=1 and e.action='legacy-adoption-corrected');
