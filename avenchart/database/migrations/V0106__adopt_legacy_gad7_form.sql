with source as (
    select r.schema_json
    from clinical_form_definitions d
    join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=d.effective_revision
    where d.stable_key='legacy.phq9'
), trimmed as (
    select jsonb_set(schema_json,'{fields}',(schema_json->'fields') - 8 - 7) as schema_json from source
), renamed as (
    select jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(schema_json,
        '{fields,0,key}','"nervous_score"'),'{fields,1,key}','"control_worry_score"'),'{fields,2,key}','"worry_score"'),
        '{fields,3,key}','"relax_score"'),'{fields,4,key}','"restless_score"'),'{fields,5,key}','"irritable_score"'),
        '{fields,6,key}','"fear_score"'),'{fields,7,helpText}','"Optional and not included in the total score. Shown when the GAD-7 total is above zero."') as schema_json
    from trimmed
), finalized as (
    select jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(jsonb_set(schema_json,
        '{stableKey}','"legacy.gad7"'),'{name}','"General Anxiety Disorder 7 (GAD-7)"'),'{purpose}','"Legacy GAD-7 compatibility screening capture with a bounded calculated total."'),
        '{fields,0,label}','"Feeling nervous, anxious, or on edge"'),'{fields,1,label}','"Not being able to stop or control worrying"'),'{fields,2,label}','"Worrying too much about different things"'),
        '{fields,3,label}','"Trouble relaxing"'),'{fields,4,label}','"Being so restless that it is hard to sit still"'),'{fields,5,label}','"Becoming easily annoyed or irritable"'),
        '{fields,6,label}','"Feeling afraid as if something awful might happen"'),'{fields,7,label}','"Difficulty with work, home care, or relationships"'),'{fields,8,label}','"Total GAD-7 score"'),'{fields,8,maximum}','21') as schema_json
    from renamed
), ruled as (
    select jsonb_set(schema_json,'{rules}',jsonb_build_array(
        jsonb_build_object('key','calculate_total_score','condition',jsonb_build_object('fieldKey','nervous_score','operator','is-not-empty','value',null),'action','calculate','targetFieldKey','total_score','message',null,'calculation',jsonb_build_object('operator','sum','operands',jsonb_build_array(jsonb_build_object('fieldKey','nervous_score','constant',null),jsonb_build_object('fieldKey','control_worry_score','constant',null),jsonb_build_object('fieldKey','worry_score','constant',null),jsonb_build_object('fieldKey','relax_score','constant',null),jsonb_build_object('fieldKey','restless_score','constant',null),jsonb_build_object('fieldKey','irritable_score','constant',null),jsonb_build_object('fieldKey','fear_score','constant',null)),'precision',0)),
        jsonb_build_object('key','hide_difficulty_when_total_zero','condition',jsonb_build_object('fieldKey','total_score','operator','equals','value',0),'action','hide','targetFieldKey','difficulty','message',null,'calculation',null),
        jsonb_build_object('key','require_difficulty_when_total_positive','condition',jsonb_build_object('fieldKey','total_score','operator','greater-than','value',0),'action','require','targetFieldKey','difficulty','message',null,'calculation',null))) as schema_json
    from finalized
), new_definition as (
    insert into clinical_form_definitions(definition_id,stable_key,latest_revision,effective_revision,created_at,created_by,updated_at,updated_by)
    values('90f00000-0000-4000-8000-000000000010','legacy.gad7',1,null,now(),'legacy-adoption-seed',now(),'legacy-adoption-seed')
    on conflict(stable_key) do update set updated_at=clinical_form_definitions.updated_at
    returning definition_id
)

insert into clinical_form_revisions(definition_id,revision,status,version,schema_json,renderer_version,schema_hash,author,reviewed_by,approved_by,effective_from,predecessor_revision,created_at,updated_at,updated_by)
select d.definition_id,1,'effective',3,schema_json,'local-clinical-form-renderer-v1',encode(sha256(convert_to(schema_json::text,'utf8')),'hex'),'legacy-adoption-seed','legacy-adoption-seed','legacy-adoption-seed',now(),null,now(),now(),'legacy-adoption-seed'
from new_definition d cross join ruled on conflict(definition_id,revision) do nothing;

update clinical_form_definitions d set effective_revision=1,updated_at=now(),updated_by='legacy-adoption-seed' where d.stable_key='legacy.gad7' and d.effective_revision is null;
