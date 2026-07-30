alter table lab_specimens
    add column if not exists specimen_status text not null default 'collected',
    add column if not exists specimen_version integer not null default 1,
    add column if not exists updated_by text,
    add column if not exists updated_at timestamp;

update lab_specimens
set specimen_status = 'collected'
where specimen_status is null
   or trim(specimen_status) = '';

alter table lab_specimens
    drop constraint if exists ck_lab_specimens_specimen_status;

alter table lab_specimens
    add constraint ck_lab_specimens_specimen_status
    check (specimen_status in ('collected', 'labeled', 'received', 'rejected', 'recollected'));

create table if not exists procedure_specimen_events (
    id bigserial primary key,
    specimen_id integer not null references lab_specimens(id) on delete cascade,
    action text not null,
    previous_status text,
    current_status text not null,
    actor text not null,
    reason text not null,
    expected_version integer not null,
    resulting_version integer not null,
    specimen_identifier text,
    accession_identifier text,
    collected_date timestamp,
    condition_code text,
    specimen_condition text,
    comments text,
    occurred_at timestamp not null,
    constraint ck_procedure_specimen_events_action
        check (action in ('collect', 'label', 'receive', 'reject', 'recollect')),
    constraint ck_procedure_specimen_events_status
        check (current_status in ('collected', 'labeled', 'received', 'rejected', 'recollected')),
    constraint ck_procedure_specimen_events_reason
        check (char_length(trim(reason)) between 1 and 500)
);

create index if not exists idx_procedure_specimen_events_specimen
    on procedure_specimen_events (specimen_id, occurred_at desc, id desc);

insert into procedure_specimen_events
    (specimen_id, action, previous_status, current_status, actor, reason, expected_version,
     resulting_version, specimen_identifier, accession_identifier, collected_date,
     condition_code, specimen_condition, comments, occurred_at)
select
    specimen.id,
    'collect',
    null,
    specimen.specimen_status,
    coalesce(nullif(trim(specimen.updated_by), ''), 'legacy-import'),
    'Existing local specimen adopted into governed lifecycle.',
    0,
    specimen.specimen_version,
    specimen.specimen_identifier,
    specimen.accession_identifier,
    specimen.collected_date,
    specimen.condition_code,
    specimen.specimen_condition,
    specimen.comments,
    coalesce(specimen.updated_at, specimen.collected_date, current_timestamp)
from lab_specimens specimen
where not exists (
    select 1
    from procedure_specimen_events event
    where event.specimen_id = specimen.id
);
