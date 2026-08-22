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
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login

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

    $activePrescription = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions" -Method Post `
        -Headers $headers -ContentType "application/json" -Body (@{
            patientId = $patientId
            startDate = "2027-01-05"
            drug = "Lifecycle continuation verification prescription"
            dosage = "One tablet daily"
            quantity = "1"
            refills = 0
            note = "Created before retirement to verify continuation controls."
            diagnosis = "Z00.00"
        } | ConvertTo-Json) -TimeoutSec 20
    $activePrescriptionId = $activePrescription.id
    $activePrescriptionItem = $activePrescription.detail.prescriptions |
        Where-Object { $_.id -eq $activePrescriptionId } | Select-Object -First 1
    if ($null -eq $activePrescriptionItem) {
        throw "The active lifecycle verification prescription was not returned after creation."
    }
    $pharmacyDirectory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/pharmacies" `
        -Headers $headers -TimeoutSec 20
    $routePharmacy = @($pharmacyDirectory.pharmacies) | Select-Object -First 1
    if ($null -eq $routePharmacy) {
        throw "The lifecycle verification fixture requires an available pharmacy."
    }

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

    $retiredAllergyStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/clinical-lists/allergies" -Method Post `
        -RequestHeaders $headers -Body @{
            patientId = $patientId
            title = "Lifecycle test allergy"
            dateTime = "2027-01-05"
            comments = "Must not write after retirement."
            reaction = "Test only"
            severity = "low"
        }
    $retiredMedicationStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/clinical-lists/medications" -Method Post `
        -RequestHeaders $headers -Body @{
            patientId = $patientId
            title = "Lifecycle test medication"
            dateTime = "2027-01-05"
            comments = "Must not write after retirement."
        }
    $retiredPrescriptionStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions" -Method Post `
        -RequestHeaders $headers -Body @{
            patientId = $patientId
            startDate = "2027-01-05"
            drug = "Lifecycle test prescription"
            dosage = "One tablet daily"
            quantity = "1"
            refills = 0
            note = "Must not write after retirement."
            diagnosis = "Z00.00"
        }
    Add-Check "Retirement blocks new clinical-list content" (
        $retiredAllergyStatus -eq 400 -and
        $retiredMedicationStatus -eq 400 -and
        $retiredPrescriptionStatus -eq 400
    ) @{
        allergyStatus = $retiredAllergyStatus
        medicationStatus = $retiredMedicationStatus
        prescriptionStatus = $retiredPrescriptionStatus
    }

    $retiredPrescriptionRefillStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$activePrescriptionId/refill" `
        -Method Put -RequestHeaders $headers -Body @{
            refillDate = "2027-01-06"
            additionalRefills = 1
            note = "Must not continue after retirement."
        }
    $retiredPrescriptionUpdateStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$activePrescriptionId" `
        -Method Put -RequestHeaders $headers -Body @{
            expectedVersion = $activePrescriptionItem.version
            startDate = "2027-01-05"
            dosage = "Two tablets daily"
            quantity = "1"
            doseAmount = $null
            doseUnit = $null
            frequency = $null
            durationDays = $null
            route = "oral"
            refills = 0
            diagnosis = "Z00.00"
            note = "Must not continue after retirement."
            editReason = "Verification that retirement blocks prescription continuation."
        }
    $retiredPrescriptionRouteStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$activePrescriptionId/route-pharmacy" `
        -Method Put -RequestHeaders $headers -Body @{
            pharmacyId = $routePharmacy.id
            sentAt = "2027-01-06T10:00:00"
            note = "Must not route after retirement."
        }
    Add-Check "Retirement blocks prescription continuation" (
        $retiredPrescriptionRefillStatus -eq 409 -and
        $retiredPrescriptionUpdateStatus -eq 409 -and
        $retiredPrescriptionRouteStatus -eq 409
    ) @{
        refillStatus = $retiredPrescriptionRefillStatus
        updateStatus = $retiredPrescriptionUpdateStatus
        routeStatus = $retiredPrescriptionRouteStatus
    }

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
