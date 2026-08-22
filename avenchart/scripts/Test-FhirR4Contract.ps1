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
$resultPath = Join-Path $artifactsRoot "latest-fhir-r4-contract-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)

    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) { $script:status = "failed" }
}

function Invoke-FhirRequest {
    param(
        [string]$Uri,
        [hashtable]$Headers,
        [string]$Accept = "application/fhir+json"
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(20)
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $Uri)
        $request.Headers.TryAddWithoutValidation("Accept", $Accept) | Out-Null
        foreach ($entry in $Headers.GetEnumerator()) {
            $request.Headers.TryAddWithoutValidation([string]$entry.Key, [string]$entry.Value) | Out-Null
        }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            return [pscustomobject]@{
                statusCode = [int]$response.StatusCode
                contentType = $response.Content.Headers.ContentType.ToString()
                body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
        }
        finally {
            $response.Dispose()
            $request.Dispose()
        }
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

    $metadataResponse = Invoke-FhirRequest -Uri "$ApiBaseUrl/api/fhir/R4/metadata" -Headers $headers
    $metadata = $metadataResponse.body | ConvertFrom-Json
    $patientCapability = @($metadata.rest[0].resource | Where-Object { $_.type -eq "Patient" }) | Select-Object -First 1
    Add-Check "FHIR capability statement is an R4 JSON instance" (
        $metadataResponse.statusCode -eq 200 -and
        $metadataResponse.contentType -like "application/fhir+json*" -and
        $metadata.resourceType -eq "CapabilityStatement" -and
        $metadata.fhirVersion -eq "4.0.1" -and
        $metadata.kind -eq "instance" -and
        -not [string]::IsNullOrWhiteSpace($metadata.url) -and
        -not [string]::IsNullOrWhiteSpace($metadata.name) -and
        -not [string]::IsNullOrWhiteSpace($metadata.publisher) -and
        $metadata.format -contains "json" -and
        $null -ne $patientCapability -and
        (@($patientCapability.searchParam | Where-Object { $_.name -eq "identifier" -and $_.type -eq "token" }).Count -eq 1)
    ) @{ status = $metadataResponse.statusCode; contentType = $metadataResponse.contentType; metadata = $metadata }

    $firstPageResponse = Invoke-FhirRequest -Uri "$ApiBaseUrl/api/fhir/R4/Patient?_count=1&page=1" -Headers $headers
    $firstPage = $firstPageResponse.body | ConvertFrom-Json
    $selfLink = @($firstPage.link | Where-Object { $_.relation -eq "self" }) | Select-Object -First 1
    $nextLink = @($firstPage.link | Where-Object { $_.relation -eq "next" }) | Select-Object -First 1
    $firstEntry = @($firstPage.entry) | Select-Object -First 1
    Add-Check "FHIR Patient search is pageable with absolute bundle URLs" (
        $firstPageResponse.statusCode -eq 200 -and
        $firstPageResponse.contentType -like "application/fhir+json*" -and
        $firstPage.resourceType -eq "Bundle" -and
        $firstPage.type -eq "searchset" -and
        @($firstPage.entry).Count -le 1 -and
        $null -ne $selfLink -and
        $selfLink.url -match '^https?://' -and
        $null -ne $firstEntry -and
        $firstEntry.fullUrl -match '^https?://' -and
        $firstEntry.fullUrl -match '/Patient/' -and
        (($firstPage.total -le 1) -or $null -ne $nextLink)
    ) @{ status = $firstPageResponse.statusCode; bundle = $firstPage }

    if ($firstPage.total -gt 1) {
        $secondPageResponse = Invoke-FhirRequest -Uri "$ApiBaseUrl/api/fhir/R4/Patient?_count=1&page=2" -Headers $headers
        $secondPage = $secondPageResponse.body | ConvertFrom-Json
        $previousLink = @($secondPage.link | Where-Object { $_.relation -eq "previous" }) | Select-Object -First 1
        Add-Check "FHIR Patient continuation link returns a subsequent page" (
            $secondPageResponse.statusCode -eq 200 -and
            $null -ne $previousLink -and
            @($secondPage.entry).Count -le 1 -and
            @($secondPage.entry).Count -gt 0 -and
            $secondPage.entry[0].resource.id -ne $firstEntry.resource.id
        ) @{ status = $secondPageResponse.statusCode; bundle = $secondPage }
    }

    $unsupportedRepresentation = Invoke-FhirRequest `
        -Uri "$ApiBaseUrl/api/fhir/R4/metadata" -Headers $headers -Accept "application/fhir+xml"
    $unsupportedOutcome = $unsupportedRepresentation.body | ConvertFrom-Json
    Add-Check "FHIR rejects unsupported representations with OperationOutcome" (
        $unsupportedRepresentation.statusCode -eq 406 -and
        $unsupportedRepresentation.contentType -like "application/fhir+json*" -and
        $unsupportedOutcome.resourceType -eq "OperationOutcome" -and
        @($unsupportedOutcome.issue | Where-Object { $_.code -eq "not-supported" }).Count -eq 1
    ) @{ status = $unsupportedRepresentation.statusCode; contentType = $unsupportedRepresentation.contentType; outcome = $unsupportedOutcome }

    $missingPatient = Invoke-FhirRequest -Uri "$ApiBaseUrl/api/fhir/R4/Patient/unknown-fhir-contract-patient" -Headers $headers
    $missingOutcome = $missingPatient.body | ConvertFrom-Json
    Add-Check "FHIR missing resources return OperationOutcome" (
        $missingPatient.statusCode -eq 404 -and
        $missingPatient.contentType -like "application/fhir+json*" -and
        $missingOutcome.resourceType -eq "OperationOutcome" -and
        @($missingOutcome.issue | Where-Object { $_.code -eq "not-found" }).Count -eq 1
    ) @{ status = $missingPatient.statusCode; contentType = $missingPatient.contentType; outcome = $missingOutcome }
}
catch {
    $status = "failed"
    $checks.Add([ordered]@{ name = "FHIR R4 contract execution"; status = "failed"; details = $_.Exception.Message })
}

$result = [ordered]@{
    status = $status
    executedAt = [DateTimeOffset]::UtcNow.ToString("O")
    checks = $checks
}
$result | ConvertTo-Json -Depth 12 | Set-Content -Path $resultPath -Encoding utf8
$result | ConvertTo-Json -Depth 12

if ($status -ne "passed") { exit 1 }
