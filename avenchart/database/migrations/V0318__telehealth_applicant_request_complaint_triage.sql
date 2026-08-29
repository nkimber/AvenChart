-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0046: deterministic applicant-owned complaint-specific synthetic
-- triage with ordered rule evidence and an explicit false-only production
-- publication gate. The fixture is unapproved clinical content.

alter table telehealth_requests
  drop constraint chk_telehealth_requests_status;
alter table telehealth_requests
  add constraint chk_telehealth_requests_status check (
    status in ('Draft','LocationConfirmed','SafetyScreening',
               'EmergencyRedirected','InPersonRecommended','Unsupported','ClinicalReview',
               'Intake','Verification','OperationalReview','Redirected','Queued',
               'Reserved','Connecting','InConsultation','WrapUp'));

alter table telehealth_requests
  drop constraint chk_telehealth_requests_triage;
alter table telehealth_requests
  add constraint chk_telehealth_requests_triage check (
    triage_outcome is null or triage_outcome in
      ('Emergency','UrgentInPerson','InPersonRequired','ClinicalReview',
       'TelehealthEligible','Unsupported'));

alter table telehealth_triage_assessments
  drop constraint chk_telehealth_assessment_outcome;
alter table telehealth_triage_assessments
  add constraint chk_telehealth_assessment_outcome check (
    outcome in ('Emergency','UrgentInPerson','InPersonRequired','ClinicalReview',
                'TelehealthEligible','Unsupported'));

create table if not exists telehealth_applicant_request_complaint_triage_assessments (
  receipt_id uuid primary key,
  assessment_id uuid not null unique references telehealth_triage_assessments(assessment_id),
  request_id uuid not null unique references telehealth_requests(request_id),
  applicant_id uuid not null unique references telehealth_prospective_applicants(applicant_id),
  request_creation_id uuid not null unique
    references telehealth_applicant_request_creations(creation_id),
  location_confirmation_id uuid not null unique
    references telehealth_applicant_request_location_confirmations(confirmation_id),
  location_id uuid not null unique references telehealth_patient_locations(location_id),
  universal_safety_receipt_id uuid not null unique
    references telehealth_applicant_request_universal_safety_assessments(receipt_id),
  universal_safety_assessment_id uuid not null unique
    references telehealth_triage_assessments(assessment_id),
  practice_id text not null,
  facility_id integer not null references facilities(id),
  canonical_patient_id text not null unique references patients(canonical_id),
  applicant_version bigint not null,
  source_request_version bigint not null,
  resulting_request_version bigint not null,
  source_request_status text not null,
  resulting_request_status text not null,
  complaint_category text not null,
  current_location_state_code character(2) not null,
  callback_phone_last4 character(4) not null,
  location_confirmed_at timestamptz not null,
  universal_safety_evaluated_at timestamptz not null,
  context_expires_at timestamptz not null,
  applicant_expires_at timestamptz not null,
  context_snapshot_fingerprint character(64) not null,
  current_location_confirmed boolean not null,
  callback_number_confirmed boolean not null,
  synthetic_data_confirmed boolean not null,
  answer_keys text[] not null,
  answer_values text[] not null,
  protocol_id uuid not null,
  protocol_key text not null,
  protocol_version integer not null,
  protocol_content_hash character(64) not null,
  engine_version text not null,
  clinical_content_status text not null,
  medical_director_approval_required boolean not null default true,
  medical_director_approval_recorded boolean not null default false,
  clinical_golden_case_pack_approved boolean not null default false,
  production_publication_allowed boolean not null default false,
  answers_fingerprint character(64) not null,
  fired_rule_codes text[] not null,
  reason_codes text[] not null,
  outcome text not null,
  public_disposition text not null,
  policy_key text not null,
  policy_version integer not null,
  evidence_type text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  complaint_triage_assessment_created boolean not null default true,
  synthetic_video_evaluation_candidate boolean not null,
  clinical_review_required boolean not null,
  clinical_review_created boolean not null default false,
  terminal_for_telehealth boolean not null,
  intake_snapshot_created boolean not null default false,
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
  evaluated_at timestamptz not null,
  constraint uq_th_app_req_complaint_triage_idempotency unique(applicant_id,idempotency_key),
  constraint chk_th_app_req_complaint_triage_scope check (
    practice_id='avenchart-synthetic-practice' and facility_id=10),
  constraint chk_th_app_req_complaint_triage_versions check (
    applicant_version=26 and source_request_version=3 and resulting_request_version=4
    and source_request_status='SafetyScreening'),
  constraint chk_th_app_req_complaint_triage_category check (
    complaint_category in ('migraine','sleep')),
  constraint chk_th_app_req_complaint_triage_context check (
    current_location_state_code in ('GA','CA','FL')
    and callback_phone_last4 ~ '^[0-9]{4}$'
    and current_location_confirmed and callback_number_confirmed and synthetic_data_confirmed),
  constraint chk_th_app_req_complaint_triage_freshness check (
    location_confirmed_at<universal_safety_evaluated_at
    and universal_safety_evaluated_at<=evaluated_at
    and evaluated_at<=context_expires_at and evaluated_at<applicant_expires_at),
  constraint chk_th_app_req_complaint_triage_answers check (
    cardinality(answer_keys)=8 and cardinality(answer_values)=8
    and array_position(answer_keys,null) is null
    and array_position(answer_values,null) is null
    and answer_values <@ array['Yes','No','NotSure']::text[]),
  constraint chk_th_app_req_complaint_triage_rule_evidence check (
    cardinality(fired_rule_codes) between 1 and 10
    and cardinality(reason_codes)=cardinality(fired_rule_codes)
    and fired_rule_codes::text !~ '[[:cntrl:]]'
    and reason_codes::text !~ '[[:cntrl:]]'),
  constraint chk_th_app_req_complaint_triage_protocol check (
    protocol_version=1
    and engine_version='synthetic-complaint-triage-engine-v1'
    and clinical_content_status='UNAPPROVED_SYNTHETIC'
    and ((complaint_category='migraine'
          and protocol_id='a37cd238-3dc3-44d9-9a94-8cfcf63e8601'::uuid
          and protocol_key='synthetic-migraine-complaint-triage')
      or (complaint_category='sleep'
          and protocol_id='b8928aa9-26cc-4b9b-8b7b-825332ae0f02'::uuid
          and protocol_key='synthetic-sleep-complaint-triage'))),
  constraint chk_th_app_req_complaint_triage_publication_gate check (
    medical_director_approval_required
    and not medical_director_approval_recorded
    and not clinical_golden_case_pack_approved
    and not production_publication_allowed),
  constraint chk_th_app_req_complaint_triage_result check (
    (outcome='Emergency' and resulting_request_status='EmergencyRedirected'
      and public_disposition='EmergencyCareNow'
      and not synthetic_video_evaluation_candidate
      and not clinical_review_required and terminal_for_telehealth)
    or
    (outcome='UrgentInPerson' and resulting_request_status='InPersonRecommended'
      and public_disposition='PromptInPersonCare'
      and not synthetic_video_evaluation_candidate
      and not clinical_review_required and terminal_for_telehealth)
    or
    (outcome='InPersonRequired' and resulting_request_status='InPersonRecommended'
      and public_disposition='InPersonCareRequired'
      and not synthetic_video_evaluation_candidate
      and not clinical_review_required and terminal_for_telehealth)
    or
    (outcome='Unsupported' and resulting_request_status='Unsupported'
      and public_disposition='TelehealthServiceUnsupported'
      and not synthetic_video_evaluation_candidate
      and not clinical_review_required and terminal_for_telehealth)
    or
    (outcome='ClinicalReview' and resulting_request_status='ClinicalReview'
      and public_disposition='ClinicalReviewRequired'
      and not synthetic_video_evaluation_candidate
      and clinical_review_required and not terminal_for_telehealth)
    or
    (outcome='TelehealthEligible' and resulting_request_status='Intake'
      and public_disposition='SyntheticVideoEvaluationCandidate'
      and synthetic_video_evaluation_candidate
      and not clinical_review_required and not terminal_for_telehealth)),
  constraint chk_th_app_req_complaint_triage_policy check (
    policy_key='SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE'
    and policy_version=1
    and evidence_type='APPLICANT_REQUEST_COMPLAINT_TRIAGE_ASSESSMENT'),
  constraint chk_th_app_req_complaint_triage_hashes check (
    context_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
    and protocol_content_hash ~ '^[0-9a-f]{64}$'
    and answers_fingerprint ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'),
  constraint chk_th_app_req_complaint_triage_idem check (length(idempotency_key) between 8 and 128),
  constraint chk_th_app_req_complaint_triage_no_consequence check (
    complaint_triage_assessment_created and not clinical_review_created
    and not intake_snapshot_created and not patient_contacted
    and not patient_care_queue_entered and not clinician_queue_entered
    and not doctor_search_started and not queue_position_assigned
    and not appointment_created and not encounter_created and not consent_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
);

create or replace function enforce_th_app_request_complaint_triage()
returns trigger
language plpgsql
as $$
declare
  applicant_row telehealth_prospective_applicants%rowtype;
  creation_row telehealth_applicant_request_creations%rowtype;
  location_confirmation_row telehealth_applicant_request_location_confirmations%rowtype;
  location_row telehealth_patient_locations%rowtype;
  universal_row telehealth_applicant_request_universal_safety_assessments%rowtype;
  request_row telehealth_requests%rowtype;
  assessment_row telehealth_triage_assessments%rowtype;
  protocol_row telehealth_protocol_versions%rowtype;
  patient_row patients%rowtype;
  expected_rules text[] := array[]::text[];
  expected_reasons text[] := array[]::text[];
  expected_outcome text;
begin
  select * into applicant_row from telehealth_prospective_applicants
  where applicant_id=new.applicant_id for key share;
  select * into creation_row from telehealth_applicant_request_creations
  where creation_id=new.request_creation_id;
  select * into location_confirmation_row
  from telehealth_applicant_request_location_confirmations
  where confirmation_id=new.location_confirmation_id;
  select * into location_row from telehealth_patient_locations where location_id=new.location_id;
  select * into universal_row from telehealth_applicant_request_universal_safety_assessments
  where receipt_id=new.universal_safety_receipt_id;
  select * into request_row from telehealth_requests where request_id=new.request_id;
  select * into assessment_row from telehealth_triage_assessments
  where assessment_id=new.assessment_id;
  select * into protocol_row from telehealth_protocol_versions where protocol_id=new.protocol_id;
  select * into patient_row from patients where canonical_id=new.canonical_patient_id;

  if applicant_row.applicant_id is null or creation_row.creation_id is null
     or location_confirmation_row.confirmation_id is null or location_row.location_id is null
     or universal_row.receipt_id is null or request_row.request_id is null
     or assessment_row.assessment_id is null or protocol_row.protocol_id is null
     or patient_row.canonical_id is null
     or applicant_row.practice_id<>new.practice_id or applicant_row.facility_id<>new.facility_id
     or applicant_row.status<>'SyntheticRequestCreated' or applicant_row.version<>new.applicant_version
     or applicant_row.expires_at<>new.applicant_expires_at or applicant_row.expires_at<=new.evaluated_at
     or creation_row.applicant_id<>new.applicant_id or creation_row.request_id<>new.request_id
     or creation_row.practice_id<>new.practice_id or creation_row.facility_id<>new.facility_id
     or creation_row.canonical_patient_id<>new.canonical_patient_id
     or creation_row.complaint_category<>new.complaint_category
     or creation_row.request_status<>'Draft' or creation_row.request_version<>1
     or creation_row.resulting_applicant_status<>'SyntheticRequestCreated'
     or creation_row.resulting_applicant_version<>new.applicant_version
     or not creation_row.telehealth_request_created
     or location_confirmation_row.applicant_id<>new.applicant_id
     or location_confirmation_row.request_id<>new.request_id
     or location_confirmation_row.request_creation_id<>new.request_creation_id
     or location_confirmation_row.location_id<>new.location_id
     or location_confirmation_row.practice_id<>new.practice_id
     or location_confirmation_row.facility_id<>new.facility_id
     or location_confirmation_row.canonical_patient_id<>new.canonical_patient_id
     or location_confirmation_row.resulting_request_status<>'LocationConfirmed'
     or location_confirmation_row.resulting_request_version<>2
     or not location_confirmation_row.location_confirmed
     or location_confirmation_row.current_location_state_code<>new.current_location_state_code
     or location_confirmation_row.callback_phone_last4<>new.callback_phone_last4
     or location_confirmation_row.confirmed_at<>new.location_confirmed_at
     or location_row.request_id<>new.request_id or location_row.request_version<>2
     or location_row.state_code<>new.current_location_state_code
     or location_row.attested_at<>new.location_confirmed_at
     or universal_row.applicant_id<>new.applicant_id
     or universal_row.request_id<>new.request_id
     or universal_row.request_creation_id<>new.request_creation_id
     or universal_row.location_confirmation_id<>new.location_confirmation_id
     or universal_row.location_id<>new.location_id
     or universal_row.assessment_id<>new.universal_safety_assessment_id
     or universal_row.practice_id<>new.practice_id or universal_row.facility_id<>new.facility_id
     or universal_row.canonical_patient_id<>new.canonical_patient_id
     or universal_row.resulting_request_status<>'SafetyScreening'
     or universal_row.resulting_request_version<>new.source_request_version
     or universal_row.outcome<>'TelehealthEligible'
     or not universal_row.universal_safety_passed
     or not universal_row.complaint_specific_triage_required
     or universal_row.complaint_specific_triage_created
     or universal_row.clinical_review_required or universal_row.terminal_for_telehealth
     or universal_row.current_location_state_code<>new.current_location_state_code
     or universal_row.callback_phone_last4<>new.callback_phone_last4
     or universal_row.location_confirmed_at<>new.location_confirmed_at
     or universal_row.evaluated_at<>new.universal_safety_evaluated_at
     or universal_row.context_expires_at<>new.context_expires_at
     or request_row.source_applicant_id<>new.applicant_id
     or request_row.patient_id<>new.canonical_patient_id
     or request_row.practice_id<>new.practice_id or request_row.facility_id<>new.facility_id
     or request_row.complaint_category<>new.complaint_category
     or request_row.status<>new.resulting_request_status
     or request_row.version<>new.resulting_request_version
     or request_row.ready_at is not null or request_row.triage_outcome<>new.outcome
     or assessment_row.request_id<>new.request_id or assessment_row.protocol_id<>new.protocol_id
     or assessment_row.answer_fingerprint<>new.answers_fingerprint
     or assessment_row.outcome<>new.outcome or assessment_row.request_version<>new.resulting_request_version
     or assessment_row.idempotency_key<>new.idempotency_key
     or assessment_row.command_fingerprint<>new.command_fingerprint
     or assessment_row.evaluated_at<>new.evaluated_at
     or protocol_row.protocol_key<>new.protocol_key
     or protocol_row.protocol_version<>new.protocol_version
     or protocol_row.content_hash<>new.protocol_content_hash or not protocol_row.is_synthetic
     or patient_row.facility_id<>new.facility_id or patient_row.portal_enabled
     or patient_row.merged_into_patient_id is not null or patient_row.lifecycle_status<>'active'
     or exists(select 1 from telehealth_intake_snapshots x where x.request_id=new.request_id)
     or exists(select 1 from telehealth_queue_entries x where x.request_id=new.request_id)
     or request_row.appointment_id is not null
     or exists(select 1 from telehealth_consultation_contexts x where x.request_id=new.request_id) then
    raise exception 'invalid telehealth applicant request complaint triage provenance';
  end if;

  if new.complaint_category='migraine' then
    if new.answer_keys<>array['SuddenOrWorstOnset','NewNeurologicOrVisionChange',
      'FeverOrStiffNeck','RecentHeadInjury','PregnantOrPostpartum',
      'CancerOrImmunocompromised','KnownSimilarPattern','PersistentVomiting']::text[] then
      raise exception 'invalid migraine complaint answer keys';
    end if;
    if new.answer_values[1]='Yes' then expected_rules:=array_append(expected_rules,'MIG-EMERGENCY-SUDDEN-WORST'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_SUDDEN_OR_WORST_WARNING'); end if;
    if new.answer_values[2]='Yes' then expected_rules:=array_append(expected_rules,'MIG-EMERGENCY-NEURO-VISION'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_NEUROLOGIC_OR_VISION_WARNING'); end if;
    if new.answer_values[3]='Yes' then expected_rules:=array_append(expected_rules,'MIG-URGENT-FEVER-STIFF-NECK'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_FEVER_OR_STIFF_NECK'); end if;
    if new.answer_values[4]='Yes' then expected_rules:=array_append(expected_rules,'MIG-URGENT-HEAD-INJURY'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_RECENT_HEAD_INJURY'); end if;
    if new.answer_values[8]='Yes' then expected_rules:=array_append(expected_rules,'MIG-URGENT-PERSISTENT-VOMITING'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_PERSISTENT_VOMITING'); end if;
    if new.answer_values[5]='Yes' then expected_rules:=array_append(expected_rules,'MIG-REVIEW-PREGNANCY-POSTPARTUM'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_PREGNANCY_OR_POSTPARTUM'); end if;
    if new.answer_values[6]='Yes' then expected_rules:=array_append(expected_rules,'MIG-REVIEW-CANCER-IMMUNOCOMPROMISED'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_CANCER_OR_IMMUNOCOMPROMISED'); end if;
    if 'NotSure'=any(new.answer_values) then expected_rules:=array_append(expected_rules,'MIG-REVIEW-UNKNOWN-ANSWER'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_ANSWER_UNCERTAIN'); end if;
    if new.answer_values[7]='No' then expected_rules:=array_append(expected_rules,'MIG-REVIEW-NEW-OR-DIFFERENT-PATTERN'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_PATTERN_NOT_ESTABLISHED'); end if;
    if cardinality(expected_rules)=0 and new.answer_values[7]='Yes' then expected_rules:=array_append(expected_rules,'MIG-CANDIDATE-KNOWN-SIMILAR-PATTERN'); expected_reasons:=array_append(expected_reasons,'MIGRAINE_SYNTHETIC_CANDIDATE'); end if;
  else
    if new.answer_keys<>array['SelfHarmThoughts','ManiaOrPsychosis','DangerousSomnolence',
      'WithdrawalConcern','BreathingPausesOrSevereSnoring',
      'PregnantOrComplexMedicationConcern','ControlledSedativeRequest',
      'UncomplicatedSleepDifficulty']::text[] then
      raise exception 'invalid sleep complaint answer keys';
    end if;
    if new.answer_values[1]='Yes' then expected_rules:=array_append(expected_rules,'SLP-EMERGENCY-SELF-HARM'); expected_reasons:=array_append(expected_reasons,'SLEEP_SELF_HARM_WARNING'); end if;
    if new.answer_values[2]='Yes' then expected_rules:=array_append(expected_rules,'SLP-URGENT-MANIA-PSYCHOSIS'); expected_reasons:=array_append(expected_reasons,'SLEEP_MANIA_OR_PSYCHOSIS_WARNING'); end if;
    if new.answer_values[4]='Yes' then expected_rules:=array_append(expected_rules,'SLP-URGENT-WITHDRAWAL'); expected_reasons:=array_append(expected_reasons,'SLEEP_WITHDRAWAL_WARNING'); end if;
    if new.answer_values[3]='Yes' then expected_rules:=array_append(expected_rules,'SLP-URGENT-DANGEROUS-SOMNOLENCE'); expected_reasons:=array_append(expected_reasons,'SLEEP_DANGEROUS_SOMNOLENCE'); end if;
    if new.answer_values[5]='Yes' then expected_rules:=array_append(expected_rules,'SLP-INPERSON-BREATHING-DISORDER'); expected_reasons:=array_append(expected_reasons,'SLEEP_BREATHING_DISORDER_WARNING'); end if;
    if new.answer_values[6]='Yes' then expected_rules:=array_append(expected_rules,'SLP-REVIEW-PREGNANCY-COMPLEX-MEDS'); expected_reasons:=array_append(expected_reasons,'SLEEP_PREGNANCY_OR_COMPLEX_MEDICATION'); end if;
    if 'NotSure'=any(new.answer_values) then expected_rules:=array_append(expected_rules,'SLP-REVIEW-UNKNOWN-ANSWER'); expected_reasons:=array_append(expected_reasons,'SLEEP_ANSWER_UNCERTAIN'); end if;
    if new.answer_values[7]='Yes' then expected_rules:=array_append(expected_rules,'SLP-UNSUPPORTED-CONTROLLED-SEDATIVE'); expected_reasons:=array_append(expected_reasons,'SLEEP_CONTROLLED_SEDATIVE_OUT_OF_SCOPE'); end if;
    if new.answer_values[8]='No' then expected_rules:=array_append(expected_rules,'SLP-REVIEW-COMPLEX-PRESENTATION'); expected_reasons:=array_append(expected_reasons,'SLEEP_UNCOMPLICATED_PRESENTATION_NOT_CONFIRMED'); end if;
    if cardinality(expected_rules)=0 and new.answer_values[8]='Yes' then expected_rules:=array_append(expected_rules,'SLP-CANDIDATE-UNCOMPLICATED-SLEEP-DIFFICULTY'); expected_reasons:=array_append(expected_reasons,'SLEEP_SYNTHETIC_CANDIDATE'); end if;
  end if;

  if cardinality(expected_rules)=0
     or expected_rules<>new.fired_rule_codes or expected_reasons<>new.reason_codes then
    raise exception 'invalid telehealth complaint triage ordered rule evidence';
  end if;

  expected_outcome:=case
    when expected_rules[1] in ('MIG-EMERGENCY-SUDDEN-WORST','MIG-EMERGENCY-NEURO-VISION','SLP-EMERGENCY-SELF-HARM') then 'Emergency'
    when expected_rules[1] in ('MIG-URGENT-FEVER-STIFF-NECK','MIG-URGENT-HEAD-INJURY','MIG-URGENT-PERSISTENT-VOMITING','SLP-URGENT-MANIA-PSYCHOSIS','SLP-URGENT-WITHDRAWAL','SLP-URGENT-DANGEROUS-SOMNOLENCE') then 'UrgentInPerson'
    when expected_rules[1]='SLP-INPERSON-BREATHING-DISORDER' then 'InPersonRequired'
    when expected_rules[1]='SLP-UNSUPPORTED-CONTROLLED-SEDATIVE' then 'Unsupported'
    when expected_rules[1] in ('MIG-CANDIDATE-KNOWN-SIMILAR-PATTERN','SLP-CANDIDATE-UNCOMPLICATED-SLEEP-DIFFICULTY') then 'TelehealthEligible'
    else 'ClinicalReview'
  end;
  if expected_outcome<>new.outcome then
    raise exception 'invalid telehealth complaint triage outcome priority';
  end if;
  return new;
end;
$$;

drop trigger if exists trg_th_app_request_complaint_triage_guard
  on telehealth_applicant_request_complaint_triage_assessments;
create trigger trg_th_app_request_complaint_triage_guard
before insert on telehealth_applicant_request_complaint_triage_assessments
for each row execute function enforce_th_app_request_complaint_triage();

drop trigger if exists trg_th_app_request_complaint_triage_append
  on telehealth_applicant_request_complaint_triage_assessments;
create trigger trg_th_app_request_complaint_triage_append
before update or delete on telehealth_applicant_request_complaint_triage_assessments
for each row execute function reject_telehealth_evidence_mutation();

create index if not exists ix_th_app_request_complaint_triage_outcome
  on telehealth_applicant_request_complaint_triage_assessments(
    practice_id,facility_id,complaint_category,outcome,evaluated_at,applicant_id);
