-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- One immutable, minimized patient-facing receipt created with synthetic
-- lifecycle closure. It is not a clinical after-visit summary or delivery.

create table if not exists telehealth_synthetic_post_visit_receipts (
  receipt_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  consultation_id uuid not null unique references telehealth_consultation_contexts(consultation_id),
  encounter_id integer not null references encounters(encounter),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  patient_id text not null references patients(canonical_id),
  consultation_version integer not null check (consultation_version > 0),
  request_version integer not null check (request_version > 0),
  source_evidence_hash text not null check (length(source_evidence_hash) = 64),
  receipt_version integer not null default 1 check (receipt_version = 1),
  receipt_state text not null check (receipt_state = 'AvailableInPortal'),
  source_mode text not null check (source_mode = 'NON_PRODUCTION'),
  synthetic_data_confirmed boolean not null,
  appointment_completed boolean not null default false,
  encounter_completed boolean not null default false,
  clinical_record_delivered boolean not null default false,
  prescription_delivered boolean not null default false,
  billing_created boolean not null default false,
  claim_created boolean not null default false,
  notification_sent boolean not null default false,
  external_destination_contacted boolean not null default false,
  created_at timestamptz not null default now(),
  constraint chk_telehealth_post_visit_receipt_synthetic check (
    synthetic_data_confirmed
    and not appointment_completed
    and not encounter_completed
    and not clinical_record_delivered
    and not prescription_delivered
    and not billing_created
    and not claim_created
    and not notification_sent
    and not external_destination_contacted)
);

create index if not exists ix_telehealth_post_visit_receipt_patient
  on telehealth_synthetic_post_visit_receipts(practice_id, facility_id, patient_id, created_at desc);

drop trigger if exists trg_telehealth_post_visit_receipt_append_only on telehealth_synthetic_post_visit_receipts;
create trigger trg_telehealth_post_visit_receipt_append_only
before update or delete on telehealth_synthetic_post_visit_receipts
for each row execute function reject_telehealth_evidence_mutation();
