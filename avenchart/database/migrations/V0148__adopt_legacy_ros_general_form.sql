insert into clinical_form_definitions(definition_id,stable_key,latest_revision,effective_revision,created_at,created_by,updated_at,updated_by)
values('90f00000-0000-4000-8000-000000000043','legacy.rosgeneral',1,null,now(),'legacy-adoption-seed',now(),'legacy-adoption-seed')
on conflict(stable_key) do nothing;

with fields as (
  select jsonb_agg(jsonb_build_object('key',key,'sectionKey','general','label',label,'type','select','sequence',sequence,'required',false,'accessibilityLabel',label,'helpText','Legacy three-state Review of Systems value. Stored codes yes/no/na retain legacy YES/NO/N/A displays.','maxLength',null,'minimum',null,'maximum',null,'precision',null,'unit',null,'codeSystem',null,'options',jsonb_build_array(jsonb_build_object('code','yes','display','YES'),jsonb_build_object('code','no','display','NO'),jsonb_build_object('code','na','display','N/A')),'repeatMinimum',null,'repeatMaximum',null,'children','[]'::jsonb,'readOnly',false) order by sequence) value
  from (values
    ('weight_change','Weight change',10),('weakness','Weakness',20),('fatigue','Fatigue',30),('anorexia','Anorexia',40),('fever','Fever',50),('chills','Chills',60),('night_sweats','Night sweats',70),('insomnia','Insomnia',80),('irritability','Irritability',90),('heat_or_cold','Heat or cold intolerance',100)
  ) as source(key,label,sequence)
), schema as (
  select jsonb_build_object('stableKey','legacy.rosgeneral','name','Review of Systems General','purpose','Legacy Review of Systems constitutional compatibility capture.','contextScope','encounter','owningService','clinical_operations','capability','encounters.auth_a','signaturePolicy','author-only','sections',jsonb_build_array(jsonb_build_object('key','general','title','General','sequence',10,'description','Mapped from the opening constitutional fields in legacy form_ros.')),'fields',fields.value,'rules','[]'::jsonb) schema_json from fields
)
insert into clinical_form_revisions(definition_id,revision,status,version,schema_json,renderer_version,schema_hash,author,reviewed_by,approved_by,effective_from,predecessor_revision,created_at,updated_at,updated_by)
select d.definition_id,1,'effective',3,s.schema_json,'local-clinical-form-renderer-v1',encode(sha256(convert_to(s.schema_json::text,'utf8')),'hex'),'legacy-adoption-seed','legacy-adoption-seed','legacy-adoption-seed',now(),null,now(),now(),'legacy-adoption-seed' from clinical_form_definitions d cross join schema s where d.stable_key='legacy.rosgeneral' on conflict(definition_id,revision) do nothing;

update clinical_form_definitions set effective_revision=1,updated_at=now(),updated_by='legacy-adoption-seed' where stable_key='legacy.rosgeneral' and effective_revision is null;

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,1,'legacy-adopted-effective',null,'effective','legacy-adoption-seed','Adopt the first ten constitutional fields from legacy form_ros with its explicit three-state values.',now(),r.schema_hash from clinical_form_definitions d join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1 where d.stable_key='legacy.rosgeneral' and not exists(select 1 from clinical_form_definition_events e where e.definition_id=d.definition_id and e.revision=1 and e.action='legacy-adopted-effective');
