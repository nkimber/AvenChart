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
    throw 'Telehealth applicant allergy-information proof is local-only.'
}

# The sealed Sprint 30 chain leaves Georgia, California, and Florida applicants
# at the synthetic medication-information receipt gate.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantMedicationInformation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-allergy-information.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-AllergyInformationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-AllergyInformationBody(
    [int]$Version,
    [string]$Fingerprint,
    [object[]]$Items=@(),
    [bool]$Additional=$false) {
    @{
        expectedVersion=$Version
        allergyInformationSnapshotFingerprint=$Fingerprint
        allergyItems=$Items
        additionalOrUnlistedItemsReported=$Additional
        patientReportedMayBeIncompleteAcknowledged=$true
        syntheticCatalogIncompleteAcknowledged=$true
        noReactionOrCriticalityCapturedAcknowledged=$true
        clinicianVerificationRequiredAcknowledged=$true
    }
}
function Get-AllergyInformation([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/allergy-information" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Invoke-AllergyInformation([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/allergy-information" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 10)
}
function Get-AllergyInformationCounts {
    (Invoke-Scalar @"
select json_build_object(
  'receipts',(select count(*) from telehealth_applicant_allergy_information_receipts),
  'reportedItems',(select count(*) from telehealth_applicant_reported_allergy_items),
  'patients',(select count(*) from patients),
  'medications',(select count(*) from medications),
  'prescriptions',(select count(*) from prescriptions),
  'allergies',(select count(*) from allergies),
  'insuranceRecords',(select count(*) from insurance_records),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'intakeSnapshots',(select count(*) from telehealth_intake_snapshots),
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims));
"@)|ConvertFrom-Json
}
function Test-AllergyInformationMutationRejected([string]$Sql) {
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
    $before=Get-AllergyInformationCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/allergy-information" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaContext=$gaWeb.Content|ConvertFrom-Json
    $gaJson=$gaContext|ConvertTo-Json -Depth 12 -Compress
    $expectedCatalog=@('amoxicillin','ibuprofen','peanut','shellfish','latex','bee-venom')
    Add-AllergyInformationCheck 'Applicant-owned read returns the fixed six-substance incomplete local catalog without canonical allergy content' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaContext.applicantStatus-eq'SyntheticMedicationInformationRecorded' -and
        [int]$gaContext.applicantVersion-eq 19 -and
        $gaContext.inventoryAllergiesOrIntolerancesStatus-eq'ItemsToReview' -and
        -not $gaContext.allergyInformationRecorded -and
        @($gaContext.catalogItems).Count-eq 6 -and
        (@($gaContext.catalogItems.catalogKey) -join ',')-eq($expectedCatalog-join ',') -and
        @($gaContext.catalogItems|Where-Object{$_.codingSystem-ne'LOCAL_SYNTHETIC_ONLY'-or$_.snomedCtMapped-or$_.rxNormMapped}).Count-eq 0 -and
        -not$gaContext.catalogComplete -and
        ([string]$gaContext.allergyInformationSnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaContext.policyKey-eq'SYNTHETIC_APPLICANT_ALLERGY_INFORMATION' -and
        $gaJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|reaction|manifestation|type|clinicalStatus|verificationStatus|severity|criticality|onset|occurrence|note|attachment|freeText|snomedCode|rxNormCode)"\s*:|TH-PAT-|4045550199')

    $gaItems=@(@{catalogKey='amoxicillin'},@{catalogKey='peanut'})
    $gaBody=New-AllergyInformationBody 19 ([string]$gaContext.allergyInformationSnapshotFingerprint) $gaItems $true
    Add-AllergyInformationCheck 'Authentication, catalog, branch, acknowledgment, version, and snapshot failures persist nothing' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/allergy-information")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp31-catalog')} (Merge-Map $gaBody @{allergyItems=@(@{catalogKey='aspirin'})}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp31-empty')} (New-AllergyInformationBody 19 ([string]$gaContext.allergyInformationSnapshotFingerprint)))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp31-ack')} (Merge-Map $gaBody @{noReactionOrCriticalityCapturedAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp31-version')} (Merge-Map $gaBody @{expectedVersion=18}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp31-snapshot')} (Merge-Map $gaBody @{allergyInformationSnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_allergy_information_receipts where applicant_id='$gaId';")-eq 0)

    $flPatient=(Invoke-Scalar @"
select json_build_object('canonicalId',p.canonical_id,'pid',p.legacy_pid)
from patients p join telehealth_applicant_synthetic_promotions promotion
  on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id='$flId';
"@)|ConvertFrom-Json
    $temporaryAllergyId="ALG-SP31-$([Guid]::NewGuid().ToString('N'))"
    Invoke-Scalar "insert into allergies(id,patient_id,pid,type,title) values('$temporaryAllergyId','$($flPatient.canonicalId)',$($flPatient.pid),'allergy','Synthetic conflict fixture');" | Out-Null
    $canonicalAllergyStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "delete from allergies where id='$temporaryAllergyId';" | Out-Null
    Add-AllergyInformationCheck 'Existing canonical allergy content fails closed before recording' (
        $canonicalAllergyStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_allergy_information_receipts where applicant_id='$flId';")-eq 0)

    $gaKey=New-Key 'sp31-ga'
    $gaRecorded=Invoke-AllergyInformation $success $gaBody $gaKey
    $gaReplay=Invoke-AllergyInformation $success $gaBody $gaKey
    $gaRecordedJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-AllergyInformationCheck 'Georgia records two ordered patient-reported substances and additional collection owns route priority' (
        $gaRecorded.applicantStatus-eq'SyntheticAllergyInformationRecorded' -and
        [int]$gaRecorded.applicantVersion-eq 20 -and $gaRecorded.allergyInformationRecorded -and
        @($gaRecorded.allergyItems).Count-eq 2 -and
        $gaRecorded.allergyItems[0].catalogKey-eq'amoxicillin' -and
        $gaRecorded.allergyItems[0].category-eq'Medication' -and
        $gaRecorded.allergyItems[1].catalogKey-eq'peanut' -and
        $gaRecorded.allergyItems[1].category-eq'Food' -and
        $gaRecorded.additionalOrUnlistedItemsReported -and
        $gaRecorded.reviewRoute-eq'AdditionalAllergyCollectionRequired' -and
        -not$gaRecorded.allergyIntoleranceCreated -and -not$gaRecorded.allergyListReconciled -and
        -not$gaRecorded.reactionAssessed -and -not$gaRecorded.criticalityAssessed -and
        -not$gaRecorded.contraindicationCheckPerformed -and -not$gaRecorded.clinicianReviewCreated -and
        -not$gaRecorded.clinicalIntakeCompleted -and -not$gaRecorded.clinicalEligibilityEstablished -and
        -not$gaRecorded.patientRecordChanged -and -not$gaRecorded.requestCreated -and
        -not$gaRecorded.queueEntered -and -not$gaRecorded.careAuthorized -and
        -not$gaRecorded.prescribingEnabled -and $gaReplay.recordedAt-eq$gaRecorded.recordedAt -and
        $gaRecordedJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|reaction|manifestation|type|clinicalStatus|verificationStatus|severity|criticality|onset|occurrence|note|attachment|freeText|snomedCode|rxNormCode)"\s*:|TH-PAT-|4045550199')
    Add-AllergyInformationCheck 'Changed idempotency reuse and a second receipt fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{additionalOrUnlistedItemsReported=$false}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/allergy-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp31-second')} (Merge-Map $gaBody @{expectedVersion=20}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_allergy_information_receipts where applicant_id='$gaId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_reported_allergy_items where applicant_id='$gaId';")-eq 2)

    $caContext=Get-AllergyInformation $race
    $caBody=New-AllergyInformationBody 19 ([string]$caContext.allergyInformationSnapshotFingerprint)
    $caJsonBody=$caBody|ConvertTo-Json -Depth 10 -Compress
    $caRaceKey=New-Key 'sp31-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/allergy-information" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    $caReload=Get-AllergyInformation $race
    $caReplay=Invoke-AllergyInformation $race $caBody $caRaceKey
    Add-AllergyInformationCheck 'Eight concurrent California attempts and an unchanged retry converge on one assisted-review receipt and event' (
        @($statuses|Where-Object{$_-eq 200}).Count-ge 1 -and
        @($statuses|Where-Object{$_-notin @(200,409)}).Count-eq 0 -and
        $caReplay.recordedAt-eq$caReload.recordedAt -and
        $caReload.inventoryAllergiesOrIntolerancesStatus-eq'Unsure' -and
        $caReload.reviewRoute-eq'AssistedAllergyReviewRequired' -and
        @($caReload.allergyItems).Count-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_allergy_information_receipts where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-allergy-information-recorded';")-eq 1) @{statuses=$statuses}

    $flContext=Get-AllergyInformation $fl
    $flBody=New-AllergyInformationBody 19 ([string]$flContext.allergyInformationSnapshotFingerprint)
    $flRecorded=Invoke-AllergyInformation $fl $flBody (New-Key 'sp31-fl')
    Add-AllergyInformationCheck 'Florida patient-reports-none remains provisional and requires clinician confirmation' (
        $flRecorded.applicantStatus-eq'SyntheticAllergyInformationRecorded' -and
        $flRecorded.inventoryAllergiesOrIntolerancesStatus-eq'PatientReportsNone' -and
        @($flRecorded.allergyItems).Count-eq 0 -and
        -not$flRecorded.additionalOrUnlistedItemsReported -and
        $flRecorded.reviewRoute-eq'PendingClinicianConfirmationOfPatientReportedNone' -and
        $flRecorded.direction-match'no-known-allergy assertion' -and
        -not$flRecorded.allergyListReconciled -and -not$flRecorded.prescribingEnabled)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaReload=Get-AllergyInformation $success
    Add-AllergyInformationCheck 'Applicant resume and reload expose the immutable receipt without canonical identity or chart identifiers' (
        $gaResume.status-eq'SyntheticAllergyInformationRecorded' -and $gaResume.canonicalPatientCreated -and
        $gaReload.allergyInformationRecorded -and $gaReload.applicantStatus-eq'SyntheticAllergyInformationRecorded' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'"(canonicalPatientId|legacyPid|pubpid|portalSession|reaction|manifestation|type|clinicalStatus|verificationStatus|severity|criticality|onset|occurrence|note|attachment|freeText)"\s*:|TH-PAT-'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',r.resulting_applicant_status,'version',r.resulting_applicant_version,
  'policy',r.policy_key,'policyVersion',r.policy_version,'evidenceType',r.evidence_type,
  'inventoryStatus',r.inventory_allergies_or_intolerances_status,'selectedCount',r.selected_item_count,
  'actualCount',(select count(*) from telehealth_applicant_reported_allergy_items item where item.receipt_id=r.receipt_id),
  'additional',r.additional_or_unlisted_items_reported,'route',r.review_route,
  'reportedAck',r.patient_reported_may_be_incomplete_acknowledged,
  'catalogAck',r.synthetic_catalog_incomplete_acknowledged,
  'noReactionAck',r.no_reaction_or_criticality_captured_acknowledged,
  'verifyAck',r.clinician_verification_required_acknowledged,
  'catalog',r.catalog_key,'coding',r.coding_system,'catalogComplete',r.catalog_complete,
  'resource',r.allergy_intolerance_created,'reconciled',r.allergy_list_reconciled,
  'reaction',r.reaction_assessed,'criticality',r.criticality_assessed,
  'contraindication',r.contraindication_check_performed,'review',r.clinician_review_created,
  'intake',r.clinical_intake_completed,'eligibility',r.clinical_eligibility_established,
  'patientChanged',r.patient_record_changed,'request',r.request_created,'queue',r.queue_entered,
  'care',r.care_authorized,'prescribing',r.prescribing_enabled,
  'medicationVersion',m.resulting_applicant_version,'inventoryVersion',i.resulting_applicant_version,
  'medicationIdMatch',r.medication_information_id=m.receipt_id,
  'inventoryIdMatch',r.clinical_inventory_id=i.inventory_id,
  'promotionIdMatch',r.promotion_id=i.promotion_id,'patientIdMatch',r.canonical_patient_id=i.canonical_patient_id))
from telehealth_applicant_allergy_information_receipts r
join telehealth_applicant_medication_information_receipts m on m.receipt_id=r.medication_information_id
join telehealth_applicant_clinical_information_inventories i on i.inventory_id=r.clinical_inventory_id
where r.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    Add-AllergyInformationCheck 'Database provenance binds three receipts to prior inventory and medication evidence with exact counts, acknowledgments, and every consequence false' (
        @($evidence).Count-eq 3 -and @($evidence|Where-Object{
            $_.status-eq'SyntheticAllergyInformationRecorded'-and[int]$_.version-eq 20-and
            $_.policy-eq'SYNTHETIC_APPLICANT_ALLERGY_INFORMATION'-and[int]$_.policyVersion-eq 1-and
            $_.evidenceType-eq'PROMOTED_PATIENT_ALLERGY_INFORMATION_RECEIPT'-and
            [int]$_.selectedCount-eq[int]$_.actualCount-and$_.reportedAck-and$_.catalogAck-and
            $_.noReactionAck-and$_.verifyAck-and$_.catalog-eq'avenchart-synthetic-applicant-allergy-substances-2026-08'-and
            $_.coding-eq'LOCAL_SYNTHETIC_ONLY'-and-not$_.catalogComplete-and-not$_.resource-and
            -not$_.reconciled-and-not$_.reaction-and-not$_.criticality-and-not$_.contraindication-and
            -not$_.review-and-not$_.intake-and-not$_.eligibility-and-not$_.patientChanged-and
            -not$_.request-and-not$_.queue-and-not$_.care-and-not$_.prescribing-and
            [int]$_.medicationVersion-eq 19-and[int]$_.inventoryVersion-eq 18-and
            $_.medicationIdMatch-and$_.inventoryIdMatch-and$_.promotionIdMatch-and$_.patientIdMatch}).Count-eq 3)

    Add-AllergyInformationCheck 'Append-only and deferred parent-child count guards reject mutation or inconsistent evidence' (
        (Test-AllergyInformationMutationRejected "update telehealth_applicant_allergy_information_receipts set review_route='ClinicianAllergyReviewRequired' where applicant_id='$gaId';") -and
        (Test-AllergyInformationMutationRejected "delete from telehealth_applicant_reported_allergy_items where applicant_id='$gaId';") -and
        (Test-AllergyInformationMutationRejected @"
begin;
set constraints all deferred;
insert into telehealth_applicant_reported_allergy_items(
 item_id,receipt_id,applicant_id,practice_id,facility_id,item_ordinal,catalog_key,
 display_name,category,catalog_version,coding_system,snomed_ct_mapped,rxnorm_mapped)
select gen_random_uuid(),receipt_id,applicant_id,practice_id,facility_id,3,'latex',
 'Latex','Environment',1,'LOCAL_SYNTHETIC_ONLY',false,false
from telehealth_applicant_allergy_information_receipts where applicant_id='$gaId';
commit;
"@))

    $after=Get-AllergyInformationCounts
    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    Add-AllergyInformationCheck 'Three receipts and two child selections change no canonical allergy, medication, prescription, patient, financial, request, queue, or care record' (
        [int]$after.receipts-[int]$before.receipts-eq 3 -and
        [int]$after.reportedItems-[int]$before.reportedItems-eq 2 -and
        [int]$after.patients-eq[int]$before.patients -and
        [int]$after.medications-eq[int]$before.medications -and
        [int]$after.prescriptions-eq[int]$before.prescriptions -and
        [int]$after.allergies-eq[int]$before.allergies -and
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
  and table_name in ('telehealth_applicant_allergy_information_receipts','telehealth_applicant_reported_allergy_items')
  and column_name in ('reaction','manifestation','allergy_type','clinical_status','verification_status','severity',
                      'criticality','onset','occurrence','clinical_date','note','attachment','free_text','snomed_code','rxnorm_code');
"@)
    Add-AllergyInformationCheck 'Schema has no prohibited allergy-detail or terminology columns and all applicants stop at the bounded receipt' (
        $prohibitedColumns-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id in ('$gaId','$caId','$flId') and status='SyntheticAllergyInformationRecorded' and version=20;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_allergy_information_receipts where applicant_id in ('$gaId','$caId','$flId');")-eq 3)
}
catch {
    $passed=$false
    Add-AllergyInformationCheck 'Applicant allergy-information proof execution' $false ("$($_.Exception.Message) | $($_.ScriptStackTrace)")
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0034'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant allergy-information proof failed. See $resultPath"}
Write-Host "Applicant allergy-information proof passed ($($checks.Count) checks). Artifact: $resultPath"
