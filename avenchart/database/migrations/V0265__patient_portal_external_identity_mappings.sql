-- SEC-04: Portal OIDC identities must be explicitly bound to one patient
-- record. A provider subject may never select a patient through a browser
-- request, and a revoked mapping must invalidate derived portal sessions.
create table if not exists patient_portal_external_identity_mappings (
  mapping_id uuid primary key,
  provider_id text not null check (
    provider_id = lower(provider_id)
    and length(provider_id) between 2 and 80
    and provider_id ~ '^[a-z0-9][a-z0-9._-]*[a-z0-9]$'
  ),
  external_subject text not null check (
    length(external_subject) between 1 and 512
    and external_subject = btrim(external_subject)
    and external_subject !~ '[[:cntrl:]]'
  ),
  patient_id text not null references patients(canonical_id) on delete restrict,
  active boolean not null default true,
  created_at timestamptz not null default now(),
  created_by text not null check (length(btrim(created_by)) between 1 and 120),
  deactivated_at timestamptz,
  deactivated_by text,
  deactivation_reason text,
  check (
    (active and deactivated_at is null and deactivated_by is null and deactivation_reason is null)
    or (not active and deactivated_at is not null and deactivated_by is not null and deactivation_reason is not null)
  )
);

create unique index if not exists ux_patient_portal_external_identity_mappings_active_subject
  on patient_portal_external_identity_mappings(provider_id, external_subject)
  where active;

create unique index if not exists ux_patient_portal_external_identity_mappings_active_patient
  on patient_portal_external_identity_mappings(provider_id, patient_id)
  where active;

create index if not exists ix_patient_portal_external_identity_mappings_patient
  on patient_portal_external_identity_mappings(patient_id, provider_id, created_at desc);

create table if not exists patient_portal_external_identity_mapping_events (
  event_id uuid primary key,
  mapping_id uuid not null references patient_portal_external_identity_mappings(mapping_id) on delete restrict,
  action text not null check (action in ('created', 'deactivated')),
  actor text not null check (length(btrim(actor)) between 1 and 120),
  reason text,
  occurred_at timestamptz not null default now()
);

create index if not exists ix_patient_portal_external_identity_mapping_events_mapping_time
  on patient_portal_external_identity_mapping_events(mapping_id, occurred_at desc, event_id desc);

create or replace function avenchart_prevent_patient_portal_external_identity_mapping_event_mutation()
returns trigger
language plpgsql
as $$
begin
  raise exception using
    errcode = '55000',
    message = 'Patient portal external identity mapping events are immutable and cannot be altered or deleted.';
end;
$$;

drop trigger if exists trg_patient_portal_external_identity_mapping_events_immutable on patient_portal_external_identity_mapping_events;
create trigger trg_patient_portal_external_identity_mapping_events_immutable
before update or delete on patient_portal_external_identity_mapping_events
for each row execute function avenchart_prevent_patient_portal_external_identity_mapping_event_mutation();

alter table patient_portal_sessions
  add column if not exists external_identity_mapping_id uuid
    references patient_portal_external_identity_mappings(mapping_id) on delete restrict,
  add column if not exists external_token_fingerprint bytea;

alter table patient_portal_sessions
  drop constraint if exists chk_patient_portal_sessions_external_identity;

alter table patient_portal_sessions
  add constraint chk_patient_portal_sessions_external_identity
  check (
    (external_identity_mapping_id is null and external_token_fingerprint is null)
    or (external_identity_mapping_id is not null and octet_length(external_token_fingerprint) = 32)
  );

create index if not exists ix_patient_portal_sessions_external_identity_token
  on patient_portal_sessions(external_identity_mapping_id, external_token_fingerprint, expires_at desc)
  where external_identity_mapping_id is not null;
