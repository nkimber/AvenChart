-- Bounded local failure handling for the generic integration outbox. This is
-- not a partner adapter, credential store, or production delivery policy.
alter table integration_outbox
  add column if not exists quarantined_at timestamptz,
  add column if not exists quarantined_by text,
  add column if not exists recovery_count integer not null default 0;

create table if not exists integration_outbox_events (
  event_log_id uuid primary key,
  event_id uuid not null references integration_outbox(event_id),
  action text not null check (action in ('quarantined', 'requeued')),
  reason text not null,
  actor text not null,
  attempt_count integer not null,
  occurred_at timestamptz not null
);

create index if not exists idx_integration_outbox_events_event_time
  on integration_outbox_events (event_id, occurred_at desc);
