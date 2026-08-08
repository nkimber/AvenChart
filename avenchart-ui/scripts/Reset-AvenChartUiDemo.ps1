# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$UiRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ProjectRoot = Resolve-Path (Join-Path $UiRoot "..")
$AvenChartRoot = Join-Path $ProjectRoot "avenchart"
$SeedScript = Join-Path $AvenChartRoot "scripts\Seed-AvenChartGoldDataset.ps1"
$SeedResult = Join-Path $AvenChartRoot "artifacts\latest-avenchart-seed-result.json"

if (-not (Test-Path -LiteralPath $SeedScript)) {
    throw "The documented AvenChart gold-dataset reset was not found at $SeedScript."
}

& $SeedScript | Out-Host
$seedExitCode = $LASTEXITCODE
if ($seedExitCode -ne 0) {
    throw "The AvenChart gold-dataset reset failed with exit code $seedExitCode."
}

$result = Get-Content -LiteralPath $SeedResult -Raw | ConvertFrom-Json
if ($result.status -ne "passed" -or $result.datasetId -ne "avenchart-shared-synthetic-v1") {
    throw "The reset did not produce a verified avenchart-shared-synthetic-v1 result."
}

if ($Json) {
    $result | ConvertTo-Json -Depth 8
}
else {
    Write-Host "AvenChart UI clean-demo reset verified against $($result.datasetId)."
    $result.counts | Format-List
}
