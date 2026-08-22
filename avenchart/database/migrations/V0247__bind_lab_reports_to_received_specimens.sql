-- Every report must point at a specimen from the same order. Existing generated
-- reports are given a received, traceable legacy specimen before the invariant is
-- made mandatory.
alter table lab_reports
    add column if not exists specimen_id integer;

insert into lab_specimens
    (order_id, specimen_identifier, accession_identifier, collected_date, comments, specimen_status, specimen_version)
select
    report.order_id,
    concat('legacy-report-', report.id),
    coalesce(nullif(btrim(report.specimen_number), ''), concat('legacy-accession-', report.id)),
    report.date_collected,
    'Legacy specimen created while binding an existing report to its source specimen.',
    'received',
    2
from lab_reports report
where report.specimen_id is null
  and not exists (
      select 1
      from lab_specimens specimen
      where specimen.order_id = report.order_id
        and specimen.specimen_identifier = concat('legacy-report-', report.id)
  );

update lab_reports report
set specimen_id = specimen.id
from lab_specimens specimen
where report.specimen_id is null
  and specimen.order_id = report.order_id
  and specimen.specimen_identifier = concat('legacy-report-', report.id);

do $$
begin
    if exists (select 1 from lab_reports where specimen_id is null) then
        raise exception 'Every lab report must have a specimen before the report/specimen invariant can be enabled.';
    end if;
end $$;

update lab_specimens specimen
set specimen_status = 'received',
    specimen_version = specimen_version + 1
from lab_reports report
where report.specimen_id = specimen.id
  and specimen.specimen_status in ('collected', 'labeled');

insert into procedure_specimen_events
    (specimen_id, action, previous_status, current_status, actor, reason, expected_version, resulting_version, occurred_at)
select
    specimen.id,
    'collect',
    null,
    'collected',
    'migration',
    'Legacy report specimen baseline.',
    0,
    1,
    specimen.collected_date
from lab_specimens specimen
inner join lab_reports report on report.specimen_id = specimen.id
where not exists (
    select 1 from procedure_specimen_events event where event.specimen_id = specimen.id
);

insert into procedure_specimen_events
    (specimen_id, action, previous_status, current_status, actor, reason, expected_version, resulting_version, occurred_at)
select
    specimen.id,
    'receive',
    'collected',
    'received',
    'migration',
    'Report-bound legacy specimen marked received.',
    greatest(specimen.specimen_version - 1, 1),
    specimen.specimen_version,
    report.report_date
from lab_specimens specimen
inner join lab_reports report on report.specimen_id = specimen.id
where specimen.specimen_status = 'received'
  and not exists (
      select 1
      from procedure_specimen_events event
      where event.specimen_id = specimen.id
        and event.action = 'receive'
  );

alter table lab_specimens
    add constraint uq_lab_specimens_id_order unique (id, order_id);

alter table lab_reports
    alter column specimen_id set not null;

alter table lab_reports
    add constraint fk_lab_reports_specimen_order
        foreign key (specimen_id, order_id)
        references lab_specimens (id, order_id)
        on delete restrict;

create index if not exists idx_lab_reports_specimen_id
    on lab_reports (specimen_id);
