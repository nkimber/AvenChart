# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
Add-Type -AssemblyName System.Net.Http

$target = [Uri]$ApiBaseUrl
if ($target.Host -notin @("localhost", "127.0.0.1", "::1")) {
    throw "This integration idempotency proof writes synthetic messages and is restricted to a local API target."
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $solutionRoot "artifacts"
$resultPath = Join-Path $artifactsRoot "latest-integration-idempotency-content-test.json"
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null

$checks = [System.Collections.Generic.List[object]]::new()
$status = "passed"

function Add-Check {
    param([string]$Name, [bool]$Passed, [object]$Details = $null)

    $script:checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
    if (-not $Passed) { $script:status = "failed" }
}

function Get-HttpStatus {
    param(
        [string]$Uri,
        [string]$Method,
        [hashtable]$RequestHeaders = @{},
        [object]$Body = $null
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(20)
        $request = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($Method), $Uri)
        foreach ($entry in $RequestHeaders.GetEnumerator()) {
            $request.Headers.TryAddWithoutValidation([string]$entry.Key, [string]$entry.Value) | Out-Null
        }
        if ($null -ne $Body) {
            $request.Content = [System.Net.Http.StringContent]::new(
                ($Body | ConvertTo-Json -Depth 10),
                [Text.Encoding]::UTF8,
                "application/json")
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try { return [int]$response.StatusCode } finally { $response.Dispose() }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 15
    Add-Check "API readiness" ($health.status -eq "healthy") $health

    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    $headers = New-AvenChartStaffAccessContextHeaders -Login $login
    $marker = [Guid]::NewGuid().ToString("N")
    $outboxKey = "idempotency-proof-$marker"
    $source = "phase3-idempotency-proof-$marker"
    $sourceMessageId = "message-$marker"

    $outboxRequest = @{
        eventType = "integration.idempotency.proof"
        aggregateType = "synthetic-verification"
        aggregateId = $marker
        destination = "local://success"
        payload = @{ fixture = "synthetic-no-phi"; revision = 1 }
        idempotencyKey = $outboxKey
    }
    $firstOutbox = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox" -Method Post `
        -Headers $headers -ContentType "application/json" `
        -Body ($outboxRequest | ConvertTo-Json -Depth 10) -TimeoutSec 20
    $replayedOutbox = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/outbox" -Method Post `
        -Headers $headers -ContentType "application/json" `
        -Body ($outboxRequest | ConvertTo-Json -Depth 10) -TimeoutSec 20
    $conflictingOutboxRequest = $outboxRequest.Clone()
    $conflictingOutboxRequest.payload = @{ fixture = "synthetic-no-phi"; revision = 2 }
    $outboxConflictStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/integrations/outbox" -Method Post `
        -RequestHeaders $headers -Body $conflictingOutboxRequest
    $missingKeyRequest = $outboxRequest.Clone()
    $missingKeyRequest.Remove("idempotencyKey")
    $missingKeyStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/integrations/outbox" -Method Post `
        -RequestHeaders $headers -Body $missingKeyRequest

    Add-Check "Outbox exact replay returns the original event and altered content is rejected" (
        $firstOutbox.eventId -eq $replayedOutbox.eventId -and
        $outboxConflictStatus -eq 409 -and
        $missingKeyStatus -eq 400
    ) @{
        originalEventId = $firstOutbox.eventId
        replayedEventId = $replayedOutbox.eventId
        conflictStatus = $outboxConflictStatus
        missingKeyStatus = $missingKeyStatus
    }

    $inboxRequest = @{
        source = $source
        sourceMessageId = $sourceMessageId
        messageType = "integration.idempotency.proof"
        payload = @{ fixture = "synthetic-no-phi"; revision = 1 }
    }
    $firstInbox = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/inbox" -Method Post `
        -Headers $headers -ContentType "application/json" `
        -Body ($inboxRequest | ConvertTo-Json -Depth 10) -TimeoutSec 20
    $replayedInbox = Invoke-RestMethod -Uri "$ApiBaseUrl/api/integrations/inbox" -Method Post `
        -Headers $headers -ContentType "application/json" `
        -Body ($inboxRequest | ConvertTo-Json -Depth 10) -TimeoutSec 20
    $conflictingInboxRequest = $inboxRequest.Clone()
    $conflictingInboxRequest.payload = @{ fixture = "synthetic-no-phi"; revision = 2 }
    $inboxConflictStatus = Get-HttpStatus -Uri "$ApiBaseUrl/api/integrations/inbox" -Method Post `
        -RequestHeaders $headers -Body $conflictingInboxRequest

    Add-Check "Inbox exact replay is marked duplicate and altered content is rejected" (
        $firstInbox.inboxId -eq $replayedInbox.inboxId -and
        $replayedInbox.duplicate -eq $true -and
        $inboxConflictStatus -eq 409
    ) @{
        originalInboxId = $firstInbox.inboxId
        replayedInboxId = $replayedInbox.inboxId
        duplicate = $replayedInbox.duplicate
        conflictStatus = $inboxConflictStatus
    }

    Push-Location $solutionRoot
    try {
        $conflicts = docker compose exec -T postgres psql -U avenchart -d avenchart -t -A -F '|' -v ON_ERROR_STOP=1 -c "
            select conflict.direction || '|' || count(*)
            from integration_idempotency_conflicts conflict
            left join integration_outbox outbox on outbox.event_id = conflict.outbox_event_id
            left join integration_inbox inbox on inbox.inbox_id = conflict.inbox_id
            where outbox.idempotency_key = '$outboxKey' or inbox.source = '$source'
            group by conflict.direction
            order by conflict.direction;"
        if ($LASTEXITCODE -ne 0) { throw "Conflict-audit query failed." }
    }
    finally { Pop-Location }
    $conflictRows = @($conflicts | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    Add-Check "Conflicting integration identities retain digest-only audit evidence" (
        ($conflictRows -contains "inbox|1") -and ($conflictRows -contains "outbox|1")
    ) @{ conflicts = $conflictRows }
}
catch {
    Add-Check "Unhandled integration idempotency content test error" $false $_.Exception.Message
}
finally {
    $result = [ordered]@{
        status = $status
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
        checks = $checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}

if ($status -ne "passed") { exit 1 }
