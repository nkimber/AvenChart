alter table recalls
  add column if not exists closed_at timestamptz,
  add column if not exists closed_by text,
  add column if not exists closure_reason text;

create table if not exists recall_lifecycle_events (
  event_id uuid primary key,
  recall_id uuid not null references recalls(id) on delete restrict,
  previous_status text,
  status text not null check (status in ('active', 'completed', 'cancelled')),
  event_type text not null check (event_type in ('created', 'completed', 'cancelled')),
  actor text not null,
  reason text,
  occurred_at timestamptz not null default now()
);

create index if not exists idx_recall_lifecycle_events_recall_occurred
  on recall_lifecycle_events(recall_id, occurred_at desc);
