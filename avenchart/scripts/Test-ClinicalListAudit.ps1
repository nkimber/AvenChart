# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
Add-Type -AssemblyName System.Net.Http

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-clinical-list-audit-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$headers = $null
$patientId = $null

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
                ($Body | ConvertTo-Json -Depth 10), [Text.Encoding]::UTF8, "application/json")
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

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post `
        -ContentType "application/json" -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "Administration login did not issue an active session."
    }
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login

    $marker = [Guid]::NewGuid().ToString("N").Substring(0, 10)
    $patient = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/" -Method Post -Headers $headers `
        -ContentType "application/json" -Body (@{
            pubpid = "TMP-PAT-REG-CLN-AUDIT-$marker"
            firstName = "Clinical"
            lastName = "Audit$marker"
            sex = "Unknown"
            dateOfBirth = "1990-01-01"
            hipaaAllowSms = "NO"
            hipaaAllowEmail = "NO"
        } | ConvertTo-Json) -TimeoutSec 20
    $patientId = $patient.canonicalId

    $problem = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems" -Method Post -Headers $headers `
        -ContentType "application/json" -Body (@{
            patientId = $patientId
            title = "Clinical audit verification problem"
            dateTime = "2027-01-05"
            diagnosis = "Z00.00"
            comments = "Initial fixture entry for audit verification."
        } | ConvertTo-Json) -TimeoutSec 20
    $problemId = $problem.id

    $createdHistory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$problemId/audit-history" `
        -Headers $headers -TimeoutSec 20
    Add-Check "Problem creation retains immutable actor and state evidence" (
        $createdHistory.eventCount -eq 1 -and
        $createdHistory.events[0].action -eq "created" -and
        $createdHistory.events[0].actor -eq "admin" -and
        $createdHistory.events[0].stateJson -match [regex]::Escape($problemId)
    ) @{
        eventCount = $createdHistory.eventCount
        action = $createdHistory.events[0].action
        actor = $createdHistory.events[0].actor
    }

    $missingReasonStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/clinical-lists/problems/$problemId/deactivate" `
        -Method Put -RequestHeaders $headers -Body @{ comments = "" }
    $deactivated = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$problemId/deactivate" `
        -Method Put -Headers $headers -ContentType "application/json" `
        -Body (@{ comments = "Focused clinical audit verification deactivation." } | ConvertTo-Json) -TimeoutSec 20
    $deactivatedHistory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$problemId/audit-history" `
        -Headers $headers -TimeoutSec 20
    Add-Check "Problem deactivation requires reason and appends evidence" (
        $missingReasonStatus -eq 400 -and
        $deactivatedHistory.eventCount -eq 2 -and
        $deactivatedHistory.events[0].action -eq "deactivated" -and
        $deactivatedHistory.events[0].actor -eq "admin" -and
        $deactivatedHistory.events[0].reason -eq "Focused clinical audit verification deactivation." -and
        $deactivated.detail.problems.Where({ $_.id -eq $problemId })[0].activity -eq 0
    ) @{
        missingReasonStatus = $missingReasonStatus
        eventCount = $deactivatedHistory.eventCount
        latestAction = $deactivatedHistory.events[0].action
        latestActor = $deactivatedHistory.events[0].actor
    }

    $prescription = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions" -Method Post -Headers $headers `
        -ContentType "application/json" -Body (@{
            patientId = $patientId
            startDate = "2027-01-05"
            drug = "Clinical audit verification prescription"
            dosage = "One tablet daily"
            quantity = "1"
            refills = 0
            note = "Initial fixture entry for prescription retention verification."
            diagnosis = "Z00.00"
        } | ConvertTo-Json) -TimeoutSec 20
    $prescriptionId = $prescription.id
    $deletePrescriptionStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$prescriptionId" `
        -Method Delete -RequestHeaders $headers
    $prescriptionHistory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$prescriptionId/audit-history" `
        -Headers $headers -TimeoutSec 20
    Add-Check "Prescription deletion is rejected without losing its audit trail" (
        $deletePrescriptionStatus -eq 409 -and
        $prescriptionHistory.eventCount -eq 1 -and
        $prescriptionHistory.events[0].action -eq "create" -and
        $prescriptionHistory.events[0].actor -eq "admin"
    ) @{
        deleteStatus = $deletePrescriptionStatus
        eventCount = $prescriptionHistory.eventCount
        action = $prescriptionHistory.events[0].action
        actor = $prescriptionHistory.events[0].actor
    }
}
catch {
    Add-Check "Unhandled clinical-list audit test error" $false $_.Exception.Message
}
finally {
    if ($null -ne $headers -and $patientId) {
        $patientCleanupStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/patients/$patientId" -Method Delete -RequestHeaders $headers
        Add-Check "Synthetic patient cleanup" ($patientCleanupStatus -eq 204) @{ status = $patientCleanupStatus }
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
