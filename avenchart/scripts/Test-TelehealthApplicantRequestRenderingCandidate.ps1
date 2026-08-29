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
    throw 'Telehealth applicant request rendering-candidate proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestPracticeNetwork.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$candidateApplicants = @($practiceNetworkApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-rendering-candidate.json'
$candidateChecks = [System.Collections.Generic.List[object]]::new()
$candidatePassed = $true

function Add-CandidateCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:candidateChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:candidatePassed=$false}
}
function Candidate-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/rendering-candidate"
}
function New-CandidateBody([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=8
        candidateSnapshotFingerprint=[string]$Ready.candidateSnapshotFingerprint
        syntheticDataConfirmed=$true
        candidateOnlyScopeAcknowledged=$true
        noAssignmentAcknowledged=$true
        networkCheckStillRequiredAcknowledged=$true
    }
}
function Copy-CandidateBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-CandidateMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedCandidatePosts([object]$Applicant,[object]$Body) {
    $url="$ApiBaseUrl$(Candidate-Path $Applicant.Id)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $headers=Applicant-Headers $Applicant.Secret (New-Key "sp48-contention-$ordinal")
        $jobs+=Start-Job -ScriptBlock {
            param($RequestUrl,$RequestHeaders,$RequestBody)
            try {
                $response=Invoke-WebRequest -Uri $RequestUrl -Method Post -Headers $RequestHeaders `
                    -ContentType 'application/json' -Body $RequestBody -TimeoutSec 60
                [int]$response.StatusCode
            }
            catch {
                if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}
            }
        } -ArgumentList $url,$headers,$bodyJson
    }
    try {
        $jobs|Wait-Job -Timeout 90|Out-Null
        @($jobs|Receive-Job)|Sort-Object
    }
    finally {$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}
}

try {
    foreach($applicant in $candidateApplicants) {
        $applicant | Add-Member -NotePropertyName CandidateReady -NotePropertyValue (
            Get-ApplicantStep $applicant (Candidate-Path $applicant.Id))
    }

    Add-CandidateCheck 'GA, CA, and FL receive one server-owned masked network-evaluation candidate without assignment or participation claims' (
        @($candidateApplicants|Where-Object{
            $_.CandidateReady.requestStatus-eq'Verification' -and
            $_.CandidateReady.requestVersion-eq 8 -and
            $_.CandidateReady.selectionReady -and -not$_.CandidateReady.selectionCompleted -and
            $_.CandidateReady.currentLocationStateCode-in@('GA','CA','FL') -and
            $_.CandidateReady.practiceNetworkBusinessOutcome-eq'PracticeInNetworkAcceptingNewPatients' -and
            $_.CandidateReady.maskedProviderReference-match'^Synthetic provider ••••[0-9]{4}$' -and
            $_.CandidateReady.candidatePurpose-eq'NETWORK_EVALUATION_ONLY' -and
            -not$_.CandidateReady.candidateSelectedForNetworkEvaluation -and
            -not$_.CandidateReady.renderingPhysicianAssigned -and
            -not$_.CandidateReady.renderingPhysicianNetworkChecked -and
            -not$_.CandidateReady.exactNetworkConfirmed -and
            $null-eq$_.CandidateReady.PSObject.Properties['candidateStaffId'] -and
            $null-eq$_.CandidateReady.PSObject.Properties['candidateNpi']
        }).Count-eq 3)

    $spare=$candidateApplicants[2]
    $validSpare=New-CandidateBody $spare.CandidateReady
    $missing=Copy-CandidateBody $validSpare;$missing.PSObject.Properties.Remove('candidateOnlyScopeAcknowledged')
    $falseAck=Copy-CandidateBody $validSpare;$falseAck.noAssignmentAcknowledged=$false
    $stale=Copy-CandidateBody $validSpare;$stale.expectedRequestVersion=9
    $badSnapshot=Copy-CandidateBody $validSpare;$badSnapshot.candidateSnapshotFingerprint=('0'*64)
    Add-CandidateCheck 'Missing/foreign access, missing or false acknowledgment, stale version, and changed snapshot fail before selection evidence' (
        (Get-StepStatus 'GET' $spare (Candidate-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (Candidate-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (Candidate-Path $spare.Id) $spare.Secret $missing (New-Key 'sp48-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Candidate-Path $spare.Id) $spare.Secret $falseAck (New-Key 'sp48-false'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Candidate-Path $spare.Id) $spare.Secret $stale (New-Key 'sp48-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Candidate-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp48-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_rendering_candidate_selections where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $applicantIdsSql=@($candidateApplicants|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    $networkBefore=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by applicant_id) from telehealth_applicant_request_practice_network_verifications v where applicant_id in ($applicantIdsSql);"
    $results=@{};$keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$candidateApplicants[$index]
        $body=New-CandidateBody $applicant.CandidateReady
        $key=New-Key 'sp48-candidate';$keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-ApplicantStep $applicant (Candidate-Path $applicant.Id) $body $key
    }
    $contended=$candidateApplicants[1]
    $contentionStatuses=Invoke-ContendedCandidatePosts $contended (New-CandidateBody $contended.CandidateReady)
    $results[$contended.Id]=Get-ApplicantStep $contended (Candidate-Path $contended.Id)
    Add-CandidateCheck 'First-writer contention creates one candidate selection and every request advances only to Verification version 9' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($candidateApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'Verification' -and $result.requestVersion-eq 9 -and
            $result.selectionCompleted -and -not$result.selectionReady -and
            $result.candidateSelectedForNetworkEvaluation -and
            -not$result.renderingPhysicianAssigned -and
            -not$result.renderingPhysicianNetworkChecked -and -not$result.exactNetworkConfirmed -and
            -not$result.canonicalCoverageCreated -and -not$result.coverageSelected -and
            -not$result.coverageVerified -and -not$result.financialRouteCreated -and
            -not$result.operationalReviewCreated -and -not$result.practiceAccepted -and
            -not$result.patientContacted -and -not$result.patientCareQueueEntered -and
            -not$result.clinicianQueueEntered -and -not$result.doctorSearchStarted -and
            -not$result.queuePositionAssigned -and -not$result.appointmentCreated -and
            -not$result.encounterCreated -and -not$result.consentCreated -and
            -not$result.careAuthorized -and -not$result.integrationEnabled -and
            -not$result.externalCallPerformed
        }).Count-eq 3)

    $first=$candidateApplicants[0]
    $firstBody=New-CandidateBody $first.CandidateReady
    $replay=Post-ApplicantStep $first (Candidate-Path $first.Id) $firstBody $keys[$first.Id]
    $changed=Copy-CandidateBody $firstBody;$changed.expectedRequestVersion=9
    Add-CandidateCheck 'Exact semantic replay is stable while changed-key reuse and a second command fail closed' (
        $replay.selectionId-eq$results[$first.Id].selectionId -and
        $replay.selectedAt-eq$results[$first.Id].selectedAt -and
        (Get-StepStatus 'POST' $first (Candidate-Path $first.Id) $first.Secret $changed $keys[$first.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first (Candidate-Path $first.Id) $first.Secret $firstBody (New-Key 'sp48-second'))-eq 409)

    $candidateDriftStatus=$null
    try {
        $null=Invoke-Scalar "update staff set npi='99999999' where id=101 and npi='18888101'; select count(*) from staff where id=101 and npi='99999999';"
        $candidateDriftStatus=Get-StepStatus 'GET' $first (Candidate-Path $first.Id) $first.Secret
    }
    finally {
        $null=Invoke-Scalar "update staff set npi='18888101' where id=101 and npi='99999999'; select count(*) from staff where id=101 and npi='18888101';"
    }
    Add-CandidateCheck 'Completed selection fails closed when the server-owned candidate roster binding drifts' (
        $candidateDriftStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from staff where id=101 and npi='18888101' and role='provider' and facility_id=10 and active;")-eq 1)

    $requestIdsSql=@($candidateApplicants|ForEach-Object{"'$($_.CandidateReady.requestId)'::uuid"})-join','
    $networkAfter=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by applicant_id) from telehealth_applicant_request_practice_network_verifications v where applicant_id in ($applicantIdsSql);"
    Add-CandidateCheck 'Candidate evidence and events are append-only while network, patient, insurance, and downstream state remain unchanged' (
        $networkBefore-eq$networkAfter -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_rendering_candidate_selections where request_id in ($requestIdsSql);")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_rendering_candidate_selections where request_id in ($requestIdsSql) and candidate_selected_for_network_evaluation and not rendering_physician_assigned and not rendering_physician_network_checked and not exact_network_confirmed and not coverage_verified and not external_call_performed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=9 and action='applicant-rendering-candidate-selected';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='Verification' and version=9 and triage_outcome='TelehealthEligible' and ready_at is null and appointment_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from information_schema.columns where table_schema='public' and table_name='telehealth_applicant_request_rendering_candidate_selections' and column_name in ('candidate_npi','member_id','group_number','billing_tin','protected_payload');")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from insurance_records where lower(patient_id) in (select lower(canonical_patient_id) from telehealth_applicant_request_rendering_candidate_selections where request_id in ($requestIdsSql));")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_selections where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_verifications where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0)

    $selectionId=Invoke-Scalar "select selection_id from telehealth_applicant_request_rendering_candidate_selections where applicant_id='$($first.Id)'::uuid;"
    Add-CandidateCheck 'Candidate evidence rejects assignment, network, exact-network, financial, downstream, and deletion mutation' (
        (Test-CandidateMutationRejected "update telehealth_applicant_request_rendering_candidate_selections set rendering_physician_assigned=true where selection_id='$selectionId'::uuid; select 'bad';") -and
        (Test-CandidateMutationRejected "update telehealth_applicant_request_rendering_candidate_selections set rendering_physician_network_checked=true where selection_id='$selectionId'::uuid; select 'bad';") -and
        (Test-CandidateMutationRejected "update telehealth_applicant_request_rendering_candidate_selections set exact_network_confirmed=true where selection_id='$selectionId'::uuid; select 'bad';") -and
        (Test-CandidateMutationRejected "update telehealth_applicant_request_rendering_candidate_selections set financial_acknowledgment_created=true where selection_id='$selectionId'::uuid; select 'bad';") -and
        (Test-CandidateMutationRejected "delete from telehealth_applicant_request_rendering_candidate_selections where selection_id='$selectionId'::uuid; select 'bad';"))
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$candidatePassed;checkCount=$candidateChecks.Count;checks=$candidateChecks}
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$candidatePassed){throw "Telehealth applicant request rendering-candidate proof failed. See $resultPath"}
Write-Host "Telehealth applicant request rendering-candidate proof passed $($candidateChecks.Count) checks."
