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
    throw 'Telehealth applicant operational-review-submission proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestParticipationEvaluation.ps1') -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$submissionApplicants = @($evaluationApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-operational-review-submission.json'
$submissionChecks = [System.Collections.Generic.List[object]]::new()
$submissionPassed = $true

function Add-SubmissionCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:submissionChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:submissionPassed=$false}
}
function Submission-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/operational-review-submission"
}
function New-SubmissionBody([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=11
        submissionSnapshotFingerprint=[string]$Ready.submissionSnapshotFingerprint
        syntheticEvidenceAcknowledged=$true
        noCoverageGuaranteeAcknowledged=$true
        practiceReviewPendingAcknowledged=$true
        noCareRelationshipAcknowledged=$true
    }
}
function Copy-SubmissionBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-SubmissionMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedSubmissionPosts([object]$Applicant,[object]$Body) {
    $url="$ApiBaseUrl$(Submission-Path $Applicant.Id)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $headers=Applicant-Headers $Applicant.Secret (New-Key "sp51-contention-$ordinal")
        $jobs+=Start-Job -ScriptBlock {
            param($RequestUrl,$RequestHeaders,$RequestBody)
            try {
                $response=Invoke-WebRequest -Uri $RequestUrl -Method Post -Headers $RequestHeaders -ContentType 'application/json' -Body $RequestBody -TimeoutSec 60
                [int]$response.StatusCode
            }
            catch {
                if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}
            }
        } -ArgumentList $url,$headers,$bodyJson
    }
    try {$jobs|Wait-Job -Timeout 90|Out-Null;@($jobs|Receive-Job)|Sort-Object}
    finally {$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}
}

try {
    foreach($applicant in $submissionApplicants) {
        $applicant | Add-Member -NotePropertyName SubmissionReady -NotePropertyValue (
            Get-ApplicantStep $applicant (Submission-Path $applicant.Id))
    }

    Add-SubmissionCheck 'GA, CA, and FL receive one minimized no-edit submission review without real or downstream claims' (
        @($submissionApplicants|Where-Object{
            $ready=$_.SubmissionReady
            $ready.requestStatus-eq'Verification' -and $ready.requestVersion-eq 11 -and
            $ready.submissionReady -and -not$ready.submissionCompleted -and
            $ready.currentLocationStateCode-in@('GA','CA','FL') -and
            $ready.sourceMode-eq'NON_PRODUCTION' -and
            $ready.compatibilityTarget-eq'AVENCHART_SYNTHETIC_OPERATIONAL_REVIEW_V1' -and
            $ready.maskedProviderReference-match'^Synthetic provider ••••[0-9]{4}$' -and
            $ready.maskedBillingProviderReference-eq'Synthetic billing provider ••••8800' -and
            -not$ready.syntheticAutomatedChecksComplete -and -not$ready.operationalReviewCreated -and
            -not$ready.practiceAccepted -and -not$ready.coverageVerified -and
            $null-eq$ready.PSObject.Properties['participationEvaluationId'] -and
            $null-eq$ready.PSObject.Properties['candidateStaffId'] -and
            $null-eq$ready.PSObject.Properties['candidateNpi']
        }).Count-eq 3)

    $spare=$submissionApplicants[2]
    $validSpare=New-SubmissionBody $spare.SubmissionReady
    $missing=Copy-SubmissionBody $validSpare;$missing.PSObject.Properties.Remove('practiceReviewPendingAcknowledged')
    $falseAck=Copy-SubmissionBody $validSpare;$falseAck.noCareRelationshipAcknowledged=$false
    $stale=Copy-SubmissionBody $validSpare;$stale.expectedRequestVersion=12
    $badSnapshot=Copy-SubmissionBody $validSpare;$badSnapshot.submissionSnapshotFingerprint=('0'*64)
    Add-SubmissionCheck 'Access, acknowledgment, stale-version, and changed-snapshot failures write no evidence' (
        (Get-StepStatus 'GET' $spare (Submission-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (Submission-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (Submission-Path $spare.Id) $spare.Secret $missing (New-Key 'sp51-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Submission-Path $spare.Id) $spare.Secret $falseAck (New-Key 'sp51-false'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Submission-Path $spare.Id) $spare.Secret $stale (New-Key 'sp51-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Submission-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp51-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_operational_review_submissions where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $results=@{};$keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$submissionApplicants[$index]
        $body=New-SubmissionBody $applicant.SubmissionReady
        $key=New-Key 'sp51-submission';$keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-ApplicantStep $applicant (Submission-Path $applicant.Id) $body $key
    }
    $contended=$submissionApplicants[1]
    $contentionStatuses=Invoke-ContendedSubmissionPosts $contended (New-SubmissionBody $contended.SubmissionReady)
    $results[$contended.Id]=Get-ApplicantStep $contended (Submission-Path $contended.Id)
    Add-SubmissionCheck 'First-writer contention creates one submission and advances only requests to OperationalReview version 12' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($submissionApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'OperationalReview' -and $result.requestVersion-eq 12 -and
            $result.submissionCompleted -and -not$result.submissionReady -and
            $result.businessOutcome-eq'SyntheticRequestSubmittedForOperationalReview' -and
            $result.syntheticAutomatedChecksComplete -and $result.operationalReviewCreated -and
            -not$result.practiceAccepted -and -not$result.coverageVerified -and
            -not$result.patientContacted -and -not$result.patientCareQueueEntered -and
            -not$result.clinicianQueueEntered -and -not$result.doctorSearchStarted -and
            -not$result.appointmentCreated -and -not$result.encounterCreated -and
            -not$result.consentCreated -and -not$result.careAuthorized -and
            -not$result.integrationEnabled -and -not$result.externalCallPerformed
        }).Count-eq 3)

    $first=$submissionApplicants[0]
    $firstBody=New-SubmissionBody $first.SubmissionReady
    $replay=Post-ApplicantStep $first (Submission-Path $first.Id) $firstBody $keys[$first.Id]
    $changed=Copy-SubmissionBody $firstBody;$changed.expectedRequestVersion=12
    Add-SubmissionCheck 'Exact replay is stable while changed-key reuse and a second command fail closed' (
        $replay.submittedAt-eq$results[$first.Id].submittedAt -and
        (Get-StepStatus 'POST' $first (Submission-Path $first.Id) $first.Secret $changed $keys[$first.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first (Submission-Path $first.Id) $first.Secret $firstBody (New-Key 'sp51-second'))-eq 409)

    $applicantIdsSql=@($submissionApplicants|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    $requestIdsSql=@($submissionApplicants|ForEach-Object{"'$($_.SubmissionReady.requestId)'::uuid"})-join','
    Add-SubmissionCheck 'Evidence and events are exact while coverage, financial, queue, appointment, encounter, and care state remain absent' (
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_operational_review_submissions where applicant_id in ($applicantIdsSql) and synthetic_automated_checks_complete and operational_review_created and not practice_accepted and not coverage_verified and not patient_care_queue_entered and not clinician_queue_entered and not appointment_created and not encounter_created and not care_authorized and not external_call_performed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=12 and action='applicant-operational-review-submitted' and from_status='Verification' and to_status='OperationalReview';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='OperationalReview' and version=12 and ready_at is null and appointment_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from insurance_records where lower(patient_id) in (select lower(canonical_patient_id) from telehealth_applicant_request_operational_review_submissions where request_id in ($requestIdsSql));")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_selections where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0)

    $submissionId=Invoke-Scalar "select submission_id from telehealth_applicant_request_operational_review_submissions where applicant_id='$($first.Id)'::uuid;"
    Add-SubmissionCheck 'Submission evidence rejects acknowledgment weakening, acceptance escalation, downstream escalation, and deletion' (
        (Test-SubmissionMutationRejected "update telehealth_applicant_request_operational_review_submissions set synthetic_evidence_acknowledged=false where submission_id='$submissionId'::uuid; select 'bad';") -and
        (Test-SubmissionMutationRejected "update telehealth_applicant_request_operational_review_submissions set practice_accepted=true where submission_id='$submissionId'::uuid; select 'bad';") -and
        (Test-SubmissionMutationRejected "update telehealth_applicant_request_operational_review_submissions set patient_care_queue_entered=true where submission_id='$submissionId'::uuid; select 'bad';") -and
        (Test-SubmissionMutationRejected "delete from telehealth_applicant_request_operational_review_submissions where submission_id='$submissionId'::uuid; select 'bad';"))

    $admin=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username='admin';password='pass'}|ConvertTo-Json) -TimeoutSec 20
    $adminHeaders=@{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations'}
    $review=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/operational-review" -Headers $adminHeaders -TimeoutSec 20
    $crossHeaders=$adminHeaders.Clone();$crossHeaders['X-AvenChart-Facility-Id']='11'
    $crossStatus=try{[int](Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/admin/operational-review" -Headers $crossHeaders -TimeoutSec 20).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    Add-SubmissionCheck 'Configured administrators see the minimal operational-review projection while another facility fails closed' (
        @($review.requests|Where-Object{$_.requestId-in@($submissionApplicants|ForEach-Object{$_.SubmissionReady.requestId})}).Count-eq 3 -and
        $crossStatus-in@(403,404))
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$submissionPassed;checkCount=$submissionChecks.Count;checks=$submissionChecks}
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$submissionPassed){throw "Telehealth applicant operational-review-submission proof failed. See $resultPath"}
Write-Host "Telehealth applicant operational-review-submission proof passed $($submissionChecks.Count) checks."
