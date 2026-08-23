# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$eventBaseline = $null
$importReason = "TMP-ADM-PACKAGE-$(New-Guid)"
$importMarker = "$importReason import"
$staleReason = "$importReason stale"
$staleMarker = "$importReason changed-after-baseline"
$rollbackReason = "$importReason compensating rollback"
$originalPracticeName = $null

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{ name=$Name; status=if ($Passed) { "passed" } else { "failed" }; details=$Details })
}

function Get-HttpStatus([scriptblock]$Operation) {
    try { & $Operation | Out-Null; return 200 }
    catch { return [int]$_.Exception.Response.StatusCode }
}

function Invoke-PostgresScalar([string]$Sql) {
    Push-Location $solutionRoot
    try { return (& docker compose exec -T postgres psql -X -U avenchart -d avenchart -Atc $Sql).Trim() }
    finally { Pop-Location }
}

try {
    $eventBaseline = [long](Invoke-PostgresScalar "select coalesce(max(event_id),0) from configuration_package_events;")
    $admin = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    $frontdesk = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"gold-frontdesk-01","password":"pass"}'
    if (-not $admin.authenticated -or -not $frontdesk.authenticated) { throw "The required synthetic sessions were not issued." }
    $adminHeaders = New-AvenChartStaffAccessContextHeaders -Login $admin
    $frontdeskHeaders = New-AvenChartStaffAccessContextHeaders -Login $frontdesk

    $historyForbidden = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests" -Headers $frontdeskHeaders -UseBasicParsing }
    $historyInvalid = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests?kind=unsupported" -Headers $adminHeaders -UseBasicParsing }
    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests?kind=import&limit=1" -Headers $adminHeaders
    Add-Check "Configuration package request history is administrator-protected, filtered, and bounded" (($historyForbidden -eq 403) -and ($historyInvalid -eq 400) -and ($history.limit -eq 1) -and ($history.total -ge @($history.requests).Count)) @{ forbidden=$historyForbidden; invalid=$historyInvalid; limit=$history.limit; total=$history.total }

    $forbidden = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/configuration-packages/export" -Method Post -Headers $frontdeskHeaders -ContentType "application/json" -Body '{}' -UseBasicParsing }
    Add-Check "Configuration packages remain administrator protected" ($forbidden -eq 403) @{ status=$forbidden }

    $export = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-packages/export" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body '{}'
    $keys = @($export.package.practiceSettings | ForEach-Object key | Sort-Object)
    Add-Check "Export contains only the adopted non-secret setting contract" (($export.package.schema -eq "avenchart-configuration-package") -and ($export.package.version -eq "1") -and ($keys -join ',' -eq 'practice.default-facility-id,practice.name,practice.time-zone') -and ($export.sha256 -match '^[0-9a-f]{64}$')) @{ keys=$keys; sha256=$export.sha256 }

    $valid = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-packages/dry-run" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ package=$export.package } | ConvertTo-Json -Depth 8)
    Add-Check "Dry run validates the exported package without enabling apply" (($valid.valid -eq $true) -and (-not $valid.applyAvailable) -and (@($valid.conflicts | Where-Object { $_.state -ne 'unchanged' }).Count -eq 0)) @{ valid=$valid.valid; applyAvailable=$valid.applyAvailable; conflicts=$valid.conflicts.Count }

    $invalidPackage = $export.package | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $invalidPackage.schema = "unknown-schema"
    $invalid = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-packages/dry-run" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ package=$invalidPackage } | ConvertTo-Json -Depth 8)
    Add-Check "Dry run rejects an unknown schema without mutation" ((-not $invalid.valid) -and (-not $invalid.applyAvailable) -and @($invalid.issues | Where-Object { $_.code -eq 'unsupported-schema' }).Count -eq 1) @{ valid=$invalid.valid; issues=$invalid.issues }

    $importPackage = $export.package | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $nameSetting = @($importPackage.practiceSettings | Where-Object { $_.key -eq 'practice.name' })[0]
    $originalPracticeName = $nameSetting.value
    $nameSetting.value = $importMarker
    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ package=$importPackage; reason=$importReason } | ConvertTo-Json -Depth 8)
    $submitted = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($created.request.requestId)/submit" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$created.request.version; note='Ready for local approval.' } | ConvertTo-Json)
    $approved = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($created.request.requestId)/approve" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$submitted.request.version; note='Approved for local activation.' } | ConvertTo-Json)
    $activated = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($created.request.requestId)/activate" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$approved.request.version; note='Activated after baseline check.' } | ConvertTo-Json)
    $settingsAfterImport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings" -Method Get -Headers $adminHeaders
    $currentPracticeName = @($settingsAfterImport.settings | Where-Object { $_.key -eq 'practice.name' })[0].value
    $actions = @($activated.events | ForEach-Object action)
    Add-Check "Reviewed import requires draft, submit, approval, baseline-checked activation, and setting revision" (($activated.request.status -eq 'activated') -and ($currentPracticeName -eq $importMarker) -and @('created','submitted','approved','activated' | Where-Object { $_ -notin $actions }).Count -eq 0) @{ status=$activated.request.status; currentPracticeName=$currentPracticeName; actions=$actions }

    $rollback = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($created.request.requestId)/compensating-rollback" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ note=$rollbackReason } | ConvertTo-Json)
    $rollbackSubmitted = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($rollback.request.requestId)/submit" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$rollback.request.version } | ConvertTo-Json)
    $rollbackApproved = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($rollback.request.requestId)/approve" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$rollbackSubmitted.request.version } | ConvertTo-Json)
    $rollbackActivated = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($rollback.request.requestId)/activate" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$rollbackApproved.request.version } | ConvertTo-Json)
    $settingsAfterRollback = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings" -Method Get -Headers $adminHeaders
    $rolledBackName = @($settingsAfterRollback.settings | Where-Object { $_.key -eq 'practice.name' })[0].value
    Add-Check "Compensating rollback is separately reviewed and restores the captured baseline" (($rollbackActivated.request.kind -eq 'rollback') -and ($rollbackActivated.request.status -eq 'activated') -and ($rolledBackName -eq $originalPracticeName)) @{ kind=$rollbackActivated.request.kind; status=$rollbackActivated.request.status; practiceName=$rolledBackName }

    $stalePackage = $export.package | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $staleRequest = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ package=$stalePackage; reason=$staleReason } | ConvertTo-Json -Depth 8)
    $staleSubmitted = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($staleRequest.request.requestId)/submit" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$staleRequest.request.version } | ConvertTo-Json)
    $staleApproved = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($staleRequest.request.requestId)/approve" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$staleSubmitted.request.version } | ConvertTo-Json)
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings/practice.name" -Method Put -Headers $adminHeaders -ContentType "application/json" -Body (@{ value=$staleMarker } | ConvertTo-Json) | Out-Null
    $staleActivationStatus = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($staleRequest.request.requestId)/activate" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$staleApproved.request.version } | ConvertTo-Json) -UseBasicParsing }
    $staleCancelled = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/configuration-package-import-requests/$($staleRequest.request.requestId)/cancel" -Method Post -Headers $adminHeaders -ContentType "application/json" -Body (@{ expectedVersion=$staleApproved.request.version; note='Baseline changed during review.' } | ConvertTo-Json)
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings/practice.name" -Method Put -Headers $adminHeaders -ContentType "application/json" -Body (@{ value=$originalPracticeName } | ConvertTo-Json) | Out-Null
    Add-Check "Activation rejects stale complete-setting baselines before mutation" (($staleActivationStatus -eq 409) -and ($staleCancelled.request.status -eq 'cancelled')) @{ status=$staleActivationStatus; requestStatus=$staleCancelled.request.status }
}
catch {
    Add-Check "Unhandled configuration package test error" $false $_.Exception.Message
}
finally {
    if ($null -ne $eventBaseline) {
        try {
            Push-Location $solutionRoot
            try {
                if ($null -ne $originalPracticeName) {
                    & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U avenchart -d avenchart -c "update practice_settings set setting_value = '$originalPracticeName' where setting_key = 'practice.name' and setting_value in ('$importMarker', '$staleMarker');" | Out-Null
                }
                & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U avenchart -d avenchart -c "delete from configuration_package_import_request_events where request_id in (select request_id from configuration_package_import_requests where reason in ('$importReason', '$rollbackReason', '$staleReason')); delete from configuration_package_import_requests where reason = '$rollbackReason'; delete from configuration_package_import_requests where reason in ('$importReason', '$staleReason'); delete from practice_setting_revisions where value in ('$importMarker', '$staleMarker') or prior_value in ('$importMarker', '$staleMarker'); delete from practice_setting_audit_events where new_value in ('$importMarker', '$staleMarker') or prior_value in ('$importMarker', '$staleMarker'); delete from configuration_package_events where event_id > $eventBaseline;" | Out-Null
            }
            finally { Pop-Location }
            $residue = [int](Invoke-PostgresScalar "select count(*) from configuration_package_events where event_id > $eventBaseline;")
            $requestResidue = [int](Invoke-PostgresScalar "select count(*) from configuration_package_import_requests where reason in ('$importReason', '$rollbackReason', '$staleReason');")
            $markerResidue = [int](Invoke-PostgresScalar "select count(*) from practice_settings where setting_value in ('$importMarker', '$staleMarker');")
            Add-Check "Configuration package test cleanup leaves no audit fixture residue" (($residue -eq 0) -and ($requestResidue -eq 0) -and ($markerResidue -eq 0)) @{ residue=$residue; requestResidue=$requestResidue; markerResidue=$markerResidue }
        }
        catch { Add-Check "Configuration package test cleanup" $false $_.Exception.Message }
    }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
