-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0047: one minimized applicant-owned request intake snapshot after an
-- exact TelehealthEligible complaint result. Verification remains pending and
-- no clinical publication, consent, coverage, operational, queue, or care
-- authority is conferred.

-- Keep the shared telehealth_intake_snapshots table one-to-many by request so
-- established patients can append a refreshed readiness snapshot after their
-- source coverage changes. The applicant-specific table below owns the tighter
-- one-request/one-intake invariant for this bounded workflow.
create table if not exists telehealth_applicant_request_intake_snapshots (
  receipt_id uuid primary key,
  intake_id uuid not null unique references telehealth_intake_snapshots(intake_id),
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  request_creation_id uuid not null unique
    references telehealth_applicant_request_creations(creation_id),
  location_confirmation_id uuid not null unique
    references telehealth_applicant_request_location_confirmations(confirmation_id),
  location_id uuid not null unique references telehealth_patient_locations(location_id),
  universal_safety_receipt_id uuid not null unique
    references telehealth_applicant_request_universal_safety_assessments(receipt_id),
  complaint_triage_receipt_id uuid not null unique
    references telehealth_applicant_request_complaint_triage_assessments(receipt_id),
  complaint_triage_assessment_id uuid not null unique
    references telehealth_triage_assessments(assessment_id),
  promotion_id uuid not null unique references telehealth_applicant_synthetic_promotions(promotion_id),
  practice_review_case_id uuid not null unique
    references telehealth_prospective_practice_review_cases(case_id),
  practice_review_authorization_id uuid not null unique
    references telehealth_practice_review_authorizations(authorization_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  source_request_status text not null,
  resulting_request_status text not null,
  complaint_category text not null,
  complaint_outcome text not null,
  complaint_summary text not null,
  symptom_duration text not null,
  current_location_state_code character(2) not null,
  callback_phone_last4 character(4) not null,
  location_confirmed_at timestamptz not null,
  complaint_evaluated_at timestamptz not null,
  context_expires_at timestamptz not null,
  applicant_expires_at timestamptz not null,
  context_snapshot_fingerprint character(64) not null,
  source_complaint_context_fingerprint character(64) not null,
  protocol_key text not null,
  protocol_version integer not null,
  protocol_content_hash character(64) not null,
  clinical_content_status text not null,
  medical_director_approval_required boolean not null default true,
  medical_director_approval_recorded boolean not null default false,
  clinical_golden_case_pack_approved boolean not null default false,
  production_publication_allowed boolean not null default false,
  current_location_confirmed boolean not null,
  callback_number_confirmed boolean not null,
  prior_information_reviewed boolean not null,
  insurance_limitations_acknowledged boolean not null,
  pending_consent_acknowledged boolean not null,
  pending_verification_acknowledged boolean not null,
  complaint_result_acknowledged boolean not null,
  synthetic_data_confirmed boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  intake_snapshot_created boolean not null default true,
  request_advanced_to_verification boolean not null default true,
  coverage_record_created boolean not null default false,
  coverage_verified boolean not null default false,
  exact_network_confirmed boolean not null default false,
  operational_review_created boolean not null default false,
  practice_accepted boolean not null default false,
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
  captured_at timestamptz not null,
  constraint uq_th_app_req_intake_idempotency unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_intake_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_th_app_req_intake_versions check (
    applicant_version=26 and source_request_version=4 and resulting_request_version=5
    and source_request_status='Intake' and resulting_request_status='Verification'),
  constraint chk_th_app_req_intake_complaint check (
    complaint_outcome='TelehealthEligible'
    and ((complaint_category='migraine'
          and complaint_summary='Synthetic migraine intake demonstration'
          and protocol_key='synthetic-migraine-complaint-triage')
      or (complaint_category='sleep'
          and complaint_summary='Synthetic sleep intake demonstration'
          and protocol_key='synthetic-sleep-complaint-triage'))),
  constraint chk_th_app_req_intake_duration check (
    symptom_duration in ('less-than-day','1-3-days','4-14-days','more-than-14-days')),
  constraint chk_th_app_req_intake_context check (
    current_location_state_code in ('GA','CA','FL')
    and callback_phone_last4 ~ '^[0-9]{4}$'
    and current_location_confirmed and callback_number_confirmed
    and prior_information_reviewed and insurance_limitations_acknowledged
    and pending_consent_acknowledged and pending_verification_acknowledged
    and complaint_result_acknowledged and synthetic_data_confirmed),
  constraint chk_th_app_req_intake_freshness check (
    location_confirmed_at<complaint_evaluated_at
    and complaint_evaluated_at<=captured_at
    and captured_at<=context_expires_at and captured_at<applicant_expires_at),
  constraint chk_th_app_req_intake_publication_gate check (
    protocol_version=1 and clinical_content_status='UNAPPROVED_SYNTHETIC'
    and medical_director_approval_required
    and not medical_director_approval_recorded
    and not clinical_golden_case_pack_approved
    and not production_publication_allowed),
  constraint chk_th_app_req_intake_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION'
    and policy_version=1
    and evidence_type='APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION'),
  constraint chk_th_app_req_intake_hashes check (
    context_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and source_complaint_context_fingerprint ~ '^[0-9a-f]{64}$'
    and protocol_content_hash ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_th_app_req_intake_idem check (length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_intake_no_consequence check (
    intake_snapshot_created and request_advanced_to_verification
    and not coverage_record_created and not coverage_verified
    and not exact_network_confirmed and not operational_review_created
    and not practice_accepted and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_th_app_request_intake_snapshot()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  creation_row telehealth_applicant_request_creations%rowtype;
  location_confirmation_row telehealth_applicant_request_location_confirmations%rowtype;
  location_row telehealth_patient_locations%rowtype;
  universal_row telehealth_applicant_request_universal_safety_assessments%rowtype;
  complaint_row telehealth_applicant_request_complaint_triage_assessments%rowtype;
  intake_row telehealth_intake_snapshots%rowtype;
  request_row telehealth_requests%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into creation_row from telehealth_applicant_request_creations
  where creation_id=new.request_creation_id;
  select * into location_confirmation_row from telehealth_applicant_request_location_confirmations
  where confirmation_id=new.location_confirmation_id;
  select * into location_row from telehealth_patient_locations where location_id=new.location_id;
  select * into universal_row from telehealth_applicant_request_universal_safety_assessments
  where receipt_id=new.universal_safety_receipt_id;
  select * into complaint_row from telehealth_applicant_request_complaint_triage_assessments
  where receipt_id=new.complaint_triage_receipt_id;
  select * into intake_row from telehealth_intake_snapshots where intake_id=new.intake_id;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null or creation_row.creation_id is null
     or location_confirmation_row.confirmation_id is null or location_row.location_id is null
     or universal_row.receipt_id is null or complaint_row.receipt_id is null
     or intake_row.intake_id is null or request_row.request_id is null
     or patient_row.canonical_id is null
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at or applicant_row.expires_at<=new.captured_at
     or creation_row.applicant_id<>new.applicant_id or creation_row.request_id<>new.request_id
     or creation_row.practice_id<>new.practice_id or creation_row.facility_id<>new.facility_id
     or creation_row.canonical_patient_id<>new.canonical_patient_id
     or creation_row.promotion_id<>new.promotion_id
     or creation_row.practice_review_case_id<>new.practice_review_case_id
     or creation_row.practice_review_authorization_id<>new.practice_review_authorization_id
     or creation_row.complaint_category<>new.complaint_category
     or creation_row.request_status<>'Draft' or creation_row.request_version<>1
     or creation_row.resulting_applicant_status<>'SyntheticRequestCreated'
     or creation_row.resulting_applicant_version<>new.applicant_version
     or location_confirmation_row.applicant_id<>new.applicant_id
     or location_confirmation_row.request_id<>new.request_id
     or location_confirmation_row.request_creation_id<>new.request_creation_id
     or location_confirmation_row.location_id<>new.location_id
     or location_confirmation_row.current_location_state_code<>new.current_location_state_code
     or location_confirmation_row.callback_phone_last4<>new.callback_phone_last4
     or location_confirmation_row.confirmed_at<>new.location_confirmed_at
     or location_confirmation_row.resulting_request_status<>'LocationConfirmed'
     or location_confirmation_row.resulting_request_version<>2
     or not location_confirmation_row.location_confirmed
     or location_row.request_id<>new.request_id or location_row.request_version<>2
     or location_row.state_code<>new.current_location_state_code
     or location_row.attested_at<>new.location_confirmed_at
     or universal_row.applicant_id<>new.applicant_id or universal_row.request_id<>new.request_id
     or universal_row.request_creation_id<>new.request_creation_id
     or universal_row.location_confirmation_id<>new.location_confirmation_id
     or universal_row.location_id<>new.location_id
     or universal_row.outcome<>'TelehealthEligible' or not universal_row.universal_safety_passed
     or universal_row.resulting_request_status<>'SafetyScreening'
     or universal_row.resulting_request_version<>3
     or complaint_row.applicant_id<>new.applicant_id or complaint_row.request_id<>new.request_id
     or complaint_row.request_creation_id<>new.request_creation_id
     or complaint_row.location_confirmation_id<>new.location_confirmation_id
     or complaint_row.location_id<>new.location_id
     or complaint_row.universal_safety_receipt_id<>new.universal_safety_receipt_id
     or complaint_row.assessment_id<>new.complaint_triage_assessment_id
     or complaint_row.complaint_category<>new.complaint_category
     or complaint_row.outcome<>new.complaint_outcome
     or complaint_row.resulting_request_status<>'Intake'
     or complaint_row.resulting_request_version<>new.source_request_version
     or complaint_row.evaluated_at<>new.complaint_evaluated_at
     or complaint_row.context_expires_at<>new.context_expires_at
     or complaint_row.context_snapshot_fingerprint<>new.source_complaint_context_fingerprint
     or complaint_row.protocol_key<>new.protocol_key
     or complaint_row.protocol_version<>new.protocol_version
     or complaint_row.protocol_content_hash<>new.protocol_content_hash
     or complaint_row.clinical_content_status<>new.clinical_content_status
     or complaint_row.medical_director_approval_recorded
     or complaint_row.clinical_golden_case_pack_approved
     or complaint_row.production_publication_allowed
     or not complaint_row.synthetic_video_evaluation_candidate
     or complaint_row.clinical_review_required or complaint_row.terminal_for_telehealth
     or intake_row.request_id<>new.request_id
     or intake_row.complaint_summary<>new.complaint_summary
     or intake_row.symptom_duration<>new.symptom_duration
     or not intake_row.synthetic_data_confirmed
     or intake_row.request_version<>new.resulting_request_version
     or intake_row.idempotency_key<>new.idempotency_key
     or intake_row.command_fingerprint<>new.command_fingerprint
     or intake_row.captured_at<>new.captured_at
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.source_promotion_id<>new.promotion_id
     or request_row.source_practice_review_case_id<>new.practice_review_case_id
     or request_row.source_practice_review_authorization_id<>new.practice_review_authorization_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.complaint_category<>new.complaint_category
     or request_row.status<>new.resulting_request_status
     or request_row.version<>new.resulting_request_version
     or request_row.triage_outcome<>new.complaint_outcome
     or request_row.ready_at is not null or request_row.appointment_id is not null
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null or patient_row.lifecycle_status<>'active'
     or not exists(select 1 from telehealth_applicant_synthetic_promotions x
                   where x.applicant_id=new.applicant_id and x.promotion_id=new.promotion_id)
     or not exists(select 1 from telehealth_applicant_notice_acknowledgments x where x.applicant_id=new.applicant_id)
     or not exists(select 1 from telehealth_applicant_registration_details_confirmations x where x.applicant_id=new.applicant_id)
     or not exists(select 1 from telehealth_applicant_insurance_handoff_confirmations x where x.applicant_id=new.applicant_id)
     or not exists(select 1 from telehealth_applicant_communication_access_readiness x where x.applicant_id=new.applicant_id)
     or not exists(select 1 from telehealth_applicant_device_preparations x where x.applicant_id=new.applicant_id)
     or not exists(select 1 from telehealth_applicant_clinical_information_summary_confirmations x where x.applicant_id=new.applicant_id)
     or not exists(select 1 from telehealth_applicant_pre_request_readiness_acknowledgments x where x.applicant_id=new.applicant_id)
     or not exists(select 1 from telehealth_applicant_practice_review_submissions x
                   where x.applicant_id=new.applicant_id and x.case_id=new.practice_review_case_id)
     or not exists(select 1 from telehealth_prospective_practice_review_cases x
                   where x.applicant_id=new.applicant_id and x.case_id=new.practice_review_case_id)
     or not exists(select 1 from telehealth_practice_review_claims x where x.case_id=new.practice_review_case_id)
     or not exists(select 1 from telehealth_practice_review_authorizations x
                   where x.applicant_id=new.applicant_id
                     and x.authorization_id=new.practice_review_authorization_id
                     and x.request_creation_authorized)
     or (select count(*) from telehealth_triage_assessments x where x.request_id=new.request_id)<>2
     or exists(select 1 from insurance_records x where lower(x.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from telehealth_patient_confirmations x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_demonstration_acknowledgments x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_coverage_selections x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_coverage_verifications x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_reservations x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_video_sessions x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_consultation_contexts x where x.request_id=new.request_id) then
    raise exception 'invalid telehealth applicant request intake snapshot provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_intake_snapshot_guard
  on telehealth_applicant_request_intake_snapshots;
create trigger trg_th_app_request_intake_snapshot_guard
before insert on telehealth_applicant_request_intake_snapshots
for each row execute function enforce_th_app_request_intake_snapshot();

drop trigger if exists trg_th_app_request_intake_snapshot_append
  on telehealth_applicant_request_intake_snapshots;
create trigger trg_th_app_request_intake_snapshot_append
before update or delete on telehealth_applicant_request_intake_snapshots
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_intake_state
  on telehealth_applicant_request_intake_snapshots(
    practice_id,facility_id,current_location_state_code,captured_at,applicant_id);
