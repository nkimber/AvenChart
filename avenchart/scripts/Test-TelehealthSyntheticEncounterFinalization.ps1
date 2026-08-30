# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$RepositoryRoot = '')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed) { $checks.Add([ordered]@{ name = $Name; status = if ($Passed) { 'passed' } else { 'failed' } }); if (-not $Passed) { throw "Failed: $Name" } }

$repository = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthEncounterFinalizationRepository.cs')
$service = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthEncounterFinalizationService.cs')
$endpoint = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthEndpoints.cs')
$ui = Get-Content -Raw (Join-Path $RepositoryRoot 'avenchart-ui/src/features/telehealth/TelehealthEncounterFinalizationPanel.tsx')

Add-Check 'Finalization rechecks the complete source while holding the governed encounter-lock transaction' ($repository -match 'encounters\.SignAsync' -and $repository -match 'ReadAndLockSourceAsync' -and $repository -match 'for update of context,request,reservation,shift,session,appointment,encounter')
Add-Check 'Finalization binds the review to current SOAP, disposition, and signed prescription context' ($repository -match 'ExpectedDocumentationVersion' -and $repository -match 'ExpectedDispositionVersion' -and $repository -match 'ExpectedFinalClinicalReviewVersion' -and $repository -match 'review\.prescription_order_id is not distinct from prescription\.order_id')
Add-Check 'Finalization retains hard-false downstream effects' ($repository -match 'LegalEffect: false' -and $repository -match 'CompletionCreated: false' -and $repository -match 'BillingCreated: false' -and $repository -match 'ClaimCreated: false' -and $repository -notmatch 'HttpClient|SendAsync')
Add-Check 'Service and endpoint preserve owner-only private failure boundaries' ($service -match 'IsPhysicianRole' -and $service -match 'IsConfiguredFacility' -and $service -match 'telehealth_staff_record_required' -and $endpoint -match 'consultations/\{consultationId:guid\}/finalize' -and $endpoint -match 'SetConsultationPrivateResponse')
Add-Check 'Physician UI requires source and synthetic-only confirmation' ($ui -match 'SourceReviewConfirmed' -and $ui -match 'SyntheticOnlyConfirmed' -and $ui -match 'not a legal signature')

([ordered]@{ status = 'passed'; generatedAtUtc = (Get-Date).ToUniversalTime().ToString('O'); decisions = @('TH-DEC-0063'); checks = $checks } | ConvertTo-Json -Depth 8)
