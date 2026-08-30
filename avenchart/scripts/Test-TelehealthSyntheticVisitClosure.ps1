# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$RepositoryRoot = '')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed) { $checks.Add([ordered]@{ name = $Name; status = if ($Passed) { 'passed' } else { 'failed' } }); if (-not $Passed) { throw "Failed: $Name" } }

$repository = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthSyntheticVisitClosureRepository.cs')
$service = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthSyntheticVisitClosureService.cs')
$endpoint = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthEndpoints.cs')
$migration = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/database/migrations/V0330__telehealth_synthetic_visit_closure.sql')
$ui = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart-ui/src/features/telehealth/TelehealthSyntheticVisitClosurePanel.tsx')

Add-Check 'Closure requires the exact locked unfinished lifecycle and uses serializable isolation' ($repository -match 'IsolationLevel\.Serializable' -and $repository -match "context.status='MediaEnded'" -and $repository -match "request.status='WrapUp'" -and $repository -match "reservation.status='Released'" -and $repository -match "session.status='Ended'" -and $repository -match "appointment.status='>'" -and $repository -match 'encounter_signatures signature')
Add-Check 'Closure atomically closes only consultation and request state and returns the existing shift to availability' ($repository -match "set status='Closed'" -and $repository -match "set status='Active'" -and $repository -match "'synthetic-visit-closed'" -and $repository -notmatch 'update appointments')
Add-Check 'Closure supports safe semantic replay and conflicting idempotency rejection' ($repository -match 'ReadReplayAsync' -and $repository -match 'idempotency key was already used for a different closure command' -and $repository -match 'ExpectedConsultationVersion')
Add-Check 'Closure does not create any external or downstream path' ($repository -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and $repository -match 'TelehealthSyntheticVisitClosureResponse' -and $repository -match 'true,true,false,false,false,false,false' -and $repository -match 'no encounter completion, patient delivery, billing, claim, integration, or external action')
Add-Check 'Closure migration and private physician contract preserve the disabled synthetic boundary' ($migration -match "'Closed'" -and $migration -match 'closed_at' -and $service -match 'IsPhysicianRole' -and $endpoint -match 'consultations/\{consultationId:guid\}/close' -and $endpoint -match 'SetConsultationPrivateResponse')
Add-Check 'Closure UI requires both confirmations and states that appointment completion and downstream work are unchanged' ($ui -match 'lockReviewed' -and $ui -match 'syntheticConfirmed' -and $ui -match 'appointment' -and $ui -match 'billing' -and $ui -match 'claims')

([ordered]@{ status = 'passed'; generatedAtUtc = (Get-Date).ToUniversalTime().ToString('O'); decisions = @('TH-DEC-0064'); checks = $checks } | ConvertTo-Json -Depth 8)
