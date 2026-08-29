# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5001',
    [ValidatePattern('^[a-z][a-z0-9_]{2,62}$')]
    [string]$DatabaseName = 'avenchart',
    [switch]$VerifyApplicantQueueStatus
)

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth applicant request queue-authorization proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestOperationalReviewSubmission.ps1') -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$queueAuthorizationApplicants = @($submissionApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-queue-authorization.json'
$queueAuthorizationChecks = [System.Collections.Generic.List[object]]::new()
$queueAuthorizationPassed = $true

function Add-QueueAuthorizationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:queueAuthorizationChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:queueAuthorizationPassed=$false}
}
function Queue-Authorization-Path([string]$RequestId) {
    "/api/telehealth/v1/admin/applicant-requests/$RequestId/queue-authorization"
}
function Applicant-Queue-Status-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/queue-status"
}
function Get-Applicant-Queue-Status([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl$(Applicant-Queue-Status-Path $Applicant.Id)" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function New-Queue-Authorization-Body([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=12
        authorizationSnapshotFingerprint=[string]$Ready.authorizationSnapshotFingerprint
        syntheticEvidenceReviewed=$true
        noCoverageGuaranteeAcknowledged=$true
        practiceAcceptsForQueueAcknowledged=$true
        queueNotCareAcknowledged=$true
    }
}
function Copy-Queue-Authorization-Body([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Get-AdminStatus([string]$Method,[string]$Path,[hashtable]$Headers,[object]$Body=$null,[string]$Key='') {
    try {
        $request=@{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=$Headers;TimeoutSec=60}
        if($null-ne$Body){$request.ContentType='application/json';$request.Body=$Body|ConvertTo-Json -Compress -Depth 8}
        if($Key){$request.Headers=$Headers.Clone();$request.Headers['X-Idempotency-Key']=$Key}
        [int](Invoke-WebRequest @request).StatusCode
    }
    catch {
        if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}
    }
}
function Post-Queue-Authorization([object]$Applicant,[object]$Body,[string]$Key,[hashtable]$Headers) {
    $postHeaders=$Headers.Clone();$postHeaders['X-Idempotency-Key']=$Key
    Invoke-RestMethod "$ApiBaseUrl$(Queue-Authorization-Path $Applicant.SubmissionReady.requestId)" -Method Post -Headers $postHeaders -ContentType 'application/json' -Body ($Body|ConvertTo-Json -Compress -Depth 8) -TimeoutSec 60
}
function Test-Queue-Authorization-MutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedQueueAuthorizations([object]$Applicant,[object]$Body,[hashtable]$Headers) {
    $url="$ApiBaseUrl$(Queue-Authorization-Path $Applicant.SubmissionReady.requestId)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $requestHeaders=$Headers.Clone();$requestHeaders['X-Idempotency-Key']=New-Key "sp52-contention-$ordinal"
        $jobs+=Start-Job -ScriptBlock {
            param($RequestUrl,$RequestHeaders,$RequestBody)
            try {
                $response=Invoke-WebRequest -Uri $RequestUrl -Method Post -Headers $RequestHeaders -ContentType 'application/json' -Body $RequestBody -TimeoutSec 60
                [int]$response.StatusCode
            }
            catch {
                if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}
            }
        } -ArgumentList $url,$requestHeaders,$bodyJson
    }
    try {$jobs|Wait-Job -Timeout 90|Out-Null;@($jobs|Receive-Job)|Sort-Object}
    finally {$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}
}

try {
    $admin=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username='admin';password='pass'}|ConvertTo-Json) -TimeoutSec 20
    $adminHeaders=@{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations'}
    $crossFacilityHeaders=@{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='11';'X-AvenChart-Purpose-Of-Use'='healthcare-operations'}
    $provider=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username='gold-provider-01';password='pass'}|ConvertTo-Json) -TimeoutSec 20
    $providerHeaders=@{'X-AvenChart-Session'=$provider.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='treatment'}
    $operational=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/operational-review" -Headers $adminHeaders -TimeoutSec 20
    Add-QueueAuthorizationCheck 'Operational-review projection labels every new-patient request for the dedicated path' (
        @($operational.requests|Where-Object{
            $_.requestId-in@($queueAuthorizationApplicants|ForEach-Object{$_.SubmissionReady.requestId}) -and $_.applicantOriginated
        }).Count-eq 3)

    foreach($applicant in $queueAuthorizationApplicants) {
        $requestId=$applicant.SubmissionReady.requestId
        $applicant | Add-Member -NotePropertyName QueueAuthorizationReady -NotePropertyValue (
            Invoke-RestMethod "$ApiBaseUrl$(Queue-Authorization-Path $requestId)" -Headers $adminHeaders -TimeoutSec 30)
    }
    Add-QueueAuthorizationCheck 'GA, CA, and FL receive minimized no-edit staff packets with all real consequences false' (
        @($queueAuthorizationApplicants|Where-Object{
            $ready=$_.QueueAuthorizationReady
            $ready.requestStatus-eq'OperationalReview' -and $ready.requestVersion-eq 12 -and
            $ready.authorizationReady -and -not$ready.authorizationCompleted -and
            $ready.sourceMode-eq'NON_PRODUCTION' -and
            $ready.compatibilityTarget-eq'AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1' -and
            $ready.currentLocationStateCode-in@('GA','CA','FL') -and
            $ready.maskedProviderReference-match'^Synthetic provider ••••[0-9]{4}$' -and
            $ready.maskedBillingProviderReference-eq'Synthetic billing provider ••••8800' -and
            -not$ready.practiceAccepted -and -not$ready.renderingPhysicianAssigned -and
            -not$ready.coverageVerified -and -not$ready.queuePositionAssigned -and
            -not$ready.encounterCreated -and -not$ready.careAuthorized -and
            $null-eq$ready.PSObject.Properties['applicantId'] -and
            $null-eq$ready.PSObject.Properties['candidateStaffId'] -and
            $null-eq$ready.PSObject.Properties['canonicalPatientId']
        }).Count-eq 3)
    if($VerifyApplicantQueueStatus) {
        $reviewingStatuses=@($queueAuthorizationApplicants|ForEach-Object{Get-Applicant-Queue-Status $_})
        Add-QueueAuthorizationCheck 'Applicant-owned status remains reviewing before the practice authorizes queue entry' (
            @($reviewingStatuses|Where-Object{
                $_.requestStatus-eq'OperationalReview' -and $_.requestVersion-eq 12 -and
                $_.phase-eq'Reviewing' -and $_.headline-eq'Reviewing your request' -and
                -not$_.practiceAccepted -and -not$_.doctorSearchStarted -and
                $null-eq$_.approximateRequestsAhead -and -not$_.positionIsApproximate -and
                -not$_.exactQueuePositionAssigned -and -not$_.waitEstimateAvailable -and
                -not$_.renderingPhysicianAssigned -and -not$_.renderingPhysicianIdentityDisclosed -and
                -not$_.coverageVerified -and -not$_.consentCreated -and -not$_.careAuthorized -and
                -not$_.integrationEnabled -and -not$_.externalCallPerformed
            }).Count-eq 3)
    }

    $spare=$queueAuthorizationApplicants[2]
    $validSpare=New-Queue-Authorization-Body $spare.QueueAuthorizationReady
    $missing=Copy-Queue-Authorization-Body $validSpare;$missing.PSObject.Properties.Remove('queueNotCareAcknowledged')
    $falseAck=Copy-Queue-Authorization-Body $validSpare;$falseAck.noCoverageGuaranteeAcknowledged=$false
    $stale=Copy-Queue-Authorization-Body $validSpare;$stale.expectedRequestVersion=11
    $badSnapshot=Copy-Queue-Authorization-Body $validSpare;$badSnapshot.authorizationSnapshotFingerprint=('0'*64)
    $genericPath="/api/telehealth/v1/admin/requests/$($spare.SubmissionReady.requestId)/authorize"
    Add-QueueAuthorizationCheck 'Access, acknowledgment, stale, changed-snapshot, cross-facility, and generic-route failures write no authorization' (
        (Get-AdminStatus 'GET' (Queue-Authorization-Path $spare.SubmissionReady.requestId) @{})-eq 401 -and
        (Get-AdminStatus 'POST' (Queue-Authorization-Path $spare.SubmissionReady.requestId) $adminHeaders $missing (New-Key 'sp52-missing'))-eq 400 -and
        (Get-AdminStatus 'POST' (Queue-Authorization-Path $spare.SubmissionReady.requestId) $adminHeaders $falseAck (New-Key 'sp52-false'))-eq 400 -and
        (Get-AdminStatus 'POST' (Queue-Authorization-Path $spare.SubmissionReady.requestId) $adminHeaders $stale (New-Key 'sp52-stale'))-eq 409 -and
        (Get-AdminStatus 'POST' (Queue-Authorization-Path $spare.SubmissionReady.requestId) $adminHeaders $badSnapshot (New-Key 'sp52-snapshot'))-eq 409 -and
        (Get-AdminStatus 'GET' (Queue-Authorization-Path $spare.SubmissionReady.requestId) $crossFacilityHeaders)-in@(403,404) -and
        (Get-AdminStatus 'POST' $genericPath $adminHeaders @{expectedVersion=12} (New-Key 'sp52-generic'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_queue_authorizations where request_id='$($spare.SubmissionReady.requestId)'::uuid;")-eq 0)

    $results=@{};$keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$queueAuthorizationApplicants[$index]
        $body=New-Queue-Authorization-Body $applicant.QueueAuthorizationReady
        $key=New-Key 'sp52-authorization';$keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-Queue-Authorization $applicant $body $key $adminHeaders
    }
    $contended=$queueAuthorizationApplicants[1]
    $contentionStatuses=Invoke-ContendedQueueAuthorizations $contended (New-Queue-Authorization-Body $contended.QueueAuthorizationReady) $adminHeaders
    $results[$contended.Id]=Invoke-RestMethod "$ApiBaseUrl$(Queue-Authorization-Path $contended.SubmissionReady.requestId)" -Headers $adminHeaders -TimeoutSec 30
    Add-QueueAuthorizationCheck 'First-writer contention creates exactly one authorization, appointment, and ready queue entry' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($queueAuthorizationApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'Queued' -and $result.requestVersion-eq 13 -and
            $result.authorizationCompleted -and -not$result.authorizationReady -and
            $result.businessOutcome-eq'SyntheticRequestAuthorizedToQueue' -and
            $result.syntheticEvidenceReviewed -and $result.practiceAccepted -and
            $result.patientCareQueueEntered -and $result.clinicianQueueEntered -and
            $result.doctorSearchStarted -and $result.appointmentCreated -and
            -not$result.renderingPhysicianAssigned -and -not$result.coverageVerified -and
            -not$result.queuePositionAssigned -and -not$result.encounterCreated -and
            -not$result.consentCreated -and -not$result.careAuthorized -and
            -not$result.integrationEnabled -and -not$result.externalCallPerformed
        }).Count-eq 3)

    $first=$queueAuthorizationApplicants[0]
    $replay=Post-Queue-Authorization $first (New-Queue-Authorization-Body $first.QueueAuthorizationReady) $keys[$first.Id] $adminHeaders
    Add-QueueAuthorizationCheck 'Exact replay is stable while a second authorization fails closed' (
        $replay.authorizedAt-eq$results[$first.Id].authorizedAt -and
        (Get-AdminStatus 'POST' (Queue-Authorization-Path $first.SubmissionReady.requestId) $adminHeaders (New-Queue-Authorization-Body $first.QueueAuthorizationReady) (New-Key 'sp52-second'))-eq 409)

    $requestIdsSql=@($queueAuthorizationApplicants|ForEach-Object{"'$($_.SubmissionReady.requestId)'::uuid"})-join','
    Add-QueueAuthorizationCheck 'Authorization evidence, requests, events, unassigned appointments, and ready queue rows are exact' (
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_queue_authorizations where request_id in ($requestIdsSql) and practice_accepted and patient_care_queue_entered and clinician_queue_entered and doctor_search_started and appointment_created and not rendering_physician_assigned and not coverage_verified and not queue_position_assigned and not encounter_created and not care_authorized and not external_call_performed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='Queued' and version=13 and ready_at is not null and appointment_id is not null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=13 and action='applicant-request-operationally-authorized' and from_status='OperationalReview' and to_status='Queued';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql) and status='Ready';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from appointments where id in (select appointment_id from telehealth_requests where request_id in ($requestIdsSql)) and provider_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations where request_id in ($requestIdsSql);")-eq 0)

    $authorizationId=Invoke-Scalar "select authorization_id from telehealth_applicant_request_queue_authorizations where request_id='$($first.SubmissionReady.requestId)'::uuid;"
    Add-QueueAuthorizationCheck 'Queue-authorization evidence rejects acknowledgment weakening, consequence escalation, and deletion' (
        (Test-Queue-Authorization-MutationRejected "update telehealth_applicant_request_queue_authorizations set synthetic_evidence_reviewed=false where authorization_id='$authorizationId'::uuid; select 'bad';") -and
        (Test-Queue-Authorization-MutationRejected "update telehealth_applicant_request_queue_authorizations set coverage_verified=true where authorization_id='$authorizationId'::uuid; select 'bad';") -and
        (Test-Queue-Authorization-MutationRejected "update telehealth_applicant_request_queue_authorizations set care_authorized=true where authorization_id='$authorizationId'::uuid; select 'bad';") -and
        (Test-Queue-Authorization-MutationRejected "delete from telehealth_applicant_request_queue_authorizations where authorization_id='$authorizationId'::uuid; select 'bad';"))

    $operationalAfter=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/operational-review" -Headers $adminHeaders -TimeoutSec 20
    $clinicianQueue=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/queue" -Headers $providerHeaders -TimeoutSec 20
    Add-QueueAuthorizationCheck 'Authorized requests leave operational review and enter only the configured clinician queue' (
        @($operationalAfter.requests|Where-Object{$_.requestId-in@($queueAuthorizationApplicants|ForEach-Object{$_.SubmissionReady.requestId})}).Count-eq 0 -and
        @($clinicianQueue.requests|Where-Object{$_.requestId-in@($queueAuthorizationApplicants|ForEach-Object{$_.SubmissionReady.requestId}) -and $_.applicantOriginated}).Count-eq 3)
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$queueAuthorizationPassed;checkCount=$queueAuthorizationChecks.Count;checks=$queueAuthorizationChecks}
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$queueAuthorizationPassed){throw "Telehealth applicant request queue-authorization proof failed. See $resultPath"}
Write-Host "Telehealth applicant request queue-authorization proof passed $($queueAuthorizationChecks.Count) checks."
