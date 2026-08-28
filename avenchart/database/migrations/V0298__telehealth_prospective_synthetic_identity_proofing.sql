-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0024: one normalized NON_PRODUCTION identity-proofing process
-- fixture. No real evidence, government identifier, biometric, authoritative
-- source, IAL claim, patient, request, queue, external, or care action.

alter table telehealth_prospective_applicants
  drop constraint chk_telehealth_applicant_status;
alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_status check (
    status in ('ContactVerificationPending','IdentityReviewPending',
               'IdentityReviewApproved','ManualReviewRequired',
               'SafetyScreenPassed','SafetyClinicalReviewRequired',
               'SafetyInPersonRequired','SafetyEmergencyRedirect',
               'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
               'MemberInsuranceDetailsRecorded','SyntheticEligibilityRecorded',
               'SyntheticPracticeNetworkRecorded','SyntheticIdentityProofingRecorded',
               'VerificationLocked','Expired'));

alter table telehealth_prospective_applicants
  drop constraint chk_telehealth_applicant_review_state;
alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_review_state check (
    (status = 'IdentityReviewPending'
      and contact_verified_at is not null
      and duplicate_disposition in ('NoCandidate','PossibleMatchManualReview')
      and duplicate_evidence_fingerprint is not null)
    or
    (status in ('IdentityReviewApproved','SafetyScreenPassed',
                'SafetyClinicalReviewRequired','SafetyInPersonRequired',
                'SafetyEmergencyRedirect','VisitPurposeRecorded',
                'PracticeNetworkPrecheckRecorded','MemberInsuranceDetailsRecorded',
                'SyntheticEligibilityRecorded','SyntheticPracticeNetworkRecorded',
                'SyntheticIdentityProofingRecorded')
      and contact_verified_at is not null
      and duplicate_disposition = 'NoCandidate'
      and duplicate_evidence_fingerprint is not null)
    or
    (status = 'ManualReviewRequired'
      and contact_verified_at is not null
      and duplicate_disposition = 'PossibleMatchManualReview'
      and duplicate_evidence_fingerprint is not null)
    or
    (status in ('ContactVerificationPending','VerificationLocked','Expired')
      and contact_verified_at is null
      and duplicate_disposition is null
      and duplicate_evidence_fingerprint is null));

alter table telehealth_applicant_events
  drop constraint chk_telehealth_applicant_event_action;
alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_action check (
    action in ('applicant-created','contact-verified','verification-locked',
               'applicant-expired','identity-review-recorded',
               'prospective-safety-triage-evaluated',
               'prospective-visit-purpose-recorded',
               'prospective-practice-network-precheck-recorded',
               'prospective-member-insurance-details-recorded',
               'prospective-synthetic-eligibility-recorded',
               'prospective-synthetic-practice-network-recorded',
               'prospective-synthetic-identity-proofing-recorded'));
alter table telehealth_applicant_events
  drop constraint chk_telehealth_applicant_event_status;
alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_status check (
    (from_status is null or from_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
      'MemberInsuranceDetailsRecorded','SyntheticEligibilityRecorded',
      'SyntheticPracticeNetworkRecorded','SyntheticIdentityProofingRecorded',
      'VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
      'MemberInsuranceDetailsRecorded','SyntheticEligibilityRecorded',
      'SyntheticPracticeNetworkRecorded','SyntheticIdentityProofingRecorded',
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_identity_proofing_results (
  identity_proofing_result_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  identity_review_decision_id uuid not null
    references telehealth_applicant_identity_review_decisions(decision_id),
  safety_triage_evaluation_id uuid not null unique
    references telehealth_applicant_safety_triage_evaluations(evaluation_id),
  visit_purpose_id uuid not null unique
    references telehealth_applicant_visit_purposes(purpose_id),
  practice_network_precheck_id uuid not null unique
    references telehealth_applicant_practice_network_prechecks(precheck_id),
  member_insurance_details_id uuid not null unique
    references telehealth_applicant_member_insurance_details(details_id),
  eligibility_result_id uuid not null unique
    references telehealth_applicant_eligibility_results(eligibility_result_id),
  network_determination_id uuid not null unique
    references telehealth_applicant_practice_network_determinations(network_determination_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  location_state_code character(2) not null,
  plan_key text not null,
  network_checked_at timestamptz not null,
  network_expires_at timestamptz not null,
  privacy_notice_key text not null,
  privacy_notice_version integer not null,
  privacy_notice_acknowledged boolean not null,
  adapter_mode text not null,
  compatibility_target text not null,
  practice_statement_key text not null,
  practice_statement_version integer not null,
  dataset_key text not null,
  dataset_version integer not null,
  dataset_effective_from timestamptz not null,
  dataset_effective_through timestamptz not null,
  source_last_updated_at timestamptz not null,
  request_trace_token uuid not null unique,
  response_trace_token uuid not null unique,
  proofing_method text not null,
  transport_outcome text not null,
  evidence_collection_status text not null,
  evidence_validation_status text not null,
  attribute_validation_status text not null,
  applicant_verification_status text not null,
  fraud_check_status text not null,
  business_outcome text not null,
  proofing_session_reference text not null unique,
  evidence_package_reference text not null unique,
  checked_at timestamptz not null,
  expires_at timestamptz not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  assurance_level_achieved text not null default 'None',
  identity_evidence_collected boolean not null default false,
  government_identifier_collected boolean not null default false,
  biometric_data_collected boolean not null default false,
  authoritative_source_queried boolean not null default false,
  proofing_notification_sent boolean not null default false,
  redress_case_created boolean not null default false,
  authenticator_bound boolean not null default false,
  identity_proofed boolean not null default false,
  canonical_patient_created boolean not null default false,
  chart_linked boolean not null default false,
  portal_account_created boolean not null default false,
  prospective_intake_completed boolean not null default false,
  consent_created boolean not null default false,
  practice_accepted boolean not null default false,
  coverage_record_created boolean not null default false,
  estimate_created boolean not null default false,
  financial_acknowledgment_created boolean not null default false,
  request_created boolean not null default false,
  queue_enabled boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  care_enabled boolean not null default false,
  prescribing_enabled boolean not null default false,
  billing_enabled boolean not null default false,
  claim_created boolean not null default false,
  communication_enabled boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  recorded_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_identity_proofing_idempotency
    unique (applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_identity_proofing_practice
    check (practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_telehealth_applicant_identity_proofing_version
    check (resulting_applicant_version >= 10),
  constraint chk_telehealth_applicant_identity_proofing_status
    check (resulting_applicant_status='SyntheticIdentityProofingRecorded'),
  constraint chk_telehealth_applicant_identity_proofing_scope
    check (location_state_code in ('GA','CA','FL') and plan_key='harbor-mutual-hd'),
  constraint chk_telehealth_applicant_identity_proofing_notice check (
    privacy_notice_key='SYNTHETIC_IDENTITY_PROOFING_NOTICE'
    and privacy_notice_version=1 and privacy_notice_acknowledged),
  constraint chk_telehealth_applicant_identity_proofing_adapter check (
    adapter_mode='NON_PRODUCTION'
    and compatibility_target='NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY'
    and practice_statement_key='SYNTHETIC_IDENTITY_PRACTICE_STATEMENT'
    and practice_statement_version=1
    and dataset_key='avenchart-synthetic-identity-proofing-2026-08'
    and dataset_version=1
    and dataset_effective_from='2026-08-27T00:00:00Z'::timestamptz
    and dataset_effective_through='2026-10-31T23:59:59Z'::timestamptz
    and source_last_updated_at='2026-08-27T00:00:00Z'::timestamptz
    and source_last_updated_at between dataset_effective_from and dataset_effective_through),
  constraint chk_telehealth_applicant_identity_proofing_outcome check (
    proofing_method='SYNTHETIC_REMOTE_UNATTENDED_NON_BIOMETRIC'
    and transport_outcome='SimulatedCompleted'
    and evidence_collection_status='FixtureReferenceAccepted'
    and evidence_validation_status='ValidatedFixture'
    and attribute_validation_status='ValidatedFixture'
    and applicant_verification_status='VerifiedFixture'
    and fraud_check_status='NoIndicatorFixture'
    and business_outcome='SyntheticProofingPassed'),
  constraint chk_telehealth_applicant_identity_proofing_references check (
    proofing_session_reference ~ '^syn-proof-session-[0-9a-f]{32}$'
    and evidence_package_reference ~ '^syn-evidence-[0-9a-f]{32}$'),
  constraint chk_telehealth_applicant_identity_proofing_freshness check (
    network_checked_at <= checked_at and checked_at < network_expires_at
    and checked_at <= recorded_at
    and expires_at > checked_at and expires_at <= checked_at + interval '15 minutes'
    and checked_at between dataset_effective_from and dataset_effective_through),
  constraint chk_telehealth_applicant_identity_proofing_idempotency
    check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_identity_proofing_fingerprint
    check (command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_identity_proofing_no_consequence check (
    assurance_level_achieved='None'
    and not identity_evidence_collected and not government_identifier_collected
    and not biometric_data_collected and not authoritative_source_queried
    and not proofing_notification_sent and not redress_case_created
    and not authenticator_bound and not identity_proofed
    and not canonical_patient_created and not chart_linked
    and not portal_account_created and not prospective_intake_completed
    and not consent_created and not practice_accepted
    and not coverage_record_created and not estimate_created
    and not financial_acknowledgment_created and not request_created
    and not queue_enabled and not appointment_created and not encounter_created
    and not care_enabled and not prescribing_enabled
    and not billing_enabled and not claim_created
    and not communication_enabled and not integration_enabled
    and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_identity_proofing_result()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  network_row telehealth_applicant_practice_network_determinations%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into network_row from telehealth_applicant_practice_network_determinations
  where network_determination_id=new.network_determination_id
    and applicant_id=new.applicant_id;

  if applicant_row.applicant_id is null
     or network_row.network_determination_id is null
     or network_row.identity_review_decision_id <> new.identity_review_decision_id
     or network_row.safety_triage_evaluation_id <> new.safety_triage_evaluation_id
     or network_row.visit_purpose_id <> new.visit_purpose_id
     or network_row.practice_network_precheck_id <> new.practice_network_precheck_id
     or network_row.member_insurance_details_id <> new.member_insurance_details_id
     or network_row.eligibility_result_id <> new.eligibility_result_id
     or network_row.location_state_code <> new.location_state_code
     or network_row.plan_key <> new.plan_key
     or network_row.eligibility_status <> 'Active'
     or network_row.benefit_information_status <> 'Reported'
     or network_row.eligibility_business_outcome <> 'EligibleBenefitsReported'
     or network_row.business_outcome <> 'PracticeInNetworkAcceptingNewPatients'
     or not network_row.practice_network_checked
     or not network_row.practice_in_network
     or not network_row.new_patients_accepted
     or network_row.checked_at <> new.network_checked_at
     or network_row.expires_at <> new.network_expires_at
     or new.checked_at >= network_row.expires_at
     or applicant_row.practice_id <> new.practice_id
     or applicant_row.facility_id <> new.facility_id
     or applicant_row.version <> new.resulting_applicant_version
     or applicant_row.status <> new.resulting_applicant_status
     or applicant_row.duplicate_disposition <> 'NoCandidate'
     or applicant_row.contact_verified_at is null
     or new.evidence_package_reference <>
        'syn-evidence-' || replace(new.applicant_id::text,'-','') then
    raise exception using
      errcode='P0001',
      message='telehealth_applicant_identity_proofing_result_snapshot_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_identity_proofing_guard
  on telehealth_applicant_identity_proofing_results;
create trigger trg_telehealth_applicant_identity_proofing_guard
before insert on telehealth_applicant_identity_proofing_results
for each row execute function enforce_telehealth_applicant_identity_proofing_result();

drop trigger if exists trg_telehealth_identity_proofing_append_only
  on telehealth_applicant_identity_proofing_results;
create trigger trg_telehealth_identity_proofing_append_only
before update or delete on telehealth_applicant_identity_proofing_results
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_identity_proofing_recorded
  on telehealth_applicant_identity_proofing_results(
    practice_id,facility_id,business_outcome,recorded_at,applicant_id);
