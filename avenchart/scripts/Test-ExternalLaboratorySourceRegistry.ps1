# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-external-laboratory-source-registry-test.json"
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
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -TimeoutSec 15
    Add-Check "API health" ($health.status -eq "healthy") $health

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post `
        -ContentType "application/json" -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "Administration login did not issue an active session."
    }

    $headers = New-AvenChartStaffAccessContextHeaders -Login $login
    $marker = [Guid]::NewGuid().ToString("N").Substring(0, 12)
    $sourceId = "synthetic-lab-$marker"
    $apiKey = "synthetic-laboratory-key-" + [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
    $source = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/laboratory-sources" -Method Post `
        -Headers $headers -ContentType "application/json" -Body (@{
            sourceId = $sourceId
            displayName = "Synthetic Laboratory $marker"
            apiKey = $apiKey
        } | ConvertTo-Json) -TimeoutSec 20

    $sourceProperties = @($source.PSObject.Properties.Name)
    Add-Check "Source creation returns no credential material" (
        $source.sourceId -eq $sourceId -and $source.active -eq $true -and
        $sourceProperties -notcontains "apiKey" -and $sourceProperties -notcontains "apiKeyHash" -and
        $sourceProperties -notcontains "apiKeySalt"
    ) @{ sourceId = $source.sourceId; properties = $sourceProperties }

    $listedSources = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/laboratory-sources" `
        -Headers $headers -TimeoutSec 20
    $listedSource = @($listedSources | Where-Object { $_.sourceId -eq $sourceId } | Select-Object -First 1)
    $listedProperties = if ($listedSource.Count -eq 1) { @($listedSource[0].PSObject.Properties.Name) } else { @() }
    Add-Check "Source registry exposes governed metadata only" (
        $listedSource.Count -eq 1 -and $listedSource[0].active -eq $true -and
        $listedProperties -notcontains "apiKey" -and $listedProperties -notcontains "apiKeyHash" -and
        $listedProperties -notcontains "apiKeySalt"
    ) @{ sourceCount = $listedSource.Count; properties = $listedProperties }

    $deactivated = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/laboratory-sources/$sourceId/deactivate" `
        -Method Post -Headers $headers -ContentType "application/json" `
        -Body (@{ reason = "Synthetic source-registry verification completed." } | ConvertTo-Json) -TimeoutSec 20
    $listedAfterDeactivation = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/laboratory-sources" `
        -Headers $headers -TimeoutSec 20
    $inactiveSource = @($listedAfterDeactivation | Where-Object { $_.sourceId -eq $sourceId } | Select-Object -First 1)
    Add-Check "Source deactivation is retained and visible" (
        $deactivated.active -eq $false -and -not [string]::IsNullOrWhiteSpace($deactivated.deactivatedAt) -and
        $inactiveSource.Count -eq 1 -and $inactiveSource[0].active -eq $false
    ) @{ sourceId = $sourceId; deactivatedAt = $deactivated.deactivatedAt }
}
catch {
    Add-Check "Unhandled source-registry test error" $false $_.Exception.Message
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
