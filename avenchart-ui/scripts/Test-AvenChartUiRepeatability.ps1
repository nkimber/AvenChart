# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$BaseUrl = "http://127.0.0.1:3100",
    [string]$Project = "desktop-chromium"
)

$ErrorActionPreference = "Stop"

$UiRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $UiRoot "test-results"
$ResultPath = Join-Path $ArtifactsRoot "repeatability-result.json"
$ResetScript = Join-Path $PSScriptRoot "Reset-AvenChartUiDemo.ps1"
$runs = @()
$SeedLock = [System.Threading.Mutex]::new($false, "Global\AvenChartGoldSeed")
$SeedLockHeld = $false
$LocationPushed = $false

New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

try {
    $SeedLockHeld = $SeedLock.WaitOne([TimeSpan]::FromMinutes(15))
    if (-not $SeedLockHeld) {
        throw "Timed out waiting for exclusive access to the AvenChart demo database."
    }

    Push-Location $UiRoot
    $LocationPushed = $true
    foreach ($runNumber in 1..2) {
        $resetJson = & $ResetScript -Json
        $reset = $resetJson | ConvertFrom-Json

        $env:MODERN_UI_BASE_URL = $BaseUrl
        # A clean reset must be deterministic. Run the shared-route smoke
        # checks serially so their authenticated navigation contexts cannot
        # compete while the fresh runtime is warming its lazy route modules.
        npx playwright test e2e/route-smoke.spec.ts --project=$Project --workers=1
        if ($LASTEXITCODE -ne 0) {
            throw "AvenChart UI route smoke run $runNumber failed with exit code $LASTEXITCODE."
        }

        $runs += [ordered]@{
            run = $runNumber
            datasetId = $reset.datasetId
            counts = $reset.counts
        }
    }

    $firstCounts = $runs[0].counts | ConvertTo-Json -Compress
    $secondCounts = $runs[1].counts | ConvertTo-Json -Compress
    if ($firstCounts -ne $secondCounts) {
        throw "Clean-demo counts changed between consecutive runs."
    }

    $result = [ordered]@{
        status = "passed"
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        baseUrl = $BaseUrl
        project = $Project
        countsMatch = $true
        runs = $runs
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
    Write-Host "AvenChart UI repeatability verified: $ResultPath"
}
finally {
    Remove-Item Env:\MODERN_UI_BASE_URL -ErrorAction SilentlyContinue
    if ($LocationPushed) {
        Pop-Location
    }
    if ($SeedLockHeld) {
        $SeedLock.ReleaseMutex()
    }
    $SeedLock.Dispose()
}
