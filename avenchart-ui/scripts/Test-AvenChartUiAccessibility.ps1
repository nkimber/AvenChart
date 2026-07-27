param(
    [string]$BaseUrl = "http://127.0.0.1:3100"
)

$ErrorActionPreference = "Stop"

$UiRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $UiRoot "test-results"
$ResultPath = Join-Path $ArtifactsRoot "accessibility-result.json"
$startedAt = Get-Date
$status = "failed"
$exitCode = 1

New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

Push-Location $UiRoot
try {
    $env:MODERN_UI_BASE_URL = $BaseUrl
    & npx.cmd playwright test e2e/accessibility.spec.ts --project=desktop-chromium --project=mobile-chromium
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
        standard = "WCAG 2.1 A/AA"
        maximumImpact = "moderate"
        reportPath = "avenchart-ui/playwright-report/index.html"
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
}

if ($exitCode -ne 0) {
    throw "Modern UI accessibility gate failed. See $ResultPath."
}

Write-Host "Modern UI accessibility gate passed: $ResultPath"
