# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) { throw 'Telehealth OpenAPI proof is local-only.' }
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-openapi-contract.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new(); $passed = $true
function Add-Check([string]$Name,[bool]$Result,[object]$Details=$null){$script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details});if(-not $Result){$script:passed=$false}}
function Get-Property($Object,[string]$Name){if($null -eq $Object){return $null};$p=$Object.PSObject.Properties[$Name];if($null -eq $p){return $null};$p.Value}
function Get-Operation($Document,[string]$Path,[string]$Method){Get-Property (Get-Property (Get-Property $Document 'paths') $Path) $Method}
function Has-Header($Operation,[string]$Name){ @($Operation.parameters | Where-Object { $_.name -eq $Name -and $_.in -eq 'header' -and $_.required -eq $true }).Count -eq 1 }
function Has-Security($Operation,[string]$Name){ @($Operation.security | Where-Object { @($_.PSObject.Properties.Name) -contains $Name }).Count -ge 1 }
try {
    $document = Invoke-RestMethod "$ApiBaseUrl/openapi/v1.json" -TimeoutSec 20
    $expected = @(
      '/api/telehealth/v1/context','/api/telehealth/v1/applicants','/api/telehealth/v1/applicants/{applicantId}',
      '/api/telehealth/v1/applicants/{applicantId}/contact-verification','/api/telehealth/v1/applicants/{applicantId}/safety-triage','/api/telehealth/v1/applicants/{applicantId}/visit-purpose','/api/telehealth/v1/applicants/{applicantId}/practice-network-precheck/options','/api/telehealth/v1/applicants/{applicantId}/practice-network-precheck','/api/telehealth/v1/applicants/{applicantId}/member-insurance-details','/api/telehealth/v1/applicants/{applicantId}/eligibility','/api/telehealth/v1/applicants/{applicantId}/practice-network-determination','/api/telehealth/v1/applicants/{applicantId}/identity-proofing','/api/telehealth/v1/applicants/{applicantId}/telehealth-notice','/api/telehealth/v1/applicants/{applicantId}/telehealth-notice/acknowledgment','/api/telehealth/v1/applicants/{applicantId}/registration-details','/api/telehealth/v1/applicants/{applicantId}/registration-details/confirmation','/api/telehealth/v1/applicants/{applicantId}/insurance-handoff','/api/telehealth/v1/applicants/{applicantId}/insurance-handoff/confirmation','/api/telehealth/v1/applicants/{applicantId}/communication-access-readiness','/api/telehealth/v1/applicants/{applicantId}/device-preparation','/api/telehealth/v1/applicants/{applicantId}/clinical-information-inventory','/api/telehealth/v1/applicants/{applicantId}/medication-information','/api/telehealth/v1/applicants/{applicantId}/allergy-information','/api/telehealth/v1/applicants/{applicantId}/health-history-information','/api/telehealth/v1/applicants/{applicantId}/clinical-information-summary','/api/telehealth/v1/applicants/{applicantId}/pre-request-readiness','/api/telehealth/v1/applicants/{applicantId}/practice-review-submission','/api/telehealth/v1/applicants/{applicantId}/telehealth-request','/api/telehealth/v1/applicants/{applicantId}/telehealth-request/location','/api/telehealth/v1/applicants/{applicantId}/telehealth-request/safety','/api/telehealth/v1/applicants/{applicantId}/telehealth-request/complaint-triage','/api/telehealth/v1/applicants/{applicantId}/telehealth-request/intake','/api/telehealth/v1/applicants/{applicantId}/telehealth-request/insurance-source','/api/telehealth/v1/patient/requests',
      '/api/telehealth/v1/patient/requests/{requestId}/location','/api/telehealth/v1/patient/requests/{requestId}/triage',
      '/api/telehealth/v1/patient/requests/{requestId}/status','/api/telehealth/v1/patient/requests/{requestId}/readiness','/api/telehealth/v1/patient/requests/{requestId}/coverage/verify',
      '/api/telehealth/v1/patient/requests/{requestId}/connection-grants',
      '/api/telehealth/v1/admin/applicant-practice-review','/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}','/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}/claim','/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}/authorization','/api/telehealth/v1/admin/applicant-identity-review','/api/telehealth/v1/admin/applicants/{applicantId}/identity-review-decision',
      '/api/telehealth/v1/admin/applicant-promotion-authorization','/api/telehealth/v1/admin/applicants/{applicantId}/promotion-authorization-decision',
      '/api/telehealth/v1/admin/applicant-synthetic-promotion','/api/telehealth/v1/admin/applicants/{applicantId}/synthetic-promotion',
      '/api/telehealth/v1/admin/operational-review','/api/telehealth/v1/admin/requests/{requestId}/authorize',
      '/api/telehealth/v1/clinician/queue','/api/telehealth/v1/clinician/shifts','/api/telehealth/v1/clinician/reservations/reserve-next',
      '/api/telehealth/v1/clinician/reservations/{reservationId}/connection-grants',
      '/api/telehealth/v1/clinician/reservations/{reservationId}/consultations/start',
      '/api/telehealth/v1/clinician/consultations/{consultationId}/workspace',
      '/api/telehealth/v1/clinician/consultations/{consultationId}/documentation/draft',
      '/api/telehealth/v1/clinician/consultations/{consultationId}/wrap-up',
      '/api/telehealth/v1/clinician/consultations/{consultationId}/pharmacy-choices',
      '/api/telehealth/v1/clinician/consultations/{consultationId}/pharmacy-choice',
      '/api/telehealth/v1/clinician/consultations/{consultationId}/prescription-preparation-draft',
      '/api/telehealth/v1/clinician/consultations/{consultationId}/safety-disposition-draft',
      '/api/telehealth/v1/clinician/consultations/{consultationId}/completion-prerequisites')
    $paths = @($document.paths.PSObject.Properties.Name)
    Add-Check 'Versioned synthetic telehealth paths are published only when enabled' (@($expected | Where-Object { $_ -notin $paths }).Count -eq 0) @{ paths=$paths | Where-Object { $_ -like '/api/telehealth/*' } }
    $patientCreate = Get-Operation $document '/api/telehealth/v1/patient/requests' 'post'
    Add-Check 'Patient mutation documents portal/OIDC authentication and idempotency' (
      (Has-Security $patientCreate 'AvenChartPatientPortalSession') -and (Has-Security $patientCreate 'AvenChartOidcBearer') -and (Has-Header $patientCreate 'X-Idempotency-Key'))
    $applicantCreate = Get-Operation $document '/api/telehealth/v1/applicants' 'post'
    $applicantRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}' 'get'
    $applicantVerify = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/contact-verification' 'post'
    Add-Check 'Prospective-applicant operations publish separate access-key security without patient or staff identity' (
      (Has-Security $applicantCreate 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $applicantRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $applicantVerify 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $applicantCreate 'AvenChartPatientPortalSession') -and
      -not (Has-Security $applicantCreate 'AvenChartLocalStaffSession'))
    Add-Check 'Prospective-applicant create and verification publish idempotency, typed bodies, conflict, and expiry outcomes' (
      (Has-Header $applicantCreate 'X-Idempotency-Key') -and $null-ne(Get-Property $applicantCreate 'requestBody') -and
      $null-ne(Get-Property $applicantCreate.responses '409') -and
      (Has-Header $applicantVerify 'X-Idempotency-Key') -and $null-ne(Get-Property $applicantVerify 'requestBody') -and
      $null-ne(Get-Property $applicantVerify.responses '409') -and $null-ne(Get-Property $applicantVerify.responses '410') -and
      -not (Has-Header $applicantRead 'X-Idempotency-Key'))
    $applicantSafety = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/safety-triage' 'post'
    $applicantSafetyRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantSafety 'requestBody') 'content') 'application/json').schema '$ref'
    $applicantSafetyRequestSchemaName = ($applicantSafetyRequestReference -split '/')[-1]
    $applicantSafetyRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantSafetyRequestSchemaName
    $applicantSafetyRequestJson = $applicantSafetyRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $applicantSafetyResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantSafety.responses '200') 'content') 'application/json').schema '$ref'
    $applicantSafetyResponseSchemaName = ($applicantSafetyResponseReference -split '/')[-1]
    $applicantSafetyResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantSafetyResponseSchemaName
    $applicantSafetyResponseJson = $applicantSafetyResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Prospective safety triage publishes applicant-only access, exact replay/version inputs, bounded failures, and explicit no-consequence output' (
      (Has-Security $applicantSafety 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $applicantSafety 'AvenChartPatientPortalSession') -and
      -not (Has-Security $applicantSafety 'AvenChartLocalStaffSession') -and
      (Has-Header $applicantSafety 'X-Idempotency-Key') -and
      $null-ne(Get-Property $applicantSafety 'requestBody') -and
      $null-ne(Get-Property $applicantSafety.responses '400') -and
      $null-ne(Get-Property $applicantSafety.responses '401') -and
      $null-ne(Get-Property $applicantSafety.responses '404') -and
      $null-ne(Get-Property $applicantSafety.responses '409') -and
      $null-ne(Get-Property $applicantSafety.responses '410') -and
      $applicantSafetyRequestJson-match'expectedVersion' -and
      $applicantSafetyRequestJson-match'currentLocationStateCode' -and
      $applicantSafetyRequestJson-match'currentLocationConfirmed' -and
      $applicantSafetyRequestJson-match'hasEmergencyWarning' -and
      $applicantSafetyRequestJson-match'severeOrWorsening' -and
      $applicantSafetyRequestJson-match'requiresHandsOnExam' -and
      $applicantSafetyRequestJson-match'unsure' -and
      $applicantSafetyRequestJson-match'syntheticDataConfirmed' -and
      $applicantSafetyRequestJson-notmatch'patientId|candidateId|accessKey|insurance|complaint|diagnosis|medication' -and
      $applicantSafetyResponseJson-match'safetyDisposition' -and
      $applicantSafetyResponseJson-match'protocolKey' -and
      $applicantSafetyResponseJson-match'protocolVersion' -and
      $applicantSafetyResponseJson-match'mayContinueProspectiveIntake' -and
      $applicantSafetyResponseJson-match'identityProofed' -and
      $applicantSafetyResponseJson-match'clinicalReviewPerformed' -and
      $applicantSafetyResponseJson-match'canonicalPatientCreated' -and
      $applicantSafetyResponseJson-match'coverageChecked' -and
      $applicantSafetyResponseJson-match'requestCreated' -and
      $applicantSafetyResponseJson-match'queueEnabled' -and
      $applicantSafetyResponseJson-match'careEnabled' -and
      $applicantSafetyResponseJson-notmatch'patientId|candidateId|canonicalPatientId|staffId|actorId|answersFingerprint|protocolContentHash')
    $applicantPurpose = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/visit-purpose' 'post'
    $applicantPurposeRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantPurpose 'requestBody') 'content') 'application/json').schema '$ref'
    $applicantPurposeRequestSchemaName = ($applicantPurposeRequestReference -split '/')[-1]
    $applicantPurposeRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantPurposeRequestSchemaName
    $applicantPurposeRequestJson = $applicantPurposeRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $applicantPurposeResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantPurpose.responses '200') 'content') 'application/json').schema '$ref'
    $applicantPurposeResponseSchemaName = ($applicantPurposeResponseReference -split '/')[-1]
    $applicantPurposeResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantPurposeResponseSchemaName
    $applicantPurposeResponseJson = $applicantPurposeResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Prospective visit purpose publishes applicant-only access, controlled minimal input, bounded failures, and explicit no-consequence output' (
      (Has-Security $applicantPurpose 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $applicantPurpose 'AvenChartPatientPortalSession') -and
      -not (Has-Security $applicantPurpose 'AvenChartLocalStaffSession') -and
      (Has-Header $applicantPurpose 'X-Idempotency-Key') -and
      $null-ne(Get-Property $applicantPurpose 'requestBody') -and
      $null-ne(Get-Property $applicantPurpose.responses '400') -and
      $null-ne(Get-Property $applicantPurpose.responses '401') -and
      $null-ne(Get-Property $applicantPurpose.responses '404') -and
      $null-ne(Get-Property $applicantPurpose.responses '409') -and
      $null-ne(Get-Property $applicantPurpose.responses '410') -and
      $applicantPurposeRequestJson-match'expectedVersion' -and
      $applicantPurposeRequestJson-match'purposeCategory' -and
      $applicantPurposeRequestJson-match'syntheticDataConfirmed' -and
      $applicantPurposeRequestJson-notmatch'freeText|complaintSummary|symptom|patientId|requestId|insurance|diagnosis|medication|accessKey' -and
      $applicantPurposeResponseJson-match'purposeCategory' -and
      $applicantPurposeResponseJson-match'purposeDisplayLabel' -and
      $applicantPurposeResponseJson-match'clinicalProtocolPublished' -and
      $applicantPurposeResponseJson-match'clinicalEligibilityDetermined' -and
      $applicantPurposeResponseJson-match'identityProofed' -and
      $applicantPurposeResponseJson-match'canonicalPatientCreated' -and
      $applicantPurposeResponseJson-match'coverageChecked' -and
      $applicantPurposeResponseJson-match'requestCreated' -and
      $applicantPurposeResponseJson-match'queueEnabled' -and
      $applicantPurposeResponseJson-match'careEnabled' -and
      $applicantPurposeResponseJson-notmatch'patientId|canonicalPatientId|staffId|actorId|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId')
    $practiceNetworkOptions = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/practice-network-precheck/options' 'get'
    $practiceNetworkWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/practice-network-precheck' 'post'
    $practiceNetworkRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceNetworkWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $practiceNetworkRequestSchemaName = ($practiceNetworkRequestReference -split '/')[-1]
    $practiceNetworkRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceNetworkRequestSchemaName
    $practiceNetworkRequestJson = $practiceNetworkRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $practiceNetworkResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceNetworkWrite.responses '200') 'content') 'application/json').schema '$ref'
    $practiceNetworkResponseSchemaName = ($practiceNetworkResponseReference -split '/')[-1]
    $practiceNetworkResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceNetworkResponseSchemaName
    $practiceNetworkResponseJson = $practiceNetworkResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Prospective practice-network options publish applicant-only private read semantics without mutation identity' (
      (Has-Security $practiceNetworkOptions 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $practiceNetworkOptions 'AvenChartPatientPortalSession') -and
      -not (Has-Security $practiceNetworkOptions 'AvenChartLocalStaffSession') -and
      -not (Has-Header $practiceNetworkOptions 'X-Idempotency-Key') -and
      $null-eq(Get-Property $practiceNetworkOptions 'requestBody') -and
      $null-ne(Get-Property $practiceNetworkOptions.responses '200') -and
      $null-ne(Get-Property $practiceNetworkOptions.responses '401') -and
      $null-ne(Get-Property $practiceNetworkOptions.responses '404') -and
      $null-ne(Get-Property $practiceNetworkOptions.responses '409') -and
      $null-ne(Get-Property $practiceNetworkOptions.responses '410'))
    Add-Check 'Prospective practice-network command publishes minimal opaque input, bounded failures, and explicit false eligibility, exact-network, coverage, and consequence output' (
      (Has-Security $practiceNetworkWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $practiceNetworkWrite 'AvenChartPatientPortalSession') -and
      -not (Has-Security $practiceNetworkWrite 'AvenChartLocalStaffSession') -and
      (Has-Header $practiceNetworkWrite 'X-Idempotency-Key') -and
      $null-ne(Get-Property $practiceNetworkWrite 'requestBody') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '400') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '401') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '404') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '409') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '410') -and
      $practiceNetworkRequestJson-match'expectedVersion' -and
      $practiceNetworkRequestJson-match'planKey' -and
      $practiceNetworkRequestJson-match'syntheticDataConfirmed' -and
      $practiceNetworkRequestJson-notmatch'member|subscriber|policy|group|card|payerDisplay|productDisplay|networkStatus|physician|price|payment|patientId|requestId|accessKey' -and
      $practiceNetworkResponseJson-match'practiceNetworkStatus' -and
      $practiceNetworkResponseJson-match'adapterMode' -and
      $practiceNetworkResponseJson-match'catalogVersion' -and
      $practiceNetworkResponseJson-match'memberEligibilityChecked' -and
      $practiceNetworkResponseJson-match'memberBenefitsChecked' -and
      $practiceNetworkResponseJson-match'renderingPhysicianNetworkChecked' -and
      $practiceNetworkResponseJson-match'coverageVerified' -and
      $practiceNetworkResponseJson-match'exactNetworkConfirmed' -and
      $practiceNetworkResponseJson-match'canonicalPatientCreated' -and
      $practiceNetworkResponseJson-match'requestCreated' -and
      $practiceNetworkResponseJson-match'queueEnabled' -and
      $practiceNetworkResponseJson-match'externalCallPerformed' -and
      $practiceNetworkResponseJson-notmatch'patientId|canonicalPatientId|memberId|subscriberId|policyNumber|groupNumber|staffId|actorId|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId|visitPurposeId')
    $memberDetailsWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/member-insurance-details' 'post'
    $memberDetailsRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $memberDetailsWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $memberDetailsRequestSchemaName = ($memberDetailsRequestReference -split '/')[-1]
    $memberDetailsRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $memberDetailsRequestSchemaName
    $memberDetailsRequestJson = $memberDetailsRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $memberDetailsResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $memberDetailsWrite.responses '200') 'content') 'application/json').schema '$ref'
    $memberDetailsResponseSchemaName = ($memberDetailsResponseReference -split '/')[-1]
    $memberDetailsResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $memberDetailsResponseSchemaName
    $memberDetailsResponseJson = $memberDetailsResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Prospective member-details command publishes applicant-only conditional input, bounded failures, mask-only receipt, and explicit false eligibility, coverage, and consequence output' (
      (Has-Security $memberDetailsWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $memberDetailsWrite 'AvenChartPatientPortalSession') -and
      -not (Has-Security $memberDetailsWrite 'AvenChartLocalStaffSession') -and
      (Has-Header $memberDetailsWrite 'X-Idempotency-Key') -and
      $null-ne(Get-Property $memberDetailsWrite 'requestBody') -and
      $null-ne(Get-Property $memberDetailsWrite.responses '400') -and
      $null-ne(Get-Property $memberDetailsWrite.responses '401') -and
      $null-ne(Get-Property $memberDetailsWrite.responses '404') -and
      $null-ne(Get-Property $memberDetailsWrite.responses '409') -and
      $null-ne(Get-Property $memberDetailsWrite.responses '410') -and
      $memberDetailsRequestJson-match'expectedVersion' -and
      $memberDetailsRequestJson-match'memberId' -and
      $memberDetailsRequestJson-match'groupNumber' -and
      $memberDetailsRequestJson-match'subscriberRelationship' -and
      $memberDetailsRequestJson-match'subscriberFirstName' -and
      $memberDetailsRequestJson-match'subscriberLastName' -and
      $memberDetailsRequestJson-match'subscriberDateOfBirth' -and
      $memberDetailsRequestJson-match'detailsConfirmed' -and
      $memberDetailsRequestJson-match'syntheticDataConfirmed' -and
      $memberDetailsRequestJson-notmatch'planKey|payerDisplay|productDisplay|networkStatus|coverageStatus|card|ssn|government|price|payment|patientId|requestId|accessKey' -and
      $memberDetailsResponseJson-match'memberIdMask' -and
      $memberDetailsResponseJson-match'groupNumberMask' -and
      $memberDetailsResponseJson-match'subscriberRelationship' -and
      $memberDetailsResponseJson-match'protectionScheme' -and
      $memberDetailsResponseJson-match'memberMatched' -and
      $memberDetailsResponseJson-match'memberEligibilityChecked' -and
      $memberDetailsResponseJson-match'memberBenefitsChecked' -and
      $memberDetailsResponseJson-match'renderingPhysicianNetworkChecked' -and
      $memberDetailsResponseJson-match'coverageVerified' -and
      $memberDetailsResponseJson-match'exactNetworkConfirmed' -and
      $memberDetailsResponseJson-match'coverageRecordCreated' -and
      $memberDetailsResponseJson-match'requestCreated' -and
      $memberDetailsResponseJson-match'queueEnabled' -and
      $memberDetailsResponseJson-match'externalCallPerformed' -and
      $memberDetailsResponseJson-notmatch'"memberId":|"groupNumber":|subscriberFirstName|subscriberLastName|subscriberDateOfBirth|protectedPayload|protectionPurpose|patientId|canonicalPatientId|staffId|actorId|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId|visitPurposeId|practiceNetworkPrecheckId')
    $eligibilityWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/eligibility' 'post'
    $eligibilityRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $eligibilityWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $eligibilityRequestSchemaName = ($eligibilityRequestReference -split '/')[-1]
    $eligibilityRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $eligibilityRequestSchemaName
    $eligibilityRequestJson = $eligibilityRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $eligibilityResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $eligibilityWrite.responses '200') 'content') 'application/json').schema '$ref'
    $eligibilityResponseSchemaName = ($eligibilityResponseReference -split '/')[-1]
    $eligibilityResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $eligibilityResponseSchemaName
    $eligibilityResponseJson = $eligibilityResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Prospective eligibility publishes applicant-only minimal input, bounded failures, normalized outcome fields, masks, and explicit false exact-network, coverage, raw-transaction, and consequence output' (
      (Has-Security $eligibilityWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $eligibilityWrite 'AvenChartPatientPortalSession') -and
      -not (Has-Security $eligibilityWrite 'AvenChartLocalStaffSession') -and
      (Has-Header $eligibilityWrite 'X-Idempotency-Key') -and
      $null-ne(Get-Property $eligibilityWrite 'requestBody') -and
      $null-ne(Get-Property $eligibilityWrite.responses '400') -and
      $null-ne(Get-Property $eligibilityWrite.responses '401') -and
      $null-ne(Get-Property $eligibilityWrite.responses '404') -and
      $null-ne(Get-Property $eligibilityWrite.responses '409') -and
      $null-ne(Get-Property $eligibilityWrite.responses '410') -and
      $eligibilityRequestJson-match'expectedVersion' -and
      $eligibilityRequestJson-match'syntheticDataConfirmed' -and
      $eligibilityRequestJson-notmatch'member|subscriber|payer|plan|network|benefit|eligibilityStatus|x12|price|payment|patientId|requestId|accessKey' -and
      $eligibilityResponseJson-match'memberIdMask' -and
      $eligibilityResponseJson-match'groupNumberMask' -and
      $eligibilityResponseJson-match'adapterMode' -and
      $eligibilityResponseJson-match'compatibilityTarget' -and
      $eligibilityResponseJson-match'transportOutcome' -and
      $eligibilityResponseJson-match'memberMatchStatus' -and
      $eligibilityResponseJson-match'eligibilityStatus' -and
      $eligibilityResponseJson-match'benefitInformationStatus' -and
      $eligibilityResponseJson-match'businessOutcome' -and
      $eligibilityResponseJson-match'rawTransactionCreated' -and
      $eligibilityResponseJson-match'renderingPhysicianNetworkChecked' -and
      $eligibilityResponseJson-match'coverageVerified' -and
      $eligibilityResponseJson-match'exactNetworkConfirmed' -and
      $eligibilityResponseJson-match'coverageRecordCreated' -and
      $eligibilityResponseJson-match'requestCreated' -and
      $eligibilityResponseJson-match'queueEnabled' -and
      $eligibilityResponseJson-match'externalCallPerformed' -and
      $eligibilityResponseJson-notmatch'"memberId":|"groupNumber":|subscriberFirstName|subscriberLastName|subscriberDateOfBirth|protectedPayload|rawRequest|rawResponse|x12Payload|patientId|canonicalPatientId|staffId|actorId|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId|visitPurposeId|practiceNetworkPrecheckId|memberInsuranceDetailsId')
    $practiceNetworkWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/practice-network-determination' 'post'
    $practiceNetworkRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceNetworkWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $practiceNetworkRequestSchemaName = ($practiceNetworkRequestReference -split '/')[-1]
    $practiceNetworkRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceNetworkRequestSchemaName
    $practiceNetworkRequestJson = $practiceNetworkRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $practiceNetworkResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceNetworkWrite.responses '200') 'content') 'application/json').schema '$ref'
    $practiceNetworkResponseSchemaName = ($practiceNetworkResponseReference -split '/')[-1]
    $practiceNetworkResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceNetworkResponseSchemaName
    $practiceNetworkResponseJson = $practiceNetworkResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Prospective practice-network determination publishes applicant-only minimal input, bounded failures, separate eligibility and normalized network outcomes, Plan-Net compatibility metadata, and explicit false physician, FHIR, coverage, and consequence output' (
      (Has-Security $practiceNetworkWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $practiceNetworkWrite 'AvenChartPatientPortalSession') -and
      -not (Has-Security $practiceNetworkWrite 'AvenChartLocalStaffSession') -and
      (Has-Header $practiceNetworkWrite 'X-Idempotency-Key') -and
      $null-ne(Get-Property $practiceNetworkWrite 'requestBody') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '400') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '401') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '404') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '409') -and
      $null-ne(Get-Property $practiceNetworkWrite.responses '410') -and
      $practiceNetworkRequestJson-match'expectedVersion' -and
      $practiceNetworkRequestJson-match'syntheticDataConfirmed' -and
      $practiceNetworkRequestJson-notmatch'member|subscriber|payer|plan|network|benefit|eligibility|physician|npi|dateOfService|serviceCategory|patientId|requestId|accessKey' -and
      $practiceNetworkResponseJson-match'eligibilityStatus' -and
      $practiceNetworkResponseJson-match'eligibilityBusinessOutcome' -and
      $practiceNetworkResponseJson-match'compatibilityTarget' -and
      $practiceNetworkResponseJson-match'planNetworkMatchStatus' -and
      $practiceNetworkResponseJson-match'practiceAffiliationStatus' -and
      $practiceNetworkResponseJson-match'serviceAvailabilityStatus' -and
      $practiceNetworkResponseJson-match'newPatientAcceptanceStatus' -and
      $practiceNetworkResponseJson-match'practiceNetworkChecked' -and
      $practiceNetworkResponseJson-match'practiceInNetwork' -and
      $practiceNetworkResponseJson-match'newPatientsAccepted' -and
      $practiceNetworkResponseJson-match'fhirResourceCreated' -and
      $practiceNetworkResponseJson-match'liveDirectoryQueried' -and
      $practiceNetworkResponseJson-match'renderingPhysicianNetworkChecked' -and
      $practiceNetworkResponseJson-match'exactNetworkConfirmed' -and
      $practiceNetworkResponseJson-match'coverageVerified' -and
      $practiceNetworkResponseJson-match'requestCreated' -and
      $practiceNetworkResponseJson-match'queueEnabled' -and
      $practiceNetworkResponseJson-match'externalCallPerformed' -and
      $practiceNetworkResponseJson-notmatch'"memberId":|"groupNumber":|subscriberFirstName|subscriberLastName|subscriberDateOfBirth|protectedPayload|rawRequest|rawResponse|fhirBundle|npi|physicianId|patientId|canonicalPatientId|staffId|actorId|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId|visitPurposeId|practiceNetworkPrecheckId|memberInsuranceDetailsId|eligibilityResultId')
    $identityProofingWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/identity-proofing' 'post'
    $identityProofingRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $identityProofingWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $identityProofingRequestSchemaName = ($identityProofingRequestReference -split '/')[-1]
    $identityProofingRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $identityProofingRequestSchemaName
    $identityProofingRequestJson = $identityProofingRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $identityProofingResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $identityProofingWrite.responses '200') 'content') 'application/json').schema '$ref'
    $identityProofingResponseSchemaName = ($identityProofingResponseReference -split '/')[-1]
    $identityProofingResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $identityProofingResponseSchemaName
    $identityProofingResponseJson = $identityProofingResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Prospective identity proofing publishes applicant-only acknowledgment input, bounded failures, normalized process-fixture statuses, NIST-concepts-only metadata, and explicit false evidence, assurance, identity, and consequence output' (
      (Has-Security $identityProofingWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $identityProofingWrite 'AvenChartPatientPortalSession') -and
      -not (Has-Security $identityProofingWrite 'AvenChartLocalStaffSession') -and
      (Has-Header $identityProofingWrite 'X-Idempotency-Key') -and
      $null-ne(Get-Property $identityProofingWrite 'requestBody') -and
      $null-ne(Get-Property $identityProofingWrite.responses '400') -and
      $null-ne(Get-Property $identityProofingWrite.responses '401') -and
      $null-ne(Get-Property $identityProofingWrite.responses '404') -and
      $null-ne(Get-Property $identityProofingWrite.responses '409') -and
      $null-ne(Get-Property $identityProofingWrite.responses '410') -and
      $identityProofingRequestJson-match'expectedVersion' -and
      $identityProofingRequestJson-match'privacyNoticeAcknowledged' -and
      $identityProofingRequestJson-match'syntheticDataConfirmed' -and
      $identityProofingRequestJson-notmatch'legalFirstName|legalLastName|dateOfBirth|email|phone|address|member|subscriber|payer|plan|network|document|governmentIdentifier|biometric|evidencePackage|proofingResult|assuranceLevel|patientId|requestId|accessKey' -and
      $identityProofingResponseJson-match'compatibilityTarget' -and
      $identityProofingResponseJson-match'evidenceCollectionStatus' -and
      $identityProofingResponseJson-match'evidenceValidationStatus' -and
      $identityProofingResponseJson-match'attributeValidationStatus' -and
      $identityProofingResponseJson-match'applicantVerificationStatus' -and
      $identityProofingResponseJson-match'fraudCheckStatus' -and
      $identityProofingResponseJson-match'businessOutcome' -and
      $identityProofingResponseJson-match'assuranceLevelAchieved' -and
      $identityProofingResponseJson-match'identityEvidenceCollected' -and
      $identityProofingResponseJson-match'governmentIdentifierCollected' -and
      $identityProofingResponseJson-match'biometricDataCollected' -and
      $identityProofingResponseJson-match'authoritativeSourceQueried' -and
      $identityProofingResponseJson-match'proofingNotificationSent' -and
      $identityProofingResponseJson-match'authenticatorBound' -and
      $identityProofingResponseJson-match'identityProofed' -and
      $identityProofingResponseJson-match'canonicalPatientCreated' -and
      $identityProofingResponseJson-match'requestCreated' -and
      $identityProofingResponseJson-match'queueEnabled' -and
      $identityProofingResponseJson-match'externalCallPerformed' -and
      $identityProofingResponseJson-notmatch'legalFirstName|legalLastName|dateOfBirth|maskedEmail|maskedPhone|memberIdMask|groupNumberMask|subscriberFirstName|subscriberLastName|subscriberDateOfBirth|protectedPayload|rawRequest|rawResponse|documentImage|biometricTemplate|authoritativeResponse|patientId|canonicalPatientId|staffId|actorId|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId|visitPurposeId|practiceNetworkPrecheckId|memberInsuranceDetailsId|eligibilityResultId|networkDeterminationId')
    $noticeRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-notice' 'get'
    $noticeWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-notice/acknowledgment' 'post'
    $noticeRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $noticeWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $noticeRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($noticeRequestReference -split '/')[-1])
    $noticeRequestJson = $noticeRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $noticeResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $noticeWrite.responses '200') 'content') 'application/json').schema '$ref'
    $noticeResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($noticeResponseReference -split '/')[-1])
    $noticeResponseJson = $noticeResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'State notice read and acknowledgment publish applicant-only access, server-bound exact input, bounded failures, and legally nonfinal no-consequence output' (
      (Has-Security $noticeRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $noticeWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $noticeRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $noticeWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $noticeRead 'X-Idempotency-Key') -and
      (Has-Header $noticeWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $noticeRead 'requestBody') -and
      $null-ne(Get-Property $noticeWrite 'requestBody') -and
      $null-ne(Get-Property $noticeRead.responses '401') -and
      $null-ne(Get-Property $noticeRead.responses '404') -and
      $null-ne(Get-Property $noticeRead.responses '409') -and
      $null-ne(Get-Property $noticeRead.responses '410') -and
      $null-ne(Get-Property $noticeWrite.responses '400') -and
      $null-ne(Get-Property $noticeWrite.responses '401') -and
      $null-ne(Get-Property $noticeWrite.responses '404') -and
      $null-ne(Get-Property $noticeWrite.responses '409') -and
      $null-ne(Get-Property $noticeWrite.responses '410') -and
      $noticeRequestJson-match'expectedVersion' -and
      $noticeRequestJson-match'noticeKey' -and
      $noticeRequestJson-match'noticeVersion' -and
      $noticeRequestJson-match'currentLocationStateCode' -and
      $noticeRequestJson-match'currentLocationConfirmed' -and
      $noticeRequestJson-match'modeOfCareAcknowledged' -and
      $noticeRequestJson-match'privacyLimitationsAcknowledged' -and
      $noticeRequestJson-match'emergencyInstructionsAcknowledged' -and
      $noticeRequestJson-match'inPersonOptionAcknowledged' -and
      $noticeRequestJson-match'clinicianReconfirmationRequiredAcknowledged' -and
      $noticeRequestJson-match'syntheticDataConfirmed' -and
      $noticeRequestJson-notmatch'patientId|canonicalPatientId|portal|insurance|coverage|requestId|queueEntry|appointment|encounter|staffId|actorId|accessKey' -and
      $noticeResponseJson-match'legalReviewStatus' -and
      $noticeResponseJson-match'legalConsentEstablished' -and
      $noticeResponseJson-match'clinicianConsentDocumented' -and
      $noticeResponseJson-match'clinicianReconfirmationRequired' -and
      $noticeResponseJson-match'portalAccountCreated' -and
      $noticeResponseJson-match'intakeCompleted' -and
      $noticeResponseJson-match'practiceAccepted' -and
      $noticeResponseJson-match'insuranceCreated' -and
      $noticeResponseJson-match'requestCreated' -and
      $noticeResponseJson-match'queueEnabled' -and
      $noticeResponseJson-match'careEnabled' -and
      $noticeResponseJson-notmatch'patientId|canonicalPatientId|legacyPid|pubpid|staffId|actorId|commandFingerprint|promotionId|safetyTriageEvaluationId')
    $registrationDetailsRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/registration-details' 'get'
    $registrationDetailsWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/registration-details/confirmation' 'post'
    $registrationDetailsRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $registrationDetailsWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $registrationDetailsRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($registrationDetailsRequestReference -split '/')[-1])
    $registrationDetailsRequestJson = $registrationDetailsRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $registrationDetailsResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $registrationDetailsWrite.responses '200') 'content') 'application/json').schema '$ref'
    $registrationDetailsResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($registrationDetailsResponseReference -split '/')[-1])
    $registrationDetailsResponseJson = $registrationDetailsResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Minimum registration-details read and confirmation publish applicant-only access, exact snapshot input, bounded failures, and no-edit no-consequence output' (
      (Has-Security $registrationDetailsRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $registrationDetailsWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $registrationDetailsRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $registrationDetailsWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $registrationDetailsRead 'X-Idempotency-Key') -and
      (Has-Header $registrationDetailsWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $registrationDetailsRead 'requestBody') -and
      $null-ne(Get-Property $registrationDetailsWrite 'requestBody') -and
      $null-ne(Get-Property $registrationDetailsRead.responses '401') -and
      $null-ne(Get-Property $registrationDetailsRead.responses '404') -and
      $null-ne(Get-Property $registrationDetailsRead.responses '409') -and
      $null-ne(Get-Property $registrationDetailsRead.responses '410') -and
      $null-ne(Get-Property $registrationDetailsWrite.responses '400') -and
      $null-ne(Get-Property $registrationDetailsWrite.responses '401') -and
      $null-ne(Get-Property $registrationDetailsWrite.responses '404') -and
      $null-ne(Get-Property $registrationDetailsWrite.responses '409') -and
      $null-ne(Get-Property $registrationDetailsWrite.responses '410') -and
      $registrationDetailsRequestJson-match'expectedVersion' -and
      $registrationDetailsRequestJson-match'detailsFingerprint' -and
      $registrationDetailsRequestJson-match'legalNameAndBirthDateConfirmed' -and
      $registrationDetailsRequestJson-match'contactChannelsConfirmed' -and
      $registrationDetailsRequestJson-match'residenceRegionConfirmed' -and
      $registrationDetailsRequestJson-match'noCorrectionsNeededConfirmed' -and
      $registrationDetailsRequestJson-match'syntheticDataConfirmed' -and
      $registrationDetailsRequestJson-notmatch'legalFirstName|legalLastName|dateOfBirth|email|phone|postalCode|address|patientId|portal|memberId|insuranceId|requestId|queueEntry|staffId|actorId|accessKey' -and
      $registrationDetailsResponseJson-match'legalFirstName' -and
      $registrationDetailsResponseJson-match'legalLastName' -and
      $registrationDetailsResponseJson-match'dateOfBirth' -and
      $registrationDetailsResponseJson-match'maskedEmail' -and
      $registrationDetailsResponseJson-match'maskedPhone' -and
      $registrationDetailsResponseJson-match'residenceStateCode' -and
      $registrationDetailsResponseJson-match'postalCode' -and
      $registrationDetailsResponseJson-match'detailsFingerprint' -and
      $registrationDetailsResponseJson-match'patientRecordChanged' -and
      $registrationDetailsResponseJson-match'correctionCompleted' -and
      $registrationDetailsResponseJson-match'identityAssuranceEstablished' -and
      $registrationDetailsResponseJson-match'insuranceConfirmed' -and
      $registrationDetailsResponseJson-match'requestCreated' -and
      $registrationDetailsResponseJson-match'queueEnabled' -and
      $registrationDetailsResponseJson-match'careEnabled' -and
      $registrationDetailsResponseJson-notmatch'patientId|canonicalPatientId|legacyPid|pubpid|staffId|actorId|commandFingerprint|promotionId|noticeAcknowledgmentId')
    $insuranceHandoffRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/insurance-handoff' 'get'
    $insuranceHandoffWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/insurance-handoff/confirmation' 'post'
    $insuranceHandoffRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $insuranceHandoffWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $insuranceHandoffRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($insuranceHandoffRequestReference -split '/')[-1])
    $insuranceHandoffRequestJson = $insuranceHandoffRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $insuranceHandoffResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $insuranceHandoffWrite.responses '200') 'content') 'application/json').schema '$ref'
    $insuranceHandoffResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($insuranceHandoffResponseReference -split '/')[-1])
    $insuranceHandoffResponseJson = $insuranceHandoffResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Synthetic insurance-handoff read and confirmation publish applicant-only access, snapshot-only input, bounded failures, masked evidence, and explicit no-consequence output' (
      (Has-Security $insuranceHandoffRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $insuranceHandoffWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $insuranceHandoffRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $insuranceHandoffWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $insuranceHandoffRead 'X-Idempotency-Key') -and
      (Has-Header $insuranceHandoffWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $insuranceHandoffRead 'requestBody') -and
      $null-ne(Get-Property $insuranceHandoffWrite 'requestBody') -and
      $null-ne(Get-Property $insuranceHandoffRead.responses '401') -and
      $null-ne(Get-Property $insuranceHandoffRead.responses '404') -and
      $null-ne(Get-Property $insuranceHandoffRead.responses '409') -and
      $null-ne(Get-Property $insuranceHandoffRead.responses '410') -and
      $null-ne(Get-Property $insuranceHandoffWrite.responses '400') -and
      $null-ne(Get-Property $insuranceHandoffWrite.responses '401') -and
      $null-ne(Get-Property $insuranceHandoffWrite.responses '404') -and
      $null-ne(Get-Property $insuranceHandoffWrite.responses '409') -and
      $null-ne(Get-Property $insuranceHandoffWrite.responses '410') -and
      $insuranceHandoffRequestJson-match'expectedVersion' -and
      $insuranceHandoffRequestJson-match'insuranceSnapshotFingerprint' -and
      $insuranceHandoffRequestJson-match'payerAndProductConfirmed' -and
      $insuranceHandoffRequestJson-match'maskedMemberDetailsConfirmed' -and
      $insuranceHandoffRequestJson-match'subscriberRelationshipConfirmed' -and
      $insuranceHandoffRequestJson-match'evidenceLimitationsAcknowledged' -and
      $insuranceHandoffRequestJson-match'syntheticDataConfirmed' -and
      $insuranceHandoffRequestJson-notmatch'memberId"|groupNumber"|subscriberFirst|subscriberLast|subscriberDate|patientId|canonicalPatientId|portal|requestId|queueEntry|appointment|encounter|staffId|actorId|accessKey' -and
      $insuranceHandoffResponseJson-match'payerDisplayName' -and
      $insuranceHandoffResponseJson-match'productDisplayName' -and
      $insuranceHandoffResponseJson-match'memberIdMask' -and
      $insuranceHandoffResponseJson-match'groupNumberMask' -and
      $insuranceHandoffResponseJson-match'subscriberRelationship' -and
      $insuranceHandoffResponseJson-match'coveragePriority' -and
      $insuranceHandoffResponseJson-match'eligibilityBusinessOutcome' -and
      $insuranceHandoffResponseJson-match'eligibilityEvidenceCurrent' -and
      $insuranceHandoffResponseJson-match'practiceNetworkBusinessOutcome' -and
      $insuranceHandoffResponseJson-match'practiceNetworkEvidenceCurrent' -and
      $insuranceHandoffResponseJson-match'renderingPhysicianNetworkChecked' -and
      $insuranceHandoffResponseJson-match'insuranceDetailsConfirmed' -and
      $insuranceHandoffResponseJson-match'coverageVerified' -and
      $insuranceHandoffResponseJson-match'exactNetworkConfirmed' -and
      $insuranceHandoffResponseJson-match'canonicalCoverageCreated' -and
      $insuranceHandoffResponseJson-match'patientRecordChanged' -and
      $insuranceHandoffResponseJson-match'requestCreated' -and
      $insuranceHandoffResponseJson-match'queueEnabled' -and
      $insuranceHandoffResponseJson-match'careEnabled' -and
      $insuranceHandoffResponseJson-notmatch'patientId|canonicalPatientId|legacyPid|pubpid|staffId|actorId|commandFingerprint|promotionId|registrationConfirmationId|memberDetailsId|eligibilityResultId|networkDeterminationId|memberId"|groupNumber"|subscriberFirst|subscriberLast|subscriberDate')
    $communicationAccessRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/communication-access-readiness' 'get'
    $communicationAccessWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/communication-access-readiness' 'post'
    $communicationAccessRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $communicationAccessWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $communicationAccessRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($communicationAccessRequestReference -split '/')[-1])
    $communicationAccessRequestJson = $communicationAccessRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $communicationAccessResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $communicationAccessWrite.responses '200') 'content') 'application/json').schema '$ref'
    $communicationAccessResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($communicationAccessResponseReference -split '/')[-1])
    $communicationAccessResponseJson = $communicationAccessResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Synthetic communication/access-readiness read and write publish applicant-only access, context-bound preference input, bounded failures, masked context, and explicit no-arrangement output' (
      (Has-Security $communicationAccessRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $communicationAccessWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $communicationAccessRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $communicationAccessWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $communicationAccessRead 'X-Idempotency-Key') -and
      (Has-Header $communicationAccessWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $communicationAccessRead 'requestBody') -and
      $null-ne(Get-Property $communicationAccessWrite 'requestBody') -and
      $null-ne(Get-Property $communicationAccessRead.responses '401') -and
      $null-ne(Get-Property $communicationAccessRead.responses '404') -and
      $null-ne(Get-Property $communicationAccessRead.responses '409') -and
      $null-ne(Get-Property $communicationAccessRead.responses '410') -and
      $null-ne(Get-Property $communicationAccessWrite.responses '400') -and
      $null-ne(Get-Property $communicationAccessWrite.responses '401') -and
      $null-ne(Get-Property $communicationAccessWrite.responses '404') -and
      $null-ne(Get-Property $communicationAccessWrite.responses '409') -and
      $null-ne(Get-Property $communicationAccessWrite.responses '410') -and
      $communicationAccessRequestJson-match'expectedVersion' -and
      $communicationAccessRequestJson-match'contextSnapshotFingerprint' -and
      $communicationAccessRequestJson-match'preferredSpokenLanguage' -and
      $communicationAccessRequestJson-match'interpreterRequested' -and
      $communicationAccessRequestJson-match'accessibilitySupportRequested' -and
      $communicationAccessRequestJson-match'currentLocationConfirmed' -and
      $communicationAccessRequestJson-match'callbackNumberConfirmed' -and
      $communicationAccessRequestJson-match'safeAndPrivateToCommunicateConfirmed' -and
      $communicationAccessRequestJson-match'disconnectionAndEmergencyPlanAcknowledged' -and
      $communicationAccessRequestJson-match'syntheticDataConfirmed' -and
      $communicationAccessRequestJson-notmatch'phone|email|address|patientId|canonicalPatientId|memberId|groupNumber|insurance|clinical|complaint|diagnosis|requestId|queueEntry|appointment|encounter|staffId|actorId|accessKey' -and
      $communicationAccessResponseJson-match'currentLocationStateCode' -and
      $communicationAccessResponseJson-match'maskedCallbackPhone' -and
      $communicationAccessResponseJson-match'supportedSpokenLanguages' -and
      $communicationAccessResponseJson-match'preferredSpokenLanguage' -and
      $communicationAccessResponseJson-match'interpreterRequested' -and
      $communicationAccessResponseJson-match'accessibilitySupportRequested' -and
      $communicationAccessResponseJson-match'readinessRecorded' -and
      $communicationAccessResponseJson-match'interpreterAssigned' -and
      $communicationAccessResponseJson-match'accessibilityAccommodationArranged' -and
      $communicationAccessResponseJson-match'communicationArrangementCompleted' -and
      $communicationAccessResponseJson-match'supportRequestCreated' -and
      $communicationAccessResponseJson-match'technologyReadinessCompleted' -and
      $communicationAccessResponseJson-match'patientRecordChanged' -and
      $communicationAccessResponseJson-match'requestCreated' -and
      $communicationAccessResponseJson-match'queueEnabled' -and
      $communicationAccessResponseJson-match'careEnabled' -and
      $communicationAccessResponseJson-notmatch'patientId|canonicalPatientId|legacyPid|pubpid|staffId|actorId|commandFingerprint|promotionId|safetyEvaluationId|insuranceHandoffConfirmationId|registrationConfirmationId|email|memberId|groupNumber|subscriber|complaint|diagnosis')
    $devicePreparationRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/device-preparation' 'get'
    $devicePreparationWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/device-preparation' 'post'
    $devicePreparationRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $devicePreparationWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $devicePreparationRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($devicePreparationRequestReference -split '/')[-1])
    $devicePreparationRequestJson = $devicePreparationRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $devicePreparationRequestProperties = @((Get-Property $devicePreparationRequestSchema 'properties').PSObject.Properties.Name)
    $devicePreparationResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $devicePreparationWrite.responses '200') 'content') 'application/json').schema '$ref'
    $devicePreparationResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($devicePreparationResponseReference -split '/')[-1])
    $devicePreparationResponseJson = $devicePreparationResponseSchema | ConvertTo-Json -Depth 30 -Compress
    $devicePreparationResponseProperties = @((Get-Property $devicePreparationResponseSchema 'properties').PSObject.Properties.Name)
    $devicePreparationForbiddenProperties = @(
      'deviceId','deviceLabel','groupId','userAgent','ipAddress','iceCandidate','sdp','codec','resolution','bandwidth',
      'mediaPayload','recording','recordingPayload','transcript','phone','email','address','patientId','canonicalPatientId',
      'legacyPid','pubpid','memberId','groupNumber','subscriber','clinical','complaint','diagnosis','requestId','queueEntry',
      'appointment','encounter','staffId','actorId','accessKey','commandFingerprint','promotionId','safetyEvaluationId',
      'insuranceHandoffConfirmationId','registrationConfirmationId','communicationAccessReadinessId')
    Add-Check 'Synthetic device-preparation read and write publish applicant-only access, coarse passing input, required acknowledgments, bounded failures, and explicit no-readiness/no-media output' (
      (Has-Security $devicePreparationRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $devicePreparationWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $devicePreparationRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $devicePreparationWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $devicePreparationRead 'X-Idempotency-Key') -and
      (Has-Header $devicePreparationWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $devicePreparationRead 'requestBody') -and
      $null-ne(Get-Property $devicePreparationWrite 'requestBody') -and
      $null-ne(Get-Property $devicePreparationRead.responses '401') -and
      $null-ne(Get-Property $devicePreparationRead.responses '404') -and
      $null-ne(Get-Property $devicePreparationRead.responses '409') -and
      $null-ne(Get-Property $devicePreparationRead.responses '410') -and
      $null-ne(Get-Property $devicePreparationWrite.responses '400') -and
      $null-ne(Get-Property $devicePreparationWrite.responses '401') -and
      $null-ne(Get-Property $devicePreparationWrite.responses '404') -and
      $null-ne(Get-Property $devicePreparationWrite.responses '409') -and
      $null-ne(Get-Property $devicePreparationWrite.responses '410') -and
      $devicePreparationRequestJson-match'expectedVersion' -and
      $devicePreparationRequestJson-match'preparationSnapshotFingerprint' -and
      $devicePreparationRequestJson-match'browserSupported' -and
      $devicePreparationRequestJson-match'cameraAvailable' -and
      $devicePreparationRequestJson-match'microphoneAvailable' -and
      $devicePreparationRequestJson-match'speakerAvailable' -and
      $devicePreparationRequestJson-match'networkQuality' -and
      $devicePreparationRequestJson-match'clientReportedResultAcknowledged' -and
      $devicePreparationRequestJson-match'noReadinessGuaranteeAcknowledged' -and
      $devicePreparationRequestJson-match'recheckBeforeConsultationAcknowledged' -and
      @($devicePreparationRequestProperties | Where-Object { $_ -in $devicePreparationForbiddenProperties }).Count -eq 0 -and
      $devicePreparationResponseJson-match'supportedNetworkQualities' -and
      $devicePreparationResponseJson-match'preparationRecorded' -and
      $devicePreparationResponseJson-match'browserSupported' -and
      $devicePreparationResponseJson-match'cameraAvailable' -and
      $devicePreparationResponseJson-match'microphoneAvailable' -and
      $devicePreparationResponseJson-match'speakerAvailable' -and
      $devicePreparationResponseJson-match'networkQuality' -and
      $devicePreparationResponseJson-match'technologyReady' -and
      $devicePreparationResponseJson-match'waitingRoomCreated' -and
      $devicePreparationResponseJson-match'mediaSessionCreated' -and
      $devicePreparationResponseJson-match'communicationStarted' -and
      $devicePreparationResponseJson-match'supportArrangementCompleted' -and
      $devicePreparationResponseJson-match'patientRecordChanged' -and
      $devicePreparationResponseJson-match'requestCreated' -and
      $devicePreparationResponseJson-match'queueEntered' -and
      $devicePreparationResponseJson-match'careAuthorized' -and
      @($devicePreparationResponseProperties | Where-Object { $_ -in $devicePreparationForbiddenProperties }).Count -eq 0)
    $clinicalInventoryRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/clinical-information-inventory' 'get'
    $clinicalInventoryWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/clinical-information-inventory' 'post'
    $clinicalInventoryRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $clinicalInventoryWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $clinicalInventoryRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($clinicalInventoryRequestReference -split '/')[-1])
    $clinicalInventoryRequestJson = $clinicalInventoryRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $clinicalInventoryRequestProperties = @((Get-Property $clinicalInventoryRequestSchema 'properties').PSObject.Properties.Name)
    $clinicalInventoryResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $clinicalInventoryWrite.responses '200') 'content') 'application/json').schema '$ref'
    $clinicalInventoryResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($clinicalInventoryResponseReference -split '/')[-1])
    $clinicalInventoryResponseJson = $clinicalInventoryResponseSchema | ConvertTo-Json -Depth 30 -Compress
    $clinicalInventoryResponseProperties = @((Get-Property $clinicalInventoryResponseSchema 'properties').PSObject.Properties.Name)
    $clinicalInventoryForbiddenProperties = @(
      'medicationName','medicationCode','substance','reaction','dose','doseUnit','diagnosis','diagnosisCode',
      'symptom','procedure','narrative','clinicalDate','clinicalIdentifier','freeText','notes','patientId',
      'canonicalPatientId','legacyPid','pubpid','requestId','queueEntry','appointment','encounter','prescription',
      'staffId','actorId','accessKey','commandFingerprint','promotionId','devicePreparationId')
    Add-Check 'Synthetic clinical-information inventory read and write publish applicant-only access, three controlled categories, required limitations, server route, bounded failures, and explicit no-reconciliation/no-care output' (
      (Has-Security $clinicalInventoryRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $clinicalInventoryWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $clinicalInventoryRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $clinicalInventoryWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $clinicalInventoryRead 'X-Idempotency-Key') -and
      (Has-Header $clinicalInventoryWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $clinicalInventoryRead 'requestBody') -and
      $null-ne(Get-Property $clinicalInventoryWrite 'requestBody') -and
      $null-ne(Get-Property $clinicalInventoryRead.responses '401') -and
      $null-ne(Get-Property $clinicalInventoryRead.responses '404') -and
      $null-ne(Get-Property $clinicalInventoryRead.responses '409') -and
      $null-ne(Get-Property $clinicalInventoryRead.responses '410') -and
      $null-ne(Get-Property $clinicalInventoryWrite.responses '400') -and
      $null-ne(Get-Property $clinicalInventoryWrite.responses '401') -and
      $null-ne(Get-Property $clinicalInventoryWrite.responses '404') -and
      $null-ne(Get-Property $clinicalInventoryWrite.responses '409') -and
      $null-ne(Get-Property $clinicalInventoryWrite.responses '410') -and
      $clinicalInventoryRequestJson-match'expectedVersion' -and
      $clinicalInventoryRequestJson-match'inventorySnapshotFingerprint' -and
      $clinicalInventoryRequestJson-match'medicationsStatus' -and
      $clinicalInventoryRequestJson-match'allergiesOrIntolerancesStatus' -and
      $clinicalInventoryRequestJson-match'otherHealthHistoryStatus' -and
      $clinicalInventoryRequestJson-match'patientReportedMayBeIncompleteAcknowledged' -and
      $clinicalInventoryRequestJson-match'noClinicalDetailsCapturedAcknowledged' -and
      $clinicalInventoryRequestJson-match'clinicianReconciliationRequiredAcknowledged' -and
      @($clinicalInventoryRequestProperties | Where-Object { $_ -in $clinicalInventoryForbiddenProperties }).Count -eq 0 -and
      $clinicalInventoryResponseJson-match'supportedCategoryStatuses' -and
      $clinicalInventoryResponseJson-match'inventoryRecorded' -and
      $clinicalInventoryResponseJson-match'medicationsStatus' -and
      $clinicalInventoryResponseJson-match'allergiesOrIntolerancesStatus' -and
      $clinicalInventoryResponseJson-match'otherHealthHistoryStatus' -and
      $clinicalInventoryResponseJson-match'reviewRoute' -and
      $clinicalInventoryResponseJson-match'medicationListReconciled' -and
      $clinicalInventoryResponseJson-match'allergyListReconciled' -and
      $clinicalInventoryResponseJson-match'healthHistoryReconciled' -and
      $clinicalInventoryResponseJson-match'clinicalIntakeCompleted' -and
      $clinicalInventoryResponseJson-match'clinicalEligibilityEstablished' -and
      $clinicalInventoryResponseJson-match'clinicianReviewCreated' -and
      $clinicalInventoryResponseJson-match'patientRecordChanged' -and
      $clinicalInventoryResponseJson-match'requestCreated' -and
      $clinicalInventoryResponseJson-match'queueEntered' -and
      $clinicalInventoryResponseJson-match'careAuthorized' -and
      $clinicalInventoryResponseJson-match'prescribingEnabled' -and
      @($clinicalInventoryResponseProperties | Where-Object { $_ -in $clinicalInventoryForbiddenProperties }).Count -eq 0)
    $medicationInformationRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/medication-information' 'get'
    $medicationInformationWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/medication-information' 'post'
    $medicationInformationRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $medicationInformationWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $medicationInformationRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($medicationInformationRequestReference -split '/')[-1])
    $medicationInformationItemRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'RecordTelehealthApplicantMedicationItemRequest'
    $medicationInformationRequestJson = ($medicationInformationRequestSchema | ConvertTo-Json -Depth 30 -Compress) + ($medicationInformationItemRequestSchema | ConvertTo-Json -Depth 30 -Compress)
    $medicationInformationRequestProperties = @(
      (Get-Property $medicationInformationRequestSchema 'properties').PSObject.Properties.Name
      (Get-Property $medicationInformationItemRequestSchema 'properties').PSObject.Properties.Name)
    $medicationInformationResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $medicationInformationWrite.responses '200') 'content') 'application/json').schema '$ref'
    $medicationInformationResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($medicationInformationResponseReference -split '/')[-1])
    $medicationInformationCatalogItemSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'SyntheticTelehealthApplicantMedicationCatalogItem'
    $medicationInformationItemResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'TelehealthApplicantMedicationItemResponse'
    $medicationInformationResponseJson = ($medicationInformationResponseSchema | ConvertTo-Json -Depth 30 -Compress) + ($medicationInformationCatalogItemSchema | ConvertTo-Json -Depth 30 -Compress) + ($medicationInformationItemResponseSchema | ConvertTo-Json -Depth 30 -Compress)
    $medicationInformationResponseProperties = @(
      (Get-Property $medicationInformationResponseSchema 'properties').PSObject.Properties.Name
      (Get-Property $medicationInformationCatalogItemSchema 'properties').PSObject.Properties.Name
      (Get-Property $medicationInformationItemResponseSchema 'properties').PSObject.Properties.Name)
    $medicationInformationForbiddenProperties = @('dose','directions','routeOfAdministration','frequency','timing','indication','prescriber','pharmacy','clinicalDate','note','attachment','freeText','rxNormCode','ndcCode','snomedCode','canonicalPatientId','legacyPid','pubpid','requestId','queueEntry','appointment','encounter','prescriptionId','staffId','actorId','accessKey','commandFingerprint','promotionId','clinicalInventoryId')
    Add-Check 'Synthetic medication-information read and write publish applicant-only access, fixed local unmapped catalog, three coarse use states, required limitations, bounded failures, and explicit no-canonical/no-care output' (
      (Has-Security $medicationInformationRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $medicationInformationWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $medicationInformationRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $medicationInformationWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $medicationInformationRead 'X-Idempotency-Key') -and
      (Has-Header $medicationInformationWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $medicationInformationRead 'requestBody') -and
      $null-ne(Get-Property $medicationInformationWrite 'requestBody') -and
      $null-ne(Get-Property $medicationInformationRead.responses '401') -and
      $null-ne(Get-Property $medicationInformationRead.responses '404') -and
      $null-ne(Get-Property $medicationInformationRead.responses '409') -and
      $null-ne(Get-Property $medicationInformationRead.responses '410') -and
      $null-ne(Get-Property $medicationInformationWrite.responses '400') -and
      $null-ne(Get-Property $medicationInformationWrite.responses '401') -and
      $null-ne(Get-Property $medicationInformationWrite.responses '404') -and
      $null-ne(Get-Property $medicationInformationWrite.responses '409') -and
      $null-ne(Get-Property $medicationInformationWrite.responses '410') -and
      $medicationInformationRequestJson-match'expectedVersion' -and
      $medicationInformationRequestJson-match'medicationInformationSnapshotFingerprint' -and
      $medicationInformationRequestJson-match'medicationItems' -and
      $medicationInformationRequestJson-match'catalogKey' -and
      $medicationInformationRequestJson-match'reportedUseStatus' -and
      $medicationInformationRequestJson-match'additionalOrUnlistedItemsReported' -and
      $medicationInformationRequestJson-match'patientReportedMayBeIncompleteAcknowledged' -and
      $medicationInformationRequestJson-match'syntheticCatalogIncompleteAcknowledged' -and
      $medicationInformationRequestJson-match'noDoseOrDirectionsCapturedAcknowledged' -and
      $medicationInformationRequestJson-match'clinicianReconciliationRequiredAcknowledged' -and
      @($medicationInformationRequestProperties | Where-Object { $_ -in $medicationInformationForbiddenProperties }).Count -eq 0 -and
      $medicationInformationResponseJson-match'catalogItems' -and
      $medicationInformationResponseJson-match'codingSystem' -and
      $medicationInformationResponseJson-match'rxNormMapped' -and
      $medicationInformationResponseJson-match'supportedReportedUseStatuses' -and
      $medicationInformationResponseJson-match'medicationStatementCreated' -and
      $medicationInformationResponseJson-match'medicationRequestCreated' -and
      $medicationInformationResponseJson-match'medicationListReconciled' -and
      $medicationInformationResponseJson-match'interactionCheckPerformed' -and
      $medicationInformationResponseJson-match'clinicianReviewCreated' -and
      $medicationInformationResponseJson-match'prescribingEnabled' -and
      @($medicationInformationResponseProperties | Where-Object { $_ -in $medicationInformationForbiddenProperties }).Count -eq 0)
    $allergyInformationRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/allergy-information' 'get'
    $allergyInformationWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/allergy-information' 'post'
    $allergyInformationRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $allergyInformationWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $allergyInformationRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($allergyInformationRequestReference -split '/')[-1])
    $allergyInformationItemRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'RecordTelehealthApplicantAllergyItemRequest'
    $allergyInformationRequestJson = ($allergyInformationRequestSchema | ConvertTo-Json -Depth 30 -Compress) + ($allergyInformationItemRequestSchema | ConvertTo-Json -Depth 30 -Compress)
    $allergyInformationRequestProperties = @(
      (Get-Property $allergyInformationRequestSchema 'properties').PSObject.Properties.Name
      (Get-Property $allergyInformationItemRequestSchema 'properties').PSObject.Properties.Name)
    $allergyInformationResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $allergyInformationWrite.responses '200') 'content') 'application/json').schema '$ref'
    $allergyInformationResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($allergyInformationResponseReference -split '/')[-1])
    $allergyInformationCatalogItemSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'SyntheticTelehealthApplicantAllergyCatalogItem'
    $allergyInformationItemResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'TelehealthApplicantAllergyItemResponse'
    $allergyInformationResponseJson = ($allergyInformationResponseSchema | ConvertTo-Json -Depth 30 -Compress) + ($allergyInformationCatalogItemSchema | ConvertTo-Json -Depth 30 -Compress) + ($allergyInformationItemResponseSchema | ConvertTo-Json -Depth 30 -Compress)
    $allergyInformationResponseProperties = @(
      (Get-Property $allergyInformationResponseSchema 'properties').PSObject.Properties.Name
      (Get-Property $allergyInformationCatalogItemSchema 'properties').PSObject.Properties.Name
      (Get-Property $allergyInformationItemResponseSchema 'properties').PSObject.Properties.Name)
    $allergyInformationForbiddenProperties = @('reaction','manifestation','type','clinicalStatus','verificationStatus','severity','criticality','onset','occurrence','clinicalDate','note','attachment','freeText','snomedCode','rxNormCode','canonicalPatientId','legacyPid','pubpid','requestId','queueEntry','appointment','encounter','prescriptionId','staffId','actorId','accessKey','commandFingerprint','promotionId','clinicalInventoryId','medicationInformationId')
    Add-Check 'Synthetic allergy-information read and write publish applicant-only access, fixed six-item local unmapped catalog, required limitations, bounded failures, and explicit no-canonical/no-reaction/no-care output' (
      (Has-Security $allergyInformationRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $allergyInformationWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $allergyInformationRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $allergyInformationWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $allergyInformationRead 'X-Idempotency-Key') -and
      (Has-Header $allergyInformationWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $allergyInformationRead 'requestBody') -and
      $null-ne(Get-Property $allergyInformationWrite 'requestBody') -and
      $null-ne(Get-Property $allergyInformationRead.responses '401') -and
      $null-ne(Get-Property $allergyInformationRead.responses '404') -and
      $null-ne(Get-Property $allergyInformationRead.responses '409') -and
      $null-ne(Get-Property $allergyInformationRead.responses '410') -and
      $null-ne(Get-Property $allergyInformationWrite.responses '400') -and
      $null-ne(Get-Property $allergyInformationWrite.responses '401') -and
      $null-ne(Get-Property $allergyInformationWrite.responses '404') -and
      $null-ne(Get-Property $allergyInformationWrite.responses '409') -and
      $null-ne(Get-Property $allergyInformationWrite.responses '410') -and
      $allergyInformationRequestJson-match'expectedVersion' -and
      $allergyInformationRequestJson-match'allergyInformationSnapshotFingerprint' -and
      $allergyInformationRequestJson-match'allergyItems' -and
      $allergyInformationRequestJson-match'catalogKey' -and
      $allergyInformationRequestJson-match'additionalOrUnlistedItemsReported' -and
      $allergyInformationRequestJson-match'patientReportedMayBeIncompleteAcknowledged' -and
      $allergyInformationRequestJson-match'syntheticCatalogIncompleteAcknowledged' -and
      $allergyInformationRequestJson-match'noReactionOrCriticalityCapturedAcknowledged' -and
      $allergyInformationRequestJson-match'clinicianVerificationRequiredAcknowledged' -and
      @($allergyInformationRequestProperties | Where-Object { $_ -in $allergyInformationForbiddenProperties }).Count -eq 0 -and
      $allergyInformationResponseJson-match'catalogItems' -and
      $allergyInformationResponseJson-match'codingSystem' -and
      $allergyInformationResponseJson-match'snomedCtMapped' -and
      $allergyInformationResponseJson-match'rxNormMapped' -and
      $allergyInformationResponseJson-match'allergyIntoleranceCreated' -and
      $allergyInformationResponseJson-match'allergyListReconciled' -and
      $allergyInformationResponseJson-match'reactionAssessed' -and
      $allergyInformationResponseJson-match'criticalityAssessed' -and
      $allergyInformationResponseJson-match'contraindicationCheckPerformed' -and
      $allergyInformationResponseJson-match'clinicianReviewCreated' -and
      $allergyInformationResponseJson-match'prescribingEnabled' -and
      @($allergyInformationResponseProperties | Where-Object { $_ -in $allergyInformationForbiddenProperties }).Count -eq 0)
    $healthHistoryInformationRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/health-history-information' 'get'
    $healthHistoryInformationWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/health-history-information' 'post'
    $healthHistoryInformationRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $healthHistoryInformationWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $healthHistoryInformationRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($healthHistoryInformationRequestReference -split '/')[-1])
    $healthHistoryInformationTopicRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'RecordTelehealthApplicantHealthHistoryTopicRequest'
    $healthHistoryInformationRequestJson = ($healthHistoryInformationRequestSchema | ConvertTo-Json -Depth 30 -Compress) + ($healthHistoryInformationTopicRequestSchema | ConvertTo-Json -Depth 30 -Compress)
    $healthHistoryInformationRequestProperties = @(
      (Get-Property $healthHistoryInformationRequestSchema 'properties').PSObject.Properties.Name
      (Get-Property $healthHistoryInformationTopicRequestSchema 'properties').PSObject.Properties.Name)
    $healthHistoryInformationResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $healthHistoryInformationWrite.responses '200') 'content') 'application/json').schema '$ref'
    $healthHistoryInformationResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($healthHistoryInformationResponseReference -split '/')[-1])
    $healthHistoryInformationCatalogTopicSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'SyntheticTelehealthApplicantHealthHistoryTopic'
    $healthHistoryInformationTopicResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'TelehealthApplicantHealthHistoryTopicResponse'
    $healthHistoryInformationResponseJson = ($healthHistoryInformationResponseSchema | ConvertTo-Json -Depth 30 -Compress) + ($healthHistoryInformationCatalogTopicSchema | ConvertTo-Json -Depth 30 -Compress) + ($healthHistoryInformationTopicResponseSchema | ConvertTo-Json -Depth 30 -Compress)
    $healthHistoryInformationResponseProperties = @(
      (Get-Property $healthHistoryInformationResponseSchema 'properties').PSObject.Properties.Name
      (Get-Property $healthHistoryInformationCatalogTopicSchema 'properties').PSObject.Properties.Name
      (Get-Property $healthHistoryInformationTopicResponseSchema 'properties').PSObject.Properties.Name)
    $healthHistoryInformationForbiddenProperties = @('diagnosis','symptom','procedureDate','clinicalStatus','verificationStatus','severity','onset','occurrence','clinicalDate','note','attachment','freeText','snomedCode','icd10Code','loincCode','canonicalPatientId','legacyPid','pubpid','requestId','queueEntry','appointment','encounter','prescriptionId','staffId','actorId','accessKey','commandFingerprint','promotionId','clinicalInventoryId','medicationInformationId','allergyInformationId')
    Add-Check 'Synthetic health-history-information read and write publish applicant-only access, fixed six-topic local unmapped catalog, required limitations, bounded failures, and explicit no-finding/no-risk/no-care output' (
      (Has-Security $healthHistoryInformationRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $healthHistoryInformationWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $healthHistoryInformationRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $healthHistoryInformationWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $healthHistoryInformationRead 'X-Idempotency-Key') -and
      (Has-Header $healthHistoryInformationWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $healthHistoryInformationRead 'requestBody') -and
      $null-ne(Get-Property $healthHistoryInformationWrite 'requestBody') -and
      $null-ne(Get-Property $healthHistoryInformationRead.responses '401') -and
      $null-ne(Get-Property $healthHistoryInformationRead.responses '404') -and
      $null-ne(Get-Property $healthHistoryInformationRead.responses '409') -and
      $null-ne(Get-Property $healthHistoryInformationRead.responses '410') -and
      $null-ne(Get-Property $healthHistoryInformationWrite.responses '400') -and
      $null-ne(Get-Property $healthHistoryInformationWrite.responses '401') -and
      $null-ne(Get-Property $healthHistoryInformationWrite.responses '404') -and
      $null-ne(Get-Property $healthHistoryInformationWrite.responses '409') -and
      $null-ne(Get-Property $healthHistoryInformationWrite.responses '410') -and
      $healthHistoryInformationRequestJson-match'expectedVersion' -and
      $healthHistoryInformationRequestJson-match'healthHistoryInformationSnapshotFingerprint' -and
      $healthHistoryInformationRequestJson-match'healthHistoryTopics' -and
      $healthHistoryInformationRequestJson-match'catalogKey' -and
      $healthHistoryInformationRequestJson-match'additionalOrUnlistedTopicsReported' -and
      $healthHistoryInformationRequestJson-match'patientReportedMayBeIncompleteAcknowledged' -and
      $healthHistoryInformationRequestJson-match'topicSelectionIsNotDiagnosisAcknowledged' -and
      $healthHistoryInformationRequestJson-match'noStatusOrTimingCapturedAcknowledged' -and
      $healthHistoryInformationRequestJson-match'clinicianVerificationRequiredAcknowledged' -and
      @($healthHistoryInformationRequestProperties | Where-Object { $_ -in $healthHistoryInformationForbiddenProperties }).Count -eq 0 -and
      $healthHistoryInformationResponseJson-match'catalogTopics' -and
      $healthHistoryInformationResponseJson-match'codingSystem' -and
      $healthHistoryInformationResponseJson-match'snomedCtMapped' -and
      $healthHistoryInformationResponseJson-match'icd10CmMapped' -and
      $healthHistoryInformationResponseJson-match'loincMapped' -and
      $healthHistoryInformationResponseJson-match'conditionCreated' -and
      $healthHistoryInformationResponseJson-match'procedureCreated' -and
      $healthHistoryInformationResponseJson-match'observationCreated' -and
      $healthHistoryInformationResponseJson-match'familyMemberHistoryCreated' -and
      $healthHistoryInformationResponseJson-match'questionnaireResponseCreated' -and
      $healthHistoryInformationResponseJson-match'healthHistoryReconciled' -and
      $healthHistoryInformationResponseJson-match'riskModifierEvaluated' -and
      $healthHistoryInformationResponseJson-match'clinicalTriageChanged' -and
      $healthHistoryInformationResponseJson-match'clinicianReviewCreated' -and
      $healthHistoryInformationResponseJson-match'prescribingEnabled' -and
      @($healthHistoryInformationResponseProperties | Where-Object { $_ -in $healthHistoryInformationForbiddenProperties }).Count -eq 0)
    $clinicalInformationSummaryRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/clinical-information-summary' 'get'
    $clinicalInformationSummaryWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/clinical-information-summary' 'post'
    $clinicalInformationSummaryRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $clinicalInformationSummaryWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $clinicalInformationSummaryRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($clinicalInformationSummaryRequestReference -split '/')[-1])
    $clinicalInformationSummaryRequestJson = $clinicalInformationSummaryRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $clinicalInformationSummaryRequestProperties = @((Get-Property $clinicalInformationSummaryRequestSchema 'properties').PSObject.Properties.Name)
    $clinicalInformationSummaryResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $clinicalInformationSummaryWrite.responses '200') 'content') 'application/json').schema '$ref'
    $clinicalInformationSummaryResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($clinicalInformationSummaryResponseReference -split '/')[-1])
    $clinicalInformationSummaryCategorySchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'TelehealthApplicantClinicalInformationSummaryCategoryResponse'
    $clinicalInformationSummaryResponseJson = ($clinicalInformationSummaryResponseSchema | ConvertTo-Json -Depth 30 -Compress) + ($clinicalInformationSummaryCategorySchema | ConvertTo-Json -Depth 30 -Compress)
    $clinicalInformationSummaryResponseProperties = @(
      (Get-Property $clinicalInformationSummaryResponseSchema 'properties').PSObject.Properties.Name
      (Get-Property $clinicalInformationSummaryCategorySchema 'properties').PSObject.Properties.Name)
    $clinicalInformationSummaryForbiddenProperties = @('displayName','legalName','dateOfBirth','email','phone','address','memberId','payer','catalogKey','diagnosis','symptom','dose','reaction','note','attachment','freeText','canonicalPatientId','legacyPid','pubpid','promotionId','clinicalInventoryId','medicationInformationId','allergyInformationId','healthHistoryInformationId','requestId','queueEntry','appointment','encounter','prescriptionId','staffId','actorId','accessKey','commandFingerprint')
    Add-Check 'Synthetic clinical-information summary read and write publish applicant-only access, no-edit bounded input, minimized three-category output, required limitations, bounded failures, and explicit no-reconciliation/no-care results' (
      (Has-Security $clinicalInformationSummaryRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $clinicalInformationSummaryWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $clinicalInformationSummaryRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $clinicalInformationSummaryWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $clinicalInformationSummaryRead 'X-Idempotency-Key') -and
      (Has-Header $clinicalInformationSummaryWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $clinicalInformationSummaryRead 'requestBody') -and
      $null-ne(Get-Property $clinicalInformationSummaryWrite 'requestBody') -and
      $null-ne(Get-Property $clinicalInformationSummaryRead.responses '401') -and
      $null-ne(Get-Property $clinicalInformationSummaryRead.responses '404') -and
      $null-ne(Get-Property $clinicalInformationSummaryRead.responses '409') -and
      $null-ne(Get-Property $clinicalInformationSummaryRead.responses '410') -and
      $null-ne(Get-Property $clinicalInformationSummaryWrite.responses '400') -and
      $null-ne(Get-Property $clinicalInformationSummaryWrite.responses '401') -and
      $null-ne(Get-Property $clinicalInformationSummaryWrite.responses '404') -and
      $null-ne(Get-Property $clinicalInformationSummaryWrite.responses '409') -and
      $null-ne(Get-Property $clinicalInformationSummaryWrite.responses '410') -and
      $clinicalInformationSummaryRequestJson-match'expectedVersion' -and
      $clinicalInformationSummaryRequestJson-match'clinicalInformationSummarySnapshotFingerprint' -and
      $clinicalInformationSummaryRequestJson-match'patientReportedMayBeIncompleteAcknowledged' -and
      $clinicalInformationSummaryRequestJson-match'notClinicallyVerifiedOrReconciledAcknowledged' -and
      $clinicalInformationSummaryRequestJson-match'noIntakeCompletionOrEligibilityAcknowledged' -and
      $clinicalInformationSummaryRequestJson-match'correctionRequiresSeparateWorkflowAcknowledged' -and
      $clinicalInformationSummaryRequestProperties.Count -eq 6 -and
      @($clinicalInformationSummaryRequestProperties | Where-Object { $_ -in $clinicalInformationSummaryForbiddenProperties }).Count -eq 0 -and
      $clinicalInformationSummaryResponseJson-match'categories' -and
      $clinicalInformationSummaryResponseJson-match'categoryKey' -and
      $clinicalInformationSummaryResponseJson-match'inventoryStatus' -and
      $clinicalInformationSummaryResponseJson-match'selectedItemCount' -and
      $clinicalInformationSummaryResponseJson-match'additionalOrUnlistedItemsReported' -and
      $clinicalInformationSummaryResponseJson-match'reviewRoute' -and
      $clinicalInformationSummaryResponseJson-match'summaryRoute' -and
      $clinicalInformationSummaryResponseJson-match'questionnaireResponseCreated' -and
      $clinicalInformationSummaryResponseJson-match'medicationListReconciled' -and
      $clinicalInformationSummaryResponseJson-match'allergyListReconciled' -and
      $clinicalInformationSummaryResponseJson-match'healthHistoryReconciled' -and
      $clinicalInformationSummaryResponseJson-match'confirmedNegativeEstablished' -and
      $clinicalInformationSummaryResponseJson-match'clinicalIntakeCompleted' -and
      $clinicalInformationSummaryResponseJson-match'clinicalEligibilityEstablished' -and
      $clinicalInformationSummaryResponseJson-match'clinicianReviewCreated' -and
      $clinicalInformationSummaryResponseJson-match'practiceAccepted' -and
      $clinicalInformationSummaryResponseJson-match'requestCreated' -and
      $clinicalInformationSummaryResponseJson-match'queueEntered' -and
      $clinicalInformationSummaryResponseJson-match'careAuthorized' -and
      $clinicalInformationSummaryResponseJson-match'prescribingEnabled' -and
      @($clinicalInformationSummaryResponseProperties | Where-Object { $_ -in $clinicalInformationSummaryForbiddenProperties }).Count -eq 0)
    $preRequestReadinessRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/pre-request-readiness' 'get'
    $preRequestReadinessWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/pre-request-readiness' 'post'
    $preRequestReadinessRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $preRequestReadinessWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $preRequestReadinessRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($preRequestReadinessRequestReference -split '/')[-1])
    $preRequestReadinessRequestJson = $preRequestReadinessRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $preRequestReadinessRequestProperties = @((Get-Property $preRequestReadinessRequestSchema 'properties').PSObject.Properties.Name)
    $preRequestReadinessResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $preRequestReadinessWrite.responses '200') 'content') 'application/json').schema '$ref'
    $preRequestReadinessResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($preRequestReadinessResponseReference -split '/')[-1])
    $preRequestReadinessSectionSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'TelehealthApplicantPreRequestReadinessSectionResponse'
    $preRequestReadinessResponseJson = ($preRequestReadinessResponseSchema | ConvertTo-Json -Depth 30 -Compress) + ($preRequestReadinessSectionSchema | ConvertTo-Json -Depth 30 -Compress)
    $preRequestReadinessResponseProperties = @(
      (Get-Property $preRequestReadinessResponseSchema 'properties').PSObject.Properties.Name
      (Get-Property $preRequestReadinessSectionSchema 'properties').PSObject.Properties.Name)
    $preRequestReadinessForbiddenProperties = @('displayName','legalName','dateOfBirth','email','phone','address','memberId','payer','catalogKey','diagnosis','symptom','dose','reaction','note','attachment','freeText','canonicalPatientId','legacyPid','pubpid','promotionId','registrationDetailsConfirmationId','insuranceHandoffConfirmationId','communicationAccessReadinessId','devicePreparationId','clinicalInventoryId','clinicalInformationSummaryConfirmationId','requestId','queueEntry','appointment','encounter','prescriptionId','staffId','actorId','accessKey','commandFingerprint')
    Add-Check 'Synthetic pre-request readiness read and write publish applicant-only access, six-field acknowledgment input, minimized five-section output, bounded failures, and explicit no-authority results' (
      (Has-Security $preRequestReadinessRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $preRequestReadinessWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $preRequestReadinessRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $preRequestReadinessWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $preRequestReadinessRead 'X-Idempotency-Key') -and
      (Has-Header $preRequestReadinessWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $preRequestReadinessRead 'requestBody') -and
      $null-ne(Get-Property $preRequestReadinessWrite 'requestBody') -and
      $null-ne(Get-Property $preRequestReadinessRead.responses '401') -and
      $null-ne(Get-Property $preRequestReadinessRead.responses '404') -and
      $null-ne(Get-Property $preRequestReadinessRead.responses '409') -and
      $null-ne(Get-Property $preRequestReadinessRead.responses '410') -and
      $null-ne(Get-Property $preRequestReadinessWrite.responses '400') -and
      $null-ne(Get-Property $preRequestReadinessWrite.responses '401') -and
      $null-ne(Get-Property $preRequestReadinessWrite.responses '404') -and
      $null-ne(Get-Property $preRequestReadinessWrite.responses '409') -and
      $null-ne(Get-Property $preRequestReadinessWrite.responses '410') -and
      $preRequestReadinessRequestJson-match'expectedVersion' -and
      $preRequestReadinessRequestJson-match'preRequestReadinessSnapshotFingerprint' -and
      $preRequestReadinessRequestJson-match'priorSectionsReviewedAcknowledged' -and
      $preRequestReadinessRequestJson-match'outstandingStepsRemainAcknowledged' -and
      $preRequestReadinessRequestJson-match'noRequestOrQueueCreatedAcknowledged' -and
      $preRequestReadinessRequestJson-match'correctionRequiresSeparateWorkflowAcknowledged' -and
      $preRequestReadinessRequestProperties.Count -eq 6 -and
      @($preRequestReadinessRequestProperties | Where-Object { $_ -in $preRequestReadinessForbiddenProperties }).Count -eq 0 -and
      $preRequestReadinessResponseJson-match'sections' -and
      $preRequestReadinessResponseJson-match'sectionKey' -and
      $preRequestReadinessResponseJson-match'receiptState' -and
      $preRequestReadinessResponseJson-match'outstandingRoute' -and
      $preRequestReadinessResponseJson-match'overallRoute' -and
      $preRequestReadinessResponseJson-match'identityAssuranceEstablished' -and
      $preRequestReadinessResponseJson-match'coverageGuaranteed' -and
      $preRequestReadinessResponseJson-match'renderingClinicianNetworkVerified' -and
      $preRequestReadinessResponseJson-match'interpreterOrAccommodationArranged' -and
      $preRequestReadinessResponseJson-match'technologyReady' -and
      $preRequestReadinessResponseJson-match'clinicalInformationReconciled' -and
      $preRequestReadinessResponseJson-match'clinicalIntakeCompleted' -and
      $preRequestReadinessResponseJson-match'clinicalEligibilityEstablished' -and
      $preRequestReadinessResponseJson-match'legalConsentEstablished' -and
      $preRequestReadinessResponseJson-match'staffReviewCreated' -and
      $preRequestReadinessResponseJson-match'clinicianReviewCreated' -and
      $preRequestReadinessResponseJson-match'practiceAccepted' -and
      $preRequestReadinessResponseJson-match'patientRecordChanged' -and
      $preRequestReadinessResponseJson-match'requestCreated' -and
      $preRequestReadinessResponseJson-match'queueEntered' -and
      $preRequestReadinessResponseJson-match'appointmentCreated' -and
      $preRequestReadinessResponseJson-match'encounterCreated' -and
      $preRequestReadinessResponseJson-match'careAuthorized' -and
      $preRequestReadinessResponseJson-match'prescribingEnabled' -and
      $preRequestReadinessResponseJson-match'billingEnabled' -and
      $preRequestReadinessResponseJson-match'claimCreated' -and
      $preRequestReadinessResponseJson-match'integrationEnabled' -and
      $preRequestReadinessResponseJson-match'externalCallPerformed' -and
      @($preRequestReadinessResponseProperties | Where-Object { $_ -in $preRequestReadinessForbiddenProperties }).Count -eq 0)
    $practiceReviewRead = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/practice-review-submission' 'get'
    $practiceReviewWrite = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/practice-review-submission' 'post'
    $practiceReviewRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $practiceReviewRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($practiceReviewRequestReference -split '/')[-1])
    $practiceReviewRequestJson = $practiceReviewRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $practiceReviewRequestProperties = @((Get-Property $practiceReviewRequestSchema 'properties').PSObject.Properties.Name)
    $practiceReviewResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewWrite.responses '200') 'content') 'application/json').schema '$ref'
    $practiceReviewResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($practiceReviewResponseReference -split '/')[-1])
    $practiceReviewResponseJson = $practiceReviewResponseSchema | ConvertTo-Json -Depth 30 -Compress
    $practiceReviewResponseProperties = @((Get-Property $practiceReviewResponseSchema 'properties').PSObject.Properties.Name)
    $practiceReviewForbiddenProperties = @('displayName','legalName','dateOfBirth','email','phone','address','memberId','payer','diagnosis','symptom','dose','reaction','note','attachment','freeText','canonicalPatientId','legacyPid','pubpid','promotionId','readinessAcknowledgmentId','requestId','queueEntry','queuePosition','priority','assignedTo','doctorId','appointment','encounter','prescriptionId','staffId','actorId','accessKey','commandFingerprint')
    Add-Check 'Synthetic practice-review read and write publish applicant-only access, six-field acknowledgment input, minimized work-item output, bounded failures, and explicit no-request-or-care-queue results' (
      (Has-Security $practiceReviewRead 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $practiceReviewWrite 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $practiceReviewRead 'AvenChartPatientPortalSession') -and
      -not (Has-Security $practiceReviewWrite 'AvenChartLocalStaffSession') -and
      -not (Has-Header $practiceReviewRead 'X-Idempotency-Key') -and
      (Has-Header $practiceReviewWrite 'X-Idempotency-Key') -and
      $null-eq(Get-Property $practiceReviewRead 'requestBody') -and
      $null-ne(Get-Property $practiceReviewWrite 'requestBody') -and
      $null-ne(Get-Property $practiceReviewRead.responses '401') -and
      $null-ne(Get-Property $practiceReviewRead.responses '404') -and
      $null-ne(Get-Property $practiceReviewRead.responses '409') -and
      $null-ne(Get-Property $practiceReviewRead.responses '410') -and
      $null-ne(Get-Property $practiceReviewWrite.responses '400') -and
      $null-ne(Get-Property $practiceReviewWrite.responses '401') -and
      $null-ne(Get-Property $practiceReviewWrite.responses '404') -and
      $null-ne(Get-Property $practiceReviewWrite.responses '409') -and
      $null-ne(Get-Property $practiceReviewWrite.responses '410') -and
      $practiceReviewRequestJson-match'expectedVersion' -and
      $practiceReviewRequestJson-match'practiceReviewSnapshotFingerprint' -and
      $practiceReviewRequestJson-match'patientReportedInformationAcknowledged' -and
      $practiceReviewRequestJson-match'practiceMayRequestInformationOrDeclineAcknowledged' -and
      $practiceReviewRequestJson-match'noTelehealthRequestOrCareQueueAcknowledged' -and
      $practiceReviewRequestJson-match'worseningSymptomsRequireImmediateActionAcknowledged' -and
      $practiceReviewRequestProperties.Count -eq 6 -and
      @($practiceReviewRequestProperties | Where-Object { $_ -in $practiceReviewForbiddenProperties }).Count -eq 0 -and
      $practiceReviewResponseJson-match'reviewRoute' -and
      $practiceReviewResponseJson-match'reviewStatus' -and
      $practiceReviewResponseJson-match'staffReviewCreated' -and
      $practiceReviewResponseJson-match'clinicianReviewCreated' -and
      $practiceReviewResponseJson-match'practiceAccepted' -and
      $practiceReviewResponseJson-match'telehealthRequestCreated' -and
      $practiceReviewResponseJson-match'patientCareQueueEntered' -and
      $practiceReviewResponseJson-match'clinicianQueueEntered' -and
      $practiceReviewResponseJson-match'appointmentCreated' -and
      $practiceReviewResponseJson-match'encounterCreated' -and
      $practiceReviewResponseJson-match'careAuthorized' -and
      $practiceReviewResponseJson-match'billingEnabled' -and
      $practiceReviewResponseJson-match'claimCreated' -and
      $practiceReviewResponseJson-match'integrationEnabled' -and
      $practiceReviewResponseJson-match'externalCallPerformed' -and
      @($practiceReviewResponseProperties | Where-Object { $_ -in $practiceReviewForbiddenProperties }).Count -eq 0)
    $practiceReviewInboxPath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/admin/applicant-practice-review'
    $practiceReviewInbox = Get-Operation $document '/api/telehealth/v1/admin/applicant-practice-review' 'get'
    $practiceReviewInboxReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewInbox.responses '200') 'content') 'application/json').schema '$ref'
    $practiceReviewInboxSchemaName = ($practiceReviewInboxReference -split '/')[-1]
    $practiceReviewInboxSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceReviewInboxSchemaName
    $practiceReviewInboxProperties = @((Get-Property $practiceReviewInboxSchema 'properties').PSObject.Properties.Name)
    $practiceReviewInboxItemReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewInboxSchema 'properties') 'items') 'items') '$ref'
    $practiceReviewInboxItemSchemaName = ($practiceReviewInboxItemReference -split '/')[-1]
    $practiceReviewInboxItemSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceReviewInboxItemSchemaName
    $practiceReviewInboxItemProperties = @((Get-Property $practiceReviewInboxItemSchema 'properties').PSObject.Properties.Name)
    $practiceReviewInboxExpectedProperties = @(
      'practiceReviewCaseId','applicantVersion','applicantStatus','reviewStatus','legalFirstName','legalLastName',
      'dateOfBirth','maskedEmail','maskedPhone','residenceStateCode','postalCode','purposeCategory',
      'purposeDisplayLabel','safetyOutcome','reviewRoute','sections','submittedAt','staffReviewWorkItemExists',
      'staffActionTaken','assigned','assignedToCurrentUser','assignmentExpiresAt','priorityAssigned','practiceAccepted','practiceDeclined','patientContacted',
      'clinicianReviewCreated','telehealthRequestCreated','patientCareQueueEntered','clinicianQueueEntered',
      'appointmentCreated','encounterCreated','careAuthorized','prescribingEnabled','billingEnabled','claimCreated',
      'integrationEnabled','externalCallPerformed')
    Add-Check 'Applicant practice-review inbox is staff-scoped, GET-only, and carries no mutation identity' (
      @($practiceReviewInboxPath.PSObject.Properties.Name).Count -eq 1 -and
      @($practiceReviewInboxPath.PSObject.Properties.Name) -contains 'get' -and
      (Has-Security $practiceReviewInbox 'AvenChartLocalStaffSession') -and
      (Has-Security $practiceReviewInbox 'AvenChartOidcBearer') -and
      -not (Has-Security $practiceReviewInbox 'AvenChartPatientPortalSession') -and
      -not (Has-Security $practiceReviewInbox 'AvenChartTelehealthApplicantAccess') -and
      (Has-Header $practiceReviewInbox 'X-AvenChart-Facility-Id') -and
      (Has-Header $practiceReviewInbox 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $practiceReviewInbox 'X-Idempotency-Key') -and
      $null-eq(Get-Property $practiceReviewInbox 'requestBody') -and
      $null-ne(Get-Property $practiceReviewInbox.responses '200') -and
      $null-ne(Get-Property $practiceReviewInbox.responses '403') -and
      $null-ne(Get-Property $practiceReviewInbox.responses '404'))
    Add-Check 'Applicant practice-review inbox publishes the exact minimized read-only response contract' (
      @(Compare-Object @('items','limitations','policyKey','policyVersion','practiceDisplayName','serverTime') ($practiceReviewInboxProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($practiceReviewInboxExpectedProperties|Sort-Object) ($practiceReviewInboxItemProperties|Sort-Object)).Count -eq 0 -and
      @($practiceReviewInboxItemProperties | Where-Object { $_ -match 'applicantId|patientId|canonicalPatientId|memberId|groupNumber|payer|diagnosis|dose|directions|reaction|criticality|freeText|narrative|accessKey|fingerprint|assignedToActor|assignedToStaff|priorityValue|queuePosition|doctorId' }).Count -eq 0)
    $practiceReviewClaimPath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}/claim'
    $practiceReviewClaim = Get-Operation $document '/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}/claim' 'post'
    $practiceReviewClaimRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewClaim 'requestBody') 'content') 'application/json').schema '$ref'
    $practiceReviewClaimRequestSchemaName = ($practiceReviewClaimRequestReference -split '/')[-1]
    $practiceReviewClaimRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceReviewClaimRequestSchemaName
    $practiceReviewClaimRequestProperties = @((Get-Property $practiceReviewClaimRequestSchema 'properties').PSObject.Properties.Name)
    $practiceReviewClaimResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewClaim.responses '200') 'content') 'application/json').schema '$ref'
    $practiceReviewClaimResponseSchemaName = ($practiceReviewClaimResponseReference -split '/')[-1]
    $practiceReviewClaimResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceReviewClaimResponseSchemaName
    $practiceReviewClaimResponseProperties = @((Get-Property $practiceReviewClaimResponseSchema 'properties').PSObject.Properties.Name)
    $practiceReviewClaimExpectedResponseProperties = @(
      'claimId','practiceReviewCaseId','applicantVersion','policyKey','policyVersion','evidenceType','assignedAt',
      'assignmentExpiresAt','assigned','assignedToCurrentUser','staffReviewWorkItemExists','staffActionTaken',
      'priorityAssigned','practiceAccepted','practiceDeclined','patientContacted','clinicianReviewCreated',
      'telehealthRequestCreated','patientCareQueueEntered','clinicianQueueEntered','appointmentCreated',
      'encounterCreated','careAuthorized','prescribingEnabled','billingEnabled','claimCreated','integrationEnabled',
      'externalCallPerformed','limitations')
    Add-Check 'Practice-review claim is staff-scoped, POST-only, idempotent, and publishes bounded failures' (
      @($practiceReviewClaimPath.PSObject.Properties.Name).Count -eq 1 -and
      @($practiceReviewClaimPath.PSObject.Properties.Name) -contains 'post' -and
      (Has-Security $practiceReviewClaim 'AvenChartLocalStaffSession') -and
      (Has-Security $practiceReviewClaim 'AvenChartOidcBearer') -and
      -not (Has-Security $practiceReviewClaim 'AvenChartPatientPortalSession') -and
      -not (Has-Security $practiceReviewClaim 'AvenChartTelehealthApplicantAccess') -and
      (Has-Header $practiceReviewClaim 'X-AvenChart-Facility-Id') -and
      (Has-Header $practiceReviewClaim 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $practiceReviewClaim 'X-Idempotency-Key') -and
      $null-ne(Get-Property $practiceReviewClaim 'requestBody') -and
      $null-ne(Get-Property $practiceReviewClaim.responses '200') -and
      $null-ne(Get-Property $practiceReviewClaim.responses '400') -and
      $null-ne(Get-Property $practiceReviewClaim.responses '403') -and
      $null-ne(Get-Property $practiceReviewClaim.responses '404') -and
      $null-ne(Get-Property $practiceReviewClaim.responses '409'))
    Add-Check 'Practice-review claim publishes exact acknowledgments and a minimized identity-free lease receipt' (
      @(Compare-Object @('expectedApplicantVersion','inboxPolicyVersion','noDecisionAcknowledged','noPatientContactAcknowledged','noRequestOrCareQueueAcknowledged') ($practiceReviewClaimRequestProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($practiceReviewClaimExpectedResponseProperties|Sort-Object) ($practiceReviewClaimResponseProperties|Sort-Object)).Count -eq 0 -and
      @($practiceReviewClaimResponseProperties | Where-Object { $_ -match 'actor|staffId|username|displayName|patientId|applicantId|canonicalPatientId|memberId|groupNumber|payer|diagnosis|freeText|narrative|accessKey|fingerprint|priorityValue|queuePosition|doctorId' }).Count -eq 0)
    $practiceReviewPacketPath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}'
    $practiceReviewPacket = Get-Operation $document '/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}' 'get'
    $practiceReviewPacketReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewPacket.responses '200') 'content') 'application/json').schema '$ref'
    $practiceReviewPacketSchemaName = ($practiceReviewPacketReference -split '/')[-1]
    $practiceReviewPacketSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceReviewPacketSchemaName
    $practiceReviewPacketPropertiesObject = Get-Property $practiceReviewPacketSchema 'properties'
    $practiceReviewPacketProperties = @($practiceReviewPacketPropertiesObject.PSObject.Properties.Name)
    $practiceReviewPacketExpectedProperties = @(
      'practiceReviewCaseId','applicantVersion','applicantStatus','reviewStatus','policyKey','policyVersion',
      'practiceDisplayName','serverTime','assignmentExpiresAt','legalFirstName','legalLastName','dateOfBirth',
      'maskedEmail','maskedPhone','residenceStateCode','postalCode','purposeCategory','purposeDisplayLabel',
      'safetyOutcome','reviewRoute','submittedAt','sections','registration','insurance','communicationAccess',
      'devicePreparation','clinicalInformationSummaryRoute','clinicalInformationSummaryConfirmedAt',
      'staffReviewWorkItemExists','staffActionTaken','assigned','assignedToCurrentUser','priorityAssigned',
      'practiceAccepted','practiceDeclined','patientContacted','clinicianReviewCreated','telehealthRequestCreated',
      'patientCareQueueEntered','clinicianQueueEntered','appointmentCreated','encounterCreated','careAuthorized',
      'prescribingEnabled','billingEnabled','claimCreated','integrationEnabled','externalCallPerformed','limitations')
    function Get-PacketNestedProperties([string]$PropertyName) {
      $reference = Get-Property (Get-Property $practiceReviewPacketPropertiesObject $PropertyName) '$ref'
      $schemaName = ($reference -split '/')[-1]
      $schema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $schemaName
      @((Get-Property $schema 'properties').PSObject.Properties.Name)
    }
    $packetRegistrationProperties = Get-PacketNestedProperties 'registration'
    $packetInsuranceProperties = Get-PacketNestedProperties 'insurance'
    $packetCommunicationProperties = Get-PacketNestedProperties 'communicationAccess'
    $packetDeviceProperties = Get-PacketNestedProperties 'devicePreparation'
    Add-Check 'Practice-review packet is staff-scoped, GET-only, claimant-bound, no-store, and carries no mutation identity' (
      @($practiceReviewPacketPath.PSObject.Properties.Name).Count -eq 1 -and
      @($practiceReviewPacketPath.PSObject.Properties.Name) -contains 'get' -and
      (Has-Security $practiceReviewPacket 'AvenChartLocalStaffSession') -and
      (Has-Security $practiceReviewPacket 'AvenChartOidcBearer') -and
      -not (Has-Security $practiceReviewPacket 'AvenChartPatientPortalSession') -and
      -not (Has-Security $practiceReviewPacket 'AvenChartTelehealthApplicantAccess') -and
      (Has-Header $practiceReviewPacket 'X-AvenChart-Facility-Id') -and
      (Has-Header $practiceReviewPacket 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $practiceReviewPacket 'X-Idempotency-Key') -and
      $null-eq(Get-Property $practiceReviewPacket 'requestBody') -and
      $null-ne(Get-Property $practiceReviewPacket.responses '200') -and
      $null-ne(Get-Property $practiceReviewPacket.responses '403') -and
      $null-ne(Get-Property $practiceReviewPacket.responses '404'))
    Add-Check 'Practice-review packet publishes the exact masked operational contract without source, claimant, chart, or clinical-detail fields' (
      @(Compare-Object ($practiceReviewPacketExpectedProperties|Sort-Object) ($practiceReviewPacketProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object @('confirmedAt','identityAssuranceEstablished','patientRecordChanged','receiptRecorded') ($packetRegistrationProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object @('canonicalCoverageCreated','confirmedAt','coveragePriority','coverageVerified','eligibilityBusinessOutcome','eligibilityCheckedAt','eligibilityEvidenceCurrent','eligibilityExpiresAt','exactNetworkConfirmed','groupNumberMask','memberIdMask','payerDisplayName','practiceNetworkBusinessOutcome','practiceNetworkCheckedAt','practiceNetworkEvidenceCurrent','practiceNetworkExpiresAt','productDisplayName','renderingPhysicianNetworkChecked','subscriberRelationship') ($packetInsuranceProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object @('accessibilityAccommodationArranged','accessibilitySupportRequested','communicationArrangementCompleted','interpreterAssigned','interpreterRequested','preferredSpokenLanguage','recordedAt','safePrivateCommunicationConfirmed') ($packetCommunicationProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object @('browserSupported','cameraAvailable','mediaSessionCreated','microphoneAvailable','networkQuality','recordedAt','speakerAvailable','technologyReady','waitingRoomCreated') ($packetDeviceProperties|Sort-Object)).Count -eq 0 -and
      @($practiceReviewPacketProperties | Where-Object { $_ -match 'applicantId|patientId|canonicalPatientId|claimId|actor|staffId|username|accessKey|fingerprint|trace|promotionId|confirmationId|readinessId|diagnosis|medication|allergy|healthHistory|selectedItem|narrative|freeText|streetAddress|employer|guardian|clinicianId|providerId|queuePosition|priorityValue' }).Count -eq 0)
    $practiceReviewAuthorizationPath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}/authorization'
    $practiceReviewAuthorization = Get-Operation $document '/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}/authorization' 'post'
    $practiceReviewAuthorizationRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewAuthorization 'requestBody') 'content') 'application/json').schema '$ref'
    $practiceReviewAuthorizationRequestSchemaName = ($practiceReviewAuthorizationRequestReference -split '/')[-1]
    $practiceReviewAuthorizationRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceReviewAuthorizationRequestSchemaName
    $practiceReviewAuthorizationRequestProperties = @((Get-Property $practiceReviewAuthorizationRequestSchema 'properties').PSObject.Properties.Name)
    $practiceReviewAuthorizationResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $practiceReviewAuthorization.responses '200') 'content') 'application/json').schema '$ref'
    $practiceReviewAuthorizationResponseSchemaName = ($practiceReviewAuthorizationResponseReference -split '/')[-1]
    $practiceReviewAuthorizationResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $practiceReviewAuthorizationResponseSchemaName
    $practiceReviewAuthorizationResponseProperties = @((Get-Property $practiceReviewAuthorizationResponseSchema 'properties').PSObject.Properties.Name)
    $practiceReviewAuthorizationExpectedResponseProperties = @(
      'practiceReviewCaseId','applicantVersion','applicantStatus','decision','rationaleCode','policyKey','policyVersion',
      'evidenceType','decidedAt','requestCreationAuthorized','practiceAccepted','patientContacted','clinicianReviewCreated',
      'telehealthRequestCreated','patientCareQueueEntered','clinicianQueueEntered','appointmentCreated','encounterCreated',
      'consentCreated','careAuthorized','prescribingEnabled','billingEnabled','claimCreated','integrationEnabled',
      'externalCallPerformed','limitations')
    Add-Check 'Practice-review authorization is staff-scoped, positive-only, idempotent, and separately version-gated' (
      @($practiceReviewAuthorizationPath.PSObject.Properties.Name).Count -eq 1 -and
      @($practiceReviewAuthorizationPath.PSObject.Properties.Name) -contains 'post' -and
      (Has-Security $practiceReviewAuthorization 'AvenChartLocalStaffSession') -and
      (Has-Security $practiceReviewAuthorization 'AvenChartOidcBearer') -and
      -not (Has-Security $practiceReviewAuthorization 'AvenChartPatientPortalSession') -and
      -not (Has-Security $practiceReviewAuthorization 'AvenChartTelehealthApplicantAccess') -and
      (Has-Header $practiceReviewAuthorization 'X-AvenChart-Facility-Id') -and
      (Has-Header $practiceReviewAuthorization 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $practiceReviewAuthorization 'X-Idempotency-Key') -and
      $null-ne(Get-Property $practiceReviewAuthorization 'requestBody') -and
      $null-ne(Get-Property $practiceReviewAuthorization.responses '200') -and
      $null-ne(Get-Property $practiceReviewAuthorization.responses '400') -and
      $null-ne(Get-Property $practiceReviewAuthorization.responses '403') -and
      $null-ne(Get-Property $practiceReviewAuthorization.responses '404') -and
      $null-ne(Get-Property $practiceReviewAuthorization.responses '409'))
    Add-Check 'Practice-review authorization publishes the exact minimized no-care contract without claimant or patient identifiers' (
      @(Compare-Object @('decision','expectedApplicantVersion','noClinicalEligibilityAcknowledged','noCoverageGuaranteeAcknowledged','noRequestOrQueueAcknowledged','packetPolicyVersion','rationaleCode') ($practiceReviewAuthorizationRequestProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($practiceReviewAuthorizationExpectedResponseProperties|Sort-Object) ($practiceReviewAuthorizationResponseProperties|Sort-Object)).Count -eq 0 -and
      @($practiceReviewAuthorizationResponseProperties | Where-Object { $_ -match 'actor|staffId|username|displayName|patientId|applicantId|canonicalPatientId|memberId|groupNumber|payer|diagnosis|freeText|narrative|accessKey|fingerprint|claimId|queuePosition|doctorId' }).Count -eq 0)
    $applicantRequestPath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/applicants/{applicantId}/telehealth-request'
    $applicantRequestGet = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request' 'get'
    $applicantRequestPost = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request' 'post'
    $applicantRequestInputReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantRequestPost 'requestBody') 'content') 'application/json').schema '$ref'
    $applicantRequestInputSchemaName = ($applicantRequestInputReference -split '/')[-1]
    $applicantRequestInputSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantRequestInputSchemaName
    $applicantRequestInputProperties = @((Get-Property $applicantRequestInputSchema 'properties').PSObject.Properties.Name)
    $applicantRequestResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantRequestGet.responses '200') 'content') 'application/json').schema '$ref'
    $applicantRequestResponseSchemaName = ($applicantRequestResponseReference -split '/')[-1]
    $applicantRequestResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantRequestResponseSchemaName
    $applicantRequestResponseProperties = @((Get-Property $applicantRequestResponseSchema 'properties').PSObject.Properties.Name)
    $applicantRequestExpectedResponseProperties = @(
      'applicantId','applicantVersion','applicantStatus','policyKey','policyVersion','authorizationPolicyVersion',
      'requestCreationReady','requestCreated','requestId','requestStatus','requestVersion','complaintCategory','createdAt',
      'telehealthRequestCreated','patientContacted','patientCareQueueEntered','clinicianQueueEntered','doctorSearchStarted',
      'queuePositionAssigned','appointmentCreated','encounterCreated','consentCreated','careAuthorized','prescribingEnabled',
      'billingEnabled','claimCreated','integrationEnabled','externalCallPerformed','direction','limitations')
    Add-Check 'Applicant Draft-request projection and command are access-key-only, private, versioned, and idempotent' (
      @($applicantRequestPath.PSObject.Properties.Name).Count -eq 2 -and
      @($applicantRequestPath.PSObject.Properties.Name) -contains 'get' -and
      @($applicantRequestPath.PSObject.Properties.Name) -contains 'post' -and
      (Has-Security $applicantRequestGet 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $applicantRequestPost 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $applicantRequestPost 'AvenChartPatientPortalSession') -and
      -not (Has-Security $applicantRequestPost 'AvenChartLocalStaffSession') -and
      -not (Has-Header $applicantRequestGet 'X-Idempotency-Key') -and
      (Has-Header $applicantRequestPost 'X-Idempotency-Key') -and
      $null-eq(Get-Property $applicantRequestGet 'requestBody') -and
      $null-ne(Get-Property $applicantRequestPost 'requestBody') -and
      $null-ne(Get-Property $applicantRequestPost.responses '201') -and
      $null-ne(Get-Property $applicantRequestPost.responses '400') -and
      $null-ne(Get-Property $applicantRequestPost.responses '409'))
    Add-Check 'Applicant Draft-request contract accepts only controlled confirmations and exposes only opaque request workflow state' (
      @(Compare-Object @('authorizationPolicyVersion','expectedApplicantVersion','noQueueOrCareAcknowledged','requestCreationConfirmed','urgentOrWorseningSymptomsRequireImmediateActionAcknowledged') ($applicantRequestInputProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($applicantRequestExpectedResponseProperties|Sort-Object) ($applicantRequestResponseProperties|Sort-Object)).Count -eq 0 -and
      @($applicantRequestInputProperties | Where-Object { $_ -match 'patientId|promotionId|caseId|authorizationId|complaint|priority|note|freeText|clinical' }).Count -eq 0 -and
      @($applicantRequestResponseProperties | Where-Object { $_ -match 'patientId|promotionId|caseId|authorizationId|accessKey|memberId|payer|diagnosis|note|freeText|doctorId' }).Count -eq 0)
    $applicantLocationPath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/location'
    $applicantLocationGet = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/location' 'get'
    $applicantLocationPost = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/location' 'post'
    $applicantLocationInputReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantLocationPost 'requestBody') 'content') 'application/json').schema '$ref'
    $applicantLocationInputSchemaName = ($applicantLocationInputReference -split '/')[-1]
    $applicantLocationInputSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantLocationInputSchemaName
    $applicantLocationInputProperties = @((Get-Property $applicantLocationInputSchema 'properties').PSObject.Properties.Name)
    $applicantLocationResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantLocationGet.responses '200') 'content') 'application/json').schema '$ref'
    $applicantLocationResponseSchemaName = ($applicantLocationResponseReference -split '/')[-1]
    $applicantLocationResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantLocationResponseSchemaName
    $applicantLocationResponseProperties = @((Get-Property $applicantLocationResponseSchema 'properties').PSObject.Properties.Name)
    $applicantLocationExpectedResponseProperties = @(
      'applicantId','applicantVersion','applicantStatus','requestId','requestVersion','requestStatus','policyKey','policyVersion',
      'contextSnapshotFingerprint','currentLocationStateCode','maskedCallbackPhone','confirmationReady','locationConfirmed','confirmedAt',
      'triageAssessmentCreated','clinicalReviewCreated','patientContacted','patientCareQueueEntered','clinicianQueueEntered',
      'doctorSearchStarted','queuePositionAssigned','appointmentCreated','encounterCreated','consentCreated','careAuthorized',
      'prescribingEnabled','billingEnabled','claimCreated','integrationEnabled','externalCallPerformed','direction','limitations')
    Add-Check 'Applicant request-location projection and command are access-key-only, private, versioned, and idempotent' (
      @($applicantLocationPath.PSObject.Properties.Name).Count -eq 2 -and
      @($applicantLocationPath.PSObject.Properties.Name) -contains 'get' -and
      @($applicantLocationPath.PSObject.Properties.Name) -contains 'post' -and
      (Has-Security $applicantLocationGet 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $applicantLocationPost 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $applicantLocationPost 'AvenChartPatientPortalSession') -and
      -not (Has-Security $applicantLocationPost 'AvenChartLocalStaffSession') -and
      -not (Has-Header $applicantLocationGet 'X-Idempotency-Key') -and
      (Has-Header $applicantLocationPost 'X-Idempotency-Key') -and
      $null-eq(Get-Property $applicantLocationGet 'requestBody') -and
      $null-ne(Get-Property $applicantLocationPost 'requestBody') -and
      $null-ne(Get-Property $applicantLocationPost.responses '200') -and
      $null-ne(Get-Property $applicantLocationPost.responses '400') -and
      $null-ne(Get-Property $applicantLocationPost.responses '409'))
    Add-Check 'Applicant request-location contract accepts only controlled state and confirmations and exposes masked callback plus false downstream state' (
      @(Compare-Object @('callbackNumberConfirmed','changedLocationRequiresRestartAcknowledged','contextSnapshotFingerprint','currentLocationConfirmed','currentLocationStateCode','expectedRequestVersion','urgentOrWorseningSymptomsRequireImmediateActionAcknowledged') ($applicantLocationInputProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($applicantLocationExpectedResponseProperties|Sort-Object) ($applicantLocationResponseProperties|Sort-Object)).Count -eq 0 -and
      @($applicantLocationInputProperties | Where-Object { $_ -match 'patientId|callbackPhone|address|postal|latitude|longitude|complaint|note|freeText|clinical' }).Count -eq 0 -and
      @($applicantLocationResponseProperties | Where-Object { $_ -match 'patientId|accessKey|^callbackPhone$|address|postal|latitude|longitude|memberId|payer|diagnosis|note|freeText|doctorId' }).Count -eq 0)
    $applicantSafetyPath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/safety'
    $applicantSafetyGet = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/safety' 'get'
    $applicantSafetyPost = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/safety' 'post'
    $applicantSafetyInputReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantSafetyPost 'requestBody') 'content') 'application/json').schema '$ref'
    $applicantSafetyInputSchemaName = ($applicantSafetyInputReference -split '/')[-1]
    $applicantSafetyInputSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantSafetyInputSchemaName
    $applicantSafetyInputProperties = @((Get-Property $applicantSafetyInputSchema 'properties').PSObject.Properties.Name)
    $applicantSafetyResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $applicantSafetyGet.responses '200') 'content') 'application/json').schema '$ref'
    $applicantSafetyResponseSchemaName = ($applicantSafetyResponseReference -split '/')[-1]
    $applicantSafetyResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $applicantSafetyResponseSchemaName
    $applicantSafetyResponseProperties = @((Get-Property $applicantSafetyResponseSchema 'properties').PSObject.Properties.Name)
    $applicantSafetyExpectedResponseProperties = @(
      'applicantId','applicantVersion','applicantStatus','requestId','requestVersion','requestStatus','policyKey','policyVersion',
      'protocolKey','protocolVersion','contextSnapshotFingerprint','contextExpiresAt','currentLocationStateCode','maskedCallbackPhone',
      'assessmentReady','assessmentCreated','outcome','publicDisposition','evaluatedAt','universalSafetyPassed',
      'complaintSpecificTriageRequired','complaintSpecificTriageCreated','clinicalReviewRequired','clinicalReviewCreated',
      'terminalForTelehealth','patientContacted','patientCareQueueEntered','clinicianQueueEntered','doctorSearchStarted',
      'queuePositionAssigned','appointmentCreated','encounterCreated','consentCreated','careAuthorized','prescribingEnabled',
      'billingEnabled','claimCreated','integrationEnabled','externalCallPerformed','direction','limitations')
    Add-Check 'Applicant request universal-safety projection and command are access-key-only, private, versioned, and idempotent' (
      @($applicantSafetyPath.PSObject.Properties.Name).Count -eq 2 -and
      @($applicantSafetyPath.PSObject.Properties.Name) -contains 'get' -and
      @($applicantSafetyPath.PSObject.Properties.Name) -contains 'post' -and
      (Has-Security $applicantSafetyGet 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $applicantSafetyPost 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $applicantSafetyPost 'AvenChartPatientPortalSession') -and
      -not (Has-Security $applicantSafetyPost 'AvenChartLocalStaffSession') -and
      -not (Has-Header $applicantSafetyGet 'X-Idempotency-Key') -and
      (Has-Header $applicantSafetyPost 'X-Idempotency-Key') -and
      $null-eq(Get-Property $applicantSafetyGet 'requestBody') -and
      $null-ne(Get-Property $applicantSafetyPost 'requestBody') -and
      $null-ne(Get-Property $applicantSafetyPost.responses '200') -and
      $null-ne(Get-Property $applicantSafetyPost.responses '400') -and
      $null-ne(Get-Property $applicantSafetyPost.responses '409'))
    Add-Check 'Applicant request universal-safety contract accepts only exact context confirmations and four explicit answers while returning no answers or answer fingerprint' (
      @(Compare-Object @('callbackNumberConfirmed','contextSnapshotFingerprint','currentLocationConfirmed','currentLocationStateCode','expectedRequestVersion','hasEmergencyWarning','requiresHandsOnExam','severeOrWorsening','syntheticDataConfirmed','unsure') ($applicantSafetyInputProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($applicantSafetyExpectedResponseProperties|Sort-Object) ($applicantSafetyResponseProperties|Sort-Object)).Count -eq 0 -and
      @($applicantSafetyInputProperties | Where-Object { $_ -match 'patientId|callbackPhone|address|postal|latitude|longitude|complaint|note|freeText|diagnosis' }).Count -eq 0 -and
      @($applicantSafetyResponseProperties | Where-Object { $_ -match 'answer|answerFingerprint|patientId|accessKey|^callbackPhone$|address|postal|latitude|longitude|memberId|payer|diagnosis|note|freeText|doctorId' }).Count -eq 0)
    $complaintTriagePath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/complaint-triage'
    $complaintTriageGet = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/complaint-triage' 'get'
    $complaintTriagePost = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/complaint-triage' 'post'
    $complaintTriageInputReference = Get-Property (Get-Property (Get-Property (Get-Property $complaintTriagePost 'requestBody') 'content') 'application/json').schema '$ref'
    $complaintTriageInputSchemaName = ($complaintTriageInputReference -split '/')[-1]
    $complaintTriageInputSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $complaintTriageInputSchemaName
    $complaintTriageInputProperties = @((Get-Property $complaintTriageInputSchema 'properties').PSObject.Properties.Name)
    $complaintTriageMigraineSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'TelehealthSyntheticMigraineComplaintTriageAnswers'
    $complaintTriageSleepSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') 'TelehealthSyntheticSleepComplaintTriageAnswers'
    $complaintTriageMigraineProperties = @((Get-Property $complaintTriageMigraineSchema 'properties').PSObject.Properties.Name)
    $complaintTriageSleepProperties = @((Get-Property $complaintTriageSleepSchema 'properties').PSObject.Properties.Name)
    $complaintTriageResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $complaintTriageGet.responses '200') 'content') 'application/json').schema '$ref'
    $complaintTriageResponseSchemaName = ($complaintTriageResponseReference -split '/')[-1]
    $complaintTriageResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $complaintTriageResponseSchemaName
    $complaintTriageResponseProperties = @((Get-Property $complaintTriageResponseSchema 'properties').PSObject.Properties.Name)
    $complaintTriageExpectedResponseProperties = @(
      'applicantId','applicantVersion','applicantStatus','requestId','requestVersion','requestStatus','complaintCategory',
      'policyKey','policyVersion','protocolKey','protocolVersion','engineVersion','clinicalContentStatus',
      'medicalDirectorApprovalRequired','medicalDirectorApprovalRecorded','clinicalGoldenCasePackApproved','productionPublicationAllowed',
      'contextSnapshotFingerprint','contextExpiresAt','currentLocationStateCode','maskedCallbackPhone','assessmentReady','assessmentCreated',
      'outcome','publicDisposition','evaluatedAt','syntheticVideoEvaluationCandidate','clinicalReviewRequired','clinicalReviewCreated',
      'terminalForTelehealth','intakeSnapshotCreated','patientContacted','patientCareQueueEntered','clinicianQueueEntered','doctorSearchStarted',
      'queuePositionAssigned','appointmentCreated','encounterCreated','consentCreated','careAuthorized','prescribingEnabled','billingEnabled',
      'claimCreated','integrationEnabled','externalCallPerformed','direction','limitations')
    Add-Check 'Applicant request complaint-triage projection and command are access-key-only, private, versioned, and idempotent' (
      @($complaintTriagePath.PSObject.Properties.Name).Count -eq 2 -and
      @($complaintTriagePath.PSObject.Properties.Name) -contains 'get' -and
      @($complaintTriagePath.PSObject.Properties.Name) -contains 'post' -and
      (Has-Security $complaintTriageGet 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $complaintTriagePost 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $complaintTriagePost 'AvenChartPatientPortalSession') -and
      -not (Has-Security $complaintTriagePost 'AvenChartLocalStaffSession') -and
      -not (Has-Header $complaintTriageGet 'X-Idempotency-Key') -and
      (Has-Header $complaintTriagePost 'X-Idempotency-Key') -and
      $null-eq(Get-Property $complaintTriageGet 'requestBody') -and
      $null-ne(Get-Property $complaintTriagePost 'requestBody') -and
      $null-ne(Get-Property $complaintTriagePost.responses '200') -and
      $null-ne(Get-Property $complaintTriagePost.responses '400') -and
      $null-ne(Get-Property $complaintTriagePost.responses '409'))
    Add-Check 'Applicant request complaint-triage contract is exact, coded, publication-blocked, and returns no answers or rule evidence' (
      @(Compare-Object @('callbackNumberConfirmed','contextSnapshotFingerprint','currentLocationConfirmed','currentLocationStateCode','expectedRequestVersion','migraine','sleep','syntheticDataConfirmed') ($complaintTriageInputProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object @('cancerOrImmunocompromised','feverOrStiffNeck','knownSimilarPattern','newNeurologicOrVisionChange','persistentVomiting','pregnantOrPostpartum','recentHeadInjury','suddenOrWorstOnset') ($complaintTriageMigraineProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object @('breathingPausesOrSevereSnoring','controlledSedativeRequest','dangerousSomnolence','maniaOrPsychosis','pregnantOrComplexMedicationConcern','selfHarmThoughts','uncomplicatedSleepDifficulty','withdrawalConcern') ($complaintTriageSleepProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($complaintTriageExpectedResponseProperties|Sort-Object) ($complaintTriageResponseProperties|Sort-Object)).Count -eq 0 -and
      @($complaintTriageInputProperties | Where-Object { $_ -match 'patientId|callbackPhone|address|postal|latitude|longitude|note|freeText|diagnosis|medication' }).Count -eq 0 -and
      @($complaintTriageResponseProperties | Where-Object { $_ -match 'answer|answersFingerprint|protocolContentHash|firedRule|reasonCode|patientId|accessKey|^callbackPhone$|address|postal|latitude|longitude|memberId|payer|diagnosis|note|freeText|doctorId' }).Count -eq 0 -and
      @($complaintTriageResponseProperties | Where-Object { $_ -in @('medicalDirectorApprovalRequired','medicalDirectorApprovalRecorded','clinicalGoldenCasePackApproved','productionPublicationAllowed') }).Count -eq 4)
    $requestIntakePath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/intake'
    $requestIntakeGet = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/intake' 'get'
    $requestIntakePost = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/intake' 'post'
    $requestIntakeInputReference = Get-Property (Get-Property (Get-Property (Get-Property $requestIntakePost 'requestBody') 'content') 'application/json').schema '$ref'
    $requestIntakeInputSchemaName = ($requestIntakeInputReference -split '/')[-1]
    $requestIntakeInputSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $requestIntakeInputSchemaName
    $requestIntakeInputProperties = @((Get-Property $requestIntakeInputSchema 'properties').PSObject.Properties.Name)
    $requestIntakeResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $requestIntakeGet.responses '200') 'content') 'application/json').schema '$ref'
    $requestIntakeResponseSchemaName = ($requestIntakeResponseReference -split '/')[-1]
    $requestIntakeResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $requestIntakeResponseSchemaName
    $requestIntakeResponseProperties = @((Get-Property $requestIntakeResponseSchema 'properties').PSObject.Properties.Name)
    $requestIntakeExpectedResponseProperties = @(
      'applicantId','applicantVersion','applicantStatus','requestId','requestVersion','requestStatus',
      'complaintCategory','complaintDisplayLabel','policyKey','policyVersion','clinicalContentStatus',
      'medicalDirectorApprovalRequired','medicalDirectorApprovalRecorded','clinicalGoldenCasePackApproved',
      'productionPublicationAllowed','contextSnapshotFingerprint','contextExpiresAt','currentLocationStateCode',
      'maskedCallbackPhone','supportedSymptomDurations','sections','snapshotReady','snapshotCreated',
      'symptomDuration','capturedAt','verificationPending','consentPending','coverageRecordCreated',
      'coverageVerified','exactNetworkConfirmed','operationalReviewCreated','practiceAccepted','patientContacted',
      'patientCareQueueEntered','clinicianQueueEntered','doctorSearchStarted','queuePositionAssigned',
      'appointmentCreated','encounterCreated','consentCreated','careAuthorized','prescribingEnabled',
      'billingEnabled','claimCreated','integrationEnabled','externalCallPerformed','direction','limitations')
    Add-Check 'Applicant request intake projection and command are access-key-only, private, versioned, and idempotent' (
      @($requestIntakePath.PSObject.Properties.Name).Count -eq 2 -and
      @($requestIntakePath.PSObject.Properties.Name) -contains 'get' -and
      @($requestIntakePath.PSObject.Properties.Name) -contains 'post' -and
      (Has-Security $requestIntakeGet 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $requestIntakePost 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $requestIntakePost 'AvenChartPatientPortalSession') -and
      -not (Has-Security $requestIntakePost 'AvenChartLocalStaffSession') -and
      -not (Has-Header $requestIntakeGet 'X-Idempotency-Key') -and
      (Has-Header $requestIntakePost 'X-Idempotency-Key') -and
      $null-eq(Get-Property $requestIntakeGet 'requestBody') -and
      $null-ne(Get-Property $requestIntakePost 'requestBody') -and
      $null-ne(Get-Property $requestIntakePost.responses '200') -and
      $null-ne(Get-Property $requestIntakePost.responses '400') -and
      $null-ne(Get-Property $requestIntakePost.responses '409'))
    Add-Check 'Applicant request intake contract is exact, controlled, no-free-text, publication-blocked, and minimized' (
      @(Compare-Object @('callbackNumberConfirmed','complaintResultAcknowledged','contextSnapshotFingerprint','currentLocationConfirmed','currentLocationStateCode','expectedRequestVersion','insuranceLimitationsAcknowledged','pendingConsentAcknowledged','pendingVerificationAcknowledged','priorInformationReviewed','symptomDuration','syntheticDataConfirmed') ($requestIntakeInputProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($requestIntakeExpectedResponseProperties|Sort-Object) ($requestIntakeResponseProperties|Sort-Object)).Count -eq 0 -and
      @($requestIntakeInputProperties | Where-Object { $_ -match 'patientId|callbackPhone|address|postal|latitude|longitude|complaintCategory|complaintSummary|narrative|note|freeText|diagnosis|treatment|outcome|rule|reason|priority|coverageResult|networkResult|consentCreated' }).Count -eq 0 -and
      @($requestIntakeResponseProperties | Where-Object { $_ -match 'sourceComplaint|commandFingerprint|protocolContentHash|answer|firedRule|reasonCode|patientId|accessKey|^callbackPhone$|address|postal|latitude|longitude|memberId|payer|diagnosis|note|freeText|doctorId' }).Count -eq 0 -and
      @($requestIntakeResponseProperties | Where-Object { $_ -in @('verificationPending','consentPending','coverageRecordCreated','coverageVerified','exactNetworkConfirmed','operationalReviewCreated','practiceAccepted') }).Count -eq 7)
    $requestInsuranceSourcePath = Get-Property (Get-Property $document 'paths') '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/insurance-source'
    $requestInsuranceSourceGet = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/insurance-source' 'get'
    $requestInsuranceSourcePost = Get-Operation $document '/api/telehealth/v1/applicants/{applicantId}/telehealth-request/insurance-source' 'post'
    $requestInsuranceSourceInputReference = Get-Property (Get-Property (Get-Property (Get-Property $requestInsuranceSourcePost 'requestBody') 'content') 'application/json').schema '$ref'
    $requestInsuranceSourceInputSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($requestInsuranceSourceInputReference -split '/')[-1])
    $requestInsuranceSourceInputProperties = @((Get-Property $requestInsuranceSourceInputSchema 'properties').PSObject.Properties.Name)
    $requestInsuranceSourceResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $requestInsuranceSourceGet.responses '200') 'content') 'application/json').schema '$ref'
    $requestInsuranceSourceResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($requestInsuranceSourceResponseReference -split '/')[-1])
    $requestInsuranceSourceResponseProperties = @((Get-Property $requestInsuranceSourceResponseSchema 'properties').PSObject.Properties.Name)
    $requestInsuranceSourceExpectedResponseProperties = @(
      'applicantId','applicantVersion','applicantStatus','requestId','requestVersion','requestStatus',
      'policyKey','policyVersion','insuranceSourceSnapshotFingerprint','contextExpiresAt','payerDisplayName',
      'productDisplayName','maskedMemberId','maskedGroupNumber','subscriberRelationship','coveragePriority',
      'previousEligibilityBusinessOutcome','previousEligibilityCheckedAt','previousEligibilityExpiresAt',
      'previousEligibilityEvidenceExpired','previousPracticeNetworkBusinessOutcome',
      'previousPracticeNetworkCheckedAt','previousPracticeNetworkExpiresAt',
      'previousPracticeNetworkEvidenceExpired','previousRenderingPhysicianNetworkChecked','previousResultReusable',
      'sourceReady','sourceConfirmed','confirmedAt','protectedPayloadReferenced','protectedPayloadCopied',
      'protectedPayloadDecrypted','freshVerificationRequested','canonicalCoverageCreated','coverageSelected',
      'eligibilityVerificationCreated','networkVerificationCreated','renderingPhysicianNetworkChecked',
      'coverageVerified','exactNetworkConfirmed','financialRouteCreated','operationalReviewCreated',
      'practiceAccepted','patientContacted','patientCareQueueEntered','clinicianQueueEntered',
      'doctorSearchStarted','queuePositionAssigned','appointmentCreated','encounterCreated','consentCreated',
      'careAuthorized','integrationEnabled','externalCallPerformed','direction','limitations')
    Add-Check 'Applicant request insurance-source projection and command are access-key-only, private, versioned, and idempotent' (
      @($requestInsuranceSourcePath.PSObject.Properties.Name).Count -eq 2 -and
      @($requestInsuranceSourcePath.PSObject.Properties.Name) -contains 'get' -and
      @($requestInsuranceSourcePath.PSObject.Properties.Name) -contains 'post' -and
      (Has-Security $requestInsuranceSourceGet 'AvenChartTelehealthApplicantAccess') -and
      (Has-Security $requestInsuranceSourcePost 'AvenChartTelehealthApplicantAccess') -and
      -not (Has-Security $requestInsuranceSourcePost 'AvenChartPatientPortalSession') -and
      -not (Has-Security $requestInsuranceSourcePost 'AvenChartLocalStaffSession') -and
      -not (Has-Header $requestInsuranceSourceGet 'X-Idempotency-Key') -and
      (Has-Header $requestInsuranceSourcePost 'X-Idempotency-Key') -and
      $null-eq(Get-Property $requestInsuranceSourceGet 'requestBody') -and
      $null-ne(Get-Property $requestInsuranceSourcePost 'requestBody') -and
      $null-ne(Get-Property $requestInsuranceSourcePost.responses '200') -and
      $null-ne(Get-Property $requestInsuranceSourcePost.responses '400') -and
      $null-ne(Get-Property $requestInsuranceSourcePost.responses '409'))
    Add-Check 'Applicant request insurance-source contract is exact, no-edit, historical-only, protected-source-safe, and minimized' (
      @(Compare-Object @('evidenceLimitationsAcknowledged','expectedRequestVersion','freshVerificationRequested','insuranceSourceSnapshotFingerprint','maskedMemberDetailsConfirmed','payerProductConfirmed','primaryCoverageSourceConfirmed','subscriberRelationshipConfirmed','syntheticDataConfirmed') ($requestInsuranceSourceInputProperties|Sort-Object)).Count -eq 0 -and
      @(Compare-Object ($requestInsuranceSourceExpectedResponseProperties|Sort-Object) ($requestInsuranceSourceResponseProperties|Sort-Object)).Count -eq 0 -and
      @($requestInsuranceSourceInputProperties | Where-Object { $_ -match '^memberId$|^groupNumber$|payerDisplayName|productDisplayName|patientId|canonicalPatient|protectedPayload|eligibilityOutcome|networkOutcome|renderingPhysician|price|financial|operational|queue|note|freeText' }).Count -eq 0 -and
      @($requestInsuranceSourceResponseProperties | Where-Object { $_ -match '^memberId$|^groupNumber$|canonicalPatientId|patientId|accessKey|commandFingerprint|protectedPayload$|raw|npi|doctorId|price|estimate' }).Count -eq 0 -and
      @($requestInsuranceSourceResponseProperties | Where-Object { $_ -in @('previousResultReusable','protectedPayloadCopied','protectedPayloadDecrypted','canonicalCoverageCreated','eligibilityVerificationCreated','networkVerificationCreated','renderingPhysicianNetworkChecked','coverageVerified','exactNetworkConfirmed','financialRouteCreated','operationalReviewCreated','practiceAccepted','patientCareQueueEntered','clinicianQueueEntered','appointmentCreated','encounterCreated','consentCreated','careAuthorized','integrationEnabled','externalCallPerformed') }).Count -eq 20)
    $identityReviewList = Get-Operation $document '/api/telehealth/v1/admin/applicant-identity-review' 'get'
    $identityReviewWrite = Get-Operation $document '/api/telehealth/v1/admin/applicants/{applicantId}/identity-review-decision' 'put'
    $identityReviewRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $identityReviewWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $identityReviewRequestSchemaName = ($identityReviewRequestReference -split '/')[-1]
    $identityReviewRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $identityReviewRequestSchemaName
    $identityReviewRequestJson = $identityReviewRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $identityReviewResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $identityReviewWrite.responses '200') 'content') 'application/json').schema '$ref'
    $identityReviewResponseSchemaName = ($identityReviewResponseReference -split '/')[-1]
    $identityReviewResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $identityReviewResponseSchemaName
    $identityReviewResponseJson = $identityReviewResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Applicant identity-review queue is staff-scoped and read-only without mutation identity' (
      (Has-Security $identityReviewList 'AvenChartLocalStaffSession') -and
      (Has-Security $identityReviewList 'AvenChartOidcBearer') -and
      -not (Has-Security $identityReviewList 'AvenChartPatientPortalSession') -and
      (Has-Header $identityReviewList 'X-AvenChart-Facility-Id') -and
      (Has-Header $identityReviewList 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $identityReviewList 'X-Idempotency-Key') -and
      $null-eq(Get-Property $identityReviewList 'requestBody'))
    Add-Check 'Applicant identity-review decision publishes staff scope, explicit replay/version input, bounded failures, and false proofing/promotion flags' (
      (Has-Security $identityReviewWrite 'AvenChartLocalStaffSession') -and
      (Has-Security $identityReviewWrite 'AvenChartOidcBearer') -and
      -not (Has-Security $identityReviewWrite 'AvenChartPatientPortalSession') -and
      (Has-Header $identityReviewWrite 'X-AvenChart-Facility-Id') -and
      (Has-Header $identityReviewWrite 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $identityReviewWrite 'X-Idempotency-Key') -and
      $null-ne(Get-Property $identityReviewWrite 'requestBody') -and
      $null-ne(Get-Property $identityReviewWrite.responses '400') -and
      $null-ne(Get-Property $identityReviewWrite.responses '403') -and
      $null-ne(Get-Property $identityReviewWrite.responses '404') -and
      $null-ne(Get-Property $identityReviewWrite.responses '409') -and
      $identityReviewRequestJson-match'expectedVersion' -and
      $identityReviewRequestJson-match'decision' -and
      $identityReviewRequestJson-match'reason' -and
      $identityReviewRequestJson-match'syntheticDataConfirmed' -and
      $identityReviewRequestJson-notmatch'patientId|candidateId|accessKey|evidenceFingerprint|staffId|actorId' -and
      $identityReviewResponseJson-match'identityProofed' -and
      $identityReviewResponseJson-match'canonicalPatientCreated' -and
      $identityReviewResponseJson-match'chartLinked' -and
      $identityReviewResponseJson-match'prospectiveIntakeCompleted' -and
      $identityReviewResponseJson-match'requestCreated' -and
      $identityReviewResponseJson-match'queueEnabled' -and
      $identityReviewResponseJson-notmatch'patientId|candidateId|canonicalPatientId|staffId|actorId')
    $promotionList = Get-Operation $document '/api/telehealth/v1/admin/applicant-promotion-authorization' 'get'
    $promotionWrite = Get-Operation $document '/api/telehealth/v1/admin/applicants/{applicantId}/promotion-authorization-decision' 'put'
    $promotionListReference = Get-Property (Get-Property (Get-Property (Get-Property $promotionList.responses '200') 'content') 'application/json').schema '$ref'
    $promotionListSchemaName = ($promotionListReference -split '/')[-1]
    $promotionListSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $promotionListSchemaName
    $promotionItemReference = Get-Property (Get-Property (Get-Property (Get-Property $promotionListSchema 'properties') 'applicants') 'items') '$ref'
    $promotionItemSchemaName = ($promotionItemReference -split '/')[-1]
    $promotionItemSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $promotionItemSchemaName
    $promotionItemJson = $promotionItemSchema | ConvertTo-Json -Depth 30 -Compress
    $promotionRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $promotionWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $promotionRequestSchemaName = ($promotionRequestReference -split '/')[-1]
    $promotionRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $promotionRequestSchemaName
    $promotionRequestJson = $promotionRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $promotionResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $promotionWrite.responses '200') 'content') 'application/json').schema '$ref'
    $promotionResponseSchemaName = ($promotionResponseReference -split '/')[-1]
    $promotionResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $promotionResponseSchemaName
    $promotionResponseJson = $promotionResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Applicant promotion-authorization queue is staff-scoped, read-only, normalized, and excludes raw evidence and canonical identifiers' (
      (Has-Security $promotionList 'AvenChartLocalStaffSession') -and
      (Has-Security $promotionList 'AvenChartOidcBearer') -and
      -not (Has-Security $promotionList 'AvenChartPatientPortalSession') -and
      (Has-Header $promotionList 'X-AvenChart-Facility-Id') -and
      (Has-Header $promotionList 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $promotionList 'X-Idempotency-Key') -and
      $null-eq(Get-Property $promotionList 'requestBody') -and
      $promotionItemJson-match'eligibilityBusinessOutcome' -and
      $promotionItemJson-match'networkBusinessOutcome' -and
      $promotionItemJson-match'proofingBusinessOutcome' -and
      $promotionItemJson-match'assuranceLevelAchieved' -and
      $promotionItemJson-match'identityProofed' -and
      $promotionItemJson-match'allowedDecisions' -and
      $promotionItemJson-notmatch'"email"|"phone"|memberId|groupNumber|subscriberFirst|subscriberLast|protectedPayload|requestTrace|responseTrace|proofingSessionReference|evidencePackageReference|identityProofingResultId|patientId|canonicalPatientId|governmentIdentifier|biometric')
    Add-Check 'Applicant promotion-authorization decision publishes staff scope, exact governed input, bounded failures, and explicit false real consequences' (
      (Has-Security $promotionWrite 'AvenChartLocalStaffSession') -and
      (Has-Security $promotionWrite 'AvenChartOidcBearer') -and
      -not (Has-Security $promotionWrite 'AvenChartPatientPortalSession') -and
      (Has-Header $promotionWrite 'X-AvenChart-Facility-Id') -and
      (Has-Header $promotionWrite 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $promotionWrite 'X-Idempotency-Key') -and
      $null-ne(Get-Property $promotionWrite 'requestBody') -and
      $null-ne(Get-Property $promotionWrite.responses '400') -and
      $null-ne(Get-Property $promotionWrite.responses '403') -and
      $null-ne(Get-Property $promotionWrite.responses '404') -and
      $null-ne(Get-Property $promotionWrite.responses '409') -and
      $null-ne(Get-Property $promotionWrite.responses '410') -and
      $promotionRequestJson-match'expectedVersion' -and
      $promotionRequestJson-match'decision' -and
      $promotionRequestJson-match'reason' -and
      $promotionRequestJson-match'noneAssuranceAcknowledged' -and
      $promotionRequestJson-match'syntheticDataConfirmed' -and
      $promotionRequestJson-notmatch'patientId|memberId|groupNumber|proofing|assuranceLevel|identityProofed|evidence|staffId|actorId|accessKey' -and
      $promotionResponseJson-match'noneAssuranceAcknowledged' -and
      $promotionResponseJson-match'realIdentityProofed' -and
      $promotionResponseJson-match'canonicalPatientCreated' -and
      $promotionResponseJson-match'chartLinked' -and
      $promotionResponseJson-match'portalAccountCreated' -and
      $promotionResponseJson-match'prospectiveIntakeCompleted' -and
      $promotionResponseJson-match'consentCreated' -and
      $promotionResponseJson-match'practiceAccepted' -and
      $promotionResponseJson-match'requestCreated' -and
      $promotionResponseJson-match'queueEnabled' -and
      $promotionResponseJson-notmatch'patientId|memberId|groupNumber|proofingResult|canonicalPatientId|staffId|actorId|commandFingerprint')
    $syntheticPromotionList = Get-Operation $document '/api/telehealth/v1/admin/applicant-synthetic-promotion' 'get'
    $syntheticPromotionWrite = Get-Operation $document '/api/telehealth/v1/admin/applicants/{applicantId}/synthetic-promotion' 'put'
    $syntheticPromotionListReference = Get-Property (Get-Property (Get-Property (Get-Property $syntheticPromotionList.responses '200') 'content') 'application/json').schema '$ref'
    $syntheticPromotionListSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($syntheticPromotionListReference -split '/')[-1])
    $syntheticPromotionItemReference = Get-Property (Get-Property (Get-Property (Get-Property $syntheticPromotionListSchema 'properties') 'applicants') 'items') '$ref'
    $syntheticPromotionItemSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($syntheticPromotionItemReference -split '/')[-1])
    $syntheticPromotionItemJson = $syntheticPromotionItemSchema | ConvertTo-Json -Depth 30 -Compress
    $syntheticPromotionRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $syntheticPromotionWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $syntheticPromotionRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($syntheticPromotionRequestReference -split '/')[-1])
    $syntheticPromotionRequestJson = $syntheticPromotionRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $syntheticPromotionResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $syntheticPromotionWrite.responses '200') 'content') 'application/json').schema '$ref'
    $syntheticPromotionResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') (($syntheticPromotionResponseReference -split '/')[-1])
    $syntheticPromotionResponseJson = $syntheticPromotionResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Atomic synthetic-promotion queue is staff-scoped, read-only, contact-masked, and excludes candidate or canonical identifiers' (
      (Has-Security $syntheticPromotionList 'AvenChartLocalStaffSession') -and
      (Has-Security $syntheticPromotionList 'AvenChartOidcBearer') -and
      -not (Has-Security $syntheticPromotionList 'AvenChartPatientPortalSession') -and
      (Has-Header $syntheticPromotionList 'X-AvenChart-Facility-Id') -and
      (Has-Header $syntheticPromotionList 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $syntheticPromotionList 'X-Idempotency-Key') -and
      $null-eq(Get-Property $syntheticPromotionList 'requestBody') -and
      $syntheticPromotionItemJson-match'maskedEmail' -and
      $syntheticPromotionItemJson-match'maskedPhone' -and
      $syntheticPromotionItemJson-match'authorizationDecision' -and
      $syntheticPromotionItemJson-match'assuranceLevelAchieved' -and
      $syntheticPromotionItemJson-match'identityProofed' -and
      $syntheticPromotionItemJson-match'allowedCommand' -and
      $syntheticPromotionItemJson-notmatch'canonicalPatientId|legacyPid|matchedPatient|candidatePatient|memberId|groupNumber|protectedPayload|requestTrace|responseTrace|governmentIdentifier|biometric')
    Add-Check 'Atomic synthetic-promotion command publishes exact acknowledgments, semantic replay, bounded failures, and privacy-safe outcome flags' (
      (Has-Security $syntheticPromotionWrite 'AvenChartLocalStaffSession') -and
      (Has-Security $syntheticPromotionWrite 'AvenChartOidcBearer') -and
      -not (Has-Security $syntheticPromotionWrite 'AvenChartPatientPortalSession') -and
      (Has-Header $syntheticPromotionWrite 'X-AvenChart-Facility-Id') -and
      (Has-Header $syntheticPromotionWrite 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $syntheticPromotionWrite 'X-Idempotency-Key') -and
      $null-ne(Get-Property $syntheticPromotionWrite 'requestBody') -and
      $null-ne(Get-Property $syntheticPromotionWrite.responses '400') -and
      $null-ne(Get-Property $syntheticPromotionWrite.responses '403') -and
      $null-ne(Get-Property $syntheticPromotionWrite.responses '404') -and
      $null-ne(Get-Property $syntheticPromotionWrite.responses '409') -and
      $null-ne(Get-Property $syntheticPromotionWrite.responses '410') -and
      $syntheticPromotionRequestJson-match'expectedVersion' -and
      $syntheticPromotionRequestJson-match'command' -and
      $syntheticPromotionRequestJson-match'reason' -and
      $syntheticPromotionRequestJson-match'canonicalPatientCreationAcknowledged' -and
      $syntheticPromotionRequestJson-match'noPortalNoCareAcknowledged' -and
      $syntheticPromotionRequestJson-notmatch'canonicalPatientId|legacyPid|matchedPatient|candidatePatient|staffId|actorId|accessKey' -and
      $syntheticPromotionResponseJson-match'outcome' -and
      $syntheticPromotionResponseJson-match'possibleMatchDetected' -and
      $syntheticPromotionResponseJson-match'canonicalPatientCreated' -and
      $syntheticPromotionResponseJson-match'portalAccountCreated' -and
      $syntheticPromotionResponseJson-match'prospectiveIntakeCompleted' -and
      $syntheticPromotionResponseJson-match'consentCreated' -and
      $syntheticPromotionResponseJson-match'practiceAccepted' -and
      $syntheticPromotionResponseJson-match'insuranceCreated' -and
      $syntheticPromotionResponseJson-match'requestCreated' -and
      $syntheticPromotionResponseJson-match'queueEnabled' -and
      $syntheticPromotionResponseJson-match'careEnabled' -and
      $syntheticPromotionResponseJson-notmatch'canonicalPatientId|legacyPid|pubpid|matchedPatient|candidatePatient|staffId|actorId|commandFingerprint')
    $readiness = Get-Operation $document '/api/telehealth/v1/patient/requests/{requestId}/readiness' 'post'
    $verification = Get-Operation $document '/api/telehealth/v1/patient/requests/{requestId}/coverage/verify' 'post'
    Add-Check 'Readiness and coverage commands publish patient authentication, idempotency, version-conflict, and typed body contracts' (
      (Has-Security $readiness 'AvenChartPatientPortalSession') -and (Has-Header $readiness 'X-Idempotency-Key') -and
      $null-ne(Get-Property $readiness.responses '409') -and $null-ne(Get-Property $readiness 'requestBody') -and
      (Has-Security $verification 'AvenChartPatientPortalSession') -and (Has-Header $verification 'X-Idempotency-Key') -and
      $null-ne(Get-Property $verification.responses '409') -and $null-ne(Get-Property $verification 'requestBody'))
    $patientStatus = Get-Operation $document '/api/telehealth/v1/patient/requests/{requestId}/status' 'get'
    Add-Check 'Patient queue status publishes patient authentication and read-only not-found semantics without mutation headers' (
      (Has-Security $patientStatus 'AvenChartPatientPortalSession') -and (Has-Security $patientStatus 'AvenChartOidcBearer') -and
      -not (Has-Header $patientStatus 'X-Idempotency-Key') -and $null -ne (Get-Property $patientStatus.responses '404'))
    $patientConnection = Get-Operation $document '/api/telehealth/v1/patient/requests/{requestId}/connection-grants' 'post'
    $physicianConnection = Get-Operation $document '/api/telehealth/v1/clinician/reservations/{reservationId}/connection-grants' 'post'
    Add-Check 'Patient and physician connection grants publish distinct auth scopes, typed preflight, idempotency, and conflict contracts' (
      (Has-Security $patientConnection 'AvenChartPatientPortalSession') -and -not (Has-Security $patientConnection 'AvenChartLocalStaffSession') -and
      (Has-Header $patientConnection 'X-Idempotency-Key') -and $null-ne(Get-Property $patientConnection 'requestBody') -and $null-ne(Get-Property $patientConnection.responses '409') -and
      (Has-Security $physicianConnection 'AvenChartLocalStaffSession') -and -not (Has-Security $physicianConnection 'AvenChartPatientPortalSession') -and
      (Has-Header $physicianConnection 'X-AvenChart-Facility-Id') -and (Has-Header $physicianConnection 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $physicianConnection 'X-Idempotency-Key') -and $null-ne(Get-Property $physicianConnection 'requestBody') -and $null-ne(Get-Property $physicianConnection.responses '409'))
    $consultationStart = Get-Operation $document '/api/telehealth/v1/clinician/reservations/{reservationId}/consultations/start' 'post'
    Add-Check 'Consultation start publishes physician-only scoped auth, affirmative input, idempotency, and all bounded failure outcomes' (
      (Has-Security $consultationStart 'AvenChartLocalStaffSession') -and -not (Has-Security $consultationStart 'AvenChartPatientPortalSession') -and
      (Has-Header $consultationStart 'X-AvenChart-Facility-Id') -and (Has-Header $consultationStart 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $consultationStart 'X-Idempotency-Key') -and $null-ne(Get-Property $consultationStart 'requestBody') -and
      $null-ne(Get-Property $consultationStart.responses '400') -and $null-ne(Get-Property $consultationStart.responses '403') -and
      $null-ne(Get-Property $consultationStart.responses '404') -and $null-ne(Get-Property $consultationStart.responses '409'))
    $consultationWorkspace = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/workspace' 'get'
    Add-Check 'Read-only consultation workspace publishes staff scope and opaque not-found semantics without mutation inputs' (
      (Has-Security $consultationWorkspace 'AvenChartLocalStaffSession') -and -not (Has-Security $consultationWorkspace 'AvenChartPatientPortalSession') -and
      (Has-Header $consultationWorkspace 'X-AvenChart-Facility-Id') -and (Has-Header $consultationWorkspace 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $consultationWorkspace 'X-Idempotency-Key') -and $null-eq(Get-Property $consultationWorkspace 'requestBody') -and
      $null-ne(Get-Property $consultationWorkspace.responses '403') -and $null-ne(Get-Property $consultationWorkspace.responses '404'))
    $consultationDraft = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/documentation/draft' 'put'
    Add-Check 'Consultation draft publishes scoped staff auth, typed versioned content, and bounded failure outcomes without idempotency or external identifiers' (
      (Has-Security $consultationDraft 'AvenChartLocalStaffSession') -and -not (Has-Security $consultationDraft 'AvenChartPatientPortalSession') -and
      (Has-Header $consultationDraft 'X-AvenChart-Facility-Id') -and (Has-Header $consultationDraft 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $consultationDraft 'X-Idempotency-Key') -and $null-ne(Get-Property $consultationDraft 'requestBody') -and
      $null-ne(Get-Property $consultationDraft.responses '400') -and $null-ne(Get-Property $consultationDraft.responses '403') -and
      $null-ne(Get-Property $consultationDraft.responses '404') -and $null-ne(Get-Property $consultationDraft.responses '409'))
    $consultationWrapUp = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/wrap-up' 'post'
    $consultationWrapUpJson = $consultationWrapUp | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Consultation wrap-up publishes physician-scoped affirmative input, idempotency, typed output, and bounded failures without canonical identifiers' (
      (Has-Security $consultationWrapUp 'AvenChartLocalStaffSession') -and -not (Has-Security $consultationWrapUp 'AvenChartPatientPortalSession') -and
      (Has-Header $consultationWrapUp 'X-AvenChart-Facility-Id') -and (Has-Header $consultationWrapUp 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $consultationWrapUp 'X-Idempotency-Key') -and $null-ne(Get-Property $consultationWrapUp 'requestBody') -and
      $null-ne(Get-Property $consultationWrapUp.responses '200') -and $null-ne(Get-Property $consultationWrapUp.responses '400') -and
      $null-ne(Get-Property $consultationWrapUp.responses '403') -and $null-ne(Get-Property $consultationWrapUp.responses '404') -and
      $null-ne(Get-Property $consultationWrapUp.responses '409') -and
      $consultationWrapUpJson-notmatch'patientId|encounterId|appointmentId|requestId|shiftId|disposition')
    $pharmacyChoices = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/pharmacy-choices' 'get'
    $pharmacyChoice = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/pharmacy-choice' 'put'
    $pharmacyChoiceRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $pharmacyChoice 'requestBody') 'content') 'application/json').schema '$ref'
    $pharmacyChoiceRequestSchemaName = ($pharmacyChoiceRequestReference -split '/')[-1]
    $pharmacyChoiceRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $pharmacyChoiceRequestSchemaName
    $pharmacyChoiceRequestJson = $pharmacyChoiceRequestSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Pharmacy search is physician-scoped, read-only, typed, and carries no mutation identity' (
      (Has-Security $pharmacyChoices 'AvenChartLocalStaffSession') -and -not (Has-Security $pharmacyChoices 'AvenChartPatientPortalSession') -and
      (Has-Header $pharmacyChoices 'X-AvenChart-Facility-Id') -and (Has-Header $pharmacyChoices 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $pharmacyChoices 'X-Idempotency-Key') -and $null-eq(Get-Property $pharmacyChoices 'requestBody') -and
      $null-ne(Get-Property $pharmacyChoices.responses '400') -and $null-ne(Get-Property $pharmacyChoices.responses '403') -and
      $null-ne(Get-Property $pharmacyChoices.responses '404'))
    Add-Check 'Pharmacy destination draft publishes physician-scoped confirmations, idempotency, version conflict, and no prescription payload' (
      (Has-Security $pharmacyChoice 'AvenChartLocalStaffSession') -and -not (Has-Security $pharmacyChoice 'AvenChartPatientPortalSession') -and
      (Has-Header $pharmacyChoice 'X-AvenChart-Facility-Id') -and (Has-Header $pharmacyChoice 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $pharmacyChoice 'X-Idempotency-Key') -and $null-ne(Get-Property $pharmacyChoice 'requestBody') -and
      $null-ne(Get-Property $pharmacyChoice.responses '200') -and $null-ne(Get-Property $pharmacyChoice.responses '400') -and
      $null-ne(Get-Property $pharmacyChoice.responses '403') -and $null-ne(Get-Property $pharmacyChoice.responses '404') -and
      $null-ne(Get-Property $pharmacyChoice.responses '409') -and
      $pharmacyChoiceRequestJson-match'expectedVersion' -and $pharmacyChoiceRequestJson-match'directoryEntryId' -and
      $pharmacyChoiceRequestJson-match'patientChoiceConfirmed' -and $pharmacyChoiceRequestJson-match'syntheticDataConfirmed' -and
      $pharmacyChoiceRequestJson-notmatch'patientId|encounterId|appointmentId|requestId|medication|drug|prescription|claim')
    $prescriptionRead = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/prescription-preparation-draft' 'get'
    $prescriptionWrite = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/prescription-preparation-draft' 'put'
    $prescriptionRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $prescriptionWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $prescriptionRequestSchemaName = ($prescriptionRequestReference -split '/')[-1]
    $prescriptionRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $prescriptionRequestSchemaName
    $prescriptionRequestJson = $prescriptionRequestSchema | ConvertTo-Json -Depth 30 -Compress
    $prescriptionResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $prescriptionWrite.responses '200') 'content') 'application/json').schema '$ref'
    $prescriptionResponseSchemaName = ($prescriptionResponseReference -split '/')[-1]
    $prescriptionResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $prescriptionResponseSchemaName
    $prescriptionResponseJson = $prescriptionResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Prescription-preparation workspace is physician-scoped, read-only, typed, and carries no mutation identity' (
      (Has-Security $prescriptionRead 'AvenChartLocalStaffSession') -and -not (Has-Security $prescriptionRead 'AvenChartPatientPortalSession') -and
      (Has-Header $prescriptionRead 'X-AvenChart-Facility-Id') -and (Has-Header $prescriptionRead 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $prescriptionRead 'X-Idempotency-Key') -and $null-eq(Get-Property $prescriptionRead 'requestBody') -and
      $null-ne(Get-Property $prescriptionRead.responses '400') -and $null-ne(Get-Property $prescriptionRead.responses '403') -and
      $null-ne(Get-Property $prescriptionRead.responses '404'))
    Add-Check 'Prescription-preparation draft publishes physician-authored inputs, idempotency, bounded failures, and permanently false consequential flags' (
      (Has-Security $prescriptionWrite 'AvenChartLocalStaffSession') -and -not (Has-Security $prescriptionWrite 'AvenChartPatientPortalSession') -and
      (Has-Header $prescriptionWrite 'X-AvenChart-Facility-Id') -and (Has-Header $prescriptionWrite 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $prescriptionWrite 'X-Idempotency-Key') -and $null-ne(Get-Property $prescriptionWrite 'requestBody') -and
      $null-ne(Get-Property $prescriptionWrite.responses '200') -and $null-ne(Get-Property $prescriptionWrite.responses '400') -and
      $null-ne(Get-Property $prescriptionWrite.responses '403') -and $null-ne(Get-Property $prescriptionWrite.responses '404') -and
      $null-ne(Get-Property $prescriptionWrite.responses '409') -and
      $prescriptionRequestJson-match'expectedVersion' -and $prescriptionRequestJson-match'rxNormCode' -and
      $prescriptionRequestJson-match'doseAmount' -and $prescriptionRequestJson-match'frequency' -and
      $prescriptionRequestJson-match'directions' -and $prescriptionRequestJson-match'medicationListReviewed' -and
      $prescriptionRequestJson-match'allergyListReviewed' -and $prescriptionRequestJson-match'adequateEvaluationCompleted' -and
      $prescriptionRequestJson-match'syntheticDataConfirmed' -and
      $prescriptionRequestJson-notmatch'patientId|encounterId|appointmentId|requestId|signature|signed|transmit|claim' -and
      $prescriptionResponseJson-match'legalEffect' -and $prescriptionResponseJson-match'safetyChecked' -and
      $prescriptionResponseJson-match'signed' -and $prescriptionResponseJson-match'transmissionQueued' -and
      $prescriptionResponseJson-notmatch'patientId|encounterId|appointmentId|requestId|signatureId|claimId')
    $dispositionRead = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/safety-disposition-draft' 'get'
    $dispositionWrite = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/safety-disposition-draft' 'put'
    $dispositionRequestReference = Get-Property (Get-Property (Get-Property (Get-Property $dispositionWrite 'requestBody') 'content') 'application/json').schema '$ref'
    $dispositionRequestSchemaName = ($dispositionRequestReference -split '/')[-1]
    $dispositionRequestSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $dispositionRequestSchemaName
    $dispositionRequestJson = $dispositionRequestSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Safety-disposition workspace is physician-scoped, read-only, typed, and carries no mutation identity' (
      (Has-Security $dispositionRead 'AvenChartLocalStaffSession') -and -not (Has-Security $dispositionRead 'AvenChartPatientPortalSession') -and
      (Has-Header $dispositionRead 'X-AvenChart-Facility-Id') -and (Has-Header $dispositionRead 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $dispositionRead 'X-Idempotency-Key') -and $null-eq(Get-Property $dispositionRead 'requestBody') -and
      $null-ne(Get-Property $dispositionRead.responses '403') -and $null-ne(Get-Property $dispositionRead.responses '404'))
    Add-Check 'Safety-disposition draft publishes physician-scoped conditional facts, idempotency, bounded failures, and no finalization or downstream payload' (
      (Has-Security $dispositionWrite 'AvenChartLocalStaffSession') -and -not (Has-Security $dispositionWrite 'AvenChartPatientPortalSession') -and
      (Has-Header $dispositionWrite 'X-AvenChart-Facility-Id') -and (Has-Header $dispositionWrite 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $dispositionWrite 'X-Idempotency-Key') -and $null-ne(Get-Property $dispositionWrite 'requestBody') -and
      $null-ne(Get-Property $dispositionWrite.responses '200') -and $null-ne(Get-Property $dispositionWrite.responses '400') -and
      $null-ne(Get-Property $dispositionWrite.responses '403') -and $null-ne(Get-Property $dispositionWrite.responses '404') -and
      $null-ne(Get-Property $dispositionWrite.responses '409') -and
      $dispositionRequestJson-match'expectedVersion' -and $dispositionRequestJson-match'dispositionCode' -and
      $dispositionRequestJson-match'adequateEvaluationCompleted' -and $dispositionRequestJson-match'followUpOwner' -and
      $dispositionRequestJson-match'warningEscalationInstructions' -and $dispositionRequestJson-match'communicationMethod' -and
      $dispositionRequestJson-match'syntheticDataConfirmed' -and
      $dispositionRequestJson-notmatch'patientId|encounterId|appointmentId|requestId|signature|signed|finalized|delivered|medication|drug|prescription|claim')
    $completionReview = Get-Operation $document '/api/telehealth/v1/clinician/consultations/{consultationId}/completion-prerequisites' 'get'
    $completionResponseReference = Get-Property (Get-Property (Get-Property (Get-Property $completionReview.responses '200') 'content') 'application/json').schema '$ref'
    $completionResponseSchemaName = ($completionResponseReference -split '/')[-1]
    $completionResponseSchema = Get-Property (Get-Property (Get-Property $document 'components') 'schemas') $completionResponseSchemaName
    $completionResponseJson = $completionResponseSchema | ConvertTo-Json -Depth 30 -Compress
    Add-Check 'Completion-prerequisites review is physician-scoped, read-only, typed, minimized, and cannot advertise consequential actions' (
      (Has-Security $completionReview 'AvenChartLocalStaffSession') -and -not (Has-Security $completionReview 'AvenChartPatientPortalSession') -and
      (Has-Header $completionReview 'X-AvenChart-Facility-Id') -and (Has-Header $completionReview 'X-AvenChart-Purpose-Of-Use') -and
      -not (Has-Header $completionReview 'X-Idempotency-Key') -and $null-eq(Get-Property $completionReview 'requestBody') -and
      $null-ne(Get-Property $completionReview.responses '200') -and $null-ne(Get-Property $completionReview.responses '403') -and
      $null-ne(Get-Property $completionReview.responses '404') -and
      $completionResponseJson-match'documentation' -and $completionResponseJson-match'safetyDisposition' -and
      $completionResponseJson-match'productBlockers' -and $completionResponseJson-match'signingEnabled' -and
      $completionResponseJson-match'completionEnabled' -and $completionResponseJson-match'downstreamCreationEnabled' -and
      $completionResponseJson-notmatch'patientId|encounterId|appointmentId|requestId|subjective\"|objective\"|assessment\"|plan\"|followUpTimeframe\"|nextStepInstructions\"|warningEscalationInstructions\"|pharmacyName|address|ncpdp|npi')
    $authorize = Get-Operation $document '/api/telehealth/v1/admin/requests/{requestId}/authorize' 'post'
    Add-Check 'Staff mutation documents identity, facility, purpose, idempotency, and conflict outcomes' (
      (Has-Security $authorize 'AvenChartLocalStaffSession') -and (Has-Security $authorize 'AvenChartOidcBearer') -and
      (Has-Header $authorize 'X-AvenChart-Facility-Id') -and (Has-Header $authorize 'X-AvenChart-Purpose-Of-Use') -and
      (Has-Header $authorize 'X-Idempotency-Key') -and $null -ne (Get-Property $authorize.responses '409'))
    $context = Get-Operation $document '/api/telehealth/v1/context' 'get'
    $contextSecurity = Get-Property $context 'security'
    Add-Check 'Public context has no protected security requirement and exposes only the public projection' ($null -eq $contextSecurity -or @($contextSecurity).Count -eq 0)
}
catch { Add-Check 'Telehealth OpenAPI contract execution' $false $_.Exception.Message }
    finally { $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decisions=@('TH-DEC-0003','TH-DEC-0005','TH-DEC-0006','TH-DEC-0007','TH-DEC-0008','TH-DEC-0009','TH-DEC-0010','TH-DEC-0011','TH-DEC-0012','TH-DEC-0013','TH-DEC-0014','TH-DEC-0015','TH-DEC-0016','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019','TH-DEC-0020','TH-DEC-0021','TH-DEC-0022','TH-DEC-0023','TH-DEC-0024','TH-DEC-0025','TH-DEC-0026','TH-DEC-0027','TH-DEC-0028','TH-DEC-0029','TH-DEC-0030','TH-DEC-0031','TH-DEC-0032','TH-DEC-0033','TH-DEC-0034','TH-DEC-0035','TH-DEC-0036','TH-DEC-0037','TH-DEC-0038','TH-DEC-0039','TH-DEC-0040','TH-DEC-0041','TH-DEC-0042','TH-DEC-0043','TH-DEC-0044','TH-DEC-0045','TH-DEC-0046','TH-DEC-0047','TH-DEC-0048');checks=$checks};$result|ConvertTo-Json -Depth 10|Set-Content $resultPath -Encoding utf8;$result|ConvertTo-Json -Depth 10 }
if(-not $passed){exit 1}
