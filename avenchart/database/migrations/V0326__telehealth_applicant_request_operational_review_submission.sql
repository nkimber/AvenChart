-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0054: submit one exact synthetic request for practice operational review.
-- OperationalReview is neither practice acceptance nor a care queue or care authorization.

create table if not exists telehealth_applicant_request_operational_review_submissions (
  submission_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  participation_evaluation_id uuid not null unique
    references telehealth_applicant_request_participation_evaluations(evaluation_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  source_request_status text not null,
  resulting_request_status text not null,
  submission_snapshot_fingerprint character(64) not null,
  evaluation_snapshot_fingerprint character(64) not null,
  practice_display_name text not null,
  payer_display_name text not null,
  product_display_name text not null,
  current_location_state_code character(2) not null,
  purpose_category text not null,
  date_of_service date not null,
  candidate_staff_id integer not null references staff(id),
  candidate_display_name text not null,
  candidate_npi_last4 character(4) not null,
  service_category text not null,
  modality text not null,
  evaluated_at timestamptz not null,
  result_valid_through timestamptz not null,
  applicant_expires_at timestamptz not null,
  source_mode text not null,
  compatibility_target text not null,
  business_outcome text not null,
  synthetic_evidence_acknowledged boolean not null,
  no_coverage_guarantee_acknowledged boolean not null,
  practice_review_pending_acknowledged boolean not null,
  no_care_relationship_acknowledged boolean not null,
  synthetic_automated_checks_complete boolean not null default true,
  operational_review_created boolean not null default true,
  real_state_authority_verified boolean not null default false,
  real_credentialing_verified boolean not null default false,
  rendering_physician_assigned boolean not null default false,
  rendering_physician_network_checked boolean not null default false,
  exact_network_confirmed boolean not null default false,
  canonical_coverage_created boolean not null default false,
  generic_coverage_selected boolean not null default false,
  coverage_verified boolean not null default false,
  estimate_created boolean not null default false,
  financial_acknowledgment_created boolean not null default false,
  financial_route_created boolean not null default false,
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
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  submitted_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  constraint uq_th_app_req_op_review_submission_idem unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_op_review_submission_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10
    and practice_display_name='AvenChart Synthetic Practice'),
  constraint chk_th_app_req_op_review_submission_versions check (
    applicant_version=26 and source_request_version=11 and resulting_request_version=12
    and source_request_status='Verification' and resulting_request_status='OperationalReview'),
  constraint chk_th_app_req_op_review_submission_source check (
    source_mode='NON_PRODUCTION'
    and compatibility_target='AVENCHART_SYNTHETIC_OPERATIONAL_REVIEW_V1'
    and business_outcome='SyntheticRequestSubmittedForOperationalReview'
    and current_location_state_code in ('GA','CA','FL')
    and purpose_category in ('migraine','sleep')
    and service_category='ProfessionalTelehealthConsultation'
    and modality='RealTimeAudioVideo'),
  constraint chk_th_app_req_op_review_submission_freshness check (
    evaluated_at<=submitted_at and submitted_at<=recorded_at
    and submitted_at<result_valid_through
    and result_valid_through<=applicant_expires_at),
  constraint chk_th_app_req_op_review_submission_ack check (
    synthetic_evidence_acknowledged and no_coverage_guarantee_acknowledged
    and practice_review_pending_acknowledged and no_care_relationship_acknowledged),
  constraint chk_th_app_req_op_review_submission_result check (
    synthetic_automated_checks_complete and operational_review_created),
  constraint chk_th_app_req_op_review_submission_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'
    and policy_version=1
    and evidence_type='APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'),
  constraint chk_th_app_req_op_review_submission_hashes check (
    submission_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and evaluation_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'
    and candidate_npi_last4 ~ '^[0-9]{4}$'),
  constraint chk_th_app_req_op_review_submission_idem check (length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_op_review_submission_no_consequence check (
    not real_state_authority_verified and not real_credentialing_verified
    and not rendering_physician_assigned and not rendering_physician_network_checked
    and not exact_network_confirmed and not canonical_coverage_created
    and not generic_coverage_selected and not coverage_verified
    and not estimate_created and not financial_acknowledgment_created
    and not financial_route_created and not practice_accepted and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_th_app_request_op_review_submission()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  request_row telehealth_requests%rowtype;
  evaluation_row telehealth_applicant_request_participation_evaluations%rowtype;
  patient_row patients%rowtype;
  candidate_row staff%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
    where applicant_id=new.applicant_id for key share;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into evaluation_row from telehealth_applicant_request_participation_evaluations
    where evaluation_id=new.participation_evaluation_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;
  select * into candidate_row from staff where id=new.candidate_staff_id;

  if applicant_row.applicant_id is null or request_row.request_id is null
     or evaluation_row.evaluation_id is null or patient_row.canonical_id is null
     or candidate_row.id is null
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at or applicant_row.expires_at<=new.submitted_at
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.status<>new.source_request_status or request_row.version<>new.source_request_version
     or request_row.triage_outcome<>'TelehealthEligible'
     or request_row.complaint_category<>new.purpose_category
     or request_row.ready_at is not null or request_row.appointment_id is not null
     or evaluation_row.request_id<>new.request_id or evaluation_row.applicant_id<>new.applicant_id
     or evaluation_row.practice_id<>new.practice_id or evaluation_row.facility_id<>new.facility_id
     or evaluation_row.canonical_patient_id<>new.canonical_patient_id
     or evaluation_row.applicant_version<>new.applicant_version
     or evaluation_row.resulting_request_version<>new.source_request_version
     or evaluation_row.resulting_request_status<>new.source_request_status
     or evaluation_row.evaluation_snapshot_fingerprint<>new.evaluation_snapshot_fingerprint
     or evaluation_row.practice_display_name<>new.practice_display_name
     or evaluation_row.payer_display_name<>new.payer_display_name
     or evaluation_row.product_display_name<>new.product_display_name
     or evaluation_row.current_location_state_code<>new.current_location_state_code
     or evaluation_row.purpose_category<>new.purpose_category
     or evaluation_row.date_of_service<>new.date_of_service
     or evaluation_row.candidate_staff_id<>new.candidate_staff_id
     or evaluation_row.candidate_display_name<>new.candidate_display_name
     or evaluation_row.candidate_npi_last4<>new.candidate_npi_last4
     or evaluation_row.service_category<>new.service_category
     or evaluation_row.modality<>new.modality
     or evaluation_row.evaluated_at<>new.evaluated_at
     or evaluation_row.result_valid_through<>new.result_valid_through
     or evaluation_row.result_valid_through<=new.submitted_at
     or evaluation_row.policy_key<>'SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_EVALUATION'
     or evaluation_row.policy_version<>1
     or evaluation_row.evidence_type<>'APPLICANT_REQUEST_PARTICIPATION_EVALUATION'
     or evaluation_row.source_mode<>'NON_PRODUCTION'
     or evaluation_row.business_outcome<>'SyntheticExactParticipationMatched'
     or not evaluation_row.synthetic_participation_evaluated
     or not evaluation_row.synthetic_billing_entity_in_network
     or not evaluation_row.synthetic_rendering_provider_in_network
     or not evaluation_row.synthetic_plan_network_matched
     or not evaluation_row.synthetic_service_location_matched
     or not evaluation_row.synthetic_new_patients_accepted
     or not evaluation_row.synthetic_exact_network_matched
     or evaluation_row.real_state_authority_verified or evaluation_row.real_credentialing_verified
     or evaluation_row.rendering_physician_assigned
     or evaluation_row.rendering_physician_network_checked
     or evaluation_row.exact_network_confirmed or evaluation_row.coverage_verified
     or candidate_row.role<>'provider' or not candidate_row.active
     or candidate_row.facility_id<>new.facility_id
     or right(candidate_row.npi,4)<>new.candidate_npi_last4
     or trim(concat(candidate_row.first_name,' ',candidate_row.last_name))<>new.candidate_display_name
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null or patient_row.lifecycle_status<>'active'
     or exists(select 1 from insurance_records x where lower(x.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from telehealth_coverage_selections x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_coverage_verifications x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_reservations x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_video_sessions x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_consultation_contexts x where x.request_id=new.request_id) then
    raise exception 'invalid telehealth applicant operational-review-submission provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_op_review_submission_guard
  on telehealth_applicant_request_operational_review_submissions;
create trigger trg_th_app_request_op_review_submission_guard
before insert on telehealth_applicant_request_operational_review_submissions
for each row execute function enforce_th_app_request_op_review_submission();

drop trigger if exists trg_th_app_request_op_review_submission_append
  on telehealth_applicant_request_operational_review_submissions;
create trigger trg_th_app_request_op_review_submission_append
before update or delete on telehealth_applicant_request_operational_review_submissions
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_op_review_submission_state
  on telehealth_applicant_request_operational_review_submissions(
    practice_id,facility_id,submitted_at,applicant_id);
