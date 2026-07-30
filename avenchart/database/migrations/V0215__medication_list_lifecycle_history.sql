alter table medications
    add column if not exists lifecycle_version integer not null default 1;

create table if not exists medication_list_lifecycle_events (
    id bigserial primary key,
    medication_id text not null references medications(id) on delete cascade,
    action text not null check (action in ('created', 'deactivated', 'restored')),
    previous_activity integer,
    current_activity integer not null,
    actor text not null,
    reason text,
    expected_version integer not null,
    resulting_version integer not null,
    occurred_at timestamp not null
);

create index if not exists idx_medication_list_lifecycle_events_medication
    on medication_list_lifecycle_events (medication_id, occurred_at desc, id desc);
