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
    throw 'Telehealth applicant health-history-information proof is local-only.'
}

# The sealed Sprint 31 chain leaves Georgia, California, and Florida applicants
# at the synthetic allergy-information receipt gate.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantAllergyInformation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-health-history-information.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-HealthHistoryInformationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-HealthHistoryInformationBody(
    [int]$Version,
    [string]$Fingerprint,
    [object[]]$Topics=@(),
    [bool]$Additional=$false) {
    @{
        expectedVersion=$Version
        healthHistoryInformationSnapshotFingerprint=$Fingerprint
        healthHistoryTopics=$Topics
        additionalOrUnlistedTopicsReported=$Additional
        patientReportedMayBeIncompleteAcknowledged=$true
        topicSelectionIsNotDiagnosisAcknowledged=$true
        noStatusOrTimingCapturedAcknowledged=$true
        clinicianVerificationRequiredAcknowledged=$true
    }
}
function Get-HealthHistoryInformation([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/health-history-information" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Invoke-HealthHistoryInformation([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/health-history-information" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 10)
}
function Get-HealthHistoryInformationCounts {
    (Invoke-Scalar @"
select json_build_object(
  'receipts',(select count(*) from telehealth_applicant_health_history_information_receipts),
  'reportedTopics',(select count(*) from telehealth_applicant_reported_health_history_topics),
  'patients',(select count(*) from patients),
  'problems',(select count(*) from problems),
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
function Test-HealthHistoryInformationMutationRejected([string]$Sql) {
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
    $before=Get-HealthHistoryInformationCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/health-history-information" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaContext=$gaWeb.Content|ConvertFrom-Json
    $gaJson=$gaContext|ConvertTo-Json -Depth 12 -Compress
    $expectedCatalog=@(
        'ongoing-health-conditions','prior-surgery-or-hospital-stay',
        'pregnancy-or-postpartum-information','immune-system-or-active-cancer-treatment',
        'behavioral-health-or-substance-use','family-health-history')
    Add-HealthHistoryInformationCheck 'Applicant-owned read returns the fixed six-topic incomplete local catalog without diagnosis or canonical health-history content' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaContext.applicantStatus-eq'SyntheticAllergyInformationRecorded' -and
        [int]$gaContext.applicantVersion-eq 20 -and
        $gaContext.inventoryOtherHealthHistoryStatus-eq'ItemsToReview' -and
        -not $gaContext.healthHistoryInformationRecorded -and
        @($gaContext.catalogTopics).Count-eq 6 -and
        (@($gaContext.catalogTopics.catalogKey) -join ',')-eq($expectedCatalog-join ',') -and
        @($gaContext.catalogTopics|Where-Object{$_.codingSystem-ne'LOCAL_SYNTHETIC_ONLY'-or$_.snomedCtMapped-or$_.icd10CmMapped-or$_.loincMapped}).Count-eq 0 -and
        -not$gaContext.catalogComplete -and
        ([string]$gaContext.healthHistoryInformationSnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaContext.policyKey-eq'SYNTHETIC_APPLICANT_HEALTH_HISTORY_INFORMATION' -and
        $gaJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|diagnosis|symptom|procedureDate|clinicalStatus|verificationStatus|onset|occurrence|note|attachment|freeText|snomedCode|icd10Code|loincCode)"\s*:|TH-PAT-|4045550199')

    $gaTopics=@(@{catalogKey='ongoing-health-conditions'},@{catalogKey='behavioral-health-or-substance-use'})
    $gaBody=New-HealthHistoryInformationBody 20 ([string]$gaContext.healthHistoryInformationSnapshotFingerprint) $gaTopics $true
    Add-HealthHistoryInformationCheck 'Authentication, catalog, branch, acknowledgment, version, and snapshot failures persist nothing' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/health-history-information")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp32-catalog')} (Merge-Map $gaBody @{healthHistoryTopics=@(@{catalogKey='hypertension'})}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp32-empty')} (New-HealthHistoryInformationBody 20 ([string]$gaContext.healthHistoryInformationSnapshotFingerprint)))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp32-ack')} (Merge-Map $gaBody @{topicSelectionIsNotDiagnosisAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp32-version')} (Merge-Map $gaBody @{expectedVersion=19}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp32-snapshot')} (Merge-Map $gaBody @{healthHistoryInformationSnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_health_history_information_receipts where applicant_id='$gaId';")-eq 0)

    $flPatient=(Invoke-Scalar @"
select json_build_object('canonicalId',p.canonical_id,'pid',p.legacy_pid)
from patients p join telehealth_applicant_synthetic_promotions promotion
  on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id='$flId';
"@)|ConvertFrom-Json
    $temporaryProblemId="PRB-SP32-$([Guid]::NewGuid().ToString('N'))"
    Invoke-Scalar "insert into problems(id,patient_id,pid,type,title) values('$temporaryProblemId','$($flPatient.canonicalId)',$($flPatient.pid),'problem','Synthetic conflict fixture');" | Out-Null
    $canonicalProblemStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "delete from problems where id='$temporaryProblemId';" | Out-Null
    Add-HealthHistoryInformationCheck 'Existing canonical problem content fails closed before recording' (
        $canonicalProblemStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_health_history_information_receipts where applicant_id='$flId';")-eq 0)

    $gaKey=New-Key 'sp32-ga'
    $gaRecorded=Invoke-HealthHistoryInformation $success $gaBody $gaKey
    $gaReplay=Invoke-HealthHistoryInformation $success $gaBody $gaKey
    $gaRecordedJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-HealthHistoryInformationCheck 'Georgia records two ordered broad topics while additional collection owns route priority and creates no clinical finding' (
        $gaRecorded.applicantStatus-eq'SyntheticHealthHistoryInformationRecorded' -and
        [int]$gaRecorded.applicantVersion-eq 21 -and $gaRecorded.healthHistoryInformationRecorded -and
        @($gaRecorded.healthHistoryTopics).Count-eq 2 -and
        $gaRecorded.healthHistoryTopics[0].catalogKey-eq'ongoing-health-conditions' -and
        $gaRecorded.healthHistoryTopics[0].category-eq'ConditionOrConcern' -and
        $gaRecorded.healthHistoryTopics[1].catalogKey-eq'behavioral-health-or-substance-use' -and
        $gaRecorded.healthHistoryTopics[1].category-eq'SensitiveHistory' -and
        $gaRecorded.additionalOrUnlistedTopicsReported -and
        $gaRecorded.reviewRoute-eq'AdditionalHealthHistoryCollectionRequired' -and
        -not$gaRecorded.conditionCreated -and -not$gaRecorded.procedureCreated -and
        -not$gaRecorded.observationCreated -and -not$gaRecorded.familyMemberHistoryCreated -and
        -not$gaRecorded.questionnaireResponseCreated -and -not$gaRecorded.healthHistoryReconciled -and
        -not$gaRecorded.riskModifierEvaluated -and -not$gaRecorded.clinicalTriageChanged -and
        -not$gaRecorded.clinicianReviewCreated -and -not$gaRecorded.clinicalIntakeCompleted -and
        -not$gaRecorded.clinicalEligibilityEstablished -and -not$gaRecorded.patientRecordChanged -and
        -not$gaRecorded.requestCreated -and -not$gaRecorded.queueEntered -and
        -not$gaRecorded.careAuthorized -and -not$gaRecorded.prescribingEnabled -and
        $gaReplay.recordedAt-eq$gaRecorded.recordedAt -and
        $gaRecordedJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|diagnosis|symptom|procedureDate|clinicalStatus|verificationStatus|onset|occurrence|note|attachment|freeText|snomedCode|icd10Code|loincCode)"\s*:|TH-PAT-|4045550199')
    Add-HealthHistoryInformationCheck 'Changed idempotency reuse and a second receipt fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{additionalOrUnlistedTopicsReported=$false}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/health-history-information" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp32-second')} (Merge-Map $gaBody @{expectedVersion=21}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_health_history_information_receipts where applicant_id='$gaId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_reported_health_history_topics where applicant_id='$gaId';")-eq 2)

    $caContext=Get-HealthHistoryInformation $race
    $caBody=New-HealthHistoryInformationBody 20 ([string]$caContext.healthHistoryInformationSnapshotFingerprint)
    $caJsonBody=$caBody|ConvertTo-Json -Depth 10 -Compress
    $caRaceKey=New-Key 'sp32-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/health-history-information" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    $caReload=Get-HealthHistoryInformation $race
    $caReplay=Invoke-HealthHistoryInformation $race $caBody $caRaceKey
    Add-HealthHistoryInformationCheck 'Eight concurrent California attempts and an unchanged retry converge on one provisional-none receipt and event' (
        @($statuses|Where-Object{$_-eq 200}).Count-ge 1 -and
        @($statuses|Where-Object{$_-notin @(200,409)}).Count-eq 0 -and
        $caReplay.recordedAt-eq$caReload.recordedAt -and
        $caReload.inventoryOtherHealthHistoryStatus-eq'PatientReportsNone' -and
        $caReload.reviewRoute-eq'PendingClinicianConfirmationOfPatientReportedNone' -and
        @($caReload.healthHistoryTopics).Count-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_health_history_information_receipts where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-health-history-information-recorded';")-eq 1) @{statuses=$statuses}

    $flContext=Get-HealthHistoryInformation $fl
    $flBody=New-HealthHistoryInformationBody 20 ([string]$flContext.healthHistoryInformationSnapshotFingerprint)
    $flRecorded=Invoke-HealthHistoryInformation $fl $flBody (New-Key 'sp32-fl')
    Add-HealthHistoryInformationCheck 'Florida patient-reports-none remains provisional and requires clinician confirmation' (
        $flRecorded.applicantStatus-eq'SyntheticHealthHistoryInformationRecorded' -and
        $flRecorded.inventoryOtherHealthHistoryStatus-eq'PatientReportsNone' -and
        @($flRecorded.healthHistoryTopics).Count-eq 0 -and
        -not$flRecorded.additionalOrUnlistedTopicsReported -and
        $flRecorded.reviewRoute-eq'PendingClinicianConfirmationOfPatientReportedNone' -and
        $flRecorded.direction-match'clinician confirmation' -and
        -not$flRecorded.healthHistoryReconciled -and -not$flRecorded.prescribingEnabled)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaReload=Get-HealthHistoryInformation $success
    Add-HealthHistoryInformationCheck 'Applicant resume and reload expose the immutable receipt without canonical identity or chart identifiers' (
        $gaResume.status-eq'SyntheticHealthHistoryInformationRecorded' -and $gaResume.canonicalPatientCreated -and
        $gaReload.healthHistoryInformationRecorded -and $gaReload.applicantStatus-eq'SyntheticHealthHistoryInformationRecorded' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'"(canonicalPatientId|legacyPid|pubpid|portalSession|diagnosis|symptom|procedureDate|clinicalStatus|verificationStatus|onset|occurrence|note|attachment|freeText)"\s*:|TH-PAT-'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',r.resulting_applicant_status,'version',r.resulting_applicant_version,
  'policy',r.policy_key,'policyVersion',r.policy_version,'evidenceType',r.evidence_type,
  'inventoryStatus',r.inventory_other_health_history_status,'selectedCount',r.selected_topic_count,
  'actualCount',(select count(*) from telehealth_applicant_reported_health_history_topics topic where topic.receipt_id=r.receipt_id),
  'additional',r.additional_or_unlisted_topics_reported,'route',r.review_route,
  'reportedAck',r.patient_reported_may_be_incomplete_acknowledged,
  'diagnosisAck',r.topic_selection_is_not_diagnosis_acknowledged,
  'noTimingAck',r.no_status_or_timing_captured_acknowledged,
  'verifyAck',r.clinician_verification_required_acknowledged,
  'catalog',r.catalog_key,'coding',r.coding_system,'catalogComplete',r.catalog_complete,
  'condition',r.condition_created,'procedure',r.procedure_created,'observation',r.observation_created,
  'familyHistory',r.family_member_history_created,'questionnaire',r.questionnaire_response_created,
  'reconciled',r.health_history_reconciled,'risk',r.risk_modifier_evaluated,'triage',r.clinical_triage_changed,
  'review',r.clinician_review_created,'intake',r.clinical_intake_completed,
  'eligibility',r.clinical_eligibility_established,'patientChanged',r.patient_record_changed,
  'request',r.request_created,'queue',r.queue_entered,'care',r.care_authorized,'prescribing',r.prescribing_enabled,
  'allergyVersion',a.resulting_applicant_version,'medicationVersion',m.resulting_applicant_version,
  'inventoryVersion',i.resulting_applicant_version,'allergyIdMatch',r.allergy_information_id=a.receipt_id,
  'medicationIdMatch',r.medication_information_id=m.receipt_id,'inventoryIdMatch',r.clinical_inventory_id=i.inventory_id,
  'promotionIdMatch',r.promotion_id=i.promotion_id,'patientIdMatch',r.canonical_patient_id=i.canonical_patient_id))
from telehealth_applicant_health_history_information_receipts r
join telehealth_applicant_allergy_information_receipts a on a.receipt_id=r.allergy_information_id
join telehealth_applicant_medication_information_receipts m on m.receipt_id=r.medication_information_id
join telehealth_applicant_clinical_information_inventories i on i.inventory_id=r.clinical_inventory_id
where r.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    Add-HealthHistoryInformationCheck 'Database provenance binds three receipts to exact allergy, medication, and inventory evidence with all acknowledgments and consequences false' (
        @($evidence).Count-eq 3 -and @($evidence|Where-Object{
            $_.status-eq'SyntheticHealthHistoryInformationRecorded'-and[int]$_.version-eq 21-and
            $_.policy-eq'SYNTHETIC_APPLICANT_HEALTH_HISTORY_INFORMATION'-and[int]$_.policyVersion-eq 1-and
            $_.evidenceType-eq'PROMOTED_PATIENT_HEALTH_HISTORY_INFORMATION_RECEIPT'-and
            [int]$_.selectedCount-eq[int]$_.actualCount-and$_.reportedAck-and$_.diagnosisAck-and
            $_.noTimingAck-and$_.verifyAck-and$_.catalog-eq'avenchart-synthetic-applicant-health-history-topics-2026-08'-and
            $_.coding-eq'LOCAL_SYNTHETIC_ONLY'-and-not$_.catalogComplete-and-not$_.condition-and
            -not$_.procedure-and-not$_.observation-and-not$_.familyHistory-and-not$_.questionnaire-and
            -not$_.reconciled-and-not$_.risk-and-not$_.triage-and-not$_.review-and-not$_.intake-and
            -not$_.eligibility-and-not$_.patientChanged-and-not$_.request-and-not$_.queue-and
            -not$_.care-and-not$_.prescribing-and[int]$_.allergyVersion-eq 20-and
            [int]$_.medicationVersion-eq 19-and[int]$_.inventoryVersion-eq 18-and
            $_.allergyIdMatch-and$_.medicationIdMatch-and$_.inventoryIdMatch-and$_.promotionIdMatch-and$_.patientIdMatch}).Count-eq 3)

    Add-HealthHistoryInformationCheck 'Append-only and deferred parent-child count guards reject mutation or inconsistent evidence' (
        (Test-HealthHistoryInformationMutationRejected "update telehealth_applicant_health_history_information_receipts set review_route='ClinicianHealthHistoryReviewRequired' where applicant_id='$gaId';") -and
        (Test-HealthHistoryInformationMutationRejected "delete from telehealth_applicant_reported_health_history_topics where applicant_id='$gaId';") -and
        (Test-HealthHistoryInformationMutationRejected @"
begin;
set constraints all deferred;
insert into telehealth_applicant_reported_health_history_topics(
 topic_id,receipt_id,applicant_id,practice_id,facility_id,topic_ordinal,catalog_key,
 display_name,category,catalog_version,coding_system,snomed_ct_mapped,icd10_cm_mapped,loinc_mapped)
select gen_random_uuid(),receipt_id,applicant_id,practice_id,facility_id,3,'family-health-history',
 'Family health history','FamilyHistory',1,'LOCAL_SYNTHETIC_ONLY',false,false,false
from telehealth_applicant_health_history_information_receipts where applicant_id='$gaId';
commit;
"@))

    $after=Get-HealthHistoryInformationCounts
    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    Add-HealthHistoryInformationCheck 'Three receipts and two broad-topic selections change no canonical clinical, patient, financial, request, queue, or care record' (
        [int]$after.receipts-[int]$before.receipts-eq 3 -and
        [int]$after.reportedTopics-[int]$before.reportedTopics-eq 2 -and
        [int]$after.patients-eq[int]$before.patients -and
        [int]$after.problems-eq[int]$before.problems -and
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
  and table_name in ('telehealth_applicant_health_history_information_receipts','telehealth_applicant_reported_health_history_topics')
  and column_name in ('diagnosis','symptom','procedure_date','clinical_status','verification_status','severity',
                      'onset','occurrence','clinical_date','note','attachment','free_text','snomed_code','icd10_code','loinc_code');
"@)
    Add-HealthHistoryInformationCheck 'Schema has no prohibited clinical-detail or terminology columns and all applicants stop at the bounded receipt' (
        $prohibitedColumns-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id in ('$gaId','$caId','$flId') and status='SyntheticHealthHistoryInformationRecorded' and version=21;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_health_history_information_receipts where applicant_id in ('$gaId','$caId','$flId');")-eq 3)
}
catch {
    $passed=$false
    Add-HealthHistoryInformationCheck 'Applicant health-history-information proof execution' $false ("$($_.Exception.Message) | $($_.ScriptStackTrace)")
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0035'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant health-history-information proof failed. See $resultPath"}
Write-Host "Applicant health-history-information proof passed ($($checks.Count) checks). Artifact: $resultPath"
