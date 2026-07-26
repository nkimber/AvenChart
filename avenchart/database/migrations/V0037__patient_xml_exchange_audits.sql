create table if not exists patient_xml_exchange_audits (
  id uuid primary key,
  patient_id text not null references patients(canonical_id),
  imported_at timestamptz not null default now(),
  imported_by text not null,
  prior_values jsonb not null,
  payload_sha256 text not null,
  rolled_back_at timestamptz,
  rolled_back_by text
);
