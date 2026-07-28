create table if not exists patient_disclosure_authorities (
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

create index if not exists ix_patient_disclosure_authorities_patient
  on patient_disclosure_authorities(patient_id, created_at desc);

create table if not exists patient_disclosure_authority_events (
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

create index if not exists ix_patient_disclosure_authority_events_authority
  on patient_disclosure_authority_events(authority_id, event_id desc);

create table if not exists patient_disclosure_requests (
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

create index if not exists ix_patient_disclosure_requests_patient
  on patient_disclosure_requests(patient_id, requested_at desc);

create table if not exists patient_disclosure_request_events (
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

create index if not exists ix_patient_disclosure_request_events_request
  on patient_disclosure_request_events(request_id, event_id desc);
