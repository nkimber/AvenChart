param(
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$UiRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ProjectRoot = Resolve-Path (Join-Path $UiRoot "..")
$ModernizedRoot = Join-Path $ProjectRoot "avenchart"
$SeedScript = Join-Path $ModernizedRoot "scripts\Seed-ModernizedGoldDataset.ps1"
$SeedResult = Join-Path $ModernizedRoot "artifacts\latest-modernized-seed-result.json"

if (-not (Test-Path -LiteralPath $SeedScript)) {
    throw "The documented modernized gold-dataset reset was not found at $SeedScript."
}

& $SeedScript | Out-Host
$seedExitCode = $LASTEXITCODE
if ($seedExitCode -ne 0) {
    throw "The modernized gold-dataset reset failed with exit code $seedExitCode."
}

$result = Get-Content -LiteralPath $SeedResult -Raw | ConvertFrom-Json
if ($result.status -ne "passed" -or $result.datasetId -ne "legacy-ehr-shared-synthetic-v1") {
    throw "The reset did not produce a verified legacy-ehr-shared-synthetic-v1 result."
}

if ($Json) {
    $result | ConvertTo-Json -Depth 8
}
else {
    Write-Host "Modern UI clean-demo reset verified against $($result.datasetId)."
    $result.counts | Format-List
}
