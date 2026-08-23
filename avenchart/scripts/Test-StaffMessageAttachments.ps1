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
$marker = "TMP-MESSAGE-ATTACHMENT-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$attachmentContent = "staff attachment verification $marker"

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

    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ patientId="MOD-PAT-0001"; title=$marker; body="Staff attachment lifecycle verification"; assignedTo="admin" } | ConvertTo-Json)
    $messageId = $created.id
    if ([string]::IsNullOrWhiteSpace($messageId)) { throw "The synthetic staff message was not created." }

    $contentBytes = [Text.Encoding]::UTF8.GetBytes($attachmentContent)
    $contentBase64 = [Convert]::ToBase64String($contentBytes)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $expectedSha256 = ([BitConverter]::ToString($sha256.ComputeHash($contentBytes))).Replace("-", "").ToLowerInvariant() }
    finally { $sha256.Dispose() }
    $attachment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/attachments" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ fileName="verification.txt"; contentType="text/plain"; contentBase64=$contentBase64 } | ConvertTo-Json)
    Add-Check "Upload accepts a bounded allowed type with uploader and digest evidence" ($attachment.fileName -eq "verification.txt" -and $attachment.contentType -eq "text/plain" -and $attachment.sizeBytes -eq $contentBytes.Length -and $attachment.sha256 -eq $expectedSha256 -and $attachment.uploadedBy -eq "admin" -and -not [string]::IsNullOrWhiteSpace($attachment.uploadedAt)) @{ attachment=$attachment }

    $attachments = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/attachments" -Headers $headers
    $listed = @($attachments | Where-Object { $_.id -eq $attachment.id }) | Select-Object -First 1
    Add-Check "List returns the uploaded attachment only for its message" ($null -ne $listed -and $listed.sha256 -eq $expectedSha256) @{ attachmentCount=@($attachments).Count; listed=$listed }

    $download = Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/attachments/$($attachment.id)" -Headers $headers -UseBasicParsing
    $downloadedText = if ($download.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($download.Content) } else { [string]$download.Content }
    Add-Check "Download returns the original file content" ($download.StatusCode -eq 200 -and $downloadedText -eq $attachmentContent) @{ status=$download.StatusCode; content=$downloadedText }

    $unsupportedStatus = Get-HttpStatus { Invoke-WebRequest -Uri "$ApiBaseUrl/api/messages/$messageId/attachments" -Method Post -Headers $headers -ContentType "application/json" -Body (@{ fileName="unsupported.exe"; contentType="application/octet-stream"; contentBase64=$contentBase64 } | ConvertTo-Json) -UseBasicParsing }
    Add-Check "Upload rejects unsupported content types" ($unsupportedStatus -eq 400) @{ status=$unsupportedStatus }
}
catch {
    Add-Check "Unhandled staff-message attachment lifecycle test error" $false $_.Exception.Message
}
finally {
    try {
        if ($messageId) {
            Invoke-Postgres "delete from staff_message_attachments where message_id='$messageId'; delete from messages where id='$messageId' and title='$marker';"
            $residue = [int](Invoke-PostgresScalar "select (select count(*) from messages where id='$messageId') + (select count(*) from staff_message_attachments where message_id='$messageId');")
            Add-Check "Synthetic attachment fixture cleanup leaves no data residue" ($residue -eq 0) @{ messageId=$messageId; residue=$residue }
        }
    }
    catch { Add-Check "Synthetic attachment fixture cleanup" $false $_.Exception.Message }
}

$result = [ordered]@{ status=if (@($checks | Where-Object { $_.status -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }; checks=$checks }
$result | ConvertTo-Json -Depth 12
if ($result.status -ne "passed") { exit 1 }
