-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- One immutable, non-transmitting structural receipt for the synthetic 837P
-- adapter seam. It contains no claim payload and creates no financial record.

create table if not exists telehealth_professional_claim_preparations (
  claim_preparation_id uuid primary key,
  consultation_id uuid not null unique references telehealth_consultation_contexts(consultation_id),
  encounter_id integer not null references encounters(encounter),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  physician_staff_id integer not null references staff(id),
  documentation_version integer not null check (documentation_version > 0),
  disposition_version integer not null check (disposition_version > 0),
  final_clinical_review_version integer not null check (final_clinical_review_version > 0),
  canonical_claim_version text not null check (canonical_claim_version = 'telehealth-claim-v1'),
  source_evidence_hash text not null check (length(source_evidence_hash) = 64),
  adapter_mode text not null check (adapter_mode = 'NON_PRODUCTION'),
  adapter_name text not null,
  target_standard text not null check (target_standard = 'ASC_X12N_837P_005010X222A1'),
  claim_state text not null check (claim_state = 'PreparedOnly'),
  correlation_reference text not null check (length(correlation_reference) = 64),
  synthetic_data_confirmed boolean not null,
  transaction_created boolean not null default false,
  external_destination_contacted boolean not null default false,
  submission_accepted boolean not null default false,
  actor_subject_hash text not null,
  idempotency_key text not null check (length(idempotency_key) between 8 and 128),
  command_fingerprint text not null check (length(command_fingerprint) = 64),
  prepared_at timestamptz not null default now(),
  constraint chk_telehealth_claim_preparation_synthetic check (
    synthetic_data_confirmed
    and not transaction_created
    and not external_destination_contacted
    and not submission_accepted),
  constraint uq_telehealth_claim_preparation_idempotency unique (consultation_id, actor_subject_hash, idempotency_key)
);

create index if not exists ix_telehealth_claim_preparation_physician
  on telehealth_professional_claim_preparations(practice_id, facility_id, physician_staff_id, prepared_at desc);

drop trigger if exists trg_telehealth_claim_preparation_append_only on telehealth_professional_claim_preparations;
create trigger trg_telehealth_claim_preparation_append_only
before update or delete on telehealth_professional_claim_preparations
for each row execute function reject_telehealth_evidence_mutation();
