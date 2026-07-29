param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-clinical-form-engine-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$headers = $null
$patientId = $null
$encounterId = $null

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)
    $script:checks.Add([ordered]@{ name = $Name; status = if ($Passed) { "passed" } else { "failed" }; details = $Details })
    if (-not $Passed) { $script:status = "failed" }
}

function Get-HttpStatus {
    param([string]$Uri, [string]$Method, [hashtable]$RequestHeaders = @{}, [object]$Body = $null)
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(20)
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Uri)
        foreach ($entry in $RequestHeaders.GetEnumerator()) {
            $request.Headers.TryAddWithoutValidation([string]$entry.Key, [string]$entry.Value) | Out-Null
        }
        if ($null -ne $Body) {
            $request.Content = [System.Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 20), [Text.Encoding]::UTF8, "application/json")
        }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try { return [int]$response.StatusCode } finally { $response.Dispose() }
    }
    finally { $client.Dispose(); $handler.Dispose() }
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -TimeoutSec 15
    Add-Check "API health" ($health.status -eq "healthy") $health
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) -TimeoutSec 20
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) { throw "Administration login did not issue an active session." }
    $headers = @{ "X-Legacy EHR-Session" = $login.sessionId }

    $catalog = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/catalog" -Headers $headers -TimeoutSec 20
    $definition = @($catalog.definitions | Where-Object { $_.stableKey -eq "clinical.observation" }) | Select-Object -First 1
    Add-Check "Effective bounded form catalog" ($null -ne $definition -and $definition.contextScope -eq "encounter") @{ total = $catalog.total; stableKey = $definition.stableKey; contextScope = $definition.contextScope }
    if ($null -eq $definition) { throw "Seeded clinical observation form was not present in the effective catalog." }

    $marker = [Guid]::NewGuid().ToString("N").Substring(0, 10)
    $patient = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ pubpid = "TMP-PAT-REG-CF-$marker"; firstName = "Clinical"; lastName = "Form$marker"; sex = "Unknown"; dateOfBirth = "1990-01-01"; hipaaAllowSms = "NO"; hipaaAllowEmail = "NO" } | ConvertTo-Json) -TimeoutSec 20
    $patientId = $patient.canonicalId
    $encounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ patientId = $patientId; providerId = $null; dateTime = "2026-07-28T10:00:00"; reason = "Clinical form synthetic verification"; facilityId = $null; billingFacilityId = $null; sensitivity = $null; referralSource = $null; externalId = $null; posCode = $null; billingNote = $null; sourceAppointmentId = $null } | ConvertTo-Json) -TimeoutSec 20
    $encounterId = $encounter.encounter

    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/patients/$patientId/instances" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ definitionId = $definition.definitionId; encounterId = $encounterId; idempotencyKey = "clinical-form-test-$marker"; values = @{}; reason = "Create a synthetic typed clinical form draft." } | ConvertTo-Json -Depth 20) -TimeoutSec 20
    Add-Check "Draft preserves incomplete validation" ($created.instance.state -eq "draft" -and -not $created.validation.valid) @{ state = $created.instance.state; valid = $created.validation.valid; issueCount = @($created.validation.issues).Count }

    $saved = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/instances/$($created.instance.instanceId)" -Method Put -Headers $headers -ContentType "application/json" -Body (@{ expectedVersion = $created.instance.version; values = @{ chief_concern = "Focused synthetic clinical form verification"; pain_score = 8; follow_up = $true; disposition = "routine"; notes = "Verify typed validation and immutable lifecycle evidence." }; reason = "Enter valid bounded synthetic observations." } | ConvertTo-Json -Depth 20) -TimeoutSec 20
    Add-Check "Typed validation and declarative rules" ($saved.validation.valid -and @($saved.validation.issues | Where-Object { $_.severity -eq "warning" }).Count -ge 1) @{ valid = $saved.validation.valid; issues = $saved.validation.issues }

    $finalized = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/instances/$($saved.instance.instanceId)/finalize" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ expectedVersion = $saved.instance.version; reason = "Finalize the verified synthetic clinical form." } | ConvertTo-Json) -TimeoutSec 20
    $signed = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/instances/$($finalized.instance.instanceId)/sign" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ expectedVersion = $finalized.instance.version; reason = "Sign the verified synthetic clinical form." } | ConvertTo-Json) -TimeoutSec 20
    Add-Check "Finalize and authenticated signature" ($finalized.instance.state -eq "ready-for-signature" -and $signed.instance.state -eq "signed" -and @($signed.signatures).Count -eq 1 -and $signed.signatures[0].signer -eq "admin") @{ finalState = $finalized.instance.state; signedState = $signed.instance.state; signatures = $signed.signatures }

    $amended = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/instances/$($signed.instance.instanceId)/amend" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ expectedVersion = $signed.instance.version; reason = "Correct the synthetic form through a successor amendment."; idempotencyKey = "clinical-form-amend-$marker" } | ConvertTo-Json) -TimeoutSec 20
    $amendmentFinalized = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/instances/$($amended.instance.instanceId)/finalize" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ expectedVersion = $amended.instance.version; reason = "Finalize the synthetic successor amendment." } | ConvertTo-Json) -TimeoutSec 20
    $amendmentSigned = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/instances/$($amendmentFinalized.instance.instanceId)/sign" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ expectedVersion = $amendmentFinalized.instance.version; reason = "Sign the synthetic successor amendment." } | ConvertTo-Json) -TimeoutSec 20
    $original = Invoke-RestMethod -Uri "$ApiBaseUrl/api/form-engine/instances/$($signed.instance.instanceId)" -Headers $headers -TimeoutSec 20
    Add-Check "Amendment creates immutable successor evidence" ($amended.instance.state -eq "draft" -and $amended.instance.predecessorInstanceId -eq $signed.instance.instanceId -and $amendmentSigned.instance.state -eq "signed" -and $original.instance.state -eq "amended" -and @($original.events).Count -ge 4) @{ successor = $amended.instance.instanceId; predecessor = $amended.instance.predecessorInstanceId; successorState = $amendmentSigned.instance.state; originalState = $original.instance.state; eventCount = @($original.events).Count }
}
catch { Add-Check "Unhandled clinical-form engine test error" $false $_.Exception.Message }
finally {
    if ($null -ne $headers -and $encounterId) {
        $encounterCleanupStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/encounters/$encounterId" -Method Delete -RequestHeaders $headers
        Add-Check "Synthetic encounter cleanup" ($encounterCleanupStatus -eq 204) @{ status = $encounterCleanupStatus }
    }
    if ($null -ne $headers -and $patientId) {
        $patientCleanupStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/patients/$patientId" -Method Delete -RequestHeaders $headers
        Add-Check "Synthetic patient and clinical-form cleanup" ($patientCleanupStatus -eq 204) @{ status = $patientCleanupStatus }
    }
    $result = [ordered]@{ status = $status; generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O"); checks = $checks }
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 20
}

if ($status -ne "passed") { exit 1 }
