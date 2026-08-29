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
    throw 'Telehealth applicant request complaint-triage proof is local-only.'
}

$allApplicants = [System.Collections.Generic.List[object]]::new()
$priorPurposeCategory = $env:AVENCHART_TELEHEALTH_TEST_PURPOSE_CATEGORY
foreach ($batch in 1..2) {
    $env:AVENCHART_TELEHEALTH_TEST_PURPOSE_CATEGORY = if ($batch -eq 2) { 'sleep' } else { 'migraine' }
    . (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestLocation.ps1') `
        -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName
    $allApplicants.Add([pscustomobject]@{Id=$gaId;Secret=$gaSecret;State='GA'})
    $allApplicants.Add([pscustomobject]@{Id=$caId;Secret=$caSecret;State='CA'})
    $allApplicants.Add([pscustomobject]@{Id=$flId;Secret=$flSecret;State='FL'})
}
if ($null -eq $priorPurposeCategory) {
    Remove-Item Env:AVENCHART_TELEHEALTH_TEST_PURPOSE_CATEGORY -ErrorAction SilentlyContinue
}
else {
    $env:AVENCHART_TELEHEALTH_TEST_PURPOSE_CATEGORY = $priorPurposeCategory
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-complaint-triage.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-ComplaintTriageCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:passed=$false}
}
function Safety-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/safety"
}
function Complaint-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/complaint-triage"
}
function Get-ApplicantStep([object]$Applicant,[string]$Path) {
    Invoke-RestMethod "$ApiBaseUrl$Path" -Headers (Applicant-Headers $Applicant.Secret) -TimeoutSec 30
}
function Post-ApplicantStep([object]$Applicant,[string]$Path,[object]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl$Path" -Method Post `
        -Headers (Applicant-Headers $Applicant.Secret $Key) -ContentType 'application/json' `
        -Body ($Body|ConvertTo-Json -Compress -Depth 8) -TimeoutSec 40
}
function Get-StepStatus(
    [string]$Method,[object]$Applicant,[string]$Path,[string]$Secret,
    [object]$Body=$null,[string]$Key='') {
    try {
        $parameters=@{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=(Applicant-Headers $Secret $Key);TimeoutSec=30}
        if($null-ne$Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Compress -Depth 8}
        $null=Invoke-WebRequest @parameters
        200
    }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}
function New-UniversalPassBody([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=2
        contextSnapshotFingerprint=[string]$Ready.contextSnapshotFingerprint
        currentLocationStateCode=[string]$Ready.currentLocationStateCode
        currentLocationConfirmed=$true
        callbackNumberConfirmed=$true
        syntheticDataConfirmed=$true
        hasEmergencyWarning=$false
        severeOrWorsening=$false
        requiresHandsOnExam=$false
        unsure=$false
    }
}
function New-MigraineAnswers([string]$Scenario) {
    $answers=[ordered]@{
        suddenOrWorstOnset='No'
        newNeurologicOrVisionChange='No'
        feverOrStiffNeck='No'
        recentHeadInjury='No'
        pregnantOrPostpartum='No'
        cancerOrImmunocompromised='No'
        knownSimilarPattern='Yes'
        persistentVomiting='No'
    }
    if($Scenario-eq'Emergency'){$answers.suddenOrWorstOnset='Yes'}
    elseif($Scenario-eq'UrgentInPerson'){$answers.feverOrStiffNeck='Yes'}
    elseif($Scenario-eq'ClinicalReview'){$answers.recentHeadInjury='NotSure'}
    $answers
}
function New-SleepAnswers([string]$Scenario) {
    $answers=[ordered]@{
        selfHarmThoughts='No'
        maniaOrPsychosis='No'
        dangerousSomnolence='No'
        withdrawalConcern='No'
        breathingPausesOrSevereSnoring='No'
        pregnantOrComplexMedicationConcern='No'
        controlledSedativeRequest='No'
        uncomplicatedSleepDifficulty='Yes'
    }
    if($Scenario-eq'InPersonRequired'){$answers.breathingPausesOrSevereSnoring='Yes'}
    elseif($Scenario-eq'Unsupported'){$answers.controlledSedativeRequest='Yes'}
    elseif($Scenario-eq'Emergency'){$answers.selfHarmThoughts='Yes'}
    elseif($Scenario-eq'ClinicalReview'){$answers.breathingPausesOrSevereSnoring='NotSure'}
    $answers
}
function New-ComplaintBody([object]$Applicant,[object]$Ready,[string]$Scenario) {
    [ordered]@{
        expectedRequestVersion=3
        contextSnapshotFingerprint=[string]$Ready.contextSnapshotFingerprint
        currentLocationStateCode=[string]$Applicant.State
        currentLocationConfirmed=$true
        callbackNumberConfirmed=$true
        syntheticDataConfirmed=$true
        migraine=$(if($Ready.complaintCategory-eq'migraine'){New-MigraineAnswers $Scenario}else{$null})
        sleep=$(if($Ready.complaintCategory-eq'sleep'){New-SleepAnswers $Scenario}else{$null})
    }
}
function Copy-ComplaintBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-ComplaintMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}

try {
    foreach($applicant in $allApplicants) {
        $safetyReady=Get-ApplicantStep $applicant (Safety-Path $applicant.Id)
        $safetyBody=New-UniversalPassBody $safetyReady
        $null=Post-ApplicantStep $applicant (Safety-Path $applicant.Id) $safetyBody (New-Key 'sp43-universal-pass')
        $applicant | Add-Member -NotePropertyName Ready -NotePropertyValue (
            Get-ApplicantStep $applicant (Complaint-Path $applicant.Id))
    }

    $migraine=@($allApplicants|Where-Object{$_.Ready.complaintCategory-eq'migraine'})
    $sleep=@($allApplicants|Where-Object{$_.Ready.complaintCategory-eq'sleep'})
    Add-ComplaintTriageCheck 'Three-state complaint projections are private, masked, publication-blocked, and include both controlled categories' (
        $migraine.Count-eq 3 -and $sleep.Count-eq 3 -and
        @($allApplicants|Where-Object{
            $_.Ready.requestStatus-eq'SafetyScreening' -and $_.Ready.requestVersion-eq 3 -and
            $_.Ready.assessmentReady -and -not$_.Ready.assessmentCreated -and
            $_.Ready.clinicalContentStatus-eq'UNAPPROVED_SYNTHETIC' -and
            $_.Ready.medicalDirectorApprovalRequired -and
            -not$_.Ready.medicalDirectorApprovalRecorded -and
            -not$_.Ready.clinicalGoldenCasePackApproved -and
            -not$_.Ready.productionPublicationAllowed -and
            $_.Ready.maskedCallbackPhone-match'^\*{3}-\*{3}-[0-9]{4}$'
        }).Count-eq 6)

    $spare=$sleep[2]
    $spareBody=New-ComplaintBody $spare $spare.Ready 'ClinicalReview'
    $missing=Copy-ComplaintBody $spareBody
    if($missing.migraine){$missing.migraine.suddenOrWorstOnset=$null}
    else{$missing.sleep.selfHarmThoughts=$null}
    $wrongSet=Copy-ComplaintBody $spareBody
    if($spare.Ready.complaintCategory-eq'migraine'){$wrongSet.migraine=$null;$wrongSet.sleep=New-SleepAnswers 'ClinicalReview'}
    else{$wrongSet.sleep=$null;$wrongSet.migraine=New-MigraineAnswers 'ClinicalReview'}
    $stale=Copy-ComplaintBody $spareBody;$stale.expectedRequestVersion=4
    $badSnapshot=Copy-ComplaintBody $spareBody;$badSnapshot.contextSnapshotFingerprint=('0'*64)
    Add-ComplaintTriageCheck 'Missing/foreign access, missing answer, category substitution, stale version, and source snapshot drift fail before evidence' (
        (Get-StepStatus 'GET' $spare (Complaint-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (Complaint-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (Complaint-Path $spare.Id) $spare.Secret $missing (New-Key 'sp43-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Complaint-Path $spare.Id) $spare.Secret $wrongSet (New-Key 'sp43-category'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Complaint-Path $spare.Id) $spare.Secret $stale (New-Key 'sp43-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Complaint-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp43-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_complaint_triage_assessments where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $cases=@(
        [pscustomobject]@{Applicant=$migraine[0];Scenario='Emergency';Outcome='Emergency';Status='EmergencyRedirected';Disposition='EmergencyCareNow'},
        [pscustomobject]@{Applicant=$migraine[1];Scenario='UrgentInPerson';Outcome='UrgentInPerson';Status='InPersonRecommended';Disposition='PromptInPersonCare'},
        [pscustomobject]@{Applicant=$migraine[2];Scenario='TelehealthEligible';Outcome='TelehealthEligible';Status='Intake';Disposition='SyntheticVideoEvaluationCandidate'},
        [pscustomobject]@{Applicant=$sleep[0];Scenario='InPersonRequired';Outcome='InPersonRequired';Status='InPersonRecommended';Disposition='InPersonCareRequired'},
        [pscustomobject]@{Applicant=$sleep[1];Scenario='Unsupported';Outcome='Unsupported';Status='Unsupported';Disposition='TelehealthServiceUnsupported'},
        [pscustomobject]@{Applicant=$sleep[2];Scenario='ClinicalReview';Outcome='ClinicalReview';Status='ClinicalReview';Disposition='ClinicalReviewRequired'}
    )
    $results=@{}
    $keys=@{}
    foreach($case in $cases) {
        $body=New-ComplaintBody $case.Applicant $case.Applicant.Ready $case.Scenario
        $key=New-Key 'sp43-outcome'
        $keys[$case.Applicant.Id]=$key
        $results[$case.Applicant.Id]=Post-ApplicantStep $case.Applicant `
            (Complaint-Path $case.Applicant.Id) $body $key
    }
    Add-ComplaintTriageCheck 'All six deterministic outcomes map to exact request version 4 states without publication or downstream authority' (
        @($cases|Where-Object{
            $result=$results[$_.Applicant.Id]
            $result.outcome-eq$_.Outcome -and $result.requestStatus-eq$_.Status -and
            $result.publicDisposition-eq$_.Disposition -and $result.requestVersion-eq 4 -and
            $result.assessmentCreated -and -not$result.assessmentReady -and
            -not$result.medicalDirectorApprovalRecorded -and
            -not$result.clinicalGoldenCasePackApproved -and
            -not$result.productionPublicationAllowed -and
            -not$result.clinicalReviewCreated -and -not$result.intakeSnapshotCreated -and
            -not$result.patientContacted -and -not$result.patientCareQueueEntered -and
            -not$result.clinicianQueueEntered -and -not$result.doctorSearchStarted -and
            -not$result.appointmentCreated -and -not$result.encounterCreated -and
            -not$result.careAuthorized -and -not$result.integrationEnabled -and
            $null-eq$result.PSObject.Properties['answerValues'] -and
            $null-eq$result.PSObject.Properties['answersFingerprint'] -and
            $null-eq$result.PSObject.Properties['firedRuleCodes'] -and
            $null-eq$result.PSObject.Properties['reasonCodes']
        }).Count-eq 6)

    $first=$cases[0]
    $firstBody=New-ComplaintBody $first.Applicant $first.Applicant.Ready $first.Scenario
    $replay=Post-ApplicantStep $first.Applicant (Complaint-Path $first.Applicant.Id) `
        $firstBody $keys[$first.Applicant.Id]
    $changed=Copy-ComplaintBody $firstBody;$changed.currentLocationStateCode='FL'
    Add-ComplaintTriageCheck 'Exact semantic replay is stable while changed-key reuse and a second command fail closed' (
        $replay.outcome-eq$first.Outcome -and $replay.evaluatedAt-eq$results[$first.Applicant.Id].evaluatedAt -and
        (Get-StepStatus 'POST' $first.Applicant (Complaint-Path $first.Applicant.Id) $first.Applicant.Secret $changed $keys[$first.Applicant.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first.Applicant (Complaint-Path $first.Applicant.Id) $first.Applicant.Secret $firstBody (New-Key 'sp43-second'))-eq 409)

    $receiptCount=[int](Invoke-Scalar 'select count(*) from telehealth_applicant_request_complaint_triage_assessments;')
    $invalidEvidence=[int](Invoke-Scalar @"
select count(*) from telehealth_applicant_request_complaint_triage_assessments
where cardinality(answer_keys)<>8 or cardinality(answer_values)<>8
   or cardinality(fired_rule_codes)<1
   or cardinality(fired_rule_codes)<>cardinality(reason_codes)
   or clinical_content_status<>'UNAPPROVED_SYNTHETIC'
   or not medical_director_approval_required or medical_director_approval_recorded
   or clinical_golden_case_pack_approved or production_publication_allowed;
"@)
    $requestIdsSql=@($cases|ForEach-Object{"'$($_.Applicant.Ready.requestId)'::uuid"})-join','
    Add-ComplaintTriageCheck 'Database evidence retains typed answers and ordered rules while every clinical and downstream consequence remains absent' (
        $receiptCount-eq 6 -and $invalidEvidence-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_triage_assessments where request_id in ($requestIdsSql);")-eq 12 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=4 and action='applicant-complaint-triage-evaluated';")-eq 6 -and
        [int](Invoke-Scalar "select count(*) from telehealth_intake_snapshots where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_video_sessions where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_consultation_contexts where request_id in ($requestIdsSql);")-eq 0)

    $firstReceipt=Invoke-Scalar "select receipt_id from telehealth_applicant_request_complaint_triage_assessments where applicant_id='$($first.Applicant.Id)'::uuid;"
    Add-ComplaintTriageCheck 'Complaint receipts and generic assessments are append-only and the false-only publication gate rejects mutation' (
        (Test-ComplaintMutationRejected "update telehealth_applicant_request_complaint_triage_assessments set production_publication_allowed=true where receipt_id='$firstReceipt'::uuid; select 'bad';") -and
        (Test-ComplaintMutationRejected "delete from telehealth_applicant_request_complaint_triage_assessments where receipt_id='$firstReceipt'::uuid; select 'bad';") -and
        (Test-ComplaintMutationRejected "update telehealth_triage_assessments set outcome='ClinicalReview' where assessment_id=(select assessment_id from telehealth_applicant_request_complaint_triage_assessments where receipt_id='$firstReceipt'::uuid); select 'bad';"))

    $spareResult=$results[$spare.Id]
    Add-ComplaintTriageCheck 'NotSure is stored as explicit evidence and routes to review rather than eligibility' (
        $spareResult.outcome-eq'ClinicalReview' -and $spareResult.requestStatus-eq'ClinicalReview' -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_complaint_triage_assessments where applicant_id='$($spare.Id)'::uuid and 'NotSure'=any(answer_values) and fired_rule_codes::text like '%REVIEW-UNKNOWN-ANSWER%';")-eq 1)
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{
        generatedAt=(Get-Date).ToUniversalTime().ToString('o')
        apiBaseUrl=$ApiBaseUrl
        database=$DatabaseName
        passed=$passed
        checkCount=$checks.Count
        checks=$checks
    }
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$passed){throw "Telehealth applicant request complaint-triage proof failed. See $resultPath"}
Write-Host "Telehealth applicant request complaint-triage proof passed $($checks.Count) checks."
