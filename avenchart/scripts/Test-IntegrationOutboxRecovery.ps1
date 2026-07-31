# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"

$solutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-integration-outbox-recovery.json"
New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed, $Details) {
    $checks.Add([ordered]@{ name = $Name; result = if ($Passed) { "passed" } else { "failed" }; details = $Details })
    if (-not $Passed) { throw "$Name failed." }
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 15
    Add-Check "API readiness" ($health.status -eq "healthy") $health

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}' -TimeoutSec 15
    $headers = @{ "X-Legacy EHR-Session" = $login.sessionId }
    $queueRequest = @{
        eventType = "integration.recovery.test"
        aggregateType = "verification"
        aggregateId = (New-Guid).Guid
        destination = "local://recoverable"
        payload = @{ fixture = "no-phi" }
        idempotencyKey = (New-Guid).Guid
    } | ConvertTo-Json -Depth 5
    $queued = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox" -Method Post -Headers $headers -ContentType "application/json" -Body $queueRequest -TimeoutSec 15

    $dispatches = @()
    1..3 | ForEach-Object {
        $dispatches += Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox/$($queued.eventId)/dispatch" -Method Post -Headers $headers -TimeoutSec 15
        if ($_ -lt 3) { Start-Sleep -Milliseconds 1200 }
    }

    $quarantined = $dispatches[-1].message
    Add-Check "Three failed automatic attempts quarantine the event" (
        $quarantined.status -eq "quarantined" -and
        $quarantined.attemptCount -eq 3 -and
        $null -ne $quarantined.quarantinedAt -and
        $quarantined.quarantinedBy -eq "local-dispatch" -and
        $quarantined.recoveryCount -eq 0) $quarantined

    $recoveryRequest = @{ reason = "Focused deterministic recovery verification."; expectedAttemptCount = 3 } | ConvertTo-Json
    $requeued = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox/$($queued.eventId)/requeue" -Method Post -Headers $headers -ContentType "application/json" -Body $recoveryRequest -TimeoutSec 15
    Add-Check "Authenticated recovery requeues the expected quarantined event" (
        $requeued.status -eq "queued" -and
        $requeued.recoveryCount -eq 1 -and
        $null -eq $requeued.quarantinedAt -and
        $null -eq $requeued.quarantinedBy) $requeued

    $recovered = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox/$($queued.eventId)/dispatch" -Method Post -Headers $headers -TimeoutSec 15
    Add-Check "Recovered event is delivered without a network transport" (
        $recovered.message.status -eq "delivered" -and
        $recovered.outcome -eq "delivered-after-recovery" -and
        $null -ne $recovered.message.externalReference) $recovered
}
catch {
    $checks.Add([ordered]@{ name = "integration outbox recovery harness"; result = "failed"; details = $_.Exception.Message })
}

$result = [ordered]@{
    status = if (($checks | Where-Object result -eq "failed").Count -eq 0) { "passed" } else { "failed" }
    generatedAt = (Get-Date).ToUniversalTime().ToString("O")
    checks = $checks
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
$result | ConvertTo-Json -Depth 8

if ($result.status -ne "passed") { exit 1 }
