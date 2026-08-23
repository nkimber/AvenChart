# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001",
    [string]$PatientId = "MOD-PAT-0001",
    [int]$Encounter = 1000013,
    [switch]$IncludeBrowser
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$avenChartUiRoot = Resolve-Path (Join-Path $solutionRoot "..\avenchart-ui")
$checks = [System.Collections.Generic.List[object]]::new()
$marker = "TMP-ENC-DOC-$(New-Guid)"
$headers = $null
$documentIds = [System.Collections.Generic.List[int]]::new()

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
}

function Invoke-JsonRequest(
    [string]$Uri,
    [string]$Method = "Get",
    [object]$Body = $null
) {
    $parameters = @{
        Uri = $Uri
        Method = $Method
        Headers = $headers
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 10
    }
    return Invoke-RestMethod @parameters
}

function Get-EncounterDetail {
    return Invoke-JsonRequest "$ApiBaseUrl/api/encounters/$Encounter`?includeArchivedDocuments=true"
}

function Get-MarkerDocument([object]$Detail, [int]$DocumentId) {
    return @($Detail.documents | Where-Object { $_.id -eq $DocumentId }) | Select-Object -First 1
}

function Invoke-BrowserProof {
    $priorPatient = $env:MODERN_UI_DOCUMENT_PATIENT_ID
    $priorEncounter = $env:MODERN_UI_DOCUMENT_ENCOUNTER
    $env:MODERN_UI_DOCUMENT_PATIENT_ID = $PatientId
    $env:MODERN_UI_DOCUMENT_ENCOUNTER = [string]$Encounter
    Push-Location $avenChartUiRoot
    try {
        & npx playwright test e2e/encounter-document-lifecycle.spec.ts --workers=4 | Out-Host
        return $LASTEXITCODE
    }
    finally {
        Pop-Location
        foreach ($entry in @(
            @{ Name = "MODERN_UI_DOCUMENT_PATIENT_ID"; Value = $priorPatient },
            @{ Name = "MODERN_UI_DOCUMENT_ENCOUNTER"; Value = $priorEncounter }
        )) {
            if ($null -eq $entry.Value) {
                Remove-Item "Env:$($entry.Name)" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item "Env:$($entry.Name)" $entry.Value
            }
        }
    }
}

try {
    if ($marker -notmatch '^TMP-ENC-DOC-[0-9a-f-]+$') {
        throw "Unsafe encounter-document marker."
    }

    $admin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body '{"username":"admin","password":"pass"}'
    if (-not $admin.authenticated) {
        throw "The synthetic administrator session was not issued."
    }
    $headers = New-AvenChartStaffAccessContextHeaders -Login $admin

    $baseline = Get-EncounterDetail
    if ($baseline.patientId -ne $PatientId) {
        throw "Encounter $Encounter does not belong to $PatientId."
    }
    $options = Invoke-JsonRequest "$ApiBaseUrl/api/documents/category-options"
    $category = @($options.categories) | Select-Object -First 1
    if ($null -eq $category) {
        throw "No document filing category is available."
    }

    $created = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$Encounter/documents" `
        "Post" `
        @{
            categoryId = $category.id
            name = "$marker protected note"
            docDate = (Get-Date).ToString("yyyy-MM-dd")
            content = "$marker original protected bytes"
            notes = "$marker filing evidence"
        }
    $documentId = [int]$created.id
    $documentIds.Add($documentId)
    $createdDetail = Get-EncounterDetail
    $createdDocument = Get-MarkerDocument $createdDetail $documentId
    Add-Check `
        "Encounter projection exposes the protected version-one filing" `
        ($null -ne $createdDocument `
            -and $createdDocument.currentVersion -eq 1 `
            -and $createdDocument.versionHistoryCount -eq 1 `
            -and $createdDocument.reviewStatus -eq "pending" `
            -and $createdDocument.canDownload) `
        $createdDocument

    Invoke-JsonRequest `
        "$ApiBaseUrl/api/documents/$documentId/content" `
        "Put" `
        @{
            fileName = "$marker.txt"
            content = "$marker replacement protected bytes"
            reason = "$marker correction reason"
            expectedVersion = 1
        } | Out-Null
    $versionedDocument = Get-MarkerDocument (Get-EncounterDetail) $documentId
    Add-Check `
        "Encounter projection adopts the authoritative append-only version" `
        ($versionedDocument.currentVersion -eq 2 `
            -and $versionedDocument.versionHistoryCount -eq 2 `
            -and $versionedDocument.hasPriorVersions `
            -and $versionedDocument.versionLabel -eq "Version 2") `
        $versionedDocument

    Invoke-JsonRequest `
        "$ApiBaseUrl/api/documents/$documentId/sign" `
        "Put" `
        @{
            reviewStatus = "denied"
            reason = "$marker denial reason"
            expectedReviewStatus = "pending"
        } | Out-Null
    $deniedDocument = Get-MarkerDocument (Get-EncounterDetail) $documentId
    Add-Check `
        "Authenticated review denial is visible on the encounter projection" `
        ($deniedDocument.reviewStatus -eq "denied" `
            -and $deniedDocument.reviewedBy -eq "admin" `
            -and @($deniedDocument.lifecycleEvents | Where-Object {
                $_.code -eq "review-denied" -and $_.actor -eq "admin"
            }).Count -eq 1) `
        $deniedDocument

    Invoke-JsonRequest `
        "$ApiBaseUrl/api/documents/$documentId/soft-delete" `
        "Put" `
        @{
            reason = "$marker archive reason"
            expectedArchived = $false
        } | Out-Null
    $archivedDocument = Get-MarkerDocument (Get-EncounterDetail) $documentId
    Add-Check `
        "Reasoned archive remains retrievable through the explicit archived view" `
        ($archivedDocument.deleted -ne 0 `
            -and @($archivedDocument.lifecycleEvents | Where-Object {
                $_.code -eq "archived" -and $_.actor -eq "admin"
            }).Count -eq 1) `
        $archivedDocument

    Invoke-JsonRequest `
        "$ApiBaseUrl/api/documents/$documentId/restore" `
        "Put" `
        @{
            reason = "$marker restore reason"
            expectedArchived = $true
        } | Out-Null
    $restoredDocument = Get-MarkerDocument (Get-EncounterDetail) $documentId
    $archiveHistory = Invoke-JsonRequest "$ApiBaseUrl/api/documents/$documentId/archive-history"
    Add-Check `
        "Restore returns the record to active state and retains both archive events" `
        ($restoredDocument.deleted -eq 0 `
            -and $archiveHistory.eventCount -eq 2 `
            -and @($archiveHistory.events).Count -eq 2) `
        @{
            document = $restoredDocument
            archiveHistory = $archiveHistory
        }

    $external = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$Encounter/documents/external-link" `
        "Post" `
        @{
            categoryId = $category.id
            name = "$marker external link"
            docDate = (Get-Date).ToString("yyyy-MM-dd")
            url = "https://example.com/clinical-reference"
            notes = "$marker link evidence"
        }
    $externalId = [int]$external.id
    $documentIds.Add($externalId)
    $externalDocument = Get-MarkerDocument (Get-EncounterDetail) $externalId
    Add-Check `
        "External-link filing retains its URL and link preview boundary" `
        ($externalDocument.storageMethod -eq "web_url" `
            -and $externalDocument.url -eq "https://example.com/clinical-reference" `
            -and $externalDocument.previewKind -eq "external-link") `
        $externalDocument

    if ($IncludeBrowser) {
        $browserExit = Invoke-BrowserProof
        Add-Check `
            "Desktop, mobile, Firefox, and WebKit encounter-document workflows" `
            ($browserExit -eq 0) `
            @{ exitCode = $browserExit }
    }
}
catch {
    Add-Check "Encounter document lifecycle execution" $false $_.Exception.Message
}
finally {
    if ($null -ne $headers) {
        foreach ($documentId in $documentIds) {
            try {
                Invoke-WebRequest `
                    -UseBasicParsing `
                    -Uri "$ApiBaseUrl/api/documents/$documentId" `
                    -Method Delete `
                    -Headers $headers | Out-Null
            }
            catch {
                Add-Check "Cleanup encounter document $documentId" $false $_.Exception.Message
            }
        }

        try {
            $register = Invoke-JsonRequest "$ApiBaseUrl/api/documents/$PatientId`?includeArchived=true"
            $remaining = @($register.documents | Where-Object { $_.name -like "$marker*" })
            Add-Check `
                "Encounter document lifecycle proof leaves zero marker residue" `
                ($remaining.Count -eq 0) `
                @{ marker = $marker; remaining = $remaining.Count }
        }
        catch {
            Add-Check "Verify encounter document cleanup" $false $_.Exception.Message
        }
    }
}

$failed = @($checks | Where-Object { $_.status -ne "passed" })
$result = [ordered]@{
    status = if ($failed.Count -eq 0) { "passed" } else { "failed" }
    checks = $checks
}
$result | ConvertTo-Json -Depth 15
if ($failed.Count -gt 0) { exit 1 }
