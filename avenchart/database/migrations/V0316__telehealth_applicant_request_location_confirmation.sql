-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0044: bind an applicant-created Draft request to the exact supported
-- current-location and masked callback context already confirmed upstream.
-- This creates no triage, clinical review, contact, queue, care, or external action.

create table if not exists telehealth_applicant_request_location_confirmations (
  confirmation_id uuid primary key,
  location_id uuid not null unique references telehealth_patient_locations(location_id),
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  request_creation_id uuid not null unique
    references telehealth_applicant_request_creations(creation_id),
  communication_readiness_id uuid not null unique
    references telehealth_applicant_communication_access_readiness(readiness_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  resulting_request_status text not null,
  current_location_state_code character(2) not null,
  callback_phone_last4 character(4) not null,
  context_snapshot_fingerprint character(64) not null,
  current_location_confirmed boolean not null,
  callback_number_confirmed boolean not null,
  changed_location_requires_restart_acknowledged boolean not null,
  urgent_or_worsening_action_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  location_confirmed boolean not null default true,
  triage_assessment_created boolean not null default false,
  clinical_review_created boolean not null default false,
  patient_contacted boolean not null default false,
  patient_care_queue_entered boolean not null default false,
  clinician_queue_entered boolean not null default false,
  doctor_search_started boolean not null default false,
  queue_position_assigned boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  consent_created boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  billing_enabled boolean not null default false,
  claim_created boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  confirmed_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_request_location_idempotency
    unique(applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_request_location_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_telehealth_applicant_request_location_versions check (
    applicant_version=26 and source_request_version=1
    and resulting_request_version=2 and resulting_request_status='LocationConfirmed'),
  constraint chk_telehealth_applicant_request_location_context check (
    current_location_state_code in ('GA','CA','FL')
    and callback_phone_last4 ~ '^[0-9]{4}$'),
  constraint chk_telehealth_applicant_request_location_acknowledgments check (
    current_location_confirmed and callback_number_confirmed
    and changed_location_requires_restart_acknowledged
    and urgent_or_worsening_action_acknowledged),
  constraint chk_telehealth_applicant_request_location_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION'
    and policy_version=1
    and evidence_type='APPLICANT_REQUEST_LOCATION_CALLBACK_CONFIRMATION'),
  constraint chk_telehealth_applicant_request_location_idempotency check (
    length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_request_location_fingerprints check (
    context_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_request_location_no_consequence check (
    location_confirmed and not triage_assessment_created
    and not clinical_review_created and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_request_location_confirmation()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  creation_row telehealth_applicant_request_creations%rowtype;
  request_row telehealth_requests%rowtype;
  location_row telehealth_patient_locations%rowtype;
  readiness_row telehealth_applicant_communication_access_readiness%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into creation_row from telehealth_applicant_request_creations
  where creation_id=new.request_creation_id;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into location_row from telehealth_patient_locations where location_id=new.location_id;
  select * into readiness_row from telehealth_applicant_communication_access_readiness
  where readiness_id=new.communication_readiness_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null or creation_row.creation_id is null
     or request_row.request_id is null or location_row.location_id is null
     or readiness_row.readiness_id is null or patient_row.canonical_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated'
     or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<=now()
     or creation_row.applicant_id<>new.applicant_id
     or creation_row.request_id<>new.request_id
     or creation_row.practice_id<>new.practice_id
     or creation_row.facility_id<>new.facility_id
     or creation_row.canonical_patient_id<>new.canonical_patient_id
     or creation_row.resulting_applicant_version<>new.applicant_version
     or creation_row.resulting_applicant_status<>'SyntheticRequestCreated'
     or creation_row.request_status<>'Draft' or creation_row.request_version<>1
     or not creation_row.telehealth_request_created
     or creation_row.patient_contacted or creation_row.patient_care_queue_entered
     or creation_row.clinician_queue_entered or creation_row.doctor_search_started
     or creation_row.queue_position_assigned or creation_row.appointment_created
     or creation_row.encounter_created or creation_row.consent_created
     or creation_row.care_authorized or creation_row.prescribing_enabled
     or creation_row.billing_enabled or creation_row.claim_created
     or creation_row.integration_enabled or creation_row.external_call_performed
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id
     or request_row.facility_id<>new.facility_id
     or request_row.status<>new.resulting_request_status
     or request_row.version<>new.resulting_request_version
     or request_row.triage_outcome is not null or request_row.ready_at is not null
     or location_row.request_id<>new.request_id
     or location_row.state_code<>new.current_location_state_code
     or location_row.request_version<>new.resulting_request_version
     or location_row.idempotency_key<>new.idempotency_key
     or location_row.command_fingerprint<>new.command_fingerprint
     or location_row.attested_at<>new.confirmed_at
     or readiness_row.applicant_id<>new.applicant_id
     or readiness_row.practice_id<>new.practice_id
     or readiness_row.facility_id<>new.facility_id
     or readiness_row.canonical_patient_id<>new.canonical_patient_id
     or readiness_row.current_location_state_code<>new.current_location_state_code
     or readiness_row.callback_phone_last4<>new.callback_phone_last4
     or not readiness_row.current_location_confirmed
     or not readiness_row.callback_number_confirmed
     or readiness_row.policy_key<>'SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
     or readiness_row.policy_version<>1
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null
     or exists(select 1 from telehealth_triage_assessments x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id) then
    raise exception 'invalid telehealth applicant request location provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_request_location_guard
  on telehealth_applicant_request_location_confirmations;
create trigger trg_telehealth_applicant_request_location_guard
before insert on telehealth_applicant_request_location_confirmations
for each row execute function enforce_telehealth_applicant_request_location_confirmation();

drop trigger if exists trg_telehealth_applicant_request_location_append_only
  on telehealth_applicant_request_location_confirmations;
create trigger trg_telehealth_applicant_request_location_append_only
before update or delete on telehealth_applicant_request_location_confirmations
for each row execute function reject_telehealth_evidence_mutation();
