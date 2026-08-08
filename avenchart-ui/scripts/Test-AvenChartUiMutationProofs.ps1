# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$BaseUrl = "http://127.0.0.1:3100"
)

$ErrorActionPreference = "Stop"

$UiRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ProjectRoot = Resolve-Path (Join-Path $UiRoot "..")
$MigrationScript = Join-Path $ProjectRoot "avenchart\scripts\Invoke-AvenChartMigrations.ps1"
$ArtifactsRoot = Join-Path $UiRoot "test-results"
$ResultPath = Join-Path $ArtifactsRoot "isolated-mutation-workflows-result.json"
$startedAt = Get-Date
$status = "failed"
$exitCode = 1

New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

Push-Location $UiRoot
try {
    & $MigrationScript
    if ($LASTEXITCODE -ne 0) {
        throw "AvenChart schema migrations failed before the isolated mutation proofs."
    }

    $env:MODERN_UI_BASE_URL = $BaseUrl
    $env:MODERN_UI_RUN_ISOLATED_MUTATIONS = "1"
    & npx.cmd playwright test e2e/isolated-mutation-workflows.spec.ts --project=desktop-chromium --workers=1
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        $status = "passed"
    }
}
finally {
    Remove-Item Env:\MODERN_UI_BASE_URL -ErrorAction SilentlyContinue
    Remove-Item Env:\MODERN_UI_RUN_ISOLATED_MUTATIONS -ErrorAction SilentlyContinue
    Pop-Location
    $result = [ordered]@{
        status = $status
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        baseUrl = $BaseUrl
        exitCode = $exitCode
        durationMilliseconds = [int]((Get-Date) - $startedAt).TotalMilliseconds
        browserProfile = "desktop-chromium"
        workerCount = 1
        reportPath = "avenchart-ui/playwright-report/index.html"
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
}

if ($exitCode -ne 0) {
    throw "AvenChart UI isolated mutation workflows failed. See $ResultPath."
}

Write-Host "AvenChart UI isolated mutation workflows passed: $ResultPath"
