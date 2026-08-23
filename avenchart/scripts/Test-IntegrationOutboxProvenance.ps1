# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")

$target = [Uri]$ApiBaseUrl
if ($target.Host -notin @("localhost", "127.0.0.1", "::1")) {
    throw "This integration provenance proof writes a synthetic event and is restricted to a local API target."
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-integration-outbox-provenance-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)

    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) { $script:status = "failed" }
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 15
    Add-Check "API readiness" ($health.status -eq "healthy") $health

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login
    $marker = [Guid]::NewGuid().ToString("N")
    $queued = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox" -Method Post `
        -Headers $headers -ContentType "application/json" `
        -Body (@{
            eventType = "integration.provenance.proof"
            aggregateType = "synthetic-verification"
            aggregateId = $marker
            destination = "local://success"
            payload = @{ fixture = "synthetic-no-phi" }
            idempotencyKey = "integration-provenance-$marker"
        } | ConvertTo-Json -Depth 10) `
        -TimeoutSec 20
    $dispatch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox/$($queued.eventId)/dispatch" `
        -Method Post -Headers $headers -TimeoutSec 20
    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox/$($queued.eventId)/history" `
        -Headers $headers -TimeoutSec 20

    $historyByAction = @{}
    foreach ($entry in $history) { $historyByAction[$entry.action] = $entry }
    Add-Check "Outbox enqueue and dispatch retain actor, state, attempt, and outcome evidence" (
        $dispatch.message.status -eq "delivered" -and
        $historyByAction.ContainsKey("queued") -and
        $historyByAction.ContainsKey("dispatch-claimed") -and
        $historyByAction.ContainsKey("delivered") -and
        $historyByAction["queued"].actor -eq "admin" -and
        $historyByAction["dispatch-claimed"].actor -eq "admin" -and
        $historyByAction["delivered"].actor -eq "admin" -and
        $historyByAction["queued"].status -eq "queued" -and
        $historyByAction["dispatch-claimed"].status -eq "dispatching" -and
        $historyByAction["delivered"].status -eq "delivered" -and
        $historyByAction["delivered"].attemptCount -eq 1
    ) @{ eventId = $queued.eventId; history = $history }

    Push-Location $solutionRoot
    try {
        $priorErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        docker compose exec -T postgres psql -U avenchart -d avenchart -v ON_ERROR_STOP=1 `
            -c "update integration_outbox_provenance_events set detail='mutation must be rejected' where event_id='$($queued.eventId)';" 2>$null | Out-Null
        $immutabilityRejected = $LASTEXITCODE -ne 0
        $ErrorActionPreference = $priorErrorActionPreference
    }
    finally {
        $ErrorActionPreference = "Stop"
        Pop-Location
    }
    Add-Check "Outbox provenance history is immutable" $immutabilityRejected @{ eventId = $queued.eventId }
}
catch {
    Add-Check "Unhandled integration outbox provenance test error" $false $_.Exception.Message
}
finally {
    $result = [ordered]@{
        status = $status
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
        checks = $checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}

if ($status -ne "passed") { exit 1 }
