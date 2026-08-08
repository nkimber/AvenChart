# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001",
    [string]$LegacyRoot = ""
)

$ErrorActionPreference = "Stop"

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($LegacyRoot)) {
    $LegacyRoot = Join-Path $solutionRoot "..\legacy-ehr"
}
$LegacyRoot = Resolve-Path $LegacyRoot
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-legacy-module-registry-parity.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)
    $script:checks.Add([ordered]@{ name = $Name; status = if ($Passed) { "passed" } else { "failed" }; details = $Details })
    if (-not $Passed) { $script:status = "failed" }
}

function Get-LegacyEnvironment {
    $values = @{}
    Get-Content (Join-Path $LegacyRoot ".env") | ForEach-Object {
        if ($_ -match '^\s*([^#=]+)=(.*)$') {
            $values[$matches[1].Trim()] = $matches[2].Trim().Trim('"')
        }
    }
    foreach ($name in @("MYSQL_USER", "MYSQL_DATABASE", "MYSQL_PASSWORD")) {
        if ([string]::IsNullOrWhiteSpace($values[$name])) { throw "Legacy .env does not define $name." }
    }
    return $values
}

$moduleMap = [ordered]@{
    "oe-module-claimrev-connect" = @{ key = "CLAIMREV_CONNECT"; status = "partner-gated" }
    "oe-module-comlink-telehealth" = @{ key = "COMLINK_TELEHEALTH"; status = "partner-gated" }
    "oe-module-dashboard-context" = @{ key = "DASHBOARD_CONTEXT"; status = "decision-required" }
    "oe-module-dorn" = @{ key = "DORN"; status = "partner-gated" }
    "oe-module-ehi-exporter" = @{ key = "EHI_EXPORTER"; status = "decision-required" }
    "oe-module-faxsms" = @{ key = "FAX_SMS"; status = "partner-gated" }
    "oe-module-prior-authorizations" = @{ key = "PRIOR_AUTHORIZATIONS"; status = "decision-required" }
    "oe-module-weno" = @{ key = "WENO"; status = "partner-gated" }
    "Carecoordination" = @{ key = "CARECOORDINATION"; status = "decision-required" }
    "Ccr" = @{ key = "CCR"; status = "decision-required" }
    "Documents" = @{ key = "DOCUMENTS_MODULE"; status = "decision-required" }
    "Immunization" = @{ key = "IMMUNIZATION_MODULE"; status = "decision-required" }
    "Syndromicsurveillance" = @{ key = "SYNDROMIC_SURVEILLANCE"; status = "decision-required" }
    "CodeTypes" = @{ key = "CODE_TYPES"; status = "decision-required" }
    "PatientFilter" = @{ key = "PATIENT_FILTER"; status = "decision-required" }
    "Patientvalidation" = @{ key = "PATIENT_VALIDATION"; status = "decision-required" }
    "PrescriptionTemplates" = @{ key = "PRESCRIPTION_TEMPLATES"; status = "decision-required" }
}

$legacyModules = @()
try {
    $legacyEnvironment = Get-LegacyEnvironment
    $legacyRows = & docker compose -f (Join-Path $LegacyRoot "docker-compose.yml") --project-directory $LegacyRoot exec -T -e "MYSQL_PWD=$($legacyEnvironment['MYSQL_PASSWORD'])" mysql mariadb -u $legacyEnvironment['MYSQL_USER'] $legacyEnvironment['MYSQL_DATABASE'] --batch --skip-column-names -e "select mod_directory,mod_active,mod_ui_active,sql_run from modules order by mod_directory"
    if ($LASTEXITCODE -ne 0) { throw "The legacy modules registry query failed." }
    $legacyModules = @($legacyRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object {
        $parts = $_ -split "`t"
        [pscustomobject]@{ directory = $parts[0]; active = [int]$parts[1]; uiActive = [int]$parts[2]; sqlRun = [int]$parts[3] }
    } | Where-Object { $moduleMap.Contains($_.directory) })
    $sourcePassed = $legacyModules.Count -eq $moduleMap.Count
    Add-Check "Legacy module registry has every mapped source record" $sourcePassed @{ directories = $legacyModules.directory; count = $legacyModules.Count }
}
catch {
    Add-Check "Legacy module registry has every mapped source record" $false $_.Exception.Message
}

try {
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}' -TimeoutSec 20
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) { throw "AvenChart administrator login failed." }
    $catalog = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/modules" -Headers @{ "X-AvenChart-Session" = $login.sessionId } -TimeoutSec 20
    $moduleByKey = @{}
    @($catalog.modules) | ForEach-Object { $moduleByKey[$_.key] = $_ }
    $mismatches = @($legacyModules | ForEach-Object {
        $mapping = $moduleMap[$_.directory]
        $target = $moduleByKey[$mapping.key]
        $flags = "mod_active=$($_.active), mod_ui_active=$($_.uiActive), sql_run=$($_.sqlRun)"
        if ($null -eq $target -or $target.status -ne $mapping.status -or $target.canChangeStatus -or $target.description -notmatch [regex]::Escape($flags)) {
            [pscustomobject]@{ directory = $_.directory; targetKey = $mapping.key; expectedStatus = $mapping.status; expectedFlags = $flags; actualStatus = if ($null -eq $target) { $null } else { $target.status }; editable = if ($null -eq $target) { $null } else { $target.canChangeStatus } }
        }
    })
    Add-Check "Target module catalog reconciles source registry state without target enablement" ($mismatches.Count -eq 0 -and $legacyModules.Count -eq $moduleMap.Count) @{ matchedCount = $legacyModules.Count; mismatches = $mismatches }
}
catch {
    Add-Check "Target module catalog reconciles source registry state without target enablement" $false $_.Exception.Message
}

[ordered]@{ status = $status; generatedAtUtc = [DateTime]::UtcNow.ToString("O"); apiBaseUrl = $ApiBaseUrl; legacyRoot = $LegacyRoot; checks = $checks } | ConvertTo-Json -Depth 12 | Set-Content -Path $resultPath -Encoding utf8
Get-Content -Raw $resultPath
if ($status -ne "passed") { exit 1 }
