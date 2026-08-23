# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$BaseUrl = "http://127.0.0.1:3100",
    [string]$ApiBaseUrl = "http://127.0.0.1:5001",
    [string]$TestFilter = ""
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
$originalAdministratorGrant = $null
$administratorLogin = $null
$grantProvisioned = $false
$restorationError = $null

. (Join-Path $ProjectRoot "avenchart\scripts\AvenChartStaffAccessContext.ps1")

New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

Push-Location $UiRoot
try {
    & $MigrationScript
    if ($LASTEXITCODE -ne 0) {
        throw "AvenChart schema migrations failed before the isolated mutation proofs."
    }

    # The synthetic mutation fixtures exercise North-facility patients. Grant
    # that facility only for this proof run, then restore the seeded
    # Main-only administrator boundary in finally.
    $administratorLogin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    $administrationHeaders = New-AvenChartStaffAccessContextHeaders `
        -Login $administratorLogin `
        -PurposeOfUse "healthcare-operations"
    $currentGrant = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/administration/access-context-grants/admin" `
        -Method Get `
        -Headers $administrationHeaders `
        -TimeoutSec 20
    $currentFacilityIds = @($currentGrant.facilities | ForEach-Object { [int]$_.facilityId } | Sort-Object -Unique)
    $defaultFacility = @($currentGrant.facilities | Where-Object { $_.isDefault -eq $true } | Select-Object -First 1)
    if ($defaultFacility.Count -ne 1) {
        throw "The synthetic administrator access-context grant must have exactly one default facility."
    }
    if ($currentFacilityIds -notcontains 11) {
        $originalAdministratorGrant = @{
            facilityIds = $currentFacilityIds
            defaultFacilityId = [int]$defaultFacility[0].facilityId
            purposes = @($currentGrant.purposes)
        }
        Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/administration/access-context-grants/admin" `
            -Method Put `
            -Headers $administrationHeaders `
            -ContentType "application/json" `
            -Body (@{
                facilityIds = @($currentFacilityIds + 11 | Sort-Object -Unique)
                defaultFacilityId = $originalAdministratorGrant.defaultFacilityId
                purposes = $originalAdministratorGrant.purposes
            } | ConvertTo-Json -Depth 5) `
            -TimeoutSec 20 | Out-Null
        $grantProvisioned = $true
    }

    $env:MODERN_UI_BASE_URL = $BaseUrl
    $env:MODERN_UI_RUN_ISOLATED_MUTATIONS = "1"
    $playwrightArguments = @(
        "playwright",
        "test",
        "e2e/isolated-mutation-workflows.spec.ts",
        "--project=desktop-chromium",
        "--workers=1"
    )
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $playwrightArguments += @("--grep", $TestFilter)
    }
    & npx.cmd @playwrightArguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        $status = "passed"
    }
}
finally {
    Remove-Item Env:\MODERN_UI_BASE_URL -ErrorAction SilentlyContinue
    Remove-Item Env:\MODERN_UI_RUN_ISOLATED_MUTATIONS -ErrorAction SilentlyContinue
    if ($null -ne $originalAdministratorGrant -and $null -ne $administratorLogin) {
        try {
            $restoreHeaders = New-AvenChartStaffAccessContextHeaders `
                -Login $administratorLogin `
                -FacilityId $originalAdministratorGrant.defaultFacilityId `
                -PurposeOfUse "healthcare-operations"
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/administration/access-context-grants/admin" `
                -Method Put `
                -Headers $restoreHeaders `
                -ContentType "application/json" `
                -Body ($originalAdministratorGrant | ConvertTo-Json -Depth 5) `
                -TimeoutSec 20 | Out-Null
        }
        catch {
            $restorationError = $_.Exception.Message
            $status = "failed"
            $exitCode = 1
        }
    }
    Pop-Location
    $result = [ordered]@{
        status = $status
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        baseUrl = $BaseUrl
        apiBaseUrl = $ApiBaseUrl
        exitCode = $exitCode
        durationMilliseconds = [int]((Get-Date) - $startedAt).TotalMilliseconds
        temporaryNorthFacilityGrantProvisioned = $grantProvisioned
        accessContextGrantRestorationError = $restorationError
        browserProfile = "desktop-chromium"
        workerCount = 1
        testFilter = $TestFilter
        reportPath = "avenchart-ui/playwright-report/index.html"
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
}

if ($exitCode -ne 0) {
    throw "AvenChart UI isolated mutation workflows failed. See $ResultPath."
}

Write-Host "AvenChart UI isolated mutation workflows passed: $ResultPath"
