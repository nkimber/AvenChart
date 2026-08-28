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
    throw 'Telehealth applicant practice-review proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantPreRequestReadiness.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-practice-review-submission.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-PracticeReviewCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function Get-PracticeReview([object]$Applicant,[string]$Secret=$Applicant.Secret) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/practice-review-submission" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Secret} -TimeoutSec 30
}
function New-PracticeReviewBody([object]$Projection) {
    @{
        expectedVersion=$Projection.applicantVersion
        practiceReviewSnapshotFingerprint=$Projection.practiceReviewSnapshotFingerprint
        patientReportedInformationAcknowledged=$true
        practiceMayRequestInformationOrDeclineAcknowledged=$true
        noTelehealthRequestOrCareQueueAcknowledged=$true
        worseningSymptomsRequireImmediateActionAcknowledged=$true
    }
}
function Submit-PracticeReview([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/practice-review-submission" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 10)
}
function Get-PracticeReviewStatus([object]$Applicant,[hashtable]$Body,[string]$Key) {
    try {
        [int](Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/practice-review-submission" `
            -Method Post -Headers @{
                'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
                'X-Idempotency-Key'=$Key
            } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 10)).StatusCode
    }
    catch {
        if ($null -ne $_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}
function Test-PracticeReviewMutationRejected([string]$Sql) {
    docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -v ON_ERROR_STOP=1 -c $Sql *> $null
    $LASTEXITCODE -ne 0
}

try {
    $applicants=@($success,$race,$fl)
    $ids=@($applicants|ForEach-Object{[string]$_.Created.applicantId})
    $idList=($ids|ForEach-Object{"'$_'"})-join','
    $before=(Invoke-Scalar @"
select json_build_object(
  'cases',(select count(*) from telehealth_prospective_practice_review_cases),
  'submissions',(select count(*) from telehealth_applicant_practice_review_submissions),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims),
  'insuranceRecords',(select count(*) from insurance_records),
  'medications',(select count(*) from medications),
  'prescriptions',(select count(*) from prescriptions),
  'allergies',(select count(*) from allergies),
  'problems',(select count(*) from problems));
"@)|ConvertFrom-Json
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ($idList);
"@)|ConvertFrom-Json -AsHashtable

    $projections=@($applicants|ForEach-Object{Get-PracticeReview $_})
    $allowedNames=@('applicantId','applicantVersion','applicantStatus','practiceReviewSnapshotFingerprint',
      'policyKey','policyVersion','reviewRoute','submissionReady','submitted','practiceReviewCaseId',
      'reviewStatus','submittedAt','patientReportedInformationAcknowledged',
      'practiceMayRequestInformationOrDeclineAcknowledged','noTelehealthRequestOrCareQueueAcknowledged',
      'worseningSymptomsRequireImmediateActionAcknowledged','staffReviewCreated','clinicianReviewCreated',
      'practiceAccepted','patientRecordChanged','telehealthRequestCreated','patientCareQueueEntered',
      'clinicianQueueEntered','appointmentCreated','encounterCreated','careAuthorized','prescribingEnabled',
      'billingEnabled','claimCreated','integrationEnabled','externalCallPerformed','direction','limitations')
    $actualNames=@($projections[0].PSObject.Properties.Name)
    Add-PracticeReviewCheck 'Applicant retrieval is minimized to the stable review contract' (
        @(Compare-Object ($allowedNames|Sort-Object) ($actualNames|Sort-Object)).Count-eq 0 -and
        @($projections|Where-Object{$_.submitted-or-not$_.submissionReady-or$_.staffReviewCreated}).Count-eq 0) $actualNames

    $wrongAccess=$false
    try { $null=Get-PracticeReview $success $race.Secret } catch { $wrongAccess=$_.Exception.Response.StatusCode-eq 404 }
    Add-PracticeReviewCheck 'Cross-applicant access remains privacy-safe' $wrongAccess

    $gaBody=New-PracticeReviewBody $projections[0]
    $caBody=New-PracticeReviewBody $projections[1]
    $flBody=New-PracticeReviewBody $projections[2]
    $gaKey="practice-review-ga-$([Guid]::NewGuid().ToString('N'))"
    $flKey="practice-review-fl-$([Guid]::NewGuid().ToString('N'))"
    $gaResult=Submit-PracticeReview $success $gaBody $gaKey
    $flResult=Submit-PracticeReview $fl $flBody $flKey
    Add-PracticeReviewCheck 'Georgia and Florida create only one pending practice-review work item each' (
        $gaResult.submitted-and$flResult.submitted-and
        $gaResult.reviewStatus-eq'PendingPracticeReview'-and$flResult.reviewStatus-eq'PendingPracticeReview'-and
        $gaResult.staffReviewCreated-and$flResult.staffReviewCreated-and
        -not$gaResult.telehealthRequestCreated-and-not$gaResult.patientCareQueueEntered-and
        -not$flResult.telehealthRequestCreated-and-not$flResult.clinicianQueueEntered)

    $gaReplay=Submit-PracticeReview $success $gaBody $gaKey
    Add-PracticeReviewCheck 'Exact retry returns the same immutable case and version' (
        $gaReplay.practiceReviewCaseId-eq$gaResult.practiceReviewCaseId-and
        $gaReplay.applicantVersion-eq$gaResult.applicantVersion)
    $changedBody=$gaBody.Clone();$changedBody.expectedVersion=[int]$gaBody.expectedVersion+1
    Add-PracticeReviewCheck 'Changed idempotency reuse and a second semantic command fail closed' (
        (Get-PracticeReviewStatus $success $changedBody $gaKey)-eq 409 -and
        (Get-PracticeReviewStatus $success $gaBody "practice-review-second-$([Guid]::NewGuid().ToString('N'))")-eq 409)

    $raceKey="practice-review-race-$([Guid]::NewGuid().ToString('N'))"
    $racePath="$ApiBaseUrl/api/telehealth/v1/applicants/$($race.Created.applicantId)/practice-review-submission"
    $raceHeaders=@{'X-AvenChart-Telehealth-Applicant-Key'=$race.Secret;'X-Idempotency-Key'=$raceKey}
    $raceJson=$caBody|ConvertTo-Json -Compress
    $raceStatuses=1..8|ForEach-Object -Parallel {
        try { [int](Invoke-WebRequest $using:racePath -Method Post -Headers $using:raceHeaders -ContentType 'application/json' -Body $using:raceJson -TimeoutSec 40).StatusCode }
        catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
    } -ThrottleLimit 8
    $raceReplay=Submit-PracticeReview $race $caBody $raceKey
    Add-PracticeReviewCheck 'Eight-way exact contention converges on one California case' (
        @($raceStatuses|Where-Object{$_-notin@(200,409)}).Count-eq 0 -and
        @($raceStatuses|Where-Object{$_-eq 200}).Count-ge 1 -and
        $raceReplay.reviewStatus-eq'PendingPracticeReview') $raceStatuses

    $gaId=[string]$success.Created.applicantId
    Add-PracticeReviewCheck 'Case and submission evidence are append-only' (
        (Test-PracticeReviewMutationRejected "update telehealth_prospective_practice_review_cases set case_status='PendingPracticeReview' where applicant_id='$gaId';") -and
        (Test-PracticeReviewMutationRejected "delete from telehealth_applicant_practice_review_submissions where applicant_id='$gaId';"))

    $persisted=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',s.resulting_applicant_status,'version',s.resulting_applicant_version,
  'reviewStatus',c.case_status,'route',s.review_route,'policy',s.policy_key,
  'allAcknowledged',s.patient_reported_information_acknowledged
    and s.practice_may_request_information_or_decline_acknowledged
    and s.no_telehealth_request_or_care_queue_acknowledged
    and s.worsening_symptoms_require_immediate_action_acknowledged,
  'boundedConsequences',s.staff_review_created and not s.clinician_review_created
    and not s.practice_accepted and not s.patient_record_changed
    and not s.telehealth_request_created and not s.patient_care_queue_entered
    and not s.clinician_queue_entered and not s.appointment_created
    and not s.encounter_created and not s.care_authorized and not s.prescribing_enabled
    and not s.billing_enabled and not s.claim_created and not s.integration_enabled
    and not s.external_call_performed))
from telehealth_applicant_practice_review_submissions s
join telehealth_prospective_practice_review_cases c on c.case_id=s.case_id
where s.applicant_id in ($idList);
"@)|ConvertFrom-Json
    Add-PracticeReviewCheck 'Persisted provenance records exactly one staff-review consequence and no care consequence' (
        @($persisted).Count-eq 3 -and
        @($persisted|Where-Object{(-not $_.allAcknowledged) -or (-not $_.boundedConsequences) -or $_.status -ne 'SyntheticPracticeReviewSubmitted' -or $_.reviewStatus -ne 'PendingPracticeReview'}).Count-eq 0) $persisted

    $after=(Invoke-Scalar @"
select json_build_object(
  'cases',(select count(*) from telehealth_prospective_practice_review_cases),
  'submissions',(select count(*) from telehealth_applicant_practice_review_submissions),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims),
  'insuranceRecords',(select count(*) from insurance_records),
  'medications',(select count(*) from medications),
  'prescriptions',(select count(*) from prescriptions),
  'allergies',(select count(*) from allergies),
  'problems',(select count(*) from problems));
"@)|ConvertFrom-Json
    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ($idList);
"@)|ConvertFrom-Json -AsHashtable
    $forbidden=@('requests','queueEntries','appointments','encounters','claims','insuranceRecords','medications','prescriptions','allergies','problems')
    Add-PracticeReviewCheck 'Three submissions add exactly three review cases and receipts with zero forbidden downstream delta' (
        [int]$after.cases-[int]$before.cases-eq 3 -and
        [int]$after.submissions-[int]$before.submissions-eq 3 -and
        @($forbidden|Where-Object{[int]$after.$_-ne[int]$before.$_}).Count-eq 0 -and
        ($patientHashesBefore|ConvertTo-Json -Compress)-eq($patientHashesAfter|ConvertTo-Json -Compress))

    $prohibitedColumns=[int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name in (
  'telehealth_prospective_practice_review_cases','telehealth_applicant_practice_review_submissions')
and column_name in ('diagnosis','symptom','dose','directions','reaction','criticality','severity',
  'note','attachment','free_text','legal_name','date_of_birth','email','phone','address','member_id','payer',
  'priority','assigned_to','queue_position','doctor_id');
"@)
    Add-PracticeReviewCheck 'Schema contains no protected-detail, priority, assignment, doctor, or queue-position column' ($prohibitedColumns-eq 0)
}
catch {
    $passed=$false
    Add-PracticeReviewCheck 'Applicant practice-review proof execution' $false ("$($_.Exception.Message) | $($_.ScriptStackTrace)")
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0038'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant practice-review proof failed. See $resultPath"}
Write-Host "Applicant practice-review proof passed ($($checks.Count) checks). Artifact: $resultPath"
