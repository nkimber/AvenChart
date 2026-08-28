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
    throw 'Telehealth applicant clinical-information inventory proof is local-only.'
}

# Reuse the sealed Sprint 28 chain. It leaves one Georgia, one California, and
# one Florida applicant at the synthetic device-preparation gate.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantDevicePreparation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-clinical-information-inventory.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-ClinicalInventoryCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-ClinicalInventoryBody(
    [int]$Version,
    [string]$Fingerprint,
    [string]$Medications='PatientReportsNone',
    [string]$Allergies='PatientReportsNone',
    [string]$History='PatientReportsNone') {
    @{
        expectedVersion=$Version
        inventorySnapshotFingerprint=$Fingerprint
        medicationsStatus=$Medications
        allergiesOrIntolerancesStatus=$Allergies
        otherHealthHistoryStatus=$History
        patientReportedMayBeIncompleteAcknowledged=$true
        noClinicalDetailsCapturedAcknowledged=$true
        clinicianReconciliationRequiredAcknowledged=$true
    }
}
function Get-ClinicalInventory([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/clinical-information-inventory" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Invoke-ClinicalInventory([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/clinical-information-inventory" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 8)
}
function Get-ClinicalInventoryCounts {
    (Invoke-Scalar @"
select json_build_object(
  'inventories',(select count(*) from telehealth_applicant_clinical_information_inventories),
  'patients',(select count(*) from patients),
  'medications',(select count(*) from medications),
  'insuranceRecords',(select count(*) from insurance_records),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'intakeSnapshots',(select count(*) from telehealth_intake_snapshots),
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims),
  'prescriptions',(select count(*) from prescriptions));
"@)|ConvertFrom-Json
}
function Test-ClinicalInventoryMutationRejected([string]$Sql) {
    Push-Location $solutionRoot
    try {
        docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName `
            -v ON_ERROR_STOP=1 -c $Sql *> $null
        return $LASTEXITCODE -ne 0
    }
    finally { Pop-Location }
}

try {
    $gaId=[string]$success.Created.applicantId
    $caId=[string]$race.Created.applicantId
    $flId=[string]$fl.Created.applicantId
    $before=Get-ClinicalInventoryCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/clinical-information-inventory" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaContext=$gaWeb.Content|ConvertFrom-Json
    $gaJson=$gaContext|ConvertTo-Json -Depth 12 -Compress
    Add-ClinicalInventoryCheck 'Applicant-owned read returns only the three-category policy and no clinical details' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaContext.applicantStatus-eq'SyntheticDevicePreparationRecorded' -and
        [int]$gaContext.applicantVersion-eq 17 -and
        @($gaContext.supportedCategoryStatuses).Count-eq 3 -and
        $gaContext.supportedCategoryStatuses[0]-eq'PatientReportsNone' -and
        $gaContext.supportedCategoryStatuses[1]-eq'ItemsToReview' -and
        $gaContext.supportedCategoryStatuses[2]-eq'Unsure' -and
        -not $gaContext.inventoryRecorded -and $null-eq$gaContext.medicationsStatus -and
        $null-eq$gaContext.allergiesOrIntolerancesStatus -and
        $null-eq$gaContext.otherHealthHistoryStatus -and $null-eq$gaContext.reviewRoute -and
        ([string]$gaContext.inventorySnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaContext.policyKey-eq'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY' -and
        $gaJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|medicationName|substance|reaction|dose|diagnosis|symptom|procedure|narrative|clinicalDate|clinicalIdentifier|freeText)"\s*:|TH-PAT-|4045550199')

    $gaBody=New-ClinicalInventoryBody 17 ([string]$gaContext.inventorySnapshotFingerprint) 'ItemsToReview' 'ItemsToReview' 'ItemsToReview'
    Add-ClinicalInventoryCheck 'Anonymous, wrong-key, detailed-value, missing-acknowledgment, stale-version, and stale-context commands fail before persistence' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/clinical-information-inventory")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp29-detail')} (Merge-Map $gaBody @{medicationsStatus='Aspirin'}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp29-ack')} (Merge-Map $gaBody @{noClinicalDetailsCapturedAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp29-stale')} (Merge-Map $gaBody @{expectedVersion=16}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp29-fingerprint')} (Merge-Map $gaBody @{inventorySnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_inventories where applicant_id='$gaId';")-eq 0)

    $flPatient=(Invoke-Scalar @"
select json_build_object('canonicalId',p.canonical_id,'pid',p.legacy_pid)
from patients p join telehealth_applicant_synthetic_promotions promotion
  on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id='$flId';
"@)|ConvertFrom-Json
    $temporaryInsuranceId="INS-SP29-$([Guid]::NewGuid().ToString('N'))"
    Invoke-Scalar "insert into insurance_records(id,patient_id,pid,type,provider,plan_name,policy_number) values('$temporaryInsuranceId','$($flPatient.canonicalId)',$($flPatient.pid),'primary','Synthetic conflict fixture','Synthetic','SYN-TEMP');" | Out-Null
    $canonicalCoverageStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "delete from insurance_records where id='$temporaryInsuranceId';" | Out-Null
    Invoke-Scalar "update patients set portal_enabled=true where canonical_id='$($flPatient.canonicalId)';" | Out-Null
    $portalStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "update patients set portal_enabled=false where canonical_id='$($flPatient.canonicalId)';" | Out-Null
    Add-ClinicalInventoryCheck 'Canonical coverage or portal enablement fails closed before inventory recording' (
        $canonicalCoverageStatus-eq 409 -and $portalStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_inventories where applicant_id='$flId';")-eq 0)

    $gaKey=New-Key 'sp29-ga'
    $gaRecorded=Invoke-ClinicalInventory $success $gaBody $gaKey
    $gaReplay=Invoke-ClinicalInventory $success $gaBody $gaKey
    $gaRecordedJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-ClinicalInventoryCheck 'Georgia records one coarse receipt and ItemsToReview owns route priority' (
        $gaRecorded.applicantStatus-eq'SyntheticClinicalInformationInventoryRecorded' -and
        [int]$gaRecorded.applicantVersion-eq 18 -and $gaRecorded.inventoryRecorded -and
        $gaRecorded.medicationsStatus-eq'ItemsToReview' -and
        $gaRecorded.allergiesOrIntolerancesStatus-eq'ItemsToReview' -and
        $gaRecorded.otherHealthHistoryStatus-eq'ItemsToReview' -and
        $gaRecorded.reviewRoute-eq'DetailedCollectionRequired' -and
        -not$gaRecorded.medicationListReconciled -and -not$gaRecorded.allergyListReconciled -and
        -not$gaRecorded.healthHistoryReconciled -and -not$gaRecorded.clinicalIntakeCompleted -and
        -not$gaRecorded.clinicalEligibilityEstablished -and -not$gaRecorded.clinicianReviewCreated -and
        -not$gaRecorded.patientRecordChanged -and -not$gaRecorded.requestCreated -and
        -not$gaRecorded.queueEntered -and -not$gaRecorded.careAuthorized -and
        -not$gaRecorded.prescribingEnabled -and $gaReplay.recordedAt-eq$gaRecorded.recordedAt -and
        $gaRecordedJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|medicationName|substance|reaction|dose|diagnosis|symptom|procedure|narrative|clinicalDate|clinicalIdentifier|freeText)"\s*:|TH-PAT-|4045550199')
    Add-ClinicalInventoryCheck 'Changed idempotency reuse and a second receipt fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{otherHealthHistoryStatus='PatientReportsNone'}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/clinical-information-inventory" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp29-second')} (Merge-Map $gaBody @{expectedVersion=18}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_inventories where applicant_id='$gaId';")-eq 1)

    $caContext=Get-ClinicalInventory $race
    $caBody=New-ClinicalInventoryBody 17 ([string]$caContext.inventorySnapshotFingerprint) 'Unsure' 'Unsure' 'PatientReportsNone'
    $caJsonBody=$caBody|ConvertTo-Json -Depth 8 -Compress
    $caRaceKey=New-Key 'sp29-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/clinical-information-inventory" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    $caReload=Get-ClinicalInventory $race
    Add-ClinicalInventoryCheck 'Eight concurrent exact California retries converge on one assisted-review receipt and event' (
        @($statuses|Where-Object{$_-eq 200}).Count-ge 1 -and
        @($statuses|Where-Object{$_-notin @(200,409)}).Count-eq 0 -and
        $caReload.reviewRoute-eq'AssistedReviewRequired' -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_inventories where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-clinical-information-inventory-recorded';")-eq 1) @{statuses=$statuses}

    $flContext=Get-ClinicalInventory $fl
    $flBody=New-ClinicalInventoryBody 17 ([string]$flContext.inventorySnapshotFingerprint)
    $flRecorded=Invoke-ClinicalInventory $fl $flBody (New-Key 'sp29-fl')
    Add-ClinicalInventoryCheck 'Florida all-none reports still require clinician reconciliation and establish no no-known finding' (
        $flRecorded.applicantStatus-eq'SyntheticClinicalInformationInventoryRecorded' -and
        $flRecorded.reviewRoute-eq'PendingClinicianReconciliation' -and
        $flRecorded.medicationsStatus-eq'PatientReportsNone' -and
        $flRecorded.allergiesOrIntolerancesStatus-eq'PatientReportsNone' -and
        $flRecorded.otherHealthHistoryStatus-eq'PatientReportsNone' -and
        $flRecorded.direction-match'Clinician reconciliation is still required' -and
        -not$flRecorded.medicationListReconciled -and -not$flRecorded.allergyListReconciled -and
        -not$flRecorded.healthHistoryReconciled -and -not$flRecorded.prescribingEnabled)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaReload=Get-ClinicalInventory $success
    Add-ClinicalInventoryCheck 'Applicant resume and reload expose the receipt without canonical identity or chart data' (
        $gaResume.status-eq'SyntheticClinicalInformationInventoryRecorded' -and $gaResume.canonicalPatientCreated -and
        $gaReload.inventoryRecorded -and $gaReload.applicantStatus-eq'SyntheticClinicalInformationInventoryRecorded' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'"(canonicalPatientId|legacyPid|pubpid|portalSession|medicationName|substance|reaction|dose|diagnosis|symptom|procedure|narrative|freeText)"\s*:|TH-PAT-'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',i.resulting_applicant_status,'version',i.resulting_applicant_version,
  'policy',i.policy_key,'policyVersion',i.policy_version,'evidenceType',i.evidence_type,
  'medications',i.medications_status,'allergies',i.allergies_or_intolerances_status,
  'history',i.other_health_history_status,'route',i.review_route,
  'patientReported',i.patient_reported_may_be_incomplete_acknowledged,
  'noDetails',i.no_clinical_details_captured_acknowledged,
  'reconciliation',i.clinician_reconciliation_required_acknowledged,
  'medicationReconciled',i.medication_list_reconciled,'allergyReconciled',i.allergy_list_reconciled,
  'historyReconciled',i.health_history_reconciled,'intake',i.clinical_intake_completed,
  'eligibility',i.clinical_eligibility_established,'review',i.clinician_review_created,
  'patientChanged',i.patient_record_changed,'request',i.request_created,'queue',i.queue_entered,
  'care',i.care_authorized,'prescribing',i.prescribing_enabled,
  'preparationVersion',d.resulting_applicant_version,'preparationPolicy',d.policy_key,
  'readinessIdMatch',i.communication_access_readiness_id=d.communication_access_readiness_id,
  'handoffIdMatch',i.insurance_handoff_confirmation_id=d.insurance_handoff_confirmation_id,
  'registrationIdMatch',i.registration_details_confirmation_id=d.registration_details_confirmation_id,
  'safetyIdMatch',i.safety_evaluation_id=d.safety_evaluation_id,
  'promotionIdMatch',i.promotion_id=d.promotion_id,'patientIdMatch',i.canonical_patient_id=d.canonical_patient_id))
from telehealth_applicant_clinical_information_inventories i
join telehealth_applicant_device_preparations d on d.preparation_id=i.device_preparation_id
where i.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    Add-ClinicalInventoryCheck 'Database provenance binds three receipts to the full prior chain with acknowledgments and every consequence false' (
        @($evidence).Count-eq 3 -and @($evidence|Where-Object{
            $_.status-eq'SyntheticClinicalInformationInventoryRecorded'-and[int]$_.version-eq 18-and
            $_.policy-eq'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY'-and[int]$_.policyVersion-eq 1-and
            $_.evidenceType-eq'PROMOTED_PATIENT_CLINICAL_INFORMATION_INVENTORY_RECEIPT'-and
            $_.medications-in @('PatientReportsNone','ItemsToReview','Unsure')-and
            $_.allergies-in @('PatientReportsNone','ItemsToReview','Unsure')-and
            $_.history-in @('PatientReportsNone','ItemsToReview','Unsure')-and
            $_.route-in @('DetailedCollectionRequired','AssistedReviewRequired','PendingClinicianReconciliation')-and
            $_.patientReported-and$_.noDetails-and$_.reconciliation-and-not$_.medicationReconciled-and
            -not$_.allergyReconciled-and-not$_.historyReconciled-and-not$_.intake-and-not$_.eligibility-and
            -not$_.review-and-not$_.patientChanged-and-not$_.request-and-not$_.queue-and-not$_.care-and
            -not$_.prescribing-and[int]$_.preparationVersion-eq 17-and
            $_.preparationPolicy-eq'SYNTHETIC_APPLICANT_DEVICE_PREPARATION'-and
            $_.readinessIdMatch-and$_.handoffIdMatch-and$_.registrationIdMatch-and$_.safetyIdMatch-and
            $_.promotionIdMatch-and$_.patientIdMatch}).Count-eq 3)

    Add-ClinicalInventoryCheck 'Append-only, route-priority, acknowledgment, and no-consequence database guards reject mutation' (
        (Test-ClinicalInventoryMutationRejected "update telehealth_applicant_clinical_information_inventories set review_route='PendingClinicianReconciliation' where applicant_id='$gaId';") -and
        (Test-ClinicalInventoryMutationRejected "delete from telehealth_applicant_clinical_information_inventories where applicant_id='$caId';") -and
        (Test-ClinicalInventoryMutationRejected @"
insert into telehealth_applicant_clinical_information_inventories(
 inventory_id,applicant_id,practice_id,facility_id,promotion_id,canonical_patient_id,
 registration_details_confirmation_id,insurance_handoff_confirmation_id,safety_evaluation_id,
 communication_access_readiness_id,device_preparation_id,resulting_applicant_version,
 resulting_applicant_status,inventory_snapshot_fingerprint,preparation_snapshot_fingerprint,
 medications_status,allergies_or_intolerances_status,other_health_history_status,review_route,
 patient_reported_may_be_incomplete_acknowledged,no_clinical_details_captured_acknowledged,
 clinician_reconciliation_required_acknowledged,policy_key,policy_version,evidence_type,
 applicant_expires_at,idempotency_key,command_fingerprint,prescribing_enabled)
select gen_random_uuid(),applicant_id,practice_id,facility_id,promotion_id,canonical_patient_id,
 registration_details_confirmation_id,insurance_handoff_confirmation_id,safety_evaluation_id,
 communication_access_readiness_id,device_preparation_id,resulting_applicant_version,
 resulting_applicant_status,inventory_snapshot_fingerprint,preparation_snapshot_fingerprint,
 medications_status,allergies_or_intolerances_status,other_health_history_status,review_route,
 patient_reported_may_be_incomplete_acknowledged,no_clinical_details_captured_acknowledged,
 clinician_reconciliation_required_acknowledged,policy_key,policy_version,evidence_type,
 applicant_expires_at,'invalid-duplicate',command_fingerprint,true
from telehealth_applicant_clinical_information_inventories where applicant_id='$flId';
"@))

    $after=Get-ClinicalInventoryCounts
    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    Add-ClinicalInventoryCheck 'Recording three inventory receipts changes no patient, medication, clinical, financial, request, queue, or care record' (
        [int]$after.inventories-[int]$before.inventories-eq 3 -and
        [int]$after.patients-eq[int]$before.patients -and
        [int]$after.medications-eq[int]$before.medications -and
        [int]$after.insuranceRecords-eq[int]$before.insuranceRecords -and
        [int]$after.requests-eq[int]$before.requests -and
        [int]$after.queueEntries-eq[int]$before.queueEntries -and
        [int]$after.intakeSnapshots-eq[int]$before.intakeSnapshots -and
        [int]$after.appointments-eq[int]$before.appointments -and
        [int]$after.encounters-eq[int]$before.encounters -and
        [int]$after.claims-eq[int]$before.claims -and
        [int]$after.prescriptions-eq[int]$before.prescriptions -and
        ($patientHashesBefore|ConvertTo-Json -Compress)-eq($patientHashesAfter|ConvertTo-Json -Compress))

    $prohibitedColumns=[int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public'
  and table_name='telehealth_applicant_clinical_information_inventories'
  and column_name in ('medication_name','substance','reaction','dose','diagnosis','symptom',
                      'procedure','narrative','clinical_date','clinical_identifier','free_text');
"@)
    Add-ClinicalInventoryCheck 'Schema contains no detailed clinical fields and all three applicants end at the bounded inventory status' (
        $prohibitedColumns-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id in ('$gaId','$caId','$flId') and status='SyntheticClinicalInformationInventoryRecorded' and version=18;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_clinical_information_inventories where applicant_id in ('$gaId','$caId','$flId');")-eq 3)
}
catch {
    $passed=$false
    Add-ClinicalInventoryCheck 'Applicant clinical-information inventory proof execution' $false ("$($_.Exception.Message) | $($_.ScriptStackTrace)")
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0032'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant clinical-information inventory proof failed. See $resultPath"}
Write-Host "Applicant clinical-information inventory proof passed ($($checks.Count) checks). Artifact: $resultPath"
