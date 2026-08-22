-- INT-12: A source credential alone must not authorize cross-facility clinical
-- mutation.  A laboratory source is explicitly granted only to facilities it
-- may reconcile, and every grant/revocation is retained as immutable evidence.
create table if not exists external_laboratory_source_facility_grants (
  source_id text not null references external_laboratory_sources(source_id) on delete restrict,
  facility_id integer not null references facilities(id) on delete restrict,
  active boolean not null default true,
  granted_at timestamptz not null default now(),
  granted_by text not null check (length(btrim(granted_by)) between 1 and 120),
  revoked_at timestamptz,
  revoked_by text,
  primary key (source_id, facility_id),
  check (
    (active and revoked_at is null and revoked_by is null)
    or (not active and revoked_at is not null and revoked_by is not null)
  )
);

create table if not exists external_laboratory_source_facility_events (
  event_id bigserial primary key,
  source_id text not null,
  facility_id integer not null,
  action text not null check (action in ('granted', 'revoked')),
  actor text not null check (length(btrim(actor)) between 1 and 120),
  occurred_at timestamptz not null default now(),
  foreign key (source_id, facility_id)
    references external_laboratory_source_facility_grants(source_id, facility_id)
    on delete restrict
);

create index if not exists idx_external_laboratory_source_facility_events_source_time
  on external_laboratory_source_facility_events(source_id, occurred_at desc, event_id desc);

create or replace function avenchart_prevent_external_laboratory_source_facility_event_mutation()
returns trigger
language plpgsql
as $$
begin
  raise exception using
    errcode = '55000',
    message = 'External laboratory source facility events are immutable and cannot be altered or deleted.';
end;
$$;

drop trigger if exists trg_external_laboratory_source_facility_events_immutable on external_laboratory_source_facility_events;
create trigger trg_external_laboratory_source_facility_events_immutable
before update or delete on external_laboratory_source_facility_events
for each row execute function avenchart_prevent_external_laboratory_source_facility_event_mutation();
