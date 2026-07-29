update clinical_form_revisions r
set schema_json=jsonb_set(r.schema_json,'{rules,3,targetFieldKey}',to_jsonb('total_score'::text)),
    schema_hash='554327a15216462cf1b2e5edfbbc444f51c9e79da984a4408153ce3621b2c900',
    updated_at=now(),
    updated_by='legacy-adoption-seed'
from clinical_form_definitions d
where d.definition_id=r.definition_id
  and d.stable_key='legacy.phq9'
  and r.revision=1;

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,1,'legacy-adoption-invalid-revision-repaired','superseded','superseded','legacy-adoption-seed','Repair the non-operational PHQ-9 seed revision so all historical revisions remain safe to deserialize.',now(),r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
where d.stable_key='legacy.phq9'
  and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=1 and e.action='legacy-adoption-invalid-revision-repaired');
