-- REC-01/02: deployment-owned document schema plus a managed record-intake
-- boundary. Managed bytes do not enter patient_documents until release.

create table if not exists patient_document_versions (
  id bigserial primary key,
  document_id integer not null references patient_documents(id) on delete cascade,
  version_no integer not null,
  captured_at timestamp not null,
  file_name text,
  mimetype text,
  size_bytes integer,
  pages integer,
  storage_method text,
  url text,
  hash text,
  content text,
  content_bytes bytea,
  unique (document_id, version_no)
);

create index if not exists idx_patient_document_versions_document
  on patient_document_versions (document_id, version_no desc);

create table if not exists patient_document_content_events (
  event_id uuid primary key,
  document_id integer not null references patient_documents(id) on delete cascade,
  document_key text not null,
  patient_id text not null,
  legacy_pid integer not null,
  from_version integer not null,
  to_version integer not null,
  from_file_name text,
  to_file_name text,
  from_mimetype text,
  to_mimetype text,
  from_size_bytes integer,
  to_size_bytes integer,
  from_hash text,
  to_hash text,
  reason varchar(250) not null,
  actor text not null,
  occurred_at timestamptz not null default now(),
  unique (document_id, to_version)
);

create index if not exists ix_patient_document_content_events_document_time
  on patient_document_content_events (document_id, occurred_at desc, event_id desc);

create index if not exists ix_patient_document_content_events_patient_time
  on patient_document_content_events (patient_id, occurred_at desc, event_id desc);

create table if not exists patient_document_review_events (
  event_id uuid primary key,
  document_id integer not null references patient_documents(id) on delete cascade,
  document_key text not null,
  patient_id text not null,
  legacy_pid integer not null,
  from_status varchar(20) not null,
  to_status varchar(20) not null,
  reason varchar(250) not null,
  actor text not null,
  occurred_at timestamptz not null default now(),
  document_version integer not null,
  content_hash text
);

create index if not exists ix_patient_document_review_events_document_time
  on patient_document_review_events (document_id, occurred_at desc, event_id desc);

create index if not exists ix_patient_document_review_events_patient_time
  on patient_document_review_events (patient_id, occurred_at desc, event_id desc);

create table if not exists patient_document_archive_events (
  event_id uuid primary key,
  document_id integer not null references patient_documents(id) on delete cascade,
  document_key text not null,
  patient_id text not null,
  legacy_pid integer not null,
  from_archived boolean not null,
  to_archived boolean not null,
  reason varchar(250) not null,
  actor text not null,
  occurred_at timestamptz not null default now(),
  document_version integer not null,
  review_status varchar(20) not null,
  content_hash text
);

create index if not exists ix_patient_document_archive_events_document_time
  on patient_document_archive_events (document_id, occurred_at desc, event_id desc);

create index if not exists ix_patient_document_archive_events_patient_time
  on patient_document_archive_events (patient_id, occurred_at desc, event_id desc);

create table if not exists patient_document_metadata_events (
  event_id uuid primary key,
  document_id integer not null references patient_documents(id) on delete cascade,
  document_key text not null,
  patient_id text not null,
  legacy_pid integer not null,
  changed_fields text[] not null,
  from_category_id integer not null,
  from_category_name text not null,
  to_category_id integer not null,
  to_category_name text not null,
  from_name text not null,
  to_name text not null,
  from_doc_date date not null,
  to_doc_date date not null,
  from_encounter integer,
  to_encounter integer,
  from_notes text,
  to_notes text,
  reason varchar(250) not null,
  actor text not null,
  occurred_at timestamptz not null default now()
);

create index if not exists ix_patient_document_metadata_events_document_time
  on patient_document_metadata_events (document_id, occurred_at desc, event_id desc);

create index if not exists ix_patient_document_metadata_events_patient_time
  on patient_document_metadata_events (patient_id, occurred_at desc, event_id desc);

create table if not exists patient_document_ocr_tasks (
  document_id integer primary key references patient_documents(id) on delete cascade,
  task_version integer not null,
  status varchar(20) not null,
  priority varchar(20) not null,
  extracted_text text,
  failure_reason varchar(500),
  started_by text,
  started_at timestamptz,
  completed_by text,
  completed_at timestamptz,
  failed_by text,
  failed_at timestamptz,
  updated_by text not null,
  updated_at timestamptz not null default now()
);

create index if not exists ix_patient_document_ocr_tasks_status_updated
  on patient_document_ocr_tasks (status, updated_at, document_id);

create index if not exists ix_patient_document_ocr_tasks_priority_status
  on patient_document_ocr_tasks (priority, status, updated_at);

create table if not exists patient_document_ocr_events (
  event_id uuid primary key,
  document_id integer not null references patient_documents(id) on delete cascade,
  document_key text not null,
  patient_id text not null,
  legacy_pid integer not null,
  action varchar(20) not null,
  from_status varchar(20) not null,
  to_status varchar(20) not null,
  reason varchar(500) not null,
  actor text not null,
  occurred_at timestamptz not null default now(),
  task_version integer not null,
  document_version integer not null,
  review_status varchar(20) not null,
  from_extracted_text_length integer not null,
  to_extracted_text_length integer not null,
  from_extracted_text_preview varchar(500),
  to_extracted_text_preview varchar(500),
  from_extracted_text_hash text,
  to_extracted_text_hash text,
  failure_reason varchar(500)
);

create index if not exists ix_patient_document_ocr_events_document_time
  on patient_document_ocr_events (document_id, occurred_at desc, event_id desc);

create index if not exists ix_patient_document_ocr_events_patient_time
  on patient_document_ocr_events (patient_id, occurred_at desc, event_id desc);

create table if not exists patient_document_routing_tasks (
  document_id integer primary key references patient_documents(id) on delete cascade,
  task_version integer not null,
  status varchar(20) not null,
  destination varchar(100) not null,
  priority varchar(20) not null,
  assigned_to text,
  routing_reason varchar(250) not null,
  routed_by text not null,
  routed_at timestamptz not null default now(),
  due_at timestamptz not null,
  completed_by text,
  completed_at timestamptz,
  completion_note varchar(250)
);

create index if not exists ix_patient_document_routing_tasks_status_due
  on patient_document_routing_tasks (status, due_at, document_id);

create index if not exists ix_patient_document_routing_tasks_assignee_status
  on patient_document_routing_tasks (assigned_to, status, due_at);

create table if not exists patient_document_routing_events (
  event_id uuid primary key,
  document_id integer not null references patient_documents(id) on delete cascade,
  document_key text not null,
  patient_id text not null,
  legacy_pid integer not null,
  action varchar(20) not null,
  from_status varchar(20) not null,
  to_status varchar(20) not null,
  from_destination varchar(100),
  to_destination varchar(100) not null,
  from_priority varchar(20),
  to_priority varchar(20) not null,
  from_assigned_to text,
  to_assigned_to text,
  reason varchar(250) not null,
  actor text not null,
  occurred_at timestamptz not null default now(),
  due_at timestamptz not null,
  task_version integer not null,
  document_version integer not null,
  review_status varchar(20) not null,
  content_hash text
);

create index if not exists ix_patient_document_routing_events_document_time
  on patient_document_routing_events (document_id, occurred_at desc, event_id desc);

create index if not exists ix_patient_document_routing_events_patient_time
  on patient_document_routing_events (patient_id, occurred_at desc, event_id desc);

create table if not exists managed_record_intakes (
  intake_id uuid primary key,
  patient_id text not null references patients(canonical_id),
  legacy_pid integer not null,
  document_id integer unique references patient_documents(id) on delete set null,
  idempotency_key varchar(120) not null,
  request_fingerprint char(64) not null check (request_fingerprint ~ '^[0-9a-f]{64}$'),
  category_id integer not null,
  category_name text not null,
  title varchar(255) not null,
  service_date date not null,
  encounter integer,
  record_class varchar(40) not null
    check (record_class in ('clinical-record','correspondence','identity','financial','administrative')),
  source_type varchar(40) not null
    check (source_type in ('file-upload','scanner-capture','external-import','generated-output')),
  author_name varchar(200) not null,
  facility_id integer references facilities(id),
  sensitivity varchar(30) not null
    check (sensitivity in ('standard','restricted','highly-sensitive')),
  language_tag varchar(35) not null,
  file_name varchar(255) not null,
  media_type varchar(150) not null,
  size_bytes integer not null check (size_bytes > 0 and size_bytes <= 26214400),
  content_version integer not null default 1 check (content_version = 1),
  content_sha256 char(64) not null check (content_sha256 ~ '^[0-9a-f]{64}$'),
  storage_adapter varchar(80) not null default 'local-database-record-intake',
  storage_reference text not null,
  content_bytes bytea not null,
  state varchar(20) not null
    check (state in ('captured','quarantined','scanning','failed','available','rejected')),
  workflow_version integer not null default 0 check (workflow_version >= 0),
  availability_status varchar(20) not null
    check (availability_status in ('withheld','available','unavailable')),
  validation_status varchar(30) not null
    check (validation_status in ('pending','queued','running','failed','locally-validated')),
  validation_adapter varchar(80) not null default 'local-structural-validator',
  anti_malware_verified boolean not null default false,
  failure_reason varchar(500),
  created_by text not null,
  created_at timestamptz not null default now(),
  updated_by text not null,
  updated_at timestamptz not null default now(),
  last_reason varchar(500) not null,
  unique (created_by, idempotency_key),
  check (
    (state = 'available' and document_id is not null and availability_status = 'available'
      and validation_status = 'locally-validated')
    or
    (state <> 'available' and document_id is null and availability_status <> 'available')
  )
);

create index if not exists ix_managed_record_intakes_patient_state
  on managed_record_intakes(patient_id, state, updated_at desc);

create index if not exists ix_managed_record_intakes_state_updated
  on managed_record_intakes(state, updated_at desc);

create table if not exists managed_record_intake_events (
  event_id uuid primary key,
  intake_id uuid not null references managed_record_intakes(intake_id) on delete cascade,
  action varchar(30) not null,
  from_state varchar(20),
  to_state varchar(20) not null,
  from_record_class varchar(40),
  to_record_class varchar(40) not null,
  from_sensitivity varchar(30),
  to_sensitivity varchar(30) not null,
  reason varchar(500) not null,
  actor text not null,
  occurred_at timestamptz not null default now(),
  workflow_version integer not null,
  validation_status varchar(30) not null,
  content_version integer not null,
  content_sha256 char(64) not null,
  document_id integer,
  unique (intake_id, workflow_version)
);

create index if not exists ix_managed_record_intake_events_intake_time
  on managed_record_intake_events(intake_id, occurred_at desc, event_id desc);
