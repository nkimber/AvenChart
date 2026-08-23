# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001"
)

$ErrorActionPreference = "Stop"
$target = [Uri]$ApiBaseUrl
if ($target.Host -notin @("localhost", "127.0.0.1", "::1")) {
    throw "This proof temporarily changes and restores a synthetic portal account, so it is restricted to a local API target."
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-patient-portal-login-disclosure-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$fixturePatientId = "MOD-PAT-0004"
$fixtureUsername = "mod-pat-0004@example.test"
$fixturePassword = "PortalPass207!"
$originalState = $null
$fixtureStateChanged = $false

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)

    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) { $script:status = "failed" }
}

function Invoke-FixtureSql {
    param([string]$Sql)

    Push-Location $solutionRoot
    try {
        $output = docker compose exec -T postgres psql -U avenchart -d avenchart -t -A -v ON_ERROR_STOP=1 -c $Sql
        if ($LASTEXITCODE -ne 0) { throw "Synthetic portal fixture database command failed." }
        return $output
    }
    finally { Pop-Location }
}

function Set-FixturePortalState {
    param(
        [bool]$PortalEnabled,
        [int]$PasswordStatus,
        [string]$OneTimeToken
    )

    $encodedToken = if ([string]::IsNullOrEmpty($OneTimeToken)) { "" } else { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($OneTimeToken)) }
    $tokenExpression = if ([string]::IsNullOrEmpty($encodedToken)) {
        "null"
    }
    else {
        "convert_from(decode('$encodedToken','base64'),'UTF8')"
    }
    $enabledValue = if ($PortalEnabled) { "true" } else { "false" }
    Invoke-FixtureSql "
        update patients set portal_enabled=$enabledValue where canonical_id='$fixturePatientId';
        update patient_portal_accounts
        set password_status=$PasswordStatus,one_time_token=$tokenExpression
        where patient_id='$fixturePatientId';" | Out-Null
}

function Invoke-PortalLogin {
    param([string]$Username, [string]$Password)

    return Invoke-RestMethod -Uri "$ApiBaseUrl/api/patient-portal/login" -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = $Username; password = $Password } | ConvertTo-Json) `
        -TimeoutSec 20
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 15
    Add-Check "API readiness" ($health.status -eq "healthy") $health

    $stateRow = @(Invoke-FixtureSql "
        select p.portal_enabled::text || '|' || ppa.password_status::text || '|' ||
               coalesce(encode(convert_to(ppa.one_time_token,'UTF8'),'base64'),'')
        from patients p
        join patient_portal_accounts ppa on ppa.patient_id=p.canonical_id
        where p.canonical_id='$fixturePatientId';" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1)
    if ($stateRow.Count -ne 1) { throw "The expected synthetic portal fixture account was not found." }
    $stateParts = $stateRow[0].Split('|', 3)
    if ($stateParts.Count -ne 3 -or $stateParts[0] -notin @("true", "false") -or $stateParts[1] -notmatch '^\d+$') {
        throw "The synthetic portal fixture state could not be read safely."
    }
    $originalState = [ordered]@{
        portalEnabled = $stateParts[0] -eq "true"
        passwordStatus = [int]$stateParts[1]
        oneTimeToken = if ([string]::IsNullOrEmpty($stateParts[2])) { "" } else { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($stateParts[2])) }
    }

    $unknown = Invoke-PortalLogin "unknown-$([Guid]::NewGuid().ToString('N'))@example.test" "WrongPortal207!"
    $activeWrongPassword = Invoke-PortalLogin $fixtureUsername "WrongPortal207!"

    Set-FixturePortalState -PortalEnabled $true -PasswordStatus 1 -OneTimeToken "phase3-reset-$([Guid]::NewGuid().ToString('N'))"
    $fixtureStateChanged = $true
    $resetPending = Invoke-PortalLogin $fixtureUsername "WrongPortal207!"

    Set-FixturePortalState -PortalEnabled $true -PasswordStatus 0 -OneTimeToken ""
    $pendingSetup = Invoke-PortalLogin $fixtureUsername "WrongPortal207!"

    Set-FixturePortalState -PortalEnabled $false -PasswordStatus 1 -OneTimeToken ""
    $disabled = Invoke-PortalLogin $fixtureUsername "WrongPortal207!"

    $responses = @($unknown, $activeWrongPassword, $resetPending, $pendingSetup, $disabled)
    Add-Check "Unknown, reset-pending, pending-setup, and disabled portal accounts return one generic failure" (
        @($responses | Where-Object {
            $_.authenticated -ne $false -or $_.failureReason -ne "Invalid username or password." -or
            -not [string]::IsNullOrEmpty($_.canonicalId) -or -not [string]::IsNullOrEmpty($_.portalUsername)
        }).Count -eq 0
    ) @{ responses = $responses | ForEach-Object { @{ authenticated = $_.authenticated; failureReason = $_.failureReason; username = $_.username } } }
}
catch {
    Add-Check "Unhandled portal login disclosure test error" $false $_.Exception.Message
}
finally {
    if ($fixtureStateChanged -and $null -ne $originalState) {
        try {
            Set-FixturePortalState -PortalEnabled $originalState.portalEnabled -PasswordStatus $originalState.passwordStatus -OneTimeToken $originalState.oneTimeToken
            $restoredLogin = Invoke-PortalLogin $fixtureUsername $fixturePassword
            Add-Check "Synthetic portal fixture is restored and can authenticate" ($restoredLogin.authenticated -eq $true) @{ authenticated = $restoredLogin.authenticated }
        }
        catch {
            Add-Check "Restore synthetic portal fixture" $false $_.Exception.Message
        }
    }

    $result = [ordered]@{
        status = $status
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
        checks = $checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}

if ($status -ne "passed") { exit 1 }
