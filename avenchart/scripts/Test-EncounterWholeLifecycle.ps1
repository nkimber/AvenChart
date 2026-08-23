# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001",
    [string]$PatientId = "MOD-PAT-0004",
    [switch]$IncludeBrowser
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$avenChartUiRoot = Resolve-Path (Join-Path $solutionRoot "..\avenchart-ui")
$checks = [System.Collections.Generic.List[object]]::new()
$marker = "TMP-ENC-LIFECYCLE-$(New-Guid)"
$headers = $null
$encounter = $null
$documentId = $null
$billingLineIds = [System.Collections.Generic.List[string]]::new()
$procedureOrderIds = [System.Collections.Generic.List[int]]::new()

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
        TimeoutSec = 30
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 12
    }
    return Invoke-RestMethod @parameters
}

function Get-HttpStatus([System.Management.Automation.ErrorRecord]$ErrorRecord) {
    if ($null -eq $ErrorRecord.Exception.Response) {
        return 0
    }
    return [int]$ErrorRecord.Exception.Response.StatusCode
}

function Invoke-StatusRequest(
    [string]$Uri,
    [string]$Method,
    [object]$Body = $null,
    [hashtable]$RequestHeaders = $null
) {
    $parameters = @{
        Uri = $Uri
        Method = $Method
        TimeoutSec = 30
    }
    if ($null -ne $RequestHeaders) {
        $parameters.Headers = $RequestHeaders
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 12
    }
    try {
        $response = Invoke-WebRequest @parameters
        return [int]$response.StatusCode
    }
    catch {
        return Get-HttpStatus $_
    }
}

function Invoke-FixtureSql([string]$Sql) {
    Push-Location $solutionRoot
    try {
        & docker compose exec -T postgres psql `
            -X `
            -U avenchart `
            -d avenchart `
            -v ON_ERROR_STOP=1 `
            -c $Sql | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Fixture SQL failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-BrowserProof {
    Push-Location $avenChartUiRoot
    try {
        & npx playwright test e2e/encounter-whole-lifecycle.spec.ts --workers=1 | Out-Host
        return $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}

function Get-EncounterDetail {
    return Invoke-JsonRequest "$ApiBaseUrl/api/encounters/$encounter`?includeArchivedDocuments=true"
}

function Get-ProjectedDocument([object]$Detail) {
    return @($Detail.documents | Where-Object { $_.id -eq [int]$documentId }) | Select-Object -First 1
}

try {
    if ($marker -notmatch '^TMP-ENC-LIFECYCLE-[0-9a-f-]+$') {
        throw "Unsafe encounter lifecycle marker."
    }
    Invoke-FixtureSql @"
delete from clinical_notes
where encounter not in (select encounter from encounters)
  and concat_ws(' ', subjective, objective, assessment, plan) like 'TMP-ENC-LIFECYCLE-%';
delete from encounter_audit_events
where encounter not in (select encounter from encounters)
  and changed_fields like '%TMP-ENC-LIFECYCLE-%';
"@

    $admin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body '{"username":"admin","password":"pass"}'
    $frontDesk = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body '{"username":"gold-frontdesk-01","password":"pass"}'
    if (-not $admin.authenticated -or -not $frontDesk.authenticated) {
        throw "Synthetic administrator and front-desk sessions are required."
    }
    $headers = New-AvenChartStaffAccessContextHeaders -Login $admin
    $frontDeskHeaders = New-AvenChartStaffAccessContextHeaders -Login $frontDesk

    $unauthenticatedStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/encounters" `
        "Post" `
        @{ patientId = $PatientId; dateTime = "2026-07-29 10:00:00"; reason = "$marker unauthorized" }
    $frontDeskStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/encounters" `
        "Post" `
        @{ patientId = $PatientId; dateTime = "2026-07-29 10:00:00"; reason = "$marker forbidden" } `
        $frontDeskHeaders
    Add-Check `
        "Encounter lifecycle rejects missing and insufficient staff authority" `
        ($unauthenticatedStatus -eq 401 -and $frontDeskStatus -eq 403) `
        @{ unauthenticated=$unauthenticatedStatus; frontDesk=$frontDeskStatus }

    $schedulingOptions = Invoke-JsonRequest "$ApiBaseUrl/api/appointments/scheduling-options"
    $provider = @($schedulingOptions.providers) | Select-Object -First 1
    $facility = @($schedulingOptions.facilities) | Select-Object -First 1
    if ($null -eq $provider -or $null -eq $facility) {
        throw "Provider and facility scheduling options are required."
    }
    $categoryOptions = Invoke-JsonRequest "$ApiBaseUrl/api/documents/category-options"
    $category = @($categoryOptions.categories) | Select-Object -First 1
    if ($null -eq $category) {
        throw "A document filing category is required."
    }
    $orderCatalog = Invoke-JsonRequest "$ApiBaseUrl/api/procedures/order-catalog"
    $catalogItem = @(
        $orderCatalog.items |
            Where-Object { $_.active -and $_.itemType -eq "ord" -and $_.code }
    ) | Select-Object -First 1
    if ($null -eq $catalogItem) {
        throw "An active procedure catalog item is required."
    }

    $today = (Get-Date).ToString("yyyy-MM-dd")
    $created = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters" `
        "Post" `
        @{
            patientId = $PatientId
            providerId = $provider.id
            dateTime = "$today 10:45:00"
            reason = "$marker complete package"
            facilityId = $facility.id
            billingFacilityId = $facility.id
            sensitivity = "standard"
            referralSource = "$marker referral"
            externalId = "$marker external"
            posCode = 11
            billingNote = "$marker billing"
        }
    $encounter = [int]$created.encounter
    $initialArchiveVersion = [int]$created.archiveVersion
    Add-Check `
        "Encounter package starts from complete visit metadata" `
        ($encounter -gt 0 `
            -and $created.patientId -eq $PatientId `
            -and $created.providerName `
            -and $created.facilityName `
            -and $created.posCode -eq 11 `
            -and $initialArchiveVersion -ge 0) `
        @{ encounter=$encounter; provider=$created.providerName; facility=$created.facilityName; archiveVersion=$created.archiveVersion }

    $soapOne = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$encounter/soap-notes" `
        "Post" `
        @{
            dateTime = "$today 10:50:00"
            expectedVersion = 0
            subjective = "$marker initial subjective"
            objective = "$marker initial objective"
            assessment = "$marker initial assessment"
            plan = "$marker initial plan"
        }
    $soapTwo = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$encounter/soap-notes" `
        "Post" `
        @{
            dateTime = "$today 10:55:00"
            expectedVersion = 1
            subjective = "$marker reviewed subjective"
            objective = "$marker reviewed objective"
            assessment = "$marker reviewed assessment"
            plan = "$marker reviewed plan"
        }
    $staleSoapStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/encounters/$encounter/soap-notes" `
        "Post" `
        @{
            dateTime = "$today 10:56:00"
            expectedVersion = 1
            subjective = "$marker stale SOAP"
        } `
        $headers
    Add-Check `
        "SOAP content is append-only and rejects a stale loaded version" `
        ($soapOne.detail.soapNote.version -eq 1 `
            -and $soapTwo.detail.soapNote.version -eq 2 `
            -and @($soapTwo.detail.soapNote.versions).Count -eq 2 `
            -and $staleSoapStatus -eq 409) `
        @{ firstVersion=$soapOne.detail.soapNote.version; currentVersion=$soapTwo.detail.soapNote.version; staleStatus=$staleSoapStatus }

    $diagnosis = Invoke-JsonRequest `
        "$ApiBaseUrl/api/billing/lines" `
        "Post" `
        @{
            patientId = $PatientId
            providerId = $provider.id
            encounter = $encounter
            billingDate = $today
            codeType = "ICD10"
            code = "Z71.89"
            codeText = "$marker diagnosis"
            fee = 0
            units = 1
            justify = ""
        }
    $billingLineIds.Add([string]$diagnosis.id)
    $charge = Invoke-JsonRequest `
        "$ApiBaseUrl/api/billing/lines" `
        "Post" `
        @{
            patientId = $PatientId
            providerId = $provider.id
            encounter = $encounter
            billingDate = $today
            codeType = "CPT"
            code = "99213"
            modifier = "25"
            codeText = "$marker charge"
            fee = 150
            units = 1
            justify = "Z71.89"
        }
    $billingLineIds.Add([string]$charge.id)
    $order = Invoke-JsonRequest `
        "$ApiBaseUrl/api/procedures/orders" `
        "Post" `
        @{
            patientId = $PatientId
            providerId = $provider.id
            labId = $catalogItem.labId
            encounterId = $encounter
            dateOrdered = $today
            priority = "urgent"
            status = "pending"
            procedureCode = $catalogItem.code
            procedureName = $catalogItem.name
            procedureType = if ($catalogItem.procedureTypeName) { $catalogItem.procedureTypeName } else { "laboratory" }
            diagnosis = "Z71.89"
            instructions = "$marker procedure"
        }
    $procedureOrderIds.Add([int]$order.id)

    $document = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$encounter/documents" `
        "Post" `
        @{
            categoryId = $category.id
            name = "$marker attachment"
            docDate = $today
            content = "$marker original attachment"
            notes = "$marker filing evidence"
        }
    $documentId = [int]$document.id
    Invoke-JsonRequest `
        "$ApiBaseUrl/api/documents/$documentId/content" `
        "Put" `
        @{
            fileName = "$marker.txt"
            content = "$marker corrected attachment"
            reason = "$marker content correction"
            expectedVersion = 1
        } | Out-Null
    $staleDocumentStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/documents/$documentId/content" `
        "Put" `
        @{
            fileName = "$marker-stale.txt"
            content = "$marker stale attachment"
            reason = "$marker stale content correction"
            expectedVersion = 1
        } `
        $headers
    Invoke-JsonRequest `
        "$ApiBaseUrl/api/documents/$documentId/sign" `
        "Put" `
        @{
            reviewStatus = "approved"
            reason = "$marker reviewed attachment"
            expectedReviewStatus = "pending"
        } | Out-Null
    $contentDetail = Get-EncounterDetail
    $projectedDocument = Get-ProjectedDocument $contentDetail
    $projectedDiagnosis = @($contentDetail.diagnosisCodes | Where-Object { $_.code -eq "Z71.89" }) | Select-Object -First 1
    Add-Check `
        "Content, attachment, coding, and order projections reconcile before sign-off" `
        ($contentDetail.soapNote.version -eq 2 `
            -and $projectedDocument.currentVersion -eq 2 `
            -and $projectedDocument.reviewStatus -eq "approved" `
            -and $projectedDocument.reviewedBy -eq "admin" `
            -and $staleDocumentStatus -eq 409 `
            -and $projectedDiagnosis.billingLineCount -eq 2 `
            -and $projectedDiagnosis.procedureOrderCount -eq 1 `
            -and @($contentDetail.billingLines | Where-Object { $_.id -eq [string]$charge.id }).Count -eq 1 `
            -and @($contentDetail.procedureOrders | Where-Object { $_.id -eq [int]$order.id }).Count -eq 1) `
        @{
            soapVersion=$contentDetail.soapNote.version
            documentVersion=$projectedDocument.currentVersion
            documentReview=$projectedDocument.reviewStatus
            staleDocumentStatus=$staleDocumentStatus
            diagnosis=$projectedDiagnosis
        }

    $frontDeskSignStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/encounters/$encounter/sign" `
        "Put" `
        @{ isLock = $false; amendment = $null } `
        $frontDeskHeaders
    $primary = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$encounter/sign" `
        "Put" `
        @{
            isLock = $false
            amendment = $null
            signerUsername = "gold-provider-02"
            signedAt = "1999-01-01T00:00:00"
        }
    $amendment = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$encounter/sign" `
        "Put" `
        @{
            isLock = $true
            amendment = "$marker signed correction"
        }
    $signedDetail = $amendment.detail
    $primarySignature = @($signedDetail.signatures | Where-Object { $_.id -eq [int]$primary.id }) | Select-Object -First 1
    $amendmentSignature = @($signedDetail.signatures | Where-Object { $_.id -eq [int]$amendment.id }) | Select-Object -First 1
    Add-Check `
        "Authenticated immutable signature and amendment evidence lock direct SOAP changes" `
        ($frontDeskSignStatus -eq 403 `
            -and $primarySignature.signerUsername -eq "admin" `
            -and ([datetime]$primarySignature.signedAt).Year -ge 2026 `
            -and $amendmentSignature.signerUsername -eq "admin" `
            -and $amendmentSignature.isLock `
            -and $amendmentSignature.amendment -eq "$marker signed correction" `
            -and @($signedDetail.amendmentHistory).Count -eq 1 `
            -and $signedDetail.soapNote.isLocked) `
        @{
            frontDeskStatus=$frontDeskSignStatus
            primarySigner=$primarySignature.signerUsername
            primarySignedAt=$primarySignature.signedAt
            amendment=$amendmentSignature
        }

    $lockedSoapStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/encounters/$encounter/soap-notes" `
        "Post" `
        @{
            dateTime = "$today 11:05:00"
            expectedVersion = 2
            assessment = "$marker forbidden signed overwrite"
        } `
        $headers
    $signatureDeleteStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/encounters/$encounter/signatures/$($primary.id)" `
        "Delete" `
        $null `
        $headers
    Add-Check `
        "Signed evidence cannot be overwritten or permanently deleted" `
        ($lockedSoapStatus -eq 409 -and $signatureDeleteStatus -in @(404, 405)) `
        @{ lockedSoapStatus=$lockedSoapStatus; retiredDeleteStatus=$signatureDeleteStatus }

    $archiveReason = "$marker archive after package review"
    Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$encounter/archive" `
        "Put" `
        @{ reason = $archiveReason; expectedArchiveVersion = $initialArchiveVersion } | Out-Null
    $staleArchiveStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/encounters/$encounter/archive" `
        "Put" `
        @{ reason = "$marker duplicate stale archive"; expectedArchiveVersion = $initialArchiveVersion } `
        $headers
    $archivedDetail = Get-EncounterDetail
    $activeSearch = Invoke-JsonRequest "$ApiBaseUrl/api/encounters?patientId=$PatientId&from=1900-01-01&limit=50&archived=false"
    $archivedSearch = Invoke-JsonRequest "$ApiBaseUrl/api/encounters?patientId=$PatientId&from=1900-01-01&limit=50&archived=true"
    Add-Check `
        "Reasoned archive is version-safe, discoverable, and preserves the package" `
        ($staleArchiveStatus -eq 409 `
            -and $archivedDetail.archiveVersion -eq ($initialArchiveVersion + 1) `
            -and $archivedDetail.archivedAt `
            -and @($activeSearch.encounters | Where-Object { $_.encounter -eq $encounter }).Count -eq 0 `
            -and @($archivedSearch.encounters | Where-Object { $_.encounter -eq $encounter }).Count -eq 1 `
            -and $archivedDetail.soapNote.version -eq 2 `
            -and @($archivedDetail.signatures).Count -eq 2 `
            -and @($archivedDetail.documents | Where-Object { $_.id -eq [int]$documentId }).Count -eq 1 `
            -and @($archivedDetail.billingLines | Where-Object { $_.id -eq [string]$charge.id }).Count -eq 1 `
            -and @($archivedDetail.procedureOrders | Where-Object { $_.id -eq [int]$order.id }).Count -eq 1) `
        @{
            staleStatus=$staleArchiveStatus
            archiveVersion=$archivedDetail.archiveVersion
            archivedAt=$archivedDetail.archivedAt
            activeMatches=@($activeSearch.encounters | Where-Object { $_.encounter -eq $encounter }).Count
            archivedMatches=@($archivedSearch.encounters | Where-Object { $_.encounter -eq $encounter }).Count
        }

    $staleRestoreStatus = Invoke-StatusRequest `
        "$ApiBaseUrl/api/encounters/$encounter/restore" `
        "Put" `
        @{ reason = "$marker stale restore"; expectedArchiveVersion = $initialArchiveVersion } `
        $headers
    $restoreReason = "$marker restore after package review"
    Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters/$encounter/restore" `
        "Put" `
        @{ reason = $restoreReason; expectedArchiveVersion = ($initialArchiveVersion + 1) } | Out-Null
    $restoredDetail = Get-EncounterDetail
    $audit = Invoke-JsonRequest "$ApiBaseUrl/api/encounters/$encounter/audit"
    $restoredSearch = Invoke-JsonRequest "$ApiBaseUrl/api/encounters?patientId=$PatientId&from=1900-01-01&limit=50&archived=false"
    $archiveEvent = @($audit.events | Where-Object { $_.action -eq "archived" }) | Select-Object -First 1
    $restoreEvent = @($audit.events | Where-Object { $_.action -eq "restored" }) | Select-Object -First 1
    Add-Check `
        "Restore rejects a stale version and retains accountable archive history" `
        ($staleRestoreStatus -eq 409 `
            -and $restoredDetail.archiveVersion -eq ($initialArchiveVersion + 2) `
            -and -not $restoredDetail.archivedAt `
            -and @($restoredSearch.encounters | Where-Object { $_.encounter -eq $encounter }).Count -eq 1 `
            -and $archiveEvent.username -eq "admin" `
            -and @($archiveEvent.changedFields) -contains "reason:$archiveReason" `
            -and $restoreEvent.username -eq "admin" `
            -and @($restoreEvent.changedFields) -contains "reason:$restoreReason" `
            -and $restoredDetail.soapNote.version -eq 2 `
            -and @($restoredDetail.signatures).Count -eq 2 `
            -and @($restoredDetail.documents | Where-Object { $_.id -eq [int]$documentId }).Count -eq 1 `
            -and @($restoredDetail.billingLines | Where-Object { $_.id -eq [string]$charge.id }).Count -eq 1 `
            -and @($restoredDetail.procedureOrders | Where-Object { $_.id -eq [int]$order.id }).Count -eq 1) `
        @{
            staleStatus=$staleRestoreStatus
            archiveVersion=$restoredDetail.archiveVersion
            archiveEvent=$archiveEvent
            restoreEvent=$restoreEvent
        }

    if ($IncludeBrowser) {
        $browserExitCode = Invoke-BrowserProof
        Add-Check `
            "Whole package lifecycle passes configured AvenChart UI browser profiles" `
            ($browserExitCode -eq 0) `
            @{ exitCode=$browserExitCode }
    }
}
finally {
    foreach ($billingLineId in $billingLineIds) {
        try {
            Invoke-JsonRequest "$ApiBaseUrl/api/billing/lines/$billingLineId" "Delete" | Out-Null
        }
        catch {
            Write-Warning "Could not delete billing-line fixture $billingLineId."
        }
    }
    foreach ($procedureOrderId in $procedureOrderIds) {
        try {
            Invoke-JsonRequest "$ApiBaseUrl/api/procedures/orders/$procedureOrderId" "Delete" | Out-Null
        }
        catch {
            Write-Warning "Could not delete procedure-order fixture $procedureOrderId."
        }
    }
    if ($null -ne $documentId -and [int]$documentId -gt 0) {
        try {
            Invoke-JsonRequest "$ApiBaseUrl/api/documents/$documentId" "Delete" | Out-Null
        }
        catch {
            Write-Warning "Could not delete document fixture $documentId through the API."
        }
    }
    if ($null -ne $encounter -and [int]$encounter -gt 0) {
        $safeEncounter = [int]$encounter
        $safeMarker = $marker.Replace("'", "''")
        Invoke-FixtureSql @"
delete from patient_documents where encounter = $safeEncounter and name like '$safeMarker%';
delete from clinical_notes
where encounter = $safeEncounter
  and concat_ws(' ', subjective, objective, assessment, plan) like '$safeMarker%';
delete from encounter_audit_events where encounter = $safeEncounter;
delete from encounters where encounter = $safeEncounter and reason = '$safeMarker complete package';
"@
    }

    $residue = 0
    if ($null -ne $encounter -and [int]$encounter -gt 0) {
        $safeEncounter = [int]$encounter
        Push-Location $solutionRoot
        try {
            $residue = [int]((& docker compose exec -T postgres psql `
                -X `
                -U avenchart `
                -d avenchart `
                -Atc "select count(*) from encounters where encounter = $safeEncounter;").Trim())
        }
        finally {
            Pop-Location
        }
    }
    Add-Check `
        "Whole-lifecycle fixtures leave no database residue" `
        ($residue -eq 0) `
        @{ encounter=$encounter; residue=$residue }
}

$failed = @($checks | Where-Object { $_.status -eq "failed" })
[ordered]@{
    script = "Test-EncounterWholeLifecycle.ps1"
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    checkCount = $checks.Count
    failedCount = $failed.Count
    checks = $checks
} | ConvertTo-Json -Depth 14

if ($failed.Count -gt 0) {
    exit 1
}
