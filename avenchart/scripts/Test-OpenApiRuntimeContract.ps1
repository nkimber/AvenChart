# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001"
)

$ErrorActionPreference = "Stop"
$target = [Uri]$ApiBaseUrl
if ($target.Host -notin @("localhost", "127.0.0.1", "::1")) {
    throw "This runtime-contract proof is restricted to a local API target."
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-openapi-runtime-contract-test.json"
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

function Get-PropertyValue {
    param([object]$Object, [string]$Name)

    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-PathOperation {
    param([object]$Document, [string]$Path, [string]$Method)

    $pathItem = Get-PropertyValue -Object (Get-PropertyValue -Object $Document -Name "paths") -Name $Path
    return Get-PropertyValue -Object $pathItem -Name $Method
}

function Test-RequiredHeader {
    param([object]$Operation, [string]$Name)

    return @($Operation.parameters | Where-Object {
        $_.name -eq $Name -and $_.in -eq "header" -and $_.required -eq $true
    }).Count -eq 1
}

function Test-SecurityAlternative {
    param([object]$Operation, [string]$Scheme)

    return @($Operation.security | Where-Object {
        @($_.PSObject.Properties.Name) -contains $Scheme
    }).Count -ge 1
}

function Test-SecurityPair {
    param([object]$Operation, [string]$First, [string]$Second)

    return @($Operation.security | Where-Object {
        @($_.PSObject.Properties.Name) -contains $First -and
        @($_.PSObject.Properties.Name) -contains $Second
    }).Count -ge 1
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 20
    Add-Check "API readiness" ($health.status -eq "healthy") $health

    $document = Invoke-RestMethod -Uri "$ApiBaseUrl/openapi/v1.json" -TimeoutSec 20
    $securitySchemes = Get-PropertyValue -Object (Get-PropertyValue -Object $document -Name "components") -Name "securitySchemes"
    $localSession = Get-PropertyValue -Object $securitySchemes -Name "AvenChartLocalStaffSession"
    $oidcBearer = Get-PropertyValue -Object $securitySchemes -Name "AvenChartOidcBearer"
    $laboratorySource = Get-PropertyValue -Object $securitySchemes -Name "AvenChartExternalLaboratorySource"
    $laboratoryKey = Get-PropertyValue -Object $securitySchemes -Name "AvenChartExternalLaboratoryApiKey"
    Add-Check "OpenAPI publishes the staff and external-laboratory authentication schemes" (
        $localSession.type -eq "apiKey" -and $localSession.in -eq "header" -and $localSession.name -eq "X-AvenChart-Session" -and
        $oidcBearer.type -eq "http" -and $oidcBearer.scheme -eq "bearer" -and $oidcBearer.bearerFormat -eq "JWT" -and
        $laboratorySource.type -eq "apiKey" -and $laboratorySource.name -eq "X-AvenChart-Lab-Source" -and
        $laboratoryKey.type -eq "apiKey" -and $laboratoryKey.name -eq "X-AvenChart-Lab-Api-Key"
    ) @{ schemes = @($securitySchemes.PSObject.Properties.Name) }

    $fhirPatient = Get-PathOperation -Document $document -Path "/api/fhir/R4/Patient/{id}" -Method "get"
    $fhirOutcomeSchema = Get-PropertyValue -Object (Get-PropertyValue -Object (Get-PropertyValue -Object $fhirPatient.responses -Name "404") -Name "content") -Name "application/fhir+json"
    Add-Check "FHIR operations describe alternative staff authentication, selected access context, and FHIR response representations" (
        (Test-SecurityAlternative -Operation $fhirPatient -Scheme "AvenChartLocalStaffSession") -and
        (Test-SecurityAlternative -Operation $fhirPatient -Scheme "AvenChartOidcBearer") -and
        (Test-RequiredHeader -Operation $fhirPatient -Name "X-AvenChart-Facility-Id") -and
        (Test-RequiredHeader -Operation $fhirPatient -Name "X-AvenChart-Purpose-Of-Use") -and
        $null -ne (Get-PropertyValue -Object (Get-PropertyValue -Object (Get-PropertyValue -Object $fhirPatient.responses -Name "200") -Name "content") -Name "application/fhir+json") -and
        $null -ne $fhirOutcomeSchema -and
        @($fhirPatient.responses.PSObject.Properties.Name) -contains "401" -and
        @($fhirPatient.responses.PSObject.Properties.Name) -contains "403" -and
        @($fhirPatient.responses.PSObject.Properties.Name) -contains "404" -and
        @($fhirPatient.responses.PSObject.Properties.Name) -contains "406"
    ) @{ responses = @($fhirPatient.responses.PSObject.Properties.Name); parameters = @($fhirPatient.parameters) }

    $externalLaboratory = Get-PathOperation -Document $document -Path "/api/external-laboratory-results/fhir-r4" -Method "post"
    $externalRequestContent = $externalLaboratory.requestBody.content
    Add-Check "External laboratory intake publishes source credentials, message idempotency, FHIR request types, and all runtime outcomes" (
        (Test-SecurityPair -Operation $externalLaboratory -First "AvenChartExternalLaboratorySource" -Second "AvenChartExternalLaboratoryApiKey") -and
        (Test-RequiredHeader -Operation $externalLaboratory -Name "X-AvenChart-Lab-Message-Id") -and
        $externalLaboratory.requestBody.required -eq $true -and
        $null -ne (Get-PropertyValue -Object $externalRequestContent -Name "application/fhir+json") -and
        $null -ne (Get-PropertyValue -Object $externalRequestContent -Name "application/json") -and
        @($externalLaboratory.responses.PSObject.Properties.Name | Sort-Object) -contains "200" -and
        @($externalLaboratory.responses.PSObject.Properties.Name | Sort-Object) -contains "201" -and
        @($externalLaboratory.responses.PSObject.Properties.Name | Sort-Object) -contains "400" -and
        @($externalLaboratory.responses.PSObject.Properties.Name | Sort-Object) -contains "401" -and
        @($externalLaboratory.responses.PSObject.Properties.Name | Sort-Object) -contains "409" -and
        @($externalLaboratory.responses.PSObject.Properties.Name | Sort-Object) -contains "415" -and
        @($externalLaboratory.responses.PSObject.Properties.Name | Sort-Object) -contains "422"
    ) @{ responses = @($externalLaboratory.responses.PSObject.Properties.Name); parameters = @($externalLaboratory.parameters) }

    $outbox = Get-PathOperation -Document $document -Path "/api/integrations/outbox" -Method "post"
    $outboxSchema = Get-PropertyValue -Object (Get-PropertyValue -Object $outbox.requestBody.content -Name "application/json") -Name "schema"
    $outboxCreated = Get-PropertyValue -Object (Get-PropertyValue -Object $outbox.responses -Name "201") -Name "content"
    Add-Check "Integration outbox queue contract documents content-bound idempotency and the actual created response" (
        (Test-SecurityAlternative -Operation $outbox -Scheme "AvenChartLocalStaffSession") -and
        (Test-SecurityAlternative -Operation $outbox -Scheme "AvenChartOidcBearer") -and
        (Test-RequiredHeader -Operation $outbox -Name "X-AvenChart-Facility-Id") -and
        (Test-RequiredHeader -Operation $outbox -Name "X-AvenChart-Purpose-Of-Use") -and
        $outbox.requestBody.required -eq $true -and
        @($outboxSchema.required) -contains "idempotencyKey" -and
        $null -ne (Get-PropertyValue -Object $outboxCreated -Name "application/json") -and
        $null -ne (Get-PropertyValue -Object $outbox.responses -Name "400") -and
        $null -ne (Get-PropertyValue -Object $outbox.responses -Name "409") -and
        $null -eq (Get-PropertyValue -Object $outbox.responses -Name "200")
    ) @{ responses = @($outbox.responses.PSObject.Properties.Name); requestRequired = @($outboxSchema.required) }
}
catch {
    Add-Check "OpenAPI runtime-contract proof execution" $false $_.Exception.Message
}
finally {
    $result = [ordered]@{
        status = $status
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
        checks = $checks
    }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 12
}

if ($status -ne "passed") { exit 1 }
