# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$messageId = $null
$marker = "TMP-MESSAGE-CORRECTION-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{ name=$Name; status=if ($Passed) { "passed" } else { "failed" }; details=$Details })
}

function Get-HttpStatus([scriptblock]$Operation) {
    try { & $Operation | Out-Null; return 200 }
    catch { return [int]$_.Exception.Response.StatusCode }
}

function Invoke-Postgres([string]$Sql) {
    Push-Location $solutionRoot
    try { & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U avenchart -d avenchart -c $Sql | Out-Null }
    finally { Pop-Location }
}

function Invoke-PostgresScalar([string]$Sql) {
    Push-Location $solutionRoot
    try { return (& docker compose exec -T postgres psql -X -U avenchart -d avenchart -Atc $Sql).Trim() }
    finally { Pop-Location }
}

try {
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) { throw "The synthetic administrator session was not issued." }
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login
    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ patientId="MOD-PAT-0001"; title=$marker; body="Original message text"; assignedTo="admin" } | ConvertTo-Json)
    $messageId = $created.id
    if ([string]::IsNullOrWhiteSpace($messageId)) { throw "The synthetic staff message was not created." }

    $corrected = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/correct" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ correction="Corrected clarification"; reason="Clarify the original statement" } | ConvertTo-Json)
    $message = @($corrected.detail.messages | Where-Object { $_.id -eq $messageId }) | Select-Object -First 1
    Add-Check "Correction preserves original text and appends the legacy-style actor entry" ($message.body -match '^Original message text' -and $message.body -match '\(admin correction\) Corrected clarification') @{ body=$message.body }

    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/correction-history" -Headers $headers
    $event = @($history.events) | Select-Object -First 1
    Add-Check "Correction records immutable correction, reason, actor, and time evidence" ($null -ne $event -and $event.correction -eq "Corrected clarification" -and $event.reason -eq "Clarify the original statement" -and $event.actor -eq "admin" -and -not [string]::IsNullOrWhiteSpace($event.occurredAt)) @{ event=$event }

    $missingReasonStatus = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/correct" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ correction="Missing reason"; reason="" } | ConvertTo-Json) -UseBasicParsing }
    Add-Check "Correction rejects a missing reason" ($missingReasonStatus -eq 400) @{ status=$missingReasonStatus }
}
catch {
    Add-Check "Unhandled staff-message correction lifecycle test error" $false $_.Exception.Message
}
finally {
    try {
        if ($messageId) {
            Invoke-Postgres "delete from message_correction_events where message_id='$messageId'; delete from messages where id='$messageId' and title='$marker';"
            $residue = [int](Invoke-PostgresScalar "select (select count(*) from messages where id='$messageId') + (select count(*) from message_correction_events where message_id='$messageId');")
            Add-Check "Synthetic correction fixture cleanup leaves no data residue" ($residue -eq 0) @{ messageId=$messageId; residue=$residue }
        }
    }
    catch { Add-Check "Synthetic correction fixture cleanup" $false $_.Exception.Message }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
