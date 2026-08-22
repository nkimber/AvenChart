# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param([string]$ApiBaseUrl = "http://localhost:5001")
$ErrorActionPreference = "Stop"; $root = Resolve-Path (Join-Path $PSScriptRoot ".."); $checks = [Collections.Generic.List[object]]::new(); $messageId = $null; $marker = "TMP-MESSAGE-ARCHIVE-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
function Add-Check([string]$Name,[bool]$Passed,[object]$Details) { $checks.Add([ordered]@{name=$Name;status=if($Passed){'passed'}else{'failed'};details=$Details}) }
function Sql([string]$Query) { Push-Location $root; try { & docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 -U avenchart -d avenchart -c $Query | Out-Null } finally { Pop-Location } }
try {
  $login=Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body '{"username":"admin","password":"pass"}'; $headers=New-AvenChartStaffAccessContextHeaders -Login $login
  $created=Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{patientId='MOD-PAT-0001';title=$marker;body='Archive lifecycle verification';assignedTo='admin'}|ConvertTo-Json); $messageId=$created.id
  $archived=Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/archive" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{reason='Resolve obsolete duplicate'}|ConvertTo-Json)
  Add-Check 'Archive removes the message from active thread results' (-not (@($archived.detail.messages|Where-Object{$_.id -eq $messageId}))) @{messageId=$messageId}
  $history=Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/retention-history" -Headers $headers; $archiveEvent=@($history.events|Where-Object{$_.action -eq 'archived'})|Select-Object -First 1
  Add-Check 'Archive records immutable reason, actor, and time evidence' ($null -ne $archiveEvent -and $archiveEvent.reason -eq 'Resolve obsolete duplicate' -and $archiveEvent.actor -eq 'admin' -and $archiveEvent.occurredAt) @{event=$archiveEvent}
  $restored=Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/restore" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{reason='Retain for patient continuity'}|ConvertTo-Json)
  Add-Check 'Restore returns the original message to active thread results' ($null -ne (@($restored.detail.messages|Where-Object{$_.id -eq $messageId})|Select-Object -First 1)) @{messageId=$messageId}
  $history=Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$messageId/retention-history" -Headers $headers; Add-Check 'Restore adds separately immutable evidence' (@($history.events|Where-Object{$_.action -eq 'restored'}).Count -eq 1) @{events=$history.events}
} catch { Add-Check 'Unhandled staff-message archive lifecycle test error' $false $_.Exception.Message }
finally { try { if($messageId){ Sql "delete from message_retention_events where message_id='$messageId'; delete from messages where id='$messageId' and title='$marker';"; Add-Check 'Synthetic archive fixture cleanup completes' $true @{messageId=$messageId} } } catch { Add-Check 'Synthetic archive fixture cleanup' $false $_.Exception.Message } }
$result=[ordered]@{status=if(@($checks|Where-Object{$_.status -ne 'passed'}).Count -eq 0){'passed'}else{'failed'};checks=$checks};$result|ConvertTo-Json -Depth 12;if($result.status -ne 'passed'){exit 1}
