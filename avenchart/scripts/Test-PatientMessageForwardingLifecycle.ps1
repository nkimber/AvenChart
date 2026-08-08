# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$messageId = $null
$marker = "TMP-MESSAGE-FORWARD-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

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

    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ patientId="MOD-PAT-0001"; title=$marker; body="Legacy-compatible forwarding verification"; assignedTo="admin" } | ConvertTo-Json)
    $messageId = $created.id
    if ([string]::IsNullOrWhiteSpace($messageId)) { throw "The synthetic forwarding message was not created." }

    $forwarded = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/forward" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ assignedTo="gold-frontdesk-01"; expectedVersion=0; note="Please complete registration follow-up" } | ConvertTo-Json)
    $forwardedMessage = $forwarded.detail.messages | Where-Object { $_.id -eq $messageId }
    $bodyHasLegacyShape = $forwardedMessage.body -match '\(admin to gold-frontdesk-01\) Please complete registration follow-up'
    Add-Check "Forward preserves the original message and appends a legacy-compatible sender-to-recipient entry" ($forwardedMessage.id -eq $messageId -and $bodyHasLegacyShape) @{ messageId=$forwardedMessage.id; body=$forwardedMessage.body }
    Add-Check "Forward changes the active recipient and increments its version" ($forwardedMessage.assignedTo -eq "gold-frontdesk-01" -and $forwardedMessage.assignmentVersion -eq 1) @{ assignedTo=$forwardedMessage.assignedTo; assignmentVersion=$forwardedMessage.assignmentVersion }

    $history = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/assignment-history" -Headers $headers
    $event = @($history.events | Where-Object { $_.action -eq "forwarded" }) | Select-Object -First 1
    Add-Check "Forward records immutable actor, previous recipient, target, note, and time evidence" ($history.currentVersion -eq 1 -and $null -ne $event -and $event.actor -eq "admin" -and $event.previousAssignedTo -eq "admin" -and $event.assignedTo -eq "gold-frontdesk-01" -and $event.reason -eq "Please complete registration follow-up" -and -not [string]::IsNullOrWhiteSpace($event.occurredAt)) @{ currentVersion=$history.currentVersion; event=$event }

    $staleStatus = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/forward" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ assignedTo="admin"; expectedVersion=0; note="Stale forwarding verification" } | ConvertTo-Json) -UseBasicParsing }
    Add-Check "Forward rejects a stale assignment version" ($staleStatus -eq 409) @{ status=$staleStatus }

    $sameRecipientStatus = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/forward" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ assignedTo="gold-frontdesk-01"; expectedVersion=1; note="Same recipient verification" } | ConvertTo-Json) -UseBasicParsing }
    Add-Check "Forward rejects the current recipient as a no-op" ($sameRecipientStatus -eq 400) @{ status=$sameRecipientStatus }
}
catch {
    Add-Check "Unhandled patient-message forwarding lifecycle test error" $false $_.Exception.Message
}
finally {
    try {
        if ($messageId) {
            Invoke-Postgres "delete from message_assignment_events where message_id='$messageId'; delete from messages where id='$messageId' and title='$marker';"
            $residue = [int](Invoke-PostgresScalar "select (select count(*) from messages where id='$messageId') + (select count(*) from message_assignment_events where message_id='$messageId');")
            Add-Check "Synthetic forwarding fixture cleanup leaves no data residue" ($residue -eq 0) @{ messageId=$messageId; residue=$residue }
        }
    }
    catch { Add-Check "Synthetic forwarding fixture cleanup" $false $_.Exception.Message }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
