-- A successful enqueue or dispatch is an accountable workflow mutation.  Keep
-- a compact immutable record of the actor, state, attempt, and outcome without
-- duplicating the potentially sensitive integration payload.
create table if not exists integration_outbox_provenance_events (
  event_log_id uuid primary key,
  event_id uuid not null references integration_outbox(event_id) on delete restrict,
  action text not null check (action in (
    'queued', 'dispatch-claimed', 'delivered', 'retry-scheduled',
    'quarantined', 'lease-recovered', 'requeued'
  )),
  actor text not null check (length(btrim(actor)) between 1 and 120),
  status text not null check (status in (
    'queued', 'dispatching', 'retry-scheduled', 'delivered', 'quarantined'
  )),
  attempt_count integer not null check (attempt_count >= 0),
  detail text not null check (length(btrim(detail)) between 1 and 500),
  occurred_at timestamptz not null
);

create index if not exists idx_integration_outbox_provenance_events_event_time
  on integration_outbox_provenance_events (event_id, occurred_at, event_log_id);

create or replace function avenchart_prevent_integration_outbox_provenance_mutation()
returns trigger
language plpgsql
as $$
begin
  raise exception using
    errcode = '55000',
    message = 'Integration outbox provenance events are immutable and cannot be altered or deleted.';
end;
$$;

drop trigger if exists trg_integration_outbox_provenance_events_immutable
  on integration_outbox_provenance_events;
create trigger trg_integration_outbox_provenance_events_immutable
before update or delete on integration_outbox_provenance_events
for each row execute function avenchart_prevent_integration_outbox_provenance_mutation();
