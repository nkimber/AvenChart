-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default established-patient readiness evidence authorized by
-- Decision 0005. All adapter evidence is constrained to NON_PRODUCTION.

alter table telehealth_requests
  drop constraint if exists chk_telehealth_requests_status;
alter table telehealth_requests
  add constraint chk_telehealth_requests_status
  check (status in ('Draft','LocationConfirmed','Intake','Verification','OperationalReview','Redirected','Queued','Reserved'));

create unique index if not exists uq_telehealth_request_patient
  on telehealth_requests(request_id, patient_id);
create unique index if not exists uq_insurance_record_patient
  on insurance_records(id, patient_id);

create table if not exists telehealth_patient_confirmations (
  confirmation_id uuid primary key,
  request_id uuid not null,
  patient_id text not null,
  demographics_fingerprint character(64) not null,
  clinical_summary_fingerprint character(64) not null,
  demographics_confirmed boolean not null,
  contact_confirmed boolean not null,
  clinical_summary_confirmed boolean not null,
  request_version bigint not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  attested_at timestamptz not null default now(),
  constraint fk_telehealth_confirmation_request_patient
    foreign key (request_id, patient_id)
    references telehealth_requests(request_id, patient_id),
  constraint chk_telehealth_confirmation_affirmative
    check (demographics_confirmed and contact_confirmed and clinical_summary_confirmed),
  constraint chk_telehealth_confirmation_version check (request_version >= 4),
  constraint chk_telehealth_confirmation_fingerprints check (
    demographics_fingerprint ~ '^[0-9a-f]{64}$'
    and clinical_summary_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint uq_telehealth_confirmation_idempotency unique (request_id, idempotency_key)
);

create table if not exists telehealth_intake_snapshots (
  intake_id uuid primary key,
  request_id uuid not null references telehealth_requests(request_id),
  complaint_summary text not null,
  symptom_duration text not null,
  synthetic_data_confirmed boolean not null,
  request_version bigint not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  captured_at timestamptz not null default now(),
  constraint chk_telehealth_intake_summary check (length(trim(complaint_summary)) between 10 and 500),
  constraint chk_telehealth_intake_duration check (
    symptom_duration in ('less-than-day','1-3-days','4-14-days','more-than-14-days')),
  constraint chk_telehealth_intake_synthetic check (synthetic_data_confirmed = true),
  constraint chk_telehealth_intake_version check (request_version >= 4),
  constraint chk_telehealth_intake_fingerprint check (command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint uq_telehealth_intake_idempotency unique (request_id, idempotency_key)
);

create table if not exists telehealth_demonstration_acknowledgments (
  acknowledgment_id uuid primary key,
  request_id uuid not null references telehealth_requests(request_id),
  acknowledgment_kind text not null,
  package_key text not null,
  package_version integer not null,
  content_hash character(64) not null,
  accepted boolean not null,
  legal_effect boolean not null default false,
  request_version bigint not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  accepted_at timestamptz not null default now(),
  constraint chk_telehealth_ack_kind
    check (acknowledgment_kind = 'SyntheticDemonstrationAcknowledgment'),
  constraint chk_telehealth_ack_package check (length(trim(package_key)) between 3 and 128 and package_version > 0),
  constraint chk_telehealth_ack_acceptance check (accepted = true and legal_effect = false),
  constraint chk_telehealth_ack_version check (request_version >= 4),
  constraint chk_telehealth_ack_fingerprints check (
    content_hash ~ '^[0-9a-f]{64}$' and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint uq_telehealth_ack_idempotency unique (request_id, idempotency_key)
);

create table if not exists telehealth_coverage_selections (
  selection_id uuid primary key,
  request_id uuid not null,
  patient_id text not null,
  insurance_record_id text not null,
  source_record_fingerprint character(64) not null,
  patient_confirmed boolean not null,
  request_version bigint not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  selected_at timestamptz not null default now(),
  constraint fk_telehealth_coverage_selection_request_patient
    foreign key (request_id, patient_id)
    references telehealth_requests(request_id, patient_id),
  constraint fk_telehealth_coverage_selection_insurance_patient
    foreign key (insurance_record_id, patient_id)
    references insurance_records(id, patient_id),
  constraint chk_telehealth_coverage_selection_confirmed check (patient_confirmed = true),
  constraint chk_telehealth_coverage_selection_version check (request_version >= 4),
  constraint chk_telehealth_coverage_selection_fingerprints check (
    source_record_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint uq_telehealth_coverage_selection_idempotency unique (request_id, idempotency_key)
);

create table if not exists telehealth_coverage_verifications (
  verification_id uuid primary key,
  request_id uuid not null references telehealth_requests(request_id),
  selection_id uuid not null references telehealth_coverage_selections(selection_id),
  adapter_mode text not null,
  eligibility_status text not null,
  network_status text not null,
  financial_route text not null,
  eligibility_source text not null,
  network_source text not null,
  evidence_key text not null,
  evidence_version integer not null,
  input_fingerprint character(64) not null,
  limitations text[] not null,
  verified_at timestamptz not null default now(),
  expires_at timestamptz not null,
  request_version bigint not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  constraint chk_telehealth_verification_adapter check (adapter_mode = 'NON_PRODUCTION'),
  constraint chk_telehealth_verification_eligibility check (eligibility_status in ('Active','Inactive','Unknown')),
  constraint chk_telehealth_verification_network check (network_status in ('ConfirmedInNetwork','OutOfNetwork','Unknown')),
  constraint chk_telehealth_verification_route check (financial_route in (
    'ConfirmedInNetwork','CoverageActiveNetworkPending','OutOfNetworkOrSelfPay','UnableToVerify','CoverageInactive')),
  constraint chk_telehealth_verification_evidence check (
    length(trim(eligibility_source)) between 3 and 128
    and length(trim(network_source)) between 3 and 128
    and length(trim(evidence_key)) between 3 and 128
    and evidence_version > 0),
  constraint chk_telehealth_verification_freshness check (expires_at > verified_at),
  constraint chk_telehealth_verification_version check (request_version >= 5),
  constraint chk_telehealth_verification_fingerprints check (
    input_fingerprint ~ '^[0-9a-f]{64}$' and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint uq_telehealth_verification_idempotency unique (request_id, idempotency_key)
);

create index if not exists ix_telehealth_coverage_selection_request
  on telehealth_coverage_selections(request_id, selected_at desc);
create index if not exists ix_telehealth_coverage_verification_request
  on telehealth_coverage_verifications(request_id, verified_at desc);
create index if not exists ix_telehealth_patient_verification
  on telehealth_requests(practice_id, patient_id, updated_at desc, request_id)
  where status = 'Verification';

drop trigger if exists trg_telehealth_patient_confirmations_append_only on telehealth_patient_confirmations;
create trigger trg_telehealth_patient_confirmations_append_only
before update or delete on telehealth_patient_confirmations
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_intake_snapshots_append_only on telehealth_intake_snapshots;
create trigger trg_telehealth_intake_snapshots_append_only
before update or delete on telehealth_intake_snapshots
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_demonstration_acknowledgments_append_only on telehealth_demonstration_acknowledgments;
create trigger trg_telehealth_demonstration_acknowledgments_append_only
before update or delete on telehealth_demonstration_acknowledgments
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_coverage_selections_append_only on telehealth_coverage_selections;
create trigger trg_telehealth_coverage_selections_append_only
before update or delete on telehealth_coverage_selections
for each row execute function reject_telehealth_evidence_mutation();
drop trigger if exists trg_telehealth_coverage_verifications_append_only on telehealth_coverage_verifications;
create trigger trg_telehealth_coverage_verifications_append_only
before update or delete on telehealth_coverage_verifications
for each row execute function reject_telehealth_evidence_mutation();

