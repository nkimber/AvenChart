# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$delegationId = $null
$requestId = $null
$marker = "TMP-ADM-SETTING-DELEGATION-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{ name=$Name; status=if ($Passed) { "passed" } else { "failed" }; details=$Details })
}

function Get-HttpStatus([scriptblock]$Operation) {
    try { & $Operation | Out-Null; return 200 }
    catch { return [int]$_.Exception.Response.StatusCode }
}

function Invoke-Postgres([string]$Sql) {
    Push-Location $solutionRoot
    try { & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U legacy-ehr -d legacy-ehr_modernized -c $Sql | Out-Null }
    finally { Pop-Location }
}

try {
    $admin = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    $frontdesk = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"gold-frontdesk-01","password":"pass"}'
    if (-not $admin.authenticated -or -not $frontdesk.authenticated) { throw "The required synthetic sessions were not issued." }
    $adminHeaders = @{ "X-Legacy EHR-Session" = $admin.sessionId }
    $delegateHeaders = @{ "X-Legacy EHR-Session" = $frontdesk.sessionId }
    $candidate = @{ value="America/Chicago"; reason=$marker; facilityId=10 } | ConvertTo-Json

    $beforeGrant = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/configuration-delegation/practice-settings/practice.time-zone/change-requests" -Method Post -Headers $delegateHeaders -ContentType "application/json" -Body $candidate -UseBasicParsing }
    Add-Check "Unassigned staff cannot create delegated setting drafts" ($beforeGrant -eq 403) @{ status=$beforeGrant }

    $grant = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-setting-delegations" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ username="gold-frontdesk-01"; settingKey="practice.time-zone"; facilityId=10; expiresAt=(Get-Date).ToUniversalTime().AddHours(1).ToString("O"); reason=$marker } | ConvertTo-Json)
    $delegationId = $grant.delegationId
    Add-Check "Administrator records a time-bounded facility delegation" (($grant.active -eq $true) -and ($grant.username -eq "gold-frontdesk-01") -and ($grant.facilityId -eq 10) -and $grant.expiresAt) @{ delegationId=$delegationId; expiresAt=$grant.expiresAt }

    $draft = Invoke-RestMethod -Uri "$ApiBaseUrl/api/configuration-delegation/practice-settings/practice.time-zone/change-requests" -Method Post -Headers $delegateHeaders -ContentType "application/json" -Body $candidate
    $requestId = $draft.request.requestId
    Add-Check "Delegate can create only its facility-scoped draft" (($draft.request.createdBy -eq "gold-frontdesk-01") -and ($draft.request.facilityId -eq 10) -and ($draft.request.status -eq "draft")) @{ requestId=$requestId; createdBy=$draft.request.createdBy }

    $submitted = Invoke-RestMethod -Uri "$ApiBaseUrl/api/configuration-delegation/practice-setting-change-requests/$requestId/submit" -Method Post -Headers $delegateHeaders -ContentType "application/json" -Body (@{ note="Delegated fixture submit"; expectedVersion=$draft.request.version } | ConvertTo-Json)
    Add-Check "Delegate can submit only its own authorized draft" (($submitted.request.status -eq "submitted") -and ($submitted.request.updatedBy -eq "gold-frontdesk-01")) @{ status=$submitted.request.status; updatedBy=$submitted.request.updatedBy }

    $adminRouteStatus = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/practice-setting-change-requests/$requestId/approve" -Method Post -Headers $delegateHeaders -ContentType "application/json" -Body (@{ note="attempt"; expectedVersion=$submitted.request.version } | ConvertTo-Json) -UseBasicParsing }
    Add-Check "Delegation does not grant administrator approval authority" ($adminRouteStatus -eq 403) @{ status=$adminRouteStatus }

    $revoked = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-setting-delegations/$delegationId/revoke" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ note="Delegation fixture cleanup" } | ConvertTo-Json)
    Add-Check "Administrator revocation ends delegated authority with evidence" (-not $revoked.active) @{ delegationId=$delegationId; active=$revoked.active }

    $afterRevoke = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/configuration-delegation/practice-settings/practice.time-zone/change-requests" -Method Post -Headers $delegateHeaders -ContentType "application/json" -Body $candidate -UseBasicParsing }
    Add-Check "Revoked delegate cannot create another setting draft" ($afterRevoke -eq 403) @{ status=$afterRevoke }
}
catch {
    Add-Check "Unhandled practice-setting delegation test error" $false $_.Exception.Message
}
finally {
    try {
        if ($requestId) { Invoke-Postgres "delete from practice_setting_change_request_events where request_id='$requestId'::uuid; delete from practice_setting_change_requests where request_id='$requestId'::uuid and reason='$marker';" }
        if ($delegationId) { Invoke-Postgres "delete from practice_setting_delegation_events where delegation_id='$delegationId'::uuid; delete from practice_setting_delegations where delegation_id='$delegationId'::uuid and reason='$marker';" }
        if ($requestId -or $delegationId) {
            Push-Location $solutionRoot
            try { $residue = [int](& docker compose exec -T postgres psql -X -U legacy-ehr -d legacy-ehr_modernized -Atc "select (select count(*) from practice_setting_change_requests where reason='$marker') + (select count(*) from practice_setting_delegations where reason='$marker');") }
            finally { Pop-Location }
            Add-Check "Delegation fixture cleanup leaves no request or authority residue" ($residue -eq 0) @{ residue=$residue }
        }
    }
    catch { Add-Check "Delegation fixture cleanup" $false $_.Exception.Message }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
