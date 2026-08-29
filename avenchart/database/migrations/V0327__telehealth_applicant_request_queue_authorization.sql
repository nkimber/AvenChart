-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0055: one explicit configured-practice staff authorization may move an
-- applicant-originated synthetic request into the clinician queue. This does not
-- verify real coverage, assign a rendering physician, or authorize care.

create table if not exists telehealth_applicant_request_queue_authorizations (
  authorization_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  submission_id uuid not null unique
    references telehealth_applicant_request_operational_review_submissions(submission_id),
  applicant_id uuid not null references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null references patients(canonical_id),
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  source_request_status text not null,
  resulting_request_status text not null,
  authorization_snapshot_fingerprint character(64) not null,
  submission_snapshot_fingerprint character(64) not null,
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
  operational_review_submitted_at timestamptz not null,
  result_valid_through timestamptz not null,
  source_mode text not null,
  compatibility_target text not null,
  business_outcome text not null,
  synthetic_evidence_reviewed boolean not null,
  no_coverage_guarantee_acknowledged boolean not null,
  practice_accepts_for_queue_acknowledged boolean not null,
  queue_not_care_acknowledged boolean not null,
  practice_accepted boolean not null default true,
  patient_care_queue_entered boolean not null default true,
  clinician_queue_entered boolean not null default true,
  doctor_search_started boolean not null default true,
  appointment_created boolean not null default true,
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
  patient_contacted boolean not null default false,
  queue_position_assigned boolean not null default false,
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
  decided_by_staff_id integer references staff(id),
  decided_by_actor_id text not null,
  decided_by_role text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  authorized_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  constraint uq_th_app_req_queue_auth_idem unique(request_id,idempotency_key),
  constraint chk_th_app_req_queue_auth_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10
    and practice_display_name='AvenChart Synthetic Practice'),
  constraint chk_th_app_req_queue_auth_versions check (
    source_request_version=12 and resulting_request_version=13
    and source_request_status='OperationalReview' and resulting_request_status='Queued'),
  constraint chk_th_app_req_queue_auth_source check (
    source_mode='NON_PRODUCTION'
    and compatibility_target='AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1'
    and business_outcome='SyntheticRequestAuthorizedToQueue'
    and current_location_state_code in ('GA','CA','FL')
    and purpose_category in ('migraine','sleep')
    and service_category='ProfessionalTelehealthConsultation'
    and modality='RealTimeAudioVideo'),
  constraint chk_th_app_req_queue_auth_time check (
    operational_review_submitted_at<=authorized_at and authorized_at<=recorded_at
    and authorized_at<result_valid_through),
  constraint chk_th_app_req_queue_auth_ack check (
    synthetic_evidence_reviewed and no_coverage_guarantee_acknowledged
    and practice_accepts_for_queue_acknowledged and queue_not_care_acknowledged),
  constraint chk_th_app_req_queue_auth_result check (
    practice_accepted and patient_care_queue_entered and clinician_queue_entered
    and doctor_search_started and appointment_created),
  constraint chk_th_app_req_queue_auth_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
    and policy_version=1 and evidence_type='APPLICANT_REQUEST_QUEUE_AUTHORIZATION'),
  constraint chk_th_app_req_queue_auth_actor check (
    decided_by_role in ('administrator','frontdesk')
    and length(decided_by_actor_id) between 1 and 200
    and (decided_by_role<>'frontdesk' or decided_by_staff_id is not null)),
  constraint chk_th_app_req_queue_auth_hashes check (
    authorization_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and submission_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'
    and candidate_npi_last4 ~ '^[0-9]{4}$'),
  constraint chk_th_app_req_queue_auth_idem check (length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_queue_auth_no_consequence check (
    not real_state_authority_verified and not real_credentialing_verified
    and not rendering_physician_assigned and not rendering_physician_network_checked
    and not exact_network_confirmed and not canonical_coverage_created
    and not generic_coverage_selected and not coverage_verified
    and not estimate_created and not financial_acknowledgment_created
    and not financial_route_created and not patient_contacted and not queue_position_assigned
    and not encounter_created and not consent_created and not care_authorized
    and not prescribing_enabled and not billing_enabled and not claim_created
    and not integration_enabled and not external_call_performed)
);

create or replace function enforce_th_app_request_queue_authorization()
returns trigger
language plpgsql
as $$
declare
  request_row telehealth_requests%rowtype;
  submission_row telehealth_applicant_request_operational_review_submissions%rowtype;
  applicant_row telehealth_prospective_applicants%rowtype;
  patient_row patients%rowtype;
  candidate_row staff%rowtype;
  actor_row staff%rowtype;
begin
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into submission_row from telehealth_applicant_request_operational_review_submissions
    where submission_id=new.submission_id;
  select * into applicant_row from telehealth_prospective_applicants
    where applicant_id=new.applicant_id for key share;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;
  select * into candidate_row from staff where id=new.candidate_staff_id;
  if new.decided_by_staff_id is not null then
    select * into actor_row from staff where id=new.decided_by_staff_id;
  end if;

  if request_row.request_id is null or submission_row.submission_id is null
     or applicant_row.applicant_id is null or patient_row.canonical_id is null
     or candidate_row.id is null
     or request_row.source_applicant_id is distinct from new.applicant_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.status<>new.source_request_status or request_row.version<>new.source_request_version
     or request_row.triage_outcome<>'TelehealthEligible'
     or request_row.complaint_category<>new.purpose_category
     or request_row.ready_at is not null or request_row.appointment_id is not null
     or submission_row.request_id<>new.request_id or submission_row.applicant_id<>new.applicant_id
     or submission_row.practice_id<>new.practice_id or submission_row.facility_id<>new.facility_id
     or submission_row.canonical_patient_id<>new.canonical_patient_id
     or submission_row.resulting_request_status<>new.source_request_status
     or submission_row.resulting_request_version<>new.source_request_version
     or submission_row.submission_snapshot_fingerprint<>new.submission_snapshot_fingerprint
     or submission_row.practice_display_name<>new.practice_display_name
     or submission_row.payer_display_name<>new.payer_display_name
     or submission_row.product_display_name<>new.product_display_name
     or submission_row.current_location_state_code<>new.current_location_state_code
     or submission_row.purpose_category<>new.purpose_category
     or submission_row.date_of_service<>new.date_of_service
     or submission_row.candidate_staff_id<>new.candidate_staff_id
     or submission_row.candidate_display_name<>new.candidate_display_name
     or submission_row.candidate_npi_last4<>new.candidate_npi_last4
     or submission_row.service_category<>new.service_category
     or submission_row.modality<>new.modality
     or submission_row.submitted_at<>new.operational_review_submitted_at
     or submission_row.result_valid_through<>new.result_valid_through
     or submission_row.result_valid_through<=new.authorized_at
     or submission_row.policy_key<>'SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'
     or submission_row.policy_version<>1
     or submission_row.evidence_type<>'APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'
     or submission_row.source_mode<>'NON_PRODUCTION'
     or submission_row.business_outcome<>'SyntheticRequestSubmittedForOperationalReview'
     or not submission_row.synthetic_automated_checks_complete
     or not submission_row.operational_review_created
     or submission_row.practice_accepted or submission_row.patient_care_queue_entered
     or submission_row.clinician_queue_entered or submission_row.appointment_created
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>26
     or applicant_row.expires_at<=new.authorized_at
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null or patient_row.lifecycle_status<>'active'
     or candidate_row.role<>'provider' or not candidate_row.active
     or candidate_row.facility_id<>new.facility_id
     or right(candidate_row.npi,4)<>new.candidate_npi_last4
     or trim(concat(candidate_row.first_name,' ',candidate_row.last_name))<>new.candidate_display_name
     or exists(select 1 from insurance_records x where lower(x.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from telehealth_coverage_selections x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_coverage_verifications x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_reservations x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_video_sessions x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_consultation_contexts x where x.request_id=new.request_id)
     or (new.decided_by_staff_id is not null and (
       actor_row.id is null or not actor_row.active or actor_row.facility_id<>new.facility_id)) then
    raise exception 'invalid telehealth applicant queue-authorization provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_queue_auth_guard
  on telehealth_applicant_request_queue_authorizations;
create trigger trg_th_app_request_queue_auth_guard
before insert on telehealth_applicant_request_queue_authorizations
for each row execute function enforce_th_app_request_queue_authorization();

drop trigger if exists trg_th_app_request_queue_auth_append
  on telehealth_applicant_request_queue_authorizations;
create trigger trg_th_app_request_queue_auth_append
before update or delete on telehealth_applicant_request_queue_authorizations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_queue_auth_state
  on telehealth_applicant_request_queue_authorizations(
    practice_id,facility_id,authorized_at,request_id);
