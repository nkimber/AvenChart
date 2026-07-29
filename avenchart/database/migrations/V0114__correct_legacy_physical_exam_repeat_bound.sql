with corrected as (
  select r.definition_id, jsonb_set(r.schema_json,'{fields,0,repeatMaximum}','20'::jsonb) as schema_json
  from clinical_form_definitions d
  join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
  where d.stable_key='legacy.physicalexam'
), retired as (
  update clinical_form_revisions r
  set status='superseded',updated_at=now(),updated_by='migration-v0114'
  from corrected c
  where r.definition_id=c.definition_id and r.revision=1 and r.status='effective'
  returning r.definition_id
)
insert into clinical_form_revisions(definition_id,revision,status,version,schema_json,renderer_version,schema_hash,author,reviewed_by,approved_by,effective_from,predecessor_revision,created_at,updated_at,updated_by)
select c.definition_id,2,'effective',4,c.schema_json,'local-clinical-form-renderer-v1',encode(sha256(convert_to(c.schema_json::text,'utf8')),'hex'),'legacy-adoption-seed','legacy-adoption-seed','legacy-adoption-seed',now(),1,now(),now(),'migration-v0114'
from corrected c
on conflict(definition_id,revision) do nothing;

update clinical_form_definitions d
set latest_revision=2,effective_revision=2,updated_at=now(),updated_by='migration-v0114'
where d.stable_key='legacy.physicalexam'
  and exists(select 1 from clinical_form_revisions r where r.definition_id=d.definition_id and r.revision=2 and r.status='effective');

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,2,'legacy-adoption-corrected','effective','effective','migration-v0114','Correct the legacy Physical Exam repeating-group bound to the governed runtime maximum of twenty items.',now(),r.schema_hash
from clinical_form_definitions d join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=2
where d.stable_key='legacy.physicalexam'
  and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=2 and e.action='legacy-adoption-corrected');
