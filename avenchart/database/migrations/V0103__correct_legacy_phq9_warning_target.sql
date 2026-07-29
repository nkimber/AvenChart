with corrected_schema as (
    select
        d.definition_id,
        jsonb_set(
            r.schema_json,
            '{rules,3,targetFieldKey}',
            '"total_score"'::jsonb) as schema_json
    from clinical_form_definitions d
    join clinical_form_revisions r
      on r.definition_id = d.definition_id
     and r.revision = 1
    where d.stable_key = 'legacy.phq9'
)
insert into clinical_form_revisions(definition_id,revision,status,version,schema_json,renderer_version,schema_hash,author,reviewed_by,approved_by,effective_from,predecessor_revision,created_at,updated_at,updated_by)
select definition_id,2,'effective',1,schema_json,'local-clinical-form-renderer-v1','554327a15216462cf1b2e5edfbbc444f51c9e79da984a4408153ce3621b2c900','legacy-adoption-seed','legacy-adoption-seed','legacy-adoption-seed',now(),1,now(),now(),'legacy-adoption-seed'
from corrected_schema
on conflict(definition_id,revision) do nothing;

update clinical_form_revisions r
set status='superseded',version=version+1,effective_to=coalesce(effective_to,now()),updated_at=now(),updated_by='legacy-adoption-seed'
from clinical_form_definitions d
where d.definition_id=r.definition_id
  and d.stable_key='legacy.phq9'
  and r.revision=1
  and r.status='effective';

update clinical_form_definitions d
set latest_revision=2,effective_revision=2,updated_at=now(),updated_by='legacy-adoption-seed'
where d.stable_key='legacy.phq9'
  and exists(select 1 from clinical_form_revisions r where r.definition_id=d.definition_id and r.revision=2 and r.status='effective');

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,1,'superseded','effective','superseded','legacy-adoption-seed','Correct the local PHQ-9 warning target without altering the applied adoption migration.',now(),r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
where d.stable_key='legacy.phq9'
  and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=1 and e.action='superseded');

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,2,'legacy-adoption-corrected-effective',null,'effective','legacy-adoption-seed','Correct the local PHQ-9 warning target without altering the applied adoption migration.',now(),r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=2
where d.stable_key='legacy.phq9'
  and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=2 and e.action='legacy-adoption-corrected-effective');
