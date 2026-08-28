-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0026: one disabled synthetic atomic patient-shell promotion or a
-- privacy-safe duplicate block. No existing patient is linked or disclosed.

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
                'SyntheticPromotionBlockedPossibleMatch')
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
               'prospective-synthetic-patient-promotion-recorded'));

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
      'VerificationLocked','Expired'))
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
      'VerificationLocked','Expired'));

create table if not exists telehealth_applicant_synthetic_promotions (
  promotion_id uuid primary key,
  applicant_id uuid not null unique
    references telehealth_prospective_applicants(applicant_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  authorization_decision_id uuid not null unique
    references telehealth_applicant_promotion_authorization_decisions(decision_id),
  resulting_applicant_version bigint not null,
  resulting_applicant_status text not null,
  command text not null,
  outcome text not null,
  possible_match_detected boolean not null,
  canonical_patient_id text references patients(canonical_id),
  canonical_legacy_pid integer,
  canonical_patient_created boolean not null,
  canonical_patient_creation_acknowledged boolean not null,
  no_portal_no_care_acknowledged boolean not null,
  reason text not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  assurance_level_achieved text not null,
  identity_proofed boolean not null,
  executed_by_staff_id integer references staff(id),
  executed_by_actor_id text not null,
  executed_by_role text not null,
  executed_at timestamptz not null default now(),
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  portal_account_created boolean not null default false,
  portal_session_created boolean not null default false,
  external_identity_mapping_created boolean not null default false,
  chart_content_created boolean not null default false,
  prospective_intake_completed boolean not null default false,
  consent_created boolean not null default false,
  practice_accepted boolean not null default false,
  insurance_created boolean not null default false,
  coverage_created boolean not null default false,
  financial_record_created boolean not null default false,
  request_created boolean not null default false,
  queue_enabled boolean not null default false,
  appointment_created boolean not null default false,
  encounter_created boolean not null default false,
  care_enabled boolean not null default false,
  prescribing_enabled boolean not null default false,
  claim_created boolean not null default false,
  communication_enabled boolean not null default false,
  integration_enabled boolean not null default false,
  external_call_performed boolean not null default false,
  constraint uq_telehealth_applicant_synthetic_promotion_idempotency
    unique(applicant_id,idempotency_key),
  constraint chk_telehealth_applicant_synthetic_promotion_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_telehealth_applicant_synthetic_promotion_version check (
    resulting_applicant_version >= 2),
  constraint chk_telehealth_applicant_synthetic_promotion_command check (
    command='PromoteAuthorizedSyntheticApplicant'),
  constraint chk_telehealth_applicant_synthetic_promotion_outcome check (
    (outcome='SyntheticPatientCreated'
      and resulting_applicant_status='SyntheticPatientPromoted'
      and not possible_match_detected
      and canonical_patient_created
      and canonical_patient_id is not null
      and canonical_legacy_pid is not null)
    or
    (outcome='BlockedPossiblePatientMatch'
      and resulting_applicant_status='SyntheticPromotionBlockedPossibleMatch'
      and possible_match_detected
      and not canonical_patient_created
      and canonical_patient_id is null
      and canonical_legacy_pid is null)),
  constraint chk_telehealth_applicant_synthetic_promotion_acknowledgments check (
    canonical_patient_creation_acknowledged and no_portal_no_care_acknowledged),
  constraint chk_telehealth_applicant_synthetic_promotion_reason check (
    length(btrim(reason)) between 10 and 1000),
  constraint chk_telehealth_applicant_synthetic_promotion_policy check (
    policy_key='SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION'
    and policy_version=1
    and evidence_type='AUTHORIZED_SYNTHETIC_APPLICANT_AND_CURRENT_DUPLICATE_RECHECK'
    and assurance_level_achieved='None'
    and not identity_proofed),
  constraint chk_telehealth_applicant_synthetic_promotion_actor check (
    length(btrim(executed_by_actor_id)) between 1 and 200
    and executed_by_role='administrator'),
  constraint chk_telehealth_applicant_synthetic_promotion_idempotency check (
    length(idempotency_key) between 8 and 200),
  constraint chk_telehealth_applicant_synthetic_promotion_fingerprint check (
    command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_telehealth_applicant_synthetic_promotion_no_downstream check (
    not portal_account_created
    and not portal_session_created
    and not external_identity_mapping_created
    and not chart_content_created
    and not prospective_intake_completed
    and not consent_created
    and not practice_accepted
    and not insurance_created
    and not coverage_created
    and not financial_record_created
    and not request_created
    and not queue_enabled
    and not appointment_created
    and not encounter_created
    and not care_enabled
    and not prescribing_enabled
    and not claim_created
    and not communication_enabled
    and not integration_enabled
    and not external_call_performed));

create or replace function enforce_telehealth_applicant_synthetic_promotion()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  authorization_row telehealth_applicant_promotion_authorization_decisions%rowtype;
  patient_row patients%rowtype;
  expected_patient_id text;
  current_match boolean;
begin
  select * into applicant_row
  from telehealth_prospective_applicants
  where applicant_id=new.applicant_id;

  select * into authorization_row
  from telehealth_applicant_promotion_authorization_decisions
  where decision_id=new.authorization_decision_id;

  if applicant_row.applicant_id is null
     or authorization_row.decision_id is null
     or authorization_row.applicant_id<>new.applicant_id
     or authorization_row.practice_id<>new.practice_id
     or authorization_row.facility_id<>new.facility_id
     or authorization_row.decision<>'AuthorizedForSyntheticPromotion'
     or authorization_row.resulting_applicant_status<>'SyntheticPromotionAuthorized'
     or authorization_row.resulting_applicant_version<>new.resulting_applicant_version-1
     or authorization_row.assurance_level_achieved<>'None'
     or authorization_row.proofing_identity_proofed
     or applicant_row.practice_id<>new.practice_id
     or applicant_row.facility_id<>new.facility_id
     or applicant_row.version<>new.resulting_applicant_version
     or applicant_row.status<>new.resulting_applicant_status then
    raise exception using
      errcode='23514',
      message='telehealth_applicant_synthetic_promotion_provenance_mismatch';
  end if;

  select exists(
    select 1
    from patients patient
    where patient.facility_id=new.facility_id
      and patient.merged_into_patient_id is null
      and (new.canonical_patient_id is null or patient.canonical_id<>new.canonical_patient_id)
      and (
        (lower(btrim(patient.first_name))=lower(applicant_row.legal_first_name)
         and lower(btrim(patient.last_name))=lower(applicant_row.legal_last_name)
         and patient.date_of_birth=applicant_row.date_of_birth)
        or
        (patient.date_of_birth=applicant_row.date_of_birth
         and lower(btrim(coalesce(patient.email,'')))=applicant_row.email)
        or
        (patient.date_of_birth=applicant_row.date_of_birth
         and right(regexp_replace(coalesce(nullif(patient.phone_cell,''),
                                            nullif(patient.phone_home,''),
                                            patient.phone,''),
                                  '[^0-9]','','g'),10)=right(applicant_row.phone,10))))
  into current_match;

  if current_match<>new.possible_match_detected then
    raise exception using
      errcode='23514',
      message='telehealth_applicant_synthetic_promotion_duplicate_snapshot_mismatch';
  end if;

  if new.canonical_patient_created then
    expected_patient_id := 'TH-PAT-' || upper(replace(new.applicant_id::text,'-',''));
    select * into patient_row from patients where canonical_id=new.canonical_patient_id;
    if patient_row.canonical_id is null
       or patient_row.canonical_id<>expected_patient_id
       or patient_row.pubpid<>expected_patient_id
       or patient_row.legacy_pid<>new.canonical_legacy_pid
       or patient_row.first_name<>applicant_row.legal_first_name
       or patient_row.last_name<>applicant_row.legal_last_name
       or patient_row.date_of_birth<>applicant_row.date_of_birth
       or patient_row.email<>applicant_row.email
       or patient_row.phone<>applicant_row.phone
       or patient_row.phone_home<>applicant_row.phone
       or patient_row.phone_cell<>applicant_row.phone
       or patient_row.state<>applicant_row.residence_state_code
       or patient_row.postal_code<>applicant_row.postal_code
       or patient_row.facility_id<>new.facility_id
       or patient_row.provider_id is not null
       or patient_row.portal_enabled
       or patient_row.purpose<>'synthetic telehealth prospective promotion' then
      raise exception using
        errcode='23514',
        message='telehealth_applicant_synthetic_promotion_patient_mismatch';
    end if;
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_applicant_synthetic_promotion_guard
  on telehealth_applicant_synthetic_promotions;
create trigger trg_telehealth_applicant_synthetic_promotion_guard
before insert on telehealth_applicant_synthetic_promotions
for each row execute function enforce_telehealth_applicant_synthetic_promotion();

drop trigger if exists trg_telehealth_applicant_synthetic_promotions_append_only
  on telehealth_applicant_synthetic_promotions;
create trigger trg_telehealth_applicant_synthetic_promotions_append_only
before update or delete on telehealth_applicant_synthetic_promotions
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_telehealth_applicant_synthetic_promotion_queue
  on telehealth_prospective_applicants(practice_id,facility_id,status,updated_at,applicant_id)
  where status='SyntheticPromotionAuthorized';
