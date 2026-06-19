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
drop table if exists lab_orders;
drop table if exists billing;
drop table if exists prescriptions;
drop table if exists encounters;
drop table if exists appointments;
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
  marital_status text,
  occupation text,
  provider_id integer references staff(id),
  facility_id integer references facilities(id),
  portal_enabled boolean not null,
  registration_date date not null
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
  encounter_date date not null,
  encounter_datetime timestamp not null,
  reason text,
  diagnosis_code text,
  diagnosis_text text,
  category_id integer
);

create table prescriptions (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  provider_id integer references staff(id),
  encounter integer,
  start_date date,
  drug text not null,
  dosage text,
  route text,
  diagnosis text
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
  justify text
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

create table messages (
  id text primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  message_date date not null,
  title text,
  body text,
  status text
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
  comments text
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
  patient.status,
  patient.occupation,
  patient.providerId,
  patient.facilityId,
  patient.portalEnabled,
  patient.registrationDate,
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
  'encounter_date',
  'encounter_datetime',
  'reason',
  'diagnosis_code',
  'diagnosis_text',
  'category_id',
], dataset.encounters.map((encounter) => [
  encounter.id,
  encounter.encounter,
  encounter.patientId,
  encounter.pid,
  encounter.providerId,
  encounter.facilityId,
  encounter.date,
  encounter.datetime,
  encounter.reason,
  encounter.diagnosisCode,
  encounter.diagnosisText,
  encounter.categoryId,
]))

copyRows('prescriptions', [
  'id',
  'patient_id',
  'pid',
  'provider_id',
  'encounter',
  'start_date',
  'drug',
  'dosage',
  'route',
  'diagnosis',
], dataset.prescriptions.map((prescription) => [
  prescription.id,
  prescription.patientId,
  prescription.pid,
  prescription.providerId,
  prescription.encounter,
  prescription.startDate,
  prescription.drug,
  prescription.dosage,
  prescription.route,
  prescription.diagnosis,
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

copyRows('messages', ['id', 'patient_id', 'pid', 'message_date', 'title', 'body', 'status'],
  dataset.messages.map((message) => [
    message.id,
    message.patientId,
    message.pid,
    message.date,
    message.title,
    message.body,
    message.status,
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

copyRows('allergies', ['id', 'patient_id', 'pid', 'type', 'title', 'reaction', 'severity', 'allergy_date', 'comments'],
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
create index idx_appointments_pid_date on appointments (pid, appointment_date, start_time);
create index idx_encounters_pid_date on encounters (pid, encounter_date);
create index idx_prescriptions_pid on prescriptions (pid);
create index idx_billing_pid on billing (pid);
create index idx_lab_orders_pid on lab_orders (pid);
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
    appointments: dataset.appointments.length,
    encounters: dataset.encounters.length,
    prescriptions: dataset.prescriptions.length,
    billing: dataset.billing.length,
    labOrders: dataset.labOrders.length,
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
