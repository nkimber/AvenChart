alter table lab_reports
    add column if not exists review_version integer not null default 1;

create table if not exists lab_report_review_events (
    id bigserial primary key,
    report_id integer not null references lab_reports(id) on delete cascade,
    action text not null,
    previous_status text,
    current_status text not null,
    assigned_to text,
    actor text not null,
    reason text,
    expected_version integer not null,
    resulting_version integer not null,
    occurred_at timestamp not null
);

create index if not exists idx_lab_report_review_events_report
    on lab_report_review_events (report_id, occurred_at desc, id desc);

insert into lab_report_review_events
    (report_id, action, previous_status, current_status, assigned_to, actor, reason, expected_version, resulting_version, occurred_at)
select
    lr.id,
    'baseline-import',
    null,
    coalesce(nullif(lr.review_status, ''), 'received'),
    lr.reviewed_by,
    coalesce(nullif(lr.reviewed_by, ''), 'legacy-import'),
    'Existing local report state at review-governance adoption.',
    0,
    lr.review_version,
    coalesce(lr.reviewed_at, lr.report_date, current_timestamp)
from lab_reports lr
where not exists (
    select 1
    from lab_report_review_events event
    where event.report_id = lr.id
);
