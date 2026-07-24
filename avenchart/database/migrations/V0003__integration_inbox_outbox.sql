-- Durable local integration contract. Production adapters are selected and
-- configured separately; no vendor credential or endpoint is stored here.
create table if not exists integration_outbox (
  event_id uuid primary key,
  idempotency_key text unique,
  event_type text not null,
  aggregate_type text not null,
  aggregate_id text not null,
  destination text not null,
  payload jsonb not null,
  status text not null,
  attempt_count integer not null default 0,
  available_at timestamptz not null,
  locked_at timestamptz,
  last_attempt_at timestamptz,
  delivered_at timestamptz,
  external_reference text,
  last_error text,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create index if not exists idx_integration_outbox_dispatch
  on integration_outbox (status, available_at, created_at);

create table if not exists integration_inbox (
  inbox_id uuid primary key,
  source text not null,
  source_message_id text not null,
  message_type text not null,
  payload jsonb not null,
  status text not null,
  attempt_count integer not null default 0,
  received_at timestamptz not null,
  processed_at timestamptz,
  last_error text,
  unique (source, source_message_id)
);

create index if not exists idx_integration_inbox_status
  on integration_inbox (status, received_at);
