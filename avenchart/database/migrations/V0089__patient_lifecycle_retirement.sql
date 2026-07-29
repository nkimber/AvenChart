alter table patients
    add column if not exists lifecycle_status text;

alter table patients
    add column if not exists retired_at timestamptz;

alter table patients
    add column if not exists retired_by text;

alter table patients
    add column if not exists retirement_reason text;

update patients
set lifecycle_status = 'active'
where lifecycle_status is null;

alter table patients
    alter column lifecycle_status set default 'active';

alter table patients
    alter column lifecycle_status set not null;

alter table patients
    drop constraint if exists ck_patients_lifecycle_status;

alter table patients
    add constraint ck_patients_lifecycle_status
    check (lifecycle_status in ('active', 'retired'));

create table if not exists patient_lifecycle_events (
    event_id uuid primary key,
    patient_id text not null references patients(canonical_id),
    legacy_pid integer not null,
    action text not null check (action in ('retired', 'reactivated')),
    prior_status text not null check (prior_status in ('active', 'retired')),
    resulting_status text not null check (resulting_status in ('active', 'retired')),
    reason text not null,
    actor text not null,
    occurred_at timestamptz not null default now()
);

create index if not exists ix_patient_lifecycle_events_patient_time
    on patient_lifecycle_events (patient_id, occurred_at desc, event_id desc);
