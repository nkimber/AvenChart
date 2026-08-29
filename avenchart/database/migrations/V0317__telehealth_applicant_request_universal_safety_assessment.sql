-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0045: append one reproducible applicant-owned universal safety
-- assessment after exact request-time location confirmation. A passing result
-- remains in SafetyScreening and is not complaint-specific clinical eligibility.

alter table telehealth_requests
  drop constraint chk_telehealth_requests_status;
alter table telehealth_requests
  add constraint chk_telehealth_requests_status check (
    status in ('Draft','LocationConfirmed','SafetyScreening',
               'EmergencyRedirected','InPersonRecommended','ClinicalReview',
               'Intake','Verification','OperationalReview','Redirected','Queued',
               'Reserved','Connecting','InConsultation','WrapUp'));

create table if not exists telehealth_applicant_request_universal_safety_assessments (
  receipt_id uuid primary key,
  assessment_id uuid not null unique references telehealth_triage_assessments(assessment_id),
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  request_creation_id uuid not null unique
    references telehealth_applicant_request_creations(creation_id),
  location_confirmation_id uuid not null unique
    references telehealth_applicant_request_location_confirmations(confirmation_id),
  location_id uuid not null unique references telehealth_patient_locations(location_id),
  source_safety_evaluation_id uuid not null unique
    references telehealth_applicant_safety_triage_evaluations(evaluation_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  resulting_request_status text not null,
  current_location_state_code character(2) not null,
  callback_phone_last4 character(4) not null,
  location_confirmed_at timestamptz not null,
  context_expires_at timestamptz not null,
  applicant_expires_at timestamptz not null,
  context_snapshot_fingerprint character(64) not null,
  current_location_confirmed boolean not null,
  callback_number_confirmed boolean not null,
  synthetic_data_confirmed boolean not null,
  has_emergency_warning boolean not null,
  severe_or_worsening boolean not null,
  requires_hands_on_exam boolean not null,
  unsure boolean not null,
  protocol_id uuid not null,
  protocol_key text not null,
  protocol_version integer not null,
  protocol_content_hash character(64) not null,
  answers_fingerprint character(64) not null,
  outcome text not null,
  public_disposition text not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  universal_safety_assessment_created boolean not null default true,
  universal_safety_passed boolean not null,
  complaint_specific_triage_required boolean not null,
  complaint_specific_triage_created boolean not null default false,
  clinical_review_required boolean not null,
  clinical_review_created boolean not null default false,
  terminal_for_telehealth boolean not null,
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
  evaluated_at timestamptz not null,
  constraint uq_th_app_req_safety_idempotency unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_safety_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_th_app_req_safety_versions check (
    applicant_version=26 and source_request_version=2 and resulting_request_version=3),
  constraint chk_th_app_req_safety_result check (
    (outcome='Emergency' and resulting_request_status='EmergencyRedirected'
      and public_disposition='EmergencyCareNow'
      and not universal_safety_passed and not complaint_specific_triage_required
      and not clinical_review_required and terminal_for_telehealth)
    or
    (outcome='UrgentInPerson' and resulting_request_status='InPersonRecommended'
      and public_disposition='PromptInPersonCare'
      and not universal_safety_passed and not complaint_specific_triage_required
      and not clinical_review_required and terminal_for_telehealth)
    or
    (outcome='InPersonRequired' and resulting_request_status='InPersonRecommended'
      and public_disposition='InPersonCareRequired'
      and not universal_safety_passed and not complaint_specific_triage_required
      and not clinical_review_required and terminal_for_telehealth)
    or
    (outcome='ClinicalReview' and resulting_request_status='ClinicalReview'
      and public_disposition='ClinicalReviewRequired'
      and not universal_safety_passed and not complaint_specific_triage_required
      and clinical_review_required and not terminal_for_telehealth)
    or
    (outcome='TelehealthEligible' and resulting_request_status='SafetyScreening'
      and public_disposition='UniversalSafetyPassed'
      and universal_safety_passed and complaint_specific_triage_required
      and not clinical_review_required and not terminal_for_telehealth)),
  constraint chk_th_app_req_safety_context check (
    current_location_state_code in ('GA','CA','FL')
    and callback_phone_last4 ~ '^[0-9]{4}$'
    and current_location_confirmed and callback_number_confirmed
    and synthetic_data_confirmed),
  constraint chk_th_app_req_safety_freshness check (
    location_confirmed_at<context_expires_at
    and context_expires_at<=location_confirmed_at+interval '30 minutes'
    and evaluated_at<=context_expires_at
    and evaluated_at<applicant_expires_at),
  constraint chk_th_app_req_safety_protocol check (
    protocol_id='8df3224f-8cc6-4a1e-b070-657ad2f71f80'::uuid
    and protocol_key='synthetic-universal-safety' and protocol_version=1),
  constraint chk_th_app_req_safety_priority check (
    (has_emergency_warning and outcome='Emergency')
    or
    (not has_emergency_warning and severe_or_worsening and outcome='UrgentInPerson')
    or
    (not has_emergency_warning and not severe_or_worsening
      and requires_hands_on_exam and outcome='InPersonRequired')
    or
    (not has_emergency_warning and not severe_or_worsening
      and not requires_hands_on_exam and unsure and outcome='ClinicalReview')
    or
    (not has_emergency_warning and not severe_or_worsening
      and not requires_hands_on_exam and not unsure and outcome='TelehealthEligible')),
  constraint chk_th_app_req_safety_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT'
    and policy_version=1
    and evidence_type='APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT'),
  constraint chk_th_app_req_safety_hashes check (
    context_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and protocol_content_hash ~ '^[0-9a-f]{64}$'
    and answers_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_th_app_req_safety_idem check (length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_safety_no_consequence check (
    universal_safety_assessment_created and not complaint_specific_triage_created
    and not clinical_review_created and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_th_app_request_universal_safety()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  creation_row telehealth_applicant_request_creations%rowtype;
  location_confirmation_row telehealth_applicant_request_location_confirmations%rowtype;
  location_row telehealth_patient_locations%rowtype;
  source_safety_row telehealth_applicant_safety_triage_evaluations%rowtype;
  request_row telehealth_requests%rowtype;
  assessment_row telehealth_triage_assessments%rowtype;
  protocol_row telehealth_protocol_versions%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into creation_row from telehealth_applicant_request_creations
  where creation_id=new.request_creation_id;
  select * into location_confirmation_row
  from telehealth_applicant_request_location_confirmations
  where confirmation_id=new.location_confirmation_id;
  select * into location_row from telehealth_patient_locations where location_id=new.location_id;
  select * into source_safety_row from telehealth_applicant_safety_triage_evaluations
  where evaluation_id=new.source_safety_evaluation_id;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into assessment_row from telehealth_triage_assessments
  where assessment_id=new.assessment_id;
  select * into protocol_row from telehealth_protocol_versions where protocol_id=new.protocol_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null or creation_row.creation_id is null
     or location_confirmation_row.confirmation_id is null or location_row.location_id is null
     or source_safety_row.evaluation_id is null or request_row.request_id is null
     or assessment_row.assessment_id is null or protocol_row.protocol_id is null
     or patient_row.canonical_id is null
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at
     or applicant_row.expires_at<=new.evaluated_at
     or creation_row.applicant_id<>new.applicant_id or creation_row.request_id<>new.request_id
     or creation_row.practice_id<>new.practice_id or creation_row.facility_id<>new.facility_id
     or creation_row.canonical_patient_id<>new.canonical_patient_id
     or creation_row.request_status<>'Draft' or creation_row.request_version<>1
     or creation_row.resulting_applicant_status<>'SyntheticRequestCreated'
     or creation_row.resulting_applicant_version<>new.applicant_version
     or not creation_row.telehealth_request_created
     or location_confirmation_row.applicant_id<>new.applicant_id
     or location_confirmation_row.request_id<>new.request_id
     or location_confirmation_row.request_creation_id<>new.request_creation_id
     or location_confirmation_row.location_id<>new.location_id
     or location_confirmation_row.practice_id<>new.practice_id
     or location_confirmation_row.facility_id<>new.facility_id
     or location_confirmation_row.canonical_patient_id<>new.canonical_patient_id
     or location_confirmation_row.resulting_request_status<>'LocationConfirmed'
     or location_confirmation_row.resulting_request_version<>new.source_request_version
     or not location_confirmation_row.location_confirmed
     or location_confirmation_row.current_location_state_code<>new.current_location_state_code
     or location_confirmation_row.callback_phone_last4<>new.callback_phone_last4
     or location_confirmation_row.confirmed_at<>new.location_confirmed_at
     or location_row.request_id<>new.request_id or location_row.request_version<>new.source_request_version
     or location_row.state_code<>new.current_location_state_code
     or location_row.attested_at<>new.location_confirmed_at
     or source_safety_row.applicant_id<>new.applicant_id
     or source_safety_row.practice_id<>new.practice_id
     or source_safety_row.facility_id<>new.facility_id
     or source_safety_row.outcome<>'TelehealthEligible'
     or source_safety_row.protocol_id<>new.protocol_id
     or source_safety_row.protocol_key<>new.protocol_key
     or source_safety_row.protocol_version<>new.protocol_version
     or source_safety_row.protocol_content_hash<>new.protocol_content_hash
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.status<>new.resulting_request_status
     or request_row.version<>new.resulting_request_version
     or request_row.ready_at is not null
     or request_row.triage_outcome is distinct from
        (case when new.outcome='TelehealthEligible' then null else new.outcome end)
     or assessment_row.request_id<>new.request_id or assessment_row.protocol_id<>new.protocol_id
     or assessment_row.answer_fingerprint<>new.answers_fingerprint
     or assessment_row.outcome<>new.outcome
     or assessment_row.request_version<>new.resulting_request_version
     or assessment_row.idempotency_key<>new.idempotency_key
     or assessment_row.command_fingerprint<>new.command_fingerprint
     or assessment_row.evaluated_at<>new.evaluated_at
     or protocol_row.protocol_key<>new.protocol_key
     or protocol_row.protocol_version<>new.protocol_version
     or protocol_row.content_hash<>new.protocol_content_hash
     or not protocol_row.is_synthetic
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null
     or patient_row.lifecycle_status<>'active'
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id)
     or request_row.appointment_id is not null
     or exists(select 1 from telehealth_consultation_contexts x where x.request_id=new.request_id) then
    raise exception 'invalid telehealth applicant request universal safety provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_universal_safety_guard
  on telehealth_applicant_request_universal_safety_assessments;
create trigger trg_th_app_request_universal_safety_guard
before insert on telehealth_applicant_request_universal_safety_assessments
for each row execute function enforce_th_app_request_universal_safety();

drop trigger if exists trg_th_app_request_universal_safety_append
  on telehealth_applicant_request_universal_safety_assessments;
create trigger trg_th_app_request_universal_safety_append
before update or delete on telehealth_applicant_request_universal_safety_assessments
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_universal_safety_outcome
  on telehealth_applicant_request_universal_safety_assessments(
    practice_id,facility_id,outcome,evaluated_at,applicant_id);
