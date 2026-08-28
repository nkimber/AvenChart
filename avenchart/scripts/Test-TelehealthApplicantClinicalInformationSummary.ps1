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
    throw 'Telehealth applicant clinical-information-summary proof is local-only.'
}

# The sealed Sprint 32 chain leaves Georgia, California, and Florida applicants
# at the immutable synthetic health-history-information receipt gate.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantHealthHistoryInformation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-clinical-information-summary.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-ClinicalInformationSummaryCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-ClinicalInformationSummaryBody([int]$Version,[string]$Fingerprint) {
    @{
        expectedVersion=$Version
        clinicalInformationSummarySnapshotFingerprint=$Fingerprint
        patientReportedMayBeIncompleteAcknowledged=$true
        notClinicallyVerifiedOrReconciledAcknowledged=$true
        noIntakeCompletionOrEligibilityAcknowledged=$true
        correctionRequiresSeparateWorkflowAcknowledged=$true
    }
}
function Get-ClinicalInformationSummary([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/clinical-information-summary" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Confirm-ClinicalInformationSummary([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/clinical-information-summary" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 10)
}
function Get-ClinicalInformationSummaryCounts {
    (Invoke-Scalar @"
select json_build_object(
  'confirmations',(select count(*) from telehealth_applicant_clinical_information_summary_confirmations),
  'inventories',(select count(*) from telehealth_applicant_clinical_information_inventories),
  'medicationReceipts',(select count(*) from telehealth_applicant_medication_information_receipts),
  'medicationItems',(select count(*) from telehealth_applicant_reported_medication_items),
  'allergyReceipts',(select count(*) from telehealth_applicant_allergy_information_receipts),
  'allergyItems',(select count(*) from telehealth_applicant_reported_allergy_items),
  'historyReceipts',(select count(*) from telehealth_applicant_health_history_information_receipts),
  'historyTopics',(select count(*) from telehealth_applicant_reported_health_history_topics),
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
function Test-SummaryMutationRejected([string]$Sql) {
    try {
        docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -v ON_ERROR_STOP=1 -c $Sql *> $null
        return $LASTEXITCODE -ne 0
    }
    finally { }
}

try {
    $gaId=[string]$success.Created.applicantId
    $caId=[string]$race.Created.applicantId
    $flId=[string]$fl.Created.applicantId
    $before=Get-ClinicalInformationSummaryCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/clinical-information-summary" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaContext=$gaWeb.Content|ConvertFrom-Json
    $gaJson=$gaContext|ConvertTo-Json -Depth 12 -Compress
    Add-ClinicalInformationSummaryCheck 'Applicant-owned read returns only three server-derived coarse summary categories' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaContext.applicantStatus-eq'SyntheticHealthHistoryInformationRecorded' -and
        [int]$gaContext.applicantVersion-eq 21 -and
        -not$gaContext.summaryConfirmed -and
        @($gaContext.categories).Count-eq 3 -and
        (($gaContext.categories.categoryKey-join',')-eq'Medications,AllergiesOrIntolerances,OtherHealthHistory') -and
        $gaContext.categories[0].inventoryStatus-eq'ItemsToReview' -and
        [int]$gaContext.categories[0].selectedItemCount-eq 2 -and
        $gaContext.categories[0].additionalOrUnlistedItemsReported -and
        $gaContext.categories[1].inventoryStatus-eq'ItemsToReview' -and
        [int]$gaContext.categories[1].selectedItemCount-eq 2 -and
        $gaContext.categories[2].inventoryStatus-eq'ItemsToReview' -and
        [int]$gaContext.categories[2].selectedItemCount-eq 2 -and
        ([string]$gaContext.clinicalInformationSummarySnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaContext.policyKey-eq'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY' -and
        $gaJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|legalName|dateOfBirth|email|phone|address|memberId|payer|catalogKey|displayName|diagnosis|symptom|dose|reaction|note|attachment|freeText)"\s*:|TH-PAT-|4045550199')

    $caContext=Get-ClinicalInformationSummary $race
    $flContext=Get-ClinicalInformationSummary $fl
    Add-ClinicalInformationSummaryCheck 'Server route priority preserves additional collection, assisted review, and provisional-none branches' (
        $gaContext.summaryRoute-eq'AdditionalClinicalInformationCollectionRequired' -and
        $caContext.summaryRoute-eq'AssistedClinicalInformationReviewRequired' -and
        $flContext.summaryRoute-eq'PendingClinicianReconciliationOfPatientReportedNone')

    $gaBody=New-ClinicalInformationSummaryBody 21 ([string]$gaContext.clinicalInformationSummarySnapshotFingerprint)
    Add-ClinicalInformationSummaryCheck 'Anonymous, wrong-key, missing-acknowledgment, stale-version, and stale-snapshot commands fail before persistence' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/clinical-information-summary")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/clinical-information-summary" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-summary" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp33-ack')} (Merge-Map $gaBody @{notClinicallyVerifiedOrReconciledAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-summary" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp33-version')} (Merge-Map $gaBody @{expectedVersion=20}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-summary" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp33-snapshot')} (Merge-Map $gaBody @{clinicalInformationSummarySnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_summary_confirmations where applicant_id='$gaId';")-eq 0)

    $flPatientId=Invoke-Scalar "select canonical_patient_id from telehealth_applicant_synthetic_promotions where applicant_id='$flId';"
    Invoke-Scalar "update patients set portal_enabled=true where canonical_id='$flPatientId';" | Out-Null
    try {
        $portalDriftStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/clinical-information-summary" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    }
    finally {
        Invoke-Scalar "update patients set portal_enabled=false where canonical_id='$flPatientId';" | Out-Null
    }
    Add-ClinicalInformationSummaryCheck 'Current portal or patient-shell drift fails closed and restoration preserves the source summary' (
        $portalDriftStatus-eq 409 -and
        (Get-ClinicalInformationSummary $fl).summaryRoute-eq'PendingClinicianReconciliationOfPatientReportedNone')

    $gaKey=New-Key 'sp33-ga'
    $gaConfirmed=Confirm-ClinicalInformationSummary $success $gaBody $gaKey
    $gaReplay=Confirm-ClinicalInformationSummary $success $gaBody $gaKey
    $gaConfirmedJson=$gaConfirmed|ConvertTo-Json -Depth 12 -Compress
    Add-ClinicalInformationSummaryCheck 'Georgia confirmation is immutable, no-edit, and creates no clinical or operational authority' (
        $gaConfirmed.applicantStatus-eq'SyntheticClinicalInformationSummaryConfirmed' -and
        [int]$gaConfirmed.applicantVersion-eq 22 -and
        $gaConfirmed.summaryConfirmed -and
        $gaConfirmed.summaryRoute-eq'AdditionalClinicalInformationCollectionRequired' -and
        $gaConfirmed.patientReportedMayBeIncompleteAcknowledged -and
        $gaConfirmed.notClinicallyVerifiedOrReconciledAcknowledged -and
        $gaConfirmed.noIntakeCompletionOrEligibilityAcknowledged -and
        $gaConfirmed.correctionRequiresSeparateWorkflowAcknowledged -and
        -not$gaConfirmed.questionnaireResponseCreated -and
        -not$gaConfirmed.medicationListReconciled -and -not$gaConfirmed.allergyListReconciled -and
        -not$gaConfirmed.healthHistoryReconciled -and -not$gaConfirmed.confirmedNegativeEstablished -and
        -not$gaConfirmed.clinicianReviewCreated -and -not$gaConfirmed.clinicalIntakeCompleted -and
        -not$gaConfirmed.clinicalEligibilityEstablished -and -not$gaConfirmed.clinicalTriageChanged -and
        -not$gaConfirmed.patientRecordChanged -and -not$gaConfirmed.practiceAccepted -and
        -not$gaConfirmed.requestCreated -and -not$gaConfirmed.queueEntered -and
        -not$gaConfirmed.careAuthorized -and -not$gaConfirmed.prescribingEnabled -and
        $gaReplay.confirmedAt-eq$gaConfirmed.confirmedAt -and
        $gaConfirmedJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|legalName|dateOfBirth|email|phone|address|memberId|payer|catalogKey|displayName|diagnosis|symptom|dose|reaction|note|attachment|freeText)"\s*:|TH-PAT-|4045550199')

    Add-ClinicalInformationSummaryCheck 'Changed idempotency reuse and a second semantic confirmation fail closed' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-summary" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{clinicalInformationSummarySnapshotFingerprint=('f'*64)}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-summary" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp33-second')} (Merge-Map $gaBody @{expectedVersion=22}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_summary_confirmations where applicant_id='$gaId';")-eq 1)

    $caBody=New-ClinicalInformationSummaryBody 21 ([string]$caContext.clinicalInformationSummarySnapshotFingerprint)
    $caJsonBody=$caBody|ConvertTo-Json -Depth 10 -Compress
    $caRaceKey=New-Key 'sp33-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/clinical-information-summary" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    $caReload=Get-ClinicalInformationSummary $race
    $caReplay=Confirm-ClinicalInformationSummary $race $caBody $caRaceKey
    Add-ClinicalInformationSummaryCheck 'Eight concurrent California attempts and unchanged retry converge on one assisted-review confirmation and event' (
        @($statuses|Where-Object{$_-eq 200}).Count-ge 1 -and
        @($statuses|Where-Object{$_-notin @(200,409)}).Count-eq 0 -and
        $caReplay.confirmedAt-eq$caReload.confirmedAt -and
        $caReload.summaryRoute-eq'AssistedClinicalInformationReviewRequired' -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_summary_confirmations where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-clinical-information-summary-confirmed';")-eq 1) @{statuses=$statuses}

    $flBody=New-ClinicalInformationSummaryBody 21 ([string]$flContext.clinicalInformationSummarySnapshotFingerprint)
    $flConfirmed=Confirm-ClinicalInformationSummary $fl $flBody (New-Key 'sp33-fl')
    Add-ClinicalInformationSummaryCheck 'Florida patient-reports-none summary remains provisional and unreconciled' (
        $flConfirmed.summaryRoute-eq'PendingClinicianReconciliationOfPatientReportedNone' -and
        -not$flConfirmed.confirmedNegativeEstablished -and
        -not$flConfirmed.medicationListReconciled -and -not$flConfirmed.allergyListReconciled -and
        -not$flConfirmed.healthHistoryReconciled -and -not$flConfirmed.clinicalIntakeCompleted)

    $gaReload=Get-ClinicalInformationSummary $success
    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    Add-ClinicalInformationSummaryCheck 'Applicant resume and summary reload stay coarse, private, and consequence-free' (
        $gaResume.status-eq'SyntheticClinicalInformationSummaryConfirmed' -and
        $gaResume.canonicalPatientCreated -and $gaReload.summaryConfirmed -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'"(canonicalPatientId|legacyPid|pubpid|legalName|dateOfBirth|email|phone|address|memberId|payer|catalogKey|displayName|diagnosis|symptom|dose|reaction|note|attachment|freeText)"\s*:|TH-PAT-'))

    $appendOnlyUpdate=Test-SummaryMutationRejected "update telehealth_applicant_clinical_information_summary_confirmations set summary_route='ClinicianClinicalInformationReviewRequired' where applicant_id='$gaId';"
    $appendOnlyDelete=Test-SummaryMutationRejected "delete from telehealth_applicant_clinical_information_summary_confirmations where applicant_id='$gaId';"
    Add-ClinicalInformationSummaryCheck 'Summary confirmations and applicant events are append-only' (
        $appendOnlyUpdate -and $appendOnlyDelete -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_summary_confirmations where applicant_id in ('$gaId','$caId','$flId');")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id in ('$gaId','$caId','$flId') and action='prospective-clinical-information-summary-confirmed';")-eq 3)

    $persisted=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',resulting_applicant_status,'version',resulting_applicant_version,
  'policy',policy_key,'policyVersion',policy_version,'evidenceType',evidence_type,
  'medicationsStatus',medications_status,'allergiesStatus',allergies_or_intolerances_status,
  'historyStatus',other_health_history_status,'medicationCount',medication_item_count,
  'allergyCount',allergy_item_count,'historyCount',health_history_topic_count,
  'summaryRoute',summary_route,'allFalse',
    not questionnaire_response_created and not medication_list_reconciled
    and not allergy_list_reconciled and not health_history_reconciled
    and not confirmed_negative_established and not clinician_review_created
    and not clinical_intake_completed and not clinical_eligibility_established
    and not clinical_triage_changed and not patient_record_changed
    and not practice_accepted and not request_created and not queue_entered
    and not care_authorized and not prescribing_enabled)
  order by summary_route)
from telehealth_applicant_clinical_information_summary_confirmations
where applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    $actualSummaryRoutes=(@($persisted.summaryRoute)|Sort-Object)-join','
    $expectedSummaryRoutes=(@(
        'AdditionalClinicalInformationCollectionRequired',
        'AssistedClinicalInformationReviewRequired',
        'PendingClinicianReconciliationOfPatientReportedNone'
    )|Sort-Object)-join','
    Add-ClinicalInformationSummaryCheck 'Persisted receipts retain exact source summary provenance and all consequence flags false' (
        @($persisted).Count-eq 3 -and
        @($persisted|Where-Object{-not$_.allFalse}).Count-eq 0 -and
        $actualSummaryRoutes-eq$expectedSummaryRoutes) $persisted

    $after=Get-ClinicalInformationSummaryCounts
    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    Add-ClinicalInformationSummaryCheck 'Three confirmations change no source, canonical clinical, patient, financial, request, queue, or care record' (
        [int]$after.confirmations-[int]$before.confirmations-eq 3 -and
        [int]$after.inventories-eq[int]$before.inventories -and
        [int]$after.medicationReceipts-eq[int]$before.medicationReceipts -and
        [int]$after.medicationItems-eq[int]$before.medicationItems -and
        [int]$after.allergyReceipts-eq[int]$before.allergyReceipts -and
        [int]$after.allergyItems-eq[int]$before.allergyItems -and
        [int]$after.historyReceipts-eq[int]$before.historyReceipts -and
        [int]$after.historyTopics-eq[int]$before.historyTopics -and
        [int]$after.patients-eq[int]$before.patients -and
        [int]$after.insuranceRecords-eq[int]$before.insuranceRecords -and
        [int]$after.problems-eq[int]$before.problems -and
        [int]$after.medications-eq[int]$before.medications -and
        [int]$after.prescriptions-eq[int]$before.prescriptions -and
        [int]$after.allergies-eq[int]$before.allergies -and
        [int]$after.requests-eq[int]$before.requests -and
        [int]$after.queueEntries-eq[int]$before.queueEntries -and
        [int]$after.intakeSnapshots-eq[int]$before.intakeSnapshots -and
        [int]$after.appointments-eq[int]$before.appointments -and
        [int]$after.encounters-eq[int]$before.encounters -and
        [int]$after.claims-eq[int]$before.claims -and
        ($patientHashesBefore|ConvertTo-Json -Compress)-eq($patientHashesAfter|ConvertTo-Json -Compress))

    $prohibitedColumns=[int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public'
  and table_name='telehealth_applicant_clinical_information_summary_confirmations'
  and column_name in ('diagnosis','symptom','dose','directions','reaction','criticality','clinical_status',
                      'verification_status','severity','onset','occurrence','clinical_date','note',
                      'attachment','free_text','legal_name','date_of_birth','email','phone','address','member_id','payer');
"@)
    Add-ClinicalInformationSummaryCheck 'Schema has no prohibited clinical-detail, identity, contact, payer, or narrative columns and all applicants stop at summary confirmation' (
        $prohibitedColumns-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id in ('$gaId','$caId','$flId') and status='SyntheticClinicalInformationSummaryConfirmed' and version=22;")-eq 3)
}
catch {
    $passed=$false
    Add-ClinicalInformationSummaryCheck 'Applicant clinical-information-summary proof execution' $false ("$($_.Exception.Message) | $($_.ScriptStackTrace)")
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0036'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant clinical-information-summary proof failed. See $resultPath"}
Write-Host "Applicant clinical-information-summary proof passed ($($checks.Count) checks). Artifact: $resultPath"
