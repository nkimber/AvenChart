-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0062: immutable, source-version-bound clinician review evidence.
-- This is explicitly not an encounter signature, completion, bill, or claim.

create table if not exists telehealth_consultation_final_clinical_review_versions (
  final_clinical_review_version_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  encounter_id integer not null references encounters(encounter),
  version integer not null,
  documentation_version integer not null,
  disposition_version integer not null,
  prescription_order_id uuid references telehealth_consultation_prescription_orders(order_id),
  documentation_reviewed boolean not null,
  physician_responsibility_confirmed boolean not null,
  no_automatic_claim_or_delivery_confirmed boolean not null,
  synthetic_data_confirmed boolean not null,
  reviewed_at timestamptz not null default now(),
  reviewed_by_staff_id integer not null references staff(id),
  content_hash character(64) not null,
  legal_effect boolean not null default false,
  encounter_signature_created boolean not null default false,
  completion_created boolean not null default false,
  patient_delivery_created boolean not null default false,
  billing_created boolean not null default false,
  claim_created boolean not null default false,
  external_destination_contacted boolean not null default false,
  constraint uq_telehealth_final_clinical_review_version unique (consultation_id,version),
  constraint chk_telehealth_final_clinical_review_version check (
    version >= 1 and documentation_version >= 1 and disposition_version >= 1),
  constraint chk_telehealth_final_clinical_review_attestations check (
    documentation_reviewed and physician_responsibility_confirmed
    and no_automatic_claim_or_delivery_confirmed and synthetic_data_confirmed),
  constraint chk_telehealth_final_clinical_review_hash check (content_hash ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_final_clinical_review_no_effect check (
    not legal_effect and not encounter_signature_created and not completion_created
    and not patient_delivery_created and not billing_created and not claim_created
    and not external_destination_contacted)
);

create index if not exists idx_telehealth_final_clinical_review_current
  on telehealth_consultation_final_clinical_review_versions(
    consultation_id,documentation_version,disposition_version,reviewed_at desc);

create table if not exists telehealth_consultation_final_clinical_review_events (
  event_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  final_clinical_review_version_id uuid not null
    references telehealth_consultation_final_clinical_review_versions(final_clinical_review_version_id),
  aggregate_version integer not null,
  action text not null,
  actor_type text not null,
  actor_id text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint uq_telehealth_final_clinical_review_event_version unique (consultation_id,aggregate_version),
  constraint uq_telehealth_final_clinical_review_event_idempotency unique (consultation_id,idempotency_key),
  constraint chk_telehealth_final_clinical_review_event check (
    aggregate_version >= 1 and action in ('FinalClinicalReviewRecorded')
    and actor_type='physician' and length(trim(actor_id)) between 1 and 128
    and length(idempotency_key) between 8 and 128 and command_fingerprint ~ '^[0-9a-f]{64}$')
);

drop trigger if exists trg_telehealth_final_clinical_review_versions_append_only
  on telehealth_consultation_final_clinical_review_versions;
create trigger trg_telehealth_final_clinical_review_versions_append_only
before update or delete on telehealth_consultation_final_clinical_review_versions
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_final_clinical_review_events_append_only
  on telehealth_consultation_final_clinical_review_events;
create trigger trg_telehealth_final_clinical_review_events_append_only
before update or delete on telehealth_consultation_final_clinical_review_events
for each row execute function reject_telehealth_evidence_mutation();
