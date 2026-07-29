insert into clinical_form_definition_events(definition_id,revision,action,from_status,to_status,actor,reason,occurred_at,snapshot_hash)
select d.definition_id,1,'legacy-adopted-effective',null,'effective','legacy-adoption-seed','Adopt the bounded legacy Transfer Summary fields as a local compatibility form.',now(),r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r on r.definition_id=d.definition_id and r.revision=1
where d.stable_key='legacy.transfersummary'
  and not exists(
      select 1
      from clinical_form_definition_events e
      where e.definition_id=d.definition_id
        and e.revision=1
        and e.action='legacy-adopted-effective'
  );
