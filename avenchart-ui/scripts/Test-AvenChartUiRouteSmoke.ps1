# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$BaseUrl = "http://127.0.0.1:3100"
)

$ErrorActionPreference = "Stop"

$UiRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $UiRoot "test-results"
$ResultPath = Join-Path $ArtifactsRoot "route-smoke-result.json"

New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

$startedAt = Get-Date
$status = "failed"
$exitCode = 1

Push-Location $UiRoot
try {
    $env:MODERN_UI_BASE_URL = $BaseUrl
    # Route projects intentionally reuse the same seeded staff and portal
    # identities. Keep them serial so concurrent login/logout does not race
    # those shared sessions and create a false navigation failure.
    & npx.cmd playwright test e2e/route-smoke.spec.ts --workers=1
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
        workers = 1
        exitCode = $exitCode
        durationMilliseconds = [int]((Get-Date) - $startedAt).TotalMilliseconds
        reportPath = "avenchart-ui/playwright-report/index.html"
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
}

if ($exitCode -ne 0) {
    throw "AvenChart UI route smoke failed. See $ResultPath."
}

Write-Host "AvenChart UI route smoke passed: $ResultPath"
