param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$eventBaseline = $null

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{ name=$Name; status=if ($Passed) { "passed" } else { "failed" }; details=$Details })
}

function Get-HttpStatus([scriptblock]$Operation) {
    try { & $Operation | Out-Null; return 200 }
    catch { return [int]$_.Exception.Response.StatusCode }
}

function Invoke-PostgresScalar([string]$Sql) {
    Push-Location $solutionRoot
    try { return (& docker compose exec -T postgres psql -X -U legacy-ehr -d legacy-ehr_modernized -Atc $Sql).Trim() }
    finally { Pop-Location }
}

try {
    $eventBaseline = [long](Invoke-PostgresScalar "select coalesce(max(event_id),0) from configuration_package_events;")
    $admin = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    $frontdesk = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"gold-frontdesk-01","password":"pass"}'
    if (-not $admin.authenticated -or -not $frontdesk.authenticated) { throw "The required synthetic sessions were not issued." }
    $adminHeaders = @{ "X-Legacy EHR-Session" = $admin.sessionId }
    $frontdeskHeaders = @{ "X-Legacy EHR-Session" = $frontdesk.sessionId }

    $forbidden = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/configuration-packages/export" -Method Post -Headers $frontdeskHeaders -ContentType "application/json" -Body '{}' -UseBasicParsing }
    Add-Check "Configuration packages remain administrator protected" ($forbidden -eq 403) @{ status=$forbidden }

    $export = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-packages/export" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body '{}'
    $keys = @($export.package.practiceSettings | ForEach-Object key | Sort-Object)
    Add-Check "Export contains only the adopted non-secret setting contract" (($export.package.schema -eq "legacy-ehr-modernized-configuration-package") -and ($export.package.version -eq "1") -and ($keys -join ',' -eq 'practice.default-facility-id,practice.name,practice.time-zone') -and ($export.sha256 -match '^[0-9a-f]{64}$')) @{ keys=$keys; sha256=$export.sha256 }

    $valid = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-packages/dry-run" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ package=$export.package } | ConvertTo-Json -Depth 8)
    Add-Check "Dry run validates the exported package without enabling apply" (($valid.valid -eq $true) -and (-not $valid.applyAvailable) -and (@($valid.conflicts | Where-Object { $_.state -ne 'unchanged' }).Count -eq 0)) @{ valid=$valid.valid; applyAvailable=$valid.applyAvailable; conflicts=$valid.conflicts.Count }

    $invalidPackage = $export.package | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $invalidPackage.schema = "unknown-schema"
    $invalid = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-packages/dry-run" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ package=$invalidPackage } | ConvertTo-Json -Depth 8)
    Add-Check "Dry run rejects an unknown schema without mutation" ((-not $invalid.valid) -and (-not $invalid.applyAvailable) -and @($invalid.issues | Where-Object { $_.code -eq 'unsupported-schema' }).Count -eq 1) @{ valid=$invalid.valid; issues=$invalid.issues }
}
catch {
    Add-Check "Unhandled configuration package test error" $false $_.Exception.Message
}
finally {
    if ($null -ne $eventBaseline) {
        try {
            Push-Location $solutionRoot
            try { & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U legacy-ehr -d legacy-ehr_modernized -c "delete from configuration_package_events where event_id > $eventBaseline;" | Out-Null }
            finally { Pop-Location }
            $residue = [int](Invoke-PostgresScalar "select count(*) from configuration_package_events where event_id > $eventBaseline;")
            Add-Check "Configuration package test cleanup leaves no audit fixture residue" ($residue -eq 0) @{ residue=$residue }
        }
        catch { Add-Check "Configuration package test cleanup" $false $_.Exception.Message }
    }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
