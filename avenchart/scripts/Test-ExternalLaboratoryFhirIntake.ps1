# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001",
    [string]$PatientId = "MOD-PAT-0004"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
Add-Type -AssemblyName System.Net.Http

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-external-laboratory-fhir-intake-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$staffHeaders = $null
$sourceId = $null

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)
    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) { $script:status = "failed" }
}

function Invoke-Json {
    param([string]$Uri, [string]$Method = "Get", [object]$Body = $null, [hashtable]$Headers = $staffHeaders)
    $parameters = @{ Uri = $Uri; Method = $Method; Headers = $Headers; TimeoutSec = 30 }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 20
    }
    Invoke-RestMethod @parameters
}

function Invoke-HttpStatus {
    param([string]$Uri, [string]$Method, [object]$Body = $null, [hashtable]$Headers = @{}, [string]$ContentType = "application/json")
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(30)
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Uri)
        foreach ($entry in $Headers.GetEnumerator()) {
            $request.Headers.TryAddWithoutValidation([string]$entry.Key, [string]$entry.Value) | Out-Null
        }
        if ($null -ne $Body) {
            $payload = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 }
            $request.Content = [System.Net.Http.StringContent]::new($payload, [Text.Encoding]::UTF8, $ContentType)
        }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            return [ordered]@{ status = [int]$response.StatusCode; body = $responseBody }
        }
        finally { $response.Dispose() }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function Get-OrderSpecimen([object]$Detail, [int]$OrderId) {
    $order = @($Detail.orders | Where-Object { $_.id -eq $OrderId } | Select-Object -First 1)
    if ($order.Count -ne 1) { throw "Order $OrderId was not returned by the procedure aggregate." }
    $specimen = @($order[0].specimens | Select-Object -First 1)
    if ($specimen.Count -ne 1) { throw "The synthetic procedure order does not have a specimen." }
    return $specimen[0]
}

function New-FhirBundle([string]$ReportId, [string]$ObservationId, [string]$MessageValue, [string]$ObservationStatus = "final") {
    $now = (Get-Date).ToUniversalTime().ToString("O")
    return [ordered]@{
        resourceType = "Bundle"
        type = "collection"
        entry = @(
            @{ resource = @{
                resourceType = "DiagnosticReport"
                id = $ReportId
                status = $ObservationStatus
                subject = @{ reference = "Patient/$PatientId" }
                basedOn = @(@{ reference = "ServiceRequest/$script:orderId" })
                specimen = @(@{ reference = "Specimen/$script:specimenId" })
                effectiveDateTime = $now
                issued = $now
                result = @(@{ reference = "Observation/$ObservationId" })
            } }
            @{ resource = @{
                resourceType = "Observation"
                id = $ObservationId
                status = $ObservationStatus
                subject = @{ reference = "Patient/$PatientId" }
                code = @{ coding = @(@{ system = "http://loinc.org"; code = "718-7"; display = "Hemoglobin" }) }
                effectiveDateTime = $now
                valueQuantity = @{ value = $MessageValue; unit = "g/dL" }
                referenceRange = @(@{ text = "12-16" })
                interpretation = @(@{ coding = @(@{ system = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation"; code = "HH" }) })
            } }
        )
    }
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -TimeoutSec 15
    Add-Check "API health" ($health.status -eq "healthy") $health

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" `
        -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) -TimeoutSec 20
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "The synthetic administrator session was not issued."
    }
    $staffHeaders = New-AvenChartStaffAccessContextHeaders -Login $login
    $facilityGrantId = [int]$staffHeaders["X-AvenChart-Facility-Id"]
    $marker = [Guid]::NewGuid().ToString("N").Substring(0, 12)
    $sourceId = "synthetic-fhir-lab-$marker"
    $apiKey = "synthetic-fhir-laboratory-key-" + [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
    $null = Invoke-Json "$ApiBaseUrl/api/integrations/laboratory-sources" "Post" @{
        sourceId = $sourceId; displayName = "Synthetic FHIR Laboratory $marker"; apiKey = $apiKey; facilityIds = @($facilityGrantId)
    }

    $options = Invoke-Json "$ApiBaseUrl/api/appointments/scheduling-options"
    $provider = @($options.providers | Select-Object -First 1)
    $facility = @($options.facilities | Where-Object { $_.id -eq $facilityGrantId } | Select-Object -First 1)
    $catalog = Invoke-Json "$ApiBaseUrl/api/procedures/order-catalog"
    $catalogItem = @($catalog.items | Where-Object { $_.active -and $_.itemType -eq "ord" -and $_.code } | Select-Object -First 1)
    if ($provider.Count -ne 1 -or $facility.Count -ne 1 -or $catalogItem.Count -ne 1) {
        throw "The disposable synthetic fixture requires an active provider, facility, and laboratory catalog item."
    }
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $encounter = Invoke-Json "$ApiBaseUrl/api/encounters" "Post" @{
        patientId = $PatientId; providerId = $provider[0].id; dateTime = "$today 10:00:00"; reason = "External laboratory FHIR verification $marker"
        facilityId = $facility[0].id; billingFacilityId = $facility[0].id; sensitivity = "standard"; posCode = 11
    }
    $order = Invoke-Json "$ApiBaseUrl/api/procedures/orders" "Post" @{
        patientId = $PatientId; providerId = $provider[0].id; labId = $catalogItem[0].labId; encounterId = $encounter.encounter
        dateOrdered = $today; priority = "routine"; status = "pending"; procedureCode = $catalogItem[0].code; procedureName = $catalogItem[0].name
        procedureType = if ($catalogItem[0].procedureTypeName) { $catalogItem[0].procedureTypeName } else { "laboratory" }
        diagnosis = "Z00.00"; instructions = "Synthetic FHIR external laboratory intake verification $marker"
    }
    $script:orderId = [int]$order.id
    $specimen = Invoke-Json "$ApiBaseUrl/api/procedures/specimens" "Post" @{
        orderId = $script:orderId; specimenIdentifier = "SYN-$marker"; accessionIdentifier = "ACC-$marker"; specimenTypeCode = "119364003"
        specimenType = "Serum specimen"; collectionMethodCode = "129316008"; collectionMethod = "Venipuncture"; specimenLocationCode = ""; specimenLocation = ""
        collectedDate = "$today 09:45:00"; volumeValue = 1.0; volumeUnit = "mL"; conditionCode = ""; specimenCondition = ""; comments = "Synthetic FHIR intake fixture."
    }
    $detail = Invoke-Json "$ApiBaseUrl/api/procedures/$PatientId"
    $currentSpecimen = Get-OrderSpecimen $detail $script:orderId
    $detail = Invoke-Json "$ApiBaseUrl/api/procedures/specimens/$($currentSpecimen.id)/lifecycle" "Put" @{ status = "labeled"; expectedVersion = $currentSpecimen.lifecycleVersion; reason = "Synthetic FHIR laboratory specimen labeling." }
    $currentSpecimen = Get-OrderSpecimen $detail.detail $script:orderId
    $detail = Invoke-Json "$ApiBaseUrl/api/procedures/specimens/$($currentSpecimen.id)/lifecycle" "Put" @{ status = "received"; expectedVersion = $currentSpecimen.lifecycleVersion; reason = "Synthetic FHIR laboratory specimen receipt." }
    $currentSpecimen = Get-OrderSpecimen $detail.detail $script:orderId
    $script:specimenId = [int]$currentSpecimen.id
    Add-Check "Synthetic patient/order/received specimen fixture" ($script:orderId -gt 0 -and $script:specimenId -gt 0 -and $currentSpecimen.lifecycleStatus -eq "received") @{ orderId = $script:orderId; specimenId = $script:specimenId; status = $currentSpecimen.lifecycleStatus }

    $externalHeaders = @{
        "X-AvenChart-Lab-Source" = $sourceId
        "X-AvenChart-Lab-Api-Key" = $apiKey
        "X-AvenChart-Lab-Message-Id" = "synthetic-initial-$marker"
    }
    $reportId = "synthetic-report-$marker"
    $observationId = "synthetic-observation-$marker"
    $initialBundle = New-FhirBundle $reportId $observationId "16.4"
    $initial = Invoke-HttpStatus "$ApiBaseUrl/api/external-laboratory-results/fhir-r4" "Post" $initialBundle $externalHeaders "application/fhir+json"
    $initialReceipt = $initial.body | ConvertFrom-Json
    Add-Check "Authenticated FHIR laboratory intake applies a new result" ($initial.status -eq 201 -and -not $initialReceipt.duplicate -and $initialReceipt.createdResultCount -eq 1 -and $initialReceipt.updatedResultCount -eq 0) @{ status = $initial.status; receipt = $initialReceipt }

    $detail = Invoke-Json "$ApiBaseUrl/api/procedures/$PatientId"
    $report = @($detail.orders | Where-Object { $_.id -eq $script:orderId } | ForEach-Object { $_.reports } | Where-Object { $_.id -eq $initialReceipt.reportId } | Select-Object -First 1)
    $result = if ($report.Count -eq 1) { @($report[0].results | Where-Object { $_.code -eq "718-7" } | Select-Object -First 1) } else { @() }
    Add-Check "Inbound report remains pending clinician review" ($report.Count -eq 1 -and $report[0].reviewStatus -eq "received" -and $result.Count -eq 1 -and $result[0].result -eq "16.4") @{ reportId = $initialReceipt.reportId; reviewStatus = if ($report.Count) { $report[0].reviewStatus } else { $null }; result = if ($result.Count) { $result[0].result } else { $null } }

    $replay = Invoke-HttpStatus "$ApiBaseUrl/api/external-laboratory-results/fhir-r4" "Post" $initialBundle $externalHeaders "application/fhir+json"
    $replayReceipt = $replay.body | ConvertFrom-Json
    Add-Check "Exact source replay is idempotent" ($replay.status -eq 200 -and $replayReceipt.duplicate -eq $true -and $replayReceipt.createdResultCount -eq 1) @{ status = $replay.status; receipt = $replayReceipt }

    $conflictBundle = New-FhirBundle $reportId $observationId "99.9"
    $conflict = Invoke-HttpStatus "$ApiBaseUrl/api/external-laboratory-results/fhir-r4" "Post" $conflictBundle $externalHeaders "application/fhir+json"
    $detailAfterConflict = Invoke-Json "$ApiBaseUrl/api/procedures/$PatientId"
    $resultAfterConflict = @($detailAfterConflict.orders | Where-Object { $_.id -eq $script:orderId } | ForEach-Object { $_.reports } | Where-Object { $_.id -eq $initialReceipt.reportId } | ForEach-Object { $_.results } | Where-Object { $_.code -eq "718-7" } | Select-Object -First 1)
    Add-Check "Divergent replay is rejected without overwriting the record" ($conflict.status -eq 409 -and $resultAfterConflict.Count -eq 1 -and $resultAfterConflict[0].result -eq "16.4") @{ status = $conflict.status; currentResult = if ($resultAfterConflict.Count) { $resultAfterConflict[0].result } else { $null } }

    $externalHeaders["X-AvenChart-Lab-Message-Id"] = "synthetic-correction-$marker"
    $correctionBundle = New-FhirBundle $reportId $observationId "17.1" "corrected"
    $correction = Invoke-HttpStatus "$ApiBaseUrl/api/external-laboratory-results/fhir-r4" "Post" $correctionBundle $externalHeaders "application/fhir+json"
    $correctionReceipt = $correction.body | ConvertFrom-Json
    $detailAfterCorrection = Invoke-Json "$ApiBaseUrl/api/procedures/$PatientId"
    $resultAfterCorrection = @($detailAfterCorrection.orders | Where-Object { $_.id -eq $script:orderId } | ForEach-Object { $_.reports } | Where-Object { $_.id -eq $initialReceipt.reportId } | ForEach-Object { $_.results } | Where-Object { $_.code -eq "718-7" } | Select-Object -First 1)
    Add-Check "Correction retains prior result history and reopens review" ($correction.status -eq 201 -and $correctionReceipt.updatedResultCount -eq 1 -and $resultAfterCorrection.Count -eq 1 -and $resultAfterCorrection[0].result -eq "17.1" -and $resultAfterCorrection[0].hasPriorVersions -eq $true) @{ status = $correction.status; updated = $correctionReceipt.updatedResultCount; result = if ($resultAfterCorrection.Count) { $resultAfterCorrection[0].result } else { $null }; versions = if ($resultAfterCorrection.Count) { $resultAfterCorrection[0].versionHistoryCount } else { $null } }

    $invalidKeyHeaders = @{}
    foreach ($entry in $externalHeaders.GetEnumerator()) { $invalidKeyHeaders[$entry.Key] = $entry.Value }
    $invalidKeyHeaders["X-AvenChart-Lab-Api-Key"] = "invalid-external-laboratory-key-" + [Guid]::NewGuid().ToString("N")
    $invalidKey = Invoke-HttpStatus "$ApiBaseUrl/api/external-laboratory-results/fhir-r4" "Post" $correctionBundle $invalidKeyHeaders "application/fhir+json"
    Add-Check "Invalid source credential fails closed" ($invalidKey.status -eq 401) @{ status = $invalidKey.status }
}
catch {
    Add-Check "Unhandled external laboratory FHIR intake test error" $false $_.Exception.Message
}
finally {
    if ($null -ne $staffHeaders -and $null -ne $sourceId) {
        try {
            $deactivation = Invoke-HttpStatus "$ApiBaseUrl/api/integrations/laboratory-sources/$sourceId/deactivate" "Post" @{ reason = "Synthetic FHIR intake verification completed." } $staffHeaders
            Add-Check "Synthetic laboratory source is deactivated after verification" ($deactivation.status -eq 200) @{ status = $deactivation.status; sourceId = $sourceId }
        }
        catch {
            Add-Check "Synthetic laboratory source deactivation" $false $_.Exception.Message
        }
    }
    $result = [ordered]@{ status = $status; generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O"); checks = $checks }
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 20
}

if ($status -ne "passed") { exit 1 }
