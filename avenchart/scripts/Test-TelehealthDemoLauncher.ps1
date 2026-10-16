# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:8088', [switch]$UseDisposableSyntheticEnvironment,
    [string]$DatabaseContainer = '', [ValidatePattern('^avenchart_[a-z0-9_]+$')][string]$DatabaseName = 'avenchart_staging')
$ErrorActionPreference = 'Stop'
if (-not $UseDisposableSyntheticEnvironment -or ([uri]$ApiBaseUrl).Host -notin @('127.0.0.1','localhost')) { throw 'Select an explicitly disposable loopback synthetic demo.' }
function New-DemoTestKey {
    $bytes = New-Object byte[] 32
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    return [BitConverter]::ToString($bytes).Replace('-','').ToLowerInvariant()
}
function Invoke-Demo([string]$Path, [string]$Key, [object]$Body = $null, [string]$Command = '') {
    $parameters = @{ Uri="$ApiBaseUrl/api/telehealth/v1/demo$Path"; TimeoutSec=30; Headers=@{ 'X-AvenChart-Telehealth-Demo-Key'=$Key } }
    if ($Command) { $parameters.Headers['X-Idempotency-Key']=$Command }
    if ($null -ne $Body) { $parameters.Method='Post'; $parameters.ContentType='application/json'; $parameters.Body=$Body|ConvertTo-Json -Compress }
    return Invoke-RestMethod @parameters
}
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function ExpectStatus([int]$Status, [scriptblock]$Action) {
    try { $null = & $Action } catch { if ([int]$_.Exception.Response.StatusCode -eq $Status) { return }; throw }
    throw "Expected HTTP $Status, but the request succeeded."
}
function Invoke-TestDatabase([string]$Sql) {
    $result = $Sql | docker exec -i $DatabaseContainer psql -X -q -At -U avenchart_staging -d $DatabaseName -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw 'The isolated acceptance database query failed.' }
    return $result
}
$roomKeys = @{}
function New-Room {
    $key = New-DemoTestKey
    $command = [guid]::NewGuid().ToString()
    $run = Invoke-Demo '/runs' $key @{ syntheticDataConfirmed=$true; observerKey=$key } $command
    $roomKeys[$run.runId] = $key
    return @{ run=$run; key=$key; command=$command }
}
$context = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/context" -TimeoutSec 30
Require ($context.syntheticOnly -and $context.demoLauncherEnabled) 'The synthetic demo launcher must be explicitly enabled.'
$counts = "select (select count(*) from patients)||':'||(select count(*) from encounters)||':'||(select count(*) from appointments)||':'||(select count(*) from auth_accounts)||':'||(select count(*) from auth_sessions)||':'||(select count(*) from patient_portal_sessions);"
if ($DatabaseContainer) {
    $inspection = docker inspect $DatabaseContainer | ConvertFrom-Json
    Require ($LASTEXITCODE -eq 0 -and $inspection[0].Config.Labels.'com.docker.compose.project' -match '^avenchart-telehealth-.*(qa|ci)$') 'Database fault injection is allowed only inside an isolated telehealth QA/CI project.'
    $before = Invoke-TestDatabase $counts
}
try {
    $one = New-Room
    $run = $one.run
    $replay = Invoke-Demo '/runs' $one.key @{ syntheticDataConfirmed=$true; observerKey=$one.key } $one.command
    Require ($replay.runId -eq $run.runId -and $replay.patientInvitation -eq $run.patientInvitation) 'An unchanged create retry must recover the same room and invitation.'
    ExpectStatus 400 { Invoke-Demo '/runs' $one.key @{ syntheticDataConfirmed=$false; observerKey=$one.key } ([guid]::NewGuid().ToString()) }
    ExpectStatus 409 { Invoke-Demo '/runs' '' @{ syntheticDataConfirmed=$true; observerKey=(New-DemoTestKey) } $one.command }
    $patientKey = New-DemoTestKey
    $physicianKey = New-DemoTestKey
    $patient = Invoke-Demo "/runs/$($run.runId)/patient/redeem" '' @{ invitation=$run.patientInvitation; sessionKey=$patientKey }
    $physician = Invoke-Demo "/runs/$($run.runId)/physician/redeem" '' @{ invitation=$run.physicianInvitation; sessionKey=$physicianKey }
    Require ($patient.runId -eq $physician.runId -and $patient.participantId -ne $physician.participantId) 'The two roles must share one room and have independent identities.'
    $again = Invoke-Demo "/runs/$($run.runId)/patient/redeem" '' @{ invitation=$run.patientInvitation; sessionKey=$patientKey }
    Require ($again.participantId -eq $patient.participantId) 'A lost redemption response must be safely retriable by the same browser.'
    ExpectStatus 404 { Invoke-Demo "/runs/$($run.runId)/patient/redeem" '' @{ invitation=$run.patientInvitation; sessionKey=(New-DemoTestKey) } }
    ExpectStatus 404 { Invoke-Demo "/runs/$($run.runId)/physician/heartbeat" $patientKey @{ stage='Opened' } }
    ExpectStatus 404 { Invoke-Demo "/runs/$($run.runId)/patient/heartbeat" $one.key @{ stage='Opened' } }
    $two = New-Room
    ExpectStatus 404 { Invoke-Demo "/runs/$($two.run.runId)" $patientKey }
    $null = Invoke-Demo "/runs/$($run.runId)/patient/heartbeat" $patientKey @{ stage='DevicesReady' }
    if ($run.transport -eq 'NON_PRODUCTION_LOCAL_WEBRTC_POC') {
        $null = Invoke-Demo "/runs/$($run.runId)/patient/signals/write" $patientKey @{ kind='request-offer'; payload='{}' }
        $signals = Invoke-Demo "/runs/$($run.runId)/physician/signals/read" $physicianKey @{ afterSequence=0 }
        Require (@($signals.signals).Count -eq 1) 'Only the paired physician should receive the local request for an offer.'
        ExpectStatus 404 { Invoke-Demo "/runs/$($two.run.runId)/physician/signals/read" $physicianKey @{ afterSequence=0 } }
    }
    $ended = Invoke-Demo "/runs/$($run.runId)/end" $patientKey @{}
    Require ($ended.status -eq 'Ended') 'A participant must be able to end only their own demo.'
    ExpectStatus 404 { Invoke-Demo "/runs/$($run.runId)/patient/heartbeat" $patientKey @{ stage='Connected' } }
    $null = Invoke-Demo "/runs/$($two.run.runId)/end" $two.key @{}
    ExpectStatus 404 { Invoke-Demo "/runs/$($two.run.runId)/physician/redeem" '' @{ invitation=$two.run.physicianInvitation; sessionKey=(New-DemoTestKey) } }
    if ($DatabaseContainer) {
        $expiring = New-Room
        $expirationKey = New-DemoTestKey
        $null = Invoke-Demo "/runs/$($expiring.run.runId)/patient/redeem" '' @{ invitation=$expiring.run.patientInvitation; sessionKey=$expirationKey }
        Start-Sleep -Milliseconds 1200
        $validatedRun = ([guid]$expiring.run.runId).ToString('D')
        # Controlled time-boundary fixture mutation, restricted to this newly
        # created synthetic room. Never delete a record or change a clinical table.
        $null = Invoke-TestDatabase "update telehealth_demo_runs set expires_at=now()-interval '0.1 seconds' where run_id='$validatedRun' and status='Active';"
        $expired = Invoke-Demo "/runs/$validatedRun" $expirationKey
        Require ($expired.status -eq 'Expired') 'The server must report its expired room authoritatively.'
        ExpectStatus 404 { Invoke-Demo "/runs/$validatedRun/patient/heartbeat" $expirationKey @{ stage='Connected' } }
        ExpectStatus 404 { Invoke-Demo "/runs/$validatedRun/physician/redeem" '' @{ invitation=$expiring.run.physicianInvitation; sessionKey=(New-DemoTestKey) } }
        Require ((Invoke-TestDatabase $counts) -eq $before) 'Demo operations changed a patient, encounter, appointment, account, or clinical sign-in count.'
    } else { Write-Host 'Not executed: server-expiry fault and canonical row-count checks (no isolated database selected).' }
    Write-Host 'PASS: synthetic room creation/retry, one-use invitations, independent roles, cross-room denial, scoped ending, and admission denial.'
} finally {
    foreach ($runId in @($roomKeys.Keys)) { try { $null = Invoke-Demo "/runs/$runId/end" $roomKeys[$runId] @{} } catch { } }
}
