-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0051: bind one server-owned synthetic clinician candidate for a
-- later exact network evaluation. This is not an assignment, availability,
-- licensure, credentialing, network, financial, operational, queue, or care
-- decision.

create table if not exists telehealth_applicant_request_rendering_candidate_selections (
  selection_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  eligibility_verification_id uuid not null unique
    references telehealth_applicant_request_eligibility_verifications(verification_id),
  practice_network_verification_id uuid not null unique
    references telehealth_applicant_request_practice_network_verifications(verification_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  source_request_status text not null,
  resulting_request_status text not null,
  candidate_snapshot_fingerprint character(64) not null,
  plan_key text not null,
  payer_display_name text not null,
  product_display_name text not null,
  practice_display_name text not null,
  network_reference text not null,
  organization_reference text not null,
  location_reference text not null,
  service_reference text not null,
  current_location_state_code character(2) not null,
  purpose_category text not null,
  date_of_service date not null,
  service_category text not null,
  modality text not null,
  practice_network_business_outcome text not null,
  practice_network_checked_at timestamptz not null,
  practice_network_expires_at timestamptz not null,
  candidate_staff_id integer not null references staff(id),
  candidate_display_name text not null,
  candidate_npi_last4 character(4) not null,
  practitioner_reference text not null,
  state_authority_reference text not null,
  candidate_purpose text not null,
  catalog_key text not null,
  catalog_version integer not null,
  catalog_effective_from timestamptz not null,
  catalog_effective_through timestamptz not null,
  context_expires_at timestamptz not null,
  applicant_expires_at timestamptz not null,
  synthetic_data_confirmed boolean not null,
  candidate_only_scope_acknowledged boolean not null,
  no_assignment_acknowledged boolean not null,
  network_check_still_required_acknowledged boolean not null,
  candidate_selected_for_network_evaluation boolean not null default true,
  rendering_physician_assigned boolean not null default false,
  rendering_physician_network_checked boolean not null default false,
  exact_network_confirmed boolean not null default false,
  canonical_coverage_created boolean not null default false,
  generic_coverage_selected boolean not null default false,
  coverage_verified boolean not null default false,
  estimate_created boolean not null default false,
  financial_acknowledgment_created boolean not null default false,
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
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  selected_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  constraint uq_th_app_req_render_candidate_idem unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_render_candidate_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10
    and practice_display_name='AvenChart Synthetic Practice'),
  constraint chk_th_app_req_render_candidate_versions check (
    applicant_version=26 and source_request_version=8 and resulting_request_version=9
    and source_request_status='Verification' and resulting_request_status='Verification'),
  constraint chk_th_app_req_render_candidate_source check (
    plan_key='harbor-mutual-hd' and network_reference='syn-network-harbor-mutual-hd'
    and organization_reference='syn-org-avenchart-practice'
    and location_reference='syn-location-main-telehealth'
    and service_reference='syn-service-professional-telehealth'
    and current_location_state_code in ('GA','CA','FL')
    and purpose_category in ('migraine','sleep')
    and service_category='ProfessionalTelehealthConsultation'
    and modality='RealTimeAudioVideo'
    and practice_network_business_outcome='PracticeInNetworkAcceptingNewPatients'),
  constraint chk_th_app_req_render_candidate_roster check (
    catalog_key='avenchart-synthetic-rendering-candidate-roster-2026-08'
    and catalog_version=1
    and catalog_effective_from='2026-08-29T00:00:00Z'::timestamptz
    and catalog_effective_through='2026-10-31T23:59:59Z'::timestamptz
    and candidate_purpose='NETWORK_EVALUATION_ONLY'
    and ((current_location_state_code='GA' and candidate_staff_id=101
          and candidate_npi_last4='8101' and practitioner_reference='syn-practitioner-ga-101'
          and state_authority_reference='syn-authority-ga-101')
      or (current_location_state_code='CA' and candidate_staff_id=104
          and candidate_npi_last4='8104' and practitioner_reference='syn-practitioner-ca-104'
          and state_authority_reference='syn-authority-ca-104')
      or (current_location_state_code='FL' and candidate_staff_id=107
          and candidate_npi_last4='8107' and practitioner_reference='syn-practitioner-fl-107'
          and state_authority_reference='syn-authority-fl-107'))),
  constraint chk_th_app_req_render_candidate_freshness check (
    practice_network_checked_at<practice_network_expires_at
    and selected_at<=recorded_at and practice_network_checked_at<=selected_at
    and selected_at<context_expires_at
    and context_expires_at<=practice_network_expires_at
    and context_expires_at<=applicant_expires_at
    and context_expires_at<=catalog_effective_through
    and catalog_effective_from<=selected_at),
  constraint chk_th_app_req_render_candidate_ack check (
    synthetic_data_confirmed and candidate_only_scope_acknowledged
    and no_assignment_acknowledged and network_check_still_required_acknowledged),
  constraint chk_th_app_req_render_candidate_boundary check (
    candidate_selected_for_network_evaluation and not rendering_physician_assigned
    and not rendering_physician_network_checked and not exact_network_confirmed),
  constraint chk_th_app_req_render_candidate_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_RENDERING_CANDIDATE_SELECTION'
    and policy_version=1
    and evidence_type='APPLICANT_REQUEST_RENDERING_CANDIDATE_SELECTION'),
  constraint chk_th_app_req_render_candidate_hashes check (
    candidate_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'
    and candidate_npi_last4 ~ '^[0-9]{4}$'),
  constraint chk_th_app_req_render_candidate_idem check (
    length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_render_candidate_no_consequence check (
    not canonical_coverage_created and not generic_coverage_selected
    and not coverage_verified and not estimate_created
    and not financial_acknowledgment_created and not operational_review_created
    and not practice_accepted and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_th_app_request_render_candidate()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  request_row telehealth_requests%rowtype;
  eligibility_row telehealth_applicant_request_eligibility_verifications%rowtype;
  network_row telehealth_applicant_request_practice_network_verifications%rowtype;
  patient_row patients%rowtype;
  candidate_row staff%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
    where applicant_id=new.applicant_id for key share;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into eligibility_row from telehealth_applicant_request_eligibility_verifications
    where verification_id=new.eligibility_verification_id;
  select * into network_row from telehealth_applicant_request_practice_network_verifications
    where verification_id=new.practice_network_verification_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;
  select * into candidate_row from staff where id=new.candidate_staff_id;

  if applicant_row.applicant_id is null or request_row.request_id is null
     or eligibility_row.verification_id is null or network_row.verification_id is null
     or patient_row.canonical_id is null or candidate_row.id is null
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at or applicant_row.expires_at<=new.selected_at
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.status<>new.source_request_status or request_row.version<>new.source_request_version
     or request_row.triage_outcome<>'TelehealthEligible'
     or request_row.complaint_category<>new.purpose_category
     or request_row.ready_at is not null or request_row.appointment_id is not null
     or eligibility_row.request_id<>new.request_id or eligibility_row.applicant_id<>new.applicant_id
     or eligibility_row.canonical_patient_id<>new.canonical_patient_id
     or eligibility_row.business_outcome<>'EligibleBenefitsReported'
     or not eligibility_row.member_matched or not eligibility_row.member_eligibility_checked
     or not eligibility_row.member_benefits_checked or eligibility_row.eligibility_status<>'Active'
     or eligibility_row.benefit_information_status<>'Reported'
     or network_row.request_id<>new.request_id or network_row.applicant_id<>new.applicant_id
     or network_row.eligibility_verification_id<>new.eligibility_verification_id
     or network_row.canonical_patient_id<>new.canonical_patient_id
     or network_row.resulting_request_version<>new.source_request_version
     or network_row.resulting_request_status<>new.source_request_status
     or network_row.plan_key<>new.plan_key
     or network_row.payer_display_name<>new.payer_display_name
     or network_row.product_display_name<>new.product_display_name
     or network_row.practice_display_name<>new.practice_display_name
     or network_row.network_reference<>new.network_reference
     or network_row.organization_reference<>new.organization_reference
     or network_row.location_reference<>new.location_reference
     or network_row.service_reference<>new.service_reference
     or network_row.current_location_state_code<>new.current_location_state_code
     or network_row.purpose_category<>new.purpose_category
     or network_row.date_of_service<>new.date_of_service
     or network_row.service_category<>new.service_category
     or network_row.business_outcome<>new.practice_network_business_outcome
     or network_row.checked_at<>new.practice_network_checked_at
     or network_row.expires_at<>new.practice_network_expires_at
     or not network_row.practice_network_checked or not network_row.practice_in_network
     or not network_row.new_patients_accepted
     or network_row.rendering_physician_selected or network_row.rendering_physician_network_checked
     or network_row.exact_network_confirmed or network_row.coverage_verified
     or least(network_row.expires_at,applicant_row.expires_at,new.catalog_effective_through)
        <>new.context_expires_at
     or network_row.expires_at<=new.selected_at
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
    raise exception 'invalid telehealth applicant request rendering-candidate provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_render_candidate_guard
  on telehealth_applicant_request_rendering_candidate_selections;
create trigger trg_th_app_request_render_candidate_guard
before insert on telehealth_applicant_request_rendering_candidate_selections
for each row execute function enforce_th_app_request_render_candidate();

drop trigger if exists trg_th_app_request_render_candidate_append
  on telehealth_applicant_request_rendering_candidate_selections;
create trigger trg_th_app_request_render_candidate_append
before update or delete on telehealth_applicant_request_rendering_candidate_selections
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_render_candidate_state
  on telehealth_applicant_request_rendering_candidate_selections(
    practice_id,facility_id,selected_at,applicant_id);
