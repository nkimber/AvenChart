-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- One immutable physician-authored synthetic plan preview created only with
-- synthetic closure. It is not an AVS, clinical delivery, or completed care.

create table if not exists telehealth_synthetic_after_visit_plan_previews (
  preview_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  consultation_id uuid not null unique references telehealth_consultation_contexts(consultation_id),
  encounter_id integer not null references encounters(encounter),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  patient_id text not null references patients(canonical_id),
  consultation_version integer not null check (consultation_version > 0),
  request_version integer not null check (request_version > 0),
  disposition_version integer not null check (disposition_version > 0),
  final_clinical_review_version integer not null check (final_clinical_review_version > 0),
  source_evidence_hash text not null check (length(source_evidence_hash) = 64),
  preview_version integer not null default 1 check (preview_version = 1),
  preview_state text not null check (preview_state = 'AvailableInPortal'),
  source_mode text not null check (source_mode = 'NON_PRODUCTION'),
  synthetic_data_confirmed boolean not null,
  disposition_code text not null,
  follow_up_owner text not null,
  follow_up_timeframe text not null,
  next_step_instructions text not null,
  warning_escalation_instructions text not null,
  communication_method text not null,
  communication_completed boolean not null,
  appointment_completed boolean not null default false,
  encounter_completed boolean not null default false,
  avs_delivered boolean not null default false,
  notification_sent boolean not null default false,
  external_destination_contacted boolean not null default false,
  created_at timestamptz not null default now(),
  constraint chk_telehealth_after_visit_plan_preview_synthetic check (
    synthetic_data_confirmed
    and not appointment_completed
    and not encounter_completed
    and not avs_delivered
    and not notification_sent
    and not external_destination_contacted)
);

create index if not exists ix_telehealth_after_visit_plan_preview_patient
  on telehealth_synthetic_after_visit_plan_previews(practice_id, facility_id, patient_id, created_at desc);

drop trigger if exists trg_telehealth_after_visit_plan_preview_append_only on telehealth_synthetic_after_visit_plan_previews;
create trigger trg_telehealth_after_visit_plan_preview_append_only
before update or delete on telehealth_synthetic_after_visit_plan_previews
for each row execute function reject_telehealth_evidence_mutation();
