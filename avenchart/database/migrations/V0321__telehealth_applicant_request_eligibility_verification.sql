-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0049: one fresh, request-bound NON_PRODUCTION member eligibility
-- result. The protected source is decrypted only in server memory. Exact
-- network, canonical coverage, financial, operational, queue, and care gates
-- remain closed.

create table if not exists telehealth_applicant_request_eligibility_verifications (
  verification_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  insurance_source_confirmation_id uuid not null unique
    references telehealth_applicant_request_insurance_source_confirmations(confirmation_id),
  member_insurance_details_id uuid not null unique
    references telehealth_applicant_member_insurance_details(details_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  source_request_status text not null,
  resulting_request_status text not null,
  eligibility_snapshot_fingerprint character(64) not null,
  insurance_source_snapshot_fingerprint character(64) not null,
  plan_key text not null,
  payer_display_name text not null,
  product_display_name text not null,
  member_id_last4 character(4) not null,
  group_number_present boolean not null,
  group_number_last4 character(4),
  subscriber_relationship text not null,
  coverage_priority text not null,
  current_location_state_code character(2) not null,
  purpose_category text not null,
  date_of_service date not null,
  service_category text not null,
  adapter_mode text not null,
  compatibility_target text not null,
  dataset_key text not null,
  dataset_version integer not null,
  dataset_effective_from timestamptz not null,
  dataset_effective_through timestamptz not null,
  inquiry_trace_token uuid not null unique,
  response_trace_token uuid not null unique,
  transport_outcome text not null,
  member_match_status text not null,
  eligibility_status text not null,
  benefit_information_status text not null,
  business_outcome text not null,
  member_matched boolean not null,
  member_eligibility_checked boolean not null,
  member_benefits_checked boolean not null,
  checked_at timestamptz not null,
  expires_at timestamptz not null,
  context_expires_at timestamptz not null,
  applicant_expires_at timestamptz not null,
  synthetic_data_confirmed boolean not null,
  no_guarantee_acknowledged boolean not null,
  protected_payload_referenced boolean not null default true,
  protected_payload_decrypted_in_server_memory boolean not null default true,
  protected_payload_copied boolean not null default false,
  prior_eligibility_result_reused boolean not null default false,
  current_eligibility_evidence_created boolean not null default true,
  raw_transaction_created boolean not null default false,
  canonical_coverage_created boolean not null default false,
  generic_coverage_selected boolean not null default false,
  network_verification_created boolean not null default false,
  rendering_physician_network_checked boolean not null default false,
  coverage_verified boolean not null default false,
  exact_network_confirmed boolean not null default false,
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
  constraint uq_th_app_req_eligibility_idempotency unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_eligibility_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_th_app_req_eligibility_versions check (
    applicant_version=26 and source_request_version=6 and resulting_request_version=7
    and source_request_status='Verification' and resulting_request_status='Verification'),
  constraint chk_th_app_req_eligibility_source check (
    plan_key in ('harbor-mutual-hd','blue-valley-standard','pine-state-choice')
    and member_id_last4 ~ '^[A-Z0-9-]{4}$'
    and ((group_number_present and group_number_last4 ~ '^[A-Z0-9-]{4}$')
      or (not group_number_present and group_number_last4 is null))
    and subscriber_relationship in ('Self','Spouse','Parent','Other')
    and coverage_priority='Primary'
    and current_location_state_code in ('GA','CA','FL')
    and purpose_category in ('migraine','sleep')),
  constraint chk_th_app_req_eligibility_adapter check (
    service_category='ProfessionalTelehealthConsultation'
    and adapter_mode='NON_PRODUCTION'
    and compatibility_target='ASC_X12N_270_271_005010X279A1'
    and dataset_key='avenchart-synthetic-prospective-eligibility-2026-08'
    and dataset_version=1
    and dataset_effective_from='2026-08-27T00:00:00Z'::timestamptz
    and dataset_effective_through='2026-10-31T23:59:59Z'::timestamptz
    and date_of_service between
      (dataset_effective_from at time zone 'UTC')::date
      and (dataset_effective_through at time zone 'UTC')::date),
  constraint chk_th_app_req_eligibility_outcome_vocabulary check (
    transport_outcome in ('SimulatedAccepted','SimulatedUnavailable')
    and member_match_status in ('Matched','NotMatched','Unknown')
    and eligibility_status in ('Active','Inactive','Unknown')
    and benefit_information_status in ('Reported','NotReported','Unknown')
    and business_outcome in ('EligibleBenefitsReported','CoverageInactive',
                             'SubscriberNotFound','UnableToDetermine')),
  constraint chk_th_app_req_eligibility_outcome_mapping check (
    (business_outcome='EligibleBenefitsReported' and transport_outcome='SimulatedAccepted'
      and member_match_status='Matched' and eligibility_status='Active'
      and benefit_information_status='Reported' and member_matched
      and member_eligibility_checked and member_benefits_checked)
    or (business_outcome='CoverageInactive' and transport_outcome='SimulatedAccepted'
      and member_match_status='Matched' and eligibility_status='Inactive'
      and benefit_information_status='NotReported' and member_matched
      and member_eligibility_checked and not member_benefits_checked)
    or (business_outcome='SubscriberNotFound' and transport_outcome='SimulatedAccepted'
      and member_match_status='NotMatched' and eligibility_status='Unknown'
      and benefit_information_status='NotReported' and not member_matched
      and member_eligibility_checked and not member_benefits_checked)
    or (business_outcome='UnableToDetermine' and transport_outcome='SimulatedUnavailable'
      and member_match_status='Unknown' and eligibility_status='Unknown'
      and benefit_information_status='Unknown' and not member_matched
      and not member_eligibility_checked and not member_benefits_checked)),
  constraint chk_th_app_req_eligibility_freshness check (
    checked_at=verified_at and checked_at<=recorded_at and expires_at=checked_at+interval '15 minutes'
    and checked_at<=context_expires_at and checked_at<applicant_expires_at),
  constraint chk_th_app_req_eligibility_acknowledgments check (
    synthetic_data_confirmed and no_guarantee_acknowledged),
  constraint chk_th_app_req_eligibility_protection check (
    protected_payload_referenced and protected_payload_decrypted_in_server_memory
    and not protected_payload_copied and not prior_eligibility_result_reused
    and current_eligibility_evidence_created and not raw_transaction_created),
  constraint chk_th_app_req_eligibility_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION'
    and policy_version=1 and evidence_type='APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION'),
  constraint chk_th_app_req_eligibility_hashes check (
    eligibility_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and insurance_source_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_th_app_req_eligibility_idem check (length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_eligibility_no_consequence check (
    not canonical_coverage_created and not generic_coverage_selected
    and not network_verification_created and not rendering_physician_network_checked
    and not coverage_verified and not exact_network_confirmed and not estimate_created
    and not financial_acknowledgment_created and not operational_review_created
    and not practice_accepted and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_th_app_request_eligibility()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  request_row telehealth_requests%rowtype;
  source_row telehealth_applicant_request_insurance_source_confirmations%rowtype;
  member_row telehealth_applicant_member_insurance_details%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
    where applicant_id=new.applicant_id for key share;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into source_row from telehealth_applicant_request_insurance_source_confirmations
    where confirmation_id=new.insurance_source_confirmation_id;
  select * into member_row from telehealth_applicant_member_insurance_details
    where details_id=new.member_insurance_details_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null or request_row.request_id is null
     or source_row.confirmation_id is null or member_row.details_id is null
     or patient_row.canonical_id is null
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
     or source_row.request_id<>new.request_id or source_row.applicant_id<>new.applicant_id
     or source_row.member_insurance_details_id<>new.member_insurance_details_id
     or source_row.canonical_patient_id<>new.canonical_patient_id
     or source_row.resulting_request_version<>new.source_request_version
     or source_row.resulting_request_status<>new.source_request_status
     or source_row.insurance_source_snapshot_fingerprint<>new.insurance_source_snapshot_fingerprint
     or source_row.payer_display_name<>new.payer_display_name
     or source_row.product_display_name<>new.product_display_name
     or source_row.member_id_last4<>new.member_id_last4
     or source_row.group_number_present<>new.group_number_present
     or source_row.group_number_last4 is distinct from new.group_number_last4
     or source_row.subscriber_relationship<>new.subscriber_relationship
     or source_row.coverage_priority<>new.coverage_priority
     or source_row.context_expires_at<>new.context_expires_at
     or source_row.context_expires_at<new.checked_at
     or not source_row.fresh_verification_requested
     or not source_row.evidence_limitations_acknowledged or not source_row.synthetic_data_confirmed
     or not source_row.protected_payload_referenced or source_row.protected_payload_copied
     or source_row.protected_payload_decrypted or source_row.prior_result_reused
     or source_row.canonical_coverage_created or source_row.generic_coverage_selected
     or source_row.eligibility_verification_created or source_row.network_verification_created
     or source_row.coverage_verified or source_row.exact_network_confirmed
     or source_row.operational_review_created
     or member_row.applicant_id<>new.applicant_id or member_row.plan_key<>new.plan_key
     or member_row.payer_display_name<>new.payer_display_name
     or member_row.product_display_name<>new.product_display_name
     or member_row.member_id_last4<>new.member_id_last4
     or member_row.group_number_present<>new.group_number_present
     or member_row.group_number_last4 is distinct from new.group_number_last4
     or member_row.subscriber_relationship<>new.subscriber_relationship
     or member_row.coverage_priority<>new.coverage_priority
     or member_row.protection_scheme<>'ASP.NET_CORE_DATA_PROTECTION'
     or member_row.protection_purpose<>'AvenChart.Telehealth.ProspectiveMemberInsuranceDetails.v1'
     or member_row.protection_version<>1 or length(member_row.protected_payload)<64
     or not member_row.details_confirmed or not member_row.synthetic_data_confirmed
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null or patient_row.lifecycle_status<>'active'
     or exists(select 1 from insurance_records x where lower(x.patient_id)=lower(new.canonical_patient_id))
     or exists(select 1 from telehealth_coverage_selections x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_coverage_verifications x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_reservations x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_video_sessions x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_consultation_contexts x where x.request_id=new.request_id) then
    raise exception 'invalid telehealth applicant request eligibility provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_eligibility_guard
  on telehealth_applicant_request_eligibility_verifications;
create trigger trg_th_app_request_eligibility_guard
before insert on telehealth_applicant_request_eligibility_verifications
for each row execute function enforce_th_app_request_eligibility();

drop trigger if exists trg_th_app_request_eligibility_append
  on telehealth_applicant_request_eligibility_verifications;
create trigger trg_th_app_request_eligibility_append
before update or delete on telehealth_applicant_request_eligibility_verifications
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_eligibility_state
  on telehealth_applicant_request_eligibility_verifications(
    practice_id,facility_id,verified_at,applicant_id);
