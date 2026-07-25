create table if not exists patient_record_requests (
  request_id uuid primary key,
  patient_id text not null references patients(canonical_id),
  pid integer not null,
  requested_at timestamptz not null,
  requested_by text not null,
  completed_at timestamptz,
  completed_by text
);

create unique index if not exists ux_patient_record_requests_one_open_per_patient
  on patient_record_requests(patient_id)
  where completed_at is null;

create index if not exists ix_patient_record_requests_patient_history
  on patient_record_requests(patient_id, requested_at desc);
