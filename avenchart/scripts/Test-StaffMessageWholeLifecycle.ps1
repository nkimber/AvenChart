param(
    [string]$ApiBaseUrl = "http://localhost:5001",
    [switch]$IncludeBrowser
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$workspaceRoot = Resolve-Path (Join-Path $solutionRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$messageId = $null
$marker = "TMP-MESSAGE-LIFECYCLE-$([Guid]::NewGuid().ToString('N').Substring(0, 10))"
$artifactPath = Join-Path $solutionRoot "artifacts/latest-staff-message-whole-lifecycle-test.json"

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
}

function Get-HttpStatus([scriptblock]$Operation) {
    try {
        & $Operation | Out-Null
        return 200
    }
    catch {
        return [int]$_.Exception.Response.StatusCode
    }
}

function Invoke-Postgres([string]$Sql) {
    Push-Location $solutionRoot
    try {
        & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U legacy-ehr -d legacy-ehr_modernized -c $Sql | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "PostgreSQL command failed with exit code $LASTEXITCODE." }
    }
    finally {
        Pop-Location
    }
}

function Invoke-PostgresScalar([string]$Sql) {
    Push-Location $solutionRoot
    try {
        $value = & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U legacy-ehr -d legacy-ehr_modernized -Atc $Sql
        if ($LASTEXITCODE -ne 0) { throw "PostgreSQL scalar command failed with exit code $LASTEXITCODE." }
        return ($value | Out-String).Trim()
    }
    finally {
        Pop-Location
    }
}

try {
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"pass"}'
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "The synthetic administrator session was not issued."
    }
    $headers = @{ "X-Legacy EHR-Session" = $login.sessionId }

    $assignees = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/assignees" -Headers $headers
    Add-Check "Active staff directory supports deliberate assignment" (
        @($assignees.assignees | Where-Object { $_.username -eq "gold-frontdesk-01" }).Count -eq 1
    ) @{ count = $assignees.count; policyRevision = $assignees.policyRevision }

    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
        patientId = "MOD-PAT-0001"
        title = $marker
        body = "Whole staff-message lifecycle verification"
        assignedTo = ""
    } | ConvertTo-Json)
    $messageId = $created.id
    if ([string]::IsNullOrWhiteSpace($messageId)) { throw "The synthetic staff message was not created." }
    $createdMessage = @($created.detail.messages | Where-Object { $_.id -eq $messageId }) | Select-Object -First 1
    Add-Check "Create returns an unassigned version-zero message" (
        $null -ne $createdMessage -and $null -eq $createdMessage.assignedTo -and $createdMessage.assignmentVersion -eq 0
    ) @{ messageId = $messageId; assignmentVersion = $createdMessage.assignmentVersion }

    $assigned = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/assignment" -Method Put -Headers $headers -ContentType "application/json" -Body (@{
        assignedTo = "gold-frontdesk-01"
        expectedVersion = 0
        reason = "Route registration follow-up"
    } | ConvertTo-Json)
    $assignedMessage = @($assigned.detail.messages | Where-Object { $_.id -eq $messageId }) | Select-Object -First 1
    $staleAssignmentStatus = Get-HttpStatus {
        Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/assignment" -Method Put -Headers $headers -ContentType "application/json" -Body (@{
            assignedTo = "admin"
            expectedVersion = 0
            reason = "Stale assignment attempt"
        } | ConvertTo-Json) -UseBasicParsing
    }
    Add-Check "Assignment validates active staff and rejects stale versions" (
        $assignedMessage.assignedTo -eq "gold-frontdesk-01" -and
        $assignedMessage.assignmentVersion -eq 1 -and
        $staleAssignmentStatus -eq 409
    ) @{ assignmentVersion = $assignedMessage.assignmentVersion; staleStatus = $staleAssignmentStatus }

    $forwarded = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/forward" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
        assignedTo = "admin"
        expectedVersion = 1
        note = "Return to the clinical inbox"
    } | ConvertTo-Json)
    $forwardedMessage = @($forwarded.detail.messages | Where-Object { $_.id -eq $messageId }) | Select-Object -First 1
    $assignmentHistory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/assignment-history" -Headers $headers
    Add-Check "Forward preserves content and immutable recipient history" (
        $forwardedMessage.assignedTo -eq "admin" -and
        $forwardedMessage.assignmentVersion -eq 2 -and
        $forwardedMessage.body -match "\(admin to admin\) Return to the clinical inbox" -and
        @($assignmentHistory.events | Where-Object { $_.action -eq "forwarded" }).Count -eq 1
    ) @{ currentVersion = $assignmentHistory.currentVersion; actions = @($assignmentHistory.events | ForEach-Object action) }

    $forgedReplyStatus = Get-HttpStatus {
        Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/reply" -Method Put -Headers $headers -ContentType "application/json" -Body (@{
            body = "Forged recipient attempt"
            assignedTo = "gold-frontdesk-01"
        } | ConvertTo-Json) -UseBasicParsing
    }
    $reply = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/reply" -Method Put -Headers $headers -ContentType "application/json" -Body (@{
        body = "Authenticated reply evidence"
        assignedTo = "admin"
    } | ConvertTo-Json)
    $repliedMessage = @($reply.detail.messages | Where-Object { $_.id -eq $messageId }) | Select-Object -First 1
    Add-Check "Reply derives staff ownership from the authenticated session" (
        $forgedReplyStatus -eq 400 -and
        $repliedMessage.body -match "\(admin to admin\) Authenticated reply evidence"
    ) @{ forgedStatus = $forgedReplyStatus; updatedBy = $repliedMessage.updatedBy }

    $attachmentText = "Bounded staff attachment $marker"
    $attachmentBytes = [Text.Encoding]::UTF8.GetBytes($attachmentText)
    $attachment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/attachments" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
        fileName = "$marker.txt"
        contentType = "text/plain"
        contentBase64 = [Convert]::ToBase64String($attachmentBytes)
    } | ConvertTo-Json)
    $download = Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/attachments/$($attachment.id)" -Headers $headers -UseBasicParsing
    $downloadedText = if ($download.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($download.Content) } else { [string]$download.Content }
    Add-Check "Attachment upload and protected download preserve bounded content" (
        $attachment.uploadedBy -eq "admin" -and $attachment.sizeBytes -eq $attachmentBytes.Length -and $downloadedText -eq $attachmentText
    ) @{ attachmentId = $attachment.id; sha256 = $attachment.sha256 }

    $corrected = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/correct" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
        correction = "Clarified lifecycle wording"
        reason = "Preserve an append-only correction"
    } | ConvertTo-Json)
    $correctedMessage = @($corrected.detail.messages | Where-Object { $_.id -eq $messageId }) | Select-Object -First 1
    $correctionHistory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/correction-history" -Headers $headers
    Add-Check "Correction appends text and retains actor, reason, and time" (
        $correctedMessage.body -match "\(admin correction\) Clarified lifecycle wording" -and
        $correctionHistory.events[0].actor -eq "admin" -and
        $correctionHistory.events[0].reason -eq "Preserve an append-only correction"
    ) @{ correctionEvent = $correctionHistory.events[0] }

    $escalated = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/escalate" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
        reason = "Local urgency review"
    } | ConvertTo-Json)
    $duplicateEscalationStatus = Get-HttpStatus {
        Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/escalate" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
            reason = "Duplicate escalation"
        } | ConvertTo-Json) -UseBasicParsing
    }
    $resolved = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/resolve-escalation" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
        reason = "Local urgency reviewed"
    } | ConvertTo-Json)
    Add-Check "Escalation enforces state and retains ordered local evidence" (
        $escalated.events[0].action -eq "escalated" -and
        $duplicateEscalationStatus -eq 400 -and
        $resolved.events[0].action -eq "resolved" -and
        $resolved.events[1].action -eq "escalated"
    ) @{ duplicateStatus = $duplicateEscalationStatus; actions = @($resolved.events | ForEach-Object action) }

    $archived = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/archive" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
        reason = "Lifecycle archive proof"
    } | ConvertTo-Json)
    $activeContainsMessage = @($archived.detail.messages | Where-Object { $_.id -eq $messageId }).Count -ne 0
    $archivedThread = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/MOD-PAT-0001`?includeArchived=true" -Headers $headers
    $archivedMessage = @($archivedThread.messages | Where-Object { $_.id -eq $messageId }) | Select-Object -First 1
    $archivedMutationStatus = Get-HttpStatus {
        Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/assignment" -Method Put -Headers $headers -ContentType "application/json" -Body (@{
            assignedTo = "gold-frontdesk-01"
            expectedVersion = 2
            reason = "Archived mutation attempt"
        } | ConvertTo-Json) -UseBasicParsing
    }
    $archivedAssignmentHistory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/assignment-history" -Headers $headers
    Add-Check "Archive is discoverable and read-only while history remains available" (
        -not $activeContainsMessage -and
        $archivedMessage.deleted -eq 1 -and
        $archivedMutationStatus -eq 404 -and
        $archivedAssignmentHistory.currentVersion -eq 2
    ) @{ archivedMutationStatus = $archivedMutationStatus; assignmentVersion = $archivedAssignmentHistory.currentVersion }

    $restored = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/restore" -Method Post -Headers $headers -ContentType "application/json" -Body (@{
        reason = "Lifecycle restore proof"
    } | ConvertTo-Json)
    $restoredMessage = @($restored.detail.messages | Where-Object { $_.id -eq $messageId }) | Select-Object -First 1
    $retentionHistory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/retention-history" -Headers $headers
    Add-Check "Restore returns the message and preserves archive history" (
        $restoredMessage.deleted -eq 0 -and
        $retentionHistory.events[0].action -eq "restored" -and
        $retentionHistory.events[1].action -eq "archived"
    ) @{ actions = @($retentionHistory.events | ForEach-Object action) }

    if ($IncludeBrowser) {
        Push-Location (Join-Path $workspaceRoot "avenchart-ui")
        try {
            $env:MODERN_UI_RUN_STAFF_MESSAGE_LIFECYCLE = "1"
            $env:MODERN_UI_API_BASE_URL = $ApiBaseUrl
            & npx playwright test e2e/staff-message-whole-lifecycle.spec.ts --project=desktop-chromium --workers=1
            Add-Check "Modern UI staff-message lifecycle browser proof" ($LASTEXITCODE -eq 0) @{ exitCode = $LASTEXITCODE }
        }
        finally {
            Remove-Item Env:MODERN_UI_RUN_STAFF_MESSAGE_LIFECYCLE -ErrorAction SilentlyContinue
            Remove-Item Env:MODERN_UI_API_BASE_URL -ErrorAction SilentlyContinue
            Pop-Location
        }
    }
}
catch {
    Add-Check "Unhandled staff-message whole-lifecycle test error" $false $_.Exception.Message
}
finally {
    try {
        if ($messageId) {
            Invoke-Postgres @"
begin;
delete from staff_message_attachments where message_id = '$messageId';
delete from message_correction_events where message_id = '$messageId';
delete from message_retention_events where message_id = '$messageId';
delete from message_escalation_events where message_id = '$messageId';
delete from message_assignment_events where message_id = '$messageId';
delete from messages where id = '$messageId' and title = '$marker';
commit;
"@
        }
        $messageIdLiteral = if ($messageId) { $messageId.Replace("'", "''") } else { "__missing__" }
        $residue = [int](Invoke-PostgresScalar @"
select
  (select count(*) from messages where id = '$messageIdLiteral' or title like 'TMP-MESSAGE-LIFECYCLE-%')
  + (select count(*) from message_assignment_events where message_id = '$messageIdLiteral')
  + (select count(*) from staff_message_attachments where file_name like 'TMP-MESSAGE-LIFECYCLE-%')
  + (select count(*) from message_correction_events where message_id = '$messageIdLiteral')
  + (select count(*) from message_retention_events where message_id = '$messageIdLiteral')
  + (select count(*) from message_escalation_events where message_id = '$messageIdLiteral');
"@)
        Add-Check "Synthetic whole-lifecycle fixture cleanup leaves zero residue" ($residue -eq 0) @{ messageId = $messageId; residue = $residue }
    }
    catch {
        Add-Check "Synthetic whole-lifecycle fixture cleanup" $false $_.Exception.Message
    }
}

$result = [ordered]@{
    status = if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }
    marker = $marker
    checks = $checks
}
New-Item -ItemType Directory -Force (Split-Path $artifactPath) | Out-Null
$result | ConvertTo-Json -Depth 12 | Set-Content -Path $artifactPath -Encoding utf8
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
