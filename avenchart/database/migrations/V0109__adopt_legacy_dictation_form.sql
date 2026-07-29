insert into clinical_form_definitions(definition_id,stable_key,latest_revision,effective_revision,created_at,created_by,updated_at,updated_by)
values('90f00000-0000-4000-8000-000000000012','legacy.dictation',1,null,now(),'legacy-adoption-seed',now(),'legacy-adoption-seed')
on conflict(stable_key) do nothing;

insert into clinical_form_revisions(definition_id,revision,status,version,schema_json,renderer_version,schema_hash,author,reviewed_by,approved_by,effective_from,predecessor_revision,created_at,updated_at,updated_by)
select d.definition_id,1,'effective',3,
  '{"stableKey":"legacy.dictation","name":"Speech Dictation","purpose":"Legacy Speech Dictation compatibility capture.","contextScope":"encounter","owningService":"clinical_operations","capability":"encounters.auth_a","signaturePolicy":"author-only","sections":[{"key":"dictation","title":"Speech Dictation","sequence":10,"description":"Mapped from the legacy Speech Dictation encounter form."}],"fields":[{"key":"dictation","sectionKey":"dictation","label":"Dictation","type":"multiline","sequence":10,"required":false,"accessibilityLabel":"Dictation","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},{"key":"additional_notes","sectionKey":"dictation","label":"Additional Notes","type":"multiline","sequence":20,"required":false,"accessibilityLabel":"Additional Notes","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false}],"rules":[]}'::jsonb,
  'local-clinical-form-renderer-v1',
  encode(sha256(convert_to('{"stableKey":"legacy.dictation","name":"Speech Dictation","purpose":"Legacy Speech Dictation compatibility capture.","contextScope":"encounter","owningService":"clinical_operations","capability":"encounters.auth_a","signaturePolicy":"author-only","sections":[{"key":"dictation","title":"Speech Dictation","sequence":10,"description":"Mapped from the legacy Speech Dictation encounter form."}],"fields":[{"key":"dictation","sectionKey":"dictation","label":"Dictation","type":"multiline","sequence":10,"required":false,"accessibilityLabel":"Dictation","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},{"key":"additional_notes","sectionKey":"dictation","label":"Additional Notes","type":"multiline","sequence":20,"required":false,"accessibilityLabel":"Additional Notes","helpText":null,"maxLength":4000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false}],"rules":[]}'::jsonb::text,'utf8')),'hex'),
  'legacy-adoption-seed','legacy-adoption-seed','legacy-adoption-seed',now(),null,now(),now(),'legacy-adoption-seed'
from clinical_form_definitions d
where d.stable_key='legacy.dictation'
on conflict(definition_id,revision) do nothing;

update clinical_form_definitions
set effective_revision=1,updated_at=now(),updated_by='legacy-adoption-seed'
where stable_key='legacy.dictation' and effective_revision is null;

insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,1,'legacy-adopted-effective',null,'effective','legacy-adoption-seed','Adopt the bounded legacy Speech Dictation fields as a local compatibility form.',now(),r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
where d.stable_key='legacy.dictation'
  and not exists(
      select 1 from clinical_form_definition_events e
      where e.definition_id=d.definition_id and e.revision=1 and e.action='legacy-adopted-effective'
  );
