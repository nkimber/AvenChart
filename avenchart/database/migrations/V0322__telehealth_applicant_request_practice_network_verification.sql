-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0050: one fresh request-bound NON_PRODUCTION practice/facility/
-- service network result. No rendering physician is selected or checked, so
-- exact network, coverage, financial, operational, queue, and care gates stay
-- closed.

create table if not exists telehealth_applicant_request_practice_network_verifications (
  verification_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  eligibility_verification_id uuid not null unique
    references telehealth_applicant_request_eligibility_verifications(verification_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  source_request_status text not null,
  resulting_request_status text not null,
  network_snapshot_fingerprint character(64) not null,
  plan_key text not null,
  payer_display_name text not null,
  product_display_name text not null,
  practice_display_name text not null,
  current_location_state_code character(2) not null,
  purpose_category text not null,
  date_of_service date not null,
  service_category text not null,
  eligibility_business_outcome text not null,
  eligibility_checked_at timestamptz not null,
  eligibility_expires_at timestamptz not null,
  adapter_mode text not null,
  compatibility_target text not null,
  dataset_key text not null,
  dataset_version integer not null,
  dataset_effective_from timestamptz not null,
  dataset_effective_through timestamptz not null,
  source_last_updated_at timestamptz not null,
  request_trace_token uuid not null unique,
  response_trace_token uuid not null unique,
  transport_outcome text not null,
  plan_network_match_status text not null,
  practice_affiliation_status text not null,
  service_availability_status text not null,
  new_patient_acceptance_status text not null,
  business_outcome text not null,
  practice_network_checked boolean not null,
  practice_in_network boolean not null,
  new_patients_accepted boolean not null,
  network_reference text,
  organization_reference text,
  location_reference text,
  service_reference text,
  checked_at timestamptz not null,
  expires_at timestamptz not null,
  context_expires_at timestamptz not null,
  applicant_expires_at timestamptz not null,
  synthetic_data_confirmed boolean not null,
  practice_only_scope_acknowledged boolean not null,
  no_guarantee_acknowledged boolean not null,
  current_eligibility_evidence_referenced boolean not null default true,
  eligibility_payload_copied boolean not null default false,
  practice_network_verification_created boolean not null default true,
  rendering_physician_selected boolean not null default false,
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
  verified_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  constraint uq_th_app_req_practice_network_idem unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_practice_network_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10
    and practice_display_name='AvenChart Synthetic Practice'),
  constraint chk_th_app_req_practice_network_versions check (
    applicant_version=26 and source_request_version=7 and resulting_request_version=8
    and source_request_status='Verification' and resulting_request_status='Verification'),
  constraint chk_th_app_req_practice_network_source check (
    plan_key in ('harbor-mutual-hd','blue-valley-standard','pine-state-choice')
    and current_location_state_code in ('GA','CA','FL')
    and purpose_category in ('migraine','sleep')
    and eligibility_business_outcome='EligibleBenefitsReported'
    and eligibility_checked_at<eligibility_expires_at),
  constraint chk_th_app_req_practice_network_adapter check (
    service_category='ProfessionalTelehealthConsultation'
    and adapter_mode='NON_PRODUCTION'
    and compatibility_target='HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0'
    and dataset_key='avenchart-synthetic-practice-network-directory-2026-08'
    and dataset_version=1
    and dataset_effective_from='2026-08-27T00:00:00Z'::timestamptz
    and dataset_effective_through='2026-10-31T23:59:59Z'::timestamptz
    and source_last_updated_at='2026-08-27T00:00:00Z'::timestamptz
    and date_of_service between
      (dataset_effective_from at time zone 'UTC')::date
      and (dataset_effective_through at time zone 'UTC')::date),
  constraint chk_th_app_req_practice_network_outcome_vocabulary check (
    transport_outcome in ('SimulatedAvailable','SimulatedUnavailable')
    and plan_network_match_status in ('Matched','Unknown')
    and practice_affiliation_status in ('InNetwork','OutOfNetwork','Unknown')
    and service_availability_status in ('Included','Excluded','Unknown')
    and new_patient_acceptance_status in ('Accepting','Unknown')
    and business_outcome in ('PracticeInNetworkAcceptingNewPatients',
                             'PracticeOutOfNetwork','UnableToDetermine')),
  constraint chk_th_app_req_practice_network_outcome_mapping check (
    (business_outcome='PracticeInNetworkAcceptingNewPatients'
      and transport_outcome='SimulatedAvailable' and plan_network_match_status='Matched'
      and practice_affiliation_status='InNetwork'
      and service_availability_status='Included'
      and new_patient_acceptance_status='Accepting'
      and practice_network_checked and practice_in_network and new_patients_accepted
      and network_reference='syn-network-harbor-mutual-hd'
      and organization_reference='syn-org-avenchart-practice'
      and location_reference='syn-location-main-telehealth'
      and service_reference='syn-service-professional-telehealth')
    or (business_outcome='PracticeOutOfNetwork'
      and transport_outcome='SimulatedAvailable' and plan_network_match_status='Matched'
      and practice_affiliation_status='OutOfNetwork'
      and service_availability_status='Excluded'
      and new_patient_acceptance_status='Unknown'
      and practice_network_checked and not practice_in_network and not new_patients_accepted
      and network_reference='syn-network-pine-state-choice'
      and organization_reference='syn-org-avenchart-practice'
      and location_reference='syn-location-main-telehealth'
      and service_reference='syn-service-professional-telehealth')
    or (business_outcome='UnableToDetermine'
      and transport_outcome='SimulatedUnavailable' and plan_network_match_status='Unknown'
      and practice_affiliation_status='Unknown'
      and service_availability_status='Unknown'
      and new_patient_acceptance_status='Unknown'
      and not practice_network_checked and not practice_in_network and not new_patients_accepted
      and network_reference is null and organization_reference is null
      and location_reference is null and service_reference is null)),
  constraint chk_th_app_req_practice_network_freshness check (
    checked_at=verified_at and checked_at<=recorded_at
    and expires_at=checked_at+interval '15 minutes'
    and eligibility_checked_at<=checked_at and checked_at<context_expires_at
    and context_expires_at<=eligibility_expires_at
    and checked_at<applicant_expires_at),
  constraint chk_th_app_req_practice_network_ack check (
    synthetic_data_confirmed and practice_only_scope_acknowledged
    and no_guarantee_acknowledged),
  constraint chk_th_app_req_practice_network_boundary check (
    current_eligibility_evidence_referenced and not eligibility_payload_copied
    and practice_network_verification_created
    and not rendering_physician_selected and not rendering_physician_network_checked
    and not exact_network_confirmed),
  constraint chk_th_app_req_practice_network_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION'
    and policy_version=1
    and evidence_type='APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION'),
  constraint chk_th_app_req_practice_network_hashes check (
    network_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_th_app_req_practice_network_idem check (
    length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_practice_network_no_consequence check (
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

create or replace function enforce_th_app_request_practice_network()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  request_row telehealth_requests%rowtype;
  eligibility_row telehealth_applicant_request_eligibility_verifications%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
    where applicant_id=new.applicant_id for key share;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into eligibility_row from telehealth_applicant_request_eligibility_verifications
    where verification_id=new.eligibility_verification_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null or request_row.request_id is null
     or eligibility_row.verification_id is null or patient_row.canonical_id is null
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at or applicant_row.expires_at<=new.checked_at
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.status<>new.source_request_status
     or request_row.version<>new.source_request_version
     or request_row.triage_outcome<>'TelehealthEligible'
     or request_row.complaint_category<>new.purpose_category
     or request_row.ready_at is not null or request_row.appointment_id is not null
     or eligibility_row.request_id<>new.request_id or eligibility_row.applicant_id<>new.applicant_id
     or eligibility_row.canonical_patient_id<>new.canonical_patient_id
     or eligibility_row.resulting_request_version<>new.source_request_version
     or eligibility_row.resulting_request_status<>new.source_request_status
     or eligibility_row.plan_key<>new.plan_key
     or eligibility_row.payer_display_name<>new.payer_display_name
     or eligibility_row.product_display_name<>new.product_display_name
     or eligibility_row.current_location_state_code<>new.current_location_state_code
     or eligibility_row.purpose_category<>new.purpose_category
     or eligibility_row.date_of_service<>new.date_of_service
     or eligibility_row.service_category<>new.service_category
     or eligibility_row.business_outcome<>new.eligibility_business_outcome
     or eligibility_row.checked_at<>new.eligibility_checked_at
     or eligibility_row.expires_at<>new.eligibility_expires_at
     or least(eligibility_row.expires_at,applicant_row.expires_at)<>new.context_expires_at
     or eligibility_row.expires_at<=new.checked_at
     or not eligibility_row.member_matched
     or not eligibility_row.member_eligibility_checked
     or not eligibility_row.member_benefits_checked
     or eligibility_row.eligibility_status<>'Active'
     or eligibility_row.benefit_information_status<>'Reported'
     or not eligibility_row.current_eligibility_evidence_created
     or eligibility_row.raw_transaction_created
     or eligibility_row.canonical_coverage_created or eligibility_row.generic_coverage_selected
     or eligibility_row.network_verification_created
     or eligibility_row.rendering_physician_network_checked
     or eligibility_row.coverage_verified or eligibility_row.exact_network_confirmed
     or eligibility_row.financial_acknowledgment_created
     or eligibility_row.operational_review_created
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null or patient_row.lifecycle_status<>'active'
     or exists(select 1 from insurance_records x
       where lower(x.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from telehealth_coverage_selections x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_coverage_verifications x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_reservations x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_video_sessions x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_consultation_contexts x where x.request_id=new.request_id) then
    raise exception 'invalid telehealth applicant request practice-network provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_practice_network_guard
  on telehealth_applicant_request_practice_network_verifications;
create trigger trg_th_app_request_practice_network_guard
before insert on telehealth_applicant_request_practice_network_verifications
for each row execute function enforce_th_app_request_practice_network();

drop trigger if exists trg_th_app_request_practice_network_append
  on telehealth_applicant_request_practice_network_verifications;
create trigger trg_th_app_request_practice_network_append
before update or delete on telehealth_applicant_request_practice_network_verifications
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_practice_network_state
  on telehealth_applicant_request_practice_network_verifications(
    practice_id,facility_id,verified_at,applicant_id);
