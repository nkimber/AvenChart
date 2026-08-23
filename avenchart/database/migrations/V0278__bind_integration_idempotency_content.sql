-- Exact replay is safe only when the retained integration identity represents
-- the same semantic message. Preserve hashes, not payloads or partner keys,
-- when a caller reuses an identity with different content.
create table if not exists integration_idempotency_conflicts (
  conflict_id uuid primary key,
  direction text not null check (direction in ('inbox', 'outbox')),
  outbox_event_id uuid references integration_outbox(event_id) on delete set null,
  inbox_id uuid references integration_inbox(inbox_id) on delete set null,
  existing_content_digest char(64) not null,
  incoming_content_digest char(64) not null,
  occurred_at timestamptz not null default now(),
  check (
    (direction = 'outbox' and outbox_event_id is not null and inbox_id is null)
    or (direction = 'inbox' and inbox_id is not null and outbox_event_id is null)
  )
);

create index if not exists idx_integration_idempotency_conflicts_occurred
  on integration_idempotency_conflicts (occurred_at desc, conflict_id desc);
