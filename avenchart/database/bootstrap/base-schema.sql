-- Generated from avenchart-shared-synthetic-v1 v1
set client_min_messages to warning;
begin;


create table dataset_metadata (
  dataset_id text primary key,
  version text not null,
  generated_at timestamptz not null,
  base_date date not null,
  patient_count integer not null,
  appointment_count integer not null,
  encounter_count integer not null
);

create table facilities (
  id integer primary key,
  code text not null,
  name text not null,
  phone text,
  street text,
  city text,
  state text,
  postal_code text,
  color text,
  inactive boolean not null default false
);

create table staff (
  id integer primary key,
  username text not null unique,
  first_name text not null,
  last_name text not null,
  role text not null,
  calendar boolean not null,
  facility_id integer references facilities(id),
  email text,
  npi text,
  active boolean not null default true
);

create table practice_settings (
  setting_key text primary key,
  setting_value text not null,
  value_type text not null,
  updated_at timestamptz not null,
  updated_by text not null
);
create table coding_catalogs (
  catalog_key text primary key, display_name text not null, sequence integer not null,
  active boolean not null, claim_enabled boolean not null, fee_enabled boolean not null,
  modifier_length integer not null, updated_at timestamptz not null, updated_by text not null
);
create table coding_catalog_audit_events (
  event_id uuid primary key, catalog_key text not null references coding_catalogs(catalog_key),
  action text not null, occurred_at timestamptz not null, username text not null
);
create table form_layouts (
  layout_key text primary key, title text not null, mapping text not null, sequence integer not null,
  active boolean not null, updated_at timestamptz not null, updated_by text not null
);
create table form_option_lists (
  list_key text primary key, title text not null, active boolean not null,
  updated_at timestamptz not null, updated_by text not null
);
create table form_option_values (
  list_key text not null references form_option_lists(list_key), option_key text not null, title text not null,
  sequence integer not null, is_default boolean not null, active boolean not null, option_value text not null,
  updated_at timestamptz not null, updated_by text not null, primary key (list_key, option_key)
);
create table clinical_alert_rules (
  rule_key text primary key, title text not null, trigger_type text not null, target_type text not null, severity text not null,
  message text not null, sequence integer not null, active boolean not null, updated_at timestamptz not null, updated_by text not null
);
create table module_catalog (
  module_key text primary key, display_name text not null, category text not null, status text not null,
  description text not null, updated_at timestamptz not null, updated_by text not null
);
create table api_client_registry (
  client_key text primary key, display_name text not null, redirect_uri text not null, scopes text not null,
  active boolean not null, updated_at timestamptz not null, updated_by text not null
);
create table form_layout_groups (
  layout_key text not null references form_layouts(layout_key), group_key text not null, title text not null,
  sequence integer not null, active boolean not null, updated_at timestamptz not null, updated_by text not null,
  primary key (layout_key, group_key)
);
create table form_layout_fields (
  layout_key text not null, field_key text not null, group_key text not null, label text not null, field_type text not null,
  sequence integer not null, required boolean not null, active boolean not null, max_length integer not null,
  list_id text, default_value text, updated_at timestamptz not null, updated_by text not null,
  primary key (layout_key, field_key), foreign key (layout_key, group_key) references form_layout_groups(layout_key, group_key)
);
create table practice_setting_audit_events (
  event_id uuid primary key,
  setting_key text not null,
  prior_value text not null,
  new_value text not null,
  occurred_at timestamptz not null,
  username text not null
);

create table auth_accounts (
  username text primary key,
  display_name text not null,
  role text not null,
  staff_id integer references staff(id),
  active boolean not null default true,
  password_salt text not null,
  password_hash text not null
);

create table auth_audit_events (
  id bigserial primary key,
  occurred_at timestamptz not null default now(),
  event text not null,
  username text not null,
  success boolean not null,
  source_ip text,
  comment text not null,
  failure_reason text,
  log_source text not null default 'avenchart'
);

create table auth_sessions (
  id uuid primary key,
  username text not null references auth_accounts(username),
  display_name text not null,
  role text not null,
  staff_id integer references staff(id),
  created_at timestamptz not null default now(),
  last_seen_at timestamptz not null default now(),
  expires_at timestamptz not null,
  ended_at timestamptz,
  source_ip text,
  user_agent text,
  session_source text not null default 'avenchart'
);

create table access_groups (
  id integer primary key,
  value text not null unique,
  name text not null,
  parent_id integer references access_groups(id)
);

create table access_permissions (
  section_value text not null,
  value text not null,
  name text not null,
  primary key (section_value, value)
);

create table access_group_permissions (
  group_value text not null references access_groups(value),
  section_value text not null,
  permission_value text not null,
  permission_name text not null,
  return_value text not null,
  primary key (group_value, section_value, permission_value, return_value),
  foreign key (section_value, permission_value) references access_permissions(section_value, value)
);

create table access_user_memberships (
  user_value text not null,
  user_name text not null,
  group_value text not null references access_groups(value),
  group_name text not null,
  staff_id integer references staff(id),
  primary key (user_value, group_value)
);

create table patients (
  canonical_id text primary key,
  legacy_pid integer not null unique,
  pubpid text not null unique,
  first_name text not null,
  last_name text not null,
  preferred_name text,
  sex text,
  date_of_birth date not null,
  cohort text,
  purpose text,
  street text,
  city text,
  state text,
  postal_code text,
  email text,
  phone text,
  phone_home text,
  phone_cell text,
  hipaa_allow_sms text,
  hipaa_allow_email text,
  marital_status text,
  occupation text,
  race text,
  ethnicity text,
  interpreter text,
  family_size integer,
  monthly_income integer,
  homeless text,
  financial_review_date date,
  mother_name text,
  guardian_name text,
  guardian_relationship text,
  guardian_phone text,
  guardian_email text,
  guardian_sex text,
  guardian_address text,
  guardian_city text,
  guardian_state text,
  guardian_postal_code text,
  guardian_country text,
  guardian_work_phone text,
  provider_id integer references staff(id),
  facility_id integer references facilities(id),
  portal_enabled boolean not null,
  cms_portal_login text,
  merged_into_patient_id text references patients(canonical_id),
  merged_at timestamptz,
  merged_by text,
  registration_date date not null,
  administration_version bigint not null default 1,
  deceased_date date,
  deceased_reason text
);

create table patient_merge_audit_plans (
  audit_id uuid primary key,
  target_patient_id text not null,
  source_patient_id text not null,
  target_legacy_pid integer not null,
  source_legacy_pid integer not null,
  match_score integer not null,
  match_reasons text[] not null,
  rationale text,
  planned_by text not null,
  planned_at timestamptz not null,
  status text not null
);

create table patient_merge_executions (
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

create table patient_merge_execution_manifest_rows (
  execution_id uuid not null references patient_merge_executions(execution_id),
  table_name text not null,
  record_id text not null,
  primary key (execution_id, table_name, record_id)
);

create index ix_patient_merge_executions_source_patient
  on patient_merge_executions(source_patient_id);

create index ix_patient_merge_executions_target_patient
  on patient_merge_executions(target_patient_id);

create table patient_record_requests (
  request_id uuid primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  requested_at timestamptz not null,
  requested_by text not null,
  completed_at timestamptz,
  completed_by text
);

create unique index ux_patient_record_requests_one_open_per_patient
  on patient_record_requests(patient_id)
  where completed_at is null;

create index ix_patient_record_requests_patient_history
  on patient_record_requests(patient_id, requested_at desc);

create table patient_disclosure_authorities (
  authority_id uuid primary key,
  patient_id text not null references patients(canonical_id),
  authority_type text not null check (authority_type in ('patient','proxy')),
  proxy_name text,
  proxy_relationship text,
  purpose text not null,
  recipient text not null,
  scope_keys text[] not null,
  effective_from timestamptz not null,
  expires_at timestamptz not null,
  verification_method text not null
    check (verification_method in ('in-person','portal-authenticated','documented-authority','other')),
  verification_reference text not null,
  policy_revision text not null,
  status text not null check (status in ('pending','active','revoked')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null,
  check (expires_at > effective_from),
  check (cardinality(scope_keys) > 0),
  check (
    (authority_type = 'patient' and proxy_name is null and proxy_relationship is null)
    or
    (authority_type = 'proxy' and proxy_name is not null and proxy_relationship is not null)
  )
);

create index ix_patient_disclosure_authorities_patient
  on patient_disclosure_authorities(patient_id, created_at desc);

create table patient_disclosure_authority_events (
  event_id bigint generated always as identity primary key,
  authority_id uuid not null references patient_disclosure_authorities(authority_id) on delete cascade,
  action text not null check (action in ('created','activated','revoked')),
  from_status text,
  to_status text not null,
  version integer not null check (version >= 0),
  reason text not null,
  occurred_at timestamptz not null,
  username text not null,
  policy_revision text not null
);

create index ix_patient_disclosure_authority_events_authority
  on patient_disclosure_authority_events(authority_id, event_id desc);

create table patient_disclosure_requests (
  request_id uuid primary key,
  patient_id text not null references patients(canonical_id),
  authority_id uuid not null references patient_disclosure_authorities(authority_id),
  purpose text not null,
  recipient text not null,
  scope_keys text[] not null,
  status text not null check (status in ('requested','approved','denied')),
  version integer not null default 0 check (version >= 0),
  policy_revision text not null,
  requested_at timestamptz not null,
  requested_by text not null,
  decided_at timestamptz,
  decided_by text,
  decision_reason text,
  check (cardinality(scope_keys) > 0),
  check (
    (status = 'requested' and decided_at is null and decided_by is null and decision_reason is null)
    or
    (status in ('approved','denied') and decided_at is not null and decided_by is not null and decision_reason is not null)
  )
);

create index ix_patient_disclosure_requests_patient
  on patient_disclosure_requests(patient_id, requested_at desc);

create table patient_disclosure_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references patient_disclosure_requests(request_id) on delete cascade,
  action text not null check (action in ('requested','approved','denied')),
  from_status text,
  to_status text not null,
  version integer not null check (version >= 0),
  reason text not null,
  occurred_at timestamptz not null,
  username text not null,
  authority_id uuid not null,
  authority_version integer not null,
  authority_effective_status text not null,
  policy_revision text not null
);

create index ix_patient_disclosure_request_events_request
  on patient_disclosure_request_events(request_id, event_id desc);

create table patient_sdoh_assessments (
  assessment_id uuid primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  assessment_date date not null,
  screening_tool text,
  assessor text not null,
  instrument_score integer not null,
  hunger_q1 text,
  hunger_q2 text,
  hunger_score integer not null default 0,
  pregnancy_status text,
  pregnancy_edd date,
  pregnancy_intent text,
  postpartum_status text,
  postpartum_end date,
  disability_status text,
  disability_status_notes text,
  disability_scale jsonb not null default '{}'::jsonb,
  domains jsonb not null default '{}'::jsonb,
  interventions text,
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);

create index ix_patient_sdoh_assessments_patient_history
  on patient_sdoh_assessments(patient_id, assessment_date desc, updated_at desc);

create table patient_portal_accounts (
  patient_id text primary key references patients(canonical_id) on delete cascade,
  pid integer not null unique,
  portal_username text not null,
  portal_login_username text,
  password_salt text not null,
  password_hash text not null,
  password_status integer not null,
  one_time_token text
);

create table patient_portal_sessions (
  id uuid primary key,
  patient_id text not null references patients(canonical_id) on delete cascade,
  pid integer not null,
  portal_username text not null,
  portal_login_username text not null,
  created_at timestamptz not null,
  last_seen_at timestamptz not null,
  expires_at timestamptz not null,
  ended_at timestamptz,
  session_source text not null default 'avenchart-portal'
);

create table patient_portal_profile_change_requests (
  id bigserial primary key,
  patient_id text not null references patients(canonical_id) on delete cascade,
  pid integer not null,
  session_id uuid references patient_portal_sessions(id) on delete set null,
  portal_username text not null,
  portal_login_username text not null,
  activity text not null default 'profile',
  require_audit integer not null default 1,
  pending_action text not null default 'review',
  action_taken text not null default '',
  status text not null default 'waiting',
  narrative text not null default 'Patient request changes to demographics.',
  table_action text not null default '',
  requested_changes jsonb not null,
  action_user text not null default '0',
  action_taken_at timestamptz,
  checksum text not null default '0',
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table patient_portal_report_audit_events (
  id bigserial primary key,
  patient_id text not null references patients(canonical_id) on delete cascade,
  pid integer not null,
  session_id uuid references patient_portal_sessions(id) on delete set null,
  portal_username text not null,
  portal_login_username text not null,
  event_type text not null,
  event_label text not null,
  report_title text not null,
  generated_on date not null,
  artifact_name text,
  artifact_content_type text,
  included_section_ids text[] not null default '{}',
  included_issue_ids text[] not null default '{}',
  included_encounter_form_ids text[] not null default '{}',
  included_procedure_order_ids text[] not null default '{}',
  summary text not null,
  created_at timestamptz not null default now(),
  event_source text not null default 'avenchart-portal'
);

create table patient_portal_message_audit_events (
  id bigserial primary key,
  patient_id text not null references patients(canonical_id) on delete cascade,
  pid integer not null,
  session_id uuid references patient_portal_sessions(id) on delete set null,
  portal_username text not null,
  portal_login_username text not null,
  event_type text not null,
  event_label text not null,
  message_id text not null,
  related_message_ids text[] not null default '{}',
  message_title text not null,
  message_status text not null,
  recipient_id text,
  recipient_name text,
  thread_id integer not null default 0,
  archived_message_count integer not null default 0,
  summary text not null,
  created_at timestamptz not null default now(),
  event_source text not null default 'avenchart-portal'
);

create table patient_employers (
  patient_id text primary key references patients(canonical_id) on delete cascade,
  pid integer not null,
  name text,
  street text,
  city text,
  state text,
  postal_code text,
  country text,
  recorded_date date
);

create table patient_histories (
  patient_id text primary key references patients(canonical_id) on delete cascade,
  pid integer not null,
  coffee text,
  tobacco text,
  alcohol text,
  sleep_patterns text,
  exercise_patterns text,
  seatbelt_use text,
  counseling text,
  hazardous_activities text,
  recreational_drugs text,
  last_physical_exam text,
  last_mammogram text,
  last_prostate_exam text,
  last_colonoscopy text,
  last_ecg text,
  last_retinal text,
  last_fluvax text,
  last_pneuvax text,
  last_ldl text,
  last_hemoglobin text,
  last_psa text,
  last_exam_results text,
  history_mother text,
  history_father text,
  history_siblings text,
  history_offspring text,
  history_spouse text,
  relatives_cancer text,
  relatives_tuberculosis text,
  relatives_diabetes text,
  relatives_high_blood_pressure text,
  relatives_heart_problems text,
  relatives_stroke text,
  relatives_epilepsy text,
  relatives_mental_illness text,
  relatives_suicide text,
  appendectomy_date date,
  tonsillectomy_date date,
  cholecystectomy_date date,
  heart_surgery_date date,
  hysterectomy_date date,
  hernia_repair_date date,
  hip_replacement_date date,
  knee_replacement_date date,
  additional_history text,
  exams text,
  recorded_at timestamp
);

create table patient_related_contacts (
  contact_id bigint primary key,
  person_id bigint not null,
  patient_id text not null references patients(canonical_id) on delete cascade,
  pid integer not null,
  display_name text not null,
  relationship text,
  phone text,
  email text,
  active boolean not null default true
);

create table patient_care_teams (
  patient_id text primary key references patients(canonical_id) on delete cascade,
  pid integer not null,
  team_name text not null default 'Care Team',
  team_status text not null default 'active',
  note text,
  updated_at timestamptz not null default now()
);

create table patient_care_team_members (
  id bigserial primary key,
  patient_id text not null references patient_care_teams(patient_id) on delete cascade,
  user_id integer references staff(id),
  contact_id bigint references patient_related_contacts(contact_id),
  role text not null,
  facility_id integer references facilities(id),
  provider_since date,
  status text not null default 'active',
  note text
);

create table insurance_records (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  type text,
  provider text,
  plan_name text,
  policy_number text,
  group_number text,
  relationship text,
  subscriber_first_name text,
  subscriber_middle_name text,
  subscriber_last_name text,
  subscriber_date_of_birth date,
  subscriber_sex text,
  subscriber_street text,
  subscriber_street_line_2 text,
  subscriber_city text,
  subscriber_state text,
  subscriber_postal_code text,
  subscriber_country text,
  subscriber_phone text,
  subscriber_employer text,
  subscriber_employer_street text,
  subscriber_employer_street_line_2 text,
  subscriber_employer_city text,
  subscriber_employer_state text,
  subscriber_employer_postal_code text,
  subscriber_employer_country text
);

create table appointments (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  provider_id integer references staff(id),
  facility_id integer references facilities(id),
  billing_location_id integer references facilities(id),
  appointment_date date not null,
  start_time time not null,
  duration_minutes integer not null,
  category_id integer,
  title text,
  status text,
  room text,
  comments text,
  recurrence_type integer not null default 0,
  repeat_frequency integer,
  repeat_unit integer,
  repeat_on_num integer,
  repeat_on_day integer,
  repeat_on_frequency integer,
  recurrence_end_date date,
  recurrence_days text,
  recurrence_exdates text
);

create table encounters (
  id integer primary key,
  encounter integer not null unique,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  provider_id integer references staff(id),
  facility_id integer references facilities(id),
  billing_facility_id integer references facilities(id),
  encounter_date date not null,
  encounter_datetime timestamp not null,
  reason text,
  diagnosis_code text,
  diagnosis_text text,
  category_id integer,
  sensitivity text,
  referral_source text,
  external_id text,
  pos_code integer,
  billing_note text,
  source_appointment_id text references appointments(id)
);
create table encounter_layout_form_records (
  record_id uuid primary key, encounter integer not null, layout_key text not null references form_layouts(layout_key),
  revision integer not null, saved_at timestamptz not null, saved_by text not null, unique (encounter, layout_key, revision)
);
create table encounter_layout_form_values (
  record_id uuid not null references encounter_layout_form_records(record_id), field_key text not null, field_label text not null,
  field_value text not null, primary key (record_id, field_key)
);
create table encounter_clinical_alert_acknowledgments (
  encounter integer not null, rule_key text not null references clinical_alert_rules(rule_key),
  acknowledged_at timestamptz not null, acknowledged_by text not null, reopened_at timestamptz, reopened_by text,
  primary key (encounter, rule_key)
);

create table encounter_signatures (
  id integer primary key,
  encounter_id integer not null references encounters(id) on delete cascade,
  encounter integer not null,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  table_name text not null,
  signer_user_id integer references staff(id),
  signer_username text not null,
  signed_at timestamp not null,
  is_lock boolean not null default false,
  amendment text,
  hash text not null,
  signature_hash text not null
);

create table encounter_audit_events (
  event_id uuid primary key,
  encounter integer not null,
  occurred_at timestamptz not null,
  username text not null,
  action text not null,
  changed_fields text not null
);

create table vitals (
  id integer primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  encounter integer,
  vital_datetime timestamp not null,
  bps integer,
  bpd integer,
  weight numeric(8,2),
  height numeric(8,2),
  temperature numeric(5,2),
  pulse integer,
  respiration integer,
  bmi numeric(6,2),
  oxygen_saturation integer,
  note text
);

create table clinical_notes (
  id integer primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  encounter integer,
  note_datetime timestamp not null,
  subjective text,
  objective text,
  assessment text,
  plan text
);

create table pharmacies (
  id integer primary key,
  name text not null,
  transmit_method integer not null default 1,
  email text,
  ncpdp integer,
  npi integer
);

create table medication_vocabulary (
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

create table inventory_items (
  item_id integer primary key,
  item_code text not null unique,
  name text not null,
  category text not null,
  unit text not null,
  reorder_point numeric(12,2) not null default 0,
  preferred_quantity numeric(12,2) not null default 0,
  active boolean not null default true
);

create sequence inventory_lot_id_seq;

create table inventory_lots (
  lot_id integer primary key default nextval('inventory_lot_id_seq'),
  item_id integer not null references inventory_items(item_id),
  facility_id integer not null references facilities(id),
  lot_number text not null,
  expiration_date date,
  quantity_on_hand numeric(12,2) not null default 0,
  unit_cost numeric(12,2) not null default 0,
  status text not null default 'active',
  unique (item_id, facility_id, lot_number)
);

create table inventory_vendors (
  vendor_id uuid primary key,
  name text not null,
  contact_name text,
  phone text,
  email text,
  active boolean not null default true,
  created_at timestamptz not null default now(),
  created_by text not null
);

create unique index ux_inventory_vendors_name_lower on inventory_vendors (lower(name));

create table inventory_purchase_receipts (
  receipt_id uuid primary key,
  vendor_id uuid not null references inventory_vendors(vendor_id),
  facility_id integer not null references facilities(id),
  reference_number text,
  received_at timestamptz not null,
  received_by text not null,
  notes text not null,
  created_at timestamptz not null default now(),
  unique (vendor_id, reference_number)
);

create table inventory_transactions (
  transaction_id uuid primary key,
  lot_id integer not null references inventory_lots(lot_id),
  transfer_id uuid,
  receipt_id uuid references inventory_purchase_receipts(receipt_id),
  transaction_type text not null,
  quantity_delta numeric(12,2) not null,
  reason text,
  performed_by text not null,
  occurred_at timestamptz not null
);

create table prescriptions (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  provider_id integer references staff(id),
  encounter integer,
  start_date date,
  date_added timestamp,
  modified_date date,
  end_date date,
  drug text not null,
  rx_norm_code text,
  dosage text,
  quantity text,
  dose_amount numeric(10,2),
  dose_unit text,
  frequency text,
  duration_days integer,
  route text,
  refills integer not null default 0,
  diagnosis text,
  note text,
  active integer not null default 1,
  pharmacy_id integer references pharmacies(id),
  pharmacy_name text,
  pharmacy_ncpdp integer,
  erx_uploaded integer not null default 0,
  erx_sent_at timestamp,
  erx_payload text
);

create table prescription_audit_events (
  event_id text primary key,
  prescription_id text not null,
  patient_id text not null references patients(canonical_id),
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

create table immunizations (
  id integer primary key,
  key text not null unique,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  encounter integer,
  immunization_id integer,
  cvx_code text,
  vaccine text,
  administered_at timestamp,
  manufacturer text,
  lot_number text,
  administered_by_id integer references staff(id),
  administered_by text,
  education_date date,
  vis_date date,
  amount_administered numeric(6,2),
  amount_administered_unit text,
  expiration_date date,
  route text,
  administration_site text,
  completion_status text,
  information_source text,
  note text,
  added_erroneously integer not null default 0
);

create table billing (
  id text primary key,
  pid integer not null,
  provider_id integer references staff(id),
  encounter integer,
  billing_date date not null,
  code_type text,
  code text,
  modifier text,
  code_text text,
  fee numeric(10,2),
  justify text,
  units integer not null default 1,
  billed integer not null default 0,
  activity integer not null default 1
);

create table claims (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  encounter integer not null,
  version integer not null,
  payer_id integer not null,
  payer_name text,
  payer_type integer not null default 0,
  status integer not null default 0,
  bill_process integer not null default 0,
  bill_time timestamp,
  process_time timestamp,
  process_file text,
  target text,
  x12_partner_id integer not null default 0,
  submitted_claim text,
  unique (pid, encounter, version)
);

create table payment_sessions (
  id integer primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  payer_id integer not null,
  payer_name text,
  user_id integer not null references staff(id),
  user_name text,
  closed integer not null default 0,
  reference text not null,
  check_date date,
  deposit_date date,
  pay_total numeric(12,2) not null default 0,
  created_time timestamp not null,
  modified_time timestamp not null,
  global_amount numeric(12,2) not null default 0,
  payment_type text not null,
  description text,
  adjustment_code text,
  post_to_date date not null,
  payment_method text not null
);

create table payment_activities (
  id text primary key,
  session_id integer not null references payment_sessions(id),
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  encounter integer not null,
  sequence_no integer not null,
  code_type text,
  code text,
  modifier text,
  payer_type integer not null,
  post_time timestamp not null,
  post_user_id integer not null references staff(id),
  post_user_name text,
  memo text,
  pay_amount numeric(12,2) not null default 0,
  adj_amount numeric(12,2) not null default 0,
  modified_time timestamp not null,
  follow_up text,
  follow_up_note text,
  account_code text,
  reason_code text,
  deleted timestamp,
  post_date date,
  payer_claim_number text,
  unique (pid, encounter, sequence_no)
);

create table statement_delivery_audit_events (
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

create table statement_email_outbox (
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

create table integration_outbox (
  event_id uuid primary key,
  idempotency_key text unique,
  event_type text not null,
  aggregate_type text not null,
  aggregate_id text not null,
  destination text not null,
  payload jsonb not null,
  status text not null,
  attempt_count integer not null default 0,
  available_at timestamptz not null,
  locked_at timestamptz,
  last_attempt_at timestamptz,
  delivered_at timestamptz,
  external_reference text,
  last_error text,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table integration_inbox (
  inbox_id uuid primary key,
  source text not null,
  source_message_id text not null,
  message_type text not null,
  payload jsonb not null,
  status text not null,
  attempt_count integer not null default 0,
  received_at timestamptz not null,
  processed_at timestamptz,
  last_error text,
  unique (source, source_message_id)
);

create table phi_access_audit_events (
  audit_id uuid primary key,
  occurred_at timestamptz not null,
  username text not null,
  session_id uuid,
  http_method text not null,
  endpoint_name text not null,
  required_permission text not null,
  authorized boolean not null,
  response_status integer not null
);

create table lab_orders (
  id integer primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  encounter integer,
  provider_id integer references staff(id),
  lab_id integer,
  order_date date not null,
  order_priority text,
  code text,
  name text,
  procedure_type text,
  diagnosis text,
  instructions text,
  order_status text,
  date_transmitted timestamp
);

create table lab_provider_address_book (
  id integer primary key,
  organization text not null,
  type text not null default 'ord_lab',
  active boolean not null default true
);

create table lab_providers (
  id integer primary key,
  name text not null,
  lab_director_id integer references lab_provider_address_book(id),
  npi text,
  protocol text not null default 'DL',
  usage text not null default 'D',
  direction text not null default 'B',
  send_app_id text not null default '',
  send_fac_id text not null default '',
  recv_app_id text not null default '',
  recv_fac_id text not null default '',
  remote_host text not null default '',
  login text not null default '',
  password text not null default '',
  orders_path text not null default '',
  results_path text not null default '',
  notes text,
  active boolean not null default true
);

create table lab_order_catalog (
  id integer primary key,
  parent_id integer,
  lab_id integer references lab_providers(id),
  code text,
  name text not null,
  item_type text not null,
  procedure_type_name text,
  description text,
  specimen text,
  standard_code text,
  seq integer not null,
  active boolean not null default true
);

create table lab_reports (
  id integer primary key,
  order_id integer not null references lab_orders(id),
  specimen_id integer,
  date_collected timestamp not null,
  report_date timestamp not null,
  specimen_number text,
  status text,
  review_status text,
  reviewed_by text,
  reviewed_at timestamp,
  review_version integer not null default 1,
  notes text
);

create table lab_report_review_events (
  id bigserial primary key,
  report_id integer not null references lab_reports(id) on delete cascade,
  action text not null,
  previous_status text,
  current_status text not null,
  assigned_to text,
  actor text not null,
  reason text,
  expected_version integer not null,
  resulting_version integer not null,
  occurred_at timestamp not null
);

create table lab_specimens (
  id integer primary key,
  order_id integer not null references lab_orders(id),
  specimen_identifier text,
  accession_identifier text,
  specimen_type_code text,
  specimen_type text,
  collection_method_code text,
  collection_method text,
  specimen_location_code text,
  specimen_location text,
  collected_date timestamp not null,
  volume_value numeric(10,3),
  volume_unit text,
  condition_code text,
  specimen_condition text,
  comments text
);

create table lab_results (
  id integer primary key,
  report_id integer not null references lab_reports(id),
  code text,
  text text,
  units text,
  result text,
  range text,
  abnormal text,
  result_date timestamp not null,
  result_status text
);

create table critical_lab_result_acknowledgements (
  result_id integer primary key references lab_results(id) on delete cascade,
  status text not null default 'open' check (status in ('open', 'acknowledged')),
  version integer not null default 1,
  acknowledged_by text,
  acknowledged_at timestamp,
  acknowledgement_reason text
);

create table critical_lab_result_acknowledgement_events (
  id bigserial primary key,
  result_id integer not null references lab_results(id) on delete cascade,
  action text not null,
  previous_status text,
  current_status text not null,
  actor text not null,
  reason text not null,
  expected_version integer not null,
  resulting_version integer not null,
  occurred_at timestamp not null
);

create table procedure_result_versions (
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

create table messages (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  message_date date not null,
  title text,
  body text,
  status text,
  assigned_to text,
  portal_relation text,
  is_encrypted boolean not null default false,
  updated_by integer,
  updated_at timestamp,
  deleted integer not null default 0,
  activity integer not null default 1,
  assignment_version integer not null default 0
);

create table portal_mailbox_messages (
  id integer primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  message_date date not null,
  body text,
  owner text not null,
  user_value text not null,
  group_name text not null default 'Default',
  activity integer not null default 1,
  authorized integer not null default 1,
  title text,
  assigned_to text,
  message_status text,
  portal_relation text,
  mail_chain integer not null,
  sender_id text not null,
  sender_name text not null,
  recipient_id text not null,
  recipient_name text not null,
  reply_mail_chain integer not null,
  is_encrypted boolean not null default false,
  deleted integer not null default 0
);

create table patient_portal_message_attachments (
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

create table patient_reminders (
  id integer primary key,
  active integer not null default 1,
  date_inactivated timestamp,
  reason_inactivated text not null default '',
  due_status text not null default '',
  pid integer not null,
  category text not null default '',
  item text not null default '',
  date_created timestamp,
  date_sent timestamp,
  voice_status integer not null default 0,
  sms_status integer not null default 0,
  email_status integer not null default 0,
  mail_status integer not null default 0
);

create table patient_documents (
  id integer primary key,
  document_key text not null unique,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  category_id integer not null,
  category_name text not null,
  name text not null,
  doc_date date not null,
  uploaded_at timestamp not null,
  mimetype text,
  file_name text,
  size_bytes integer,
  pages integer,
  encounter integer,
  storage_method text,
  url text,
  hash text,
  documentation_of text,
  notes text,
  review_status text not null default 'pending',
  reviewed_by text,
  reviewed_at timestamp,
  content text,
  content_bytes bytea,
  deleted integer not null default 0
);

create table patient_document_versions (
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

create table problems (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  type text,
  title text,
  diagnosis text,
  problem_date date,
  comments text,
  activity integer not null default 1,
  end_date date
);

create table allergies (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  type text,
  title text,
  reaction text,
  severity text,
  allergy_date date,
  comments text,
  activity integer not null default 1,
  end_date date,
  list_option_id text
);

create table medications (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  type text,
  title text,
  diagnosis text,
  medication_date date,
  modified_date date,
  comments text,
  activity integer not null default 1,
  end_date date,
  lifecycle_version integer not null default 1
);

create table medication_list_lifecycle_events (
  id bigserial primary key,
  medication_id text not null references medications(id) on delete cascade,
  action text not null check (action in ('created', 'deactivated', 'restored', 'edited')),
  previous_activity integer,
  current_activity integer not null,
  actor text not null,
  reason text,
  expected_version integer not null,
  resulting_version integer not null,
  occurred_at timestamp not null
);


create index idx_patients_name on patients (last_name, first_name);
create index idx_patients_legacy_pid on patients (legacy_pid);
create index idx_patient_employers_pid on patient_employers (pid);
create index idx_patient_histories_pid on patient_histories (pid);
create index idx_patient_related_contacts_patient on patient_related_contacts (patient_id);
create index idx_patient_care_team_members_patient on patient_care_team_members (patient_id);
create index idx_insurance_records_pid on insurance_records (pid);
create index idx_appointments_pid_date on appointments (pid, appointment_date, start_time);
create index idx_encounters_pid_date on encounters (pid, encounter_date);
create index idx_encounter_signatures_encounter on encounter_signatures (encounter, signed_at);
create index idx_encounter_audit_events_encounter on encounter_audit_events (encounter, occurred_at desc);
create index idx_vitals_pid_date on vitals (pid, vital_datetime);
create index idx_clinical_notes_pid_date on clinical_notes (pid, note_datetime);
create index idx_prescriptions_pid on prescriptions (pid);
create index idx_prescription_audit_events_prescription on prescription_audit_events (prescription_id, occurred_at, event_id);
create index idx_prescription_audit_events_pid on prescription_audit_events (pid, occurred_at desc, event_id desc);
create index idx_immunizations_pid_date on immunizations (pid, administered_at);
create index idx_billing_pid on billing (pid);
create index idx_payment_sessions_pid on payment_sessions (pid);
create index idx_payment_activities_pid_encounter on payment_activities (pid, encounter);
create index idx_statement_delivery_audit_dispatch on statement_delivery_audit_events (dispatch_id, dispatched_at desc);
create index idx_statement_delivery_audit_pid_created on statement_delivery_audit_events (legacy_pid, created_at desc);
create index idx_statement_email_outbox_batch on statement_email_outbox (outbox_batch_id, queued_at desc);
create index idx_statement_email_outbox_pid_created on statement_email_outbox (legacy_pid, created_at desc);
create index idx_integration_outbox_dispatch on integration_outbox (status, available_at, created_at);
create index idx_integration_inbox_status on integration_inbox (status, received_at);
create index idx_phi_access_audit_username_occurred on phi_access_audit_events (username, occurred_at desc);
create index idx_phi_access_audit_endpoint_occurred on phi_access_audit_events (endpoint_name, occurred_at desc);
create index idx_inventory_lots_item_facility on inventory_lots (item_id, facility_id, status);
create index idx_inventory_transactions_lot_occurred on inventory_transactions (lot_id, occurred_at desc);
create index idx_inventory_transactions_transfer on inventory_transactions (transfer_id, occurred_at desc) where transfer_id is not null;
create index idx_inventory_purchase_receipts_facility_received on inventory_purchase_receipts (facility_id, received_at desc);
create index idx_inventory_transactions_receipt on inventory_transactions (receipt_id) where receipt_id is not null;
create index idx_lab_orders_pid on lab_orders (pid);
create index idx_lab_orders_lab_id on lab_orders (lab_id);
create index idx_lab_order_catalog_parent_id on lab_order_catalog (parent_id);
create index idx_lab_order_catalog_lab_id on lab_order_catalog (lab_id);
create index idx_lab_reports_date on lab_reports (report_date);
create index idx_lab_report_review_events_report on lab_report_review_events (report_id, occurred_at desc, id desc);
create index idx_lab_results_date on lab_results (result_date);
create index idx_critical_lab_result_ack_events_result on critical_lab_result_acknowledgement_events (result_id, occurred_at desc, id desc);
create index idx_procedure_result_versions_result on procedure_result_versions (result_id, version_no desc);
create index idx_messages_pid on messages (pid);
create index idx_portal_mailbox_owner_recipient on portal_mailbox_messages (owner, recipient_id, deleted);
create index idx_portal_mailbox_owner_sender on portal_mailbox_messages (owner, sender_id, deleted);
create index idx_patient_portal_message_attachments_message on patient_portal_message_attachments (message_id, uploaded_at, id);
create index idx_patient_reminders_pid_active_created on patient_reminders (pid, active, date_created desc);
create index idx_patient_portal_report_audit_patient_created on patient_portal_report_audit_events (patient_id, created_at desc, id desc);
create index idx_patient_portal_report_audit_session on patient_portal_report_audit_events (session_id);
create index idx_patient_portal_message_audit_patient_created on patient_portal_message_audit_events (patient_id, created_at desc, id desc);
create index idx_patient_portal_message_audit_session on patient_portal_message_audit_events (session_id);
create index idx_patient_portal_message_audit_message on patient_portal_message_audit_events (message_id);
create index idx_patient_portal_profile_change_pending on patient_portal_profile_change_requests (patient_id, status, pending_action, created_at, id);
create index idx_patient_documents_pid_date on patient_documents (pid, doc_date);
create index idx_patient_documents_category on patient_documents (category_name);
create index idx_patient_document_versions_document on patient_document_versions (document_id, version_no desc);
create index idx_problems_pid on problems (pid);
create index idx_allergies_pid on allergies (pid);
create index idx_medications_pid on medications (pid);
create index idx_medication_list_lifecycle_events_medication on medication_list_lifecycle_events (medication_id, occurred_at desc, id desc);
create index idx_access_group_permissions_group on access_group_permissions (group_value);
create index idx_access_group_permissions_permission on access_group_permissions (section_value, permission_value);
create index idx_access_user_memberships_user on access_user_memberships (user_value);
create index idx_access_user_memberships_group on access_user_memberships (group_value);
commit;
