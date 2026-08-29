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
    throw 'Telehealth applicant request participation-context proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestRenderingCandidate.ps1') -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$participationApplicants = @($candidateApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-participation-context.json'
$participationChecks = [System.Collections.Generic.List[object]]::new()
$participationPassed = $true

function Add-ParticipationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:participationChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:participationPassed=$false}
}
function Participation-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/participation-context"
}
function New-ParticipationBody([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=9
        contextSnapshotFingerprint=[string]$Ready.contextSnapshotFingerprint
        syntheticDataConfirmed=$true
        npiNotCredentialAcknowledged=$true
        realAuthorityNotVerifiedAcknowledged=$true
        exactParticipationStillRequiredAcknowledged=$true
    }
}
function Copy-ParticipationBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-ParticipationMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedParticipationPosts([object]$Applicant,[object]$Body) {
    $url="$ApiBaseUrl$(Participation-Path $Applicant.Id)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $headers=Applicant-Headers $Applicant.Secret (New-Key "sp49-contention-$ordinal")
        $jobs+=Start-Job -ScriptBlock {
            param($RequestUrl,$RequestHeaders,$RequestBody)
            try {
                $invokeParameters=@{Uri=$RequestUrl;Method='Post';Headers=$RequestHeaders;ContentType='application/json';Body=$RequestBody;TimeoutSec=60}
                $response=Invoke-WebRequest @invokeParameters
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
    foreach($applicant in $participationApplicants) {
        $applicant | Add-Member -NotePropertyName ParticipationReady -NotePropertyValue (
            Get-ApplicantStep $applicant (Participation-Path $applicant.Id))
    }

    Add-ParticipationCheck 'GA, CA, and FL receive one masked effective-dated prerequisite context without real verification or participation claims' (
        @($participationApplicants|Where-Object{
            $_.ParticipationReady.requestStatus-eq'Verification' -and
            $_.ParticipationReady.requestVersion-eq 9 -and
            $_.ParticipationReady.confirmationReady -and -not$_.ParticipationReady.confirmationCompleted -and
            $_.ParticipationReady.authorityJurisdiction-in@('GA','CA','FL') -and
            $_.ParticipationReady.maskedProviderReference-match'^Synthetic provider ••••[0-9]{4}$' -and
            $_.ParticipationReady.maskedBillingProviderReference-eq'Synthetic billing provider ••••8800' -and
            $_.ParticipationReady.authorityContextStatus-eq'SYNTHETIC_ACTIVE' -and
            $_.ParticipationReady.practitionerRoleContextStatus-eq'SYNTHETIC_ACTIVE' -and
            $_.ParticipationReady.organizationAffiliationContextStatus-eq'SYNTHETIC_ACTIVE' -and
            $_.ParticipationReady.billingContractContextStatus-eq'SYNTHETIC_ACTIVE' -and
            $_.ParticipationReady.contextPurpose-eq'PARTICIPATION_EVALUATION_PREREQUISITES_ONLY' -and
            -not$_.ParticipationReady.participationEvaluationContextConfirmed -and
            -not$_.ParticipationReady.realStateAuthorityVerified -and
            -not$_.ParticipationReady.realCredentialingVerified -and
            -not$_.ParticipationReady.renderingPhysicianNetworkChecked -and
            -not$_.ParticipationReady.exactNetworkConfirmed -and
            $null-eq$_.ParticipationReady.PSObject.Properties['candidateStaffId'] -and
            $null-eq$_.ParticipationReady.PSObject.Properties['contractReference'] -and
            $null-eq$_.ParticipationReady.PSObject.Properties['practitionerRoleReference'] -and
            $null-eq$_.ParticipationReady.PSObject.Properties['stateAuthorityReference']
        }).Count-eq 3)

    $spare=$participationApplicants[2]
    $validSpare=New-ParticipationBody $spare.ParticipationReady
    $missing=Copy-ParticipationBody $validSpare;$missing.PSObject.Properties.Remove('npiNotCredentialAcknowledged')
    $falseAck=Copy-ParticipationBody $validSpare;$falseAck.realAuthorityNotVerifiedAcknowledged=$false
    $stale=Copy-ParticipationBody $validSpare;$stale.expectedRequestVersion=10
    $badSnapshot=Copy-ParticipationBody $validSpare;$badSnapshot.contextSnapshotFingerprint=('0'*64)
    Add-ParticipationCheck 'Missing/foreign access, missing or false acknowledgment, stale version, and changed snapshot fail before context evidence' (
        (Get-StepStatus 'GET' $spare (Participation-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (Participation-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (Participation-Path $spare.Id) $spare.Secret $missing (New-Key 'sp49-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Participation-Path $spare.Id) $spare.Secret $falseAck (New-Key 'sp49-false'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Participation-Path $spare.Id) $spare.Secret $stale (New-Key 'sp49-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Participation-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp49-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_participation_contexts where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $applicantIdsSql=@($participationApplicants|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    $candidateBefore=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by applicant_id) from telehealth_applicant_request_rendering_candidate_selections v where applicant_id in ($applicantIdsSql);"
    $results=@{};$keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$participationApplicants[$index]
        $body=New-ParticipationBody $applicant.ParticipationReady
        $key=New-Key 'sp49-context';$keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-ApplicantStep $applicant (Participation-Path $applicant.Id) $body $key
    }
    $contended=$participationApplicants[1]
    $contentionStatuses=Invoke-ContendedParticipationPosts $contended (New-ParticipationBody $contended.ParticipationReady)
    $results[$contended.Id]=Get-ApplicantStep $contended (Participation-Path $contended.Id)
    Add-ParticipationCheck 'First-writer contention creates one prerequisite context and every request advances only to Verification version 10' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($participationApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'Verification' -and $result.requestVersion-eq 10 -and
            $result.confirmationCompleted -and -not$result.confirmationReady -and
            $result.participationEvaluationContextConfirmed -and
            -not$result.realStateAuthorityVerified -and -not$result.realCredentialingVerified -and
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

    $first=$participationApplicants[0]
    $firstBody=New-ParticipationBody $first.ParticipationReady
    $replay=Post-ApplicantStep $first (Participation-Path $first.Id) $firstBody $keys[$first.Id]
    $changed=Copy-ParticipationBody $firstBody;$changed.expectedRequestVersion=10
    Add-ParticipationCheck 'Exact semantic replay is stable while changed-key reuse and a second command fail closed' (
        $replay.confirmationId-eq$results[$first.Id].confirmationId -and
        $replay.confirmedAt-eq$results[$first.Id].confirmedAt -and
        (Get-StepStatus 'POST' $first (Participation-Path $first.Id) $first.Secret $changed $keys[$first.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first (Participation-Path $first.Id) $first.Secret $firstBody (New-Key 'sp49-second'))-eq 409)

    $candidateDriftStatus=$null
    try {
        $null=Invoke-Scalar "update staff set npi='99999999' where id=101 and npi='18888101'; select count(*) from staff where id=101 and npi='99999999';"
        $candidateDriftStatus=Get-StepStatus 'GET' $first (Participation-Path $first.Id) $first.Secret
    }
    finally {
        $null=Invoke-Scalar "update staff set npi='18888101' where id=101 and npi='99999999'; select count(*) from staff where id=101 and npi='18888101';"
    }
    Add-ParticipationCheck 'Completed context fails closed when the selected server-owned candidate roster binding drifts' (
        $candidateDriftStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from staff where id=101 and npi='18888101' and role='provider' and facility_id=10 and active;")-eq 1)

    $requestIdsSql=@($participationApplicants|ForEach-Object{"'$($_.ParticipationReady.requestId)'::uuid"})-join','
    $candidateAfter=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by applicant_id) from telehealth_applicant_request_rendering_candidate_selections v where applicant_id in ($applicantIdsSql);"
    Add-ParticipationCheck 'Context evidence and events are append-only while candidate, patient, insurance, and downstream state remain unchanged' (
        $candidateBefore-eq$candidateAfter -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_participation_contexts where request_id in ($requestIdsSql);")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_participation_contexts where request_id in ($requestIdsSql) and participation_evaluation_context_confirmed and not real_state_authority_verified and not real_credentialing_verified and not rendering_physician_assigned and not rendering_physician_network_checked and not exact_network_confirmed and not coverage_verified and not external_call_performed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=10 and action='applicant-participation-context-confirmed';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='Verification' and version=10 and triage_outcome='TelehealthEligible' and ready_at is null and appointment_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from information_schema.columns where table_schema='public' and table_name='telehealth_applicant_request_participation_contexts' and column_name in ('candidate_npi','member_id','group_number','billing_tin','license_number','protected_payload');")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from insurance_records where lower(patient_id) in (select lower(canonical_patient_id) from telehealth_applicant_request_participation_contexts where request_id in ($requestIdsSql));")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_selections where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_verifications where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0)

    $confirmationId=Invoke-Scalar "select confirmation_id from telehealth_applicant_request_participation_contexts where applicant_id='$($first.Id)'::uuid;"
    Add-ParticipationCheck 'Context evidence rejects real-verification, exact-network, financial, downstream, and deletion mutation' (
        (Test-ParticipationMutationRejected "update telehealth_applicant_request_participation_contexts set real_state_authority_verified=true where confirmation_id='$confirmationId'::uuid; select 'bad';") -and
        (Test-ParticipationMutationRejected "update telehealth_applicant_request_participation_contexts set real_credentialing_verified=true where confirmation_id='$confirmationId'::uuid; select 'bad';") -and
        (Test-ParticipationMutationRejected "update telehealth_applicant_request_participation_contexts set rendering_physician_network_checked=true where confirmation_id='$confirmationId'::uuid; select 'bad';") -and
        (Test-ParticipationMutationRejected "update telehealth_applicant_request_participation_contexts set exact_network_confirmed=true where confirmation_id='$confirmationId'::uuid; select 'bad';") -and
        (Test-ParticipationMutationRejected "update telehealth_applicant_request_participation_contexts set financial_acknowledgment_created=true where confirmation_id='$confirmationId'::uuid; select 'bad';") -and
        (Test-ParticipationMutationRejected "delete from telehealth_applicant_request_participation_contexts where confirmation_id='$confirmationId'::uuid; select 'bad';"))
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$participationPassed;checkCount=$participationChecks.Count;checks=$participationChecks}
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$participationPassed){throw "Telehealth applicant request participation-context proof failed. See $resultPath"}
Write-Host "Telehealth applicant request participation-context proof passed $($participationChecks.Count) checks."
