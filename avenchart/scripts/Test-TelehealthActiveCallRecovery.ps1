# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:8088', [switch]$UseDisposableSyntheticEnvironment,
    [string]$FixtureDatabaseContainer = '', [switch]$ResumePreparedRequest)
$ErrorActionPreference = 'Stop'
if (-not $UseDisposableSyntheticEnvironment -or ([uri]$ApiBaseUrl).Host -notin @('127.0.0.1','localhost')) {
    throw 'Explicitly select a disposable loopback synthetic environment. This test creates a visit and leaves it in wrap-up; it never resets or deletes records.'
}
function Post([string]$Path, [hashtable]$Headers, [object]$Body) {
    $args = @{ Uri = "$ApiBaseUrl$Path"; Method = 'Post'; Headers = $Headers; ContentType = 'application/json'; TimeoutSec = 30 }
    if ($null -ne $Body) { $args.Body = $Body | ConvertTo-Json -Compress }
    try { return Invoke-RestMethod @args }
    catch {
        if ([int]$_.Exception.Response.StatusCode -eq 409) {
            $body = $_.ErrorDetails.Message
            if (-not $body) {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $body = $reader.ReadToEnd()
                $reader.Dispose()
            }
            $problem = if ($body) { $body | ConvertFrom-Json } else { @{ code='unknown'; detail='No error body returned.' } }
            Write-Host "Conflict at $Path ($($problem.code)): $($problem.detail)"
        }
        throw
    }
}
function Get([string]$Path, [hashtable]$Headers) { Invoke-RestMethod "$ApiBaseUrl$Path" -Headers $Headers -TimeoutSec 30 }
function Staff([string]$Username) {
    $login = Post '/api/auth/login' @{} @{ username=$Username; password='pass' }
    if (-not $login.authenticated) { throw "The synthetic physician fixture $Username is unavailable." }
    return @{ 'X-AvenChart-Session'=$login.sessionId; 'X-AvenChart-Facility-Id'='10'; 'X-AvenChart-Purpose-Of-Use'='treatment' }
}
function ExpectStatus([int[]]$Allowed, [scriptblock]$Action) {
    try { $null = & $Action } catch {
        if ([int]$_.Exception.Response.StatusCode -in $Allowed) { return }
        throw
    }
    throw "Expected HTTP $($Allowed -join '/'), but the request succeeded."
}
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }

if ($FixtureDatabaseContainer) {
    $inspection = docker inspect $FixtureDatabaseContainer | ConvertFrom-Json
    $project = $inspection[0].Config.Labels.'com.docker.compose.project'
    if ($LASTEXITCODE -ne 0 -or $project -notmatch '^avenchart-telehealth-[a-z0-9-]*qa$') { throw 'Refusing fixture provisioning outside a named isolated telehealth QA project.' }
    Get-Content -LiteralPath (Join-Path $PSScriptRoot 'fixtures/telehealth-recovery-second-physician.sql') -Raw | docker exec -i $FixtureDatabaseContainer psql -X -U avenchart_staging -d avenchart_staging -v ON_ERROR_STOP=1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The second synthetic physician could not be provisioned.' }
}
if (-not $ResumePreparedRequest) { & (Join-Path $PSScriptRoot 'Prepare-TelehealthPocDemo.ps1') -ApiBaseUrl $ApiBaseUrl }
$physician = Staff 'gold-provider-01'
$otherPhysician = Staff 'synthetic-recovery-physician'
$portal = Post '/api/patient-portal/login' @{} @{ username='mod-pat-0012@example.test'; password='PortalPass207!' }
Require $portal.authenticated 'The synthetic patient fixture is unavailable.'
$patient = @{ 'X-AvenChart-Patient-Portal-Session'=$portal.sessionId }
$work = Get '/api/telehealth/v1/clinician/active-work' $physician
if ($ResumePreparedRequest -and $null -eq $work.reservation -and $null -eq $work.consultationId) {
    $queue = Get '/api/telehealth/v1/clinician/queue' $physician
    Require (@($queue.requests).Count -eq 1) 'Resuming requires exactly one known queued synthetic request.'
    $physician['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
    $work.reservation = Post '/api/telehealth/v1/clinician/reservations/reserve-next' $physician $null
}
Require ($null -ne $work.reservation -and $null -eq $work.consultationId) 'A prepared, unstarted synthetic reservation is required.'
$requestId = $work.reservation.requestId
$reservationId = $work.reservation.reservationId
$preflight = @{ expectedVersion=$work.reservation.requestVersion; browserSupported=$true; cameraAvailable=$true; microphoneAvailable=$true; speakerAvailable=$true; networkQuality='good'; syntheticDataConfirmed=$true }
$patient['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
$patientGrant = Post "/api/telehealth/v1/patient/requests/$requestId/connection-grants" $patient $preflight
$physician['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
$physicianGrant = Post "/api/telehealth/v1/clinician/reservations/$reservationId/connection-grants" $physician $preflight
Require ($patientGrant.sessionId -eq $physicianGrant.sessionId) 'The participants were placed in different rooms.'
$physician['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
$start = Post "/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start" $physician @{
    expectedVersion=$physicianGrant.requestVersion; patientLocationState='GA'; patientIdentityDiscussed=$true; callbackConfirmed=$true
    privacyConfirmed=$true; consentDiscussed=$true; noConcerningSymptomChange=$true; emergencyPlanConfirmed=$true; communicationSufficient=$true; syntheticDataConfirmed=$true
}
$consultationId = $start.consultationId
$preflight.expectedVersion = $start.requestVersion
$patient['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
$freshPatient = Post "/api/telehealth/v1/patient/requests/$requestId/active-call-grants" $patient $preflight
$physician['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
$freshPhysician = Post "/api/telehealth/v1/clinician/consultations/$consultationId/active-call-grants" $physician $preflight
Require ($freshPatient.sessionId -eq $patientGrant.sessionId -and $freshPhysician.sessionId -eq $patientGrant.sessionId) 'Recovery changed the room identity.'
Require ($freshPatient.requestStatus -eq 'InConsultation' -and $freshPatient.grantId -ne $patientGrant.grantId) 'Recovery did not issue a fresh active-call grant.'
$replay = Post "/api/telehealth/v1/clinician/consultations/$consultationId/active-call-grants" $physician $preflight
Require ($replay.grantId -eq $freshPhysician.grantId) 'An unchanged retry issued another grant.'
$otherPhysician['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
ExpectStatus @(403,404) { Post "/api/telehealth/v1/clinician/consultations/$consultationId/active-call-grants" $otherPhysician $preflight }
ExpectStatus @(404) { Post "/api/telehealth/v1/patient/requests/$([guid]::NewGuid())/active-call-grants" $patient $preflight }
function SignalBody([object]$Grant) { @{ sessionId=$Grant.sessionId; grantId=$Grant.grantId; joinCredential=$Grant.joinCredential; afterSequence=0 } }
ExpectStatus @(404) { Post '/api/telehealth/v1/local-webrtc/signals/read' $patient (SignalBody $patientGrant) }
$null = Post '/api/telehealth/v1/local-webrtc/signals/read' $patient (SignalBody $freshPatient)
$stillOwned = Get '/api/telehealth/v1/clinician/active-work' $physician
Require ($stillOwned.consultationId -eq $consultationId) 'Reconnection changed the existing consultation.'
$workspace = Get "/api/telehealth/v1/clinician/consultations/$consultationId/workspace" $physician
$physician['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
$null = Post "/api/telehealth/v1/clinician/consultations/$consultationId/wrap-up" $physician @{
    expectedVersion=$workspace.consultationVersion; syntheticSessionEndedConfirmed=$true; documentationStillIncompleteAcknowledged=$true; wrapUpResponsibilityAcknowledged=$true
}
$patient['X-Idempotency-Key'] = [guid]::NewGuid().ToString()
ExpectStatus @(404) { Post "/api/telehealth/v1/patient/requests/$requestId/active-call-grants" $patient $preflight }
ExpectStatus @(404) { Post '/api/telehealth/v1/local-webrtc/signals/read' $patient (SignalBody $freshPatient) }
Write-Host 'PASS: active-call recovery preserves the room/consultation, retries safely, rejects another physician and revoked grants, and closes authorization at wrap-up.'
