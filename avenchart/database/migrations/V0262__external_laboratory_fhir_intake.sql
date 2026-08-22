-- INT-12: Provenance and replay protection for the selected FHIR R4 synthetic
-- laboratory boundary.  The inbound clinical record must remain traceable even
-- when a source retries, corrects, or sends conflicting content for a message.
create table if not exists external_laboratory_ingestions (
  ingestion_id uuid primary key,
  source_id text not null references external_laboratory_sources(source_id) on delete restrict,
  source_message_id text not null check (
    length(source_message_id) between 3 and 160
    and source_message_id ~ '^[A-Za-z0-9][A-Za-z0-9._:-]*$'
  ),
  fhir_version text not null check (fhir_version = '4.0.1'),
  payload jsonb not null,
  payload_hash bytea not null check (octet_length(payload_hash) = 32),
  status text not null check (status in ('applied', 'rejected')),
  rejection_reason text,
  patient_id text references patients(canonical_id) on delete restrict,
  order_id integer references lab_orders(id) on delete restrict,
  specimen_id integer references lab_specimens(id) on delete restrict,
  report_id integer references lab_reports(id) on delete restrict,
  created_result_count integer not null default 0 check (created_result_count >= 0),
  updated_result_count integer not null default 0 check (updated_result_count >= 0),
  received_at timestamptz not null default now(),
  processed_at timestamptz not null default now(),
  unique (source_id, source_message_id),
  check (
    (status = 'applied' and rejection_reason is null and patient_id is not null and order_id is not null and specimen_id is not null and report_id is not null)
    or (status = 'rejected' and rejection_reason is not null)
  )
);

create index if not exists idx_external_laboratory_ingestions_source_processed
  on external_laboratory_ingestions(source_id, processed_at desc, ingestion_id desc);

create table if not exists external_laboratory_ingestion_events (
  event_id bigserial primary key,
  ingestion_id uuid not null references external_laboratory_ingestions(ingestion_id) on delete restrict,
  action text not null check (action in ('received', 'applied', 'rejected', 'duplicate', 'replay-conflict', 'correction')),
  detail text,
  occurred_at timestamptz not null default now()
);

create index if not exists idx_external_laboratory_ingestion_events_ingestion
  on external_laboratory_ingestion_events(ingestion_id, occurred_at desc, event_id desc);

create table if not exists external_laboratory_report_links (
  source_id text not null references external_laboratory_sources(source_id) on delete restrict,
  external_report_id text not null check (length(external_report_id) between 1 and 120),
  report_id integer not null unique references lab_reports(id) on delete restrict,
  linked_at timestamptz not null default now(),
  primary key (source_id, external_report_id)
);

create table if not exists external_laboratory_result_links (
  source_id text not null references external_laboratory_sources(source_id) on delete restrict,
  external_result_id text not null check (length(external_result_id) between 1 and 120),
  result_id integer not null unique references lab_results(id) on delete restrict,
  linked_at timestamptz not null default now(),
  primary key (source_id, external_result_id)
);

create or replace function avenchart_prevent_external_laboratory_intake_event_mutation()
returns trigger
language plpgsql
as $$
begin
  raise exception using
    errcode = '55000',
    message = 'External laboratory ingestion events are immutable and cannot be altered or deleted.';
end;
$$;

drop trigger if exists trg_external_laboratory_ingestion_events_immutable on external_laboratory_ingestion_events;
create trigger trg_external_laboratory_ingestion_events_immutable
before update or delete on external_laboratory_ingestion_events
for each row execute function avenchart_prevent_external_laboratory_intake_event_mutation();
