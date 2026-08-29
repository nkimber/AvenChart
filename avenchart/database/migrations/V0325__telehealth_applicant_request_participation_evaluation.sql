-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0053: evaluate one exact server-owned synthetic participation tuple.
-- A synthetic catalog match is not real authority, licensure, credentialing,
-- payer/provider-directory participation, coverage, assignment, or care.

create table if not exists telehealth_applicant_request_participation_evaluations (
  evaluation_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  participation_context_confirmation_id uuid not null unique
    references telehealth_applicant_request_participation_contexts(confirmation_id),
  eligibility_verification_id uuid not null unique
    references telehealth_applicant_request_eligibility_verifications(verification_id),
  practice_network_verification_id uuid not null unique
    references telehealth_applicant_request_practice_network_verifications(verification_id),
  candidate_selection_id uuid not null unique
    references telehealth_applicant_request_rendering_candidate_selections(selection_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  source_request_status text not null,
  resulting_request_status text not null,
  evaluation_snapshot_fingerprint character(64) not null,
  participation_context_snapshot_fingerprint character(64) not null,
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
  candidate_staff_id integer not null references staff(id),
  candidate_display_name text not null,
  candidate_npi_last4 character(4) not null,
  practitioner_reference text not null,
  state_authority_reference text not null,
  billing_organization_reference text not null,
  billing_provider_reference text not null,
  practitioner_role_reference text not null,
  organization_affiliation_reference text not null,
  contract_reference text not null,
  source_mode text not null,
  compatibility_target text not null,
  evaluation_scope text not null,
  business_outcome text not null,
  catalog_key text not null,
  catalog_version integer not null,
  effective_from timestamptz not null,
  effective_through timestamptz not null,
  context_confirmed_at timestamptz not null,
  context_expires_at timestamptz not null,
  result_valid_through timestamptz not null,
  applicant_expires_at timestamptz not null,
  synthetic_data_confirmed boolean not null,
  exact_tuple_scope_acknowledged boolean not null,
  no_coverage_guarantee_acknowledged boolean not null,
  real_verification_still_required_acknowledged boolean not null,
  synthetic_participation_evaluated boolean not null default true,
  synthetic_billing_entity_in_network boolean not null default true,
  synthetic_rendering_provider_in_network boolean not null default true,
  synthetic_plan_network_matched boolean not null default true,
  synthetic_service_location_matched boolean not null default true,
  synthetic_new_patients_accepted boolean not null default true,
  synthetic_exact_network_matched boolean not null default true,
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
  evaluated_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  constraint uq_th_app_req_part_eval_idem unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_part_eval_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10
    and practice_display_name='AvenChart Synthetic Practice'),
  constraint chk_th_app_req_part_eval_versions check (
    applicant_version=26 and source_request_version=10 and resulting_request_version=11
    and source_request_status='Verification' and resulting_request_status='Verification'),
  constraint chk_th_app_req_part_eval_source check (
    source_mode='NON_PRODUCTION'
    and compatibility_target='HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0'
    and evaluation_scope='BILLING_ENTITY_RENDERING_PROVIDER_NETWORK_LOCATION_SERVICE_MODALITY_NEW_PATIENT'
    and plan_key='harbor-mutual-hd' and network_reference='syn-network-harbor-mutual-hd'
    and organization_reference='syn-org-avenchart-practice'
    and billing_organization_reference=organization_reference
    and location_reference='syn-location-main-telehealth'
    and service_reference='syn-service-professional-telehealth'
    and billing_provider_reference='syn-billing-provider-avenchart-8800'
    and current_location_state_code in ('GA','CA','FL')
    and purpose_category in ('migraine','sleep')
    and service_category='ProfessionalTelehealthConsultation'
    and modality='RealTimeAudioVideo'),
  constraint chk_th_app_req_part_eval_matrix check (
    catalog_key='avenchart-synthetic-participation-evaluation-2026-08'
    and catalog_version=1
    and effective_from='2026-08-29T00:00:00Z'::timestamptz
    and effective_through='2026-10-31T23:59:59Z'::timestamptz
    and business_outcome='SyntheticExactParticipationMatched'
    and ((current_location_state_code='GA' and candidate_staff_id=101
          and candidate_npi_last4='8101' and practitioner_reference='syn-practitioner-ga-101'
          and state_authority_reference='syn-authority-ga-101'
          and practitioner_role_reference='syn-practitioner-role-ga-101'
          and organization_affiliation_reference='syn-org-affiliation-harbor-ga'
          and contract_reference='syn-contract-harbor-telehealth-ga')
      or (current_location_state_code='CA' and candidate_staff_id=104
          and candidate_npi_last4='8104' and practitioner_reference='syn-practitioner-ca-104'
          and state_authority_reference='syn-authority-ca-104'
          and practitioner_role_reference='syn-practitioner-role-ca-104'
          and organization_affiliation_reference='syn-org-affiliation-harbor-ca'
          and contract_reference='syn-contract-harbor-telehealth-ca')
      or (current_location_state_code='FL' and candidate_staff_id=107
          and candidate_npi_last4='8107' and practitioner_reference='syn-practitioner-fl-107'
          and state_authority_reference='syn-authority-fl-107'
          and practitioner_role_reference='syn-practitioner-role-fl-107'
          and organization_affiliation_reference='syn-org-affiliation-harbor-fl'
          and contract_reference='syn-contract-harbor-telehealth-fl'))),
  constraint chk_th_app_req_part_eval_freshness check (
    effective_from<=evaluated_at and evaluated_at<effective_through
    and context_confirmed_at<=evaluated_at and evaluated_at<=recorded_at
    and evaluated_at<result_valid_through
    and result_valid_through<=context_expires_at
    and result_valid_through<=applicant_expires_at
    and result_valid_through<=effective_through),
  constraint chk_th_app_req_part_eval_ack check (
    synthetic_data_confirmed and exact_tuple_scope_acknowledged
    and no_coverage_guarantee_acknowledged
    and real_verification_still_required_acknowledged),
  constraint chk_th_app_req_part_eval_result check (
    synthetic_participation_evaluated and synthetic_billing_entity_in_network
    and synthetic_rendering_provider_in_network and synthetic_plan_network_matched
    and synthetic_service_location_matched and synthetic_new_patients_accepted
    and synthetic_exact_network_matched
    and not real_state_authority_verified and not real_credentialing_verified
    and not rendering_physician_assigned and not rendering_physician_network_checked
    and not exact_network_confirmed),
  constraint chk_th_app_req_part_eval_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_EVALUATION'
    and policy_version=1 and evidence_type='APPLICANT_REQUEST_PARTICIPATION_EVALUATION'),
  constraint chk_th_app_req_part_eval_hashes check (
    evaluation_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and participation_context_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'
    and candidate_npi_last4 ~ '^[0-9]{4}$'),
  constraint chk_th_app_req_part_eval_idem check (length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_part_eval_no_consequence check (
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

create or replace function enforce_th_app_request_part_eval()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  request_row telehealth_requests%rowtype;
  context_row telehealth_applicant_request_participation_contexts%rowtype;
  patient_row patients%rowtype;
  candidate_row staff%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
    where applicant_id=new.applicant_id for key share;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into context_row from telehealth_applicant_request_participation_contexts
    where confirmation_id=new.participation_context_confirmation_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;
  select * into candidate_row from staff where id=new.candidate_staff_id;

  if applicant_row.applicant_id is null or request_row.request_id is null
     or context_row.confirmation_id is null or patient_row.canonical_id is null
     or candidate_row.id is null
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at or applicant_row.expires_at<=new.evaluated_at
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.status<>new.source_request_status or request_row.version<>new.source_request_version
     or request_row.triage_outcome<>'TelehealthEligible'
     or request_row.complaint_category<>new.purpose_category
     or request_row.ready_at is not null or request_row.appointment_id is not null
     or context_row.request_id<>new.request_id or context_row.applicant_id<>new.applicant_id
     or context_row.eligibility_verification_id<>new.eligibility_verification_id
     or context_row.practice_network_verification_id<>new.practice_network_verification_id
     or context_row.candidate_selection_id<>new.candidate_selection_id
     or context_row.practice_id<>new.practice_id or context_row.facility_id<>new.facility_id
     or context_row.canonical_patient_id<>new.canonical_patient_id
     or context_row.applicant_version<>new.applicant_version
     or context_row.resulting_request_version<>new.source_request_version
     or context_row.resulting_request_status<>new.source_request_status
     or context_row.context_snapshot_fingerprint<>new.participation_context_snapshot_fingerprint
     or context_row.plan_key<>new.plan_key
     or context_row.payer_display_name<>new.payer_display_name
     or context_row.product_display_name<>new.product_display_name
     or context_row.practice_display_name<>new.practice_display_name
     or context_row.network_reference<>new.network_reference
     or context_row.organization_reference<>new.organization_reference
     or context_row.location_reference<>new.location_reference
     or context_row.service_reference<>new.service_reference
     or context_row.current_location_state_code<>new.current_location_state_code
     or context_row.purpose_category<>new.purpose_category
     or context_row.date_of_service<>new.date_of_service
     or context_row.service_category<>new.service_category
     or context_row.modality<>new.modality
     or context_row.candidate_staff_id<>new.candidate_staff_id
     or context_row.candidate_display_name<>new.candidate_display_name
     or context_row.candidate_npi_last4<>new.candidate_npi_last4
     or context_row.practitioner_reference<>new.practitioner_reference
     or context_row.state_authority_reference<>new.state_authority_reference
     or context_row.billing_organization_reference<>new.billing_organization_reference
     or context_row.billing_provider_reference<>new.billing_provider_reference
     or context_row.practitioner_role_reference<>new.practitioner_role_reference
     or context_row.organization_affiliation_reference<>new.organization_affiliation_reference
     or context_row.contract_reference<>new.contract_reference
     or context_row.effective_from<>new.effective_from
     or context_row.effective_through<>new.effective_through
     or context_row.confirmed_at<>new.context_confirmed_at
     or context_row.context_expires_at<>new.context_expires_at
     or context_row.context_purpose<>'PARTICIPATION_EVALUATION_PREREQUISITES_ONLY'
     or not context_row.participation_evaluation_context_confirmed
     or context_row.real_state_authority_verified or context_row.real_credentialing_verified
     or context_row.rendering_physician_assigned or context_row.rendering_physician_network_checked
     or context_row.exact_network_confirmed or context_row.coverage_verified
     or least(context_row.context_expires_at,applicant_row.expires_at,new.effective_through)
        <>new.result_valid_through
     or context_row.context_expires_at<=new.evaluated_at
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
    raise exception 'invalid telehealth applicant request participation-evaluation provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_part_eval_guard
  on telehealth_applicant_request_participation_evaluations;
create trigger trg_th_app_request_part_eval_guard
before insert on telehealth_applicant_request_participation_evaluations
for each row execute function enforce_th_app_request_part_eval();

drop trigger if exists trg_th_app_request_part_eval_append
  on telehealth_applicant_request_participation_evaluations;
create trigger trg_th_app_request_part_eval_append
before update or delete on telehealth_applicant_request_participation_evaluations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_part_eval_state
  on telehealth_applicant_request_participation_evaluations(
    practice_id,facility_id,evaluated_at,applicant_id);
