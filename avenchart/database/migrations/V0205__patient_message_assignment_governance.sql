alter table messages
  add column if not exists assignment_version integer not null default 0;

create table if not exists message_assignment_events (
  event_id bigint generated always as identity primary key,
  message_id text not null references messages(id),
  patient_id text not null references patients(canonical_id),
  action text not null check (action in ('assigned', 'reassigned', 'unassigned')),
  previous_assigned_to text,
  assigned_to text,
  reason text,
  actor text not null,
  assignment_version integer not null check (assignment_version > 0),
  occurred_at timestamptz not null
);

create unique index if not exists ux_message_assignment_events_message_version
  on message_assignment_events(message_id, assignment_version);

create index if not exists ix_message_assignment_events_message_time
  on message_assignment_events(message_id, occurred_at desc, event_id desc);
