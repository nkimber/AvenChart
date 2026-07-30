create table if not exists critical_lab_result_acknowledgements (
    result_id integer primary key references lab_results(id) on delete cascade,
    status text not null default 'open' check (status in ('open', 'acknowledged')),
    version integer not null default 1,
    acknowledged_by text,
    acknowledged_at timestamp,
    acknowledgement_reason text
);

create table if not exists critical_lab_result_acknowledgement_events (
    id bigserial primary key,
    result_id integer not null references lab_results(id) on delete cascade,
    action text not null,
    previous_status text,
    current_status text not null,
    actor text not null,
    reason text not null,
    expected_version integer not null,
    resulting_version integer not null,
    occurred_at timestamp not null
);

create index if not exists idx_critical_lab_result_ack_events_result
    on critical_lab_result_acknowledgement_events (result_id, occurred_at desc, id desc);
