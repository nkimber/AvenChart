-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0043: one applicant-bound Draft request shell after exact positive
-- practice-review authorization. No queue, appointment, encounter, consent,
-- care, financial, integration, or external consequence is created here.

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
               'SyntheticRequestCreated','VerificationLocked','Expired'));

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
                'SyntheticPracticeReviewSubmitted','SyntheticPracticeReviewAuthorized',
                'SyntheticRequestCreated')
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
               'prospective-practice-review-authorized',
               'prospective-telehealth-request-created'));

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
      'SyntheticRequestCreated','VerificationLocked','Expired'))
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
      'SyntheticRequestCreated','VerificationLocked','Expired'));

alter table telehealth_request_events
  drop constraint if exists chk_telehealth_event_actor;
alter table telehealth_request_events
  add constraint chk_telehealth_event_actor check (
    actor_type in ('patient','applicant','administrator','physician','system'));

alter table telehealth_requests
  add column if not exists source_applicant_id uuid
    references telehealth_prospective_applicants(applicant_id),
  add column if not exists source_promotion_id uuid
    references telehealth_applicant_synthetic_promotions(promotion_id),
  add column if not exists source_practice_review_case_id uuid
    references telehealth_prospective_practice_review_cases(case_id),
  add column if not exists source_practice_review_authorization_id uuid
    references telehealth_practice_review_authorizations(authorization_id);

alter table telehealth_requests
  drop constraint if exists chk_telehealth_request_applicant_provenance;
alter table telehealth_requests
  add constraint chk_telehealth_request_applicant_provenance check (
    (source_applicant_id is null and source_promotion_id is null
      and source_practice_review_case_id is null
      and source_practice_review_authorization_id is null)
    or
    (source_applicant_id is not null and source_promotion_id is not null
      and source_practice_review_case_id is not null
      and source_practice_review_authorization_id is not null));

create unique index if not exists uq_telehealth_request_source_applicant
  on telehealth_requests(source_applicant_id) where source_applicant_id is not null;
create unique index if not exists uq_telehealth_request_source_authorization
  on telehealth_requests(source_practice_review_authorization_id)
  where source_practice_review_authorization_id is not null;

create table if not exists telehealth_applicant_request_creations (
  creation_id uuid primary key,
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  promotion_id uuid not null unique
    references telehealth_applicant_synthetic_promotions(promotion_id),
  practice_review_case_id uuid not null unique
    references telehealth_prospective_practice_review_cases(case_id),
  practice_review_authorization_id uuid not null unique
    references telehealth_practice_review_authorizations(authorization_id),
  source_applicant_version bigint not null,
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  complaint_category text not null,
  request_status text not null,
  request_version bigint not null,
  authorization_policy_version integer not null,
  request_creation_confirmed boolean not null,
  no_queue_or_care_acknowledged boolean not null,
  urgent_or_worsening_action_acknowledged boolean not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  telehealth_request_created boolean not null default true,
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
  created_at timestamptz not null default now(),
  constraint uq_telehealth_applicant_request_creation_idempotency
    unique(applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_request_creation_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_telehealth_applicant_request_creation_version check (
    source_applicant_version >= 25
    and resulting_applicant_version=source_applicant_version+1
    and resulting_applicant_status='SyntheticRequestCreated'),
  constraint chk_telehealth_applicant_request_creation_request check (
    complaint_category in ('migraine','sleep')
    and request_status='Draft' and request_version=1),
  constraint chk_telehealth_applicant_request_creation_acknowledgments check (
    authorization_policy_version=1 and request_creation_confirmed
    and no_queue_or_care_acknowledged
    and urgent_or_worsening_action_acknowledged),
  constraint chk_telehealth_applicant_request_creation_policy check (
    policy_key='SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION'
    and policy_version=1
    and evidence_type='APPLICANT_CONFIRMATION_WITH_AUTHORIZED_SOURCE_PROVENANCE'),
  constraint chk_telehealth_applicant_request_creation_idempotency check (
    length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_applicant_request_creation_fingerprint check (
    command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_request_creation_no_consequence check (
    telehealth_request_created and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_telehealth_applicant_request_creation()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  request_row telehealth_requests%rowtype;
  authorization_row telehealth_practice_review_authorizations%rowtype;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into authorization_row from telehealth_practice_review_authorizations
  where authorization_id=new.practice_review_authorization_id;

  if applicant_row.applicant_id is null or request_row.request_id is null
     or authorization_row.authorization_id is null
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>new.resulting_applicant_status
     or applicant_row.version<>new.resulting_applicant_version
     or request_row.practice_id<>new.practice_id
     or request_row.facility_id<>new.facility_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.status<>new.request_status
     or request_row.version<>new.request_version
     or request_row.complaint_category<>new.complaint_category
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.source_promotion_id<>new.promotion_id
     or request_row.source_practice_review_case_id<>new.practice_review_case_id
     or request_row.source_practice_review_authorization_id<>new.practice_review_authorization_id
     or request_row.create_idempotency_key<>new.idempotency_key
     or request_row.create_fingerprint<>new.command_fingerprint
     or request_row.created_at<>new.created_at
     or authorization_row.applicant_id<>new.applicant_id
     or authorization_row.practice_id<>new.practice_id
     or authorization_row.facility_id<>new.facility_id
     or authorization_row.canonical_patient_id<>new.canonical_patient_id
     or authorization_row.case_id<>new.practice_review_case_id
     or authorization_row.resulting_applicant_version<>new.source_applicant_version
     or authorization_row.resulting_applicant_status<>'SyntheticPracticeReviewAuthorized'
     or authorization_row.decision<>'AuthorizedForSyntheticRequestCreation'
     or authorization_row.policy_key<>'SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION'
     or authorization_row.policy_version<>new.authorization_policy_version
     or not authorization_row.request_creation_authorized
     or authorization_row.telehealth_request_created
     or authorization_row.patient_care_queue_entered
     or authorization_row.clinician_queue_entered
     or authorization_row.appointment_created or authorization_row.encounter_created
     or authorization_row.care_authorized or authorization_row.prescribing_enabled
     or authorization_row.billing_enabled or authorization_row.claim_created
     or authorization_row.integration_enabled or authorization_row.external_call_performed then
    raise exception 'invalid telehealth applicant request-creation provenance';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_request_creation_guard
  on telehealth_applicant_request_creations;
create trigger trg_telehealth_applicant_request_creation_guard
before insert on telehealth_applicant_request_creations
for each row execute function enforce_telehealth_applicant_request_creation();

drop trigger if exists trg_telehealth_applicant_request_creations_append_only
  on telehealth_applicant_request_creations;
create trigger trg_telehealth_applicant_request_creations_append_only
before update or delete on telehealth_applicant_request_creations
for each row execute function reject_telehealth_evidence_mutation();

create or replace function protect_telehealth_request_applicant_provenance()
returns trigger
language plpgsql
as $$
begin
  if tg_op='DELETE' and old.source_applicant_id is not null then
    raise exception 'applicant request provenance is immutable';
  end if;
  if tg_op='UPDATE' and (
       old.source_applicant_id is distinct from new.source_applicant_id
       or old.source_promotion_id is distinct from new.source_promotion_id
       or old.source_practice_review_case_id is distinct from new.source_practice_review_case_id
       or old.source_practice_review_authorization_id is distinct from new.source_practice_review_authorization_id) then
    raise exception 'applicant request provenance is immutable';
  end if;
  return case when tg_op='DELETE' then old else new end;
end;
$$;

drop trigger if exists trg_telehealth_request_applicant_provenance
  on telehealth_requests;
create trigger trg_telehealth_request_applicant_provenance
before update or delete on telehealth_requests
for each row execute function protect_telehealth_request_applicant_provenance();
