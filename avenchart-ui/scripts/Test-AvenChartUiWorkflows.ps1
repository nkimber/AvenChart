# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$BaseUrl = "http://127.0.0.1:3100"
)

$ErrorActionPreference = "Stop"

$UiRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $UiRoot "test-results"
$ResultPath = Join-Path $ArtifactsRoot "material-workflows-result.json"
$startedAt = Get-Date
$status = "failed"
$exitCode = 1

New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

Push-Location $UiRoot
try {
    $env:MODERN_UI_BASE_URL = $BaseUrl
    & npx.cmd playwright test e2e/material-workflows.spec.ts --workers=4
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        $status = "passed"
    }
}
finally {
    Remove-Item Env:\MODERN_UI_BASE_URL -ErrorAction SilentlyContinue
    Pop-Location
    $result = [ordered]@{
        status = $status
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        baseUrl = $BaseUrl
        exitCode = $exitCode
        durationMilliseconds = [int]((Get-Date) - $startedAt).TotalMilliseconds
        browserProfiles = @(
            "desktop-chromium",
            "mobile-chromium",
            "desktop-firefox",
            "desktop-webkit"
        )
        reportPath = "avenchart-ui/playwright-report/index.html"
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
}

if ($exitCode -ne 0) {
    throw "AvenChart UI material workflows failed. See $ResultPath."
}

Write-Host "AvenChart UI material workflows passed: $ResultPath"
