# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$requestId = $null
$testValue = "America/Chicago"

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
    $headers = @{ "X-AvenChart-Session" = $login.sessionId }

    $before = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings/effective?facilityId=10" -Headers $headers
    $beforeTimeZone = @($before.settings | Where-Object { $_.key -eq "practice.time-zone" }) | Select-Object -First 1
    Add-Check "Scoped lifecycle starts from the facility's system fallback" ($beforeTimeZone.sourceScope -eq "system") @{ value=$beforeTimeZone.value; source=$beforeTimeZone.sourceScope }
    if ($beforeTimeZone.sourceScope -ne "system") { throw "Facility 10 already has a time-zone override; the guarded fixture cannot safely continue." }

    $marker = "TMP-ADM-SETTING-SCOPE-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
    $draft = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings/practice.time-zone/change-requests" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ value=$testValue; reason=$marker; facilityId=10 } | ConvertTo-Json)
    $requestId = $draft.request.requestId
    Add-Check "Facility-scoped draft retains scope and resolved baseline" (($draft.request.facilityId -eq 10) -and ($draft.request.baselineValue -eq $beforeTimeZone.value) -and ($draft.request.status -eq "draft")) @{ requestId=$requestId; facilityId=$draft.request.facilityId; baseline=$draft.request.baselineValue }

    $submitted = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-setting-change-requests/$requestId/submit" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ note="Scoped fixture submit"; expectedVersion=$draft.request.version } | ConvertTo-Json)
    $approved = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-setting-change-requests/$requestId/approve" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ note="Scoped fixture approve"; expectedVersion=$submitted.request.version } | ConvertTo-Json)
    $activated = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-setting-change-requests/$requestId/activate" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ note="Scoped fixture activate"; expectedVersion=$approved.request.version } | ConvertTo-Json)
    $after = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings/effective?facilityId=10" -Headers $headers
    $afterTimeZone = @($after.settings | Where-Object { $_.key -eq "practice.time-zone" }) | Select-Object -First 1
    Add-Check "Approved scoped activation changes only the facility effective value" (($activated.request.status -eq "activated") -and ($afterTimeZone.value -eq $testValue) -and ($afterTimeZone.sourceScope -eq "facility") -and ($afterTimeZone.sourceFacilityId -eq 10)) @{ status=$activated.request.status; value=$afterTimeZone.value; source=$afterTimeZone.sourceScope }

    Push-Location $solutionRoot
    try { $revisionCount = [int](& docker compose exec -T postgres psql -X -U avenchart -d avenchart -Atc "select count(*) from practice_setting_facility_override_revisions where setting_key='practice.time-zone' and facility_id=10 and value='$testValue';") }
    finally { Pop-Location }
    Add-Check "Scoped activation records immutable facility override evidence" ($revisionCount -eq 1) @{ revisions=$revisionCount }
}
catch {
    Add-Check "Unhandled scoped practice-setting lifecycle test error" $false $_.Exception.Message
}
finally {
    if ($requestId) {
        try {
            Invoke-Postgres "delete from practice_setting_facility_override_revisions where setting_key='practice.time-zone' and facility_id=10 and value='$testValue' and username='admin'; delete from practice_setting_facility_overrides where setting_key='practice.time-zone' and facility_id=10 and setting_value='$testValue' and updated_by='admin';"
            Invoke-Postgres "delete from practice_setting_change_request_events where request_id='$requestId'::uuid; delete from practice_setting_change_requests where request_id='$requestId'::uuid and reason like 'TMP-ADM-SETTING-SCOPE-%';"
            Push-Location $solutionRoot
            try { $residue = [int](& docker compose exec -T postgres psql -X -U avenchart -d avenchart -Atc "select count(*) from practice_setting_change_requests where request_id='$requestId'::uuid;") }
            finally { Pop-Location }
            Add-Check "Scoped lifecycle fixture cleanup leaves no request residue" ($residue -eq 0) @{ residue=$residue }
        }
        catch { Add-Check "Scoped lifecycle fixture cleanup" $false $_.Exception.Message }
    }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
