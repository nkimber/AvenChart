create table if not exists message_escalation_events (
    event_id bigserial primary key,
    message_id text not null references messages(id) on delete cascade,
    patient_id text not null,
    action text not null check (action in ('escalated', 'resolved')),
    reason text not null check (char_length(reason) between 1 and 500),
    actor text not null,
    occurred_at timestamptz not null default now()
);

create index if not exists ix_message_escalation_events_message_occurred
    on message_escalation_events (message_id, occurred_at desc, event_id desc);
