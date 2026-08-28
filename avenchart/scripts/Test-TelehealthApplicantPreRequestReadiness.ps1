# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5001',
    [ValidatePattern('^[a-z][a-z0-9_]{2,62}$')]
    [string]$DatabaseName = 'avenchart'
)

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth applicant pre-request-readiness proof is local-only.'
}

# The sealed Sprint 33 proof leaves one applicant in each launch state at the
# immutable synthetic clinical-information-summary confirmation boundary.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantClinicalInformationSummary.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-pre-request-readiness.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-PreRequestReadinessCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-PreRequestReadinessBody([int]$Version,[string]$Fingerprint) {
    @{
        expectedVersion=$Version
        preRequestReadinessSnapshotFingerprint=$Fingerprint
        priorSectionsReviewedAcknowledged=$true
        outstandingStepsRemainAcknowledged=$true
        noRequestOrQueueCreatedAcknowledged=$true
        correctionRequiresSeparateWorkflowAcknowledged=$true
    }
}
function Get-PreRequestReadiness([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/pre-request-readiness" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Confirm-PreRequestReadiness([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/pre-request-readiness" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 10)
}
function Get-PreRequestReadinessCounts {
    (Invoke-Scalar @"
select json_build_object(
  'acknowledgments',(select count(*) from telehealth_applicant_pre_request_readiness_acknowledgments),
  'registrations',(select count(*) from telehealth_applicant_registration_details_confirmations),
  'insuranceHandoffs',(select count(*) from telehealth_applicant_insurance_handoff_confirmations),
  'communicationReceipts',(select count(*) from telehealth_applicant_communication_access_readiness),
  'deviceReceipts',(select count(*) from telehealth_applicant_device_preparations),
  'inventories',(select count(*) from telehealth_applicant_clinical_information_inventories),
  'summaries',(select count(*) from telehealth_applicant_clinical_information_summary_confirmations),
  'patients',(select count(*) from patients),
  'insuranceRecords',(select count(*) from insurance_records),
  'problems',(select count(*) from problems),
  'medications',(select count(*) from medications),
  'prescriptions',(select count(*) from prescriptions),
  'allergies',(select count(*) from allergies),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'intakeSnapshots',(select count(*) from telehealth_intake_snapshots),
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims));
"@) | ConvertFrom-Json
}
function Test-PreRequestReadinessMutationRejected([string]$Sql) {
    docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -v ON_ERROR_STOP=1 -c $Sql *> $null
    return $LASTEXITCODE -ne 0
}

try {
    $gaId=[string]$success.Created.applicantId
    $caId=[string]$race.Created.applicantId
    $flId=[string]$fl.Created.applicantId
    $before=Get-PreRequestReadinessCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/pre-request-readiness" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaContext=$gaWeb.Content|ConvertFrom-Json
    $gaJson=$gaContext|ConvertTo-Json -Depth 12 -Compress
    Add-PreRequestReadinessCheck 'Applicant-owned read returns exactly five coarse server-derived receipt sections' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaContext.applicantStatus-eq'SyntheticClinicalInformationSummaryConfirmed' -and
        [int]$gaContext.applicantVersion-eq 22 -and
        -not$gaContext.readinessAcknowledged -and
        @($gaContext.sections).Count-eq 5 -and
        (($gaContext.sections.sectionKey-join',')-eq'Registration,Insurance,CommunicationAccess,DevicePreparation,ClinicalInformation') -and
        (($gaContext.sections.receiptState-join',')-eq'ReceiptRecorded,ReceiptRecorded,PreferencesRecorded,ClientReportedPreparationRecorded,PatientReportedSummaryConfirmed') -and
        ([string]$gaContext.preRequestReadinessSnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaContext.policyKey-eq'SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS' -and
        $gaJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|legalName|dateOfBirth|email|phone|address|memberId|payer|catalogKey|displayName|diagnosis|symptom|dose|reaction|note|attachment|freeText)"\s*:|TH-PAT-|4045550199')

    $caContext=Get-PreRequestReadiness $race
    $flContext=Get-PreRequestReadiness $fl
    Add-PreRequestReadinessCheck 'Server route priority preserves additional-information ahead of requested assisted support' (
        $gaContext.overallRoute-eq'AdditionalClinicalInformationRequired' -and
        $caContext.overallRoute-eq'AssistedPreRequestSupportRequired' -and
        $flContext.overallRoute-eq'AssistedPreRequestSupportRequired')

    $gaBody=New-PreRequestReadinessBody 22 ([string]$gaContext.preRequestReadinessSnapshotFingerprint)
    Add-PreRequestReadinessCheck 'Anonymous, wrong-key, missing-acknowledgment, stale-version, and stale-snapshot commands fail before persistence' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/pre-request-readiness")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/pre-request-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/pre-request-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp34-ack')} (Merge-Map $gaBody @{outstandingStepsRemainAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/pre-request-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp34-version')} (Merge-Map $gaBody @{expectedVersion=21}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/pre-request-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp34-snapshot')} (Merge-Map $gaBody @{preRequestReadinessSnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_pre_request_readiness_acknowledgments where applicant_id='$gaId';")-eq 0)

    $flPatientId=Invoke-Scalar "select canonical_patient_id from telehealth_applicant_synthetic_promotions where applicant_id='$flId';"
    Invoke-Scalar "update patients set portal_enabled=true where canonical_id='$flPatientId';" | Out-Null
    try {
        $portalDriftStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/pre-request-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    }
    finally {
        Invoke-Scalar "update patients set portal_enabled=false where canonical_id='$flPatientId';" | Out-Null
    }
    Add-PreRequestReadinessCheck 'Current patient-shell drift fails closed and restoration preserves the source snapshot' (
        $portalDriftStatus-eq 409 -and
        (Get-PreRequestReadiness $fl).overallRoute-eq'AssistedPreRequestSupportRequired')

    $gaKey=New-Key 'sp34-ga'
    $gaAcknowledged=Confirm-PreRequestReadiness $success $gaBody $gaKey
    $gaReplay=Confirm-PreRequestReadiness $success $gaBody $gaKey
    $gaAcknowledgedJson=$gaAcknowledged|ConvertTo-Json -Depth 12 -Compress
    $falseFlags=@(
        'identityAssuranceEstablished','coverageGuaranteed','renderingClinicianNetworkVerified',
        'interpreterOrAccommodationArranged','technologyReady','clinicalInformationReconciled',
        'clinicalIntakeCompleted','clinicalEligibilityEstablished','legalConsentEstablished',
        'staffReviewCreated','clinicianReviewCreated','practiceAccepted','patientRecordChanged',
        'requestCreated','queueEntered','appointmentCreated','encounterCreated','careAuthorized',
        'prescribingEnabled','billingEnabled','claimCreated','integrationEnabled','externalCallPerformed')
    Add-PreRequestReadinessCheck 'Georgia acknowledgment is immutable, no-edit, and creates no operational or clinical authority' (
        $gaAcknowledged.applicantStatus-eq'SyntheticPreRequestReadinessAcknowledged' -and
        [int]$gaAcknowledged.applicantVersion-eq 23 -and
        $gaAcknowledged.readinessAcknowledged -and
        $gaAcknowledged.overallRoute-eq'AdditionalClinicalInformationRequired' -and
        $gaAcknowledged.priorSectionsReviewedAcknowledged -and
        $gaAcknowledged.outstandingStepsRemainAcknowledged -and
        $gaAcknowledged.noRequestOrQueueCreatedAcknowledged -and
        $gaAcknowledged.correctionRequiresSeparateWorkflowAcknowledged -and
        @($falseFlags|Where-Object{$gaAcknowledged.$_}).Count-eq 0 -and
        $gaReplay.acknowledgedAt-eq$gaAcknowledged.acknowledgedAt -and
        $gaAcknowledgedJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|legalName|dateOfBirth|email|phone|address|memberId|payer|catalogKey|displayName|diagnosis|symptom|dose|reaction|note|attachment|freeText)"\s*:|TH-PAT-|4045550199')

    Add-PreRequestReadinessCheck 'Changed idempotency reuse and a second semantic acknowledgment fail closed' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/pre-request-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{preRequestReadinessSnapshotFingerprint=('f'*64)}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/pre-request-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp34-second')} (Merge-Map $gaBody @{expectedVersion=23}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_pre_request_readiness_acknowledgments where applicant_id='$gaId';")-eq 1)

    $caBody=New-PreRequestReadinessBody 22 ([string]$caContext.preRequestReadinessSnapshotFingerprint)
    $caJsonBody=$caBody|ConvertTo-Json -Depth 10 -Compress
    $caRaceKey=New-Key 'sp34-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/pre-request-readiness" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    $caReload=Get-PreRequestReadiness $race
    $caReplay=Confirm-PreRequestReadiness $race $caBody $caRaceKey
    Add-PreRequestReadinessCheck 'Eight concurrent California attempts and unchanged retry converge on one assisted-support acknowledgment and event' (
        @($statuses|Where-Object{$_-eq 200}).Count-ge 1 -and
        @($statuses|Where-Object{$_-notin @(200,409)}).Count-eq 0 -and
        $caReplay.acknowledgedAt-eq$caReload.acknowledgedAt -and
        $caReload.overallRoute-eq'AssistedPreRequestSupportRequired' -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_pre_request_readiness_acknowledgments where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-pre-request-readiness-acknowledged';")-eq 1) @{statuses=$statuses}

    $flBody=New-PreRequestReadinessBody 22 ([string]$flContext.preRequestReadinessSnapshotFingerprint)
    $flAcknowledged=Confirm-PreRequestReadiness $fl $flBody (New-Key 'sp34-fl')
    Add-PreRequestReadinessCheck 'Florida requested language support remains unarranged with every downstream consequence false' (
        $flAcknowledged.overallRoute-eq'AssistedPreRequestSupportRequired' -and
        -not$flAcknowledged.interpreterOrAccommodationArranged -and
        @($falseFlags|Where-Object{$flAcknowledged.$_}).Count-eq 0)

    $gaReload=Get-PreRequestReadiness $success
    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    Add-PreRequestReadinessCheck 'Applicant resume and readiness reload stay coarse, private, and consequence-free' (
        $gaResume.status-eq'SyntheticPreRequestReadinessAcknowledged' -and
        $gaResume.canonicalPatientCreated -and $gaReload.readinessAcknowledged -and
        @($falseFlags|Where-Object{$gaReload.$_}).Count-eq 0 -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'"(canonicalPatientId|legacyPid|pubpid|legalName|dateOfBirth|email|phone|address|memberId|payer|catalogKey|displayName|diagnosis|symptom|dose|reaction|note|attachment|freeText)"\s*:|TH-PAT-'))

    $appendOnlyUpdate=Test-PreRequestReadinessMutationRejected "update telehealth_applicant_pre_request_readiness_acknowledgments set overall_route='PendingPracticePreRequestReview' where applicant_id='$gaId';"
    $appendOnlyDelete=Test-PreRequestReadinessMutationRejected "delete from telehealth_applicant_pre_request_readiness_acknowledgments where applicant_id='$gaId';"
    Add-PreRequestReadinessCheck 'Readiness acknowledgments and applicant events are append-only' (
        $appendOnlyUpdate -and $appendOnlyDelete -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_pre_request_readiness_acknowledgments where applicant_id in ('$gaId','$caId','$flId');")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id in ('$gaId','$caId','$flId') and action='prospective-pre-request-readiness-acknowledged';")-eq 3)

    $persisted=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',resulting_applicant_status,'version',resulting_applicant_version,
  'policy',policy_key,'policyVersion',policy_version,'evidenceType',evidence_type,
  'summaryRoute',clinical_information_summary_route,'overallRoute',overall_route,
  'allAcknowledged',prior_sections_reviewed_acknowledged
    and outstanding_steps_remain_acknowledged and no_request_or_queue_created_acknowledged
    and correction_requires_separate_workflow_acknowledged,
  'allFalse',not identity_assurance_established and not coverage_guaranteed
    and not rendering_clinician_network_verified and not interpreter_or_accommodation_arranged
    and not technology_ready and not clinical_information_reconciled
    and not clinical_intake_completed and not clinical_eligibility_established
    and not legal_consent_established and not staff_review_created and not clinician_review_created
    and not practice_accepted and not patient_record_changed and not request_created
    and not queue_entered and not appointment_created and not encounter_created
    and not care_authorized and not prescribing_enabled and not billing_enabled
    and not claim_created and not integration_enabled and not external_call_performed)
  order by overall_route)
from telehealth_applicant_pre_request_readiness_acknowledgments
where applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    $actualRoutes=(@($persisted.overallRoute)|Sort-Object)-join','
    $expectedRoutes=(@('AdditionalClinicalInformationRequired','AssistedPreRequestSupportRequired','AssistedPreRequestSupportRequired')|Sort-Object)-join','
    Add-PreRequestReadinessCheck 'Persisted receipts retain exact source provenance with every acknowledgment true and consequence false' (
        @($persisted).Count-eq 3 -and
        @($persisted|Where-Object{-not$_.allAcknowledged-or-not$_.allFalse}).Count-eq 0 -and
        $actualRoutes-eq$expectedRoutes) $persisted

    $after=Get-PreRequestReadinessCounts
    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    $unchangedFields=@('registrations','insuranceHandoffs','communicationReceipts','deviceReceipts','inventories','summaries','patients','insuranceRecords','problems','medications','prescriptions','allergies','requests','queueEntries','intakeSnapshots','appointments','encounters','claims')
    Add-PreRequestReadinessCheck 'Three acknowledgments change no source, canonical, financial, request, queue, appointment, encounter, or care record' (
        [int]$after.acknowledgments-[int]$before.acknowledgments-eq 3 -and
        @($unchangedFields|Where-Object{[int]$after.$_-ne[int]$before.$_}).Count-eq 0 -and
        ($patientHashesBefore|ConvertTo-Json -Compress)-eq($patientHashesAfter|ConvertTo-Json -Compress))

    $prohibitedColumns=[int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public'
  and table_name='telehealth_applicant_pre_request_readiness_acknowledgments'
  and column_name in ('diagnosis','symptom','dose','directions','reaction','criticality','clinical_status',
                      'verification_status','severity','onset','occurrence','clinical_date','note',
                      'attachment','free_text','legal_name','date_of_birth','email','phone','address','member_id','payer');
"@)
    Add-PreRequestReadinessCheck 'Schema has no prohibited clinical-detail, identity, contact, payer, or narrative columns and all applicants stop before request creation' (
        $prohibitedColumns-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id in ('$gaId','$caId','$flId') and status='SyntheticPreRequestReadinessAcknowledged' and version=23;")-eq 3 -and
        [int]$after.requests-eq[int]$before.requests)
}
catch {
    $passed=$false
    Add-PreRequestReadinessCheck 'Applicant pre-request-readiness proof execution' $false ("$($_.Exception.Message) | $($_.ScriptStackTrace)")
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0037'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant pre-request-readiness proof failed. See $resultPath"}
Write-Host "Applicant pre-request-readiness proof passed ($($checks.Count) checks). Artifact: $resultPath"
