-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0042: a positive-only, claimant-bound operational authorization for
-- a separately gated future synthetic request-creation step. No request, queue,
-- patient contact, appointment, encounter, consent, care, financial, integration,
-- or external action occurs here.

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
               'SyntheticClinicalInformationInventoryRecorded',
               'SyntheticMedicationInformationRecorded',
               'SyntheticAllergyInformationRecorded',
               'SyntheticHealthHistoryInformationRecorded',
               'SyntheticClinicalInformationSummaryConfirmed',
               'SyntheticPreRequestReadinessAcknowledged',
               'SyntheticPracticeReviewSubmitted','SyntheticPracticeReviewAuthorized',
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
                'SyntheticDevicePreparationRecorded',
                'SyntheticClinicalInformationInventoryRecorded',
                'SyntheticMedicationInformationRecorded',
                'SyntheticAllergyInformationRecorded',
                'SyntheticHealthHistoryInformationRecorded',
                'SyntheticClinicalInformationSummaryConfirmed',
                'SyntheticPreRequestReadinessAcknowledged',
                'SyntheticPracticeReviewSubmitted','SyntheticPracticeReviewAuthorized')
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
               'prospective-device-preparation-recorded',
               'prospective-clinical-information-inventory-recorded',
               'prospective-medication-information-recorded',
               'prospective-allergy-information-recorded',
               'prospective-health-history-information-recorded',
               'prospective-clinical-information-summary-confirmed',
               'prospective-pre-request-readiness-acknowledged',
               'prospective-practice-review-submitted',
               'prospective-practice-review-authorized'));

alter table telehealth_applicant_events
  drop constraint if exists chk_telehealth_applicant_event_status;
alter table telehealth_applicant_events
  add constraint chk_telehealth_applicant_event_status check (
    (from_status is null or from_status in (
      'ContactVerificationPending','IdentityReviewPending','IdentityReviewApproved',
      'ManualReviewRequired','SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect','VisitPurposeRecorded',
      'PracticeNetworkPrecheckRecorded','MemberInsuranceDetailsRecorded',
      'SyntheticEligibilityRecorded','SyntheticPracticeNetworkRecorded',
      'SyntheticIdentityProofingRecorded','SyntheticPromotionAuthorized',
      'SyntheticPromotionDenied','SyntheticPatientPromoted',
      'SyntheticPromotionBlockedPossibleMatch','SyntheticTelehealthNoticeAcknowledged',
      'SyntheticMinimumRegistrationDetailsConfirmed','SyntheticInsuranceDetailsConfirmed',
      'SyntheticCommunicationAccessReadinessRecorded','SyntheticDevicePreparationRecorded',
      'SyntheticClinicalInformationInventoryRecorded','SyntheticMedicationInformationRecorded',
      'SyntheticAllergyInformationRecorded','SyntheticHealthHistoryInformationRecorded',
      'SyntheticClinicalInformationSummaryConfirmed','SyntheticPreRequestReadinessAcknowledged',
      'SyntheticPracticeReviewSubmitted','SyntheticPracticeReviewAuthorized',
      'VerificationLocked','Expired'))
    and to_status in (
      'ContactVerificationPending','IdentityReviewPending','IdentityReviewApproved',
      'ManualReviewRequired','SafetyScreenPassed','SafetyClinicalReviewRequired',
      'SafetyInPersonRequired','SafetyEmergencyRedirect','VisitPurposeRecorded',
      'PracticeNetworkPrecheckRecorded','MemberInsuranceDetailsRecorded',
      'SyntheticEligibilityRecorded','SyntheticPracticeNetworkRecorded',
      'SyntheticIdentityProofingRecorded','SyntheticPromotionAuthorized',
      'SyntheticPromotionDenied','SyntheticPatientPromoted',
      'SyntheticPromotionBlockedPossibleMatch','SyntheticTelehealthNoticeAcknowledged',
      'SyntheticMinimumRegistrationDetailsConfirmed','SyntheticInsuranceDetailsConfirmed',
      'SyntheticCommunicationAccessReadinessRecorded','SyntheticDevicePreparationRecorded',
      'SyntheticClinicalInformationInventoryRecorded','SyntheticMedicationInformationRecorded',
      'SyntheticAllergyInformationRecorded','SyntheticHealthHistoryInformationRecorded',
      'SyntheticClinicalInformationSummaryConfirmed','SyntheticPreRequestReadinessAcknowledged',
      'SyntheticPracticeReviewSubmitted','SyntheticPracticeReviewAuthorized',
      'VerificationLocked','Expired'));

create table if not exists telehealth_practice_review_authorizations (
  authorization_id uuid primary key,
  case_id uuid not null unique
    references telehealth_prospective_practice_review_cases(case_id),
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  submission_id uuid not null unique
    references telehealth_applicant_practice_review_submissions(submission_id),
  readiness_acknowledgment_id uuid not null unique
    references telehealth_applicant_pre_request_readiness_acknowledgments(acknowledgment_id),
  claim_id uuid not null unique references telehealth_practice_review_claims(claim_id),
  source_applicant_version bigint not null,
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  decision text not null,
  rationale_code text not null,
  packet_policy_key text not null,
  packet_policy_version integer not null,
  no_clinical_eligibility_acknowledged boolean not null,
  no_coverage_guarantee_acknowledged boolean not null,
  no_request_or_queue_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  decided_by_staff_id integer references staff(id),
  decided_by_actor_id text not null,
  decided_by_role text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  request_creation_authorized boolean not null default true,
  practice_accepted boolean not null default false,
  patient_contacted boolean not null default false,
  clinician_review_created boolean not null default false,
  telehealth_request_created boolean not null default false,
  patient_care_queue_entered boolean not null default false,
  clinician_queue_entered boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  consent_created boolean not null default false,
  care_authorized boolean not null default false,
  prescribing_enabled boolean not null default false,
  billing_enabled boolean not null default false,
  claim_created boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  decided_at timestamptz not null default now(),
  constraint uq_telehealth_practice_review_authorization_idempotency
    unique(case_id,idempotency_key),
  constraint chk_telehealth_practice_review_authorization_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_telehealth_practice_review_authorization_version check (
    source_applicant_version >= 24
    and resulting_applicant_version=source_applicant_version+1
    and resulting_applicant_status='SyntheticPracticeReviewAuthorized'),
  constraint chk_telehealth_practice_review_authorization_decision check (
    decision='AuthorizedForSyntheticRequestCreation'
    and rationale_code='OperationalPrerequisitesReviewed'),
  constraint chk_telehealth_practice_review_authorization_packet check (
    packet_policy_key='SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET'
    and packet_policy_version=1),
  constraint chk_telehealth_practice_review_authorization_acknowledgments check (
    no_clinical_eligibility_acknowledged
    and no_coverage_guarantee_acknowledged
    and no_request_or_queue_acknowledged),
  constraint chk_telehealth_practice_review_authorization_policy check (
    policy_key='SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION'
    and policy_version=1
    and evidence_type='CURRENT_CLAIMANT_MINIMIZED_PACKET_REVIEW_ONLY'),
  constraint chk_telehealth_practice_review_authorization_actor check (
    length(trim(decided_by_actor_id)) between 1 and 128
    and decided_by_role in ('administrator','frontdesk')
    and (decided_by_role<>'frontdesk' or decided_by_staff_id is not null)),
  constraint chk_telehealth_practice_review_authorization_idempotency check (
    length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_practice_review_authorization_fingerprint check (
    command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_practice_review_authorization_no_consequence check (
    request_creation_authorized and not practice_accepted and not patient_contacted
    and not clinician_review_created and not telehealth_request_created
    and not patient_care_queue_entered and not clinician_queue_entered
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_telehealth_practice_review_authorization()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  case_row telehealth_prospective_practice_review_cases%rowtype;
  submission_row telehealth_applicant_practice_review_submissions%rowtype;
  claim_row telehealth_practice_review_claims%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into case_row from telehealth_prospective_practice_review_cases
  where case_id=new.case_id;
  select * into submission_row from telehealth_applicant_practice_review_submissions
  where submission_id=new.submission_id;
  select * into claim_row from telehealth_practice_review_claims
  where claim_id=new.claim_id;

  if applicant_row.applicant_id is null or case_row.case_id is null
     or submission_row.submission_id is null or claim_row.claim_id is null
     or case_row.applicant_id<>new.applicant_id
     or case_row.practice_id<>new.practice_id or case_row.facility_id<>new.facility_id
     or case_row.canonical_patient_id<>new.canonical_patient_id
     or case_row.readiness_acknowledgment_id<>new.readiness_acknowledgment_id
     or case_row.case_status<>'PendingPracticeReview'
     or case_row.applicant_expires_at<=new.decided_at
     or submission_row.case_id<>new.case_id
     or submission_row.applicant_id<>new.applicant_id
     or submission_row.practice_id<>new.practice_id
     or submission_row.facility_id<>new.facility_id
     or submission_row.canonical_patient_id<>new.canonical_patient_id
     or submission_row.readiness_acknowledgment_id<>new.readiness_acknowledgment_id
     or submission_row.resulting_applicant_version<>new.source_applicant_version
     or submission_row.resulting_applicant_status<>'SyntheticPracticeReviewSubmitted'
     or not submission_row.staff_review_created
     or submission_row.clinician_review_created or submission_row.practice_accepted
     or submission_row.patient_record_changed or submission_row.telehealth_request_created
     or submission_row.patient_care_queue_entered or submission_row.clinician_queue_entered
     or submission_row.appointment_created or submission_row.encounter_created
     or submission_row.care_authorized or submission_row.prescribing_enabled
     or submission_row.billing_enabled or submission_row.claim_created
     or submission_row.integration_enabled or submission_row.external_call_performed
     or claim_row.case_id<>new.case_id or claim_row.practice_id<>new.practice_id
     or claim_row.facility_id<>new.facility_id
     or claim_row.expected_applicant_version<>new.source_applicant_version
     or claim_row.assigned_to_actor_id<>new.decided_by_actor_id
     or claim_row.assigned_to_role<>new.decided_by_role
     or claim_row.lease_expires_at<=new.decided_at
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.expires_at<>case_row.applicant_expires_at then
    raise exception using
      errcode='P0001',
      message='telehealth_practice_review_authorization_snapshot_mismatch';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_telehealth_practice_review_authorization_guard
  on telehealth_practice_review_authorizations;
create trigger trg_telehealth_practice_review_authorization_guard
before insert on telehealth_practice_review_authorizations
for each row execute function enforce_telehealth_practice_review_authorization();

drop trigger if exists trg_telehealth_practice_review_authorizations_append_only
  on telehealth_practice_review_authorizations;
create trigger trg_telehealth_practice_review_authorizations_append_only
before update or delete on telehealth_practice_review_authorizations
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_practice_review_authorization_actor
  on telehealth_practice_review_authorizations(
    practice_id,facility_id,decided_by_actor_id,decided_at,authorization_id);
