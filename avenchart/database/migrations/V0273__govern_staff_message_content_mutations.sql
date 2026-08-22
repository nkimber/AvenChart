-- A staff message has one optimistic-concurrency version across its body,
-- status, corrections, replies, forwards, and assignment changes.
alter table messages
    add column if not exists message_version integer not null default 1;

create table if not exists message_content_events (
    event_id bigserial primary key,
    message_id text not null references messages(id) on delete restrict,
    patient_id text not null references patients(canonical_id) on delete restrict,
    action text not null check (action in ('status-updated', 'content-updated', 'replied')),
    prior_version integer not null,
    message_version integer not null,
    prior_title text,
    title text,
    prior_body text,
    body text,
    prior_status text,
    status text,
    actor text not null,
    occurred_at timestamptz not null default now(),
    check (message_version = prior_version + 1)
);

create index if not exists idx_message_content_events_message_occurred
    on message_content_events (message_id, occurred_at desc, event_id desc);
