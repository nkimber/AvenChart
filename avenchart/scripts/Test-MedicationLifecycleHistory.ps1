# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$medicationId = $null

try {
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "The synthetic administrator session was not issued."
    }

    $headers = @{ "X-AvenChart-Session" = $login.sessionId }
    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications" -Method Post -Headers $headers -ContentType "application/json" -Body '{"patientId":"MOD-PAT-0006","title":"Temporary medication lifecycle proof","dateTime":"2026-07-29","diagnosis":"Z79.899","comments":"Temporary proof fixture"}'
    $medicationId = $created.id
    $createdItem = @($created.detail.medications | Where-Object { $_.id -eq $medicationId })[0]
    if ($createdItem.lifecycleVersion -ne 1 -or $createdItem.lifecycleEventCount -ne 1) {
        throw "The created medication does not expose the initial lifecycle evidence."
    }

    $deactivated = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$medicationId/deactivate" -Method Put -Headers $headers -ContentType "application/json" -Body '{"comments":"No longer taking; temporary proof.","expectedVersion":1}'
    $inactiveItem = @($deactivated.detail.medications | Where-Object { $_.id -eq $medicationId })[0]
    if ($inactiveItem.activity -ne 0 -or $inactiveItem.lifecycleVersion -ne 2) {
        throw "The temporary medication did not deactivate at lifecycle version two."
    }

    $staleStatus = 0
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/api/clinical-lists/medications/$medicationId/restore" -Method Put -Headers $headers -ContentType "application/json" -Body '{"reason":"Stale lifecycle proof.","expectedVersion":1}' -UseBasicParsing | Out-Null
    }
    catch {
        $staleStatus = [int]$_.Exception.Response.StatusCode
    }
    if ($staleStatus -ne 409) {
        throw "Expected HTTP 409 for stale restore, got $staleStatus."
    }

    $restored = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$medicationId/restore" -Method Put -Headers $headers -ContentType "application/json" -Body '{"reason":"Medication reconciliation confirmed active use.","expectedVersion":2}'
    $activeItem = @($restored.detail.medications | Where-Object { $_.id -eq $medicationId })[0]
    if ($activeItem.activity -ne 1 -or $activeItem.lifecycleVersion -ne 3 -or $activeItem.lifecycleEventCount -ne 3) {
        throw "The temporary medication did not restore with complete versioned evidence."
    }

    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$medicationId/lifecycle-history" -Headers $headers
    $actions = @($history.events | ForEach-Object action) -join ","
    if ($history.currentVersion -ne 3 -or $history.eventCount -ne 3 -or $actions -ne "restored,deactivated,created") {
        throw "Unexpected medication lifecycle evidence: version=$($history.currentVersion); eventCount=$($history.eventCount); actions=$actions"
    }

    Write-Host "Medication lifecycle history workflow passed."
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($medicationId)) {
        docker compose exec -T postgres psql -X -U avenchart -d avenchart -v ON_ERROR_STOP=1 -c "delete from medications where id = '$medicationId';" | Out-Null
    }
}
