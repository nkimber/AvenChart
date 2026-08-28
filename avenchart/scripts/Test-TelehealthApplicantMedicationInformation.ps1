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
    throw 'Telehealth applicant medication-information proof is local-only.'
}

# Reuse the sealed Sprint 29 chain. It leaves one Georgia, one California, and
# one Florida applicant at the synthetic clinical-information inventory gate.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantClinicalInformationInventory.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-medication-information.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-MedicationInformationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-MedicationInformationBody(
    [int]$Version,
    [string]$Fingerprint,
    [object[]]$Items=@(),
    [bool]$Additional=$false) {
    @{
        expectedVersion=$Version
        medicationInformationSnapshotFingerprint=$Fingerprint
        medicationItems=$Items
        additionalOrUnlistedItemsReported=$Additional
        patientReportedMayBeIncompleteAcknowledged=$true
        syntheticCatalogIncompleteAcknowledged=$true
        noDoseOrDirectionsCapturedAcknowledged=$true
        clinicianReconciliationRequiredAcknowledged=$true
    }
}
function Get-MedicationInformation([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/medication-information" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Invoke-MedicationInformation([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/medication-information" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 10)
}
function Get-MedicationInformationCounts {
    (Invoke-Scalar @"
select json_build_object(
  'receipts',(select count(*) from telehealth_applicant_medication_information_receipts),
  'reportedItems',(select count(*) from telehealth_applicant_reported_medication_items),
  'patients',(select count(*) from patients),
  'medications',(select count(*) from medications),
  'prescriptions',(select count(*) from prescriptions),
  'insuranceRecords',(select count(*) from insurance_records),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'intakeSnapshots',(select count(*) from telehealth_intake_snapshots),
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims));
"@)|ConvertFrom-Json
}
function Test-MedicationInformationMutationRejected([string]$Sql) {
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
    $before=Get-MedicationInformationCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/medication-information" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaContext=$gaWeb.Content|ConvertFrom-Json
    $gaJson=$gaContext|ConvertTo-Json -Depth 12 -Compress
    $expectedCatalog=@('acetaminophen','ibuprofen','sumatriptan','melatonin','lisinopril','metformin')
    Add-MedicationInformationCheck 'Applicant-owned read returns the fixed incomplete local catalog without canonical medication content' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaContext.applicantStatus-eq'SyntheticClinicalInformationInventoryRecorded' -and
        [int]$gaContext.applicantVersion-eq 18 -and
        $gaContext.inventoryMedicationsStatus-eq'ItemsToReview' -and
        -not $gaContext.medicationInformationRecorded -and
        @($gaContext.catalogItems).Count-eq 6 -and
        (@($gaContext.catalogItems.catalogKey) -join ',')-eq($expectedCatalog-join ',') -and
        @($gaContext.catalogItems|Where-Object{$_.codingSystem-ne'LOCAL_SYNTHETIC_ONLY'-or$_.rxNormMapped}).Count-eq 0 -and
        -not$gaContext.catalogComplete -and
        (($gaContext.supportedReportedUseStatuses-join',')-eq'Taking,NotTaking,Unsure') -and
        ([string]$gaContext.medicationInformationSnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaContext.policyKey-eq'SYNTHETIC_APPLICANT_MEDICATION_INFORMATION' -and
        $gaJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|dose|directions|route|frequency|indication|prescriber|pharmacy|note|attachment|freeText)"\s*:|TH-PAT-|4045550199')

    $gaItems=@(
        @{catalogKey='sumatriptan';reportedUseStatus='Taking'},
        @{catalogKey='melatonin';reportedUseStatus='Unsure'})
    $gaBody=New-MedicationInformationBody 18 ([string]$gaContext.medicationInformationSnapshotFingerprint) $gaItems $true
    Add-MedicationInformationCheck 'Authentication, catalog, use-state, branch, acknowledgment, version, and snapshot failures persist nothing' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/medication-information")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp30-catalog')} (Merge-Map $gaBody @{medicationItems=@(@{catalogKey='aspirin';reportedUseStatus='Taking'})}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp30-use')} (Merge-Map $gaBody @{medicationItems=@(@{catalogKey='ibuprofen';reportedUseStatus='Daily'})}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp30-empty')} (New-MedicationInformationBody 18 ([string]$gaContext.medicationInformationSnapshotFingerprint)))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp30-ack')} (Merge-Map $gaBody @{noDoseOrDirectionsCapturedAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp30-version')} (Merge-Map $gaBody @{expectedVersion=17}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp30-snapshot')} (Merge-Map $gaBody @{medicationInformationSnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_medication_information_receipts where applicant_id='$gaId';")-eq 0)

    $flPatient=(Invoke-Scalar @"
select json_build_object('canonicalId',p.canonical_id,'pid',p.legacy_pid)
from patients p join telehealth_applicant_synthetic_promotions promotion
  on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id='$flId';
"@)|ConvertFrom-Json
    $temporaryMedicationId="MED-SP30-$([Guid]::NewGuid().ToString('N'))"
    Invoke-Scalar "insert into medications(id,patient_id,pid,type,title) values('$temporaryMedicationId','$($flPatient.canonicalId)',$($flPatient.pid),'synthetic','Synthetic conflict fixture');" | Out-Null
    $canonicalMedicationStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "delete from medications where id='$temporaryMedicationId';" | Out-Null
    Add-MedicationInformationCheck 'Existing canonical medication content fails closed before recording' (
        $canonicalMedicationStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_medication_information_receipts where applicant_id='$flId';")-eq 0)

    $gaKey=New-Key 'sp30-ga'
    $gaRecorded=Invoke-MedicationInformation $success $gaBody $gaKey
    $gaReplay=Invoke-MedicationInformation $success $gaBody $gaKey
    $gaRecordedJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-MedicationInformationCheck 'Georgia records two ordered patient-reported ingredients and additional collection owns route priority' (
        $gaRecorded.applicantStatus-eq'SyntheticMedicationInformationRecorded' -and
        [int]$gaRecorded.applicantVersion-eq 19 -and $gaRecorded.medicationInformationRecorded -and
        @($gaRecorded.medicationItems).Count-eq 2 -and
        $gaRecorded.medicationItems[0].catalogKey-eq'sumatriptan' -and
        $gaRecorded.medicationItems[0].reportedUseStatus-eq'Taking' -and
        $gaRecorded.medicationItems[1].catalogKey-eq'melatonin' -and
        $gaRecorded.medicationItems[1].reportedUseStatus-eq'Unsure' -and
        $gaRecorded.additionalOrUnlistedItemsReported -and
        $gaRecorded.reviewRoute-eq'AdditionalMedicationCollectionRequired' -and
        -not$gaRecorded.medicationStatementCreated -and -not$gaRecorded.medicationRequestCreated -and
        -not$gaRecorded.medicationListReconciled -and -not$gaRecorded.interactionCheckPerformed -and
        -not$gaRecorded.clinicianReviewCreated -and -not$gaRecorded.clinicalIntakeCompleted -and
        -not$gaRecorded.clinicalEligibilityEstablished -and -not$gaRecorded.patientRecordChanged -and
        -not$gaRecorded.requestCreated -and -not$gaRecorded.queueEntered -and
        -not$gaRecorded.careAuthorized -and -not$gaRecorded.prescribingEnabled -and
        $gaReplay.recordedAt-eq$gaRecorded.recordedAt -and
        $gaRecordedJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|dose|directions|routeDetail|frequency|indication|prescriber|pharmacy|note|attachment|freeText)"\s*:|TH-PAT-|4045550199')
    Add-MedicationInformationCheck 'Changed idempotency reuse and a second receipt fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{additionalOrUnlistedItemsReported=$false}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/medication-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp30-second')} (Merge-Map $gaBody @{expectedVersion=19}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_medication_information_receipts where applicant_id='$gaId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_reported_medication_items where applicant_id='$gaId';")-eq 2)

    $caContext=Get-MedicationInformation $race
    $caBody=New-MedicationInformationBody 18 ([string]$caContext.medicationInformationSnapshotFingerprint)
    $caJsonBody=$caBody|ConvertTo-Json -Depth 10 -Compress
    $caRaceKey=New-Key 'sp30-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/medication-information" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    $caReload=Get-MedicationInformation $race
    $caReplay=Invoke-MedicationInformation $race $caBody $caRaceKey
    Add-MedicationInformationCheck 'Eight concurrent California attempts and an unchanged retry converge on one assisted-review receipt and event' (
        @($statuses|Where-Object{$_-eq 200}).Count-ge 1 -and
        @($statuses|Where-Object{$_-notin @(200,409)}).Count-eq 0 -and
        $caReplay.recordedAt-eq$caReload.recordedAt -and
        $caReload.reviewRoute-eq'AssistedMedicationReviewRequired' -and
        @($caReload.medicationItems).Count-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_medication_information_receipts where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-medication-information-recorded';")-eq 1) @{statuses=$statuses}

    $flContext=Get-MedicationInformation $fl
    $flBody=New-MedicationInformationBody 18 ([string]$flContext.medicationInformationSnapshotFingerprint)
    $flRecorded=Invoke-MedicationInformation $fl $flBody (New-Key 'sp30-fl')
    Add-MedicationInformationCheck 'Florida patient-reports-none remains provisional and requires clinician confirmation' (
        $flRecorded.applicantStatus-eq'SyntheticMedicationInformationRecorded' -and
        $flRecorded.inventoryMedicationsStatus-eq'PatientReportsNone' -and
        @($flRecorded.medicationItems).Count-eq 0 -and
        -not$flRecorded.additionalOrUnlistedItemsReported -and
        $flRecorded.reviewRoute-eq'PendingClinicianConfirmationOfNone' -and
        $flRecorded.direction-match'Clinician confirmation is still required' -and
        -not$flRecorded.medicationListReconciled -and -not$flRecorded.prescribingEnabled)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaReload=Get-MedicationInformation $success
    Add-MedicationInformationCheck 'Applicant resume and reload expose the immutable receipt without canonical identity or chart identifiers' (
        $gaResume.status-eq'SyntheticMedicationInformationRecorded' -and $gaResume.canonicalPatientCreated -and
        $gaReload.medicationInformationRecorded -and $gaReload.applicantStatus-eq'SyntheticMedicationInformationRecorded' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'"(canonicalPatientId|legacyPid|pubpid|portalSession|dose|directions|frequency|indication|prescriber|pharmacy|note|attachment|freeText)"\s*:|TH-PAT-'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',r.resulting_applicant_status,'version',r.resulting_applicant_version,
  'policy',r.policy_key,'policyVersion',r.policy_version,'evidenceType',r.evidence_type,
  'inventoryStatus',r.inventory_medications_status,'selectedCount',r.selected_item_count,
  'actualCount',(select count(*) from telehealth_applicant_reported_medication_items item where item.receipt_id=r.receipt_id),
  'additional',r.additional_or_unlisted_items_reported,'route',r.review_route,
  'reportedAck',r.patient_reported_may_be_incomplete_acknowledged,
  'catalogAck',r.synthetic_catalog_incomplete_acknowledged,
  'noDoseAck',r.no_dose_or_directions_captured_acknowledged,
  'reconcileAck',r.clinician_reconciliation_required_acknowledged,
  'catalog',r.catalog_key,'coding',r.coding_system,'catalogComplete',r.catalog_complete,
  'statement',r.medication_statement_created,'requestResource',r.medication_request_created,
  'reconciled',r.medication_list_reconciled,'interaction',r.interaction_check_performed,
  'review',r.clinician_review_created,'intake',r.clinical_intake_completed,
  'eligibility',r.clinical_eligibility_established,'patientChanged',r.patient_record_changed,
  'request',r.request_created,'queue',r.queue_entered,'care',r.care_authorized,
  'prescribing',r.prescribing_enabled,'inventoryVersion',i.resulting_applicant_version,
  'inventoryIdMatch',r.clinical_inventory_id=i.inventory_id,
  'promotionIdMatch',r.promotion_id=i.promotion_id,'patientIdMatch',r.canonical_patient_id=i.canonical_patient_id))
from telehealth_applicant_medication_information_receipts r
join telehealth_applicant_clinical_information_inventories i on i.inventory_id=r.clinical_inventory_id
where r.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    Add-MedicationInformationCheck 'Database provenance binds three receipts to the prior inventory with exact counts, acknowledgments, and every consequence false' (
        @($evidence).Count-eq 3 -and @($evidence|Where-Object{
            $_.status-eq'SyntheticMedicationInformationRecorded'-and[int]$_.version-eq 19-and
            $_.policy-eq'SYNTHETIC_APPLICANT_MEDICATION_INFORMATION'-and[int]$_.policyVersion-eq 1-and
            $_.evidenceType-eq'PROMOTED_PATIENT_MEDICATION_INFORMATION_RECEIPT'-and
            [int]$_.selectedCount-eq[int]$_.actualCount-and$_.reportedAck-and$_.catalogAck-and
            $_.noDoseAck-and$_.reconcileAck-and$_.catalog-eq'avenchart-synthetic-applicant-medication-ingredients-2026-08'-and
            $_.coding-eq'LOCAL_SYNTHETIC_ONLY'-and-not$_.catalogComplete-and-not$_.statement-and
            -not$_.requestResource-and-not$_.reconciled-and-not$_.interaction-and-not$_.review-and
            -not$_.intake-and-not$_.eligibility-and-not$_.patientChanged-and-not$_.request-and
            -not$_.queue-and-not$_.care-and-not$_.prescribing-and[int]$_.inventoryVersion-eq 18-and
            $_.inventoryIdMatch-and$_.promotionIdMatch-and$_.patientIdMatch}).Count-eq 3)

    Add-MedicationInformationCheck 'Append-only and deferred parent-child count guards reject mutation or inconsistent evidence' (
        (Test-MedicationInformationMutationRejected "update telehealth_applicant_medication_information_receipts set review_route='ClinicianMedicationReviewRequired' where applicant_id='$gaId';") -and
        (Test-MedicationInformationMutationRejected "delete from telehealth_applicant_reported_medication_items where applicant_id='$gaId';") -and
        (Test-MedicationInformationMutationRejected @"
begin;
set constraints all deferred;
insert into telehealth_applicant_reported_medication_items(
 item_id,receipt_id,applicant_id,practice_id,facility_id,item_ordinal,catalog_key,
 display_name,catalog_version,coding_system,rxnorm_mapped,reported_use_status)
select gen_random_uuid(),receipt_id,applicant_id,practice_id,facility_id,3,'ibuprofen',
 'Ibuprofen',1,'LOCAL_SYNTHETIC_ONLY',false,'Taking'
from telehealth_applicant_medication_information_receipts where applicant_id='$gaId';
commit;
"@))

    $after=Get-MedicationInformationCounts
    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    Add-MedicationInformationCheck 'Three receipts and two child selections change no canonical medication, prescription, patient, financial, request, queue, or care record' (
        [int]$after.receipts-[int]$before.receipts-eq 3 -and
        [int]$after.reportedItems-[int]$before.reportedItems-eq 2 -and
        [int]$after.patients-eq[int]$before.patients -and
        [int]$after.medications-eq[int]$before.medications -and
        [int]$after.prescriptions-eq[int]$before.prescriptions -and
        [int]$after.insuranceRecords-eq[int]$before.insuranceRecords -and
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
  and table_name in ('telehealth_applicant_medication_information_receipts','telehealth_applicant_reported_medication_items')
  and column_name in ('dose','directions','route','frequency','timing','indication','prescriber',
                      'pharmacy','clinical_date','note','attachment','free_text','rxnorm_code','ndc_code','snomed_code');
"@)
    Add-MedicationInformationCheck 'Schema has no prohibited medication-detail or terminology columns and all applicants stop at the bounded receipt' (
        $prohibitedColumns-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id in ('$gaId','$caId','$flId') and status='SyntheticMedicationInformationRecorded' and version=19;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_medication_information_receipts where applicant_id in ('$gaId','$caId','$flId');")-eq 3)
}
catch {
    $passed=$false
    Add-MedicationInformationCheck 'Applicant medication-information proof execution' $false ("$($_.Exception.Message) | $($_.ScriptStackTrace)")
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0033'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant medication-information proof failed. See $resultPath"}
Write-Host "Applicant medication-information proof passed ($($checks.Count) checks). Artifact: $resultPath"
