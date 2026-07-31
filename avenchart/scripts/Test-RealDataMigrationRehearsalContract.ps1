# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ContractPath = (Join-Path $PSScriptRoot '..\migration-contracts\real-data-rehearsal-contract.json')
)

$ErrorActionPreference = 'Stop'

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts\migration-contracts'
$resultPath = Join-Path $artifactsRoot 'latest-real-data-rehearsal-contract-validation.json'
$requiredFamilies = @('patients', 'identifiers', 'encounters', 'documents', 'financial-history', 'audit-history', 'configuration', 'active-integrations')
$requiredApprovalArtifacts = @(
    'source-inventory-and-classification',
    'retention-and-legal-hold-requirements',
    'source-to-target-mapping-approval',
    'identity-and-duplicate-resolution-rules',
    'document-storage-and-security-rules',
    'extract-transform-load-runbook',
    'reconciliation-and-quarantine-runbook',
    'rollback-backup-and-restore-runbook',
    'cutover-and-coexistence-runbook',
    'representative-rehearsal-owner-signoff'
)
$checks = New-Object System.Collections.Generic.List[object]
$status = 'passed'

function Add-Check {
    param([string]$Name, [string]$Result, [object]$Details = $null)

    $script:checks.Add([ordered]@{ name = $Name; status = $Result; details = $Details })
    if ($Result -eq 'failed') { $script:status = 'failed' }
}

try {
    if (-not (Test-Path -LiteralPath $ContractPath -PathType Leaf)) {
        throw "Migration rehearsal contract was not found at '$ContractPath'."
    }

    $rawContract = Get-Content -LiteralPath $ContractPath -Raw
    $contract = $rawContract | ConvertFrom-Json
    $identityPassed = $contract.contractId -eq 'legacy-ehr-real-data-rehearsal-v1' `
        -and $contract.contractVersion -eq 1 `
        -and $contract.status -eq 'owner-approval-required' `
        -and $contract.source.system -eq 'legacy-legacy-ehr' `
        -and $contract.source.database -eq 'MariaDB' `
        -and $contract.target.system -eq 'avenchart' `
        -and $contract.target.database -eq 'PostgreSQL'
    Add-Check -Name 'Contract identity and systems' -Result $(if ($identityPassed) { 'passed' } else { 'failed' }) -Details @{
        contractId = $contract.contractId
        version = $contract.contractVersion
        status = $contract.status
        source = "$($contract.source.system)/$($contract.source.database)"
        target = "$($contract.target.system)/$($contract.target.database)"
    }

    $familyKeys = @($contract.mappingFamilies | ForEach-Object { $_.key })
    $missingFamilies = @($requiredFamilies | Where-Object { $_ -notin $familyKeys })
    $duplicateFamilies = @($familyKeys | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
    $incompleteFamilies = @($contract.mappingFamilies | Where-Object {
        $_.classification -notin @('PHI', 'restricted') `
            -or [string]::IsNullOrWhiteSpace($_.sourceEvidence) `
            -or [string]::IsNullOrWhiteSpace($_.targetOutcome) `
            -or $_.mappingStatus -ne 'owner-approval-required' `
            -or @($_.reconciliation).Count -eq 0
    } | ForEach-Object key)
    $familiesPassed = $missingFamilies.Count -eq 0 -and $duplicateFamilies.Count -eq 0 -and $incompleteFamilies.Count -eq 0
    Add-Check -Name 'Required mapping families' -Result $(if ($familiesPassed) { 'passed' } else { 'failed' }) -Details @{
        mapped = $familyKeys
        missing = $missingFamilies
        duplicate = $duplicateFamilies
        incomplete = $incompleteFamilies
    }

    $approvalArtifacts = @($contract.requiredApprovalArtifacts)
    $missingApprovalArtifacts = @($requiredApprovalArtifacts | Where-Object { $_ -notin $approvalArtifacts })
    Add-Check -Name 'Owner approval prerequisites' -Result $(if ($missingApprovalArtifacts.Count -eq 0) { 'passed' } else { 'failed' }) -Details @{
        required = $approvalArtifacts
        missing = $missingApprovalArtifacts
    }

    $guardText = ($contract.executionGuards -join "`n")
    $guardPassed = $contract.dataAccessAuthorized -eq $false `
        -and $guardText -match 'No real source data may be accessed' `
        -and $guardText -match 'Secrets, credentials, and PHI must not appear'
    Add-Check -Name 'No-data execution guard' -Result $(if ($guardPassed) { 'passed' } else { 'failed' }) -Details @{
        dataAccessAuthorized = $contract.dataAccessAuthorized
        contractStatus = $contract.status
        guardCount = @($contract.executionGuards).Count
    }
}
catch {
    Add-Check -Name 'Migration rehearsal contract validation' -Result 'failed' -Details $_.Exception.Message
}
finally {
    New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
    [ordered]@{
        status = $status
        completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        contractPath = $ContractPath
        checks = $checks
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
}

Write-Host "Real-data migration rehearsal contract validation: $resultPath"
if ($status -ne 'passed') { exit 1 }
