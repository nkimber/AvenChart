create table if not exists referrals (
  id uuid primary key,
  patient_id text not null references patients(canonical_id),
  encounter_id integer,
  destination text not null,
  reason text not null,
  status text not null,
  external_reference text,
  notes text,
  requested_at timestamptz not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table if not exists authorizations (
  id uuid primary key,
  patient_id text not null references patients(canonical_id),
  referral_id uuid references referrals(id),
  payer text not null,
  service text not null,
  status text not null,
  authorization_number text,
  requested_at timestamptz not null,
  expires_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

alter table authorizations
  add column if not exists workflow_version integer not null default 1,
  add column if not exists assigned_to text,
  add column if not exists due_at timestamptz,
  add column if not exists created_by text;

create index if not exists ix_authorizations_patient_status
  on authorizations(patient_id, status, requested_at desc);

create table if not exists clinical_workflow_events (
  event_id uuid primary key,
  workflow_type text not null,
  entity_id text not null,
  patient_id text,
  workflow_version integer not null,
  action text not null,
  from_state text,
  to_state text not null,
  from_assigned_to text,
  to_assigned_to text,
  reason_code text not null,
  reason text not null,
  actor text not null,
  policy_revision text not null,
  occurred_at timestamptz not null
);

create index if not exists ix_clinical_workflow_events_entity
  on clinical_workflow_events(workflow_type, entity_id, workflow_version desc);

create index if not exists ix_clinical_workflow_events_patient
  on clinical_workflow_events(patient_id, occurred_at desc);
