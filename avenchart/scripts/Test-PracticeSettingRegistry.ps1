# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{ name=$Name; status=if ($Passed) { "passed" } else { "failed" }; details=$Details })
}

try {
    $unauthenticatedStatus = $null
    try { Invoke-WebRequest -Uri "$ApiBaseUrl/api/administration/practice-settings/registry" -UseBasicParsing | Out-Null } catch { $unauthenticatedStatus = [int]$_.Exception.Response.StatusCode }
    Add-Check "Configuration registry rejects unauthenticated access" ($unauthenticatedStatus -eq 401) @{ status=$unauthenticatedStatus }

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated) { throw "The synthetic administrator session was not issued." }
    $registry = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/practice-settings/registry" -Headers @{ "X-Legacy EHR-Session" = $login.sessionId }
    $keys = @($registry.items | ForEach-Object key | Sort-Object)
    Add-Check "Registry identifies all adopted non-secret setting contracts" (($registry.registryRevision -eq "local-practice-setting-registry-v1") -and ($keys -join "," -eq "practice.default-facility-id,practice.name,practice.time-zone") -and @($registry.items | Where-Object { $_.sensitivity -ne "non-secret" -or $_.breakGlassPermitted -or $_.allowedScopes.Count -ne 2 }).Count -eq 0) @{ revision=$registry.registryRevision; keys=$keys }

    $timeZone = @($registry.items | Where-Object { $_.key -eq "practice.time-zone" }) | Select-Object -First 1
    Add-Check "Registry records time-zone impact and corrective rollback boundary" (($timeZone.impactClass -eq "schedule display") -and ($timeZone.impactPreviewAvailability -match "future appointment") -and ($timeZone.rollbackPath -match "corrective request")) @{ impactClass=$timeZone.impactClass; impactPreview=$timeZone.impactPreviewAvailability }
}
catch {
    Add-Check "Unhandled practice-setting registry test error" $false $_.Exception.Message
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
