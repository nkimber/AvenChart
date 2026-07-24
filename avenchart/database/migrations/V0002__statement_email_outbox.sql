-- Promotes the deterministic statement-email outbox from runtime DDL to a
-- versioned target schema contract. No email is sent by this table itself.
create table if not exists statement_email_outbox (
  outbox_message_id text primary key,
  dataset_id text not null,
  dataset_version text not null,
  as_of_date date not null,
  outbox_batch_id text not null,
  queued_at timestamp not null,
  pubpid text not null,
  legacy_pid integer not null,
  patient_display_name text not null,
  statement_number text not null,
  statement_status text not null,
  statement_date date not null,
  due_date date not null,
  balance_due_amount numeric(12,2) not null default 0,
  past_due_amount numeric(12,2) not null default 0,
  current_due_amount numeric(12,2) not null default 0,
  to_email text not null,
  from_email text not null,
  subject text not null,
  body_preview text not null,
  attachment_file_name text not null,
  queue_name text not null,
  delivery_status text not null,
  external_reference text not null,
  created_at timestamp not null
);

create index if not exists idx_statement_email_outbox_batch
  on statement_email_outbox (outbox_batch_id, queued_at desc);

create index if not exists idx_statement_email_outbox_pid_created
  on statement_email_outbox (legacy_pid, created_at desc);
