insert into clinical_form_definitions(definition_id,stable_key,latest_revision,effective_revision,created_at,created_by,updated_at,updated_by)
values('90f00000-0000-4000-8000-000000000025','legacy.bronchitis',1,null,now(),'legacy-adoption-seed',now(),'legacy-adoption-seed')
on conflict(stable_key) do nothing;

with sources as (
select d.stable_key,r.schema_json
from clinical_form_definitions d
join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=d.effective_revision
where d.stable_key in ('legacy.bronchitishistory','legacy.bronchitisearnoseexam','legacy.bronchitissinusoropharynx','legacy.bronchitiscardiacexam','legacy.bronchitislungexam','legacy.bronchitisdiagnosisplan')
), source_fields as (
select case stable_key
when 'legacy.bronchitishistory' then 10
when 'legacy.bronchitisearnoseexam' then 20
when 'legacy.bronchitissinusoropharynx' then 30
when 'legacy.bronchitiscardiacexam' then 40
when 'legacy.bronchitislungexam' then 50
when 'legacy.bronchitisdiagnosisplan' then 60 end as source_order,
field,ordinal
from sources cross join lateral jsonb_array_elements(schema_json->'fields') with ordinality as fields(field,ordinal)
), schema as (
select jsonb_build_object('stableKey','legacy.bronchitis','name','Bronchitis Form','purpose','Unified legacy Bronchitis encounter form compatibility capture.','contextScope','encounter','owningService','clinical_operations','capability','encounters.auth_a','signaturePolicy','author-only','sections',jsonb_build_array(
jsonb_build_object('key','illness_history','title','Illness history','sequence',10,'description','Onset of illness and HPI from the legacy Bronchitis form.'),
jsonb_build_object('key','pertinent_symptoms','title','Other pertinent symptoms','sequence',20,'description','Pertinent symptom checklist from the legacy Bronchitis form.'),
jsonb_build_object('key','history_review','title','History review','sequence',30,'description','History review checklist from the legacy Bronchitis form.'),
jsonb_build_object('key','ear_nose_exam','title','TM and nares examination','sequence',40,'description','Tympanic-membrane and nares checklist from the legacy Bronchitis form.'),
jsonb_build_object('key','sinus_oropharynx_exam','title','Sinus tenderness and oropharynx examination','sequence',50,'description','Sinus and oropharynx checklist from the legacy Bronchitis form.'),
jsonb_build_object('key','cardiac_exam','title','Cardiac examination','sequence',60,'description','Cardiac checklist and descriptive findings from the legacy Bronchitis form.'),
jsonb_build_object('key','lung_exam','title','Lung examination','sequence',70,'description','Lung checklist from the legacy Bronchitis form.'),
jsonb_build_object('key','diagnostic_plan','title','Diagnostic tests diagnosis and treatment','sequence',80,'description','Diagnostic tests, diagnoses, and treatment from the legacy Bronchitis form.')),'fields',(select jsonb_agg(field order by source_order,ordinal) from source_fields),'rules','[]'::jsonb) schema_json
)
insert into clinical_form_revisions(definition_id,revision,status,version,schema_json,renderer_version,schema_hash,author,reviewed_by,approved_by,effective_from,predecessor_revision,created_at,updated_at,updated_by) select d.definition_id,1,'effective',3,s.schema_json,'local-clinical-form-renderer-v1',encode(sha256(convert_to(s.schema_json::text,'utf8')),'hex'),'legacy-adoption-seed','legacy-adoption-seed','legacy-adoption-seed',now(),null,now(),now(),'legacy-adoption-seed' from clinical_form_definitions d cross join schema s where d.stable_key='legacy.bronchitis' on conflict(definition_id,revision) do nothing;
update clinical_form_definitions set effective_revision=1,updated_at=now(),updated_by='legacy-adoption-seed' where stable_key='legacy.bronchitis' and effective_revision is null;
insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash) select d.definition_id,1,'legacy-adopted-effective',null,'effective','legacy-adoption-seed','Compose all bounded legacy Bronchitis sections into one local compatibility form.',now(),r.schema_hash from clinical_form_definitions d join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1 where d.stable_key='legacy.bronchitis' and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=1 and e.action='legacy-adopted-effective');
