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
$resultPath = Join-Path $artifactsRoot "latest-legacy-clinical-form-display-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"
$headers = $null
$providerHeaders = $null
$manifestId = "90f00000-0000-4000-a000-000000000001"

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
            $request.Content = [System.Net.Http.StringContent]::new(
                ($Body | ConvertTo-Json -Depth 20),
                [System.Text.Encoding]::UTF8,
                "application/json"
            )
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
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login
    $providerLogin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{
            username = "gold-provider-01"
            password = "pass"
        } | ConvertTo-Json) `
        -TimeoutSec 20
    if (-not $providerLogin.authenticated -or
        [string]::IsNullOrWhiteSpace($providerLogin.sessionId)) {
        throw "Provider login did not issue an active session."
    }
    $providerHeaders = New-AvenChartStaffAccessContextHeaders -Login $providerLogin

    $unauthenticated = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/legacy-snapshots"
    Add-Check "Legacy snapshots require authentication" ($unauthenticated -eq 401) @{
        status = $unauthenticated
    }

    $unauthenticatedManifest = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/legacy-migration-manifests/legacy.clinicnote"
    Add-Check "Migration manifest requires authentication" (
        $unauthenticatedManifest -eq 401
    ) @{
        status = $unauthenticatedManifest
    }

    $unauthenticatedDecision = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/review" `
        -Method Post `
        -Body @{
            expectedVersion = 1
            reason = "Unauthenticated review must be rejected."
        }
    Add-Check "Manifest decisions require authentication" (
        $unauthenticatedDecision -eq 401
    ) @{
        status = $unauthenticatedDecision
    }

    $list = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/legacy-snapshots" `
        -Headers $headers `
        -TimeoutSec 20
    $mappedSummary = @($list.snapshots | Where-Object sourceRowId -eq "880001")[0]
    $unmappedSummary = @($list.snapshots | Where-Object sourceRowId -eq "880002")[0]
    $instructionSummaries = @(
        $list.snapshots |
            Where-Object stableKey -eq "legacy.clinicalinstructions"
    )
    $soapSummaries = @(
        $list.snapshots |
            Where-Object stableKey -eq "legacy.soap"
    )
    Add-Check "Bounded patient snapshot list" (
        $list.total -eq 6 `
        -and $list.returned -eq 6 `
        -and $list.limit -eq 100 `
        -and $mappedSummary.readOnly `
        -and -not $mappedSummary.converted `
        -and $mappedSummary.unmappedCount -eq 0 `
        -and $unmappedSummary.unmappedCount -eq 1 `
        -and $instructionSummaries.Count -eq 2 `
        -and (@($instructionSummaries | Where-Object {
            -not $_.readOnly -or $_.converted -or $_.unmappedCount -ne 0
        }).Count -eq 0) `
        -and $soapSummaries.Count -eq 2 `
        -and (@($soapSummaries | Where-Object {
            -not $_.readOnly -or $_.converted -or $_.unmappedCount -ne 0
        }).Count -eq 0)
    ) $list

    $emptyList = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0002/legacy-snapshots" `
        -Headers $headers `
        -TimeoutSec 20
    Add-Check "Patient filtering does not leak snapshots" (
        $emptyList.total -eq 0 -and @($emptyList.snapshots).Count -eq 0
    ) $emptyList

    $manifest = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/legacy-migration-manifests/legacy.clinicnote" `
        -Headers $headers `
        -TimeoutSec 20
    $eligibleDisposition = @(
        $manifest.reconciliation.rows |
            Where-Object sourceRowId -eq "880001"
    )[0]
    $blockedDisposition = @(
        $manifest.reconciliation.rows |
            Where-Object sourceRowId -eq "880002"
    )[0]
    Add-Check "Draft migration manifest and reconciliation remain non-executing" (
        $manifest.manifest.status -eq "draft" `
        -and $manifest.manifest.manifestRevision -eq 1 `
        -and $manifest.manifest.version -eq 1 `
        -and $manifest.manifest.contract.contractRevision -eq "local-clinical-form-migration-manifest-v1" `
        -and @($manifest.manifest.contract.mappingRules).Count -eq 5 `
        -and @($manifest.manifest.contract.changedSemantics).Count -eq 3 `
        -and @($manifest.manifest.contract.errorDisposition).Count -eq 4 `
        -and @($manifest.manifest.contract.compensationRollback).Count -eq 4 `
        -and @($manifest.manifest.contract.requiredApprovals).Count -eq 5 `
        -and @($manifest.manifest.blockers).Count -eq 4 `
        -and $manifest.manifest.manifestSha256 -match "^[0-9a-f]{64}$" `
        -and -not $manifest.manifest.productionApproved `
        -and -not $manifest.manifest.executionEnabled `
        -and $manifest.reconciliation.sourceRows -eq 2 `
        -and $manifest.reconciliation.activeRows -eq 1 `
        -and $manifest.reconciliation.inactiveRows -eq 1 `
        -and $manifest.reconciliation.fullyMappedRows -eq 1 `
        -and $manifest.reconciliation.rowsWithUnmappedFacts -eq 1 `
        -and $manifest.reconciliation.eligibleRows -eq 1 `
        -and $manifest.reconciliation.blockedRows -eq 1 `
        -and $manifest.reconciliation.governedInstancesCreated -eq 0 `
        -and $manifest.reconciliation.sourceSnapshotDigest -match "^[0-9a-f]{64}$" `
        -and $eligibleDisposition.disposition -eq "eligible-for-review" `
        -and $blockedDisposition.disposition -eq "blocked" `
        -and $blockedDisposition.unmappedCount -eq 1 `
        -and @($manifest.events).Count -eq 1 `
        -and $manifest.events[0].action -eq "created" `
        -and $manifest.events[0].snapshotSha256 -match "^[0-9a-f]{64}$" `
        -and @($manifest.allowedActions).Count -eq 1 `
        -and $manifest.allowedActions[0] -eq "review"
    ) $manifest

    $adminReview = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/review" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{
            expectedVersion = 1
            reason = "Administrator review for separation-of-duties verification."
        } | ConvertTo-Json) `
        -TimeoutSec 20
    $sameActorApproval = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/approve" `
        -Method Post `
        -RequestHeaders $headers `
        -Body @{
            expectedVersion = 2
            reason = "The same reviewer must not approve this manifest."
        }
    Add-Check "Reviewer cannot approve their own manifest review" (
        $adminReview.status -eq "in-review" `
        -and $adminReview.version -eq 2 `
        -and $sameActorApproval -eq 409
    ) @{
        review = $adminReview
        approvalStatus = $sameActorApproval
    }

    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/test-fixture" `
        -Method Delete `
        -Headers $headers `
        -TimeoutSec 20 | Out-Null
    $providerReviewForRejection = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/review" `
        -Method Post `
        -Headers $providerHeaders `
        -ContentType "application/json" `
        -Body (@{
            expectedVersion = 1
            reason = "Clinical reviewer checked mappings before rejection verification."
        } | ConvertTo-Json) `
        -TimeoutSec 20
    $adminRejection = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/reject" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{
            expectedVersion = 2
            reason = "Reject the local manifest during reversible fixture verification."
        } | ConvertTo-Json) `
        -TimeoutSec 20
    Add-Check "Separate reviewer and administrator rejection is evidenced" (
        $providerReviewForRejection.status -eq "in-review" `
        -and $providerReviewForRejection.decision.actor -eq "gold-provider-01" `
        -and $adminRejection.status -eq "rejected" `
        -and $adminRejection.version -eq 3 `
        -and $adminRejection.decision.actor -eq "admin" `
        -and -not $adminRejection.productionApproved `
        -and -not $adminRejection.executionEnabled
    ) @{
        review = $providerReviewForRejection
        rejection = $adminRejection
    }

    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/test-fixture" `
        -Method Delete `
        -Headers $headers `
        -TimeoutSec 20 | Out-Null
    $providerReview = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/review" `
        -Method Post `
        -Headers $providerHeaders `
        -ContentType "application/json" `
        -Body (@{
            expectedVersion = 1
            reason = "Clinical owner reviewed the bounded mapping and semantic changes."
        } | ConvertTo-Json) `
        -TimeoutSec 20
    $providerApproval = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/approve" `
        -Method Post `
        -RequestHeaders $providerHeaders `
        -Body @{
            expectedVersion = 2
            reason = "A clinician cannot record administrator approval."
        }
    Add-Check "Clinical reviewer cannot exercise administrator approval" (
        $providerReview.status -eq "in-review" `
        -and $providerReview.version -eq 2 `
        -and $providerApproval -eq 403
    ) @{
        review = $providerReview
        providerApprovalStatus = $providerApproval
    }

    $staleApproval = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/approve" `
        -Method Post `
        -RequestHeaders $headers `
        -Body @{
            expectedVersion = 1
            reason = "Stale approval must fail optimistic concurrency."
        }
    Add-Check "Stale manifest approval is rejected" (
        $staleApproval -eq 409
    ) @{
        status = $staleApproval
    }

    $adminApproval = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/approve" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body (@{
            expectedVersion = 2
            reason = "Administrator accepts the local synthetic manifest evidence."
        } | ConvertTo-Json) `
        -TimeoutSec 20
    $approvedManifest = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/legacy-migration-manifests/legacy.clinicnote" `
        -Headers $headers `
        -TimeoutSec 20
    Add-Check "Separate local approval has immutable non-executing evidence" (
        $adminApproval.status -eq "locally-approved" `
        -and $adminApproval.version -eq 3 `
        -and $adminApproval.decision.actor -eq "admin" `
        -and -not $adminApproval.productionApproved `
        -and -not $adminApproval.executionEnabled `
        -and $approvedManifest.manifest.status -eq "locally-approved" `
        -and $approvedManifest.manifest.version -eq 3 `
        -and $approvedManifest.manifest.reviewedBy -eq "gold-provider-01" `
        -and $approvedManifest.manifest.approvedBy -eq "admin" `
        -and -not $approvedManifest.manifest.productionApproved `
        -and -not $approvedManifest.manifest.executionEnabled `
        -and $approvedManifest.reconciliation.governedInstancesCreated -eq 0 `
        -and @($approvedManifest.events).Count -eq 3 `
        -and (@($approvedManifest.events | Where-Object action -eq "review").Count -eq 1) `
        -and (@($approvedManifest.events | Where-Object action -eq "approve").Count -eq 1) `
        -and (@($approvedManifest.events | Where-Object {
            $_.snapshotSha256 -notmatch "^[0-9a-f]{64}$"
        }).Count -eq 0) `
        -and @($approvedManifest.allowedActions).Count -eq 0
    ) $approvedManifest

    $terminalRejection = Get-HttpStatus `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/reject" `
        -Method Post `
        -RequestHeaders $headers `
        -Body @{
            expectedVersion = 3
            reason = "An approved local manifest cannot be silently rejected."
        }
    Add-Check "Local approval cannot be silently overwritten" (
        $terminalRejection -eq 409
    ) @{
        status = $terminalRejection
    }

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

    $instructions = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-snapshots/90f00000-0000-4000-9000-000000000003" `
        -Headers $headers `
        -TimeoutSec 20
    $instruction = @(
        $instructions.fields | Where-Object sourceField -eq "instruction"
    )[0]
    Add-Check "Mapped Clinical Instructions display evidence" (
        $instructions.snapshot.stableKey -eq "legacy.clinicalinstructions" `
        -and $instructions.snapshot.sourceTable -eq "form_clinical_instructions" `
        -and $instructions.snapshot.adapterRevision -eq "local-legacy-clinical-instructions-display-v1" `
        -and $instructions.snapshot.targetDefinitionRevision -eq 1 `
        -and $instructions.snapshot.targetSchemaHash -match "^[0-9a-f]{64}$" `
        -and $instructions.snapshot.rawSha256 -match "^[0-9a-f]{64}$" `
        -and @($instructions.fields).Count -eq 1 `
        -and $instruction.targetField -eq "instruction" `
        -and $instruction.mappingState -eq "exact" `
        -and $instruction.displayValue -match "^Continue the current regimen" `
        -and @($instructions.unmappedFacts).Count -eq 0 `
        -and -not $instructions.migrationApproved `
        -and $null -eq $instructions.governedInstanceId
    ) $instructions

    $soap = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/form-engine/legacy-snapshots/90f00000-0000-4000-9000-000000000005" `
        -Headers $headers `
        -TimeoutSec 20
    $soapFields = @($soap.fields)
    Add-Check "Mapped SOAP display evidence" (
        $soap.snapshot.stableKey -eq "legacy.soap" `
        -and $soap.snapshot.sourceTable -eq "form_soap" `
        -and $soap.snapshot.adapterRevision -eq "local-legacy-soap-display-v1" `
        -and $soap.snapshot.targetDefinitionRevision -eq 1 `
        -and $soap.snapshot.targetSchemaHash -match "^[0-9a-f]{64}$" `
        -and $soap.snapshot.rawSha256 -match "^[0-9a-f]{64}$" `
        -and $soapFields.Count -eq 4 `
        -and (@($soapFields | Where-Object mappingState -ne "exact").Count -eq 0) `
        -and (@($soapFields | Where-Object {
            $_.sourceField -ne $_.targetField
        }).Count -eq 0) `
        -and (@($soapFields.sourceField | Sort-Object) -join ",") -eq "assessment,objective,plan,subjective" `
        -and (@($soap.fields | Where-Object {
            $_.sourceField -eq "plan"
        })[0].displayValue -eq "Continue medications and return in two weeks.") `
        -and @($soap.unmappedFacts).Count -eq 0 `
        -and -not $soap.migrationApproved `
        -and $null -eq $soap.governedInstanceId
    ) $soap

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
    if ($null -ne $headers) {
        try {
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/form-engine/legacy-migration-manifests/$manifestId/test-fixture" `
                -Method Delete `
                -Headers $headers `
                -TimeoutSec 20 | Out-Null
            $resetManifest = Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/form-engine/patients/MOD-PAT-0001/legacy-migration-manifests/legacy.clinicnote" `
                -Headers $headers `
                -TimeoutSec 20
            Add-Check "Manifest fixture cleanup restores the draft boundary" (
                $resetManifest.manifest.status -eq "draft" `
                -and $resetManifest.manifest.version -eq 1 `
                -and $null -eq $resetManifest.manifest.reviewedBy `
                -and $null -eq $resetManifest.manifest.approvedBy `
                -and -not $resetManifest.manifest.productionApproved `
                -and -not $resetManifest.manifest.executionEnabled `
                -and @($resetManifest.events).Count -eq 1 `
                -and @($resetManifest.allowedActions).Count -eq 1 `
                -and $resetManifest.allowedActions[0] -eq "review"
            ) $resetManifest
        }
        catch {
            Add-Check "Manifest fixture cleanup restores the draft boundary" $false @{
                message = $_.Exception.Message
            }
        }
    }

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
