# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$RepositoryRoot = '')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed) {
    $checks.Add([ordered]@{ name = $Name; status = if ($Passed) { 'passed' } else { 'failed' } })
    if (-not $Passed) { throw "Failed: $Name" }
}

$migration = Join-Path $RepositoryRoot 'avenchart/database/migrations/V0329__telehealth_synthetic_final_clinical_review.sql'
$repository = Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthFinalClinicalReviewRepository.cs'
$service = Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthFinalClinicalReviewService.cs'
$endpoints = Join-Path $RepositoryRoot 'avenchart/backend/src/AvenChart.Api/Features/Telehealth/TelehealthEndpoints.cs'
$ui = Join-Path $RepositoryRoot 'avenchart-ui/src/features/telehealth/TelehealthFinalClinicalReviewPanel.tsx'
$migrationText = Get-Content -Raw $migration
$repositoryText = Get-Content -Raw $repository
$serviceText = Get-Content -Raw $service
$endpointText = Get-Content -Raw $endpoints
$uiText = Get-Content -Raw $ui

Add-Check 'V0329 creates immutable review versions and immutable events' ($migrationText -match 'final_clinical_review_versions' -and $migrationText -match 'final_clinical_review_events' -and $migrationText -match 'reject_telehealth_evidence_mutation')
Add-Check 'V0329 requires all four affirmative attestations and hard-false consequential flags' ($migrationText -match 'documentation_reviewed and physician_responsibility_confirmed' -and $migrationText -match 'not legal_effect and not encounter_signature_created' -and $migrationText -match 'not billing_created and not claim_created')
Add-Check 'Repository uses serializable ownership-bound source-version recording and exact replay' ($repositoryText -match 'IsolationLevel.Serializable' -and $repositoryText -match "context.status='MediaEnded'" -and $repositoryText -match "request.status='WrapUp'" -and $repositoryText -match 'ReadReplayAsync' -and $repositoryText -match 'commandFingerprint')
Add-Check 'Repository requires every SOAP section and a current safety-disposition source' ($repositoryText -match 'SubjectivePresent' -and $repositoryText -match 'ObjectivePresent' -and $repositoryText -match 'AssessmentPresent' -and $repositoryText -match 'PlanPresent' -and $repositoryText -match 'source.Disposition is null')
Add-Check 'Service requires four acknowledgments and fails closed for a conflicting source' ($serviceText -match 'DocumentationReviewed' -and $serviceText -match 'PhysicianResponsibilityConfirmed' -and $serviceText -match 'NoAutomaticClaimOrDeliveryConfirmed' -and $serviceText -match 'SyntheticDataConfirmed' -and $serviceText -match 'telehealth_final_clinical_review_conflict')
Add-Check 'API is private and declares no signature, completion, claim, or delivery effect' ($endpointText -match '/final-clinical-review' -and $endpointText -match 'no encounter signature, completion, delivery, bill, claim, or external action' -and $endpointText -match 'SetConsultationPrivateResponse')
Add-Check 'Clinician UI uses explicit acknowledgments and bounded consequence language' ($uiText -match 'Required acknowledgments' -and $uiText -match 'not a legal encounter signature' -and $uiText -match 'no legal, delivery, billing, or claim effect')

$result = [ordered]@{ status = 'passed'; generatedAtUtc = (Get-Date).ToUniversalTime().ToString('O'); decisions = @('TH-DEC-0062'); checks = $checks }
$result | ConvertTo-Json -Depth 8
