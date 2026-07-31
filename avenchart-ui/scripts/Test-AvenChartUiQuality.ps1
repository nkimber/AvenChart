# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

$ErrorActionPreference = "Stop"

$UiRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $UiRoot "test-results"
$ResultPath = Join-Path $ArtifactsRoot "quality-result.json"
$checks = @()
$overallStatus = "passed"

New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

Push-Location $UiRoot
try {
    foreach ($check in @(
        @{ Name = "lint"; Arguments = @("run", "lint") },
        @{ Name = "unit"; Arguments = @("test") },
        @{ Name = "build-and-bundle-budget"; Arguments = @("run", "build") }
    )) {
        $startedAt = Get-Date
        & npm.cmd @($check.Arguments)
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            $overallStatus = "failed"
        }
        $checks += [ordered]@{
            name = $check.Name
            status = if ($exitCode -eq 0) { "passed" } else { "failed" }
            exitCode = $exitCode
            durationMilliseconds = [int]((Get-Date) - $startedAt).TotalMilliseconds
        }
    }
}
finally {
    Pop-Location
}

$result = [ordered]@{
    status = $overallStatus
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    checks = $checks
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultPath -Encoding UTF8

if ($overallStatus -ne "passed") {
    throw "Modern UI quality checks failed. See $ResultPath."
}

Write-Host "Modern UI quality checks passed: $ResultPath"
