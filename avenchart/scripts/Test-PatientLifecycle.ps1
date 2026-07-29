param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-patient-lifecycle-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$headers = $null
$patientId = $null
$appointmentId = $null

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
    $headers = @{ "X-Legacy EHR-Session" = $login.sessionId }

    $unauthenticatedHistory = Get-HttpStatus -Uri "$ApiBaseUrl/api/patients/unknown/lifecycle-history" -Method Get
    Add-Check "Lifecycle history is protected" ($unauthenticatedHistory -eq 401) @{ status = $unauthenticatedHistory }

    $marker = [Guid]::NewGuid().ToString("N").Substring(0, 10)
    $patient = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/" -Method Post -Headers $headers `
        -ContentType "application/json" -Body (@{
            pubpid = "TMP-PAT-REG-LC-$marker"
            firstName = "Lifecycle"
            lastName = "Verification$marker"
            sex = "Unknown"
            dateOfBirth = "1990-01-01"
            hipaaAllowSms = "NO"
            hipaaAllowEmail = "NO"
        } | ConvertTo-Json) -TimeoutSec 20
    $patientId = $patient.canonicalId

    $missingReasonStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/patients/$patientId/lifecycle/retire" `
        -Method Post -RequestHeaders $headers -Body @{ reason = "" }
    $retired = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$patientId/lifecycle/retire" -Method Post `
        -Headers $headers -ContentType "application/json" `
        -Body (@{ reason = "Focused lifecycle fixture retirement." } | ConvertTo-Json) -TimeoutSec 20
    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$patientId/lifecycle-history" -Headers $headers -TimeoutSec 20
    Add-Check "Required retirement evidence" (
        $missingReasonStatus -eq 400 -and $retired.lifecycleStatus -eq "retired" -and
        $history.eventCount -eq 1 -and $history.events[0].action -eq "retired" -and
        $history.events[0].actor -eq "admin"
    ) @{ missingReasonStatus = $missingReasonStatus; eventCount = $history.eventCount; action = $history.events[0].action }

    $appointmentRequest = @{
        patientId = $patientId
        date = "2027-01-05"
        startTime = "09:00"
        durationMinutes = 30
        title = "Lifecycle verification"
        enforceConflictPolicy = $false
    }
    $retiredSchedulingStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/appointments/" -Method Post `
        -RequestHeaders $headers -Body $appointmentRequest
    Add-Check "Retirement blocks scheduling" ($retiredSchedulingStatus -eq 400) @{ status = $retiredSchedulingStatus }

    $reactivated = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$patientId/lifecycle/reactivate" -Method Post `
        -Headers $headers -ContentType "application/json" `
        -Body (@{ reason = "Focused lifecycle fixture reactivation." } | ConvertTo-Json) -TimeoutSec 20
    $appointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/" -Method Post -Headers $headers `
        -ContentType "application/json" -Body ($appointmentRequest | ConvertTo-Json) -TimeoutSec 20
    $appointmentId = $appointment.id
    Add-Check "Reactivation permits scheduling" (
        $reactivated.lifecycleStatus -eq "active" -and -not [string]::IsNullOrWhiteSpace($appointmentId)
    ) @{ status = $reactivated.lifecycleStatus; appointmentId = $appointmentId }
}
catch {
    Add-Check "Unhandled lifecycle test error" $false $_.Exception.Message
}
finally {
    if ($null -ne $headers -and $appointmentId) {
        $appointmentCleanupStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/appointments/$appointmentId" -Method Delete -RequestHeaders $headers
        Add-Check "Synthetic appointment cleanup" ($appointmentCleanupStatus -eq 204) @{ status = $appointmentCleanupStatus }
    }
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
