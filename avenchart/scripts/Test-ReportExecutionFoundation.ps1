param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-report-execution-foundation-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$headers = $null
$providerHeaders = $null
$frontdeskHeaders = $null
$careTeamFixtureActive = $false
$definitionIds = [System.Collections.Generic.List[string]]::new()
$marker = "tmp-report-execution-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)
    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) {
        $script:status = "failed"
    }
}

function Invoke-Api {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$RequestHeaders = @{},
        [object]$Body = $null
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(30)
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
        if ($null -ne $Body) {
            $request.Content = [System.Net.Http.StringContent]::new(
                ($Body | ConvertTo-Json -Depth 30 -Compress),
                [Text.Encoding]::UTF8,
                "application/json"
            )
        }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            $content = [Text.Encoding]::UTF8.GetString($bytes)
            $json = $null
            if (-not [string]::IsNullOrWhiteSpace($content)) {
                try {
                    $json = $content | ConvertFrom-Json
                }
                catch {
                    $json = $null
                }
            }
            return [pscustomobject]@{
                Status = [int]$response.StatusCode
                Content = $content
                Bytes = $bytes
                Json = $json
                ContentType = $response.Content.Headers.ContentType.MediaType
                FileName = $response.Content.Headers.ContentDisposition.FileNameStar
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

function Invoke-Json {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        [hashtable]$RequestHeaders = @{},
        [object]$Body = $null
    )
    $response = Invoke-Api `
        -Uri $Uri `
        -Method $Method `
        -RequestHeaders $RequestHeaders `
        -Body $Body
    if ($response.Status -lt 200 -or $response.Status -ge 300) {
        throw "$Method $Uri returned $($response.Status): $($response.Content)"
    }
    return $response.Json
}

function Get-BytesSha256 {
    param([byte[]]$Bytes)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return (
            [BitConverter]::ToString(
                $sha256.ComputeHash($Bytes)
            ).Replace("-", "").ToLowerInvariant()
        )
    }
    finally {
        $sha256.Dispose()
    }
}

function Invoke-Transition {
    param(
        [string]$DefinitionId,
        [string]$Action,
        [int]$ExpectedVersion,
        [string]$Reason
    )
    return Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$DefinitionId/$Action" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body @{
            expectedVersion = $ExpectedVersion
            reason = $Reason
        }
}

function New-ActiveDefinition {
    param(
        [string]$StableKey,
        [string]$Title,
        [string]$Purpose,
        [string]$Family,
        [string]$RowPolicy
    )

    $created = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body @{
            stableKey = $StableKey
            title = $Title
            ownerUsername = "admin"
            purpose = $Purpose
            reportFamily = $Family
            sensitivity = "restricted"
            rowPolicy = $RowPolicy
            retentionDays = 30
            allowedRecipients = @("requesting-user", "report-owner")
            deliveryModes = @("local-download")
            reason = "Create a synthetic governed execution fixture."
        }
    $script:definitionIds.Add([string]$created.definitionId)
    $reviewed = Invoke-Transition `
        -DefinitionId $created.definitionId `
        -Action "review" `
        -ExpectedVersion 0 `
        -Reason "Owner reviewed the synthetic execution contract."
    $approved = Invoke-Transition `
        -DefinitionId $created.definitionId `
        -Action "approve" `
        -ExpectedVersion $reviewed.revisions[0].version `
        -Reason "Approve the bounded local execution fixture."
    return Invoke-Transition `
        -DefinitionId $created.definitionId `
        -Action "activate" `
        -ExpectedVersion $approved.revisions[0].version `
        -Reason "Activate the bounded local execution fixture."
}

try {
    $health = Invoke-Json -Uri "$ApiBaseUrl/health"
    Add-Check "API health" ($health.status -eq "healthy") $health

    $login = Invoke-Json `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method "POST" `
        -Body @{ username = "admin"; password = "pass" }
    $headers = @{ "X-Legacy EHR-Session" = $login.sessionId }
    $providerLogin = Invoke-Json `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method "POST" `
        -Body @{ username = "gold-provider-01"; password = "pass" }
    $providerHeaders = @{
        "X-Legacy EHR-Session" = $providerLogin.sessionId
    }
    $frontdeskLogin = Invoke-Json `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method "POST" `
        -Body @{ username = "gold-frontdesk-01"; password = "pass" }
    $frontdeskHeaders = @{
        "X-Legacy EHR-Session" = $frontdeskLogin.sessionId
    }

    $unauthenticated = Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/execution-policy"
    $policy = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/execution-policy" `
        -RequestHeaders $headers
    $providerPolicy = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/execution-policy" `
        -RequestHeaders $providerHeaders
    Add-Check "Protected actor-aware execution policy" (
        $unauthenticated.Status -eq 401 `
            -and $policy.revision -eq "local-report-execution-v2" `
            -and $policy.scopeRevision -eq "local-report-scope-v1" `
            -and $policy.executableRowPolicies.Count -eq 3 `
            -and -not $policy.currentActorScope.activeStaffLinked `
            -and $providerPolicy.currentActorScope.activeStaffLinked `
            -and $providerPolicy.currentActorScope.staffId -eq 101 `
            -and $providerPolicy.currentActorScope.facilityId -eq 10 `
            -and $providerPolicy.currentActorScope.facilityCode -eq "MAIN" `
            -and $providerPolicy.currentActorScope.assignedPatientCount -eq 83 `
            -and $providerPolicy.rowPolicyFamilySupport."facility-scoped".Count -eq 7 `
            -and $providerPolicy.rowPolicyFamilySupport."patient-assigned".Count -eq 6 `
            -and $policy.deliveryModes.Count -eq 1 `
            -and -not $policy.externalDeliveryEnabled `
            -and -not $policy.artifactStorageProductionApproved `
            -and $policy.productionBlockers.Count -eq 7
    ) @{
        unauthenticatedStatus = $unauthenticated.Status
        revision = $policy.revision
        dataset = "$($policy.datasetId)@$($policy.datasetVersion)"
        requiredAsOfDate = $policy.requiredAsOfDate
        executableRowPolicies = $policy.executableRowPolicies
        adminScope = $policy.currentActorScope
        providerScope = $providerPolicy.currentActorScope
    }

    $purpose = "Verify revision-pinned patient report execution."
    $active = New-ActiveDefinition `
        -StableKey "$marker-practice" `
        -Title "Synthetic patient execution" `
        -Purpose $purpose `
        -Family "patients" `
        -RowPolicy "practice-wide"
    $definitionId = [string]$active.definitionId
    $baseRequest = @{
        purpose = $purpose
        recipientUsername = "admin"
        deliveryMode = "local-download"
        asOfDate = $policy.requiredAsOfDate
        parameters = @{}
    }
    $frontdeskPreview = Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/preview" `
        -Method "POST" `
        -RequestHeaders $frontdeskHeaders `
        -Body $baseRequest
    Add-Check "Report-view capability protects execution and artifact paths" (
        $frontdeskPreview.Status -eq 403 `
            -and $frontdeskPreview.Json.requiredSection -eq "patients" `
            -and $frontdeskPreview.Json.requiredPermission -eq "pat_rep" `
            -and $frontdeskPreview.Json.requiredReturnValue -eq "view"
    ) @{
        status = $frontdeskPreview.Status
        requiredSection = $frontdeskPreview.Json.requiredSection
        requiredPermission = $frontdeskPreview.Json.requiredPermission
        requiredReturnValue = $frontdeskPreview.Json.requiredReturnValue
    }

    $historyBefore = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/runs?page=1&pageSize=10" `
        -RequestHeaders $headers
    $preview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/preview" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $baseRequest
    Add-Check "Policy-equivalent preview does not persist" (
        $historyBefore.total -eq 0 `
            -and $preview.revisionNumber -eq 1 `
            -and $preview.rowPolicy -eq "practice-wide" `
            -and $preview.totalRows -eq 1000 `
            -and $preview.scopeRevision -eq "local-report-scope-v1" `
            -and $preview.scopeSubjectCount -eq 1000 `
            -and $preview.scopeSnapshotChecksum -match "^[a-f0-9]{64}$" `
            -and $preview.rows.Count -eq 10 `
            -and $preview.columns -join "," -eq "Identifier,Subject,Date,Detail" `
            -and $preview.resultChecksum -match "^[a-f0-9]{64}$"
    ) @{
        historyCount = $historyBefore.total
        totalRows = $preview.totalRows
        previewRows = $preview.rows.Count
        checksum = $preview.resultChecksum
    }

    $wrongAsOf = $baseRequest.Clone()
    $wrongAsOf.asOfDate = "2026-01-01"
    $wrongPurpose = $baseRequest.Clone()
    $wrongPurpose.purpose = "A different unapproved purpose."
    $unknownParameter = $baseRequest.Clone()
    $unknownParameter.parameters = @{ rawSql = "not accepted" }
    $wrongAsOfStatus = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/preview" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $wrongAsOf).Status
    $wrongPurposeStatus = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/preview" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $wrongPurpose).Status
    $unknownParameterStatus = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/preview" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $unknownParameter).Status
    Add-Check "As-of, purpose, and parameter policy fail closed" (
        $wrongAsOfStatus -eq 400 `
            -and $wrongPurposeStatus -eq 400 `
            -and $unknownParameterStatus -eq 400
    ) @{
        wrongAsOfStatus = $wrongAsOfStatus
        wrongPurposeStatus = $wrongPurposeStatus
        unknownParameterStatus = $unknownParameterStatus
    }

    $runRequest = $baseRequest.Clone()
    $runRequest.idempotencyKey = "report-run-$([Guid]::NewGuid().ToString('N'))"
    $firstResponse = Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/run" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $runRequest
    $first = $firstResponse.Json
    Add-Check "Completed run retains reproducibility evidence" (
        $firstResponse.Status -eq 201 `
            -and $first.run.status -eq "completed" `
            -and $first.run.revisionNumber -eq 1 `
            -and $first.run.rowCount -eq 1000 `
            -and $first.run.resultChecksum -eq $preview.resultChecksum `
            -and $first.run.definitionSnapshotChecksum -match "^[a-f0-9]{64}$" `
            -and $first.run.datasetId -eq $policy.datasetId `
            -and $first.run.datasetVersion -eq $policy.datasetVersion `
            -and $first.run.scopeRevision -eq "local-report-scope-v1" `
            -and $first.run.scopeSubjectCount -eq 1000 `
            -and $first.run.scopeSnapshotChecksum -eq $preview.scopeSnapshotChecksum `
            -and $first.events.Count -eq 3 `
            -and ($first.events.action -join ",") -eq "queued,started,completed"
    ) @{
        status = $first.run.status
        runId = $first.run.runId
        revision = $first.run.revisionNumber
        rows = $first.run.rowCount
        checksum = $first.run.resultChecksum
        actions = $first.events.action
    }

    $replayResponse = Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/run" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $runRequest
    $replay = $replayResponse.Json
    Add-Check "Exact run retry is idempotent" (
        $replayResponse.Status -eq 201 `
            -and $replay.run.runId -eq $first.run.runId `
            -and $replay.run.replay `
            -and $replay.events.Count -eq 3
    ) @{
        originalRunId = $first.run.runId
        replayRunId = $replay.run.runId
        replay = $replay.run.replay
    }

    $secondRequest = $baseRequest.Clone()
    $secondRequest.idempotencyKey = "report-run-$([Guid]::NewGuid().ToString('N'))"
    $second = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/run" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $secondRequest).Json
    $history = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/runs?page=1&pageSize=10" `
        -RequestHeaders $headers
    Add-Check "Equivalent snapshot runs reproduce the artifact" (
        $second.run.runId -ne $first.run.runId `
            -and $second.run.resultChecksum -eq $first.run.resultChecksum `
            -and $second.run.rowCount -eq $first.run.rowCount `
            -and $history.total -eq 2 `
            -and @($history.runs | Where-Object { $_.status -ne "completed" }).Count -eq 0
    ) @{
        firstRun = $first.run.runId
        secondRun = $second.run.runId
        checksum = $second.run.resultChecksum
        historyTotal = $history.total
    }

    $download = Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/runs/$($first.run.runId)/download" `
        -RequestHeaders $headers
    $downloadChecksum = Get-BytesSha256 -Bytes $download.Bytes
    $detailAfterDownload = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/runs/$($first.run.runId)" `
        -RequestHeaders $headers
    Add-Check "Protected artifact download retains evidence" (
        $download.Status -eq 200 `
            -and $download.ContentType -eq "text/csv" `
            -and $downloadChecksum -eq $first.run.resultChecksum `
            -and $detailAfterDownload.events.Count -eq 4 `
            -and $detailAfterDownload.events[3].action -eq "downloaded"
    ) @{
        status = $download.Status
        contentType = $download.ContentType
        bytes = $download.Bytes.Length
        checksum = $downloadChecksum
        eventActions = $detailAfterDownload.events.action
    }

    $scopedPurpose = "Verify actor-bound facility report scope."
    $scoped = New-ActiveDefinition `
        -StableKey "$marker-scoped" `
        -Title "Synthetic scoped execution" `
        -Purpose $scopedPurpose `
        -Family "appointments" `
        -RowPolicy "facility-scoped"
    $scopedDefinitionId = [string]$scoped.definitionId
    $scopedRequest = @{
        purpose = $scopedPurpose
        recipientUsername = "admin"
        deliveryMode = "local-download"
        asOfDate = $policy.requiredAsOfDate
        parameters = @{}
        idempotencyKey = "report-run-$([Guid]::NewGuid().ToString('N'))"
    }
    $scopeRun = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$scopedDefinitionId/run" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $scopedRequest).Json
    $scopeDownload = Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/runs/$($scopeRun.run.runId)/download" `
        -RequestHeaders $headers
    Add-Check "Missing staff scope creates visible failed evidence" (
        $scopeRun.run.status -eq "failed" `
            -and $scopeRun.run.failureCode -eq "scope-identity-unavailable" `
            -and $scopeRun.run.scopeRevision -eq "local-report-scope-v1" `
            -and $scopeRun.run.scopeSnapshotChecksum -match "^[a-f0-9]{64}$" `
            -and -not $scopeRun.run.downloadAvailable `
            -and ($scopeRun.events.action -join ",") -eq "queued,failed" `
            -and $scopeDownload.Status -eq 404
    ) @{
        status = $scopeRun.run.status
        failureCode = $scopeRun.run.failureCode
        eventActions = $scopeRun.events.action
        downloadStatus = $scopeDownload.Status
    }

    $providerScopedRequest = @{
        purpose = $scopedPurpose
        recipientUsername = "gold-provider-01"
        deliveryMode = "local-download"
        asOfDate = $providerPolicy.requiredAsOfDate
        parameters = @{}
    }
    $providerScopedPreview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$scopedDefinitionId/preview" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body $providerScopedRequest
    $providerScopedRunRequest = $providerScopedRequest.Clone()
    $providerScopedRunRequest.idempotencyKey =
        "report-run-$([Guid]::NewGuid().ToString('N'))"
    $providerScopedRun = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$scopedDefinitionId/run" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body $providerScopedRunRequest).Json
    Add-Check "Facility scope filters and pins the active staff facility" (
        $providerScopedPreview.totalRows -eq 501 `
            -and $providerScopedPreview.scopeFacilityId -eq 10 `
            -and $providerScopedPreview.scopeSubjectCount -eq 333 `
            -and $providerScopedRun.run.status -eq "completed" `
            -and $providerScopedRun.run.rowCount -eq 501 `
            -and $providerScopedRun.run.scopeFacilityId -eq 10 `
            -and $providerScopedRun.run.scopeSubjectCount -eq 333 `
            -and $providerScopedRun.run.scopeSnapshotChecksum -eq
                $providerScopedPreview.scopeSnapshotChecksum
    ) @{
        previewRows = $providerScopedPreview.totalRows
        runRows = $providerScopedRun.run.rowCount
        facilityId = $providerScopedRun.run.scopeFacilityId
        subjectCount = $providerScopedRun.run.scopeSubjectCount
        scopeChecksum = $providerScopedRun.run.scopeSnapshotChecksum
    }

    $assignedPurpose = "Verify provider and care-team patient assignment scope."
    $assigned = New-ActiveDefinition `
        -StableKey "$marker-assigned" `
        -Title "Synthetic assigned-patient execution" `
        -Purpose $assignedPurpose `
        -Family "patients" `
        -RowPolicy "patient-assigned"
    $assignedDefinitionId = [string]$assigned.definitionId
    $assignedRequest = @{
        purpose = $assignedPurpose
        recipientUsername = "gold-provider-01"
        deliveryMode = "local-download"
        asOfDate = $providerPolicy.requiredAsOfDate
        parameters = @{}
    }
    $assignedPreview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$assignedDefinitionId/preview" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body $assignedRequest
    $assignedRunRequest = $assignedRequest.Clone()
    $assignedRunRequest.idempotencyKey =
        "report-run-$([Guid]::NewGuid().ToString('N'))"
    $assignedRun = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$assignedDefinitionId/run" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body $assignedRunRequest).Json
    Add-Check "Patient assignment scope filters and pins provider relationships" (
        $assignedPreview.totalRows -eq 83 `
            -and $assignedPreview.scopeSubjectCount -eq 83 `
            -and $assignedRun.run.status -eq "completed" `
            -and $assignedRun.run.rowCount -eq 83 `
            -and $assignedRun.run.scopeFacilityId -eq 10 `
            -and $assignedRun.run.scopeSubjectCount -eq 83 `
            -and $assignedRun.run.resultChecksum -eq $assignedPreview.resultChecksum `
            -and $assignedRun.run.scopeSnapshotChecksum -eq
                $assignedPreview.scopeSnapshotChecksum
    ) @{
        previewRows = $assignedPreview.totalRows
        runRows = $assignedRun.run.rowCount
        subjectCount = $assignedRun.run.scopeSubjectCount
        resultChecksum = $assignedRun.run.resultChecksum
        scopeChecksum = $assignedRun.run.scopeSnapshotChecksum
    }

    $inventoryPurpose = "Verify unsupported patient-linked inventory scope."
    $inventory = New-ActiveDefinition `
        -StableKey "$marker-assigned-inventory" `
        -Title "Synthetic patient-linked inventory execution" `
        -Purpose $inventoryPurpose `
        -Family "inventory" `
        -RowPolicy "patient-assigned"
    $inventoryRequest = @{
        purpose = $inventoryPurpose
        recipientUsername = "gold-provider-01"
        deliveryMode = "local-download"
        asOfDate = $providerPolicy.requiredAsOfDate
        parameters = @{}
        idempotencyKey = "report-run-$([Guid]::NewGuid().ToString('N'))"
    }
    $inventoryRun = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$($inventory.definitionId)/run" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body $inventoryRequest).Json
    Add-Check "Unsupported patient-linked family fails without an artifact" (
        $inventoryRun.run.status -eq "failed" `
            -and $inventoryRun.run.failureCode -eq "scope-family-unavailable" `
            -and -not $inventoryRun.run.downloadAvailable `
            -and ($inventoryRun.events.action -join ",") -eq "queued,failed"
    ) @{
        status = $inventoryRun.run.status
        failureCode = $inventoryRun.run.failureCode
        actions = $inventoryRun.events.action
    }

    $facilityMatrix = [System.Collections.Generic.List[object]]::new()
    foreach ($family in @(
        "operational",
        "patients",
        "encounters",
        "referrals",
        "chart-tracker",
        "inventory"
    )) {
        $familyPurpose = "Verify facility-scoped $family report execution."
        $familyDefinition = New-ActiveDefinition `
            -StableKey "$marker-facility-$family" `
            -Title "Synthetic facility $family execution" `
            -Purpose $familyPurpose `
            -Family $family `
            -RowPolicy "facility-scoped"
        $familyRequest = @{
            purpose = $familyPurpose
            recipientUsername = "gold-provider-01"
            deliveryMode = "local-download"
            asOfDate = $providerPolicy.requiredAsOfDate
            parameters = @{}
        }
        $familyPreview = Invoke-Json `
            -Uri "$ApiBaseUrl/api/reports/definitions/$($familyDefinition.definitionId)/preview" `
            -Method "POST" `
            -RequestHeaders $providerHeaders `
            -Body $familyRequest
        $familyRunRequest = $familyRequest.Clone()
        $familyRunRequest.idempotencyKey =
            "report-run-$([Guid]::NewGuid().ToString('N'))"
        $familyRun = (Invoke-Api `
            -Uri "$ApiBaseUrl/api/reports/definitions/$($familyDefinition.definitionId)/run" `
            -Method "POST" `
            -RequestHeaders $providerHeaders `
            -Body $familyRunRequest).Json
        $familyDownload = Invoke-Api `
            -Uri "$ApiBaseUrl/api/reports/runs/$($familyRun.run.runId)/download" `
            -RequestHeaders $providerHeaders
        $facilityMatrix.Add([ordered]@{
            family = $family
            passed = (
                $familyPreview.scopeFacilityId -eq 10 `
                    -and $familyPreview.scopeSubjectCount -eq 333 `
                    -and $familyRun.run.status -eq "completed" `
                    -and $familyRun.run.scopeFacilityId -eq 10 `
                    -and $familyRun.run.scopeSubjectCount -eq 333 `
                    -and $familyRun.run.resultChecksum -eq
                        $familyPreview.resultChecksum `
                    -and $familyDownload.Status -eq 200 `
                    -and (Get-BytesSha256 -Bytes $familyDownload.Bytes) -eq
                        $familyRun.run.resultChecksum
            )
            rows = $familyRun.run.rowCount
            checksum = $familyRun.run.resultChecksum
        })
    }
    Add-Check "Facility scope covers every curated report family and artifact path" (
        $facilityMatrix.Count -eq 6 `
            -and @($facilityMatrix | Where-Object { -not $_.passed }).Count -eq 0
    ) $facilityMatrix

    $assignedMatrix = [System.Collections.Generic.List[object]]::new()
    foreach ($family in @(
        "operational",
        "appointments",
        "encounters",
        "referrals",
        "chart-tracker"
    )) {
        $familyPurpose = "Verify assigned-patient $family report execution."
        $familyDefinition = New-ActiveDefinition `
            -StableKey "$marker-assigned-$family" `
            -Title "Synthetic assigned-patient $family execution" `
            -Purpose $familyPurpose `
            -Family $family `
            -RowPolicy "patient-assigned"
        $familyRequest = @{
            purpose = $familyPurpose
            recipientUsername = "gold-provider-01"
            deliveryMode = "local-download"
            asOfDate = $providerPolicy.requiredAsOfDate
            parameters = @{}
        }
        $familyPreview = Invoke-Json `
            -Uri "$ApiBaseUrl/api/reports/definitions/$($familyDefinition.definitionId)/preview" `
            -Method "POST" `
            -RequestHeaders $providerHeaders `
            -Body $familyRequest
        $familyRunRequest = $familyRequest.Clone()
        $familyRunRequest.idempotencyKey =
            "report-run-$([Guid]::NewGuid().ToString('N'))"
        $familyRun = (Invoke-Api `
            -Uri "$ApiBaseUrl/api/reports/definitions/$($familyDefinition.definitionId)/run" `
            -Method "POST" `
            -RequestHeaders $providerHeaders `
            -Body $familyRunRequest).Json
        $familyDownload = Invoke-Api `
            -Uri "$ApiBaseUrl/api/reports/runs/$($familyRun.run.runId)/download" `
            -RequestHeaders $providerHeaders
        $assignedMatrix.Add([ordered]@{
            family = $family
            passed = (
                $familyPreview.scopeSubjectCount -eq 83 `
                    -and $familyRun.run.status -eq "completed" `
                    -and $familyRun.run.scopeSubjectCount -eq 83 `
                    -and $familyRun.run.resultChecksum -eq
                        $familyPreview.resultChecksum `
                    -and $familyDownload.Status -eq 200 `
                    -and (Get-BytesSha256 -Bytes $familyDownload.Bytes) -eq
                        $familyRun.run.resultChecksum
            )
            rows = $familyRun.run.rowCount
            checksum = $familyRun.run.resultChecksum
        })
    }
    Add-Check "Patient assignment scope covers every patient-linked family and artifact path" (
        $assignedMatrix.Count -eq 5 `
            -and @($assignedMatrix | Where-Object { -not $_.passed }).Count -eq 0
    ) $assignedMatrix

    $careTeamCreate = Invoke-Api `
        -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0001/care-team" `
        -Method "PUT" `
        -RequestHeaders $headers `
        -Body @{
            teamName = "Temporary report-scope fixture"
            teamStatus = "active"
            members = @(
                @{
                    userId = 101
                    role = "primary_care_provider"
                    facilityId = 10
                    status = "active"
                    note = $marker
                }
            )
        }
    $careTeamFixtureActive = $careTeamCreate.Status -eq 200
    $teamPolicy = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/execution-policy" `
        -RequestHeaders $providerHeaders
    $teamPreview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$assignedDefinitionId/preview" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body $assignedRequest
    $teamRunRequest = $assignedRequest.Clone()
    $teamRunRequest.idempotencyKey =
        "report-run-$([Guid]::NewGuid().ToString('N'))"
    $teamRun = (Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$assignedDefinitionId/run" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body $teamRunRequest).Json

    $careTeamDelete = Invoke-Api `
        -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0001/care-team" `
        -Method "PUT" `
        -RequestHeaders $headers `
        -Body @{
            teamName = "Care Team"
            teamStatus = "active"
            members = @()
        }
    if ($careTeamDelete.Status -eq 200) {
        $careTeamFixtureActive = $false
    }
    $restoredPolicy = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/execution-policy" `
        -RequestHeaders $providerHeaders
    $restoredPreview = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$assignedDefinitionId/preview" `
        -Method "POST" `
        -RequestHeaders $providerHeaders `
        -Body $assignedRequest
    $historicTeamRun = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/runs/$($teamRun.run.runId)" `
        -RequestHeaders $providerHeaders
    Add-Check "Active care-team membership expands only the pinned assigned-patient scope" (
        $careTeamCreate.Status -eq 200 `
            -and $teamPolicy.currentActorScope.assignedPatientCount -eq 84 `
            -and $teamPreview.totalRows -eq 84 `
            -and $teamPreview.scopeSubjectCount -eq 84 `
            -and $teamRun.run.status -eq "completed" `
            -and $teamRun.run.rowCount -eq 84 `
            -and $teamRun.run.scopeSubjectCount -eq 84 `
            -and $teamRun.run.scopeSnapshotChecksum -ne
                $assignedRun.run.scopeSnapshotChecksum `
            -and $careTeamDelete.Status -eq 200 `
            -and $restoredPolicy.currentActorScope.assignedPatientCount -eq 83 `
            -and $restoredPreview.totalRows -eq 83 `
            -and $restoredPreview.scopeSnapshotChecksum -eq
                $assignedPreview.scopeSnapshotChecksum `
            -and $historicTeamRun.run.scopeSubjectCount -eq 84 `
            -and $historicTeamRun.run.resultChecksum -eq $teamRun.run.resultChecksum
    ) @{
        createStatus = $careTeamCreate.Status
        expandedCount = $teamPolicy.currentActorScope.assignedPatientCount
        expandedRows = $teamRun.run.rowCount
        expandedScopeChecksum = $teamRun.run.scopeSnapshotChecksum
        cleanupStatus = $careTeamDelete.Status
        restoredCount = $restoredPolicy.currentActorScope.assignedPatientCount
        restoredRows = $restoredPreview.totalRows
        historicPinnedCount = $historicTeamRun.run.scopeSubjectCount
    }

    $changedKeyRequest = $scopedRequest.Clone()
    $changedKeyRequest.idempotencyKey = $runRequest.idempotencyKey
    $conflict = Invoke-Api `
        -Uri "$ApiBaseUrl/api/reports/definitions/$scopedDefinitionId/run" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body $changedKeyRequest
    Add-Check "Changed idempotent request conflicts" (
        $conflict.Status -eq 409 `
            -and $conflict.Json.existingRun.runId -eq $first.run.runId
    ) @{
        status = $conflict.Status
        existingRunId = $conflict.Json.existingRun.runId
    }

    $successor = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/revisions" `
        -Method "POST" `
        -RequestHeaders $headers `
        -Body @{
            expectedLatestRevisionNumber = 1
            title = "Synthetic patient execution v2"
            ownerUsername = "admin"
            purpose = "Verify successor report execution meaning."
            reportFamily = "patients"
            sensitivity = "restricted"
            rowPolicy = "practice-wide"
            retentionDays = 45
            allowedRecipients = @("requesting-user", "report-owner")
            deliveryModes = @("local-download")
            reason = "Create a successor after completing revision-one runs."
        }
    $reviewedSuccessor = Invoke-Transition `
        -DefinitionId $definitionId `
        -Action "review" `
        -ExpectedVersion $successor.revisions[0].version `
        -Reason "Owner reviewed the successor execution contract."
    $approvedSuccessor = Invoke-Transition `
        -DefinitionId $definitionId `
        -Action "approve" `
        -ExpectedVersion $reviewedSuccessor.revisions[0].version `
        -Reason "Approve the successor execution contract."
    $null = Invoke-Transition `
        -DefinitionId $definitionId `
        -Action "activate" `
        -ExpectedVersion $approvedSuccessor.revisions[0].version `
        -Reason "Activate the successor execution contract."
    $historicRun = Invoke-Json `
        -Uri "$ApiBaseUrl/api/reports/runs/$($first.run.runId)" `
        -RequestHeaders $headers
    Add-Check "Successor activation preserves historic run meaning" (
        $historicRun.run.revisionNumber -eq 1 `
            -and $historicRun.run.definitionTitle -eq "Synthetic patient execution" `
            -and $historicRun.run.purpose -eq $purpose `
            -and $historicRun.run.resultChecksum -eq $first.run.resultChecksum
    ) @{
        historicRevision = $historicRun.run.revisionNumber
        historicTitle = $historicRun.run.definitionTitle
        historicPurpose = $historicRun.run.purpose
        checksum = $historicRun.run.resultChecksum
    }
}
catch {
    Add-Check "Unhandled report execution test error" $false @{
        message = $_.Exception.Message
        position = $_.InvocationInfo.PositionMessage
        stack = $_.ScriptStackTrace
    }
}
finally {
    if ($careTeamFixtureActive -and $null -ne $headers) {
        $careTeamCleanup = Invoke-Api `
            -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0001/care-team" `
            -Method "PUT" `
            -RequestHeaders $headers `
            -Body @{
                teamName = "Care Team"
                teamStatus = "active"
                members = @()
            }
        Add-Check "Care-team scope fixture emergency cleanup" (
            $careTeamCleanup.Status -eq 200
        ) @{ cleanupStatus = $careTeamCleanup.Status }
    }
    if ($null -ne $headers) {
        foreach ($definitionId in $definitionIds) {
            $cleanup = Invoke-Api `
                -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/test-fixture" `
                -Method "DELETE" `
                -RequestHeaders $headers
            $after = Invoke-Api `
                -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId" `
                -RequestHeaders $headers
            Add-Check "Synthetic report execution fixture cleanup: $definitionId" (
                $cleanup.Status -eq 204 -and $after.Status -eq 404
            ) @{
                cleanupStatus = $cleanup.Status
                definitionStatus = $after.Status
            }
        }
    }

    $result = [ordered]@{
        status = $status
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
        apiBaseUrl = $ApiBaseUrl
        marker = $marker
        checks = $checks
    }
    $result |
        ConvertTo-Json -Depth 30 |
        Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 30
}

if ($status -ne "passed") {
    exit 1
}
