# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:8088'
)

$ErrorActionPreference = 'Stop'

if (([Uri]$ApiBaseUrl).Host -notin @('localhost', '127.0.0.1', '::1')) {
    throw 'The two-person telehealth POC preparation is intentionally loopback-only.'
}

function New-IdempotencyKey([string]$Prefix) {
    return "$Prefix-$([Guid]::NewGuid().ToString('N'))"
}

function Invoke-JsonGet {
    param(
        [string]$Path,
        [hashtable]$Headers
    )

    if ($null -eq $Headers -or $Headers.Count -eq 0) {
        return (Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl$Path" -TimeoutSec 30).Content | ConvertFrom-Json
    }

    return (Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl$Path" -Headers $Headers -TimeoutSec 30).Content | ConvertFrom-Json
}

function Invoke-JsonPost([string]$Path, [hashtable]$Headers, [object]$Body = $null) {
    if ($null -ne $Body) {
        return (Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl$Path" -Method Post -Headers $Headers -ContentType 'application/json' -TimeoutSec 30 -Body (
            $Body | ConvertTo-Json -Depth 8 -Compress)).Content | ConvertFrom-Json
    }

    return (Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl$Path" -Method Post -Headers $Headers -TimeoutSec 30).Content | ConvertFrom-Json
}

function Get-StaffLogin([string]$Username) {
    return (Invoke-WebRequest -UseBasicParsing "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -TimeoutSec 30 -Body (
        @{ username = $Username; password = 'pass' } | ConvertTo-Json -Compress)).Content | ConvertFrom-Json
}

function Get-StaffHeaders([object]$Login, [string]$PurposeOfUse, [string]$IdempotencyKey) {
    return @{
        'X-AvenChart-Session' = $Login.sessionId
        'X-AvenChart-Facility-Id' = '10'
        'X-AvenChart-Purpose-Of-Use' = $PurposeOfUse
        'X-Idempotency-Key' = $IdempotencyKey
    }
}

$practice = Invoke-JsonGet '/api/telehealth/v1/context'
if (-not $practice.available -or -not $practice.syntheticOnly) {
    throw 'The target does not report an available synthetic-only telehealth practice. Start the local staging stack before preparing a demo.'
}

$physician = Get-StaffLogin 'gold-provider-01'
if (-not $physician.authenticated -or [string]::IsNullOrWhiteSpace($physician.sessionId)) {
    throw 'The synthetic staging demo accounts are not initialized. Run Reset-AvenChartTelehealthPocStaging.ps1 -ResetStagingData first.'
}
$physicianReadHeaders = Get-StaffHeaders $physician 'treatment' (New-IdempotencyKey 'unused-read-key')
$physicianReadHeaders.Remove('X-Idempotency-Key')
$activeWork = Invoke-JsonGet '/api/telehealth/v1/clinician/active-work' $physicianReadHeaders
if ($null -ne $activeWork.shift -or $null -ne $activeWork.reservation) {
    throw 'The demo physician already has active synthetic telehealth work. End the existing idle shift or abandon the existing connection in the UI before preparing another demo.'
}

$existingQueue = Invoke-JsonGet '/api/telehealth/v1/clinician/queue' $physicianReadHeaders
if (@($existingQueue.requests).Count -ne 0) {
    throw 'The synthetic clinician queue is not empty. Use an empty staging volume for a deterministic two-person demo; this command will not alter existing queue work.'
}

$portal = (Invoke-WebRequest -UseBasicParsing "$ApiBaseUrl/api/patient-portal/login" -Method Post -ContentType 'application/json' -TimeoutSec 30 -Body (
    @{ username = 'mod-pat-0012@example.test'; password = 'PortalPass207!' } | ConvertTo-Json -Compress)).Content | ConvertFrom-Json
if (-not $portal.authenticated -or [string]::IsNullOrWhiteSpace($portal.sessionId)) {
    throw 'The synthetic patient portal fixture is not available. Run Reset-AvenChartTelehealthPocStaging.ps1 -ResetStagingData first.'
}
$portalHeaders = @{
    'X-AvenChart-Patient-Portal-Session' = $portal.sessionId
    'X-Idempotency-Key' = New-IdempotencyKey 'two-person-create'
}

$request = Invoke-JsonPost '/api/telehealth/v1/patient/requests' $portalHeaders @{ complaintCategory = 'sleep' }
$portalHeaders['X-Idempotency-Key'] = New-IdempotencyKey 'two-person-location'
$request = Invoke-JsonPost "/api/telehealth/v1/patient/requests/$($request.requestId)/location" $portalHeaders @{
    stateCode = 'GA'
    expectedVersion = $request.version
}
$portalHeaders['X-Idempotency-Key'] = New-IdempotencyKey 'two-person-triage'
$request = Invoke-JsonPost "/api/telehealth/v1/patient/requests/$($request.requestId)/triage" $portalHeaders @{
    hasEmergencyWarning = $false
    severeOrWorsening = $false
    requiresHandsOnExam = $false
    unsure = $false
    expectedVersion = $request.version
}

$readiness = Invoke-JsonGet "/api/telehealth/v1/patient/requests/$($request.requestId)/readiness" @{
    'X-AvenChart-Patient-Portal-Session' = $portal.sessionId
}
$coverage = @($readiness.coverageOptions | Where-Object {
    $_.provider -eq 'Harbor Mutual' -and $_.planName -eq 'High Deductible'
}) | Select-Object -First 1
if ($null -eq $coverage) {
    throw 'The deterministic synthetic coverage fixture required for the demo was not available.'
}

$portalHeaders['X-Idempotency-Key'] = New-IdempotencyKey 'two-person-readiness'
$request = Invoke-JsonPost "/api/telehealth/v1/patient/requests/$($request.requestId)/readiness" $portalHeaders @{
    expectedVersion = $readiness.requestVersion
    demographicsFingerprint = $readiness.patientDetails.fingerprint
    clinicalSummaryFingerprint = $readiness.clinicalSummary.fingerprint
    demographicsConfirmed = $true
    contactConfirmed = $true
    clinicalSummaryConfirmed = $true
    complaintSummary = 'Synthetic two-person local media demonstration'
    symptomDuration = '1-3-days'
    syntheticDataConfirmed = $true
    coverageToken = $coverage.coverageToken
    coverageFingerprint = $coverage.fingerprint
    coverageConfirmed = $true
    acknowledgmentPackageKey = $readiness.acknowledgment.packageKey
    acknowledgmentPackageVersion = $readiness.acknowledgment.packageVersion
    acknowledgmentContentHash = $readiness.acknowledgment.contentHash
    acknowledgmentAccepted = $true
}
$portalHeaders['X-Idempotency-Key'] = New-IdempotencyKey 'two-person-coverage'
$request = Invoke-JsonPost "/api/telehealth/v1/patient/requests/$($request.requestId)/coverage/verify" $portalHeaders @{
    expectedVersion = $request.version
}

if ($request.status -ne 'OperationalReview') {
    throw "The synthetic request did not reach operational review (current status: $($request.status))."
}

$administrator = Get-StaffLogin 'admin'
if (-not $administrator.authenticated -or [string]::IsNullOrWhiteSpace($administrator.sessionId)) {
    throw 'The synthetic administrator fixture is not available. Run Reset-AvenChartTelehealthPocStaging.ps1 -ResetStagingData first.'
}
$administratorHeaders = Get-StaffHeaders $administrator 'healthcare-operations' (New-IdempotencyKey 'two-person-authorize')
$request = Invoke-JsonPost "/api/telehealth/v1/admin/requests/$($request.requestId)/authorize" $administratorHeaders @{
    expectedVersion = $request.version
}
if ($request.status -ne 'Queued') {
    throw "The synthetic request was not queued (current status: $($request.status))."
}

$physicianHeaders = Get-StaffHeaders $physician 'treatment' (New-IdempotencyKey 'two-person-shift')
$null = Invoke-JsonPost '/api/telehealth/v1/clinician/shifts' $physicianHeaders
$physicianHeaders['X-Idempotency-Key'] = New-IdempotencyKey 'two-person-reserve'
$reservation = Invoke-JsonPost '/api/telehealth/v1/clinician/reservations/reserve-next' $physicianHeaders
if ($reservation.requestId -ne $request.requestId) {
    throw 'A different request was reserved. No cleanup was attempted; use an empty staging volume before retrying.'
}

Write-Host ''
Write-Host 'The two-person local WebRTC demo is prepared.' -ForegroundColor Green
Write-Host "Reservation lease expires at $([DateTimeOffset]$reservation.leaseExpiresAt)."
Write-Host ''
Write-Host 'Patient browser (one profile):'
Write-Host "  $ApiBaseUrl/portal/login"
Write-Host '  mod-pat-0012@example.test / PortalPass207!'
Write-Host "  Then open $ApiBaseUrl/portal/telehealth, run the device check, and enter the waiting room."
Write-Host ''
Write-Host 'Physician browser (a separate profile):'
Write-Host "  $ApiBaseUrl/login"
Write-Host '  gold-provider-01 / pass'
Write-Host "  Then open $ApiBaseUrl/clinician/telehealth/physician, run the device check, and enter the waiting room."
Write-Host ''
Write-Host 'Start local media in the physician browser first, then join it in the patient browser.'
Write-Host 'This is a loopback-only, non-production demonstration. It never records, transcribes, stores, or relays media through AvenChart.'
