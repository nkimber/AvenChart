# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
Add-Type -AssemblyName System.Net.Http

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts"
$ResultPath = Join-Path $ArtifactsRoot "latest-report-definition-foundation-test.json"
New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$definitionId = $null
$headers = $null
$marker = "tmp-report-focused-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

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
        [string]$Method,
        [hashtable]$RequestHeaders = @{},
        [object]$Body = $null
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
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 10
            $request.Content = [System.Net.Http.StringContent]::new(
                $json,
                [Text.Encoding]::UTF8,
                "application/json"
            )
        }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            return [int]$response.StatusCode
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function Invoke-Transition {
    param(
        [string]$Action,
        [int]$ExpectedVersion,
        [string]$Reason
    )

    return Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/$Action" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{
            expectedVersion = $ExpectedVersion
            reason = $Reason
        } | ConvertTo-Json) `
        -TimeoutSec 20
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
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login

    $unauthenticatedStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/reports/definition-policy" `
        -Method Get
    Add-Check "Protected definition policy" ($unauthenticatedStatus -eq 401) @{
        unauthenticatedStatus = $unauthenticatedStatus
    }

    $policy = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/definition-policy" `
        -Headers $headers `
        -TimeoutSec 20
    $formFamily = @(
        $policy.families |
            Where-Object { $_.key -eq "clinical-forms" }
    ) | Select-Object -First 1
    Add-Check "Truthful bounded policy" (
        $policy.revision -eq "local-report-definition-v2" `
            -and -not $policy.rawSqlAccepted `
            -and -not $policy.executableTemplatesAccepted `
            -and -not $policy.externalDeliveryEnabled `
            -and $policy.rowPolicyExecutionEnforced `
            -and $policy.families.Count -eq 8 `
            -and $formFamily.parameterSchema.Count -eq 2 `
            -and ($formFamily.parameterSchema.key -join ",") -eq "from,to" `
            -and $formFamily.outputSchema.Count -eq 21 `
            -and $formFamily.outputSchema.key -contains "schema_hash" `
            -and $formFamily.outputSchema.key -contains "report_column" `
            -and $formFamily.outputSchema.key -contains "content_hash" `
            -and $policy.productionBlockers.Count -eq 8
    ) @{
        revision = $policy.revision
        familyCount = $policy.families.Count
        formOutputFields = $formFamily.outputSchema.key
        blockerCount = $policy.productionBlockers.Count
        rawSqlAccepted = $policy.rawSqlAccepted
        rowPolicyExecutionEnforced = $policy.rowPolicyExecutionEnforced
    }
    $executionPolicy = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/execution-policy" `
        -Headers $headers `
        -TimeoutSec 20

    $legacy = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/definitions?search=legacy.&status=draft&page=1&pageSize=50" `
        -Headers $headers `
        -TimeoutSec 20
    $legacyBlocked = @(
        $legacy.definitions |
            Where-Object { -not $_.legacyReviewRequired -or $_.activeRevisionNumber }
    ).Count
    Add-Check "Legacy promotion is fail closed" (
        $legacy.total -ge 1 -and $legacyBlocked -eq 0
    ) @{
        promotedDrafts = $legacy.total
        incorrectlyRunnable = $legacyBlocked
    }

    $createBody = @{
        stableKey = $marker
        title = "Synthetic appointment governance"
        ownerUsername = "admin"
        purpose = "Verify the complete governed report definition lifecycle safely."
        reportFamily = "appointments"
        sensitivity = "restricted"
        rowPolicy = "facility-scoped"
        retentionDays = 30
        allowedRecipients = @("requesting-user")
        deliveryModes = @("local-download")
        reason = "Create a bounded synthetic report-definition fixture."
    }
    $unknownFieldBody = $createBody.Clone()
    $unknownFieldBody.stableKey = "$marker-unknown"
    $unknownFieldBody.rawSql = "select * from patients"
    $unknownFieldStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/reports/definitions" `
        -Method Post `
        -RequestHeaders $headers `
        -Body $unknownFieldBody
    $executableBody = $createBody.Clone()
    $executableBody.stableKey = "$marker-executable"
    $executableBody.purpose = "Execute select every patient row from the patients table for a synthetic verification."
    $executableStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/reports/definitions" `
        -Method Post `
        -RequestHeaders $headers `
        -Body $executableBody
    Add-Check "Unsafe definition content rejected" (
        $unknownFieldStatus -eq 400 -and $executableStatus -eq 400
    ) @{
        unknownFieldStatus = $unknownFieldStatus
        executableContentStatus = $executableStatus
    }

    $created = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/definitions" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body ($createBody | ConvertTo-Json -Depth 10) `
        -TimeoutSec 20
    $definitionId = $created.definitionId
    $draft = $created.revisions[0]
    $duplicateStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/reports/definitions" `
        -Method Post `
        -RequestHeaders $headers `
        -Body $createBody
    Add-Check "Complete draft and stable-key uniqueness" (
        $draft.status -eq "draft" `
            -and $draft.version -eq 0 `
            -and $draft.metricDictionary.Count -eq 4 `
            -and $draft.parameterSchema.Count -eq 2 `
            -and $draft.sourceDatasets.Count -eq 1 `
            -and $draft.outputSchema.Count -eq 4 `
            -and $draft.validationFixture.datasetId -eq "gold-avenchart-synthetic" `
            -and $duplicateStatus -eq 409
    ) @{
        definitionId = $definitionId
        stableKey = $created.stableKey
        metricCount = $draft.metricDictionary.Count
        parameterCount = $draft.parameterSchema.Count
        duplicateStatus = $duplicateStatus
    }

    $reviewed = Invoke-Transition `
        -Action "review" `
        -ExpectedVersion $draft.version `
        -Reason "Owner reviewed the metric dictionary and permitted purpose."
    $staleStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/approve" `
        -Method Post `
        -RequestHeaders $headers `
        -Body @{
            expectedVersion = 0
            reason = "Attempt approval from a stale loaded definition version."
        }
    $approved = Invoke-Transition `
        -Action "approve" `
        -ExpectedVersion $reviewed.revisions[0].version `
        -Reason "Approve the bounded synthetic definition for local activation."
    $active = Invoke-Transition `
        -Action "activate" `
        -ExpectedVersion $approved.revisions[0].version `
        -Reason "Activate the approved local governed report definition."
    $catalogAtRevisionOne = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/catalog?search=$marker&page=1&pageSize=50" `
        -Headers $headers `
        -TimeoutSec 20
    Add-Check "Version-safe owner review and activation" (
        $reviewed.revisions[0].status -eq "reviewed" `
            -and $approved.revisions[0].status -eq "approved" `
            -and $active.revisions[0].status -eq "active" `
            -and $staleStatus -eq 409 `
            -and $catalogAtRevisionOne.total -eq 1 `
            -and $catalogAtRevisionOne.definitions[0].activeRevisionNumber -eq 1
    ) @{
        staleStatus = $staleStatus
        versions = @(
            $draft.version,
            $reviewed.revisions[0].version,
            $approved.revisions[0].version,
            $active.revisions[0].version
        )
        catalogRevision = $catalogAtRevisionOne.definitions[0].activeRevisionNumber
    }

    $successorBody = @{
        expectedLatestRevisionNumber = 1
        title = "Synthetic appointment governance v2"
        ownerUsername = "admin"
        purpose = "Verify immutable successor activation for the governed report lifecycle."
        reportFamily = "appointments"
        sensitivity = "restricted"
        rowPolicy = "patient-assigned"
        retentionDays = 45
        allowedRecipients = @("requesting-user")
        deliveryModes = @("local-download")
        reason = "Create a complete immutable successor report revision."
    }
    $successor = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/revisions" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body ($successorBody | ConvertTo-Json -Depth 10) `
        -TimeoutSec 20
    $catalogDuringDraft = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/catalog?search=$marker&page=1&pageSize=50" `
        -Headers $headers `
        -TimeoutSec 20
    Add-Check "Successor draft preserves active meaning" (
        $successor.revisions.Count -eq 2 `
            -and $successor.revisions[0].revisionNumber -eq 2 `
            -and $successor.revisions[0].status -eq "draft" `
            -and $successor.revisions[1].revisionNumber -eq 1 `
            -and $successor.revisions[1].status -eq "active" `
            -and $catalogDuringDraft.definitions[0].activeRevisionNumber -eq 1
    ) @{
        latestRevision = $successor.revisions[0].revisionNumber
        activeCatalogRevision = $catalogDuringDraft.definitions[0].activeRevisionNumber
        priorStatus = $successor.revisions[1].status
    }

    $reviewedSuccessor = Invoke-Transition `
        -Action "review" `
        -ExpectedVersion $successor.revisions[0].version `
        -Reason "Owner reviewed successor metrics, scope, and purpose."
    $approvedSuccessor = Invoke-Transition `
        -Action "approve" `
        -ExpectedVersion $reviewedSuccessor.revisions[0].version `
        -Reason "Approve the successor definition for controlled local activation."
    $activeSuccessor = Invoke-Transition `
        -Action "activate" `
        -ExpectedVersion $approvedSuccessor.revisions[0].version `
        -Reason "Activate the successor while preserving the prior revision."
    $actions = ($activeSuccessor.events | ForEach-Object { $_.action }) -join ","
    $invalidChecksumCount = @(
        $activeSuccessor.events |
            Where-Object { $_.snapshotChecksum -notmatch "^[a-f0-9]{64}$" }
    ).Count
    Add-Check "Immutable successor and event evidence" (
        $activeSuccessor.revisions[0].status -eq "active" `
            -and $activeSuccessor.revisions[0].revisionNumber -eq 2 `
            -and $activeSuccessor.revisions[1].status -eq "suspended" `
            -and $activeSuccessor.revisions[1].revisionNumber -eq 1 `
            -and $activeSuccessor.events.Count -eq 9 `
            -and $actions -eq "activate,superseded,approve,review,revision-created,activate,approve,review,created" `
            -and $invalidChecksumCount -eq 0
    ) @{
        revisionStates = @(
            $activeSuccessor.revisions |
                ForEach-Object { "$($_.revisionNumber):$($_.status)" }
        )
        eventCount = $activeSuccessor.events.Count
        actions = $actions
        invalidChecksumCount = $invalidChecksumCount
    }

    $retired = Invoke-Transition `
        -Action "retire" `
        -ExpectedVersion $activeSuccessor.revisions[0].version `
        -Reason "Retire the completed synthetic governed report fixture."
    $catalogAfterRetire = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/reports/catalog?search=$marker&page=1&pageSize=50" `
        -Headers $headers `
        -TimeoutSec 20
    $runAfterRetireStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/run" `
        -Method Post `
        -RequestHeaders $headers `
        -Body @{
            purpose = $successorBody.purpose
            recipientUsername = "admin"
            deliveryMode = "local-download"
            asOfDate = $executionPolicy.requiredAsOfDate
            parameters = @{}
            idempotencyKey = "report-run-$([Guid]::NewGuid().ToString('N'))"
        }
    Add-Check "Retirement blocks catalog and new run" (
        $retired.revisions[0].status -eq "retired" `
            -and $retired.events.Count -eq 10 `
            -and $catalogAfterRetire.total -eq 0 `
            -and $runAfterRetireStatus -eq 404
    ) @{
        status = $retired.revisions[0].status
        eventCount = $retired.events.Count
        catalogCount = $catalogAfterRetire.total
        runStatus = $runAfterRetireStatus
    }
}
catch {
    Add-Check "Report definition focused workflow" $false $_.Exception.Message
}
finally {
    if ($definitionId -and $headers) {
        try {
            $cleanupStatus = Get-HttpStatus `
                -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId/test-fixture" `
                -Method Delete `
                -RequestHeaders $headers
            $afterCleanup = Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/reports/definitions?search=$marker&page=1&pageSize=50" `
                -Headers $headers `
                -TimeoutSec 20
            $detailStatus = Get-HttpStatus `
                -Uri "$ApiBaseUrl/api/reports/definitions/$definitionId" `
                -Method Get `
                -RequestHeaders $headers
            Add-Check "Owned fixture cleanup" (
                $cleanupStatus -eq 204 `
                    -and $afterCleanup.total -eq 0 `
                    -and $detailStatus -eq 404
            ) @{
                cleanupStatus = $cleanupStatus
                listResidue = $afterCleanup.total
                detailStatus = $detailStatus
            }
        }
        catch {
            Add-Check "Owned fixture cleanup" $false $_.Exception.Message
        }
    }
}

$result = [ordered]@{
    status = $status
    apiBaseUrl = $ApiBaseUrl
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    marker = $marker
    checks = $checks
}
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
Write-Host "Report definition foundation test result: $ResultPath"

if ($status -ne "passed") {
    exit 1
}
