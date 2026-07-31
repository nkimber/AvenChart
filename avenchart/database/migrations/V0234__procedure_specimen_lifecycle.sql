alter table lab_specimens
    add column if not exists specimen_status text not null default 'collected',
    add column if not exists specimen_version integer not null default 1;

alter table lab_specimens
    drop constraint if exists chk_lab_specimens_specimen_status;

alter table lab_specimens
    add constraint chk_lab_specimens_specimen_status
        check (specimen_status in ('collected', 'labeled', 'received', 'rejected', 'recollected'));

create table if not exists procedure_specimen_events (
    id bigserial primary key,
    specimen_id integer not null references lab_specimens(id) on delete cascade,
    action text not null check (action in ('collect', 'label', 'receive', 'reject', 'recollect')),
    previous_status text,
    current_status text not null,
    actor text not null,
    reason text not null,
    expected_version integer not null,
    resulting_version integer not null,
    occurred_at timestamp not null
);

create index if not exists idx_procedure_specimen_events_specimen
    on procedure_specimen_events (specimen_id, occurred_at desc, id desc);

insert into procedure_specimen_events
    (specimen_id, action, previous_status, current_status, actor, reason, expected_version, resulting_version, occurred_at)
select id, 'collect', null, specimen_status, 'migration', 'Existing specimen lifecycle baseline.', 0, specimen_version, collected_date
from lab_specimens specimen
where not exists (
    select 1 from procedure_specimen_events event where event.specimen_id = specimen.id
);
