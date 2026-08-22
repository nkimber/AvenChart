# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$systemRequestId = $null
$facilityRequestId = $null
$marker = "TMP-ADM-SETTING-IMPACT-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{ name=$Name; status=if ($Passed) { "passed" } else { "failed" }; details=$Details })
}

function Invoke-Postgres([string]$Sql) {
    Push-Location $solutionRoot
    try { & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U avenchart -d avenchart -c $Sql | Out-Null }
    finally { Pop-Location }
}

try {
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated) { throw "The synthetic administrator session was not issued." }
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login

    $systemDraft = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings/practice.name/change-requests" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ value=$marker; reason=$marker } | ConvertTo-Json)
    $systemRequestId = $systemDraft.request.requestId
    $systemPreview = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-setting-change-requests/$systemRequestId/impact-preview" -Headers $headers
    $systemFacilities = @($systemPreview.impacts | Where-Object { $_.resourceType -eq "Active facilities" }) | Select-Object -First 1
    $systemUnavailable = @($systemPreview.impacts | Where-Object { -not $_.previewAvailable })
    Add-Check "System request preview reports local facility reach and explicit unavailable categories" (($systemPreview.scope -eq "system") -and ($systemPreview.facilityId -eq $null) -and ($systemFacilities.previewAvailable -eq $true) -and ($systemFacilities.affectedCount -gt 0) -and ($systemUnavailable.Count -eq 5)) @{ facilities=$systemFacilities.affectedCount; unavailable=@($systemUnavailable | ForEach-Object resourceType) }

    $facilityDraft = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings/practice.time-zone/change-requests" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ value="America/Chicago"; reason=$marker; facilityId=10 } | ConvertTo-Json)
    $facilityRequestId = $facilityDraft.request.requestId
    $facilityPreview = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-setting-change-requests/$facilityRequestId/impact-preview" -Headers $headers
    $facilityScope = @($facilityPreview.impacts | Where-Object { $_.resourceType -eq "Active facilities" }) | Select-Object -First 1
    $futureAppointments = @($facilityPreview.impacts | Where-Object { $_.resourceType -eq "Future appointment views" }) | Select-Object -First 1
    Add-Check "Facility time-zone preview scopes countable appointment-view impact locally" (($facilityPreview.scope -eq "facility") -and ($facilityPreview.facilityId -eq 10) -and ($facilityScope.affectedCount -eq 1) -and $futureAppointments.previewAvailable -and ($futureAppointments.affectedCount -ge 0)) @{ facilityCount=$facilityScope.affectedCount; appointmentViews=$futureAppointments.affectedCount }

    $unknownStatus = $null
    try { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/practice-setting-change-requests/$([Guid]::NewGuid())/impact-preview" -Headers $headers -UseBasicParsing | Out-Null } catch { $unknownStatus = [int]$_.Exception.Response.StatusCode }
    Add-Check "Unknown change-request impact preview is rejected" ($unknownStatus -eq 404) @{ status=$unknownStatus }
}
catch {
    Add-Check "Unhandled practice-setting impact preview test error" $false $_.Exception.Message
}
finally {
    try {
        if ($systemRequestId) { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/practice-setting-change-requests/$systemRequestId/test-fixture" -Method Delete -Headers $headers -UseBasicParsing | Out-Null }
        if ($facilityRequestId) { Invoke-Postgres "delete from practice_setting_change_request_events where request_id='$facilityRequestId'::uuid; delete from practice_setting_change_requests where request_id='$facilityRequestId'::uuid and reason='$marker';" }
        if ($systemRequestId -or $facilityRequestId) {
            Push-Location $solutionRoot
            try { $residue = [int](& docker compose exec -T postgres psql -X -U avenchart -d avenchart -Atc "select count(*) from practice_setting_change_requests where reason='$marker';") }
            finally { Pop-Location }
            Add-Check "Impact-preview fixtures leave no request residue" ($residue -eq 0) @{ residue=$residue }
        }
    }
    catch { Add-Check "Impact-preview fixture cleanup" $false $_.Exception.Message }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
