-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0048: one request-bound masked insurance-source confirmation. The
-- existing protected payload is referenced, never copied or decrypted, and all
-- current coverage, network, financial, operational, queue, and care gates stay closed.

create table if not exists telehealth_applicant_request_insurance_source_confirmations (
  confirmation_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  request_intake_receipt_id uuid not null unique
    references telehealth_applicant_request_intake_snapshots(receipt_id),
  request_creation_id uuid not null unique
    references telehealth_applicant_request_creations(creation_id),
  insurance_handoff_confirmation_id uuid not null unique
    references telehealth_applicant_insurance_handoff_confirmations(confirmation_id),
  member_insurance_details_id uuid not null unique
    references telehealth_applicant_member_insurance_details(details_id),
  eligibility_result_id uuid not null unique
    references telehealth_applicant_eligibility_results(eligibility_result_id),
  network_determination_id uuid not null unique
    references telehealth_applicant_practice_network_determinations(network_determination_id),
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
  insurance_source_snapshot_fingerprint character(64) not null,
  source_insurance_snapshot_fingerprint character(64) not null,
  payer_display_name text not null,
  product_display_name text not null,
  member_id_last4 character(4) not null,
  group_number_present boolean not null,
  group_number_last4 character(4),
  subscriber_relationship text not null,
  coverage_priority text not null,
  previous_eligibility_business_outcome text not null,
  previous_eligibility_checked_at timestamptz not null,
  previous_eligibility_expires_at timestamptz not null,
  previous_practice_network_business_outcome text not null,
  previous_practice_network_checked_at timestamptz not null,
  previous_practice_network_expires_at timestamptz not null,
  previous_rendering_physician_network_checked boolean not null,
  request_intake_captured_at timestamptz not null,
  context_expires_at timestamptz not null,
  applicant_expires_at timestamptz not null,
  payer_product_confirmed boolean not null,
  masked_member_details_confirmed boolean not null,
  subscriber_relationship_confirmed boolean not null,
  primary_coverage_source_confirmed boolean not null,
  fresh_verification_requested boolean not null,
  evidence_limitations_acknowledged boolean not null,
  synthetic_data_confirmed boolean not null,
  protected_payload_referenced boolean not null default true,
  protected_payload_copied boolean not null default false,
  protected_payload_decrypted boolean not null default false,
  prior_result_reused boolean not null default false,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  canonical_coverage_created boolean not null default false,
  generic_coverage_selected boolean not null default false,
  eligibility_verification_created boolean not null default false,
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
  confirmed_at timestamptz not null,
  constraint uq_th_app_req_ins_source_idempotency unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_ins_source_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_th_app_req_ins_source_versions check (
    applicant_version=26 and source_request_version=5 and resulting_request_version=6
    and source_request_status='Verification' and resulting_request_status='Verification'),
  constraint chk_th_app_req_ins_source_masks check (
    member_id_last4 ~ '^[A-Z0-9-]{4}$'
    and ((group_number_present and group_number_last4 ~ '^[A-Z0-9-]{4}$')
      or (not group_number_present and group_number_last4 is null))),
  constraint chk_th_app_req_ins_source_relationship check (
    subscriber_relationship in ('Self','Spouse','Parent','Other')
    and coverage_priority='Primary'),
  constraint chk_th_app_req_ins_source_history check (
    previous_eligibility_business_outcome in (
      'EligibleBenefitsReported','CoverageInactive','SubscriberNotFound','UnableToDetermine')
    and previous_practice_network_business_outcome in (
      'PracticeInNetworkAcceptingNewPatients','PracticeOutOfNetwork','UnableToDetermine')
    and previous_eligibility_checked_at<previous_eligibility_expires_at
    and previous_practice_network_checked_at<previous_practice_network_expires_at
    and not previous_rendering_physician_network_checked),
  constraint chk_th_app_req_ins_source_freshness check (
    request_intake_captured_at<=confirmed_at and confirmed_at<=context_expires_at
    and confirmed_at<applicant_expires_at),
  constraint chk_th_app_req_ins_source_confirmations check (
    payer_product_confirmed and masked_member_details_confirmed
    and subscriber_relationship_confirmed and primary_coverage_source_confirmed
    and fresh_verification_requested and evidence_limitations_acknowledged
    and synthetic_data_confirmed),
  constraint chk_th_app_req_ins_source_protection check (
    protected_payload_referenced and not protected_payload_copied
    and not protected_payload_decrypted and not prior_result_reused),
  constraint chk_th_app_req_ins_source_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION'
    and policy_version=1
    and evidence_type='APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION'),
  constraint chk_th_app_req_ins_source_hashes check (
    insurance_source_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and source_insurance_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_th_app_req_ins_source_idem check (length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_ins_source_no_consequence check (
    not canonical_coverage_created and not generic_coverage_selected
    and not eligibility_verification_created and not network_verification_created
    and not rendering_physician_network_checked and not coverage_verified
    and not exact_network_confirmed and not estimate_created
    and not financial_acknowledgment_created and not operational_review_created
    and not practice_accepted and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_th_app_request_insurance_source()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  request_row telehealth_requests%rowtype;
  intake_row telehealth_applicant_request_intake_snapshots%rowtype;
  creation_row telehealth_applicant_request_creations%rowtype;
  handoff_row telehealth_applicant_insurance_handoff_confirmations%rowtype;
  member_row telehealth_applicant_member_insurance_details%rowtype;
  eligibility_row telehealth_applicant_eligibility_results%rowtype;
  network_row telehealth_applicant_practice_network_determinations%rowtype;
  patient_row patients%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into intake_row from telehealth_applicant_request_intake_snapshots
  where receipt_id=new.request_intake_receipt_id;
  select * into creation_row from telehealth_applicant_request_creations
  where creation_id=new.request_creation_id;
  select * into handoff_row from telehealth_applicant_insurance_handoff_confirmations
  where confirmation_id=new.insurance_handoff_confirmation_id;
  select * into member_row from telehealth_applicant_member_insurance_details
  where details_id=new.member_insurance_details_id;
  select * into eligibility_row from telehealth_applicant_eligibility_results
  where eligibility_result_id=new.eligibility_result_id;
  select * into network_row from telehealth_applicant_practice_network_determinations
  where network_determination_id=new.network_determination_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null or request_row.request_id is null
     or intake_row.receipt_id is null or creation_row.creation_id is null
     or handoff_row.confirmation_id is null or member_row.details_id is null
     or eligibility_row.eligibility_result_id is null or network_row.network_determination_id is null
     or patient_row.canonical_id is null
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at or applicant_row.expires_at<=new.confirmed_at
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.source_promotion_id<>new.promotion_id
     or request_row.source_practice_review_case_id<>new.practice_review_case_id
     or request_row.source_practice_review_authorization_id<>new.practice_review_authorization_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.status<>'Verification' or request_row.version<>new.resulting_request_version
     or request_row.triage_outcome<>'TelehealthEligible'
     or request_row.ready_at is not null or request_row.appointment_id is not null
     or intake_row.applicant_id<>new.applicant_id or intake_row.request_id<>new.request_id
     or intake_row.request_creation_id<>new.request_creation_id
     or intake_row.promotion_id<>new.promotion_id
     or intake_row.practice_review_case_id<>new.practice_review_case_id
     or intake_row.practice_review_authorization_id<>new.practice_review_authorization_id
     or intake_row.canonical_patient_id<>new.canonical_patient_id
     or intake_row.resulting_request_status<>'Verification'
     or intake_row.resulting_request_version<>new.source_request_version
     or intake_row.captured_at<>new.request_intake_captured_at
     or intake_row.context_expires_at<>new.context_expires_at
     or not intake_row.intake_snapshot_created or not intake_row.request_advanced_to_verification
     or intake_row.coverage_record_created or intake_row.coverage_verified
     or intake_row.exact_network_confirmed or intake_row.operational_review_created
     or creation_row.applicant_id<>new.applicant_id or creation_row.request_id<>new.request_id
     or creation_row.promotion_id<>new.promotion_id
     or creation_row.practice_review_case_id<>new.practice_review_case_id
     or creation_row.practice_review_authorization_id<>new.practice_review_authorization_id
     or handoff_row.applicant_id<>new.applicant_id
     or handoff_row.promotion_id<>new.promotion_id
     or handoff_row.canonical_patient_id<>new.canonical_patient_id
     or handoff_row.member_insurance_details_id<>new.member_insurance_details_id
     or handoff_row.eligibility_result_id<>new.eligibility_result_id
     or handoff_row.network_determination_id<>new.network_determination_id
     or handoff_row.insurance_snapshot_fingerprint<>new.source_insurance_snapshot_fingerprint
     or handoff_row.payer_display_name<>new.payer_display_name
     or handoff_row.product_display_name<>new.product_display_name
     or handoff_row.member_id_last4<>new.member_id_last4
     or handoff_row.group_number_present<>new.group_number_present
     or handoff_row.group_number_last4 is distinct from new.group_number_last4
     or handoff_row.subscriber_relationship<>new.subscriber_relationship
     or handoff_row.coverage_priority<>new.coverage_priority
     or not handoff_row.payer_product_confirmed
     or not handoff_row.masked_member_details_confirmed
     or not handoff_row.subscriber_relationship_confirmed
     or not handoff_row.evidence_limitations_acknowledged
     or not handoff_row.synthetic_data_confirmed
     or handoff_row.coverage_verified or handoff_row.exact_network_confirmed
     or handoff_row.canonical_coverage_created or handoff_row.patient_record_changed
     or member_row.applicant_id<>new.applicant_id
     or member_row.payer_display_name<>new.payer_display_name
     or member_row.product_display_name<>new.product_display_name
     or member_row.member_id_last4<>new.member_id_last4
     or member_row.group_number_present<>new.group_number_present
     or member_row.group_number_last4 is distinct from new.group_number_last4
     or member_row.subscriber_relationship<>new.subscriber_relationship
     or member_row.coverage_priority<>new.coverage_priority
     or member_row.protection_scheme<>'ASP.NET_CORE_DATA_PROTECTION'
     or member_row.protection_version<>1 or length(member_row.protected_payload)<64
     or eligibility_row.applicant_id<>new.applicant_id
     or eligibility_row.member_insurance_details_id<>new.member_insurance_details_id
     or eligibility_row.business_outcome<>new.previous_eligibility_business_outcome
     or eligibility_row.checked_at<>new.previous_eligibility_checked_at
     or eligibility_row.expires_at<>new.previous_eligibility_expires_at
     or network_row.applicant_id<>new.applicant_id
     or network_row.member_insurance_details_id<>new.member_insurance_details_id
     or network_row.eligibility_result_id<>new.eligibility_result_id
     or network_row.business_outcome<>new.previous_practice_network_business_outcome
     or network_row.checked_at<>new.previous_practice_network_checked_at
     or network_row.expires_at<>new.previous_practice_network_expires_at
     or network_row.rendering_physician_network_checked<>new.previous_rendering_physician_network_checked
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null or patient_row.lifecycle_status<>'active'
     or exists(select 1 from insurance_records x where lower(x.patient_id)=lower(new.canonical_patient_id))
     or (select count(*) from telehealth_intake_snapshots x where x.request_id=new.request_id)<>1
     or exists(select 1 from telehealth_coverage_selections x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_coverage_verifications x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_reservations x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_video_sessions x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_consultation_contexts x where x.request_id=new.request_id) then
    raise exception 'invalid telehealth applicant request insurance-source provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_insurance_source_guard
  on telehealth_applicant_request_insurance_source_confirmations;
create trigger trg_th_app_request_insurance_source_guard
before insert on telehealth_applicant_request_insurance_source_confirmations
for each row execute function enforce_th_app_request_insurance_source();

drop trigger if exists trg_th_app_request_insurance_source_append
  on telehealth_applicant_request_insurance_source_confirmations;
create trigger trg_th_app_request_insurance_source_append
before update or delete on telehealth_applicant_request_insurance_source_confirmations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_insurance_source_state
  on telehealth_applicant_request_insurance_source_confirmations(
    practice_id,facility_id,confirmed_at,applicant_id);
