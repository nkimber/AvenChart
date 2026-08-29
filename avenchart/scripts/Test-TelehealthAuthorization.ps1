# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) { throw 'Telehealth authorization proof is local-only.' }
. (Join-Path $PSScriptRoot 'AvenChartStaffAccessContext.ps1')
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'; New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-authorization.json'
$checks=[System.Collections.Generic.List[object]]::new();$passed=$true
function Add-Check([string]$Name,[bool]$Result,[object]$Details=$null){$script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details});if(-not $Result){$script:passed=$false}}
function Invoke-Status([string]$Method,[string]$Path,[hashtable]$Headers=@{},[object]$Body=$null){
  $parameters=@{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=$Headers;TimeoutSec=20}
  if($null-ne$Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Depth 8}
  try{return [int](Invoke-WebRequest @parameters).StatusCode}catch{if($null-ne$_.Exception.Response){return [int]$_.Exception.Response.StatusCode};throw}
}
function Login-Staff([string]$Username){Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username=$Username;password='pass'}|ConvertTo-Json) -TimeoutSec 20}
function Login-Portal([string]$Username){Invoke-RestMethod "$ApiBaseUrl/api/patient-portal/login" -Method Post -ContentType 'application/json' -Body (@{username=$Username;password='PortalPass207!'}|ConvertTo-Json) -TimeoutSec 20}
try {
  Add-Check 'Unknown branded host fails closed' ((Invoke-Status 'GET' '/api/telehealth/v1/context' @{Host='unknown.example.test'}) -eq 404)
  Add-Check 'Patient request listing rejects an absent patient identity' ((Invoke-Status 'GET' '/api/telehealth/v1/patient/requests') -eq 401)
  Add-Check 'Patient connection grant rejects an absent patient identity' ((Invoke-Status 'POST' '/api/telehealth/v1/patient/requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/connection-grants' @{} @{expectedVersion=1;browserSupported=$true;cameraAvailable=$true;microphoneAvailable=$true;speakerAvailable=$true;networkQuality='unknown';syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Consultation workspace rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/workspace') -eq 401)
  Add-Check 'Consultation draft rejects an absent staff identity' ((Invoke-Status 'PUT' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/documentation/draft' @{} @{expectedVersion=0;subjective='synthetic'}) -eq 401)
  Add-Check 'Consultation wrap-up rejects an absent staff identity' ((Invoke-Status 'POST' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/wrap-up' @{} @{expectedVersion=1;syntheticSessionEndedConfirmed=$true;documentationStillIncompleteAcknowledged=$true;wrapUpResponsibilityAcknowledged=$true}) -eq 401)
  Add-Check 'Pharmacy-choice search rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pharmacy-choices') -eq 401)
  Add-Check 'Pharmacy destination draft rejects an absent staff identity' ((Invoke-Status 'PUT' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pharmacy-choice' @{} @{expectedVersion=0;directoryEntryId='00000000-0000-4000-8000-000000001001';patientChoiceConfirmed=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Prescription-preparation workspace rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/prescription-preparation-draft') -eq 401)
  Add-Check 'Prescription-preparation draft rejects an absent staff identity' ((Invoke-Status 'PUT' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/prescription-preparation-draft' @{} @{expectedVersion=0;rxNormCode='860975';doseAmount=1;doseUnit='tablet';frequency='synthetic';quantityValue=1;quantityUnit='tablet';durationDays=1;refills=0;indication='synthetic';directions='synthetic';medicationListReviewed=$true;allergyListReviewed=$true;adequateEvaluationCompleted=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Safety-disposition workspace rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/safety-disposition-draft') -eq 401)
  Add-Check 'Safety-disposition draft rejects an absent staff identity' ((Invoke-Status 'PUT' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/safety-disposition-draft' @{} @{expectedVersion=0;dispositionCode='TreatedTelehealth';adequateEvaluationCompleted=$true;followUpOwner='Patient';followUpTimeframe='synthetic';nextStepInstructions='synthetic';warningEscalationInstructions='synthetic';communicationMethod='DiscussedDuringSyntheticConsultation';communicationCompleted=$true;locationCallbackReconfirmed=$false;emergencyInstructionProvided=$false;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Completion-prerequisites review rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/completion-prerequisites') -eq 401)
  Add-Check 'Prospective applicant read rejects an absent applicant access key' ((Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') -eq 401)
  Add-Check 'Applicant practice-review inbox rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review') -eq 401)
  Add-Check 'Applicant practice-review claim rejects an absent staff identity' ((Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/claim' @{} @{expectedApplicantVersion=24;inboxPolicyVersion=1;noDecisionAcknowledged=$true;noPatientContactAcknowledged=$true;noRequestOrCareQueueAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant practice-review packet rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') -eq 401)
  Add-Check 'Applicant practice-review authorization rejects an absent staff identity' ((Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/authorization' @{} @{expectedApplicantVersion=25;packetPolicyVersion=1;decision='AuthorizedForSyntheticRequestCreation';rationaleCode='OperationalPrerequisitesReviewed';noClinicalEligibilityAcknowledged=$true;noCoverageGuaranteeAcknowledged=$true;noRequestOrQueueAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant Draft-request operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request' @{} @{expectedApplicantVersion=25;authorizationPolicyVersion=1;requestCreationConfirmed=$true;noQueueOrCareAcknowledged=$true;urgentOrWorseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant request-location operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/location') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/location' @{} @{expectedRequestVersion=1;contextSnapshotFingerprint=('0'*64);currentLocationStateCode='GA';currentLocationConfirmed=$true;callbackNumberConfirmed=$true;changedLocationRequiresRestartAcknowledged=$true;urgentOrWorseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant request universal-safety operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/safety') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/safety' @{} @{expectedRequestVersion=2;contextSnapshotFingerprint=('0'*64);currentLocationStateCode='GA';currentLocationConfirmed=$true;callbackNumberConfirmed=$true;syntheticDataConfirmed=$true;hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false}) -eq 401)
  $complaintTriageBody=@{expectedRequestVersion=3;contextSnapshotFingerprint=('0'*64);currentLocationStateCode='GA';currentLocationConfirmed=$true;callbackNumberConfirmed=$true;syntheticDataConfirmed=$true;migraine=@{suddenOrWorstOnset='No';newNeurologicOrVisionChange='No';feverOrStiffNeck='No';recentHeadInjury='No';pregnantOrPostpartum='No';cancerOrImmunocompromised='No';knownSimilarPattern='Yes';persistentVomiting='No'}}
  $requestIntakeBody=@{expectedRequestVersion=4;contextSnapshotFingerprint=('0'*64);currentLocationStateCode='GA';symptomDuration='1-3-days';currentLocationConfirmed=$true;callbackNumberConfirmed=$true;priorInformationReviewed=$true;insuranceLimitationsAcknowledged=$true;pendingConsentAcknowledged=$true;pendingVerificationAcknowledged=$true;complaintResultAcknowledged=$true;syntheticDataConfirmed=$true}
  $requestInsuranceSourceBody=@{expectedRequestVersion=5;insuranceSourceSnapshotFingerprint=('0'*64);payerProductConfirmed=$true;maskedMemberDetailsConfirmed=$true;subscriberRelationshipConfirmed=$true;primaryCoverageSourceConfirmed=$true;freshVerificationRequested=$true;evidenceLimitationsAcknowledged=$true;syntheticDataConfirmed=$true}
  $requestEligibilityBody=@{expectedRequestVersion=6;eligibilitySnapshotFingerprint=('0'*64);syntheticDataConfirmed=$true;noGuaranteeAcknowledged=$true}
  $requestPracticeNetworkBody=@{expectedRequestVersion=7;networkSnapshotFingerprint=('0'*64);syntheticDataConfirmed=$true;practiceOnlyScopeAcknowledged=$true;noGuaranteeAcknowledged=$true}
  $requestRenderingCandidateBody=@{expectedRequestVersion=8;candidateSnapshotFingerprint=('0'*64);syntheticDataConfirmed=$true;candidateOnlyScopeAcknowledged=$true;noAssignmentAcknowledged=$true;networkCheckStillRequiredAcknowledged=$true}
  $requestParticipationContextBody=@{expectedRequestVersion=9;contextSnapshotFingerprint=('0'*64);syntheticDataConfirmed=$true;npiNotCredentialAcknowledged=$true;realAuthorityNotVerifiedAcknowledged=$true;exactParticipationStillRequiredAcknowledged=$true}
  $requestParticipationEvaluationBody=@{expectedRequestVersion=10;evaluationSnapshotFingerprint=('0'*64);syntheticDataConfirmed=$true;exactTupleScopeAcknowledged=$true;noCoverageGuaranteeAcknowledged=$true;realVerificationStillRequiredAcknowledged=$true}
  $requestOperationalReviewSubmissionBody=@{expectedRequestVersion=11;submissionSnapshotFingerprint=('0'*64);syntheticEvidenceAcknowledged=$true;noCoverageGuaranteeAcknowledged=$true;practiceReviewPendingAcknowledged=$true;noCareRelationshipAcknowledged=$true}
  $requestQueueAuthorizationBody=@{expectedRequestVersion=12;authorizationSnapshotFingerprint=('0'*64);syntheticEvidenceReviewed=$true;noCoverageGuaranteeAcknowledged=$true;practiceAcceptsForQueueAcknowledged=$true;queueNotCareAcknowledged=$true}
  Add-Check 'Applicant request complaint-triage operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/complaint-triage') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/complaint-triage' @{} $complaintTriageBody) -eq 401)
  Add-Check 'Applicant request intake operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/intake') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/intake' @{} $requestIntakeBody) -eq 401)
  Add-Check 'Applicant request insurance-source operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/insurance-source') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/insurance-source' @{} $requestInsuranceSourceBody) -eq 401)
  Add-Check 'Applicant request eligibility operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/eligibility') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/eligibility' @{} $requestEligibilityBody) -eq 401)
  Add-Check 'Applicant request practice-network operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/practice-network') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/practice-network' @{} $requestPracticeNetworkBody) -eq 401)
  Add-Check 'Applicant request rendering-candidate operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/rendering-candidate') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/rendering-candidate' @{} $requestRenderingCandidateBody) -eq 401)
  Add-Check 'Applicant request participation-context operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-context') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-context' @{} $requestParticipationContextBody) -eq 401)
  Add-Check 'Applicant request participation-evaluation operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-evaluation') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-evaluation' @{} $requestParticipationEvaluationBody) -eq 401)
  Add-Check 'Applicant request operational-review submission rejects an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/operational-review-submission') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/operational-review-submission' @{} $requestOperationalReviewSubmissionBody) -eq 401)
  Add-Check 'Applicant request queue status rejects an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/queue-status') -eq 401)
  Add-Check 'Applicant request queue authorization rejects an absent staff identity' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' @{} $requestQueueAuthorizationBody) -eq 401)
  Add-Check 'Applicant identity-review queue rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-identity-review') -eq 401)
  Add-Check 'Applicant identity-review decision rejects an absent staff identity' ((Invoke-Status 'PUT' '/api/telehealth/v1/admin/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/identity-review-decision' @{} @{expectedVersion=2;decision='ApprovedForProspectiveIntake';reason='Synthetic authorization boundary.';syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Applicant promotion-authorization queue rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-promotion-authorization') -eq 401)
  Add-Check 'Applicant promotion-authorization decision rejects an absent staff identity' ((Invoke-Status 'PUT' '/api/telehealth/v1/admin/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/promotion-authorization-decision' @{} @{expectedVersion=10;decision='AuthorizedForSyntheticPromotion';reason='Synthetic promotion authorization boundary.';noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Applicant synthetic-promotion queue rejects an absent staff identity' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-synthetic-promotion') -eq 401)
  Add-Check 'Applicant synthetic-promotion command rejects an absent staff identity' ((Invoke-Status 'PUT' '/api/telehealth/v1/admin/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/synthetic-promotion' @{} @{expectedVersion=11;command='PromoteAuthorizedSyntheticApplicant';reason='Synthetic patient promotion authorization boundary.';canonicalPatientCreationAcknowledged=$true;noPortalNoCareAcknowledged=$true}) -eq 401)
  Add-Check 'Prospective safety triage rejects an absent applicant access key' ((Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/safety-triage' @{} @{expectedVersion=3;currentLocationStateCode='GA';currentLocationConfirmed=$true;hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Prospective visit purpose rejects an absent applicant access key' ((Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/visit-purpose' @{} @{expectedVersion=4;purposeCategory='migraine';syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Prospective practice-network options reject an absent applicant access key' ((Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-network-precheck/options') -eq 401)
  Add-Check 'Prospective practice-network command rejects an absent applicant access key' ((Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-network-precheck' @{} @{expectedVersion=5;planKey='harbor-mutual-hd';syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Prospective member-details command rejects an absent applicant access key' ((Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/member-insurance-details' @{} @{expectedVersion=6;memberId='SYN-HM-1001';groupNumber='SYN-GROUP-01';subscriberRelationship='Self';detailsConfirmed=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Prospective eligibility command rejects an absent applicant access key' ((Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/eligibility' @{} @{expectedVersion=7;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Prospective practice-network determination rejects an absent applicant access key' ((Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-network-determination' @{} @{expectedVersion=8;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Prospective identity-proofing process rejects an absent applicant access key' ((Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/identity-proofing' @{} @{expectedVersion=9;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'State-specific applicant telehealth notice rejects an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-notice') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-notice/acknowledgment' @{} @{expectedVersion=12;noticeKey='GA_TELEHEALTH_NOTICE_V1';noticeVersion=1;currentLocationStateCode='GA';currentLocationConfirmed=$true;modeOfCareAcknowledged=$true;privacyLimitationsAcknowledged=$true;emergencyInstructionsAcknowledged=$true;inPersonOptionAcknowledged=$true;clinicianReconfirmationRequiredAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Minimum registration-details read and confirmation reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/registration-details') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/registration-details/confirmation' @{} @{expectedVersion=13;detailsFingerprint=('0'*64);legalNameAndBirthDateConfirmed=$true;contactChannelsConfirmed=$true;residenceRegionConfirmed=$true;noCorrectionsNeededConfirmed=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Synthetic insurance-handoff read and confirmation reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/insurance-handoff') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/insurance-handoff/confirmation' @{} @{expectedVersion=14;insuranceSnapshotFingerprint=('0'*64);payerAndProductConfirmed=$true;maskedMemberDetailsConfirmed=$true;subscriberRelationshipConfirmed=$true;evidenceLimitationsAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Synthetic communication/access-readiness operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/communication-access-readiness') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/communication-access-readiness' @{} @{expectedVersion=15;contextSnapshotFingerprint=('0'*64);preferredSpokenLanguage='English';interpreterRequested=$false;accessibilitySupportRequested=$false;currentLocationConfirmed=$true;callbackNumberConfirmed=$true;safeAndPrivateToCommunicateConfirmed=$true;disconnectionAndEmergencyPlanAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Synthetic device-preparation operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/device-preparation') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/device-preparation' @{} @{expectedVersion=16;preparationSnapshotFingerprint=('0'*64);browserSupported=$true;cameraAvailable=$true;microphoneAvailable=$true;speakerAvailable=$true;networkQuality='Good';clientReportedResultAcknowledged=$true;noReadinessGuaranteeAcknowledged=$true;recheckBeforeConsultationAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic clinical-information inventory operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-inventory') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-inventory' @{} @{expectedVersion=17;inventorySnapshotFingerprint=('0'*64);medicationsStatus='PatientReportsNone';allergiesOrIntolerancesStatus='PatientReportsNone';otherHealthHistoryStatus='PatientReportsNone';patientReportedMayBeIncompleteAcknowledged=$true;noClinicalDetailsCapturedAcknowledged=$true;clinicianReconciliationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic medication-information operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/medication-information') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/medication-information' @{} @{expectedVersion=18;medicationInformationSnapshotFingerprint=('0'*64);medicationItems=@();additionalOrUnlistedItemsReported=$false;patientReportedMayBeIncompleteAcknowledged=$true;syntheticCatalogIncompleteAcknowledged=$true;noDoseOrDirectionsCapturedAcknowledged=$true;clinicianReconciliationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic allergy-information operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/allergy-information') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/allergy-information' @{} @{expectedVersion=19;allergyInformationSnapshotFingerprint=('0'*64);allergyItems=@();additionalOrUnlistedItemsReported=$false;patientReportedMayBeIncompleteAcknowledged=$true;syntheticCatalogIncompleteAcknowledged=$true;noReactionOrCriticalityCapturedAcknowledged=$true;clinicianVerificationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic health-history-information operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/health-history-information') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/health-history-information' @{} @{expectedVersion=20;healthHistoryInformationSnapshotFingerprint=('0'*64);healthHistoryTopics=@();additionalOrUnlistedTopicsReported=$false;patientReportedMayBeIncompleteAcknowledged=$true;topicSelectionIsNotDiagnosisAcknowledged=$true;noStatusOrTimingCapturedAcknowledged=$true;clinicianVerificationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic clinical-information summary operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-summary') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-summary' @{} @{expectedVersion=21;clinicalInformationSummarySnapshotFingerprint=('0'*64);patientReportedMayBeIncompleteAcknowledged=$true;notClinicallyVerifiedOrReconciledAcknowledged=$true;noIntakeCompletionOrEligibilityAcknowledged=$true;correctionRequiresSeparateWorkflowAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic pre-request readiness operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pre-request-readiness') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pre-request-readiness' @{} @{expectedVersion=22;preRequestReadinessSnapshotFingerprint=('0'*64);priorSectionsReviewedAcknowledged=$true;outstandingStepsRemainAcknowledged=$true;noRequestOrQueueCreatedAcknowledged=$true;correctionRequiresSeparateWorkflowAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic practice-review submission operations reject an absent applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-review-submission') -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-review-submission' @{} @{expectedVersion=23;practiceReviewSnapshotFingerprint=('0'*64);patientReportedInformationAcknowledged=$true;practiceMayRequestInformationOrDeclineAcknowledged=$true;noTelehealthRequestOrCareQueueAcknowledged=$true;worseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)

  $patient=Login-Portal 'mod-pat-0012@example.test'; if(-not$patient.authenticated){throw 'Synthetic portal patient login failed.'}
  $patientHeaders=@{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId;'X-Idempotency-Key'="th-auth-$([Guid]::NewGuid().ToString('N'))"}
  $unknownPatientHeaders = $patientHeaders.Clone()
  $unknownPatientHeaders['Host'] = 'unknown.example.test'
  Add-Check 'Patient mutations reject an unknown branded host' ((Invoke-Status 'POST' '/api/telehealth/v1/patient/requests' $unknownPatientHeaders @{complaintCategory='migraine'}) -eq 404)
  $created=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests" -Method Post -Headers $patientHeaders -ContentType 'application/json' -Body (@{complaintCategory='migraine'}|ConvertTo-Json) -TimeoutSec 20
  Add-Check 'Authenticated established patient can create only a server-bound own request' ($created.status -eq 'Draft') @{requestId=$created.requestId}

  $other=Login-Portal 'mod-pat-0024@example.test'; if(-not$other.authenticated){throw 'Second synthetic portal patient login failed.'}
  $otherList=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests" -Headers @{'X-AvenChart-Patient-Portal-Session'=$other.sessionId} -TimeoutSec 20
  Add-Check 'A different patient cannot list the first patient request' (@($otherList.requests|Where-Object{$_.requestId-eq$created.requestId}).Count -eq 0)
  Add-Check 'A different patient cannot read the first patient readiness projection' ((Invoke-Status 'GET' "/api/telehealth/v1/patient/requests/$($created.requestId)/readiness" @{'X-AvenChart-Patient-Portal-Session'=$other.sessionId}) -eq 404)
  Add-Check 'A different patient cannot read the first patient queue status' ((Invoke-Status 'GET' "/api/telehealth/v1/patient/requests/$($created.requestId)/status" @{'X-AvenChart-Patient-Portal-Session'=$other.sessionId}) -eq 404)
  $otherConnectionHeaders=@{'X-AvenChart-Patient-Portal-Session'=$other.sessionId;'X-Idempotency-Key'="th-auth-video-$([Guid]::NewGuid().ToString('N'))"}
  Add-Check 'A different patient cannot prepare the first patient connection room' ((Invoke-Status 'POST' "/api/telehealth/v1/patient/requests/$($created.requestId)/connection-grants" $otherConnectionHeaders @{expectedVersion=1;browserSupported=$true;cameraAvailable=$true;microphoneAvailable=$true;speakerAvailable=$true;networkQuality='unknown';syntheticDataConfirmed=$true}) -eq 404)
  Add-Check 'A patient portal session does not substitute for an applicant access key' ((Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401)
  Add-Check 'A patient portal session does not substitute for practice-network applicant ownership' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-network-precheck/options' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-network-precheck' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=5;planKey='harbor-mutual-hd';syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'A patient portal session does not substitute for member-details applicant ownership' (
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/member-insurance-details' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=6;memberId='SYN-HM-1001';groupNumber='SYN-GROUP-01';subscriberRelationship='Self';detailsConfirmed=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'A patient portal session does not substitute for eligibility applicant ownership' (
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/eligibility' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=7;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'A patient portal session does not substitute for practice-network determination applicant ownership' (
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-network-determination' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=8;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Prospective identity-proofing process rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/identity-proofing' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=9;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'State-specific applicant notice rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-notice' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-notice/acknowledgment' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=12;noticeKey='GA_TELEHEALTH_NOTICE_V1';noticeVersion=1;currentLocationStateCode='GA';currentLocationConfirmed=$true;modeOfCareAcknowledged=$true;privacyLimitationsAcknowledged=$true;emergencyInstructionsAcknowledged=$true;inPersonOptionAcknowledged=$true;clinicianReconfirmationRequiredAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Minimum registration-details operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/registration-details' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/registration-details/confirmation' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=13;detailsFingerprint=('0'*64);legalNameAndBirthDateConfirmed=$true;contactChannelsConfirmed=$true;residenceRegionConfirmed=$true;noCorrectionsNeededConfirmed=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Synthetic insurance-handoff operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/insurance-handoff' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/insurance-handoff/confirmation' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=14;insuranceSnapshotFingerprint=('0'*64);payerAndProductConfirmed=$true;maskedMemberDetailsConfirmed=$true;subscriberRelationshipConfirmed=$true;evidenceLimitationsAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Synthetic communication/access-readiness operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/communication-access-readiness' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/communication-access-readiness' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=15;contextSnapshotFingerprint=('0'*64);preferredSpokenLanguage='English';interpreterRequested=$false;accessibilitySupportRequested=$false;currentLocationConfirmed=$true;callbackNumberConfirmed=$true;safeAndPrivateToCommunicateConfirmed=$true;disconnectionAndEmergencyPlanAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 401)
  Add-Check 'Synthetic device-preparation operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/device-preparation' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/device-preparation' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=16;preparationSnapshotFingerprint=('0'*64);browserSupported=$true;cameraAvailable=$true;microphoneAvailable=$true;speakerAvailable=$true;networkQuality='Good';clientReportedResultAcknowledged=$true;noReadinessGuaranteeAcknowledged=$true;recheckBeforeConsultationAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic clinical-information inventory rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-inventory' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-inventory' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=17;inventorySnapshotFingerprint=('0'*64);medicationsStatus='PatientReportsNone';allergiesOrIntolerancesStatus='PatientReportsNone';otherHealthHistoryStatus='PatientReportsNone';patientReportedMayBeIncompleteAcknowledged=$true;noClinicalDetailsCapturedAcknowledged=$true;clinicianReconciliationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic medication-information rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/medication-information' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/medication-information' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=18;medicationInformationSnapshotFingerprint=('0'*64);medicationItems=@();additionalOrUnlistedItemsReported=$false;patientReportedMayBeIncompleteAcknowledged=$true;syntheticCatalogIncompleteAcknowledged=$true;noDoseOrDirectionsCapturedAcknowledged=$true;clinicianReconciliationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic allergy-information rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/allergy-information' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/allergy-information' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=19;allergyInformationSnapshotFingerprint=('0'*64);allergyItems=@();additionalOrUnlistedItemsReported=$false;patientReportedMayBeIncompleteAcknowledged=$true;syntheticCatalogIncompleteAcknowledged=$true;noReactionOrCriticalityCapturedAcknowledged=$true;clinicianVerificationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic health-history-information rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/health-history-information' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/health-history-information' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=20;healthHistoryInformationSnapshotFingerprint=('0'*64);healthHistoryTopics=@();additionalOrUnlistedTopicsReported=$false;patientReportedMayBeIncompleteAcknowledged=$true;topicSelectionIsNotDiagnosisAcknowledged=$true;noStatusOrTimingCapturedAcknowledged=$true;clinicianVerificationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic clinical-information summary rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-summary' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-summary' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=21;clinicalInformationSummarySnapshotFingerprint=('0'*64);patientReportedMayBeIncompleteAcknowledged=$true;notClinicallyVerifiedOrReconciledAcknowledged=$true;noIntakeCompletionOrEligibilityAcknowledged=$true;correctionRequiresSeparateWorkflowAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic pre-request readiness rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pre-request-readiness' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pre-request-readiness' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=22;preRequestReadinessSnapshotFingerprint=('0'*64);priorSectionsReviewedAcknowledged=$true;outstandingStepsRemainAcknowledged=$true;noRequestOrQueueCreatedAcknowledged=$true;correctionRequiresSeparateWorkflowAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic practice-review submission rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-review-submission' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-review-submission' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedVersion=23;practiceReviewSnapshotFingerprint=('0'*64);patientReportedInformationAcknowledged=$true;practiceMayRequestInformationOrDeclineAcknowledged=$true;noTelehealthRequestOrCareQueueAcknowledged=$true;worseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant promotion authorization rejects portal-session substitution for staff identity' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-promotion-authorization' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401)
  Add-Check 'Applicant synthetic promotion rejects portal-session substitution for staff identity' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-synthetic-promotion' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401)
  Add-Check 'Applicant practice-review inbox rejects portal-session substitution for staff identity' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401)
  Add-Check 'Applicant practice-review claim rejects portal-session substitution for staff identity' (
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/claim' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedApplicantVersion=24;inboxPolicyVersion=1;noDecisionAcknowledged=$true;noPatientContactAcknowledged=$true;noRequestOrCareQueueAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant practice-review packet rejects portal-session substitution for staff identity' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401)
  Add-Check 'Applicant practice-review authorization rejects portal-session substitution for staff identity' (
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/authorization' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedApplicantVersion=25;packetPolicyVersion=1;decision='AuthorizedForSyntheticRequestCreation';rationaleCode='OperationalPrerequisitesReviewed';noClinicalEligibilityAcknowledged=$true;noCoverageGuaranteeAcknowledged=$true;noRequestOrQueueAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant Draft-request operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedApplicantVersion=25;authorizationPolicyVersion=1;requestCreationConfirmed=$true;noQueueOrCareAcknowledged=$true;urgentOrWorseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant request-location operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/location' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/location' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedRequestVersion=1;contextSnapshotFingerprint=('0'*64);currentLocationStateCode='GA';currentLocationConfirmed=$true;callbackNumberConfirmed=$true;changedLocationRequiresRestartAcknowledged=$true;urgentOrWorseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant request universal-safety operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/safety' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/safety' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} @{expectedRequestVersion=2;contextSnapshotFingerprint=('0'*64);currentLocationStateCode='GA';currentLocationConfirmed=$true;callbackNumberConfirmed=$true;syntheticDataConfirmed=$true;hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false}) -eq 401)
  Add-Check 'Applicant request complaint-triage operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/complaint-triage' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/complaint-triage' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $complaintTriageBody) -eq 401)
  Add-Check 'Applicant request intake operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/intake' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/intake' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestIntakeBody) -eq 401)
  Add-Check 'Applicant request insurance-source operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/insurance-source' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/insurance-source' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestInsuranceSourceBody) -eq 401)
  Add-Check 'Applicant request eligibility operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/eligibility' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/eligibility' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestEligibilityBody) -eq 401)
  Add-Check 'Applicant request practice-network operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/practice-network' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/practice-network' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestPracticeNetworkBody) -eq 401)
  Add-Check 'Applicant request rendering-candidate operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/rendering-candidate' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/rendering-candidate' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestRenderingCandidateBody) -eq 401)
  Add-Check 'Applicant request participation-context operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-context' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-context' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestParticipationContextBody) -eq 401)
  Add-Check 'Applicant request participation-evaluation operations reject portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-evaluation' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-evaluation' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestParticipationEvaluationBody) -eq 401)
  Add-Check 'Applicant request operational-review submission rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/operational-review-submission' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/operational-review-submission' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestOperationalReviewSubmissionBody) -eq 401)
  Add-Check 'Applicant request queue status rejects portal-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/queue-status' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401)
  Add-Check 'Applicant request queue authorization rejects portal-session substitution for staff identity' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId}) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' @{'X-AvenChart-Patient-Portal-Session'=$patient.sessionId} $requestQueueAuthorizationBody) -eq 401)

  $admin=Login-Staff 'admin'
  $adminFacilities = @($admin.accessContext.facilities | Where-Object { $_.facilityId -eq 10 })
  Add-Check 'Administrator login carries the configured facility grant' ($adminFacilities.Count -eq 1) @{
    responseCount = @($admin).Count
    facilityIds = @($admin.accessContext.facilities | ForEach-Object { $_.facilityId })
  }
  $adminHeaders=@{
    'X-AvenChart-Session'=$admin.sessionId
    'X-AvenChart-Facility-Id'='10'
    'X-AvenChart-Purpose-Of-Use'='healthcare-operations'
  }
  Add-Check 'Administrator can read the configured-facility practice-review inbox' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review' $adminHeaders) -eq 200)
  $adminClaimHeaders=$adminHeaders.Clone();$adminClaimHeaders['X-Idempotency-Key']="th-auth-claim-$([Guid]::NewGuid().ToString('N'))"
  $claimBody=@{expectedApplicantVersion=24;inboxPolicyVersion=1;noDecisionAcknowledged=$true;noPatientContactAcknowledged=$true;noRequestOrCareQueueAcknowledged=$true}
  $authorizationBody=@{expectedApplicantVersion=25;packetPolicyVersion=1;decision='AuthorizedForSyntheticRequestCreation';rationaleCode='OperationalPrerequisitesReviewed';noClinicalEligibilityAcknowledged=$true;noCoverageGuaranteeAcknowledged=$true;noRequestOrQueueAcknowledged=$true}
  Add-Check 'Administrator claim authorization reaches an opaque nonexistent-case boundary' (
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/claim' $adminClaimHeaders $claimBody) -eq 404)
  Add-Check 'Administrator packet authorization reaches an opaque nonexistent-or-unclaimed-case boundary' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' $adminHeaders) -eq 404)
  $adminAuthorizationHeaders=$adminHeaders.Clone();$adminAuthorizationHeaders['X-Idempotency-Key']="th-auth-practice-review-$([Guid]::NewGuid().ToString('N'))"
  Add-Check 'Administrator practice-review authorization reaches an opaque nonexistent-or-unclaimed-case boundary' (
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/authorization' $adminAuthorizationHeaders $authorizationBody) -eq 404)
  $adminQueueAuthorizationHeaders=$adminHeaders.Clone();$adminQueueAuthorizationHeaders['X-Idempotency-Key']="th-auth-queue-authorization-$([Guid]::NewGuid().ToString('N'))"
  Add-Check 'Configured administrator reaches only the opaque tenant-scoped applicant queue-authorization boundary' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $adminHeaders) -eq 404 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $adminQueueAuthorizationHeaders $requestQueueAuthorizationBody) -eq 404)
  $frontdesk=Login-Staff 'gold-frontdesk-01'
  $frontdeskGrant = @($frontdesk.accessContext.facilities | Where-Object { $_.facilityId -eq 11 })
  if ($frontdeskGrant.Count -ne 1 -or @($frontdesk.accessContext.purposes) -notcontains 'treatment' -or $null -eq $frontdesk.staffId) {
    throw 'Synthetic front-desk user lacks the expected current foreign-facility staff grant.'
  }
  $frontdeskHeaders=@{
    'X-AvenChart-Session'=$frontdesk.sessionId
    'X-AvenChart-Facility-Id'='11'
    'X-AvenChart-Purpose-Of-Use'='treatment'
  }
  $frontdeskQueueAuthorizationHeaders=$frontdeskHeaders.Clone();$frontdeskQueueAuthorizationHeaders['X-Idempotency-Key']="th-auth-queue-authorization-frontdesk-$([Guid]::NewGuid().ToString('N'))"
  Add-Check 'Current front-desk staff outside the configured facility receives only an opaque denial' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $frontdeskHeaders) -eq 404 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $frontdeskQueueAuthorizationHeaders $requestQueueAuthorizationBody) -eq 404)
  Add-Check 'Synthetic allergy-information rejects staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/allergy-information' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/allergy-information' $adminHeaders @{expectedVersion=19;allergyInformationSnapshotFingerprint=('0'*64);allergyItems=@();additionalOrUnlistedItemsReported=$false;patientReportedMayBeIncompleteAcknowledged=$true;syntheticCatalogIncompleteAcknowledged=$true;noReactionOrCriticalityCapturedAcknowledged=$true;clinicianVerificationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic health-history-information rejects staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/health-history-information' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/health-history-information' $adminHeaders @{expectedVersion=20;healthHistoryInformationSnapshotFingerprint=('0'*64);healthHistoryTopics=@();additionalOrUnlistedTopicsReported=$false;patientReportedMayBeIncompleteAcknowledged=$true;topicSelectionIsNotDiagnosisAcknowledged=$true;noStatusOrTimingCapturedAcknowledged=$true;clinicianVerificationRequiredAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic clinical-information summary rejects staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-summary' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/clinical-information-summary' $adminHeaders @{expectedVersion=21;clinicalInformationSummarySnapshotFingerprint=('0'*64);patientReportedMayBeIncompleteAcknowledged=$true;notClinicallyVerifiedOrReconciledAcknowledged=$true;noIntakeCompletionOrEligibilityAcknowledged=$true;correctionRequiresSeparateWorkflowAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic pre-request readiness rejects staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pre-request-readiness' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pre-request-readiness' $adminHeaders @{expectedVersion=22;preRequestReadinessSnapshotFingerprint=('0'*64);priorSectionsReviewedAcknowledged=$true;outstandingStepsRemainAcknowledged=$true;noRequestOrQueueCreatedAcknowledged=$true;correctionRequiresSeparateWorkflowAcknowledged=$true}) -eq 401)
  Add-Check 'Synthetic practice-review submission rejects staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-review-submission' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/practice-review-submission' $adminHeaders @{expectedVersion=23;practiceReviewSnapshotFingerprint=('0'*64);patientReportedInformationAcknowledged=$true;practiceMayRequestInformationOrDeclineAcknowledged=$true;noTelehealthRequestOrCareQueueAcknowledged=$true;worseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant Draft-request operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request' $adminHeaders @{expectedApplicantVersion=25;authorizationPolicyVersion=1;requestCreationConfirmed=$true;noQueueOrCareAcknowledged=$true;urgentOrWorseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant request-location operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/location' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/location' $adminHeaders @{expectedRequestVersion=1;contextSnapshotFingerprint=('0'*64);currentLocationStateCode='GA';currentLocationConfirmed=$true;callbackNumberConfirmed=$true;changedLocationRequiresRestartAcknowledged=$true;urgentOrWorseningSymptomsRequireImmediateActionAcknowledged=$true}) -eq 401)
  Add-Check 'Applicant request universal-safety operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/safety' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/safety' $adminHeaders @{expectedRequestVersion=2;contextSnapshotFingerprint=('0'*64);currentLocationStateCode='GA';currentLocationConfirmed=$true;callbackNumberConfirmed=$true;syntheticDataConfirmed=$true;hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false}) -eq 401)
  Add-Check 'Applicant request complaint-triage operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/complaint-triage' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/complaint-triage' $adminHeaders $complaintTriageBody) -eq 401)
  Add-Check 'Applicant request intake operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/intake' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/intake' $adminHeaders $requestIntakeBody) -eq 401)
  Add-Check 'Applicant request insurance-source operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/insurance-source' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/insurance-source' $adminHeaders $requestInsuranceSourceBody) -eq 401)
  Add-Check 'Applicant request eligibility operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/eligibility' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/eligibility' $adminHeaders $requestEligibilityBody) -eq 401)
  Add-Check 'Applicant request practice-network operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/practice-network' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/practice-network' $adminHeaders $requestPracticeNetworkBody) -eq 401)
  Add-Check 'Applicant request rendering-candidate operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/rendering-candidate' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/rendering-candidate' $adminHeaders $requestRenderingCandidateBody) -eq 401)
  Add-Check 'Applicant request participation-context operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-context' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-context' $adminHeaders $requestParticipationContextBody) -eq 401)
  Add-Check 'Applicant request participation-evaluation operations reject staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-evaluation' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/participation-evaluation' $adminHeaders $requestParticipationEvaluationBody) -eq 401)
  Add-Check 'Applicant request operational-review submission rejects staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/operational-review-submission' $adminHeaders) -eq 401 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/operational-review-submission' $adminHeaders $requestOperationalReviewSubmissionBody) -eq 401)
  Add-Check 'Applicant request queue status rejects staff-session substitution for the applicant access key' (
    (Invoke-Status 'GET' '/api/telehealth/v1/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/telehealth-request/queue-status' $adminHeaders) -eq 401)
  $missingPurpose=@{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='10'}
  $missingPurposeClaim=$missingPurpose.Clone();$missingPurposeClaim['X-Idempotency-Key']="th-auth-claim-missing-purpose-$([Guid]::NewGuid().ToString('N'))"
  $missingPurposeAuthorization=$missingPurpose.Clone();$missingPurposeAuthorization['X-Idempotency-Key']="th-auth-authorization-missing-purpose-$([Guid]::NewGuid().ToString('N'))"
  $missingPurposeQueueAuthorization=$missingPurpose.Clone();$missingPurposeQueueAuthorization['X-Idempotency-Key']="th-auth-queue-authorization-missing-purpose-$([Guid]::NewGuid().ToString('N'))"
  Add-Check 'Staff endpoints deny a missing purpose of use' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/operational-review' $missingPurpose) -eq 403 -and
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review' $missingPurpose) -eq 403 -and
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' $missingPurpose) -eq 403 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/claim' $missingPurposeClaim $claimBody) -eq 403 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/authorization' $missingPurposeAuthorization $authorizationBody) -eq 403 -and
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $missingPurpose) -eq 403 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $missingPurposeQueueAuthorization $requestQueueAuthorizationBody) -eq 403)
  $crossFacility=@{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='11';'X-AvenChart-Purpose-Of-Use'='healthcare-operations'}
  $crossFacilityClaim=$crossFacility.Clone();$crossFacilityClaim['X-Idempotency-Key']="th-auth-claim-cross-facility-$([Guid]::NewGuid().ToString('N'))"
  $crossFacilityAuthorization=$crossFacility.Clone();$crossFacilityAuthorization['X-Idempotency-Key']="th-auth-authorization-cross-facility-$([Guid]::NewGuid().ToString('N'))"
  $crossFacilityQueueAuthorization=$crossFacility.Clone();$crossFacilityQueueAuthorization['X-Idempotency-Key']="th-auth-queue-authorization-cross-facility-$([Guid]::NewGuid().ToString('N'))"
  Add-Check 'Staff endpoints obscure or deny a different facility' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/operational-review' $crossFacility) -in @(403,404) -and
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review' $crossFacility) -in @(403,404) -and
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' $crossFacility) -in @(403,404) -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/claim' $crossFacilityClaim $claimBody) -in @(403,404) -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/authorization' $crossFacilityAuthorization $authorizationBody) -in @(403,404) -and
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $crossFacility) -in @(403,404) -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $crossFacilityQueueAuthorization $requestQueueAuthorizationBody) -in @(403,404))

  $provider=Login-Staff 'gold-provider-01'
  $providerGrant = @($provider.accessContext.facilities | Where-Object { $_.facilityId -eq 10 })
  if ($providerGrant.Count -ne 1 -or @($provider.accessContext.purposes) -notcontains 'treatment') {
    throw 'Synthetic provider lacks the required facility or treatment grant.'
  }
  $providerHeaders=@{
    'X-AvenChart-Session'=$provider.sessionId
    'X-AvenChart-Facility-Id'='10'
    'X-AvenChart-Purpose-Of-Use'='treatment'
  }
  Add-Check 'Physician role cannot perform administrator operational review' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/operational-review' $providerHeaders) -eq 403)
  Add-Check 'Physician role cannot read the administrator practice-review inbox' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review' $providerHeaders) -eq 403)
  $providerClaimHeaders=$providerHeaders.Clone();$providerClaimHeaders['X-Idempotency-Key']="th-auth-claim-provider-$([Guid]::NewGuid().ToString('N'))"
  Add-Check 'Physician role cannot claim an administrator practice-review case' ((Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/claim' $providerClaimHeaders $claimBody) -eq 403)
  Add-Check 'Physician role cannot read a claimant-bound practice-review packet' ((Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' $providerHeaders) -eq 403)
  $providerAuthorizationHeaders=$providerHeaders.Clone();$providerAuthorizationHeaders['X-Idempotency-Key']="th-auth-authorization-provider-$([Guid]::NewGuid().ToString('N'))"
  Add-Check 'Physician role cannot authorize an administrator practice-review case' ((Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-practice-review/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/authorization' $providerAuthorizationHeaders $authorizationBody) -eq 403)
  Add-Check 'Physician role cannot read or authorize an applicant request queue decision' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $providerHeaders) -eq 403 -and
    (Invoke-Status 'POST' '/api/telehealth/v1/admin/applicant-requests/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/queue-authorization' $providerAuthorizationHeaders $requestQueueAuthorizationBody) -eq 403)
  Add-Check 'Physician role cannot read or decide prospective applicant identity review' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-identity-review' $providerHeaders) -eq 403 -and
    (Invoke-Status 'PUT' '/api/telehealth/v1/admin/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/identity-review-decision' $providerHeaders @{expectedVersion=2;decision='ApprovedForProspectiveIntake';reason='Synthetic authorization boundary.';syntheticDataConfirmed=$true}) -eq 403)
  Add-Check 'Physician role cannot read or decide prospective promotion authorization' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-promotion-authorization' $providerHeaders) -eq 403 -and
    (Invoke-Status 'PUT' '/api/telehealth/v1/admin/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/promotion-authorization-decision' $providerHeaders @{expectedVersion=10;decision='AuthorizedForSyntheticPromotion';reason='Synthetic promotion authorization boundary.';noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 403)
  Add-Check 'Physician role cannot read or execute canonical synthetic promotion' (
    (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-synthetic-promotion' $providerHeaders) -eq 403 -and
    (Invoke-Status 'PUT' '/api/telehealth/v1/admin/applicants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/synthetic-promotion' $providerHeaders @{expectedVersion=11;command='PromoteAuthorizedSyntheticApplicant';reason='Synthetic patient promotion authorization boundary.';canonicalPatientCreationAcknowledged=$true;noPortalNoCareAcknowledged=$true}) -eq 403)
  Add-Check 'Administrator role cannot enter the physician queue' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/queue' $adminHeaders) -eq 403)
  $adminVideoHeaders=$adminHeaders.Clone();$adminVideoHeaders['X-Idempotency-Key']="th-auth-video-$([Guid]::NewGuid().ToString('N'))"
  Add-Check 'Administrator role cannot issue a physician connection grant' ((Invoke-Status 'POST' '/api/telehealth/v1/clinician/reservations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/connection-grants' $adminVideoHeaders @{expectedVersion=1;browserSupported=$true;cameraAvailable=$true;microphoneAvailable=$true;speakerAvailable=$true;networkQuality='unknown';syntheticDataConfirmed=$true}) -eq 403)
  Add-Check 'Administrator role cannot start a consultation lifecycle' ((Invoke-Status 'POST' '/api/telehealth/v1/clinician/reservations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/consultations/start' $adminVideoHeaders @{expectedVersion=1;patientLocationState='GA';patientIdentityDiscussed=$true;callbackConfirmed=$true;privacyConfirmed=$true;consentDiscussed=$true;noConcerningSymptomChange=$true;emergencyPlanConfirmed=$true;communicationSufficient=$true;syntheticDataConfirmed=$true}) -eq 403)
  Add-Check 'Administrator role cannot read a consultation workspace' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/workspace' $adminHeaders) -eq 403)
  Add-Check 'Administrator role cannot write a consultation draft' ((Invoke-Status 'PUT' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/documentation/draft' $adminHeaders @{expectedVersion=0;subjective='synthetic'}) -eq 403)
  Add-Check 'Administrator role cannot enter a consultation wrap-up lifecycle' ((Invoke-Status 'POST' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/wrap-up' $adminVideoHeaders @{expectedVersion=1;syntheticSessionEndedConfirmed=$true;documentationStillIncompleteAcknowledged=$true;wrapUpResponsibilityAcknowledged=$true}) -eq 403)
  Add-Check 'Administrator role cannot search physician-owned pharmacy choices' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pharmacy-choices' $adminHeaders) -eq 403)
  Add-Check 'Administrator role cannot record a pharmacy destination draft' ((Invoke-Status 'PUT' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/pharmacy-choice' $adminVideoHeaders @{expectedVersion=0;directoryEntryId='00000000-0000-4000-8000-000000001001';patientChoiceConfirmed=$true;syntheticDataConfirmed=$true}) -eq 403)
  Add-Check 'Administrator role cannot read physician-owned prescription preparation' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/prescription-preparation-draft' $adminHeaders) -eq 403)
  Add-Check 'Administrator role cannot record a prescription-preparation draft' ((Invoke-Status 'PUT' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/prescription-preparation-draft' $adminVideoHeaders @{expectedVersion=0;rxNormCode='860975';doseAmount=1;doseUnit='tablet';frequency='synthetic';quantityValue=1;quantityUnit='tablet';durationDays=1;refills=0;indication='synthetic';directions='synthetic';medicationListReviewed=$true;allergyListReviewed=$true;adequateEvaluationCompleted=$true;syntheticDataConfirmed=$true}) -eq 403)
  Add-Check 'Administrator role cannot read a physician-owned safety-disposition draft' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/safety-disposition-draft' $adminHeaders) -eq 403)
  Add-Check 'Administrator role cannot record a physician-owned safety-disposition draft' ((Invoke-Status 'PUT' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/safety-disposition-draft' $adminVideoHeaders @{expectedVersion=0;dispositionCode='TreatedTelehealth';adequateEvaluationCompleted=$true;followUpOwner='Patient';followUpTimeframe='synthetic';nextStepInstructions='synthetic';warningEscalationInstructions='synthetic';communicationMethod='DiscussedDuringSyntheticConsultation';communicationCompleted=$true;locationCallbackReconfirmed=$false;emergencyInstructionProvided=$false;syntheticDataConfirmed=$true}) -eq 403)
  Add-Check 'Administrator role cannot read physician completion prerequisites' ((Invoke-Status 'GET' '/api/telehealth/v1/clinician/consultations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/completion-prerequisites' $adminHeaders) -eq 403)
}
catch {
  Add-Check 'Telehealth authorization matrix execution' $false @{
    message = $_.Exception.Message
    stack = $_.ScriptStackTrace
  }
}
finally{$result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decisions=@('TH-DEC-0003','TH-DEC-0005','TH-DEC-0006','TH-DEC-0007','TH-DEC-0008','TH-DEC-0009','TH-DEC-0010','TH-DEC-0011','TH-DEC-0012','TH-DEC-0013','TH-DEC-0014','TH-DEC-0015','TH-DEC-0016','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019','TH-DEC-0020','TH-DEC-0021','TH-DEC-0022','TH-DEC-0023','TH-DEC-0024','TH-DEC-0025','TH-DEC-0026','TH-DEC-0027','TH-DEC-0028','TH-DEC-0029','TH-DEC-0030','TH-DEC-0031','TH-DEC-0032','TH-DEC-0033','TH-DEC-0034','TH-DEC-0035','TH-DEC-0036','TH-DEC-0037','TH-DEC-0038','TH-DEC-0039','TH-DEC-0040','TH-DEC-0041','TH-DEC-0042','TH-DEC-0043','TH-DEC-0044','TH-DEC-0045','TH-DEC-0046','TH-DEC-0047','TH-DEC-0048','TH-DEC-0049','TH-DEC-0050','TH-DEC-0051','TH-DEC-0052','TH-DEC-0053','TH-DEC-0054','TH-DEC-0055','TH-DEC-0056');checks=$checks};$result|ConvertTo-Json -Depth 10|Set-Content $resultPath -Encoding utf8;$result|ConvertTo-Json -Depth 10}
if(-not$passed){exit 1}
