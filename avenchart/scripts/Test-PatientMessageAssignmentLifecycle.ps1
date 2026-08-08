# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$messageId = $null
$marker = "TMP-MESSAGE-ASSIGNMENT-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

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

function Invoke-Postgres([string]$Sql) {
    Push-Location $solutionRoot
    try { & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U avenchart -d avenchart -c $Sql | Out-Null }
    finally { Pop-Location }
}

try {
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) { throw "The synthetic administrator session was not issued." }
    $headers = @{ "X-AvenChart-Session" = $login.sessionId }

    $assignees = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/assignees" -Headers $headers
    $hasFrontdesk = @($assignees.assignees | Where-Object { $_.username -eq "gold-frontdesk-01" }).Count -eq 1
    Add-Check "Message assignment directory returns active staff" ($assignees.count -ge 2 -and $hasFrontdesk) @{ count=$assignees.count; policyRevision=$assignees.policyRevision }

    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ patientId="MOD-PAT-0001"; title=$marker; body="Synthetic assignment lifecycle verification"; assignedTo="" } | ConvertTo-Json)
    $messageId = $created.id
    if ([string]::IsNullOrWhiteSpace($messageId)) { throw "The synthetic message was not created." }
    Add-Check "Creates an unassigned message at assignment version zero" (@($created.detail.messages | Where-Object { $_.id -eq $messageId -and $_.assignmentVersion -eq 0 -and $null -eq $_.assignedTo }).Count -eq 1) @{ messageId=$messageId }

    $assigned = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/assignment" -Method Put -Headers $headers -ContentType "application/json" -Body (@{ assignedTo="gold-frontdesk-01"; expectedVersion=0; reason="Lifecycle verification assignment" } | ConvertTo-Json)
    $assignedMessage = $assigned.detail.messages | Where-Object { $_.id -eq $messageId }
    Add-Check "Assigns only to an active staff target and increments the version" ($assignedMessage.assignedTo -eq "gold-frontdesk-01" -and $assignedMessage.assignmentVersion -eq 1) @{ assignedTo=$assignedMessage.assignedTo; assignmentVersion=$assignedMessage.assignmentVersion }

    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/assignment-history" -Headers $headers
    Add-Check "Assignment is retained as immutable actor/time evidence" ($history.currentVersion -eq 1 -and $history.events.Count -eq 1 -and $history.events[0].action -eq "assigned" -and $history.events[0].actor -eq "admin" -and $history.events[0].reason -eq "Lifecycle verification assignment") @{ currentVersion=$history.currentVersion; events=$history.events }

    $staleStatus = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/assignment" -Method Put -Headers $headers -ContentType "application/json" -Body (@{ assignedTo="admin"; expectedVersion=0; reason="Stale lifecycle verification" } | ConvertTo-Json) -UseBasicParsing }
    Add-Check "Rejects a stale assignment version" ($staleStatus -eq 409) @{ status=$staleStatus }

    $unassigned = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/assignment" -Method Put -Headers $headers -ContentType "application/json" -Body (@{ assignedTo=$null; expectedVersion=1; reason="Lifecycle verification cleanup" } | ConvertTo-Json)
    $unassignedMessage = $unassigned.detail.messages | Where-Object { $_.id -eq $messageId }
    Add-Check "Requires and records explicit unassignment" ($null -eq $unassignedMessage.assignedTo -and $unassignedMessage.assignmentVersion -eq 2) @{ assignmentVersion=$unassignedMessage.assignmentVersion }

    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/assignment-history" -Headers $headers
    Add-Check "History retains ordered assignment and unassignment events" ($history.currentVersion -eq 2 -and $history.events.Count -eq 2 -and $history.events[0].action -eq "unassigned" -and $history.events[1].action -eq "assigned") @{ currentVersion=$history.currentVersion; actions=@($history.events | ForEach-Object action) }
}
catch {
    Add-Check "Unhandled patient-message assignment lifecycle test error" $false $_.Exception.Message
}
finally {
    try {
        if ($messageId) {
            Invoke-Postgres "delete from message_assignment_events where message_id='$messageId'; delete from messages where id='$messageId' and title='$marker';"
            $residue = [int](Invoke-PostgresScalar "select (select count(*) from messages where id='$messageId') + (select count(*) from message_assignment_events where message_id='$messageId');")
            Add-Check "Synthetic assignment fixture cleanup leaves no data residue" ($residue -eq 0) @{ messageId=$messageId; residue=$residue }
        }
    }
    catch { Add-Check "Synthetic assignment fixture cleanup" $false $_.Exception.Message }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
