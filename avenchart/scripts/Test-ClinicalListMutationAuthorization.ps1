# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001",
    [switch]$AllowNonLocalTarget
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
Add-Type -AssemblyName System.Net.Http

$target = [Uri]$ApiBaseUrl
if (-not $AllowNonLocalTarget -and $target.Host -notin @("localhost", "127.0.0.1", "::1")) {
    throw "This verification temporarily grants a synthetic view permission and is restricted to a local API target. Use -AllowNonLocalTarget only in an approved disposable environment."
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-clinical-list-mutation-authorization-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$administrationHeaders = $null
$originalPermission = $null
$temporaryPermissionApplied = $false

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)

    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) { $script:status = "failed" }
}

function Get-HttpStatus {
    param(
        [string]$Uri,
        [string]$Method,
        [hashtable]$RequestHeaders = @{},
        [object]$Body = $null
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(20)
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($Method), $Uri)
        foreach ($entry in $RequestHeaders.GetEnumerator()) {
            $request.Headers.TryAddWithoutValidation([string]$entry.Key, [string]$entry.Value) | Out-Null
        }
        if ($null -ne $Body) {
            $request.Content = [System.Net.Http.StringContent]::new(
                ($Body | ConvertTo-Json -Depth 10),
                [Text.Encoding]::UTF8,
                "application/json")
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try { return [int]$response.StatusCode } finally { $response.Dispose() }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -TimeoutSec 15
    Add-Check "API health" ($health.status -eq "healthy") $health

    $adminLogin = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    if (-not $adminLogin.authenticated -or [string]::IsNullOrWhiteSpace($adminLogin.sessionId)) {
        throw "Administration login did not issue an active session."
    }
    $administrationHeaders = New-AvenChartStaffAccessContextHeaders -Login $adminLogin

    $directory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/directory" `
        -Headers $administrationHeaders -TimeoutSec 20
    $originalPermission = @($directory.accessControl.groupPermissions | Where-Object {
        $_.groupValue -eq "front" -and
        $_.sectionValue -eq "patients" -and
        $_.permissionValue -eq "med"
    } | Select-Object -First 1)

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" `
        -Method Put `
        -Headers $administrationHeaders `
        -ContentType "application/json" `
        -Body (@{
            groupValue = "front"
            sectionValue = "patients"
            permissionValue = "med"
            returnValue = "view"
        } | ConvertTo-Json) `
        -TimeoutSec 20 | Out-Null
    $temporaryPermissionApplied = $true

    $frontDeskLogin = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "gold-frontdesk-01"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    if (-not $frontDeskLogin.authenticated -or [string]::IsNullOrWhiteSpace($frontDeskLogin.sessionId)) {
        throw "Synthetic front-desk login did not issue an active session."
    }
    $frontDeskHeaders = New-AvenChartStaffAccessContextHeaders -Login $frontDeskLogin

    $viewStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/clinical-lists/medication-vocabulary?query=metformin" `
        -Method Get `
        -RequestHeaders $frontDeskHeaders
    $mutationStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/clinical-lists/problems" `
        -Method Post `
        -RequestHeaders $frontDeskHeaders `
        -Body @{
            patientId = "MOD-PAT-0001"
            title = "This write authorization proof must not persist"
            dateTime = "2026-08-22"
            diagnosis = "Z00.00"
            comments = "The request must be rejected before a clinical mutation occurs."
        }

    Add-Check "Clinical-list view permission cannot create clinical content" (
        $viewStatus -eq 200 -and $mutationStatus -eq 403
    ) @{
        viewStatus = $viewStatus
        mutationStatus = $mutationStatus
    }
}
catch {
    Add-Check "Unhandled clinical-list mutation authorization test error" $false $_.Exception.Message
}
finally {
    if ($null -ne $administrationHeaders -and $temporaryPermissionApplied) {
        try {
            if ($originalPermission.Count -eq 1) {
                Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" `
                    -Method Put `
                    -Headers $administrationHeaders `
                    -ContentType "application/json" `
                    -Body (@{
                        groupValue = "front"
                        sectionValue = "patients"
                        permissionValue = "med"
                        returnValue = $originalPermission[0].returnValue
                    } | ConvertTo-Json) `
                    -TimeoutSec 20 | Out-Null
            }
            else {
                Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions/front/patients/med" `
                    -Method Delete `
                    -Headers $administrationHeaders `
                    -UseBasicParsing `
                    -TimeoutSec 20 | Out-Null
            }
        }
        catch {
            Add-Check "Restore synthetic front-desk medical-history permission" $false $_.Exception.Message
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
