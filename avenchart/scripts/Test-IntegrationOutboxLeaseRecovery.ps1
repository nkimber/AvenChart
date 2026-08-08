# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$solutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-integration-outbox-lease-recovery.json"
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
    $headers = @{ "X-AvenChart-Session" = $login.sessionId }
    $queueRequest = @{ eventType = "integration.lease.test"; aggregateType = "verification"; aggregateId = (New-Guid).Guid; destination = "local://success"; payload = @{ fixture = "no-phi" }; idempotencyKey = (New-Guid).Guid } | ConvertTo-Json -Depth 5
    $queued = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox" -Method Post -Headers $headers -ContentType "application/json" -Body $queueRequest -TimeoutSec 15
    $query = "update integration_outbox set status='dispatching', attempt_count=1, locked_at=now() - interval '6 minutes', last_attempt_at=now() - interval '6 minutes', updated_at=now() - interval '6 minutes' where event_id='$($queued.eventId)';"
    Push-Location $solutionRoot
    try { docker compose exec -T postgres psql -U avenchart -d avenchart -v ON_ERROR_STOP=1 -c $query | Out-Null; if ($LASTEXITCODE -ne 0) { throw "Expired dispatch lease setup failed." } }
    finally { Pop-Location }
    $dispatch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox/$($queued.eventId)/dispatch" -Method Post -Headers $headers -TimeoutSec 15
    Add-Check "Expired claim is recovered then delivered as a new attempt" ($dispatch.message.status -eq "delivered" -and $dispatch.message.attemptCount -eq 2 -and $dispatch.outcome -eq "delivered") $dispatch
    Push-Location $solutionRoot
    try { $audit = docker compose exec -T postgres psql -U avenchart -d avenchart -t -A -c "select action || ':' || actor || ':' || attempt_count from integration_outbox_events where event_id='$($queued.eventId)' order by occurred_at;" }
    finally { Pop-Location }
    Add-Check "Lease recovery has immutable system evidence" (($audit -join '') -match "lease-recovered:local-dispatch-lease-recovery:1") @{ audit = $audit }
}
catch { $checks.Add([ordered]@{ name = "integration outbox lease recovery harness"; result = "failed"; details = $_.Exception.Message }) }

$result = [ordered]@{ status = if (($checks | Where-Object result -eq "failed").Count -eq 0) { "passed" } else { "failed" }; generatedAt = (Get-Date).ToUniversalTime().ToString("O"); checks = $checks }
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
$result | ConvertTo-Json -Depth 8
if ($result.status -ne "passed") { exit 1 }
