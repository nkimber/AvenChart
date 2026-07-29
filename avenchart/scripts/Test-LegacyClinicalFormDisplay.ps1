param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-legacy-clinical-form-display-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"

function Add-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [object]$Details = $null
    )

    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) {
        $script:status = "failed"
    }
}

function Get-HttpStatus {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$RequestHeaders = @{}
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(20)
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($Method),
            $Uri
        )
        foreach ($entry in $RequestHeaders.GetEnumerator()) {
            $request.Headers.TryAddWithoutValidation(
                [string]$entry.Key,
                [string]$entry.Value
            ) | Out-Null
        }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            return [int]$response.StatusCode
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

    $login = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "Administration login did not issue an active session."
    }
    $headers = @{ "X-Legacy EHR-Session" = $login.sessionId }

    $unauthenticated = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/legacy-snapshots"
    Add-Check "Legacy snapshots require authentication" ($unauthenticated -eq 401) @{
        status = $unauthenticated
    }

    $list = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/legacy-snapshots" `
        -Headers $headers `
        -TimeoutSec 20
    $mappedSummary = @($list.snapshots | Where-Object sourceRowId -eq "880001")[0]
    $unmappedSummary = @($list.snapshots | Where-Object sourceRowId -eq "880002")[0]
    Add-Check "Bounded patient snapshot list" (
        $list.total -eq 2 `
        -and $list.returned -eq 2 `
        -and $list.limit -eq 100 `
        -and $mappedSummary.readOnly `
        -and -not $mappedSummary.converted `
        -and $mappedSummary.unmappedCount -eq 0 `
        -and $unmappedSummary.unmappedCount -eq 1
    ) $list

    $emptyList = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0002/legacy-snapshots" `
        -Headers $headers `
        -TimeoutSec 20
    Add-Check "Patient filtering does not leak snapshots" (
        $emptyList.total -eq 0 -and @($emptyList.snapshots).Count -eq 0
    ) $emptyList

    $mapped = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-snapshots/90f00000-0000-4000-9000-000000000001" `
        -Headers $headers `
        -TimeoutSec 20
    $followUp = @($mapped.fields | Where-Object sourceField -eq "followup_required")[0]
    Add-Check "Mapped Clinic Note display evidence" (
        $mapped.snapshot.adapterRevision -eq "local-legacy-clinic-note-display-v1" `
        -and $mapped.snapshot.targetDefinitionRevision -eq 1 `
        -and $mapped.snapshot.targetSchemaHash -match "^[0-9a-f]{64}$" `
        -and $mapped.snapshot.rawSha256 -match "^[0-9a-f]{64}$" `
        -and @($mapped.fields).Count -eq 5 `
        -and $followUp.targetField -eq "follow_up_status" `
        -and $followUp.mappingState -eq "normalized" `
        -and $followUp.displayValue -eq "Required in" `
        -and @($mapped.unmappedFacts).Count -eq 0 `
        -and -not $mapped.migrationApproved `
        -and $null -eq $mapped.governedInstanceId
    ) $mapped

    $unmapped = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-snapshots/90f00000-0000-4000-9000-000000000002" `
        -Headers $headers `
        -TimeoutSec 20
    $unmappedFollowUp = @(
        $unmapped.fields | Where-Object sourceField -eq "followup_required"
    )[0]
    Add-Check "Unmapped and inactive source facts remain explicit" (
        -not $unmapped.snapshot.sourceActive `
        -and $unmapped.snapshot.unmappedCount -eq 1 `
        -and $unmappedFollowUp.mappingState -eq "unmapped" `
        -and $unmappedFollowUp.sourceValue -eq 9 `
        -and @($unmapped.unmappedFacts).Count -eq 1 `
        -and $unmapped.unmappedFacts[0].sourceValue -eq 9
    ) $unmapped

    $missingStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-snapshots/90f00000-0000-4000-9000-ffffffffffff" `
        -RequestHeaders $headers
    Add-Check "Unknown snapshot is not found" ($missingStatus -eq 404) @{
        status = $missingStatus
    }

    $writeStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-snapshots/90f00000-0000-4000-9000-000000000001" `
        -Method Post `
        -RequestHeaders $headers
    Add-Check "No snapshot mutation route exists" ($writeStatus -eq 405) @{
        status = $writeStatus
    }
}
catch {
    $status = "failed"
    Add-Check "Unhandled verification failure" $false @{
        message = $_.Exception.Message
        type = $_.Exception.GetType().FullName
    }
}
finally {
    [ordered]@{
        status = $status
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        apiBaseUrl = $ApiBaseUrl
        checks = $checks
    } | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $resultPath -Encoding UTF8

    Write-Host "Legacy clinical form display verification: $resultPath"
}

if ($status -ne "passed") {
    exit 1
}
