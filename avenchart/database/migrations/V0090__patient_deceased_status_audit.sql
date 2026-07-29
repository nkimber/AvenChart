create table if not exists patient_deceased_status_events (
    event_id uuid primary key,
    patient_id text not null references patients(canonical_id),
    legacy_pid integer not null,
    action text not null check (action in ('recorded', 'corrected', 'cleared')),
    prior_deceased_date date,
    prior_deceased_reason text,
    resulting_deceased_date date,
    resulting_deceased_reason text,
    correction_reason text not null,
    actor text not null,
    occurred_at timestamptz not null default now()
);

create index if not exists ix_patient_deceased_status_events_patient_time
    on patient_deceased_status_events (patient_id, occurred_at desc, event_id desc);
