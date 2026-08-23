# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts"
$ResultPath = Join-Path $ArtifactsRoot "latest-managed-record-foundation-test.json"
New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$intakeId = $null
$marker = "TMP-RECORD-FOCUSED-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$headers = $null

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

    try {
        $arguments = @{
            Uri = $Uri
            Method = $Method
            Headers = $RequestHeaders
            UseBasicParsing = $true
            TimeoutSec = 20
            ErrorAction = "Stop"
        }
        if ($null -ne $Body) {
            $arguments.ContentType = "application/json"
            $arguments.Body = $Body | ConvertTo-Json -Depth 8
        }
        $response = Invoke-WebRequest @arguments
        return [int]$response.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
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
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login

    $unauthenticatedStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/records/policy" `
        -Method Get
    Add-Check "Protected policy" ($unauthenticatedStatus -eq 401) @{
        unauthenticatedStatus = $unauthenticatedStatus
    }

    $policy = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/policy" `
        -Headers $headers `
        -TimeoutSec 20
    Add-Check "Truthful local policy" (
        $policy.revision -eq "local-record-control-v1" `
            -and -not $policy.antiMalwareVerified `
            -and $policy.productionBlockers.Count -eq 7
    ) @{
        revision = $policy.revision
        antiMalwareVerified = $policy.antiMalwareVerified
        blockerCount = $policy.productionBlockers.Count
    }

    $bytes = [Text.Encoding]::UTF8.GetBytes("$marker focused content")
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $checksum = ([BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $hasher.Dispose()
    }
    $createBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        title = $marker
        serviceDate = "2026-07-28"
        encounter = $null
        recordClass = "clinical-record"
        sourceType = "file-upload"
        authorName = "Focused managed-record verifier"
        facilityId = $null
        sensitivity = "standard"
        languageTag = "en-US"
        fileName = "$marker.txt"
        mediaType = "text/plain"
        contentBase64 = [Convert]::ToBase64String($bytes)
        expectedChecksumSha256 = $checksum
        idempotencyKey = [Guid]::NewGuid().ToString()
        reason = "Focused REC-01 capture"
    }

    $created = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body ($createBody | ConvertTo-Json) `
        -TimeoutSec 20
    $intakeId = $created.intake.intakeId
    $replay = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body ($createBody | ConvertTo-Json) `
        -TimeoutSec 20
    $conflictBody = $createBody.Clone()
    $conflictBody.title = "$marker changed"
    $conflictStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/records/" `
        -Method Post `
        -RequestHeaders $headers `
        -Body $conflictBody
    Add-Check "Idempotent capture" (
        -not $created.idempotentReplay `
            -and $replay.idempotentReplay `
            -and $replay.intake.intakeId -eq $intakeId `
            -and $conflictStatus -eq 409
    ) @{
        intakeId = $intakeId
        createReplay = $created.idempotentReplay
        exactReplay = $replay.idempotentReplay
        conflictStatus = $conflictStatus
    }

    $invalidChecksumBody = $createBody.Clone()
    $invalidChecksumBody.idempotencyKey = [Guid]::NewGuid().ToString()
    $invalidChecksumBody.expectedChecksumSha256 = "0" * 64
    $checksumStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/records/" `
        -Method Post `
        -RequestHeaders $headers `
        -Body $invalidChecksumBody
    Add-Check "Checksum rejection" ($checksumStatus -eq 400) @{
        status = $checksumStatus
    }

    $documentsBefore = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001?includeArchived=true" `
        -Headers $headers `
        -TimeoutSec 20
    $listBefore = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/?patientId=MOD-PAT-0001" `
        -Headers $headers `
        -TimeoutSec 20
    $listedBefore = @($listBefore.items | Where-Object {
        $_.intakeId -eq $intakeId -and $_.availabilityStatus -eq "withheld"
    })
    $ordinaryBefore = @($documentsBefore.documents | Where-Object { $_.name -eq $marker })
    Add-Check "Pre-release content boundary" (
        $listedBefore.Count -eq 1 -and $ordinaryBefore.Count -eq 0
    ) @{
        managedWithheld = $listedBefore.Count
        ordinaryDocuments = $ordinaryBefore.Count
    }

    $classified = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/$intakeId/classification" `
        -Method Put `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{
            expectedVersion = 0
            recordClass = "correspondence"
            sourceType = "scanner-capture"
            authorName = "Focused classification verifier"
            facilityId = $null
            sensitivity = "restricted"
            languageTag = "en-US"
            reason = "Focused REC-01 classification revision"
        } | ConvertTo-Json) `
        -TimeoutSec 20
    $quarantined = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/$intakeId/quarantine" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{ expectedVersion = 1; reason = "Focused quarantine" } | ConvertTo-Json) `
        -TimeoutSec 20
    $staleStatus = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/records/$intakeId/start" `
        -Method Post `
        -RequestHeaders $headers `
        -Body @{ expectedVersion = 1; reason = "Focused stale start" }
    $started = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/$intakeId/start" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{ expectedVersion = 2; reason = "Focused validation start" } | ConvertTo-Json) `
        -TimeoutSec 20
    $failed = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/$intakeId/fail" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{ expectedVersion = 3; reason = "Focused simulated failure" } | ConvertTo-Json) `
        -TimeoutSec 20
    $retried = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/$intakeId/retry" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{ expectedVersion = 4; reason = "Focused retry" } | ConvertTo-Json) `
        -TimeoutSec 20
    $restarted = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/$intakeId/start" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{ expectedVersion = 5; reason = "Focused validation restart" } | ConvertTo-Json) `
        -TimeoutSec 20
    $released = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/$intakeId/release" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{ expectedVersion = 6; reason = "Focused local release" } | ConvertTo-Json) `
        -TimeoutSec 20

    Add-Check "Version-safe lifecycle" (
        $classified.workflowVersion -eq 1 `
            -and $classified.recordClass -eq "correspondence" `
            -and $classified.sensitivity -eq "restricted" `
            -and $quarantined.workflowVersion -eq 2 `
            -and $staleStatus -eq 409 `
            -and $started.workflowVersion -eq 3 `
            -and $failed.workflowVersion -eq 4 `
            -and $failed.state -eq "failed" `
            -and $retried.workflowVersion -eq 5 `
            -and $restarted.workflowVersion -eq 6 `
            -and $released.workflowVersion -eq 7 `
            -and $released.state -eq "available" `
            -and -not $released.antiMalwareVerified
    ) @{
        versions = @(
            $classified.workflowVersion,
            $quarantined.workflowVersion,
            $started.workflowVersion,
            $failed.workflowVersion,
            $retried.workflowVersion,
            $restarted.workflowVersion,
            $released.workflowVersion
        )
        staleStatus = $staleStatus
        finalState = $released.state
        antiMalwareVerified = $released.antiMalwareVerified
    }

    $history = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/records/$intakeId/history" `
        -Headers $headers `
        -TimeoutSec 20
    $documentsAfter = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001?includeArchived=true" `
        -Headers $headers `
        -TimeoutSec 20
    $ordinaryAfter = @($documentsAfter.documents | Where-Object {
        $_.id -eq $released.documentId -and $_.name -eq $marker
    })
    $historyActions = @($history.events.action) -join ","
    Add-Check "Release and immutable history" (
        $ordinaryAfter.Count -eq 1 `
            -and $history.eventCount -eq 8 `
            -and $historyActions -eq "release,start,retry,fail,start,quarantine,reclassified,captured"
    ) @{
        documentId = $released.documentId
        ordinaryDocuments = $ordinaryAfter.Count
        eventCount = $history.eventCount
        actions = $historyActions
    }
}
catch {
    Add-Check "Managed record focused workflow" $false $_.Exception.Message
}
finally {
    if ($intakeId -and $headers) {
        try {
            $cleanupStatus = Get-HttpStatus `
                -Uri "$ApiBaseUrl/api/records/$intakeId/test-fixture" `
                -Method Delete `
                -RequestHeaders $headers
            $listAfterCleanup = Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/records/?patientId=MOD-PAT-0001" `
                -Headers $headers `
                -TimeoutSec 20
            $documentsAfterCleanup = Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001?includeArchived=true" `
                -Headers $headers `
                -TimeoutSec 20
            $managedResidue = @($listAfterCleanup.items | Where-Object { $_.intakeId -eq $intakeId }).Count
            $documentResidue = @($documentsAfterCleanup.documents | Where-Object { $_.name -eq $marker }).Count
            Add-Check "Owned fixture cleanup" (
                $cleanupStatus -eq 204 `
                    -and $managedResidue -eq 0 `
                    -and $documentResidue -eq 0
            ) @{
                cleanupStatus = $cleanupStatus
                managedResidue = $managedResidue
                documentResidue = $documentResidue
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
Write-Host "Managed record foundation test result: $ResultPath"

if ($status -ne "passed") {
    exit 1
}
