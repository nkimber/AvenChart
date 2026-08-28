-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0031: one applicant-owned, client-reported synthetic device-
-- preparation receipt. It contains no identifiers or media and creates no
-- technology-readiness, waiting-room, communication, request, queue, or care capability.

alter table telehealth_prospective_applicants
  drop constraint if exists chk_telehealth_applicant_status;

alter table telehealth_prospective_applicants
  add constraint chk_telehealth_applicant_status check (
    status in ('ContactVerificationPending','IdentityReviewPending',
               'IdentityReviewApproved','ManualReviewRequired',
               'SafetyScreenPassed','SafetyClinicalReviewRequired',
               'SafetyInPersonRequired','SafetyEmergencyRedirect',
               'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
               'MemberInsuranceDetailsRecorded','SyntheticEligibilityRecorded',
               'SyntheticPracticeNetworkRecorded','SyntheticIdentityProofingRecorded',
               'SyntheticPromotionAuthorized','SyntheticPromotionDenied',
               'SyntheticPatientPromoted','SyntheticPromotionBlockedPossibleMatch',
               'SyntheticTelehealthNoticeAcknowledged',
               'SyntheticMinimumRegistrationDetailsConfirmed',
               'SyntheticInsuranceDetailsConfirmed',
               'SyntheticCommunicationAccessReadinessRecorded',
               'SyntheticDevicePreparationRecorded',
               'VerificationLocked','Expired'));

alter table telehealth_prospective_applicants
  drop constraint if exists chk_telehealth_applicant_review_state;

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
                'SyntheticIdentityProofingRecorded','SyntheticPromotionAuthorized',
                'SyntheticPromotionDenied','SyntheticPatientPromoted',
                'SyntheticPromotionBlockedPossibleMatch',
                'SyntheticTelehealthNoticeAcknowledged',
                'SyntheticMinimumRegistrationDetailsConfirmed',
                'SyntheticInsuranceDetailsConfirmed',
                'SyntheticCommunicationAccessReadinessRecorded',
                'SyntheticDevicePreparationRecorded')
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
  drop constraint if exists chk_telehealth_applicant_event_action;

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
               'prospective-synthetic-identity-proofing-recorded',
               'prospective-synthetic-promotion-authorization-recorded',
               'prospective-synthetic-patient-promotion-recorded',
               'prospective-telehealth-notice-acknowledged',
               'prospective-minimum-registration-details-confirmed',
               'prospective-insurance-handoff-confirmed',
               'prospective-communication-access-readiness-recorded',
               'prospective-device-preparation-recorded'));

alter table telehealth_applicant_events
  drop constraint if exists chk_telehealth_applicant_event_status;

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
      'SyntheticPromotionAuthorized','SyntheticPromotionDenied',
      'SyntheticPatientPromoted','SyntheticPromotionBlockedPossibleMatch',
      'SyntheticTelehealthNoticeAcknowledged',
      'SyntheticMinimumRegistrationDetailsConfirmed',
      'SyntheticInsuranceDetailsConfirmed',
      'SyntheticCommunicationAccessReadinessRecorded',
      'SyntheticDevicePreparationRecorded','VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending',
      'IdentityReviewApproved','ManualReviewRequired',
      'SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect',
      'VisitPurposeRecorded','PracticeNetworkPrecheckRecorded',
      'MemberInsuranceDetailsRecorded','SyntheticEligibilityRecorded',
      'SyntheticPracticeNetworkRecorded','SyntheticIdentityProofingRecorded',
      'SyntheticPromotionAuthorized','SyntheticPromotionDenied',
      'SyntheticPatientPromoted','SyntheticPromotionBlockedPossibleMatch',
      'SyntheticTelehealthNoticeAcknowledged',
      'SyntheticMinimumRegistrationDetailsConfirmed',
      'SyntheticInsuranceDetailsConfirmed',
      'SyntheticCommunicationAccessReadinessRecorded',
      'SyntheticDevicePreparationRecorded','VerificationLocked','Expired'));

create table if not exists telehealth_applicant_device_preparations (
  preparation_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  promotion_id uuid not null unique
    references telehealth_applicant_synthetic_promotions(promotion_id),
  canonical_patient_id text not null unique references patients(canonical_id),
  registration_details_confirmation_id uuid not null unique
    references telehealth_applicant_registration_details_confirmations(confirmation_id),
  insurance_handoff_confirmation_id uuid not null unique
    references telehealth_applicant_insurance_handoff_confirmations(confirmation_id),
  safety_evaluation_id uuid not null unique
    references telehealth_applicant_safety_triage_evaluations(evaluation_id),
  communication_access_readiness_id uuid not null unique
    references telehealth_applicant_communication_access_readiness(readiness_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  preparation_snapshot_fingerprint character(64) not null,
  communication_context_fingerprint character(64) not null,
  browser_supported boolean not null,
  camera_available boolean not null,
  microphone_available boolean not null,
  speaker_available boolean not null,
  network_quality text not null,
  client_reported_result_acknowledged boolean not null,
  no_readiness_guarantee_acknowledged boolean not null,
  recheck_before_consultation_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  applicant_expires_at timestamptz not null,
  recorded_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  technology_ready boolean not null default false,
  waiting_room_created boolean not null default false,
  media_session_created boolean not null default false,
  communication_started boolean not null default false,
  support_arrangement_completed boolean not null default false,
  patient_record_changed boolean not null default false,
  portal_access_enabled boolean not null default false,
  intake_completed boolean not null default false,
  legal_consent_established boolean not null default false,
  practice_accepted boolean not null default false,
  financial_record_created boolean not null default false,
  request_created boolean not null default false,
  queue_entered boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  billing_enabled boolean not null default false,
  claim_created boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  constraint uq_telehealth_device_preparation_practice_key
    unique(practice_id,idempotency_key),
  constraint chk_telehealth_device_preparation_result check (
    resulting_applicant_version > 0
    and resulting_applicant_status='SyntheticDevicePreparationRecorded'),
  constraint chk_telehealth_device_preparation_hashes check (
    preparation_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and communication_context_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_device_preparation_capabilities check (
    browser_supported and camera_available and microphone_available and speaker_available),
  constraint chk_telehealth_device_preparation_network check (
    network_quality in ('Unknown','Good')),
  constraint chk_telehealth_device_preparation_acknowledgments check (
    client_reported_result_acknowledged
    and no_readiness_guarantee_acknowledged
    and recheck_before_consultation_acknowledged),
  constraint chk_telehealth_device_preparation_policy check (
    policy_key='SYNTHETIC_APPLICANT_DEVICE_PREPARATION'
    and policy_version=1
    and evidence_type='PROMOTED_PATIENT_DEVICE_PREPARATION_RECEIPT'),
  constraint chk_telehealth_device_preparation_expiry check (
    recorded_at <= applicant_expires_at),
  constraint chk_telehealth_device_preparation_no_consequence check (
    not technology_ready
    and not waiting_room_created
    and not media_session_created
    and not communication_started
    and not support_arrangement_completed
    and not patient_record_changed
    and not portal_access_enabled
    and not intake_completed
    and not legal_consent_established
    and not practice_accepted
    and not financial_record_created
    and not request_created
    and not queue_entered
    and not appointment_created
    and not encounter_created
    and not care_authorized
    and not prescribing_enabled
    and not billing_enabled
    and not claim_created
    and not integration_enabled
    and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_device_preparation()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  promotion_row telehealth_applicant_synthetic_promotions%rowtype;
  patient_row patients%rowtype;
  registration_row telehealth_applicant_registration_details_confirmations%rowtype;
  handoff_row telehealth_applicant_insurance_handoff_confirmations%rowtype;
  safety_row telehealth_applicant_safety_triage_evaluations%rowtype;
  readiness_row telehealth_applicant_communication_access_readiness%rowtype;
begin
  select * into applicant_row
  from telehealth_prospective_applicants where applicant_id=new.applicant_id;
  select * into promotion_row
  from telehealth_applicant_synthetic_promotions where promotion_id=new.promotion_id;
  select * into patient_row
  from patients where canonical_id=new.canonical_patient_id;
  select * into registration_row
  from telehealth_applicant_registration_details_confirmations
  where confirmation_id=new.registration_details_confirmation_id;
  select * into handoff_row
  from telehealth_applicant_insurance_handoff_confirmations
  where confirmation_id=new.insurance_handoff_confirmation_id;
  select * into safety_row
  from telehealth_applicant_safety_triage_evaluations
  where evaluation_id=new.safety_evaluation_id;
  select * into readiness_row
  from telehealth_applicant_communication_access_readiness
  where readiness_id=new.communication_access_readiness_id;

  if applicant_row.applicant_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at
     or applicant_row.expires_at<=now() then
    raise exception using errcode='23514',message='telehealth_device_preparation_applicant_mismatch';
  end if;

  if promotion_row.applicant_id<>new.applicant_id
     or promotion_row.practice_id<>new.practice_id
     or promotion_row.facility_id<>new.facility_id
     or promotion_row.outcome<>'SyntheticPatientCreated'
     or not promotion_row.canonical_patient_created
     or promotion_row.canonical_patient_id<>new.canonical_patient_id
     or patient_row.canonical_id is null
     or patient_row.facility_id<>new.facility_id
     or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null
     or patient_row.first_name<>applicant_row.legal_first_name
     or patient_row.last_name<>applicant_row.legal_last_name
     or patient_row.date_of_birth<>applicant_row.date_of_birth
     or patient_row.email<>applicant_row.email
     or coalesce(nullif(patient_row.phone_cell,''),nullif(patient_row.phone_home,''),patient_row.phone)<>applicant_row.phone
     or patient_row.state<>applicant_row.residence_state_code
     or patient_row.postal_code<>applicant_row.postal_code
     or exists(select 1 from insurance_records insurance
               where lower(insurance.patient_id)=lower(new.canonical_patient_id)) then
    raise exception using errcode='23514',message='telehealth_device_preparation_patient_mismatch';
  end if;

  if registration_row.applicant_id<>new.applicant_id
     or registration_row.practice_id<>new.practice_id
     or registration_row.facility_id<>new.facility_id
     or registration_row.promotion_id<>new.promotion_id
     or registration_row.canonical_patient_id<>new.canonical_patient_id
     or handoff_row.applicant_id<>new.applicant_id
     or handoff_row.practice_id<>new.practice_id
     or handoff_row.facility_id<>new.facility_id
     or handoff_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or handoff_row.promotion_id<>new.promotion_id
     or handoff_row.canonical_patient_id<>new.canonical_patient_id
     or handoff_row.resulting_applicant_status<>'SyntheticInsuranceDetailsConfirmed'
     or handoff_row.policy_key<>'SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
     or handoff_row.policy_version<>1
     or handoff_row.coverage_verified
     or handoff_row.exact_network_confirmed
     or handoff_row.canonical_coverage_created
     or handoff_row.patient_record_changed
     or handoff_row.request_created
     or handoff_row.queue_enabled
     or handoff_row.care_enabled then
    raise exception using errcode='23514',message='telehealth_device_preparation_handoff_mismatch';
  end if;

  if safety_row.applicant_id<>new.applicant_id
     or safety_row.practice_id<>new.practice_id
     or safety_row.facility_id<>new.facility_id
     or safety_row.outcome<>'TelehealthEligible'
     or safety_row.resulting_applicant_status<>'SafetyScreenPassed'
     or not safety_row.current_location_confirmed
     or safety_row.current_location_state_code not in ('GA','CA','FL') then
    raise exception using errcode='23514',message='telehealth_device_preparation_safety_mismatch';
  end if;

  if readiness_row.applicant_id<>new.applicant_id
     or readiness_row.practice_id<>new.practice_id
     or readiness_row.facility_id<>new.facility_id
     or readiness_row.promotion_id<>new.promotion_id
     or readiness_row.canonical_patient_id<>new.canonical_patient_id
     or readiness_row.registration_details_confirmation_id<>new.registration_details_confirmation_id
     or readiness_row.insurance_handoff_confirmation_id<>new.insurance_handoff_confirmation_id
     or readiness_row.safety_evaluation_id<>new.safety_evaluation_id
     or readiness_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or readiness_row.resulting_applicant_status<>'SyntheticCommunicationAccessReadinessRecorded'
     or readiness_row.context_snapshot_fingerprint<>new.communication_context_fingerprint
     or readiness_row.current_location_state_code<>safety_row.current_location_state_code
     or readiness_row.callback_phone_last4<>right(regexp_replace(applicant_row.phone,'[^0-9]','','g'),4)
     or readiness_row.preferred_spoken_language not in ('English','Spanish')
     or not readiness_row.current_location_confirmed
     or not readiness_row.callback_number_confirmed
     or not readiness_row.safe_private_communication_confirmed
     or not readiness_row.disconnection_emergency_plan_acknowledged
     or not readiness_row.synthetic_data_confirmed
     or readiness_row.policy_key<>'SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
     or readiness_row.policy_version<>1
     or readiness_row.interpreter_assigned
     or readiness_row.accessibility_accommodation_arranged
     or readiness_row.communication_arrangement_completed
     or readiness_row.support_request_created
     or readiness_row.technology_readiness_completed
     or readiness_row.patient_record_changed
     or readiness_row.portal_access_enabled
     or readiness_row.intake_completed
     or readiness_row.legal_consent_established
     or readiness_row.practice_accepted
     or readiness_row.request_created
     or readiness_row.queue_enabled
     or readiness_row.care_enabled
     or readiness_row.communication_enabled
     or readiness_row.integration_enabled
     or readiness_row.external_call_performed then
    raise exception using errcode='23514',message='telehealth_device_preparation_readiness_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_device_preparation_guard
  on telehealth_applicant_device_preparations;
create trigger trg_telehealth_applicant_device_preparation_guard
before insert on telehealth_applicant_device_preparations
for each row execute function enforce_telehealth_applicant_device_preparation();

drop trigger if exists trg_telehealth_applicant_device_preparation_append_only
  on telehealth_applicant_device_preparations;
create trigger trg_telehealth_applicant_device_preparation_append_only
before update or delete on telehealth_applicant_device_preparations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_device_preparation_practice
  on telehealth_applicant_device_preparations(practice_id,facility_id,recorded_at);
