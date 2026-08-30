# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$RepositoryRoot = '')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed) { $checks.Add([ordered]@{ name = $Name; status = if ($Passed) { 'passed' } else { 'failed' } }); if (-not $Passed) { throw "Failed: $Name" } }

$projector = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthPatientQueueStatusProjector.cs')
$policy = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestQueueStatusPolicy.cs')
$repository = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthApplicantRequestQueueStatusRepository.cs')
$polling = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart-ui/src/features/telehealth/polling.ts')
$ui = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart-ui/src/features/telehealth/ApplicantTelehealthQueueStatus.tsx')

Add-Check 'Closed has explicit truthful terminal status content without completion claims' ($projector -match 'SyntheticLifecycleClosed' -and $projector -match 'appointment or encounter is complete' -and $projector -match 'prescription delivery, bill, claim, or external action')
Add-Check 'Applicant closure status remains an explicitly bounded visible state with no physician assignment disclosure' ($policy -match 'TelehealthRequestStatus\.Closed' -and $policy -match 'appointment and encounter remain incomplete' -and $policy -match 'RenderingPhysicianAssigned: assigned')
Add-Check 'Applicant closure status requires closed lifecycle, returned shift, encounter lock, and closure-event provenance' ($repository -match "r.status='Closed'" -and $repository -match "consultation.status='Closed'" -and $repository -match "shift.status='Active'" -and $repository -match 'encounter_signatures signature' -and $repository -match "action='synthetic-visit-closed'")
Add-Check 'Terminal closure stops browser polling and exposes no terminal connection control' ($polling -notmatch "'Closed'" -and $ui -match "status.requestStatus === 'Reserved' \|\| status.requestStatus === 'Connecting'")
Add-Check 'Closure-status projection has no outbound implementation path' ($projector -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and $policy -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync' -and $repository -notmatch 'HttpClient|ClientWebSocket|SmtpClient|HubConnection|WebRequest|SendAsync')

([ordered]@{ status = 'passed'; generatedAtUtc = (Get-Date).ToUniversalTime().ToString('O'); decisions = @('TH-DEC-0065'); checks = $checks } | ConvertTo-Json -Depth 8)
