# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$medicationId = $null

try {
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "The synthetic administrator session was not issued."
    }

    $headers = New-AvenChartStaffAccessContextHeaders -Login $login
    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications" -Method Post -Headers $headers -ContentType "application/json" -Body '{"patientId":"MOD-PAT-0006","title":"Temporary medication correction proof","dateTime":"2026-07-29","diagnosis":"Z79.899","comments":"Temporary proof fixture"}'
    $medicationId = $created.id

    $edited = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$medicationId" -Method Put -Headers $headers -ContentType "application/json" -Body '{"title":"Corrected temporary medication proof","diagnosis":"Z79.891","date":"2026-07-28","comments":"Corrected local medication-list details.","reason":"Correct temporary proof data.","expectedVersion":1}'
    $editedItem = @($edited.detail.medications | Where-Object { $_.id -eq $medicationId })[0]
    if ($editedItem.title -ne "Corrected temporary medication proof" -or $editedItem.lifecycleVersion -ne 2 -or $editedItem.lifecycleEventCount -ne 2) {
        throw "The medication content correction did not update the list or its lifecycle evidence."
    }

    $staleStatus = 0
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/api/clinical-lists/medications/$medicationId" -Method Put -Headers $headers -ContentType "application/json" -Body '{"title":"Stale correction","diagnosis":null,"date":"2026-07-29","comments":null,"reason":"Stale proof.","expectedVersion":1}' -UseBasicParsing | Out-Null
    }
    catch {
        $staleStatus = [int]$_.Exception.Response.StatusCode
    }
    if ($staleStatus -ne 409) {
        throw "Expected HTTP 409 for a stale medication correction, got $staleStatus."
    }

    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$medicationId/lifecycle-history" -Headers $headers
    $actions = @($history.events | ForEach-Object action) -join ","
    if ($history.currentVersion -ne 2 -or $history.eventCount -ne 2 -or $actions -ne "edited,created" -or $history.events[0].reason -ne "Correct temporary proof data.") {
        throw "Unexpected medication correction evidence: version=$($history.currentVersion); eventCount=$($history.eventCount); actions=$actions"
    }

    Write-Host "Medication content edit workflow passed."
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($medicationId)) {
        docker compose -f (Join-Path $PSScriptRoot "..\\docker-compose.yml") exec -T postgres psql -X -U avenchart -d avenchart -v ON_ERROR_STOP=1 -c "delete from medications where id = '$medicationId';" | Out-Null
    }
}
