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
    throw 'Telehealth applicant request participation-evaluation proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestParticipationContext.ps1') -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$evaluationApplicants = @($participationApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-participation-evaluation.json'
$evaluationChecks = [System.Collections.Generic.List[object]]::new()
$evaluationPassed = $true

function Add-EvaluationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:evaluationChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:evaluationPassed=$false}
}
function Evaluation-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/participation-evaluation"
}
function New-EvaluationBody([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=10
        evaluationSnapshotFingerprint=[string]$Ready.evaluationSnapshotFingerprint
        syntheticDataConfirmed=$true
        exactTupleScopeAcknowledged=$true
        noCoverageGuaranteeAcknowledged=$true
        realVerificationStillRequiredAcknowledged=$true
    }
}
function Copy-EvaluationBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-EvaluationMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedEvaluationPosts([object]$Applicant,[object]$Body) {
    $url="$ApiBaseUrl$(Evaluation-Path $Applicant.Id)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $headers=Applicant-Headers $Applicant.Secret (New-Key "sp50-contention-$ordinal")
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
    try {
        $jobs|Wait-Job -Timeout 90|Out-Null
        @($jobs|Receive-Job)|Sort-Object
    }
    finally {$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}
}

try {
    foreach($applicant in $evaluationApplicants) {
        $applicant | Add-Member -NotePropertyName EvaluationReady -NotePropertyValue (
            Get-ApplicantStep $applicant (Evaluation-Path $applicant.Id))
    }

    Add-EvaluationCheck 'GA, CA, and FL receive one bounded masked exact-tuple evaluation with no real-verification claims' (
        @($evaluationApplicants|Where-Object{
            $ready=$_.EvaluationReady
            $ready.requestStatus-eq'Verification' -and $ready.requestVersion-eq 10 -and
            $ready.evaluationReady -and -not$ready.evaluationCompleted -and
            $ready.currentLocationStateCode-in@('GA','CA','FL') -and
            $ready.sourceMode-eq'NON_PRODUCTION' -and
            $ready.compatibilityTarget-eq'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
            $ready.evaluationScope-eq'BILLING_ENTITY_RENDERING_PROVIDER_NETWORK_LOCATION_SERVICE_MODALITY_NEW_PATIENT' -and
            $ready.maskedProviderReference-match'^Synthetic provider ••••[0-9]{4}$' -and
            $ready.maskedBillingProviderReference-eq'Synthetic billing provider ••••8800' -and
            -not$ready.syntheticParticipationEvaluated -and -not$ready.realStateAuthorityVerified -and
            -not$ready.realCredentialingVerified -and -not$ready.renderingPhysicianNetworkChecked -and
            -not$ready.exactNetworkConfirmed -and
            $null-eq$ready.PSObject.Properties['candidateStaffId'] -and
            $null-eq$ready.PSObject.Properties['candidateNpi'] -and
            $null-eq$ready.PSObject.Properties['contractReference']
        }).Count-eq 3)

    $spare=$evaluationApplicants[2]
    $validSpare=New-EvaluationBody $spare.EvaluationReady
    $missing=Copy-EvaluationBody $validSpare;$missing.PSObject.Properties.Remove('exactTupleScopeAcknowledged')
    $falseAck=Copy-EvaluationBody $validSpare;$falseAck.noCoverageGuaranteeAcknowledged=$false
    $stale=Copy-EvaluationBody $validSpare;$stale.expectedRequestVersion=11
    $badSnapshot=Copy-EvaluationBody $validSpare;$badSnapshot.evaluationSnapshotFingerprint=('0'*64)
    Add-EvaluationCheck 'Missing or foreign access, acknowledgment failures, stale version, and changed snapshot fail before evidence' (
        (Get-StepStatus 'GET' $spare (Evaluation-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (Evaluation-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (Evaluation-Path $spare.Id) $spare.Secret $missing (New-Key 'sp50-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Evaluation-Path $spare.Id) $spare.Secret $falseAck (New-Key 'sp50-false'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Evaluation-Path $spare.Id) $spare.Secret $stale (New-Key 'sp50-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Evaluation-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp50-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_participation_evaluations where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $applicantIdsSql=@($evaluationApplicants|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    $contextBefore=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by applicant_id) from telehealth_applicant_request_participation_contexts v where applicant_id in ($applicantIdsSql);"
    $staffBefore=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by id) from staff v where id in (101,104,107);"
    $results=@{};$keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$evaluationApplicants[$index]
        $body=New-EvaluationBody $applicant.EvaluationReady
        $key=New-Key 'sp50-evaluation';$keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-ApplicantStep $applicant (Evaluation-Path $applicant.Id) $body $key
    }
    $contended=$evaluationApplicants[1]
    $contentionStatuses=Invoke-ContendedEvaluationPosts $contended (New-EvaluationBody $contended.EvaluationReady)
    $results[$contended.Id]=Get-ApplicantStep $contended (Evaluation-Path $contended.Id)
    Add-EvaluationCheck 'First-writer contention creates one exact synthetic evaluation and every request advances only to Verification version 11' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($evaluationApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'Verification' -and $result.requestVersion-eq 11 -and
            $result.evaluationCompleted -and -not$result.evaluationReady -and
            $result.businessOutcome-eq'SyntheticExactParticipationMatched' -and
            $result.syntheticParticipationEvaluated -and $result.syntheticBillingEntityInNetwork -and
            $result.syntheticRenderingProviderInNetwork -and $result.syntheticPlanNetworkMatched -and
            $result.syntheticServiceLocationMatched -and $result.syntheticNewPatientsAccepted -and
            $result.syntheticExactNetworkMatched -and
            -not$result.realStateAuthorityVerified -and -not$result.realCredentialingVerified -and
            -not$result.renderingPhysicianAssigned -and -not$result.renderingPhysicianNetworkChecked -and
            -not$result.exactNetworkConfirmed -and -not$result.coverageVerified -and
            -not$result.operationalReviewCreated -and -not$result.practiceAccepted -and
            -not$result.patientCareQueueEntered -and -not$result.clinicianQueueEntered -and
            -not$result.appointmentCreated -and -not$result.encounterCreated -and
            -not$result.careAuthorized -and -not$result.integrationEnabled -and -not$result.externalCallPerformed
        }).Count-eq 3)

    $first=$evaluationApplicants[0]
    $firstBody=New-EvaluationBody $first.EvaluationReady
    $replay=Post-ApplicantStep $first (Evaluation-Path $first.Id) $firstBody $keys[$first.Id]
    $changed=Copy-EvaluationBody $firstBody;$changed.expectedRequestVersion=11
    Add-EvaluationCheck 'Exact semantic replay is stable while changed-key reuse and a second command fail closed' (
        $replay.evaluationId-eq$results[$first.Id].evaluationId -and
        $replay.evaluatedAt-eq$results[$first.Id].evaluatedAt -and
        (Get-StepStatus 'POST' $first (Evaluation-Path $first.Id) $first.Secret $changed $keys[$first.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first (Evaluation-Path $first.Id) $first.Secret $firstBody (New-Key 'sp50-second'))-eq 409)

    $candidateDriftStatus=$null
    try {
        $null=Invoke-Scalar "update staff set npi='99999999' where id=101 and npi='18888101'; select count(*) from staff where id=101 and npi='99999999';"
        $candidateDriftStatus=Get-StepStatus 'GET' $first (Evaluation-Path $first.Id) $first.Secret
    }
    finally {
        $null=Invoke-Scalar "update staff set npi='18888101' where id=101 and npi='99999999'; select count(*) from staff where id=101 and npi='18888101';"
    }
    Add-EvaluationCheck 'Completed evaluation fails closed when the server-owned rendering-provider binding drifts' (
        $candidateDriftStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from staff where id=101 and npi='18888101' and role='provider' and facility_id=10 and active;")-eq 1)

    $requestIdsSql=@($evaluationApplicants|ForEach-Object{"'$($_.EvaluationReady.requestId)'::uuid"})-join','
    $contextAfter=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by applicant_id) from telehealth_applicant_request_participation_contexts v where applicant_id in ($applicantIdsSql);"
    $staffAfter=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by id) from staff v where id in (101,104,107);"
    Add-EvaluationCheck 'Evaluation evidence and events are append-only while context, staff, insurance, and downstream state remain unchanged' (
        $contextBefore-eq$contextAfter -and $staffBefore-eq$staffAfter -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_participation_evaluations where request_id in ($requestIdsSql);")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_participation_evaluations where request_id in ($requestIdsSql) and synthetic_participation_evaluated and synthetic_billing_entity_in_network and synthetic_rendering_provider_in_network and synthetic_plan_network_matched and synthetic_service_location_matched and synthetic_new_patients_accepted and synthetic_exact_network_matched and not real_state_authority_verified and not real_credentialing_verified and not rendering_physician_assigned and not exact_network_confirmed and not coverage_verified and not external_call_performed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=11 and action='applicant-participation-evaluated';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='Verification' and version=11 and triage_outcome='TelehealthEligible' and ready_at is null and appointment_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from insurance_records where lower(patient_id) in (select lower(canonical_patient_id) from telehealth_applicant_request_participation_evaluations where request_id in ($requestIdsSql));")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_selections where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_verifications where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0)

    $evaluationId=Invoke-Scalar "select evaluation_id from telehealth_applicant_request_participation_evaluations where applicant_id='$($first.Id)'::uuid;"
    Add-EvaluationCheck 'Evaluation evidence rejects synthetic-result weakening, real-verification escalation, downstream escalation, and deletion' (
        (Test-EvaluationMutationRejected "update telehealth_applicant_request_participation_evaluations set synthetic_exact_network_matched=false where evaluation_id='$evaluationId'::uuid; select 'bad';") -and
        (Test-EvaluationMutationRejected "update telehealth_applicant_request_participation_evaluations set real_state_authority_verified=true where evaluation_id='$evaluationId'::uuid; select 'bad';") -and
        (Test-EvaluationMutationRejected "update telehealth_applicant_request_participation_evaluations set exact_network_confirmed=true where evaluation_id='$evaluationId'::uuid; select 'bad';") -and
        (Test-EvaluationMutationRejected "update telehealth_applicant_request_participation_evaluations set coverage_verified=true where evaluation_id='$evaluationId'::uuid; select 'bad';") -and
        (Test-EvaluationMutationRejected "delete from telehealth_applicant_request_participation_evaluations where evaluation_id='$evaluationId'::uuid; select 'bad';"))
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$evaluationPassed;checkCount=$evaluationChecks.Count;checks=$evaluationChecks}
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$evaluationPassed){throw "Telehealth applicant request participation-evaluation proof failed. See $resultPath"}
Write-Host "Telehealth applicant request participation-evaluation proof passed $($evaluationChecks.Count) checks."
