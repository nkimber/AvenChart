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

const lines = []
lines.push(`-- Generated from ${dataset.datasetId} ${dataset.version}`)
lines.push('set client_min_messages to warning;')
lines.push('begin;')
lines.push(`
drop table if exists medications;
drop table if exists allergies;
drop table if exists problems;
drop table if exists messages;
drop table if exists lab_results;
drop table if exists lab_reports;
drop table if exists lab_orders;
drop table if exists billing;
drop table if exists prescriptions;
drop table if exists clinical_notes;
drop table if exists vitals;
drop table if exists encounters;
drop table if exists appointments;
drop table if exists insurance_records;
drop table if exists patients;
drop table if exists staff;
drop table if exists facilities;
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
  color text
);

create table staff (
  id integer primary key,
  username text not null unique,
  first_name text not null,
  last_name text not null,
  role text not null,
  calendar boolean not null,
  facility_id integer references facilities(id)
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
  appointment_date date not null,
  start_time time not null,
  duration_minutes integer not null,
  category_id integer,
  title text,
  status text,
  room text
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
  billing_note text
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

create table billing (
  id text primary key,
  pid integer not null,
  provider_id integer references staff(id),
  encounter integer,
  billing_date date not null,
  code_type text,
  code text,
  code_text text,
  fee numeric(10,2),
  justify text,
  units integer not null default 1,
  billed integer not null default 0,
  activity integer not null default 1
);

create table lab_orders (
  id integer primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  encounter integer,
  provider_id integer references staff(id),
  order_date date not null,
  code text,
  name text,
  diagnosis text,
  order_status text
);

create table lab_reports (
  id integer primary key,
  order_id integer not null references lab_orders(id),
  report_date timestamp not null,
  status text
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

create table problems (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  type text,
  title text,
  diagnosis text,
  problem_date date,
  comments text
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
  comments text
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

copyRows('staff', ['id', 'username', 'first_name', 'last_name', 'role', 'calendar', 'facility_id'],
  dataset.staff.map((staff) => [
    staff.id,
    staff.username,
    staff.fname,
    staff.lname,
    staff.role,
    staff.calendar,
    staff.facilityId,
  ]))

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
  'appointment_date',
  'start_time',
  'duration_minutes',
  'category_id',
  'title',
  'status',
  'room',
], dataset.appointments.map((appointment) => [
  appointment.id,
  appointment.patientId,
  appointment.pid,
  appointment.providerId,
  appointment.facilityId,
  appointment.date,
  appointment.start,
  appointment.duration,
  appointment.categoryId,
  appointment.title,
  appointment.status,
  appointment.room,
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

copyRows('billing', [
  'id',
  'pid',
  'provider_id',
  'encounter',
  'billing_date',
  'code_type',
  'code',
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
  item.codeText,
  item.fee,
  item.justify,
  item.units ?? 1,
  item.billed ?? 0,
  item.activity ?? 1,
]))

copyRows('lab_orders', [
  'id',
  'patient_id',
  'pid',
  'encounter',
  'provider_id',
  'order_date',
  'code',
  'name',
  'diagnosis',
  'order_status',
], dataset.labOrders.map((order) => [
  order.id,
  order.patientId,
  order.pid,
  order.encounter,
  order.providerId,
  order.date,
  order.code,
  order.name,
  order.diagnosis,
  order.orderStatus,
]))

copyRows('lab_reports', [
  'id',
  'order_id',
  'report_date',
  'status',
], dataset.labReports.map((report) => [
  report.id,
  report.orderId,
  report.date,
  report.status,
]))

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

copyRows('problems', ['id', 'patient_id', 'pid', 'type', 'title', 'diagnosis', 'problem_date', 'comments'],
  dataset.problems.map((problem) => [
    problem.id,
    problem.patientId,
    problem.pid,
    problem.type,
    problem.title,
    problem.diagnosis,
    problem.date,
    problem.comments,
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

copyRows('medications', ['id', 'patient_id', 'pid', 'type', 'title', 'diagnosis', 'medication_date', 'comments'],
  dataset.medicationLists.map((medication) => [
    medication.id,
    medication.patientId,
    medication.pid,
    medication.type,
    medication.title,
    medication.diagnosis,
    medication.date,
    medication.comments,
  ]))

lines.push(`
create index idx_patients_name on patients (last_name, first_name);
create index idx_patients_legacy_pid on patients (legacy_pid);
create index idx_insurance_records_pid on insurance_records (pid);
create index idx_appointments_pid_date on appointments (pid, appointment_date, start_time);
create index idx_encounters_pid_date on encounters (pid, encounter_date);
create index idx_vitals_pid_date on vitals (pid, vital_datetime);
create index idx_clinical_notes_pid_date on clinical_notes (pid, note_datetime);
create index idx_prescriptions_pid on prescriptions (pid);
create index idx_billing_pid on billing (pid);
create index idx_lab_orders_pid on lab_orders (pid);
create index idx_lab_reports_date on lab_reports (report_date);
create index idx_lab_results_date on lab_results (result_date);
create index idx_messages_pid on messages (pid);
create index idx_problems_pid on problems (pid);
create index idx_allergies_pid on allergies (pid);
create index idx_medications_pid on medications (pid);
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
    billing: dataset.billing.length,
    labOrders: dataset.labOrders.length,
    labReports: dataset.labReports.length,
    labResults: dataset.labResults.length,
    messages: dataset.messages.length,
    problems: dataset.problems.length,
    allergies: dataset.allergies.length,
    medications: dataset.medicationLists.length,
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
