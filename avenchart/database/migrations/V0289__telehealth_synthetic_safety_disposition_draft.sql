-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

create table if not exists telehealth_consultation_disposition_draft_versions (
  disposition_version_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  encounter_id integer not null references encounters(encounter),
  version integer not null,
  disposition_code text not null,
  adequate_evaluation_completed boolean not null,
  follow_up_owner text not null,
  follow_up_timeframe text not null,
  next_step_instructions text not null,
  warning_escalation_instructions text not null,
  communication_method text not null,
  communication_completed boolean not null,
  location_callback_reconfirmed boolean not null,
  emergency_instruction_provided boolean not null,
  emergency_handoff_status text,
  contact_attempt_summary text,
  legal_effect boolean not null default false,
  recorded_at timestamptz not null default now(),
  recorded_by_staff_id integer not null references staff(id),
  constraint uq_telehealth_disposition_draft_version unique (consultation_id,version),
  constraint chk_telehealth_disposition_draft_version check (version >= 1),
  constraint chk_telehealth_disposition_code check (disposition_code in (
    'TreatedTelehealth','NoTreatmentNeeded','TestingOrReferralRequired','UrgentInPerson',
    'EmergencyTransferRecommended','TechnicalAbort','PatientLeft','ClinicianUnableToComplete')),
  constraint chk_telehealth_disposition_follow_up_owner check (follow_up_owner in (
    'Patient','Practice','TreatingPhysician','EmergencyServices','ExternalClinician','NoneClinicallyRequired')),
  constraint chk_telehealth_disposition_communication_method check (communication_method in (
    'DiscussedDuringSyntheticConsultation','SyntheticCallback','NotYetCommunicated')),
  constraint chk_telehealth_disposition_communication_state check (
    (communication_completed and communication_method <> 'NotYetCommunicated')
    or (not communication_completed and communication_method = 'NotYetCommunicated')),
  constraint chk_telehealth_disposition_handoff_state check (
    emergency_handoff_status is null or emergency_handoff_status in (
      'RecommendedOnly','PatientCalling','PracticeCalling','Connected','UnableToConfirm')),
  constraint chk_telehealth_disposition_text check (
    length(trim(follow_up_timeframe)) between 1 and 160
    and length(trim(next_step_instructions)) between 1 and 2000
    and length(trim(warning_escalation_instructions)) between 1 and 2000
    and (contact_attempt_summary is null or length(trim(contact_attempt_summary)) between 1 and 2000)),
  constraint chk_telehealth_disposition_evaluation check (
    disposition_code in ('TechnicalAbort','PatientLeft','ClinicianUnableToComplete')
    or adequate_evaluation_completed),
  constraint chk_telehealth_disposition_location check (
    disposition_code not in ('UrgentInPerson','EmergencyTransferRecommended')
    or location_callback_reconfirmed),
  constraint chk_telehealth_disposition_emergency check (
    (disposition_code='EmergencyTransferRecommended' and emergency_instruction_provided and emergency_handoff_status is not null)
    or (disposition_code<>'EmergencyTransferRecommended' and not emergency_instruction_provided and emergency_handoff_status is null)),
  constraint chk_telehealth_disposition_interrupted check (
    (disposition_code in ('TechnicalAbort','PatientLeft','ClinicianUnableToComplete') and contact_attempt_summary is not null)
    or (disposition_code not in ('TechnicalAbort','PatientLeft','ClinicianUnableToComplete') and contact_attempt_summary is null)),
  constraint chk_telehealth_disposition_legal_effect check (legal_effect=false)
);

create table if not exists telehealth_consultation_disposition_draft_events (
  event_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  disposition_version_id uuid not null references telehealth_consultation_disposition_draft_versions(disposition_version_id),
  aggregate_version integer not null,
  action text not null,
  actor_type text not null,
  actor_id text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint uq_telehealth_disposition_event_version unique (consultation_id,aggregate_version),
  constraint uq_telehealth_disposition_event_idempotency unique (consultation_id,idempotency_key),
  constraint chk_telehealth_disposition_event_version check (aggregate_version >= 1),
  constraint chk_telehealth_disposition_event_action check (action in ('DraftRecorded','DraftRevised')),
  constraint chk_telehealth_disposition_event_actor check (actor_type='physician'),
  constraint chk_telehealth_disposition_event_actor_id check (length(trim(actor_id)) between 1 and 128),
  constraint chk_telehealth_disposition_event_idempotency check (length(idempotency_key) between 8 and 128)
);

drop trigger if exists trg_telehealth_disposition_versions_append_only
  on telehealth_consultation_disposition_draft_versions;
create trigger trg_telehealth_disposition_versions_append_only
before update or delete on telehealth_consultation_disposition_draft_versions
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_disposition_events_append_only
  on telehealth_consultation_disposition_draft_events;
create trigger trg_telehealth_disposition_events_append_only
before update or delete on telehealth_consultation_disposition_draft_events
for each row execute function reject_telehealth_evidence_mutation();
