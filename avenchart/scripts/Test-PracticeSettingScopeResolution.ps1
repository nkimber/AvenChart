# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$overrideCreated = $false

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{ name=$Name; status=if ($Passed) { "passed" } else { "failed" }; details=$Details })
}

function Invoke-ScopedApi([string]$Uri, [hashtable]$Headers) {
    Invoke-RestMethod -Uri $Uri -Headers $Headers -Method Get
}

function Invoke-Postgres([string]$Sql) {
    Push-Location $solutionRoot
    try { & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U legacy-ehr -d legacy-ehr_modernized -c $Sql | Out-Null }
    finally { Pop-Location }
}

try {
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated) { throw "The synthetic administrator session was not issued." }
    $headers = @{ "X-Legacy EHR-Session" = $login.sessionId }

    $global = Invoke-ScopedApi "$ApiBaseUrl/api/administration/practice-settings/effective" $headers
    Add-Check "Effective practice settings resolve system values without a facility" (($global.requestedFacilityId -eq $null) -and @($global.settings).Count -eq 3 -and @($global.settings | Where-Object { $_.sourceScope -ne "system" }).Count -eq 0) @{ sources=@($global.settings | ForEach-Object sourceScope) }

    $baseline = Invoke-ScopedApi "$ApiBaseUrl/api/administration/practice-settings/effective?facilityId=10" $headers
    Add-Check "Facility resolution returns the requested active facility with system fallback evidence" (($baseline.requestedFacilityId -eq 10) -and @($baseline.settings | Where-Object { -not $_.facilityOverrideAvailable }).Count -eq 0 -and @($baseline.settings | Where-Object { $_.sourceScope -ne "system" }).Count -eq 0) @{ requestedFacilityId=$baseline.requestedFacilityId; sources=@($baseline.settings | ForEach-Object sourceScope) }

    Invoke-Postgres "insert into practice_setting_facility_overrides(setting_key,facility_id,setting_value,updated_at,updated_by) values('practice.time-zone',10,'America/Chicago',now(),'scope-resolution-test') on conflict(setting_key,facility_id) do update set setting_value=excluded.setting_value,updated_at=excluded.updated_at,updated_by=excluded.updated_by;"
    $overrideCreated = $true
    $resolved = Invoke-ScopedApi "$ApiBaseUrl/api/administration/practice-settings/effective?facilityId=10" $headers
    $timeZone = @($resolved.settings | Where-Object { $_.key -eq "practice.time-zone" }) | Select-Object -First 1
    Add-Check "Facility override wins deterministic effective-setting precedence" ($timeZone.value -eq "America/Chicago" -and $timeZone.sourceScope -eq "facility" -and $timeZone.sourceFacilityId -eq 10) @{ value=$timeZone.value; sourceScope=$timeZone.sourceScope; sourceFacilityId=$timeZone.sourceFacilityId }

    $invalidStatus = $null
    try { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/practice-settings/effective?facilityId=999999" -Headers $headers -UseBasicParsing | Out-Null } catch { $invalidStatus = [int]$_.Exception.Response.StatusCode }
    Add-Check "Unknown facility scope is rejected" ($invalidStatus -eq 400) @{ status=$invalidStatus }
}
catch {
    Add-Check "Unhandled practice-setting scope resolution test error" $false $_.Exception.Message
}
finally {
    if ($overrideCreated) { try { Invoke-Postgres "delete from practice_setting_facility_overrides where setting_key='practice.time-zone' and facility_id=10 and updated_by='scope-resolution-test';" } catch { Add-Check "Scope resolver cleanup" $false $_.Exception.Message } }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
