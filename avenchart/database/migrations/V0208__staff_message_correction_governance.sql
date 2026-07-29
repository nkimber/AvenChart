create table if not exists message_correction_events (
  event_id bigint generated always as identity primary key,
  message_id text not null references messages(id),
  patient_id text not null references patients(canonical_id),
  correction text not null check (length(trim(correction)) between 1 and 2000),
  reason text not null check (length(trim(reason)) between 1 and 500),
  actor text not null,
  occurred_at timestamptz not null default now()
);

create index if not exists ix_message_correction_events_message_time
  on message_correction_events(message_id, occurred_at desc, event_id desc);
