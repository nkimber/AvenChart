create table if not exists patient_portal_appointment_requests (
    appointment_id text primary key references appointments(id) on delete cascade,
    patient_id text not null,
    legacy_pid integer not null,
    appointment_date date not null,
    start_time time without time zone not null,
    duration_minutes integer not null,
    category_id integer null,
    provider_id integer null,
    facility_id integer null,
    title text not null,
    reason text null,
    raw_status text not null,
    current_state text not null
        check (current_state in ('pending', 'accepted', 'declined', 'cancelled')),
    version integer not null check (version > 0),
    requested_at timestamp with time zone not null,
    updated_at timestamp with time zone not null,
    evidence_source text not null
        check (evidence_source in ('runtime', 'migration-backfill'))
);

create index if not exists idx_patient_portal_appointment_requests_patient
    on patient_portal_appointment_requests (patient_id, requested_at desc, appointment_id);

create table if not exists patient_portal_appointment_request_events (
    event_id uuid primary key default gen_random_uuid(),
    appointment_id text not null
        references patient_portal_appointment_requests(appointment_id) on delete cascade,
    sequence integer not null check (sequence > 0),
    action text not null
        check (action in ('requested', 'accepted', 'declined', 'cancelled', 'pending', 'updated', 'discovered')),
    state text not null
        check (state in ('pending', 'accepted', 'declined', 'cancelled')),
    raw_status text not null,
    occurred_at timestamp with time zone not null default current_timestamp,
    evidence_source text not null
        check (evidence_source in ('runtime', 'migration-backfill')),
    unique (appointment_id, sequence)
);

create index if not exists idx_patient_portal_appointment_request_events_request
    on patient_portal_appointment_request_events (appointment_id, sequence desc);

create or replace function capture_patient_portal_appointment_request()
returns trigger
language plpgsql
as $$
declare
    next_state text;
    next_action text;
    next_version integer;
    prior_state text;
begin
    if new.id not like 'APPT-PORTAL-%' then
        return new;
    end if;

    if tg_op = 'INSERT' then
        next_state := case
            when coalesce(new.status, '^') in ('^', '~', '!') then 'pending'
            when new.status = 'x' then 'declined'
            else 'accepted'
        end;

        insert into patient_portal_appointment_requests (
            appointment_id,
            patient_id,
            legacy_pid,
            appointment_date,
            start_time,
            duration_minutes,
            category_id,
            provider_id,
            facility_id,
            title,
            reason,
            raw_status,
            current_state,
            version,
            requested_at,
            updated_at,
            evidence_source
        )
        values (
            new.id,
            new.patient_id,
            new.pid,
            new.appointment_date,
            new.start_time,
            new.duration_minutes,
            new.category_id,
            new.provider_id,
            new.facility_id,
            coalesce(nullif(trim(new.title), ''), 'Appointment request'),
            nullif(trim(new.comments), ''),
            coalesce(new.status, '^'),
            next_state,
            1,
            current_timestamp,
            current_timestamp,
            'runtime'
        )
        on conflict (appointment_id) do nothing;

        insert into patient_portal_appointment_request_events (
            appointment_id,
            sequence,
            action,
            state,
            raw_status,
            evidence_source
        )
        values (
            new.id,
            1,
            'requested',
            next_state,
            coalesce(new.status, '^'),
            'runtime'
        )
        on conflict (appointment_id, sequence) do nothing;

        return new;
    end if;

    if not (
        old.appointment_date is distinct from new.appointment_date
        or old.start_time is distinct from new.start_time
        or old.duration_minutes is distinct from new.duration_minutes
        or old.category_id is distinct from new.category_id
        or old.provider_id is distinct from new.provider_id
        or old.facility_id is distinct from new.facility_id
        or old.title is distinct from new.title
        or old.comments is distinct from new.comments
        or old.status is distinct from new.status
    ) then
        return new;
    end if;

    select current_state
    into prior_state
    from patient_portal_appointment_requests
    where appointment_id = new.id
    for update;

    if prior_state is null then
        prior_state := case
            when coalesce(old.status, '^') in ('^', '~', '!') then 'pending'
            when old.status = 'x' then 'declined'
            else 'accepted'
        end;

        insert into patient_portal_appointment_requests (
            appointment_id,
            patient_id,
            legacy_pid,
            appointment_date,
            start_time,
            duration_minutes,
            category_id,
            provider_id,
            facility_id,
            title,
            reason,
            raw_status,
            current_state,
            version,
            requested_at,
            updated_at,
            evidence_source
        )
        values (
            new.id,
            new.patient_id,
            new.pid,
            new.appointment_date,
            new.start_time,
            new.duration_minutes,
            new.category_id,
            new.provider_id,
            new.facility_id,
            coalesce(nullif(trim(new.title), ''), 'Appointment request'),
            nullif(trim(new.comments), ''),
            coalesce(old.status, '^'),
            prior_state,
            1,
            current_timestamp,
            current_timestamp,
            'migration-backfill'
        )
        on conflict (appointment_id) do nothing;

        insert into patient_portal_appointment_request_events (
            appointment_id,
            sequence,
            action,
            state,
            raw_status,
            evidence_source
        )
        values (
            new.id,
            1,
            'discovered',
            prior_state,
            coalesce(old.status, '^'),
            'migration-backfill'
        )
        on conflict (appointment_id, sequence) do nothing;
    end if;

    next_state := case
        when coalesce(new.status, '^') in ('^', '~', '!') then 'pending'
        when new.status = 'x' and prior_state = 'accepted' then 'cancelled'
        when new.status = 'x' then 'declined'
        else 'accepted'
    end;
    next_action := case
        when next_state is distinct from prior_state then next_state
        else 'updated'
    end;

    update patient_portal_appointment_requests
    set patient_id = new.patient_id,
        legacy_pid = new.pid,
        appointment_date = new.appointment_date,
        start_time = new.start_time,
        duration_minutes = new.duration_minutes,
        category_id = new.category_id,
        provider_id = new.provider_id,
        facility_id = new.facility_id,
        title = coalesce(nullif(trim(new.title), ''), 'Appointment request'),
        reason = nullif(trim(new.comments), ''),
        raw_status = coalesce(new.status, '^'),
        current_state = next_state,
        version = version + 1,
        updated_at = current_timestamp
    where appointment_id = new.id
    returning version into next_version;

    insert into patient_portal_appointment_request_events (
        appointment_id,
        sequence,
        action,
        state,
        raw_status,
        evidence_source
    )
    values (
        new.id,
        next_version,
        next_action,
        next_state,
        coalesce(new.status, '^'),
        'runtime'
    );

    return new;
end;
$$;

drop trigger if exists trg_capture_patient_portal_appointment_request on appointments;

create trigger trg_capture_patient_portal_appointment_request
after insert or update on appointments
for each row
execute function capture_patient_portal_appointment_request();

insert into patient_portal_appointment_requests (
    appointment_id,
    patient_id,
    legacy_pid,
    appointment_date,
    start_time,
    duration_minutes,
    category_id,
    provider_id,
    facility_id,
    title,
    reason,
    raw_status,
    current_state,
    version,
    requested_at,
    updated_at,
    evidence_source
)
select
    appointment.id,
    appointment.patient_id,
    appointment.pid,
    appointment.appointment_date,
    appointment.start_time,
    appointment.duration_minutes,
    appointment.category_id,
    appointment.provider_id,
    appointment.facility_id,
    coalesce(nullif(trim(appointment.title), ''), 'Appointment request'),
    nullif(trim(appointment.comments), ''),
    coalesce(appointment.status, '^'),
    case
        when coalesce(appointment.status, '^') in ('^', '~', '!') then 'pending'
        when appointment.status = 'x' then 'declined'
        else 'accepted'
    end,
    1,
    current_timestamp,
    current_timestamp,
    'migration-backfill'
from appointments appointment
where appointment.id like 'APPT-PORTAL-%'
on conflict (appointment_id) do nothing;

insert into patient_portal_appointment_request_events (
    appointment_id,
    sequence,
    action,
    state,
    raw_status,
    evidence_source
)
select
    request.appointment_id,
    1,
    'discovered',
    request.current_state,
    request.raw_status,
    'migration-backfill'
from patient_portal_appointment_requests request
where request.evidence_source = 'migration-backfill'
on conflict (appointment_id, sequence) do nothing;
