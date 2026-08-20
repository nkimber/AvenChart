-- Makes the versioned migration catalog authoritative for schema that was
-- previously created lazily by repositories during normal API requests.

alter table encounters add column if not exists archived_at timestamp null;
alter table encounters add column if not exists archive_version integer not null default 1;

alter table patients add column if not exists merged_into_patient_id text references patients(canonical_id);
alter table patients add column if not exists merged_at timestamptz;
alter table patients add column if not exists merged_by text;

alter table prescriptions
  add column if not exists dose_amount numeric(10,2),
  add column if not exists dose_unit text,
  add column if not exists frequency text,
  add column if not exists duration_days integer;

create table if not exists appointment_reminder_dispatch_audit (
  audit_id text primary key,
  dataset_id text not null,
  dataset_version text not null,
  as_of_date date not null,
  appointment_id text not null,
  dispatch_id text not null,
  dispatched_at timestamp not null,
  patient_id text not null,
  legacy_pid integer not null,
  pubpid text not null,
  patient_display_name text not null,
  appointment_date date not null,
  start_time time not null,
  end_time time not null,
  title text not null,
  reminder_status text not null,
  reminder_channel text not null,
  reminder_contact text,
  reminder_lead_days integer,
  queue_name text not null,
  dispatch_status text not null,
  external_reference text not null,
  template_name text not null,
  message_preview text not null,
  retry_of_dispatch_id text,
  retry_attempt integer not null default 0,
  created_at timestamp not null
);
alter table appointment_reminder_dispatch_audit add column if not exists retry_of_dispatch_id text;
alter table appointment_reminder_dispatch_audit add column if not exists retry_attempt integer not null default 0;
create index if not exists idx_appointment_reminder_dispatch_appointment
  on appointment_reminder_dispatch_audit (appointment_id, created_at desc);
create index if not exists idx_appointment_reminder_dispatch_dispatch
  on appointment_reminder_dispatch_audit (dispatch_id, dispatched_at desc);
create index if not exists idx_appointment_reminder_dispatch_retry
  on appointment_reminder_dispatch_audit (retry_of_dispatch_id)
  where retry_of_dispatch_id is not null;

create table if not exists statement_delivery_audit_events (
  dispatch_audit_id text primary key,
  dataset_id text not null,
  dataset_version text not null,
  as_of_date date not null,
  delivery_id text not null,
  dispatch_id text not null,
  dispatched_at timestamp not null,
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
  delivery_method text not null,
  destination text not null,
  file_name text not null,
  queue_name text not null,
  dispatch_status text not null,
  external_reference text not null,
  created_at timestamp not null
);
create index if not exists idx_statement_delivery_audit_dispatch
  on statement_delivery_audit_events (dispatch_id, dispatched_at desc);
create index if not exists idx_statement_delivery_audit_pid_created
  on statement_delivery_audit_events (legacy_pid, created_at desc);

create table if not exists medication_vocabulary (
  rx_norm_code text primary key,
  drug_name text not null,
  display_name text not null,
  form text not null,
  strength text not null,
  route text not null,
  dose_amount numeric(10,2),
  dose_unit text,
  frequency text,
  duration_days integer,
  controlled_substance_schedule text,
  active boolean not null default true
);
insert into medication_vocabulary
  (rx_norm_code, drug_name, display_name, form, strength, route, dose_amount,
   dose_unit, frequency, duration_days, controlled_substance_schedule)
values
  ('860975', 'Metformin', 'Metformin 500 mg tablet', 'tablet', '500 mg', 'oral', 500, 'mg', 'twice daily', 30, null),
  ('1049502', 'Omeprazole', 'Omeprazole 20 mg delayed release capsule', 'capsule', '20 mg', 'oral', 20, 'mg', 'once daily', 30, null),
  ('312615', 'Lisinopril', 'Lisinopril 10 mg tablet', 'tablet', '10 mg', 'oral', 10, 'mg', 'once daily', 30, null),
  ('617314', 'Atorvastatin', 'Atorvastatin 20 mg tablet', 'tablet', '20 mg', 'oral', 20, 'mg', 'nightly', 30, null),
  ('1049621', 'Oxycodone', 'Oxycodone 5 mg tablet', 'tablet', '5 mg', 'oral', 5, 'mg', 'every 6 hours as needed', 7, 'CII')
on conflict (rx_norm_code) do update
set drug_name = excluded.drug_name,
    display_name = excluded.display_name,
    form = excluded.form,
    strength = excluded.strength,
    route = excluded.route,
    dose_amount = excluded.dose_amount,
    dose_unit = excluded.dose_unit,
    frequency = excluded.frequency,
    duration_days = excluded.duration_days,
    controlled_substance_schedule = excluded.controlled_substance_schedule;

create table if not exists prescription_audit_events (
  event_id text primary key,
  prescription_id text not null,
  patient_id text not null,
  pid integer not null,
  action text not null,
  occurred_at timestamp not null,
  actor text not null,
  detail text,
  before_refills integer,
  after_refills integer,
  pharmacy_id integer,
  pharmacy_name text,
  failure_reason text
);
create index if not exists idx_prescription_audit_events_prescription
  on prescription_audit_events (prescription_id, occurred_at, event_id);
create index if not exists idx_prescription_audit_events_pid
  on prescription_audit_events (pid, occurred_at desc, event_id desc);

create table if not exists prescription_refill_request_lifecycle (
  thread_id integer primary key,
  staff_message_id integer not null,
  pid integer not null,
  patient_id text not null,
  prescription_id text not null,
  request_date date,
  drug text,
  patient_note text,
  status text not null check (
    status in ('pending', 'clarification-requested', 'approved', 'denied', 'completed')
  ),
  staff_response text,
  updated_at timestamp not null,
  updated_by text not null
);
alter table prescription_refill_request_lifecycle add column if not exists request_date date;
alter table prescription_refill_request_lifecycle add column if not exists drug text;
alter table prescription_refill_request_lifecycle add column if not exists patient_note text;
create index if not exists idx_prescription_refill_lifecycle_status
  on prescription_refill_request_lifecycle (status, updated_at desc, thread_id desc);
create index if not exists idx_prescription_refill_lifecycle_patient
  on prescription_refill_request_lifecycle (pid, updated_at desc, thread_id desc);
insert into prescription_refill_request_lifecycle
  (thread_id, staff_message_id, pid, patient_id, prescription_id,
   request_date, drug, patient_note, status, staff_response, updated_at, updated_by)
select
  message.reply_mail_chain,
  message.id,
  message.pid,
  prescription.patient_id,
  prescription.id::text,
  message.message_date,
  prescription.drug,
  nullif(substring(message.body from 'Patient note: ([^\r\n]+)'), ''),
  case when message.message_status = 'Done' then 'approved' else 'pending' end,
  null,
  message.message_date::timestamp,
  message.assigned_to
from portal_mailbox_messages message
join prescriptions prescription
  on prescription.pid = message.pid
 and prescription.id::text = nullif(
   substring(message.body from 'Prescription ID: ([^\r\n]+)'),
   ''
 )
where message.deleted = 0
  and message.owner = message.assigned_to
  and message.portal_relation = 'portal:prescription-refill-request'
on conflict (thread_id) do nothing;

create table if not exists patient_provider_assignment_events (
  event_id uuid primary key,
  patient_id text not null,
  legacy_pid integer not null,
  from_provider_id integer,
  from_provider_name text,
  from_facility_id integer,
  from_facility_name text,
  to_provider_id integer,
  to_provider_name text,
  to_facility_id integer,
  to_facility_name text,
  reason varchar(250) not null,
  actor text not null,
  occurred_at timestamptz not null default now()
);
create index if not exists ix_patient_provider_assignment_events_patient_time
  on patient_provider_assignment_events (patient_id, occurred_at desc, event_id desc);

create table if not exists patient_administration_audit_events (
  event_id uuid primary key,
  patient_id text not null,
  legacy_pid integer not null,
  area varchar(24) not null,
  action varchar(24) not null,
  entity_id text,
  changed_fields text[] not null,
  before_values jsonb not null,
  after_values jsonb not null,
  actor text not null,
  occurred_at timestamptz not null default now()
);
create index if not exists ix_patient_administration_audit_events_patient_time
  on patient_administration_audit_events (patient_id, occurred_at desc, event_id desc);

create table if not exists patient_portal_message_attachments (
  id uuid primary key,
  message_id integer not null references portal_mailbox_messages(id) on delete cascade,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  file_name text not null,
  content_type text not null,
  size_bytes integer not null,
  content bytea not null,
  source text not null default 'portal-upload',
  uploaded_at timestamptz not null default now()
);
create index if not exists idx_patient_portal_message_attachments_message
  on patient_portal_message_attachments (message_id, uploaded_at, id);

create table if not exists therapy_groups (
  id uuid primary key,
  name text not null,
  status text not null,
  facilitator_id integer references staff(id),
  description text,
  capacity integer not null,
  created_at timestamptz not null
);
create table if not exists therapy_group_members (
  group_id uuid not null references therapy_groups(id),
  patient_id text not null references patients(canonical_id),
  joined_at timestamptz not null,
  primary key (group_id, patient_id)
);
create table if not exists therapy_group_sessions (
  id uuid primary key,
  group_id uuid not null references therapy_groups(id),
  starts_at timestamptz not null,
  duration_minutes integer not null,
  topic text,
  status text not null,
  created_at timestamptz not null
);
create table if not exists therapy_group_session_participants (
  session_id uuid not null references therapy_group_sessions(id),
  patient_id text not null references patients(canonical_id),
  primary key (session_id, patient_id)
);
create table if not exists therapy_group_session_encounters (
  session_id uuid not null references therapy_group_sessions(id),
  patient_id text not null references patients(canonical_id),
  encounter_id integer not null,
  created_at timestamptz not null,
  primary key (session_id, patient_id)
);
create table if not exists therapy_group_session_attendance (
  session_id uuid not null references therapy_group_sessions(id),
  patient_id text not null references patients(canonical_id),
  attendance_status text not null default 'unrecorded',
  note text,
  recorded_at timestamptz,
  primary key (session_id, patient_id),
  check (attendance_status in ('unrecorded', 'present', 'absent', 'excused'))
);

create table if not exists patient_merge_audit_plans (
  audit_id uuid primary key,
  target_patient_id text not null,
  source_patient_id text not null,
  target_legacy_pid integer not null,
  source_legacy_pid integer not null,
  match_score integer not null,
  match_reasons text[] not null,
  rationale text null,
  planned_by text not null,
  planned_at timestamptz not null,
  status text not null
);
create table if not exists patient_merge_executions (
  execution_id uuid primary key,
  audit_id uuid not null references patient_merge_audit_plans(audit_id),
  target_patient_id text not null references patients(canonical_id),
  source_patient_id text not null references patients(canonical_id),
  executed_by text not null,
  executed_at timestamptz not null,
  rolled_back_by text,
  rolled_back_at timestamptz,
  status text not null
);
create table if not exists patient_merge_execution_manifest_rows (
  execution_id uuid not null references patient_merge_executions(execution_id),
  table_name text not null,
  record_id text not null,
  primary key (execution_id, table_name, record_id)
);

create table if not exists procedure_result_versions (
  id bigserial primary key,
  result_id integer not null references lab_results(id) on delete cascade,
  version_no integer not null,
  captured_at timestamp not null,
  code text,
  text text,
  units text,
  result text,
  range text,
  abnormal text,
  result_date timestamp,
  result_status text,
  unique (result_id, version_no)
);
create index if not exists idx_procedure_result_versions_result
  on procedure_result_versions (result_id, version_no desc);

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

-- Atomic integer allocation for legacy tables that do not have identity/default
-- columns and for version numbers scoped to an aggregate.
create table if not exists avenchart_integer_counters (
  counter_key text primary key,
  current_value bigint not null,
  updated_at timestamptz not null default now()
);

create or replace function avenchart_next_integer(p_counter_key text, p_floor bigint)
returns integer
language sql
volatile
as $$
  insert into avenchart_integer_counters(counter_key, current_value, updated_at)
  values (p_counter_key, p_floor + 1, now())
  on conflict (counter_key) do update
  set current_value = greatest(
        avenchart_integer_counters.current_value + 1,
        excluded.current_value),
      updated_at = now()
  returning current_value::integer;
$$;
