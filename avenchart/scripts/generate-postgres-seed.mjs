import fs from 'node:fs'
import crypto from 'node:crypto'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const solutionRoot = path.resolve(__dirname, '..')
const workspaceRoot = path.resolve(solutionRoot, '..')
const datasetPath = path.join(
  workspaceRoot,
  'demo-data',
  'seed-data',
  'legacy-ehr-shared-synthetic-v1',
  'generated',
  'canonical',
  'gold-dataset.json',
)
const outputDir = path.join(solutionRoot, 'artifacts', 'postgres')
const outputPath = path.join(outputDir, 'seed-gold.sql')

const dataset = JSON.parse(fs.readFileSync(datasetPath, 'utf8'))
fs.mkdirSync(outputDir, { recursive: true })

const copyEmptyString = Symbol('copy-empty-string')
const demoCredentialSalt = 'legacy-ehr-modernized-demo-v1'
const patientsByPid = new Map(dataset.patients.map((patient) => [patient.pid, patient]))
const portalMailboxMessages = dataset.messages
  .map((message, index) => {
    const patient = patientsByPid.get(message.pid)
    if (!patient?.portalAccount) {
      return null
    }

    return {
      id: 9300000 + index + 1,
      message,
      patient,
    }
  })
  .filter(Boolean)

function hashDemoPassword(password) {
  return crypto.createHash('sha256').update(`${demoCredentialSalt}:${password}`, 'utf8').digest('hex')
}

const accessGroups = [
  [10, 'users', 'Legacy EHR Users', null],
  [11, 'admin', 'Administrators', 10],
  [12, 'clin', 'Clinicians', 10],
  [13, 'doc', 'Physicians', 10],
  [14, 'front', 'Front Office', 10],
  [15, 'back', 'Accounting', 10],
  [16, 'breakglass', 'Emergency Login', 10],
]

const accessUserMemberships = [
  ['admin', 'Administrator', 'admin', 'Administrators', null],
  ['oe-system', 'System Operation User', 'admin', 'Administrators', null],
  ['gold-frontdesk-01', 'Parker Fleming', 'front', 'Front Office', 117],
  ['gold-provider-01', 'Alex Walker', 'clin', 'Clinicians', 101],
]

const accessPermissions = [
  ['acct', 'bill', 'Billing (write optional)'],
  ['acct', 'disc', 'Price Discounting'],
  ['acct', 'eob', 'EOB Data Entry'],
  ['acct', 'rep', 'Financial Reporting - my encounters'],
  ['acct', 'rep_a', 'Financial Reporting - anything'],
  ['admin', 'acl', 'ACL Administration'],
  ['admin', 'batchcom', 'Batch Communication Tool'],
  ['admin', 'calendar', 'Calendar Settings'],
  ['admin', 'database', 'Database Reporting'],
  ['admin', 'drugs', 'Inventory Administration'],
  ['admin', 'forms', 'Forms Administration'],
  ['admin', 'language', 'Language Interface Tool'],
  ['admin', 'manage_modules', 'Manage modules'],
  ['admin', 'menu', 'Menu'],
  ['admin', 'practice', 'Practice Settings'],
  ['admin', 'super', 'Superuser'],
  ['admin', 'superbill', 'Superbill Codes Administration'],
  ['admin', 'users', 'Users/Groups/Logs Administration'],
  ['encounters', 'auth', 'Authorize - my encounters'],
  ['encounters', 'auth_a', 'Authorize - any encounters'],
  ['encounters', 'coding', 'Coding - my encounters (write,wsome optional)'],
  ['encounters', 'coding_a', 'Coding - any encounters (write,wsome optional)'],
  ['encounters', 'date_a', 'Fix encounter dates - any encounters'],
  ['encounters', 'notes', 'Notes - my encounters (write,addonly optional)'],
  ['encounters', 'notes_a', 'Notes - any encounters (write,addonly optional)'],
  ['encounters', 'relaxed', 'Less-protected information (write,addonly optional)'],
  ['groups', 'gadd', 'View/Add/Update groups'],
  ['groups', 'gcalendar', 'View/Create/Update groups appointment in calendar'],
  ['groups', 'gdlog', 'Group detailed log of appointment in patient record'],
  ['groups', 'glog', 'Group encounter log'],
  ['groups', 'gm', 'Send message from the permanent group therapist to the personal therapist'],
  ['inventory', 'adjustments', 'Adjustments'],
  ['inventory', 'consumption', 'Consumption'],
  ['inventory', 'destruction', 'Destruction'],
  ['inventory', 'lots', 'Lots'],
  ['inventory', 'purchases', 'Purchases'],
  ['inventory', 'reporting', 'Reporting'],
  ['inventory', 'sales', 'Sales'],
  ['inventory', 'transfers', 'Transfers'],
  ['lists', 'country', 'Country List (write,addonly optional)'],
  ['lists', 'default', 'Default List (write,addonly optional)'],
  ['lists', 'ethrace', 'Ethnicity-Race List (write,addonly optional)'],
  ['lists', 'language', 'Language List (write,addonly optional)'],
  ['lists', 'state', 'State List (write,addonly optional)'],
  ['menus', 'modle', 'Modules'],
  ['nationnotes', 'nn_configure', 'Nation Notes Configure'],
  ['patientportal', 'portal', 'Patient Portal'],
  ['patients', 'alert', 'Clinical Reminders/Alerts (write,addonly optional)'],
  ['patients', 'amendment', 'Amendments (write,addonly optional)'],
  ['patients', 'appt', 'Appointments (write,wsome optional)'],
  ['patients', 'demo', 'Demographics (write,addonly optional)'],
  ['patients', 'disclosure', 'Disclosures (write,addonly optional)'],
  ['patients', 'docs', 'Documents (write,addonly optional)'],
  ['patients', 'docs_rm', 'Documents Delete'],
  ['patients', 'lab', 'Lab Results (write,addonly optional)'],
  ['patients', 'med', 'Medical/History (write,addonly optional)'],
  ['patients', 'notes', 'Patient Notes (write,addonly optional)'],
  ['patients', 'pat_rep', 'Patient Report'],
  ['patients', 'reminder', 'Patient Reminders (write,addonly optional)'],
  ['patients', 'rx', 'Prescriptions (write,addonly optional)'],
  ['patients', 'sign', 'Sign Lab Results (write,addonly optional)'],
  ['patients', 'trans', 'Transactions (write optional)'],
  ['placeholder', 'filler', 'Placeholder (Maintains empty ACLs)'],
  ['sensitivities', 'high', 'High'],
  ['sensitivities', 'normal', 'Normal'],
]

const permissionName = new Map(accessPermissions.map(([section, value, name]) => [`${section}:${value}`, name]))
const allNonPlaceholderPermissions = accessPermissions
  .filter(([section]) => section !== 'placeholder')
  .map(([section, value]) => [section, value, 'write'])
const groupPermissionRules = {
  admin: allNonPlaceholderPermissions,
  breakglass: allNonPlaceholderPermissions,
  clin: [
    ['admin', 'drugs', 'write'],
    ['encounters', 'auth', 'write'],
    ['encounters', 'coding', 'write'],
    ['encounters', 'notes', 'addonly'],
    ['encounters', 'notes', 'write'],
    ['encounters', 'relaxed', 'addonly'],
    ['groups', 'gcalendar', 'write'],
    ['groups', 'glog', 'write'],
    ['patients', 'alert', 'addonly'],
    ['patients', 'amendment', 'addonly'],
    ['patients', 'appt', 'write'],
    ['patients', 'demo', 'addonly'],
    ['patients', 'disclosure', 'addonly'],
    ['patients', 'docs', 'addonly'],
    ['patients', 'lab', 'addonly'],
    ['patients', 'med', 'write'],
    ['patients', 'notes', 'addonly'],
    ['patients', 'pat_rep', 'view'],
    ['patients', 'reminder', 'addonly'],
    ['patients', 'rx', 'addonly'],
    ['patients', 'trans', 'addonly'],
    ['placeholder', 'filler', 'wsome'],
    ['sensitivities', 'normal', 'addonly'],
  ],
  doc: [
    ['acct', 'disc', 'write'],
    ['acct', 'rep', 'write'],
    ['admin', 'drugs', 'write'],
    ['encounters', 'auth', 'write'],
    ['encounters', 'auth_a', 'write'],
    ['encounters', 'coding', 'write'],
    ['encounters', 'coding_a', 'write'],
    ['encounters', 'date_a', 'write'],
    ['encounters', 'notes', 'write'],
    ['encounters', 'notes_a', 'write'],
    ['encounters', 'relaxed', 'write'],
    ['groups', 'gcalendar', 'write'],
    ['groups', 'glog', 'write'],
    ['patients', 'alert', 'write'],
    ['patients', 'amendment', 'write'],
    ['patients', 'appt', 'write'],
    ['patients', 'demo', 'write'],
    ['patients', 'disclosure', 'write'],
    ['patients', 'docs', 'write'],
    ['patients', 'lab', 'write'],
    ['patients', 'med', 'write'],
    ['patients', 'notes', 'write'],
    ['patients', 'pat_rep', 'view'],
    ['patients', 'reminder', 'write'],
    ['patients', 'rx', 'write'],
    ['patients', 'sign', 'write'],
    ['patients', 'trans', 'write'],
    ['placeholder', 'filler', 'addonly'],
    ['placeholder', 'filler', 'wsome'],
    ['sensitivities', 'high', 'write'],
    ['sensitivities', 'normal', 'write'],
  ],
  front: [
    ['groups', 'gcalendar', 'write'],
    ['patients', 'alert', 'view'],
    ['patients', 'appt', 'write'],
    ['patients', 'demo', 'write'],
    ['placeholder', 'filler', 'addonly'],
    ['placeholder', 'filler', 'wsome'],
  ],
  back: [
    ['acct', 'bill', 'write'],
    ['acct', 'disc', 'write'],
    ['acct', 'eob', 'write'],
    ['acct', 'rep', 'write'],
    ['acct', 'rep_a', 'write'],
    ['admin', 'practice', 'write'],
    ['admin', 'superbill', 'write'],
    ['encounters', 'auth_a', 'write'],
    ['encounters', 'coding_a', 'write'],
    ['encounters', 'date_a', 'write'],
    ['patients', 'alert', 'view'],
    ['patients', 'appt', 'write'],
    ['patients', 'demo', 'write'],
    ['placeholder', 'filler', 'addonly'],
    ['placeholder', 'filler', 'wsome'],
  ],
}

const accessGroupPermissions = Object.entries(groupPermissionRules).flatMap(([groupValue, rules]) =>
  rules.map(([section, value, returnValue]) => [
    groupValue,
    section,
    value,
    permissionName.get(`${section}:${value}`) ?? value,
    returnValue,
  ]),
)

const lines = []
lines.push(`-- Generated from ${dataset.datasetId} ${dataset.version}`)
lines.push('set client_min_messages to warning;')
lines.push('begin;')
lines.push(`
drop table if exists medications;
drop table if exists allergies;
drop table if exists problems;
drop table if exists patient_documents;
drop table if exists patient_reminders;
drop table if exists portal_mailbox_messages;
drop table if exists messages;
drop table if exists lab_results;
drop table if exists lab_reports;
drop table if exists lab_specimens;
drop table if exists lab_orders;
drop table if exists lab_order_catalog;
drop table if exists lab_providers;
drop table if exists lab_provider_address_book;
drop table if exists encounter_signatures;
drop table if exists statement_delivery_audit_events;
drop table if exists payment_activities;
drop table if exists payment_sessions;
drop table if exists claims;
drop table if exists billing;
drop table if exists immunizations;
drop table if exists prescriptions;
drop table if exists pharmacies;
drop table if exists clinical_notes;
drop table if exists vitals;
drop table if exists encounters;
drop table if exists appointments;
drop table if exists insurance_records;
drop table if exists patient_care_team_members;
drop table if exists patient_care_teams;
drop table if exists patient_related_contacts;
drop table if exists patient_histories;
drop table if exists patient_employers;
drop table if exists patient_portal_profile_change_requests;
drop table if exists patient_portal_message_audit_events;
drop table if exists patient_portal_report_audit_events;
drop table if exists patient_portal_sessions;
drop table if exists patient_portal_accounts;
drop table if exists patients;
drop table if exists access_user_memberships;
drop table if exists auth_sessions;
drop table if exists auth_audit_events;
drop table if exists auth_accounts;
drop table if exists staff;
drop table if exists facilities;
drop table if exists access_group_permissions;
drop table if exists access_permissions;
drop table if exists access_groups;
drop table if exists dataset_metadata;

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
  registration_date date not null,
  deceased_date date,
  deceased_reason text
);

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
  date_collected timestamp not null,
  report_date timestamp not null,
  specimen_number text,
  status text,
  review_status text,
  reviewed_by text,
  reviewed_at timestamp,
  notes text
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
  activity integer not null default 1
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
  end_date date
);
`)

copyRows('dataset_metadata', [
  'dataset_id',
  'version',
  'generated_at',
  'base_date',
  'patient_count',
  'appointment_count',
  'encounter_count',
], [
  [
    dataset.datasetId,
    dataset.version,
    dataset.generatedAt,
    dataset.baseDate,
    dataset.patients.length,
    dataset.appointments.length,
    dataset.encounters.length,
  ],
])

copyRows('facilities', ['id', 'code', 'name', 'phone', 'street', 'city', 'state', 'postal_code', 'color'],
  dataset.facilities.map((facility) => [
    facility.id,
    facility.code,
    facility.name,
    facility.phone,
    facility.street,
    facility.city,
    facility.state,
    facility.postalCode,
    facility.color,
  ]))

copyRows('staff', ['id', 'username', 'first_name', 'last_name', 'role', 'calendar', 'facility_id', 'email', 'npi', 'active'],
  dataset.staff.map((staff) => [
    staff.id,
    staff.username,
    staff.fname,
    staff.lname,
    staff.role,
    staff.calendar,
    staff.facilityId,
    `${staff.username}@example.test`,
    staff.role === 'provider' ? `18888${staff.id}` : null,
    true,
  ]))

copyRows('auth_accounts', ['username', 'display_name', 'role', 'staff_id', 'active', 'password_salt', 'password_hash'], [
  ['admin', 'Administrator', 'administrator', null, true, demoCredentialSalt, hashDemoPassword('pass')],
  ['gold-frontdesk-01', 'Parker Fleming', 'frontdesk', 117, true, demoCredentialSalt, hashDemoPassword('pass')],
  ['gold-provider-01', 'Alex Walker', 'provider', 101, true, demoCredentialSalt, hashDemoPassword('pass')],
])

copyRows('access_groups', ['id', 'value', 'name', 'parent_id'], accessGroups)

copyRows('access_permissions', ['section_value', 'value', 'name'], accessPermissions)

copyRows(
  'access_group_permissions',
  ['group_value', 'section_value', 'permission_value', 'permission_name', 'return_value'],
  accessGroupPermissions,
)

copyRows(
  'access_user_memberships',
  ['user_value', 'user_name', 'group_value', 'group_name', 'staff_id'],
  accessUserMemberships,
)

copyRows('patients', [
  'canonical_id',
  'legacy_pid',
  'pubpid',
  'first_name',
  'last_name',
  'preferred_name',
  'sex',
  'date_of_birth',
  'cohort',
  'purpose',
  'street',
  'city',
  'state',
  'postal_code',
  'email',
  'phone',
  'phone_home',
  'phone_cell',
  'hipaa_allow_sms',
  'hipaa_allow_email',
  'marital_status',
  'occupation',
  'race',
  'ethnicity',
  'interpreter',
  'family_size',
  'monthly_income',
  'homeless',
  'financial_review_date',
  'mother_name',
  'guardian_name',
  'guardian_relationship',
  'guardian_phone',
  'guardian_email',
  'guardian_sex',
  'guardian_address',
  'guardian_city',
  'guardian_state',
  'guardian_postal_code',
  'guardian_country',
  'guardian_work_phone',
  'provider_id',
  'facility_id',
  'portal_enabled',
  'cms_portal_login',
  'registration_date',
  'deceased_date',
  'deceased_reason',
], dataset.patients.map((patient) => [
  patient.canonicalId,
  patient.pid,
  patient.pubpid,
  patient.fname,
  patient.lname,
  patient.preferredName,
  patient.sex,
  patient.dob,
  patient.cohort,
  patient.purpose,
  patient.street,
  patient.city,
  patient.state,
  patient.postalCode,
  patient.email,
  patient.phone,
  patient.phone,
  patient.phone,
  'YES',
  'YES',
  patient.status,
  patient.occupation,
  patient.race,
  patient.ethnicity,
  patient.interpreter,
  patient.familySize,
  patient.monthlyIncome,
  patient.homeless,
  patient.financialReviewDate,
  patient.motherName,
  patient.guardianName,
  patient.guardianRelationship,
  patient.guardianPhone,
  patient.guardianEmail,
  patient.guardianSex,
  patient.guardianAddress,
  patient.guardianCity,
  patient.guardianState,
  patient.guardianPostalCode,
  patient.guardianCountry,
  patient.guardianWorkPhone,
  patient.providerId,
  patient.facilityId,
  patient.portalEnabled,
  patient.cmsPortalLogin,
  patient.registrationDate,
  null,
  null,
]))

copyRows('patient_portal_accounts', [
  'patient_id',
  'pid',
  'portal_username',
  'portal_login_username',
  'password_salt',
  'password_hash',
  'password_status',
  'one_time_token',
], dataset.patients.filter((patient) => patient.portalAccount).map((patient) => [
  patient.canonicalId,
  patient.pid,
  patient.portalAccount.portalUsername,
  patient.portalAccount.portalLoginUsername,
  demoCredentialSalt,
  hashDemoPassword(patient.portalAccount.portalPassword),
  patient.portalAccount.passwordStatus,
  patient.portalAccount.oneTimeToken,
]))

copyRows('patient_employers', [
  'patient_id',
  'pid',
  'name',
  'street',
  'city',
  'state',
  'postal_code',
  'country',
  'recorded_date',
], dataset.patients.map((patient) => [
  patient.canonicalId,
  patient.pid,
  patient.employerName,
  patient.employerStreet,
  patient.employerCity,
  patient.employerState,
  patient.employerPostalCode,
  patient.employerCountry,
  patient.registrationDate,
]))

copyRows('patient_histories', [
  'patient_id',
  'pid',
  'coffee',
  'tobacco',
  'alcohol',
  'sleep_patterns',
  'exercise_patterns',
  'seatbelt_use',
  'counseling',
  'hazardous_activities',
  'recreational_drugs',
  'last_physical_exam',
  'last_mammogram',
  'last_prostate_exam',
  'last_colonoscopy',
  'last_ecg',
  'last_retinal',
  'last_fluvax',
  'last_pneuvax',
  'last_ldl',
  'last_hemoglobin',
  'last_psa',
  'last_exam_results',
  'history_mother',
  'history_father',
  'history_siblings',
  'history_offspring',
  'history_spouse',
  'relatives_cancer',
  'relatives_tuberculosis',
  'relatives_diabetes',
  'relatives_high_blood_pressure',
  'relatives_heart_problems',
  'relatives_stroke',
  'relatives_epilepsy',
  'relatives_mental_illness',
  'relatives_suicide',
  'appendectomy_date',
  'tonsillectomy_date',
  'cholecystectomy_date',
  'heart_surgery_date',
  'hysterectomy_date',
  'hernia_repair_date',
  'hip_replacement_date',
  'knee_replacement_date',
  'additional_history',
  'exams',
  'recorded_at',
], dataset.patientHistories.map((history) => [
  history.patientId,
  history.pid,
  history.coffee,
  history.tobacco,
  history.alcohol,
  history.sleepPatterns,
  history.exercisePatterns,
  history.seatbeltUse,
  postgresTextDefault(history.counseling),
  history.hazardousActivities,
  history.recreationalDrugs,
  history.lastPhysicalExam,
  postgresTextDefault(history.lastMammogram),
  postgresTextDefault(history.lastProstateExam),
  history.lastColonoscopy,
  postgresTextDefault(history.lastEcg),
  postgresTextDefault(history.lastRetinal),
  history.lastFluvax,
  postgresTextDefault(history.lastPneuvax),
  history.lastLdl,
  history.lastHemoglobin,
  postgresTextDefault(history.lastPsa),
  history.lastExamResults,
  history.historyMother,
  history.historyFather,
  history.historySiblings,
  history.historyOffspring,
  postgresTextDefault(history.historySpouse),
  postgresTextDefault(history.relativesCancer),
  postgresTextDefault(history.relativesTuberculosis),
  history.relativesDiabetes,
  history.relativesHighBloodPressure,
  postgresTextDefault(history.relativesHeartProblems),
  postgresTextDefault(history.relativesStroke),
  postgresTextDefault(history.relativesEpilepsy),
  postgresTextDefault(history.relativesMentalIllness),
  postgresTextDefault(history.relativesSuicide),
  dateOnlyOrNull(history.appendectomy),
  dateOnlyOrNull(history.tonsillectomy),
  dateOnlyOrNull(history.cholecystectomy),
  dateOnlyOrNull(history.heartSurgery),
  dateOnlyOrNull(history.hysterectomy),
  dateOnlyOrNull(history.herniaRepair),
  dateOnlyOrNull(history.hipReplacement),
  dateOnlyOrNull(history.kneeReplacement),
  history.additionalHistory,
  history.exams,
  history.recordedDate,
]))

copyRows('patient_related_contacts', [
  'contact_id',
  'person_id',
  'patient_id',
  'pid',
  'display_name',
  'relationship',
  'phone',
  'email',
  'active',
], dataset.patients.map((patient) => [
  3200000 + (patient.pid - 100000),
  3100000 + (patient.pid - 100000),
  patient.canonicalId,
  patient.pid,
  patient.guardianName,
  patient.guardianRelationship,
  patient.guardianPhone,
  patient.guardianEmail,
  true,
]))

copyRows('insurance_records', [
  'id',
  'patient_id',
  'pid',
  'type',
  'provider',
  'plan_name',
  'policy_number',
  'group_number',
  'relationship',
  'subscriber_first_name',
  'subscriber_middle_name',
  'subscriber_last_name',
  'subscriber_date_of_birth',
  'subscriber_sex',
  'subscriber_street',
  'subscriber_street_line_2',
  'subscriber_city',
  'subscriber_state',
  'subscriber_postal_code',
  'subscriber_country',
  'subscriber_phone',
  'subscriber_employer',
  'subscriber_employer_street',
  'subscriber_employer_street_line_2',
  'subscriber_employer_city',
  'subscriber_employer_state',
  'subscriber_employer_postal_code',
  'subscriber_employer_country',
], dataset.insuranceRecords.map((insurance) => [
  insurance.id,
  insurance.patientId,
  insurance.pid,
  insurance.type,
  insurance.provider,
  insurance.planName,
  insurance.policyNumber,
  insurance.groupNumber,
  insurance.relationship,
  insurance.subscriberFirstName,
  insurance.subscriberMiddleName,
  insurance.subscriberLastName,
  dateOnlyOrNull(insurance.subscriberDateOfBirth),
  insurance.subscriberSex,
  insurance.subscriberStreet,
  insurance.subscriberStreetLine2,
  insurance.subscriberCity,
  insurance.subscriberState,
  insurance.subscriberPostalCode,
  insurance.subscriberCountry,
  insurance.subscriberPhone,
  insurance.subscriberEmployer,
  insurance.subscriberEmployerStreet,
  insurance.subscriberEmployerStreetLine2,
  insurance.subscriberEmployerCity,
  insurance.subscriberEmployerState,
  insurance.subscriberEmployerPostalCode,
  insurance.subscriberEmployerCountry,
]))

copyRows('appointments', [
  'id',
  'patient_id',
  'pid',
  'provider_id',
  'facility_id',
  'billing_location_id',
  'appointment_date',
  'start_time',
  'duration_minutes',
  'category_id',
  'title',
  'status',
  'room',
  'comments',
  'recurrence_type',
  'repeat_frequency',
  'repeat_unit',
  'repeat_on_num',
  'repeat_on_day',
  'repeat_on_frequency',
  'recurrence_end_date',
  'recurrence_days',
  'recurrence_exdates',
], dataset.appointments.map((appointment) => [
  appointment.id,
  appointment.patientId,
  appointment.pid,
  appointment.providerId,
  appointment.facilityId,
  appointment.billingLocationId ?? appointment.facilityId,
  appointment.date,
  appointment.start,
  appointment.duration,
  appointment.categoryId,
  appointment.title,
  appointment.status,
  appointment.room,
  appointment.comments ?? appointment.homeText ?? `Gold dataset appointment ${appointment.id}`,
  appointment.recurrenceType ?? 0,
  appointment.repeatFrequency ?? null,
  appointment.repeatUnit ?? null,
  appointment.repeatOnNum ?? null,
  appointment.repeatOnDay ?? null,
  appointment.repeatOnFrequency ?? null,
  appointment.recurrenceEndDate ?? null,
  (appointment.recurrenceDays ?? []).join(',') || null,
  (appointment.recurrenceExdates ?? []).join(',') || null,
]))

copyRows('encounters', [
  'id',
  'encounter',
  'patient_id',
  'pid',
  'provider_id',
  'facility_id',
  'billing_facility_id',
  'encounter_date',
  'encounter_datetime',
  'reason',
  'diagnosis_code',
  'diagnosis_text',
  'category_id',
  'sensitivity',
  'referral_source',
  'external_id',
  'pos_code',
  'billing_note',
], dataset.encounters.map((encounter) => [
  encounter.id,
  encounter.encounter,
  encounter.patientId,
  encounter.pid,
  encounter.providerId,
  encounter.facilityId,
  encounter.facilityId,
  encounter.date,
  encounter.datetime,
  encounter.reason,
  encounter.diagnosisCode,
  encounter.diagnosisText,
  encounter.categoryId,
  encounter.sensitivity,
  encounter.referralSource,
  encounter.externalId,
  encounter.posCode,
  encounter.billingNote,
]))

copyRows('vitals', [
  'id',
  'patient_id',
  'pid',
  'encounter',
  'vital_datetime',
  'bps',
  'bpd',
  'weight',
  'height',
  'temperature',
  'pulse',
  'respiration',
  'bmi',
  'oxygen_saturation',
  'note',
], dataset.vitals.map((vital) => [
  vital.id,
  vital.patientId,
  vital.pid,
  vital.encounter,
  vital.date,
  vital.bps,
  vital.bpd,
  vital.weight,
  vital.height,
  vital.temperature,
  vital.pulse,
  vital.respiration,
  vital.bmi,
  vital.oxygenSaturation,
  vital.note,
]))

copyRows('clinical_notes', [
  'id',
  'patient_id',
  'pid',
  'encounter',
  'note_datetime',
  'subjective',
  'objective',
  'assessment',
  'plan',
], dataset.clinicalNotes.map((note) => [
  note.id,
  note.patientId,
  note.pid,
  note.encounter,
  note.date,
  note.subjective,
  note.objective,
  note.assessment,
  note.plan,
]))

copyRows('pharmacies', [
  'id',
  'name',
  'transmit_method',
  'email',
  'ncpdp',
  'npi',
], [
  [9001, 'Northstar Community Pharmacy', 1, 'rx@northstar.example.test', 4501001, 1800001001],
  [9002, 'Summit Mail Order Pharmacy', 1, 'mailorder@summit.example.test', 4501002, 1800001002],
  [9003, 'Lakeside 24 Hour Pharmacy', 1, 'afterhours@lakeside.example.test', 4501003, 1800001003],
])

copyRows('prescriptions', [
  'id',
  'patient_id',
  'pid',
  'provider_id',
  'encounter',
  'start_date',
  'date_added',
  'modified_date',
  'end_date',
  'drug',
  'rx_norm_code',
  'dosage',
  'quantity',
  'route',
  'refills',
  'diagnosis',
  'note',
  'active',
  'pharmacy_id',
  'pharmacy_name',
  'pharmacy_ncpdp',
  'erx_uploaded',
  'erx_sent_at',
  'erx_payload',
], dataset.prescriptions.map((prescription) => [
  prescription.id,
  prescription.patientId,
  prescription.pid,
  prescription.providerId,
  prescription.encounter,
  prescription.startDate,
  prescription.dateAdded ?? `${prescription.startDate} 10:00:00`,
  prescription.modifiedDate ?? prescription.startDate,
  prescription.endDate,
  prescription.drug,
  prescription.rxNormCode ?? '',
  prescription.dosage,
  prescription.quantity ?? '30',
  prescription.route,
  prescription.refills ?? 2,
  prescription.diagnosis,
  prescription.note ?? 'Gold dataset prescription',
  prescription.active ?? 1,
  prescription.pharmacyId ?? '',
  prescription.pharmacyName ?? '',
  prescription.pharmacyNcpdp ?? '',
  prescription.erxUploaded ?? 0,
  prescription.erxSentAt ?? '',
  prescription.erxPayload ?? '',
]))

copyRows('immunizations', [
  'id',
  'key',
  'patient_id',
  'pid',
  'encounter',
  'immunization_id',
  'cvx_code',
  'vaccine',
  'administered_at',
  'manufacturer',
  'lot_number',
  'administered_by_id',
  'administered_by',
  'education_date',
  'vis_date',
  'amount_administered',
  'amount_administered_unit',
  'expiration_date',
  'route',
  'administration_site',
  'completion_status',
  'information_source',
  'note',
  'added_erroneously',
], dataset.immunizations.map((immunization) => [
  immunization.id,
  immunization.key,
  immunization.patientId,
  immunization.pid,
  immunization.encounter,
  immunization.immunizationId,
  immunization.cvxCode,
  immunization.vaccine,
  immunization.administeredDate,
  immunization.manufacturer,
  immunization.lotNumber,
  immunization.administeredById,
  immunization.administeredBy,
  immunization.educationDate,
  immunization.visDate,
  immunization.amountAdministered,
  immunization.amountAdministeredUnit,
  immunization.expirationDate,
  immunization.route,
  immunization.administrationSite,
  immunization.completionStatus,
  immunization.informationSource,
  immunization.note,
  0,
]))

copyRows('billing', [
  'id',
  'pid',
  'provider_id',
  'encounter',
  'billing_date',
  'code_type',
  'code',
  'modifier',
  'code_text',
  'fee',
  'justify',
  'units',
  'billed',
  'activity',
], dataset.billing.map((item) => [
  item.id,
  item.pid,
  item.providerId,
  item.encounter,
  item.date,
  item.codeType,
  item.code,
  item.modifier ?? '',
  item.codeText,
  item.fee,
  item.justify,
  item.units ?? 1,
  item.billed ?? 0,
  item.activity ?? 1,
]))

copyRows('claims', [
  'id',
  'patient_id',
  'pid',
  'encounter',
  'version',
  'payer_id',
  'payer_name',
  'payer_type',
  'status',
  'bill_process',
  'bill_time',
  'process_time',
  'process_file',
  'target',
  'x12_partner_id',
  'submitted_claim',
], dataset.claims.map((claim) => [
  claim.id,
  claim.patientId,
  claim.pid,
  claim.encounter,
  claim.version,
  claim.payerId,
  claim.payerName,
  claim.payerType,
  claim.status,
  claim.billProcess,
  claim.billTime,
  claim.processTime,
  claim.processFile,
  claim.target,
  claim.x12PartnerId,
  claim.submittedClaim,
]))

copyRows('payment_sessions', [
  'id',
  'patient_id',
  'pid',
  'payer_id',
  'payer_name',
  'user_id',
  'user_name',
  'closed',
  'reference',
  'check_date',
  'deposit_date',
  'pay_total',
  'created_time',
  'modified_time',
  'global_amount',
  'payment_type',
  'description',
  'adjustment_code',
  'post_to_date',
  'payment_method',
], dataset.paymentSessions.map((session) => [
  session.id,
  session.patientId,
  session.pid,
  session.payerId,
  session.payerName,
  session.userId,
  session.userName,
  session.closed,
  session.reference,
  session.checkDate,
  session.depositDate,
  session.payTotal,
  session.createdTime,
  session.modifiedTime,
  session.globalAmount,
  session.paymentType,
  session.description,
  session.adjustmentCode,
  session.postToDate,
  session.paymentMethod,
]))

copyRows('payment_activities', [
  'id',
  'session_id',
  'patient_id',
  'pid',
  'encounter',
  'sequence_no',
  'code_type',
  'code',
  'modifier',
  'payer_type',
  'post_time',
  'post_user_id',
  'post_user_name',
  'memo',
  'pay_amount',
  'adj_amount',
  'modified_time',
  'follow_up',
  'follow_up_note',
  'account_code',
  'reason_code',
  'deleted',
  'post_date',
  'payer_claim_number',
], dataset.paymentActivities.map((activity) => [
  activity.id,
  activity.sessionId,
  activity.patientId,
  activity.pid,
  activity.encounter,
  activity.sequenceNo,
  activity.codeType,
  activity.code,
  activity.modifier,
  activity.payerType,
  activity.postTime,
  activity.postUserId,
  activity.postUserName,
  activity.memo,
  activity.payAmount,
  activity.adjustmentAmount,
  activity.modifiedTime,
  activity.followUp,
  activity.followUpNote,
  activity.accountCode,
  activity.reasonCode,
  activity.deleted,
  activity.postDate,
  activity.payerClaimNumber,
]))

copyRows('lab_providers', [
  'id',
  'name',
  'lab_director_id',
  'npi',
  'protocol',
  'usage',
  'direction',
  'send_app_id',
  'send_fac_id',
  'recv_app_id',
  'recv_fac_id',
  'remote_host',
  'login',
  'password',
  'orders_path',
  'results_path',
  'notes',
  'active',
], (dataset.labProviders ?? []).map((provider) => [
  provider.id,
  provider.name,
  provider.labDirectorId ?? null,
  provider.npi ?? null,
  provider.protocol ?? 'DL',
  provider.usage ?? 'D',
  provider.direction ?? 'B',
  postgresTextDefault(provider.sendApplicationId),
  postgresTextDefault(provider.sendFacilityId),
  postgresTextDefault(provider.receiveApplicationId),
  postgresTextDefault(provider.receiveFacilityId),
  postgresTextDefault(provider.remoteHost),
  postgresTextDefault(provider.login),
  postgresTextDefault(provider.password),
  postgresTextDefault(provider.ordersPath),
  postgresTextDefault(provider.resultsPath),
  provider.notes ?? null,
  provider.active ?? true,
]))

copyRows('lab_order_catalog', [
  'id',
  'parent_id',
  'lab_id',
  'code',
  'name',
  'item_type',
  'procedure_type_name',
  'description',
  'specimen',
  'standard_code',
  'seq',
  'active',
], (dataset.procedureOrderCatalog ?? []).map((item) => [
  item.id,
  item.parentId ? item.parentId : null,
  item.labId ? item.labId : null,
  postgresTextDefault(item.code),
  item.name,
  item.itemType,
  postgresTextDefault(item.procedureTypeName),
  postgresTextDefault(item.description),
  postgresTextDefault(item.specimen),
  postgresTextDefault(item.standardCode),
  item.seq,
  item.active ?? true,
]))

copyRows('lab_orders', [
  'id',
  'patient_id',
  'pid',
  'encounter',
  'provider_id',
  'lab_id',
  'order_date',
  'order_priority',
  'code',
  'name',
  'procedure_type',
  'diagnosis',
  'instructions',
  'order_status',
  'date_transmitted',
], dataset.labOrders.map((order) => [
  order.id,
  order.patientId,
  order.pid,
  order.encounter,
  order.providerId,
  order.labId ?? null,
  order.date,
  order.orderPriority ?? 'routine',
  order.code,
  order.name,
  order.procedureType ?? 'laboratory',
  order.diagnosis,
  order.instructions ?? 'Gold dataset lab order',
  order.orderStatus,
  order.dateTransmitted ?? null,
]))

copyRows('lab_reports', [
  'id',
  'order_id',
  'date_collected',
  'report_date',
  'specimen_number',
  'status',
  'review_status',
  'reviewed_by',
  'reviewed_at',
  'notes',
], dataset.labReports.map((report) => {
  const reviewStatus = report.reviewStatus ?? (report.status === 'complete' ? 'reviewed' : 'pending')
  return [
    report.id,
    report.orderId,
    report.date,
    report.date,
    `SP-${report.id}`,
    report.status,
    reviewStatus,
    reviewStatus === 'reviewed' ? 'admin' : null,
    reviewStatus === 'reviewed' ? report.date : null,
    report.notes ?? 'Gold dataset result',
  ]
}))

copyRows('lab_results', [
  'id',
  'report_id',
  'code',
  'text',
  'units',
  'result',
  'range',
  'abnormal',
  'result_date',
  'result_status',
], dataset.labResults.map((result) => [
  result.id,
  result.reportId,
  result.code,
  result.text,
  result.units,
  result.result,
  result.range,
  result.abnormal,
  result.date,
  result.resultStatus,
]))

copyRows('messages', [
  'id',
  'patient_id',
  'pid',
  'message_date',
  'title',
  'body',
  'status',
  'assigned_to',
  'portal_relation',
  'is_encrypted',
  'updated_by',
  'updated_at',
  'deleted',
  'activity',
],
  dataset.messages.map((message) => [
    message.id,
    message.patientId,
    message.pid,
    message.date,
    message.title,
    message.body,
    message.status,
    message.assignedTo,
    message.portalRelation,
    Boolean(message.isEncrypted),
    message.updatedBy ?? null,
    message.updatedAt ?? null,
    0,
    1,
  ]))

copyRows('portal_mailbox_messages', [
  'id',
  'patient_id',
  'pid',
  'message_date',
  'body',
  'owner',
  'user_value',
  'group_name',
  'activity',
  'authorized',
  'title',
  'assigned_to',
  'message_status',
  'portal_relation',
  'mail_chain',
  'sender_id',
  'sender_name',
  'recipient_id',
  'recipient_name',
  'reply_mail_chain',
  'is_encrypted',
  'deleted',
],
  portalMailboxMessages.map(({ id, message, patient }) => [
    id,
    message.patientId,
    message.pid,
    message.date,
    message.body,
    patient.portalAccount.portalUsername,
    'admin',
    'Default',
    1,
    1,
    message.title,
    message.assignedTo,
    message.status,
    message.portalRelation,
    id,
    message.assignedTo,
    'Administrator',
    patient.portalAccount.portalUsername,
    `${patient.fname} ${patient.lname}`,
    id,
    Boolean(message.isEncrypted),
    0,
  ]))

copyRows('patient_documents', [
  'id',
  'document_key',
  'patient_id',
  'pid',
  'category_id',
  'category_name',
  'name',
  'doc_date',
  'uploaded_at',
  'mimetype',
  'file_name',
  'size_bytes',
  'pages',
  'encounter',
  'storage_method',
  'url',
  'hash',
  'documentation_of',
  'notes',
  'review_status',
  'reviewed_by',
  'reviewed_at',
  'content',
  'content_bytes',
  'deleted',
], dataset.patientDocuments.map((document) => [
  document.id,
  document.documentKey,
  document.patientId,
  document.pid,
  document.categoryId,
  document.categoryName,
  document.name,
  document.docDate,
  document.uploadedAt,
  document.mimetype,
  document.name.endsWith('.txt') ? document.name : `${document.name}.txt`,
  document.sizeBytes,
  document.pages,
  document.encounter,
  document.storageMethod,
  document.url,
  document.hash,
  document.documentationOf,
  document.notes,
  'pending',
  null,
  null,
  document.content,
  null,
  0,
]))

copyRows('problems', ['id', 'patient_id', 'pid', 'type', 'title', 'diagnosis', 'problem_date', 'comments', 'activity', 'end_date'],
  dataset.problems.map((problem) => [
    problem.id,
    problem.patientId,
    problem.pid,
    problem.type,
    problem.title,
    problem.diagnosis,
    problem.date,
    problem.comments,
    1,
    null,
  ]))

copyRows('allergies', ['id', 'patient_id', 'pid', 'type', 'title', 'reaction', 'severity', 'allergy_date', 'comments', 'activity', 'end_date', 'list_option_id'],
  dataset.allergies.map((allergy) => [
    allergy.id,
    allergy.patientId,
    allergy.pid,
    allergy.type,
    allergy.title,
    allergy.reaction,
    allergy.severity,
    allergy.date,
    allergy.comments,
    1,
    null,
    allergy.listOptionId,
  ]))

copyRows('medications', ['id', 'patient_id', 'pid', 'type', 'title', 'diagnosis', 'medication_date', 'modified_date', 'comments', 'activity', 'end_date'],
  dataset.medicationLists.map((medication) => [
    medication.id,
    medication.patientId,
    medication.pid,
    medication.type,
    medication.title,
    medication.diagnosis,
    medication.date,
    medication.modifiedDate ?? medication.date,
    medication.comments,
    1,
    null,
  ]))

lines.push(`
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
create index idx_lab_orders_pid on lab_orders (pid);
create index idx_lab_orders_lab_id on lab_orders (lab_id);
create index idx_lab_order_catalog_parent_id on lab_order_catalog (parent_id);
create index idx_lab_order_catalog_lab_id on lab_order_catalog (lab_id);
create index idx_lab_reports_date on lab_reports (report_date);
create index idx_lab_results_date on lab_results (result_date);
create index idx_messages_pid on messages (pid);
create index idx_portal_mailbox_owner_recipient on portal_mailbox_messages (owner, recipient_id, deleted);
create index idx_portal_mailbox_owner_sender on portal_mailbox_messages (owner, sender_id, deleted);
create index idx_patient_reminders_pid_active_created on patient_reminders (pid, active, date_created desc);
create index idx_patient_portal_report_audit_patient_created on patient_portal_report_audit_events (patient_id, created_at desc, id desc);
create index idx_patient_portal_report_audit_session on patient_portal_report_audit_events (session_id);
create index idx_patient_portal_message_audit_patient_created on patient_portal_message_audit_events (patient_id, created_at desc, id desc);
create index idx_patient_portal_message_audit_session on patient_portal_message_audit_events (session_id);
create index idx_patient_portal_message_audit_message on patient_portal_message_audit_events (message_id);
create index idx_patient_portal_profile_change_pending on patient_portal_profile_change_requests (patient_id, status, pending_action, created_at, id);
create index idx_patient_documents_pid_date on patient_documents (pid, doc_date);
create index idx_patient_documents_category on patient_documents (category_name);
create index idx_problems_pid on problems (pid);
create index idx_allergies_pid on allergies (pid);
create index idx_medications_pid on medications (pid);
create index idx_access_group_permissions_group on access_group_permissions (group_value);
create index idx_access_group_permissions_permission on access_group_permissions (section_value, permission_value);
create index idx_access_user_memberships_user on access_user_memberships (user_value);
create index idx_access_user_memberships_group on access_user_memberships (group_value);
commit;
`)

fs.writeFileSync(outputPath, `${lines.join('\n')}\n`)

const summaryPath = path.join(outputDir, 'seed-gold-summary.json')
fs.writeFileSync(summaryPath, JSON.stringify({
  generatedAt: new Date().toISOString(),
  datasetId: dataset.datasetId,
  version: dataset.version,
  outputPath,
  counts: {
    patients: dataset.patients.length,
    insuranceRecords: dataset.insuranceRecords.length,
    patientHistories: dataset.patientHistories.length,
    appointments: dataset.appointments.length,
    encounters: dataset.encounters.length,
    vitals: dataset.vitals.length,
    clinicalNotes: dataset.clinicalNotes.length,
    prescriptions: dataset.prescriptions.length,
    immunizations: dataset.immunizations.length,
    billing: dataset.billing.length,
    claims: dataset.claims.length,
    paymentSessions: dataset.paymentSessions.length,
    paymentActivities: dataset.paymentActivities.length,
    portalAccounts: dataset.patients.filter((patient) => patient.portalAccount).length,
    portalProfileChangeRequests: 0,
    labOrders: dataset.labOrders.length,
    labReports: dataset.labReports.length,
    labResults: dataset.labResults.length,
    messages: dataset.messages.length,
    portalMailboxMessages: portalMailboxMessages.length,
    patientDocuments: dataset.patientDocuments.length,
    problems: dataset.problems.length,
    allergies: dataset.allergies.length,
    medications: dataset.medicationLists.length,
    accessGroups: accessGroups.length,
    accessPermissions: accessPermissions.length,
    accessGroupPermissions: accessGroupPermissions.length,
    accessUserMemberships: accessUserMemberships.length,
  },
}, null, 2))

console.log(`Generated ${outputPath}`)

function copyRows(table, columns, rows) {
  lines.push(`copy ${table} (${columns.join(', ')}) from stdin;`)
  for (const row of rows) {
    lines.push(row.map(copyValue).join('\t'))
  }
  lines.push('\\.')
}

function copyValue(value) {
  if (value === copyEmptyString) {
    return ''
  }

  if (value === null || value === undefined || value === '') {
    return '\\N'
  }

  if (typeof value === 'boolean') {
    return value ? 'true' : 'false'
  }

  return String(value)
    .replaceAll('\\', '\\\\')
    .replaceAll('\t', '\\t')
    .replaceAll('\r', '\\r')
    .replaceAll('\n', '\\n')
}

function postgresTextDefault(value) {
  return value === null || value === undefined || value === '' ? copyEmptyString : value
}

function dateOnlyOrNull(value) {
  return value ? String(value).slice(0, 10) : null
}
