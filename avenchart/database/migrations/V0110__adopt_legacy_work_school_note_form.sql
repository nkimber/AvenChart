insert into clinical_form_definitions(definition_id,stable_key,latest_revision,effective_revision,created_at,created_by,updated_at,updated_by)
values('90f00000-0000-4000-8000-000000000013','legacy.workschoolnote',1,null,now(),'legacy-adoption-seed',now(),'legacy-adoption-seed')
on conflict(stable_key) do nothing;

with schema as (
  select jsonb_build_object(
    'stableKey','legacy.workschoolnote',
    'name','Work/School Note',
    'purpose','Legacy Work/School Note compatibility capture.',
    'contextScope','encounter',
    'owningService','clinical_operations',
    'capability','encounters.auth_a',
    'signaturePolicy','author-only',
    'sections',jsonb_build_array(jsonb_build_object('key','note','title','Work/School Note','sequence',10,'description','Mapped from the legacy Work/School Note encounter form.')),
    'fields',jsonb_build_array(
      jsonb_build_object('key','note_type','sectionKey','note','label','Note type','type','select','sequence',10,'required',true,'accessibilityLabel','Note type','helpText',null,'maxLength',null,'minimum',null,'maximum',null,'precision',null,'unit',null,'codeSystem','local-work-school-note-type-v1','options',jsonb_build_array(jsonb_build_object('code','work_note','display','WORK NOTE'),jsonb_build_object('code','school_note','display','SCHOOL NOTE')),'repeatMinimum',null,'repeatMaximum',null,'children','[]'::jsonb,'readOnly',false),
      jsonb_build_object('key','message','sectionKey','note','label','Message','type','multiline','sequence',20,'required',false,'accessibilityLabel','Message','helpText',null,'maxLength',4000,'minimum',null,'maximum',null,'precision',null,'unit',null,'codeSystem',null,'options','[]'::jsonb,'repeatMinimum',null,'repeatMaximum',null,'children','[]'::jsonb,'readOnly',false),
      jsonb_build_object('key','doctor','sectionKey','note','label','Doctor','type','text','sequence',30,'required',false,'accessibilityLabel','Doctor','helpText',null,'maxLength',255,'minimum',null,'maximum',null,'precision',null,'unit',null,'codeSystem',null,'options','[]'::jsonb,'repeatMinimum',null,'repeatMaximum',null,'children','[]'::jsonb,'readOnly',false),
      jsonb_build_object('key','date_of_signature','sectionKey','note','label','Date of signature','type','date','sequence',40,'required',false,'accessibilityLabel','Date of signature','helpText',null,'maxLength',null,'minimum',null,'maximum',null,'precision',null,'unit',null,'codeSystem',null,'options','[]'::jsonb,'repeatMinimum',null,'repeatMaximum',null,'children','[]'::jsonb,'readOnly',false)
    ),
    'rules','[]'::jsonb
  ) as schema_json
)
insert into clinical_form_revisions(definition_id,revision,status,version,schema_json,renderer_version,schema_hash,author,reviewed_by,approved_by,effective_from,predecessor_revision,created_at,updated_at,updated_by)
select d.definition_id,1,'effective',3,s.schema_json,'local-clinical-form-renderer-v1',encode(sha256(convert_to(s.schema_json::text,'utf8')),'hex'),'legacy-adoption-seed','legacy-adoption-seed','legacy-adoption-seed',now(),null,now(),now(),'legacy-adoption-seed'
from clinical_form_definitions d cross join schema s
where d.stable_key='legacy.workschoolnote'
on conflict(definition_id,revision) do nothing;

update clinical_form_definitions
set effective_revision=1,updated_at=now(),updated_by='legacy-adoption-seed'
where stable_key='legacy.workschoolnote' and effective_revision is null;

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,1,'legacy-adopted-effective',null,'effective','legacy-adoption-seed','Adopt the bounded legacy Work/School Note fields as a local compatibility form.',now(),r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
where d.stable_key='legacy.workschoolnote'
  and not exists(
      select 1 from clinical_form_definition_events e
      where e.definition_id=d.definition_id and e.revision=1 and e.action='legacy-adopted-effective'
  );
