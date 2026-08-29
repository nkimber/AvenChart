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
    throw 'Telehealth applicant request queue-status proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestQueueAuthorization.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName -VerifyApplicantQueueStatus

$queueStatusApplicants = @($queueAuthorizationApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-queue-status.json'
$queueStatusChecks = [System.Collections.Generic.List[object]]::new()
$queueStatusPassed = $true

function Add-QueueStatusCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:queueStatusChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:queueStatusPassed=$false}
}
function Get-QueueStatusFingerprint {
    $requestIdsSql=@($queueStatusApplicants|ForEach-Object{"'$($_.SubmissionReady.requestId)'::uuid"})-join','
    Invoke-Scalar @"
select md5(concat_ws('|',
  coalesce((select jsonb_agg(to_jsonb(r) order by r.request_id)::text from telehealth_requests r where r.request_id in ($requestIdsSql)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(q) order by q.queue_entry_id)::text from telehealth_queue_entries q where q.request_id in ($requestIdsSql)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(a) order by a.authorization_id)::text from telehealth_applicant_request_queue_authorizations a where a.request_id in ($requestIdsSql)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(e) order by e.event_id)::text from telehealth_request_events e where e.request_id in ($requestIdsSql)),'[]')));
"@
}
function Get-QueueStatusWeb([object]$Applicant) {
    Invoke-WebRequest "$ApiBaseUrl$(Applicant-Queue-Status-Path $Applicant.Id)" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Get-QueueStatusHttp([object]$Applicant,[hashtable]$Headers) {
    try {
        [int](Invoke-WebRequest "$ApiBaseUrl$(Applicant-Queue-Status-Path $Applicant.Id)" `
            -Headers $Headers -TimeoutSec 30).StatusCode
    }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}

try {
    $before=Get-QueueStatusFingerprint
    $responses=@($queueStatusApplicants|ForEach-Object{
        $web=Get-QueueStatusWeb $_
        [pscustomobject]@{Applicant=$_;Web=$web;Status=($web.Content|ConvertFrom-Json)}
    })
    $positions=@($responses|ForEach-Object{[int]$_.Status.approximateRequestsAhead}|Sort-Object)
    Add-QueueStatusCheck 'GA, CA, and FL applicants receive private authoritative queued projections with approximate ordering only' (
        @($responses|Where-Object{
            $_.Web.StatusCode-eq 200 -and
            [string]$_.Web.Headers['Cache-Control']-match'no-store' -and
            [string]$_.Web.Headers['Vary']-match'X-AvenChart-Telehealth-Applicant-Key' -and
            $_.Status.requestId-eq$_.Applicant.SubmissionReady.requestId -and
            $_.Status.requestStatus-eq'Queued' -and $_.Status.requestVersion-eq 13 -and
            $_.Status.policyKey-eq'SYNTHETIC_APPLICANT_REQUEST_QUEUE_STATUS' -and
            $_.Status.policyVersion-eq 1 -and $_.Status.sourceMode-eq'NON_PRODUCTION' -and
            $_.Status.phase-eq'InQueue' -and $_.Status.headline-eq"You're in line" -and
            $_.Status.positionIsApproximate -and -not$_.Status.exactQueuePositionAssigned -and
            -not$_.Status.waitEstimateAvailable -and -not$_.Status.realtimeAvailable -and
            $_.Status.practiceAccepted -and $_.Status.doctorSearchStarted -and
            -not$_.Status.renderingPhysicianAssigned -and -not$_.Status.renderingPhysicianIdentityDisclosed -and
            -not$_.Status.syntheticRenderingCandidateMatched -and -not$_.Status.realRenderingPhysicianNetworkConfirmed -and
            -not$_.Status.coverageVerified -and -not$_.Status.consentCreated -and
            -not$_.Status.careAuthorized -and -not$_.Status.integrationEnabled -and
            -not$_.Status.externalCallPerformed -and @($_.Status.safetyActions).Count-eq 2
        }).Count-eq 3 -and ($positions-join',')-eq'0,1,2') $positions

    $first=$queueStatusApplicants[0]
    $admin=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='admin';password='pass'}|ConvertTo-Json) -TimeoutSec 20
    $portal=Invoke-RestMethod "$ApiBaseUrl/api/patient-portal/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='gold-patient-01';password='PortalPass207!'}|ConvertTo-Json) -TimeoutSec 20
    Add-QueueStatusCheck 'Absent, foreign, staff, and portal credentials cannot substitute for the owning applicant key' (
        (Get-QueueStatusHttp $first @{})-eq 401 -and
        (Get-QueueStatusHttp $first @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Get-QueueStatusHttp $first @{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations'})-eq 401 -and
        (Get-QueueStatusHttp $first @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId})-eq 401)

    $serialized=$responses.Status|ConvertTo-Json -Depth 10 -Compress
    Add-QueueStatusCheck 'Applicant status excludes clinician identity, insurance evidence, clinical detail, prescribing, claims, and integrations' (
        $serialized-notmatch '"(applicantId|patientId|canonicalPatientId|accessKey|providerId|doctorId|physicianId|physicianName|npi|memberId|groupNumber|payer|diagnosis|medication|prescription|claim|encounter|price|estimateAmount)"\s*:')
    $after=Get-QueueStatusFingerprint
    Add-QueueStatusCheck 'Repeated applicant status reads do not mutate queue, request, event, or authorization evidence' ($before-eq$after)
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$queueStatusPassed;checkCount=$queueStatusChecks.Count;checks=$queueStatusChecks}
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$queueStatusPassed){throw "Telehealth applicant request queue-status proof failed. See $resultPath"}
Write-Host "Telehealth applicant request queue-status proof passed $($queueStatusChecks.Count) checks."
