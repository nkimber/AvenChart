import fs from 'node:fs'
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
  ['admin', 'Administrator', 'admin', 'Administrators'],
  ['oe-system', 'System Operation User', 'admin', 'Administrators'],
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
drop table if exists messages;
drop table if exists lab_results;
drop table if exists lab_reports;
drop table if exists lab_specimens;
drop table if exists lab_orders;
drop table if exists lab_providers;
drop table if exists encounter_signatures;
drop table if exists payment_activities;
drop table if exists payment_sessions;
drop table if exists claims;
drop table if exists billing;
drop table if exists immunizations;
drop table if exists prescriptions;
drop table if exists clinical_notes;
drop table if exists vitals;
drop table if exists encounters;
drop table if exists appointments;
drop table if exists insurance_records;
drop table if exists patients;
drop table if exists access_user_memberships;
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
  provider_id integer references staff(id),
  facility_id integer references facilities(id),
  portal_enabled boolean not null,
  registration_date date not null
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
  relationship text
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
  billing_note text
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

create table prescriptions (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  provider_id integer references staff(id),
  encounter integer,
  start_date date,
  end_date date,
  drug text not null,
  rx_norm_code text,
  dosage text,
  quantity text,
  route text,
  refills integer not null default 0,
  diagnosis text,
  note text,
  active integer not null default 1
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
  order_status text
);

create table lab_providers (
  id integer primary key,
  name text not null,
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
  deleted integer not null default 0,
  activity integer not null default 1
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
  accessUserMemberships.map(([userValue, userName, groupValue, groupName]) => [userValue, userName, groupValue, groupName, null]),
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
  'provider_id',
  'facility_id',
  'portal_enabled',
  'registration_date',
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
  patient.providerId,
  patient.facilityId,
  patient.portalEnabled,
  patient.registrationDate,
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

copyRows('prescriptions', [
  'id',
  'patient_id',
  'pid',
  'provider_id',
  'encounter',
  'start_date',
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
], dataset.prescriptions.map((prescription) => [
  prescription.id,
  prescription.patientId,
  prescription.pid,
  prescription.providerId,
  prescription.encounter,
  prescription.startDate,
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

copyRows('messages', ['id', 'patient_id', 'pid', 'message_date', 'title', 'body', 'status', 'assigned_to', 'deleted', 'activity'],
  dataset.messages.map((message) => [
    message.id,
    message.patientId,
    message.pid,
    message.date,
    message.title,
    message.body,
    message.status,
    message.assignedTo,
    0,
    1,
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

copyRows('medications', ['id', 'patient_id', 'pid', 'type', 'title', 'diagnosis', 'medication_date', 'comments', 'activity', 'end_date'],
  dataset.medicationLists.map((medication) => [
    medication.id,
    medication.patientId,
    medication.pid,
    medication.type,
    medication.title,
    medication.diagnosis,
    medication.date,
    medication.comments,
    1,
    null,
  ]))

lines.push(`
create index idx_patients_name on patients (last_name, first_name);
create index idx_patients_legacy_pid on patients (legacy_pid);
create index idx_insurance_records_pid on insurance_records (pid);
create index idx_appointments_pid_date on appointments (pid, appointment_date, start_time);
create index idx_encounters_pid_date on encounters (pid, encounter_date);
create index idx_encounter_signatures_encounter on encounter_signatures (encounter, signed_at);
create index idx_vitals_pid_date on vitals (pid, vital_datetime);
create index idx_clinical_notes_pid_date on clinical_notes (pid, note_datetime);
create index idx_prescriptions_pid on prescriptions (pid);
create index idx_immunizations_pid_date on immunizations (pid, administered_at);
create index idx_billing_pid on billing (pid);
create index idx_payment_sessions_pid on payment_sessions (pid);
create index idx_payment_activities_pid_encounter on payment_activities (pid, encounter);
create index idx_lab_orders_pid on lab_orders (pid);
create index idx_lab_orders_lab_id on lab_orders (lab_id);
create index idx_lab_reports_date on lab_reports (report_date);
create index idx_lab_results_date on lab_results (result_date);
create index idx_messages_pid on messages (pid);
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
    labOrders: dataset.labOrders.length,
    labReports: dataset.labReports.length,
    labResults: dataset.labResults.length,
    messages: dataset.messages.length,
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
