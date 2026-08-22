# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001",
    [switch]$IncludeBrowser
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$marker = "TMP-SOAP-VERSION-$(New-Guid)"
$headers = $null
$fixture = $null
$signatureId = $null
$initialNoteCount = 0

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
}

function Invoke-PostgresScalar([string]$Sql) {
    Push-Location $solutionRoot
    try {
        return (& docker compose exec -T postgres psql -X -U avenchart -d avenchart -Atc $Sql).Trim()
    }
    finally {
        Pop-Location
    }
}

function Get-HttpErrorPayload([System.Management.Automation.ErrorRecord]$ErrorRecord) {
    $content = $ErrorRecord.ErrorDetails.Message
    if (-not $content -and $null -ne $ErrorRecord.Exception.Response) {
        try {
            $stream = $ErrorRecord.Exception.Response.GetResponseStream()
            $reader = [System.IO.StreamReader]::new($stream)
            try {
                $content = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        catch {
            $content = $null
        }
    }
    if (-not $content) {
        return $null
    }
    return $content | ConvertFrom-Json
}

function Save-SoapVersion(
    [int]$Encounter,
    [int]$ExpectedVersion,
    [object]$Current,
    [string]$Subjective,
    [string]$Objective,
    [string]$Assessment,
    [string]$Plan
) {
    $body = @{
        dateTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")
        expectedVersion = $ExpectedVersion
        subjective = $Subjective
        objective = $Objective
        assessment = $Assessment
        plan = $Plan
    } | ConvertTo-Json
    return Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/encounters/$Encounter/soap-notes" `
        -Method Post `
        -Headers $headers `
        -ContentType "application/json" `
        -Body $body
}

function Invoke-BrowserProof([bool]$LockedMode) {
    $avenChartUiRoot = Resolve-Path (Join-Path $solutionRoot "..\avenchart-ui")
    $priorEncounter = $env:MODERN_UI_SOAP_ENCOUNTER
    $priorPatient = $env:MODERN_UI_SOAP_PATIENT_ID
    $priorMarker = $env:MODERN_UI_SOAP_MARKER
    $priorLocked = $env:MODERN_UI_SOAP_LOCKED_MODE
    $env:MODERN_UI_SOAP_ENCOUNTER = [string]$fixture.encounter
    $env:MODERN_UI_SOAP_PATIENT_ID = [string]$fixture.patientId
    $env:MODERN_UI_SOAP_MARKER = $marker
    $env:MODERN_UI_SOAP_LOCKED_MODE = if ($LockedMode) { "1" } else { "0" }
    Push-Location $avenChartUiRoot
    try {
        if ($LockedMode) {
            & npx playwright test e2e/encounter-soap-version-conflict.spec.ts --workers=1 --grep "locking signature" | Out-Host
        }
        else {
            & npx playwright test e2e/encounter-soap-version-conflict.spec.ts --workers=1 --grep "optimistic save conflict" | Out-Host
        }
        return $LASTEXITCODE
    }
    finally {
        Pop-Location
        foreach ($entry in @(
            @{ Name = "MODERN_UI_SOAP_ENCOUNTER"; Value = $priorEncounter },
            @{ Name = "MODERN_UI_SOAP_PATIENT_ID"; Value = $priorPatient },
            @{ Name = "MODERN_UI_SOAP_MARKER"; Value = $priorMarker },
            @{ Name = "MODERN_UI_SOAP_LOCKED_MODE"; Value = $priorLocked }
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
    if ($marker -notmatch '^TMP-SOAP-VERSION-[0-9a-f-]+$') {
        throw "Unsafe SOAP test marker."
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

    $fixtureText = Invoke-PostgresScalar @"
select json_build_object(
    'patientId', e.patient_id,
    'encounter', e.encounter,
    'noteCount', (select count(*) from clinical_notes note where note.encounter=e.encounter)
)
from encounters e
where exists (
    select 1 from clinical_notes note where note.encounter=e.encounter
)
and not exists (
    select 1 from clinical_notes note
    where note.encounter=e.encounter and note.evidence_source='runtime'
)
and not exists (
    select 1 from encounter_signatures signature
    where signature.encounter=e.encounter and signature.is_lock
)
order by e.encounter
limit 1;
"@
    if (-not $fixtureText) {
        throw "No unlocked single-baseline-note encounter is available."
    }
    $fixture = $fixtureText | ConvertFrom-Json
    $initialNoteCount = [int]$fixture.noteCount

    $baseline = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/encounters/$($fixture.encounter)?includeArchivedDocuments=true" `
        -Headers $headers
    Add-Check `
        "Existing SOAP note is explicitly versioned migration evidence" `
        ($baseline.soapNote.version -eq 1 `
            -and $baseline.soapNote.evidenceSource -eq "migration-backfill" `
            -and @($baseline.soapNote.versions).Count -eq $initialNoteCount `
            -and -not $baseline.soapNote.isLocked) `
        @{ encounter=$fixture.encounter; patientId=$fixture.patientId; version=$baseline.soapNote.version; source=$baseline.soapNote.evidenceSource; versions=@($baseline.soapNote.versions).Count }

    $versionTwo = Save-SoapVersion `
        -Encounter $fixture.encounter `
        -ExpectedVersion 1 `
        -Current $baseline.soapNote `
        -Subjective "$marker API version two" `
        -Objective ([string]$baseline.soapNote.objective) `
        -Assessment ([string]$baseline.soapNote.assessment) `
        -Plan ([string]$baseline.soapNote.plan)
    Add-Check `
        "Authenticated save appends runtime SOAP version two" `
        ($versionTwo.detail.soapNote.version -eq 2 `
            -and $versionTwo.detail.soapNote.savedBy -eq "admin" `
            -and $versionTwo.detail.soapNote.evidenceSource -eq "runtime" `
            -and @($versionTwo.detail.soapNote.versions).Count -eq ($initialNoteCount + 1) `
            -and $versionTwo.detail.soapNote.versions[0].supersedesNoteId -eq $baseline.soapNote.id) `
        @{ id=$versionTwo.id; version=$versionTwo.detail.soapNote.version; actor=$versionTwo.detail.soapNote.savedBy; versions=@($versionTwo.detail.soapNote.versions).Count }

    $staleStatus = 0
    $stalePayload = $null
    try {
        Save-SoapVersion `
            -Encounter $fixture.encounter `
            -ExpectedVersion 1 `
            -Current $versionTwo.detail.soapNote `
            -Subjective "$marker stale write" `
            -Objective ([string]$versionTwo.detail.soapNote.objective) `
            -Assessment ([string]$versionTwo.detail.soapNote.assessment) `
            -Plan ([string]$versionTwo.detail.soapNote.plan) | Out-Null
    }
    catch {
        $staleStatus = [int]$_.Exception.Response.StatusCode
        $stalePayload = Get-HttpErrorPayload $_
    }
    $countAfterStale = [int](Invoke-PostgresScalar "select count(*) from clinical_notes where encounter=$($fixture.encounter);")
    Add-Check `
        "Stale SOAP draft receives structured 409 without a write" `
        ($staleStatus -eq 409 `
            -and $stalePayload.code -eq "soap_note_version_conflict" `
            -and $stalePayload.currentVersion -eq 2 `
            -and -not $stalePayload.isLocked `
            -and $countAfterStale -eq ($initialNoteCount + 1)) `
        @{ status=$staleStatus; code=$stalePayload.code; currentVersion=$stalePayload.currentVersion; noteCount=$countAfterStale }

    $versionThree = Save-SoapVersion `
        -Encounter $fixture.encounter `
        -ExpectedVersion 2 `
        -Current $versionTwo.detail.soapNote `
        -Subjective ([string]$versionTwo.detail.soapNote.subjective) `
        -Objective ([string]$versionTwo.detail.soapNote.objective) `
        -Assessment ([string]$versionTwo.detail.soapNote.assessment) `
        -Plan "$marker API version three"
    Add-Check `
        "Reviewed current draft appends version three and retains newest-first history" `
        ($versionThree.detail.soapNote.version -eq 3 `
            -and @($versionThree.detail.soapNote.versions).Count -eq ($initialNoteCount + 2) `
            -and $versionThree.detail.soapNote.versions[0].version -eq 3 `
            -and $versionThree.detail.soapNote.versions[1].version -eq 2 `
            -and $versionThree.detail.soapNote.versions[-1].evidenceSource -eq "migration-backfill") `
        @{ version=$versionThree.detail.soapNote.version; versions=@($versionThree.detail.soapNote.versions.version); sources=@($versionThree.detail.soapNote.versions.evidenceSource) }

    if ($IncludeBrowser) {
        $browserExitCode = Invoke-BrowserProof $false
        Add-Check `
            "SOAP draft conflict and reviewed rebase pass all configured browser profiles" `
            ($browserExitCode -eq 0) `
            @{ exitCode=$browserExitCode; encounter=$fixture.encounter; patientId=$fixture.patientId }
    }

    $beforeLock = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/encounters/$($fixture.encounter)?includeArchivedDocuments=true" `
        -Headers $headers
    $signBody = @{
        signerUsername = "admin"
        signedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")
        isLock = $true
        amendment = "$marker locking signature"
    } | ConvertTo-Json
    $signature = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/encounters/$($fixture.encounter)/sign" `
        -Method Put `
        -Headers $headers `
        -ContentType "application/json" `
        -Body $signBody
    $signatureId = [int]$signature.id

    $lockedStatus = 0
    $lockedPayload = $null
    try {
        Save-SoapVersion `
            -Encounter $fixture.encounter `
            -ExpectedVersion $beforeLock.soapNote.version `
            -Current $beforeLock.soapNote `
            -Subjective "$marker blocked by signature" `
            -Objective ([string]$beforeLock.soapNote.objective) `
            -Assessment ([string]$beforeLock.soapNote.assessment) `
            -Plan ([string]$beforeLock.soapNote.plan) | Out-Null
    }
    catch {
        $lockedStatus = [int]$_.Exception.Response.StatusCode
        $lockedPayload = Get-HttpErrorPayload $_
    }
    $lockedDetail = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/encounters/$($fixture.encounter)?includeArchivedDocuments=true" `
        -Headers $headers
    Add-Check `
        "Locking signature blocks direct SOAP writes with current-version evidence" `
        ($lockedStatus -eq 409 `
            -and $lockedPayload.code -eq "encounter_locked" `
            -and $lockedPayload.isLocked `
            -and $lockedPayload.currentVersion -eq $beforeLock.soapNote.version `
            -and $lockedDetail.soapNote.isLocked `
            -and $lockedDetail.soapNote.version -eq $beforeLock.soapNote.version) `
        @{ status=$lockedStatus; code=$lockedPayload.code; currentVersion=$lockedPayload.currentVersion; locked=$lockedDetail.soapNote.isLocked }

    if ($IncludeBrowser) {
        $lockedBrowserExitCode = Invoke-BrowserProof $true
        Add-Check `
            "Signed SOAP note is visibly read-only in all configured browser profiles" `
            ($lockedBrowserExitCode -eq 0) `
            @{ exitCode=$lockedBrowserExitCode; encounter=$fixture.encounter }
    }
}
catch {
    Add-Check "Unhandled encounter SOAP versioning test error" $false $_.Exception.Message
}
finally {
    if ($null -ne $headers -and $null -ne $fixture -and $null -ne $signatureId) {
        try {
            Invoke-WebRequest `
                -Uri "$ApiBaseUrl/api/encounters/$($fixture.encounter)/signatures/$signatureId" `
                -Method Delete `
                -Headers $headers `
                -UseBasicParsing | Out-Null
        }
        catch {
            Add-Check "Cleanup SOAP locking signature" $false $_.Exception.Message
        }
    }

    if ($null -ne $fixture) {
        try {
            $escapedMarker = $marker.Replace("'", "''")
            Invoke-PostgresScalar @"
delete from encounter_signatures
where encounter=$($fixture.encounter) and amendment like '$escapedMarker%';
delete from clinical_notes
where encounter=$($fixture.encounter)
  and saved_by='admin'
  and concat_ws(' ', subjective, objective, assessment, plan) like '%$escapedMarker%';
select 1;
"@ | Out-Null
            $remaining = Invoke-PostgresScalar @"
select json_build_object(
    'markerNotes', (
        select count(*) from clinical_notes
        where encounter=$($fixture.encounter)
          and concat_ws(' ', subjective, objective, assessment, plan) like '%$escapedMarker%'
    ),
    'markerSignatures', (
        select count(*) from encounter_signatures
        where encounter=$($fixture.encounter) and amendment like '$escapedMarker%'
    ),
    'noteCount', (
        select count(*) from clinical_notes where encounter=$($fixture.encounter)
    ),
    'currentVersion', (
        select max(version) from clinical_notes where encounter=$($fixture.encounter)
    )
);
"@
            $remainingFacts = $remaining | ConvertFrom-Json
            Add-Check `
                "SOAP versioning proof restores its baseline and leaves zero marker residue" `
                ($remainingFacts.markerNotes -eq 0 `
                    -and $remainingFacts.markerSignatures -eq 0 `
                    -and $remainingFacts.noteCount -eq $initialNoteCount `
                    -and $remainingFacts.currentVersion -eq 1) `
                $remainingFacts
        }
        catch {
            Add-Check "Cleanup SOAP versioning fixtures" $false $_.Exception.Message
        }
    }
}

$failed = @($checks | Where-Object { $_.status -ne "passed" })
$result = [ordered]@{
    status = if ($failed.Count -eq 0) { "passed" } else { "failed" }
    checks = $checks
}
$result | ConvertTo-Json -Depth 12
if ($failed.Count -gt 0) { exit 1 }
