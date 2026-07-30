create table if not exists therapy_groups (
    id uuid primary key,
    name text not null,
    status text not null,
    facilitator_id integer references staff(id),
    description text,
    capacity integer not null,
    created_at timestamptz not null
);

create table if not exists therapy_group_members (
    group_id uuid not null references therapy_groups(id),
    patient_id text not null references patients(canonical_id),
    joined_at timestamptz not null,
    primary key (group_id, patient_id)
);

create table if not exists therapy_group_sessions (
    id uuid primary key,
    group_id uuid not null references therapy_groups(id),
    starts_at timestamptz not null,
    duration_minutes integer not null,
    topic text,
    status text not null,
    created_at timestamptz not null
);

create table if not exists therapy_group_session_participants (
    session_id uuid not null references therapy_group_sessions(id),
    patient_id text not null references patients(canonical_id),
    primary key (session_id, patient_id)
);

create table if not exists therapy_group_session_encounters (
    session_id uuid not null references therapy_group_sessions(id),
    patient_id text not null references patients(canonical_id),
    encounter_id integer not null,
    created_at timestamptz not null,
    primary key (session_id, patient_id)
);

create table if not exists therapy_group_session_attendance (
    session_id uuid not null references therapy_group_sessions(id),
    patient_id text not null references patients(canonical_id),
    attendance_status text not null default 'unrecorded' check (attendance_status in ('unrecorded', 'present', 'absent', 'excused')),
    note text,
    recorded_at timestamptz,
    primary key (session_id, patient_id)
);
