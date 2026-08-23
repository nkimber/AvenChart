# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$temporaryResultId = 2090000000 + (Get-Random -Minimum 1 -Maximum 999999)
$composeArguments = @("compose", "exec", "-T", "postgres", "psql", "-X", "-U", "avenchart", "-d", "avenchart", "-v", "ON_ERROR_STOP=1")

function Invoke-AvenChartDatabase {
    param([string]$Sql, [switch]$TabSeparated)

    $arguments = @($composeArguments)
    if ($TabSeparated) {
        $arguments += @("-t", "-A")
    }
    $arguments += @("-c", $Sql)
    $output = & docker @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "AvenChart PostgreSQL command failed."
    }
    return $output
}

try {
    $existing = (Invoke-AvenChartDatabase -Sql "select count(*) from lab_results where id = $temporaryResultId;" -TabSeparated).Trim()
    if ($existing -ne "0") {
        throw "The generated temporary result ID already exists; rerun the check."
    }

    Invoke-AvenChartDatabase -Sql "insert into lab_results (id, report_id, code, text, units, result, range, abnormal, result_date, result_status) values ($temporaryResultId, 6000001, 'CRIT-LOCAL', 'Temporary critical-result acknowledgement proof', 'mg/dL', '9.9', '0-1', 'critical', now(), 'final');" | Out-Null

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "The synthetic administrator session was not issued."
    }

    $headers = New-AvenChartStaffAccessContextHeaders -Login $login
    $before = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/critical-result-queue" -Headers $headers
    $openResult = @($before.results | Where-Object { $_.resultId -eq $temporaryResultId })[0]
    if ($null -eq $openResult -or $openResult.acknowledgementVersion -ne 1) {
        throw "The temporary critical result was not returned as an open version-one acknowledgement."
    }

    $acknowledgement = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/results/$temporaryResultId/critical-acknowledgement" -Headers $headers -Method Put -ContentType "application/json" -Body '{"expectedVersion":1,"reason":"Temporary end-to-end acknowledgement proof."}'
    if (-not $acknowledgement.acknowledged) {
        throw "The critical-result acknowledgement did not return confirmation."
    }

    $after = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/critical-result-queue" -Headers $headers
    if (@($after.results | Where-Object { $_.resultId -eq $temporaryResultId }).Count -ne 0) {
        throw "The acknowledged temporary result remained in the open critical-result queue."
    }

    $staleStatus = 0
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/api/procedures/results/$temporaryResultId/critical-acknowledgement" -Headers $headers -Method Put -ContentType "application/json" -Body '{"expectedVersion":1,"reason":"Stale acknowledgement proof."}' -UseBasicParsing | Out-Null
    }
    catch {
        $staleStatus = [int]$_.Exception.Response.StatusCode
    }
    if ($staleStatus -ne 409) {
        throw "Expected HTTP 409 for the stale acknowledgement, got $staleStatus."
    }

    $event = (Invoke-AvenChartDatabase -Sql "select action || '|' || previous_status || '|' || current_status || '|' || expected_version || '|' || resulting_version from critical_lab_result_acknowledgement_events where result_id = $temporaryResultId;" -TabSeparated).Trim()
    if ($event -ne "acknowledged|open|acknowledged|1|2") {
        throw "The acknowledgement event did not retain the expected transition evidence: $event"
    }

    Write-Host "Critical-result acknowledgement workflow passed."
}
finally {
    Invoke-AvenChartDatabase -Sql "delete from lab_results where id = $temporaryResultId;" | Out-Null
}
