# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',

    [ValidateSet('None', 'DropFirstCoverage', 'BreakWireframeLabel', 'ExpireDecision')]
    [string]$TestMutation = 'None'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptPath = if (-not [string]::IsNullOrWhiteSpace($PSCommandPath)) {
        $PSCommandPath
    }
    else {
        $MyInvocation.MyCommand.Path
    }

    if ([string]::IsNullOrWhiteSpace($scriptPath)) {
        throw 'RepositoryRoot is required when the validator script path is unavailable.'
    }

    $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $scriptPath)
}

$validatorVersion = '3.18.0'
$startedAt = [DateTime]::UtcNow
$checks = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[string]]::new()

function Add-ValidationCheck {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [bool]$Passed,

        [object]$Details = $null
    )

    $checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { 'passed' } else { 'failed' }
        details = $Details
    })

    if (-not $Passed) {
        $failures.Add($Name)
    }
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Test-ExactSet {
    param(
        [object[]]$Actual,
        [object[]]$Expected
    )

    return @(Compare-Object -ReferenceObject @($Expected) -DifferenceObject @($Actual)).Count -eq 0
}

function Get-RepositoryRelativePath {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootUri = [Uri]::new(($Root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar))
    $pathUri = [Uri]::new([IO.Path]::GetFullPath($Path))
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString())
}

$resolvedRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$telehealthRoot = Join-Path $resolvedRoot 'docs/telehealth'
$backlogPath = Join-Path $telehealthRoot 'backlog/backlog.json'
$safeguardsPath = Join-Path $telehealthRoot 'backlog/safeguards.json'
$wireframePath = Join-Path $telehealthRoot 'wireframes/telehealth-wireframes.html'
$decisionOnePath = Join-Path $telehealthRoot 'decisions/0001-g0-development-baseline.md'
$decisionTwoPath = Join-Path $telehealthRoot 'decisions/0002-proposed-scoped-verification-authorization.md'
$decisionThreePath = Join-Path $telehealthRoot 'decisions/0003-proposed-sprint-01-synthetic-foundation.md'
$decisionFourPath = Join-Path $telehealthRoot 'decisions/0004-proposed-bootstrap-schema-reconciliation.md'
$decisionFivePath = Join-Path $telehealthRoot 'decisions/0005-approved-sprint-02-established-patient-readiness.md'
$decisionSixPath = Join-Path $telehealthRoot 'decisions/0006-approved-sprint-03-patient-queue-transparency.md'
$decisionSevenPath = Join-Path $telehealthRoot 'decisions/0007-approved-sprint-04-prospective-patient-identity-shell.md'
$decisionEightPath = Join-Path $telehealthRoot 'decisions/0008-approved-sprint-05-connection-room-shell.md'
$decisionNinePath = Join-Path $telehealthRoot 'decisions/0009-approved-sprint-06-consultation-start-handoff.md'
$decisionTenPath = Join-Path $telehealthRoot 'decisions/0010-approved-sprint-07-read-only-consultation-workspace.md'
$decisionElevenPath = Join-Path $telehealthRoot 'decisions/0011-approved-sprint-08-consultation-documentation-draft.md'
$decisionTwelvePath = Join-Path $telehealthRoot 'decisions/0012-approved-sprint-09-consultation-wrap-up-handoff.md'
$decisionThirteenPath = Join-Path $telehealthRoot 'decisions/0013-approved-sprint-10-synthetic-pharmacy-choice.md'
$decisionFourteenPath = Join-Path $telehealthRoot 'decisions/0014-approved-sprint-11-synthetic-safety-disposition-draft.md'
$decisionFifteenPath = Join-Path $telehealthRoot 'decisions/0015-approved-sprint-12-completion-prerequisites-review.md'
$decisionSixteenPath = Join-Path $telehealthRoot 'decisions/0016-approved-sprint-13-synthetic-prescription-preparation-draft.md'
$decisionSeventeenPath = Join-Path $telehealthRoot 'decisions/0017-approved-sprint-14-synthetic-applicant-identity-review.md'
$decisionEighteenPath = Join-Path $telehealthRoot 'decisions/0018-approved-sprint-15-prospective-safety-triage.md'
$decisionNineteenPath = Join-Path $telehealthRoot 'decisions/0019-approved-sprint-16-prospective-visit-purpose.md'
$decisionTwentyPath = Join-Path $telehealthRoot 'decisions/0020-approved-sprint-17-prospective-practice-network-precheck.md'
$decisionTwentyOnePath = Join-Path $telehealthRoot 'decisions/0021-approved-sprint-18-prospective-member-insurance-details.md'
$decisionTwentyTwoPath = Join-Path $telehealthRoot 'decisions/0022-approved-sprint-19-synthetic-prospective-eligibility-result.md'
$decisionTwentyThreePath = Join-Path $telehealthRoot 'decisions/0023-approved-sprint-20-synthetic-practice-network-determination.md'
$decisionTwentyFourPath = Join-Path $telehealthRoot 'decisions/0024-approved-sprint-21-synthetic-identity-proofing-process.md'
$decisionTwentyFivePath = Join-Path $telehealthRoot 'decisions/0025-approved-sprint-22-synthetic-promotion-authorization.md'
$decisionTwentySixPath = Join-Path $telehealthRoot 'decisions/0026-approved-sprint-23-atomic-synthetic-patient-promotion.md'
$decisionTwentySevenPath = Join-Path $telehealthRoot 'decisions/0027-approved-sprint-24-state-specific-telehealth-notice-acknowledgment.md'
$decisionTwentyEightPath = Join-Path $telehealthRoot 'decisions/0028-approved-sprint-25-minimum-registration-details-confirmation.md'
$decisionTwentyNinePath = Join-Path $telehealthRoot 'decisions/0029-approved-sprint-26-synthetic-insurance-handoff-confirmation.md'
$decisionThirtyPath = Join-Path $telehealthRoot 'decisions/0030-approved-sprint-27-synthetic-communication-access-readiness.md'
$decisionThirtyOnePath = Join-Path $telehealthRoot 'decisions/0031-approved-sprint-28-synthetic-device-preparation.md'
$decisionThirtyTwoPath = Join-Path $telehealthRoot 'decisions/0032-approved-sprint-29-synthetic-clinical-information-inventory.md'
$decisionThirtyThreePath = Join-Path $telehealthRoot 'decisions/0033-approved-sprint-30-synthetic-medication-information.md'
$decisionThirtyFourPath = Join-Path $telehealthRoot 'decisions/0034-approved-sprint-31-synthetic-allergy-information.md'
$decisionThirtyFivePath = Join-Path $telehealthRoot 'decisions/0035-approved-sprint-32-synthetic-health-history-topics.md'
$decisionThirtySixPath = Join-Path $telehealthRoot 'decisions/0036-approved-sprint-33-synthetic-clinical-information-summary-confirmation.md'
$decisionThirtySevenPath = Join-Path $telehealthRoot 'decisions/0037-approved-sprint-34-synthetic-pre-request-readiness-acknowledgment.md'
$decisionThirtyEightPath = Join-Path $telehealthRoot 'decisions/0038-approved-sprint-35-synthetic-practice-review-submission.md'
$decisionThirtyNinePath = Join-Path $telehealthRoot 'decisions/0039-approved-sprint-36-read-only-practice-review-inbox.md'
$decisionFortyPath = Join-Path $telehealthRoot 'decisions/0040-approved-sprint-37-synthetic-practice-review-claim.md'
$decisionFortyOnePath = Join-Path $telehealthRoot 'decisions/0041-approved-sprint-38-claimant-bound-practice-review-packet.md'
$decisionFortyTwoPath = Join-Path $telehealthRoot 'decisions/0042-approved-sprint-39-synthetic-practice-review-authorization.md'
$decisionFortyThreePath = Join-Path $telehealthRoot 'decisions/0043-approved-sprint-40-applicant-bound-request-creation.md'
$decisionFortyFourPath = Join-Path $telehealthRoot 'decisions/0044-approved-sprint-41-applicant-request-location-confirmation.md'
$decisionFortyFivePath = Join-Path $telehealthRoot 'decisions/0045-approved-sprint-42-applicant-request-universal-safety-assessment.md'
$decisionFortySixPath = Join-Path $telehealthRoot 'decisions/0046-approved-sprint-43-applicant-request-complaint-triage.md'
$decisionFortySevenPath = Join-Path $telehealthRoot 'decisions/0047-approved-sprint-44-applicant-request-intake-snapshot-confirmation.md'
$decisionFortyEightPath = Join-Path $telehealthRoot 'decisions/0048-approved-sprint-45-applicant-request-insurance-source-confirmation.md'
$decisionFortyNinePath = Join-Path $telehealthRoot 'decisions/0049-approved-sprint-46-applicant-request-eligibility-verification.md'
$decisionFiftyPath = Join-Path $telehealthRoot 'decisions/0050-approved-sprint-47-applicant-request-practice-network-verification.md'
$decisionFiftyOnePath = Join-Path $telehealthRoot 'decisions/0051-approved-sprint-48-applicant-request-rendering-candidate-selection.md'
$decisionFiftyTwoPath = Join-Path $telehealthRoot 'decisions/0052-approved-sprint-49-applicant-request-participation-context.md'
$decisionFiftyThreePath = Join-Path $telehealthRoot 'decisions/0053-approved-sprint-50-applicant-request-participation-evaluation.md'
$decisionFiftyFourPath = Join-Path $telehealthRoot 'decisions/0054-approved-sprint-51-applicant-request-operational-review-submission.md'
$sprintTwentyNinePath = Join-Path $telehealthRoot 'backlog/sprint-29-synthetic-clinical-information-inventory.md'
$sprintTwentyNineEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-29-evidence.md'
$sprintThirtyPath = Join-Path $telehealthRoot 'backlog/sprint-30-synthetic-medication-information.md'
$sprintThirtyEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-30-evidence.md'
$sprintThirtyOnePath = Join-Path $telehealthRoot 'backlog/sprint-31-synthetic-allergy-information.md'
$sprintThirtyOneEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-31-evidence.md'
$sprintThirtyTwoPath = Join-Path $telehealthRoot 'backlog/sprint-32-synthetic-health-history-topics.md'
$sprintThirtyTwoEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-32-evidence.md'
$sprintThirtyThreePath = Join-Path $telehealthRoot 'backlog/sprint-33-synthetic-clinical-information-summary-confirmation.md'
$sprintThirtyThreeEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-33-evidence.md'
$sprintThirtyFourPath = Join-Path $telehealthRoot 'backlog/sprint-34-synthetic-pre-request-readiness-acknowledgment.md'
$sprintThirtyFourEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-34-evidence.md'
$sprintThirtyFivePath = Join-Path $telehealthRoot 'backlog/sprint-35-synthetic-practice-review-submission.md'
$sprintThirtyFiveEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-35-evidence.md'
$sprintThirtySixPath = Join-Path $telehealthRoot 'backlog/sprint-36-read-only-practice-review-inbox.md'
$sprintThirtySixEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-36-evidence.md'
$sprintThirtySevenPath = Join-Path $telehealthRoot 'backlog/sprint-37-synthetic-practice-review-claim.md'
$sprintThirtySevenEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-37-evidence.md'
$sprintThirtyEightPath = Join-Path $telehealthRoot 'backlog/sprint-38-claimant-bound-practice-review-packet.md'
$sprintThirtyEightEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-38-evidence.md'
$sprintThirtyNinePath = Join-Path $telehealthRoot 'backlog/sprint-39-synthetic-practice-review-authorization.md'
$sprintThirtyNineEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-39-evidence.md'
$sprintFortyPath = Join-Path $telehealthRoot 'backlog/sprint-40-applicant-bound-request-creation.md'
$sprintFortyEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-40-evidence.md'
$sprintFortyOnePath = Join-Path $telehealthRoot 'backlog/sprint-41-applicant-request-location-confirmation.md'
$sprintFortyOneEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-41-evidence.md'
$sprintFortyTwoPath = Join-Path $telehealthRoot 'backlog/sprint-42-applicant-request-universal-safety-assessment.md'
$sprintFortyTwoEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-42-evidence.md'
$sprintFortyThreePath = Join-Path $telehealthRoot 'backlog/sprint-43-applicant-request-complaint-triage.md'
$sprintFortyThreeEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-43-evidence.md'
$sprintFortyFourPath = Join-Path $telehealthRoot 'backlog/sprint-44-applicant-request-intake-snapshot-confirmation.md'
$sprintFortyFourEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-44-evidence.md'
$sprintFortyFivePath = Join-Path $telehealthRoot 'backlog/sprint-45-applicant-request-insurance-source-confirmation.md'
$sprintFortyFiveEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-45-evidence.md'
$sprintFortySixPath = Join-Path $telehealthRoot 'backlog/sprint-46-applicant-request-eligibility-verification.md'
$sprintFortySixEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-46-evidence.md'
$sprintFortySevenPath = Join-Path $telehealthRoot 'backlog/sprint-47-applicant-request-practice-network-verification.md'
$sprintFortySevenEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-47-evidence.md'
$sprintFortyEightPath = Join-Path $telehealthRoot 'backlog/sprint-48-applicant-request-rendering-candidate-selection.md'
$sprintFortyEightEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-48-evidence.md'
$sprintFortyNinePath = Join-Path $telehealthRoot 'backlog/sprint-49-applicant-request-participation-context.md'
$sprintFortyNineEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-49-evidence.md'
$sprintFiftyPath = Join-Path $telehealthRoot 'backlog/sprint-50-applicant-request-participation-evaluation.md'
$sprintFiftyEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-50-evidence.md'
$sprintFiftyOnePath = Join-Path $telehealthRoot 'backlog/sprint-51-applicant-request-operational-review-submission.md'
$sprintFiftyOneEvidencePath = Join-Path $telehealthRoot 'backlog/sprint-51-evidence.md'
$workflowPath = Join-Path $resolvedRoot '.github/workflows/verify.yml'

Add-ValidationCheck -Name 'Telehealth planning root exists' -Passed (Test-Path -LiteralPath $telehealthRoot -PathType Container) -Details 'docs/telehealth'

$requiredFiles = @(
    $backlogPath,
    $safeguardsPath,
    $wireframePath,
    $decisionOnePath,
    $decisionTwoPath,
    $decisionThreePath,
    $decisionFourPath,
    $decisionFivePath,
    $decisionSixPath,
    $decisionSevenPath,
    $decisionEightPath,
    $decisionNinePath,
    $decisionTenPath,
    $decisionElevenPath,
    $decisionTwelvePath,
    $decisionThirteenPath,
    $decisionFourteenPath,
    $decisionFifteenPath,
    $decisionSixteenPath,
    $decisionSeventeenPath,
    $decisionEighteenPath,
    $decisionNineteenPath,
    $decisionTwentyPath,
    $decisionTwentyOnePath,
    $decisionTwentyTwoPath,
    $decisionTwentyThreePath,
    $decisionTwentyFourPath,
    $decisionTwentyFivePath,
    $decisionTwentySixPath,
    $decisionTwentySevenPath,
    $decisionTwentyEightPath,
    $decisionTwentyNinePath,
    $decisionThirtyPath,
    $decisionThirtyOnePath,
    $decisionThirtyTwoPath,
    $decisionThirtyThreePath,
    $decisionThirtyFourPath,
    $decisionThirtyFivePath,
    $decisionThirtySixPath,
    $decisionThirtySevenPath,
    $decisionThirtyEightPath,
    $decisionThirtyNinePath,
    $decisionFortyPath,
    $decisionFortyOnePath,
    $decisionFortyTwoPath,
    $decisionFortyThreePath,
    $decisionFortyFourPath,
    $decisionFortyFivePath,
    $decisionFortySixPath,
    $decisionFortySevenPath,
    $decisionFortyEightPath,
    $decisionFortyNinePath,
    $decisionFiftyPath,
    $decisionFiftyOnePath,
    $decisionFiftyTwoPath,
    $decisionFiftyThreePath,
    $decisionFiftyFourPath,
    $sprintTwentyNinePath,
    $sprintTwentyNineEvidencePath,
    $sprintThirtyPath,
    $sprintThirtyEvidencePath,
    $sprintThirtyOnePath,
    $sprintThirtyOneEvidencePath,
    $sprintThirtyTwoPath,
    $sprintThirtyTwoEvidencePath,
    $sprintThirtyThreePath,
    $sprintThirtyThreeEvidencePath,
    $sprintThirtyFourPath,
    $sprintThirtyFourEvidencePath,
    $sprintThirtyFivePath,
    $sprintThirtyFiveEvidencePath,
    $sprintThirtySixPath,
    $sprintThirtySixEvidencePath,
    $sprintThirtySevenPath,
    $sprintThirtySevenEvidencePath,
    $sprintThirtyEightPath,
    $sprintThirtyEightEvidencePath,
    $sprintThirtyNinePath,
    $sprintThirtyNineEvidencePath,
    $sprintFortyPath,
    $sprintFortyEvidencePath,
    $sprintFortyOnePath,
    $sprintFortyOneEvidencePath,
    $sprintFortyTwoPath,
    $sprintFortyTwoEvidencePath,
    $sprintFortyThreePath,
    $sprintFortyThreeEvidencePath,
    $sprintFortyFourPath,
    $sprintFortyFourEvidencePath,
    $sprintFortyFivePath,
    $sprintFortyFiveEvidencePath,
    $sprintFortySixPath,
    $sprintFortySixEvidencePath,
    $sprintFortySevenPath,
    $sprintFortySevenEvidencePath,
    $sprintFortyEightPath,
    $sprintFortyEightEvidencePath,
    $sprintFortyNinePath,
    $sprintFortyNineEvidencePath,
    $sprintFiftyPath,
    $sprintFiftyEvidencePath,
    $sprintFiftyOnePath,
    $sprintFiftyOneEvidencePath,
    $workflowPath
)
$missingRequiredFiles = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) } | ForEach-Object {
    Get-RepositoryRelativePath -Root $resolvedRoot -Path $_
})
Add-ValidationCheck -Name 'Required planning and authorization files exist' -Passed ($missingRequiredFiles.Count -eq 0) -Details @{ missing = $missingRequiredFiles }

$expectedFamilies = [ordered]@{
    'TEL-PROD' = 24
    'TEL-ACT' = 18
    'TEL-WF' = 15
    'TEL-IDN' = 16
    'TEL-TRI' = 16
    'TEL-REG' = 14
    'TEL-PRA' = 16
    'TEL-INS' = 16
    'TEL-CON' = 16
    'TEL-VID' = 16
    'TEL-RX' = 16
    'TEL-CLM' = 16
    'TEL-ARC' = 14
    'TEL-DAT' = 14
    'TEL-API' = 14
    'TEL-SEC' = 16
    'TEL-UX' = 20
    'TEL-NFR' = 20
    'TEL-TST' = 16
    'TEL-ROL' = 16
}

$specificationFiles = @(Get-ChildItem -LiteralPath $telehealthRoot -File -Filter '*.md' | Where-Object {
    $_.Name -match '^\d{2}-'
} | Sort-Object Name)
$specificationNumbers = @($specificationFiles | ForEach-Object { [int]$_.Name.Substring(0, 2) })
Add-ValidationCheck -Name 'Twenty numbered specifications are present' -Passed (
    $specificationFiles.Count -eq 20 -and (Test-ExactSet -Actual $specificationNumbers -Expected @(1..20))
) -Details @{ count = $specificationFiles.Count; numbers = $specificationNumbers }

$requirementDefinitions = [System.Collections.Generic.List[string]]::new()
foreach ($file in $specificationFiles) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($match in [regex]::Matches($content, '(?m)^\|\s*(TEL-[A-Z]+-\d{3})\s*\|')) {
        $requirementDefinitions.Add($match.Groups[1].Value)
    }
}

$definitionGroups = @($requirementDefinitions | Group-Object)
$duplicateDefinitions = @($definitionGroups | Where-Object Count -ne 1 | ForEach-Object Name)
$definedRequirementIds = @($definitionGroups.Name | Sort-Object)
Add-ValidationCheck -Name 'Requirement definitions are globally unique' -Passed ($duplicateDefinitions.Count -eq 0) -Details @{ duplicates = $duplicateDefinitions }
Add-ValidationCheck -Name 'Exactly 329 normative requirements are defined' -Passed (
    $requirementDefinitions.Count -eq 329 -and $definedRequirementIds.Count -eq 329
) -Details @{ definitions = $requirementDefinitions.Count; unique = $definedRequirementIds.Count }

foreach ($family in $expectedFamilies.Keys) {
    $expectedIds = @(1..$expectedFamilies[$family] | ForEach-Object { '{0}-{1:D3}' -f $family, $_ })
    $actualIds = @($definedRequirementIds | Where-Object { $_.StartsWith("$family-", [StringComparison]::Ordinal) })
    Add-ValidationCheck -Name "Requirement family $family is contiguous" -Passed (Test-ExactSet -Actual $actualIds -Expected $expectedIds) -Details @{
        expected = $expectedIds.Count
        actual = $actualIds.Count
    }
}

$backlog = $null
try {
    $backlog = Get-Content -Raw -LiteralPath $backlogPath | ConvertFrom-Json
    Add-ValidationCheck -Name 'Backlog JSON parses' -Passed $true -Details 'docs/telehealth/backlog/backlog.json'
}
catch {
    Add-ValidationCheck -Name 'Backlog JSON parses' -Passed $false -Details $_.Exception.Message
}

$coveredRequirementIds = [System.Collections.Generic.List[string]]::new()
if ($null -ne $backlog) {
    $allowedStatuses = @('planned', 'ready', 'in_progress', 'blocked', 'verification', 'done')
    $allowedPriorities = @('critical', 'high', 'medium', 'low')
    $declaredStatuses = @(Get-PropertyValue -Object $backlog -Name 'allowedStatuses')
    $epics = @(Get-PropertyValue -Object $backlog -Name 'epics')

    Add-ValidationCheck -Name 'Backlog schema and baseline decision are current' -Passed (
        (Get-PropertyValue -Object $backlog -Name 'schemaVersion') -eq 1 -and
        (Get-PropertyValue -Object $backlog -Name 'baselineDecision') -eq 'TH-DEC-0001' -and
        (Get-PropertyValue -Object $backlog -Name 'implementationAuthorization') -eq 'disabled-synthetic-sprints-01-through-51-authorized-by-th-dec-0003-and-th-dec-0005-through-th-dec-0054-through-2026-10-31; all-other-feature-code-blocked-by-phase-2-exit-gate'
    ) -Details @{ baselineDecision = Get-PropertyValue -Object $backlog -Name 'baselineDecision'; implementationAuthorization = Get-PropertyValue -Object $backlog -Name 'implementationAuthorization' }
    Add-ValidationCheck -Name 'Backlog declares the permitted status vocabulary' -Passed (Test-ExactSet -Actual $declaredStatuses -Expected $allowedStatuses) -Details @{ statuses = $declaredStatuses }

    $epicIds = @($epics | ForEach-Object { Get-PropertyValue -Object $_ -Name 'id' })
    $storyIds = @($epics | ForEach-Object { @(Get-PropertyValue -Object $_ -Name 'stories') } | ForEach-Object { Get-PropertyValue -Object $_ -Name 'id' })
    $expectedEpicIds = @(1..20 | ForEach-Object { 'TH-E{0:D2}' -f $_ })
    $expectedStoryIds = @(1..20 | ForEach-Object {
        $epicNumber = $_
        1..3 | ForEach-Object { 'TH-E{0:D2}-S{1:D2}' -f $epicNumber, $_ }
    })

    Add-ValidationCheck -Name 'Backlog contains 20 uniquely identified epics' -Passed (
        $epicIds.Count -eq 20 -and @($epicIds | Sort-Object -Unique).Count -eq 20 -and (Test-ExactSet -Actual $epicIds -Expected $expectedEpicIds)
    ) -Details @{ count = $epicIds.Count }
    Add-ValidationCheck -Name 'Backlog contains 60 uniquely identified stories' -Passed (
        $storyIds.Count -eq 60 -and @($storyIds | Sort-Object -Unique).Count -eq 60 -and (Test-ExactSet -Actual $storyIds -Expected $expectedStoryIds)
    ) -Details @{ count = $storyIds.Count }

    $invalidDependencies = [System.Collections.Generic.List[string]]::new()
    $invalidStories = [System.Collections.Generic.List[string]]::new()
    $rangeErrors = [System.Collections.Generic.List[string]]::new()
    $dependencyMap = @{}

    for ($epicIndex = 0; $epicIndex -lt $epics.Count; $epicIndex++) {
        $epic = $epics[$epicIndex]
        $epicId = [string](Get-PropertyValue -Object $epic -Name 'id')
        $family = [string](Get-PropertyValue -Object $epic -Name 'requirementFamily')
        $title = [string](Get-PropertyValue -Object $epic -Name 'title')
        $gate = [string](Get-PropertyValue -Object $epic -Name 'gate')
        $dependencies = @(Get-PropertyValue -Object $epic -Name 'dependsOnEpics')
        $dependencyMap[$epicId] = $dependencies

        $expectedFamily = if ($epicIndex -lt $expectedFamilies.Keys.Count) { @($expectedFamilies.Keys)[$epicIndex] } else { $null }
        if ([string]::IsNullOrWhiteSpace($title) -or $family -ne $expectedFamily -or $gate -notmatch '^G[1-6](?:-G[1-6])?$') {
            $invalidStories.Add("$epicId has invalid title, requirement family, or gate")
        }

        foreach ($dependency in $dependencies) {
            if ($dependency -notin $epicIds -or $dependency -eq $epicId) {
                $invalidDependencies.Add("$epicId -> $dependency")
            }
        }

        $stories = @(Get-PropertyValue -Object $epic -Name 'stories')
        if ($stories.Count -ne 3) {
            $invalidStories.Add("$epicId has $($stories.Count) stories")
        }

        foreach ($story in $stories) {
            $storyId = [string](Get-PropertyValue -Object $story -Name 'id')
            $storyTitle = [string](Get-PropertyValue -Object $story -Name 'title')
            $status = [string](Get-PropertyValue -Object $story -Name 'status')
            $priority = [string](Get-PropertyValue -Object $story -Name 'priority')
            $acceptance = [string](Get-PropertyValue -Object $story -Name 'acceptance')
            if ([string]::IsNullOrWhiteSpace($storyTitle) -or [string]::IsNullOrWhiteSpace($acceptance) -or $status -notin $allowedStatuses -or $priority -notin $allowedPriorities) {
                $invalidStories.Add("$storyId has invalid title, acceptance, status, or priority")
            }

            $primaryRequirements = Get-PropertyValue -Object $story -Name 'primaryRequirements'
            $from = [string](Get-PropertyValue -Object $primaryRequirements -Name 'from')
            $to = [string](Get-PropertyValue -Object $primaryRequirements -Name 'to')
            $fromMatch = [regex]::Match($from, '^(TEL-[A-Z]+)-(\d{3})$')
            $toMatch = [regex]::Match($to, '^(TEL-[A-Z]+)-(\d{3})$')

            if (-not $fromMatch.Success -or -not $toMatch.Success) {
                $rangeErrors.Add("$storyId has malformed primary requirement range")
                continue
            }

            $fromFamily = $fromMatch.Groups[1].Value
            $toFamily = $toMatch.Groups[1].Value
            $fromNumber = [int]$fromMatch.Groups[2].Value
            $toNumber = [int]$toMatch.Groups[2].Value
            if ($fromFamily -ne $toFamily -or $fromFamily -ne $family -or $fromNumber -gt $toNumber) {
                $rangeErrors.Add("$storyId has inconsistent primary requirement range $from..$to")
                continue
            }

            for ($number = $fromNumber; $number -le $toNumber; $number++) {
                $coveredRequirementIds.Add(('{0}-{1:D3}' -f $fromFamily, $number))
            }
        }
    }

    $remainingEpics = @($epicIds)
    do {
        $removable = @($remainingEpics | Where-Object {
            $candidate = $_
            @($dependencyMap[$candidate] | Where-Object { $_ -in $remainingEpics }).Count -eq 0
        })
        if ($removable.Count -gt 0) {
            $remainingEpics = @($remainingEpics | Where-Object { $_ -notin $removable })
        }
    } while ($removable.Count -gt 0 -and $remainingEpics.Count -gt 0)

    Add-ValidationCheck -Name 'Epic dependencies reference known epics and are acyclic' -Passed (
        $invalidDependencies.Count -eq 0 -and $remainingEpics.Count -eq 0
    ) -Details @{ invalid = @($invalidDependencies); cycle = $remainingEpics }
    Add-ValidationCheck -Name 'Story metadata and primary ranges are valid' -Passed (
        $invalidStories.Count -eq 0 -and $rangeErrors.Count -eq 0
    ) -Details @{ invalidStories = @($invalidStories); rangeErrors = @($rangeErrors) }
}

$coverageGroups = @($coveredRequirementIds | Group-Object)
$appliedTestMutation = $TestMutation
if ($TestMutation -eq 'DropFirstCoverage' -and $coveredRequirementIds.Count -gt 0) {
    $coveredRequirementIds.RemoveAt(0)
    $coverageGroups = @($coveredRequirementIds | Group-Object)
}
$duplicateCoverage = @($coverageGroups | Where-Object Count -ne 1 | ForEach-Object Name)
$coveredUnique = @($coverageGroups.Name | Sort-Object)
$missingCoverage = @($definedRequirementIds | Where-Object { $_ -notin $coveredUnique })
$unknownCoverage = @($coveredUnique | Where-Object { $_ -notin $definedRequirementIds })
Add-ValidationCheck -Name 'Primary backlog coverage assigns every requirement exactly once' -Passed (
    $coveredRequirementIds.Count -eq 329 -and
    $duplicateCoverage.Count -eq 0 -and
    $missingCoverage.Count -eq 0 -and
    $unknownCoverage.Count -eq 0
) -Details @{
    assignments = $coveredRequirementIds.Count
    duplicates = $duplicateCoverage
    missing = $missingCoverage
    unknown = $unknownCoverage
}

$safeguards = $null
try {
    $safeguards = Get-Content -Raw -LiteralPath $safeguardsPath | ConvertFrom-Json
    Add-ValidationCheck -Name 'Safeguard manifest JSON parses' -Passed $true -Details 'docs/telehealth/backlog/safeguards.json'
}
catch {
    Add-ValidationCheck -Name 'Safeguard manifest JSON parses' -Passed $false -Details $_.Exception.Message
}

if ($null -ne $safeguards) {
    $safeguardItems = @(Get-PropertyValue -Object $safeguards -Name 'safeguards')
    $safeguardIds = @($safeguardItems | ForEach-Object { Get-PropertyValue -Object $_ -Name 'id' })
    $expectedSafeguardIds = @(1..56 | ForEach-Object { 'TH-SG-{0:D3}' -f $_ })
    $activeSafeguards = @(Get-PropertyValue -Object $safeguards -Name 'activeSafeguards')
    $requiredPaths = @($safeguardItems | ForEach-Object { [string](Get-PropertyValue -Object $_ -Name 'requiredPath') })
    $invalidSafeguards = @($safeguardItems | Where-Object {
        [string]::IsNullOrWhiteSpace([string](Get-PropertyValue -Object $_ -Name 'name')) -or
        [string]::IsNullOrWhiteSpace([string](Get-PropertyValue -Object $_ -Name 'requiredPath')) -or
        [string]::IsNullOrWhiteSpace([string](Get-PropertyValue -Object $_ -Name 'trigger')) -or
        [string]::IsNullOrWhiteSpace([string](Get-PropertyValue -Object $_ -Name 'evidence'))
    } | ForEach-Object { Get-PropertyValue -Object $_ -Name 'id' })
    $missingActivePaths = @($safeguardItems | Where-Object {
        (Get-PropertyValue -Object $_ -Name 'id') -in $activeSafeguards -and
        -not (Test-Path -LiteralPath (Join-Path $resolvedRoot ([string](Get-PropertyValue -Object $_ -Name 'requiredPath'))) -PathType Leaf)
    } | ForEach-Object { Get-PropertyValue -Object $_ -Name 'requiredPath' })

    Add-ValidationCheck -Name 'Safeguard manifest records the scoped Decision 0002, 0003, 0005 through 0054 activation' -Passed (
        (Get-PropertyValue -Object $safeguards -Name 'schemaVersion') -eq 1 -and
        (Get-PropertyValue -Object $safeguards -Name 'status') -eq 'active-for-decisions-0003-0005-through-0054-synthetic-sprints-01-through-51' -and
        (Get-PropertyValue -Object $safeguards -Name 'activationPrerequisite') -eq 'Active only for the exact disabled synthetic Sprints 1 through 51 scopes and paths in TH-DEC-0003 and TH-DEC-0005 through TH-DEC-0054 through 2026-10-31' -and
        (Test-ExactSet -Actual @(Get-PropertyValue -Object $safeguards -Name 'authorizationDecisions') -Expected @('TH-DEC-0002', 'TH-DEC-0003', 'TH-DEC-0005', 'TH-DEC-0006', 'TH-DEC-0007', 'TH-DEC-0008', 'TH-DEC-0009', 'TH-DEC-0010', 'TH-DEC-0011', 'TH-DEC-0012', 'TH-DEC-0013', 'TH-DEC-0014', 'TH-DEC-0015', 'TH-DEC-0016', 'TH-DEC-0017', 'TH-DEC-0018', 'TH-DEC-0019', 'TH-DEC-0020', 'TH-DEC-0021', 'TH-DEC-0022', 'TH-DEC-0023', 'TH-DEC-0024', 'TH-DEC-0025', 'TH-DEC-0026', 'TH-DEC-0027', 'TH-DEC-0028', 'TH-DEC-0029', 'TH-DEC-0030', 'TH-DEC-0031', 'TH-DEC-0032', 'TH-DEC-0033', 'TH-DEC-0034', 'TH-DEC-0035', 'TH-DEC-0036', 'TH-DEC-0037', 'TH-DEC-0038', 'TH-DEC-0039', 'TH-DEC-0040', 'TH-DEC-0041', 'TH-DEC-0042', 'TH-DEC-0043', 'TH-DEC-0044', 'TH-DEC-0045', 'TH-DEC-0046', 'TH-DEC-0047', 'TH-DEC-0048', 'TH-DEC-0049', 'TH-DEC-0050', 'TH-DEC-0051', 'TH-DEC-0052', 'TH-DEC-0053', 'TH-DEC-0054')) -and
        (Test-ExactSet -Actual $activeSafeguards -Expected $expectedSafeguardIds)
    ) -Details @{ status = Get-PropertyValue -Object $safeguards -Name 'status'; active = $activeSafeguards }
    Add-ValidationCheck -Name 'Fifty-six safeguards have unique complete identifiers and complete definitions' -Passed (
        $safeguardIds.Count -eq 56 -and
        @($safeguardIds | Sort-Object -Unique).Count -eq 56 -and
        (Test-ExactSet -Actual $safeguardIds -Expected $expectedSafeguardIds) -and
        @($requiredPaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -eq 56 -and
        $invalidSafeguards.Count -eq 0
    ) -Details @{ identifiers = $safeguardIds; invalid = $invalidSafeguards }
    Add-ValidationCheck -Name 'Every active safeguard implementation path exists' -Passed ($missingActivePaths.Count -eq 0) -Details @{ missing = $missingActivePaths }
}

$markdownFiles = @(Get-ChildItem -LiteralPath $telehealthRoot -Recurse -File -Filter '*.md')
$brokenLinks = [System.Collections.Generic.List[string]]::new()
$checkedRelativeLinkCount = 0
$rootPrefix = $resolvedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
foreach ($file in $markdownFiles) {
    $content = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($match in [regex]::Matches($content, '!?(?:\[[^\]]*\])\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim()
        if ([string]::IsNullOrWhiteSpace($target) -or $target -match '^(https?://|mailto:|tel:|#)') {
            continue
        }

        $checkedRelativeLinkCount++
        $pathPart = [Uri]::UnescapeDataString(($target -split '#', 2)[0]).Trim([char[]]@('<', '>'))
        if ([string]::IsNullOrWhiteSpace($pathPart) -or [IO.Path]::IsPathRooted($pathPart)) {
            $brokenLinks.Add("$(Get-RepositoryRelativePath -Root $resolvedRoot -Path $file.FullName): $target")
            continue
        }

        $candidate = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $pathPart))
        if (-not $candidate.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $candidate)) {
            $brokenLinks.Add("$(Get-RepositoryRelativePath -Root $resolvedRoot -Path $file.FullName): $target")
        }
    }
}
Add-ValidationCheck -Name 'Telehealth Markdown relative links resolve inside the repository' -Passed ($brokenLinks.Count -eq 0) -Details @{
    markdownFiles = $markdownFiles.Count
    relativeLinks = $checkedRelativeLinkCount
    broken = @($brokenLinks)
}

$wireframeHtml = if (Test-Path -LiteralPath $wireframePath -PathType Leaf) { Get-Content -Raw -LiteralPath $wireframePath } else { '' }
if ($TestMutation -eq 'BreakWireframeLabel') {
    $wireframeHtml = $wireframeHtml.Replace('id="callback"', 'id="callback-mutated"')
}
$htmlIds = @([regex]::Matches($wireframeHtml, '\sid="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
$duplicateHtmlIds = @($htmlIds | Group-Object | Where-Object Count -ne 1 | ForEach-Object Name)
$labelTargets = @([regex]::Matches($wireframeHtml, '<label[^>]+for="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
$missingLabelTargets = @($labelTargets | Where-Object { $_ -notin $htmlIds } | Sort-Object -Unique)
$localAnchorTargets = @([regex]::Matches($wireframeHtml, 'href="#([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
$missingAnchorTargets = @($localAnchorTargets | Where-Object { $_ -notin $htmlIds } | Sort-Object -Unique)
$screenIds = @([regex]::Matches($wireframeHtml, '<span class="screen-id">((?:PAT|ADM|PHY)-\d{2})</span>') | ForEach-Object { $_.Groups[1].Value })
$expectedScreenIds = @(
    1..7 | ForEach-Object { 'PAT-{0:D2}' -f $_ }
) + @(
    1..2 | ForEach-Object { 'ADM-{0:D2}' -f $_ }
) + @(
    1..3 | ForEach-Object { 'PHY-{0:D2}' -f $_ }
)
$frameCount = [regex]::Matches($wireframeHtml, '<article\b[^>]*class="[^"]*\bframe\b[^"]*"').Count

Add-ValidationCheck -Name 'Wireframe sheet contains the 12 stable patient, administrator, and physician screens' -Passed (
    $frameCount -eq 12 -and $screenIds.Count -eq 12 -and @($screenIds | Sort-Object -Unique).Count -eq 12 -and (Test-ExactSet -Actual $screenIds -Expected $expectedScreenIds)
) -Details @{ frames = $frameCount; screens = $screenIds }
Add-ValidationCheck -Name 'Wireframe identifiers, labels, and local anchors are internally consistent' -Passed (
    $duplicateHtmlIds.Count -eq 0 -and $missingLabelTargets.Count -eq 0 -and $missingAnchorTargets.Count -eq 0
) -Details @{ duplicateIds = $duplicateHtmlIds; missingLabelTargets = $missingLabelTargets; missingAnchorTargets = $missingAnchorTargets }
Add-ValidationCheck -Name 'Wireframe is static and has no script or external-resource execution path' -Passed (
    $wireframeHtml -notmatch '(?i)<script\b' -and
    $wireframeHtml -notmatch '(?i)\son[a-z]+\s*=' -and
    $wireframeHtml -notmatch '(?i)\s(?:src|href)\s*=\s*["'']https?://' -and
    $wireframeHtml -match '<html lang="en">' -and
    $wireframeHtml -match '<meta name="viewport"'
) -Details @{ inlineEventHandlers = [regex]::Matches($wireframeHtml, '(?i)\son[a-z]+\s*=').Count }

$decisionOne = if (Test-Path -LiteralPath $decisionOnePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionOnePath } else { '' }
$decisionTwo = if (Test-Path -LiteralPath $decisionTwoPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwoPath } else { '' }
$decisionThree = if (Test-Path -LiteralPath $decisionThreePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThreePath } else { '' }
$decisionFour = if (Test-Path -LiteralPath $decisionFourPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFourPath } else { '' }
$decisionFive = if (Test-Path -LiteralPath $decisionFivePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFivePath } else { '' }
$decisionSix = if (Test-Path -LiteralPath $decisionSixPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionSixPath } else { '' }
$decisionSeven = if (Test-Path -LiteralPath $decisionSevenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionSevenPath } else { '' }
$decisionEight = if (Test-Path -LiteralPath $decisionEightPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionEightPath } else { '' }
$decisionNine = if (Test-Path -LiteralPath $decisionNinePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionNinePath } else { '' }
$decisionTen = if (Test-Path -LiteralPath $decisionTenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTenPath } else { '' }
$decisionEleven = if (Test-Path -LiteralPath $decisionElevenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionElevenPath } else { '' }
$decisionTwelve = if (Test-Path -LiteralPath $decisionTwelvePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwelvePath } else { '' }
$decisionThirteen = if (Test-Path -LiteralPath $decisionThirteenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirteenPath } else { '' }
$decisionFourteen = if (Test-Path -LiteralPath $decisionFourteenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFourteenPath } else { '' }
$decisionFifteen = if (Test-Path -LiteralPath $decisionFifteenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFifteenPath } else { '' }
$decisionSixteen = if (Test-Path -LiteralPath $decisionSixteenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionSixteenPath } else { '' }
$decisionSeventeen = if (Test-Path -LiteralPath $decisionSeventeenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionSeventeenPath } else { '' }
$decisionEighteen = if (Test-Path -LiteralPath $decisionEighteenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionEighteenPath } else { '' }
$decisionNineteen = if (Test-Path -LiteralPath $decisionNineteenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionNineteenPath } else { '' }
$decisionTwenty = if (Test-Path -LiteralPath $decisionTwentyPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentyPath } else { '' }
$decisionTwentyOne = if (Test-Path -LiteralPath $decisionTwentyOnePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentyOnePath } else { '' }
$decisionTwentyTwo = if (Test-Path -LiteralPath $decisionTwentyTwoPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentyTwoPath } else { '' }
$decisionTwentyThree = if (Test-Path -LiteralPath $decisionTwentyThreePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentyThreePath } else { '' }
$decisionTwentyFour = if (Test-Path -LiteralPath $decisionTwentyFourPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentyFourPath } else { '' }
$decisionTwentyFive = if (Test-Path -LiteralPath $decisionTwentyFivePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentyFivePath } else { '' }
$decisionTwentySix = if (Test-Path -LiteralPath $decisionTwentySixPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentySixPath } else { '' }
$decisionTwentySeven = if (Test-Path -LiteralPath $decisionTwentySevenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentySevenPath } else { '' }
$decisionTwentyEight = if (Test-Path -LiteralPath $decisionTwentyEightPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentyEightPath } else { '' }
$decisionTwentyNine = if (Test-Path -LiteralPath $decisionTwentyNinePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionTwentyNinePath } else { '' }
$decisionThirty = if (Test-Path -LiteralPath $decisionThirtyPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtyPath } else { '' }
$decisionThirtyOne = if (Test-Path -LiteralPath $decisionThirtyOnePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtyOnePath } else { '' }
$decisionThirtyTwo = if (Test-Path -LiteralPath $decisionThirtyTwoPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtyTwoPath } else { '' }
$decisionThirtyThree = if (Test-Path -LiteralPath $decisionThirtyThreePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtyThreePath } else { '' }
$decisionThirtyFour = if (Test-Path -LiteralPath $decisionThirtyFourPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtyFourPath } else { '' }
$decisionThirtyFive = if (Test-Path -LiteralPath $decisionThirtyFivePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtyFivePath } else { '' }
$decisionThirtySix = if (Test-Path -LiteralPath $decisionThirtySixPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtySixPath } else { '' }
$decisionThirtySeven = if (Test-Path -LiteralPath $decisionThirtySevenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtySevenPath } else { '' }
$decisionThirtyEight = if (Test-Path -LiteralPath $decisionThirtyEightPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtyEightPath } else { '' }
$decisionThirtyNine = if (Test-Path -LiteralPath $decisionThirtyNinePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionThirtyNinePath } else { '' }
$decisionForty = if (Test-Path -LiteralPath $decisionFortyPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortyPath } else { '' }
$decisionFortyOne = if (Test-Path -LiteralPath $decisionFortyOnePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortyOnePath } else { '' }
$decisionFortyTwo = if (Test-Path -LiteralPath $decisionFortyTwoPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortyTwoPath } else { '' }
$decisionFortyThree = if (Test-Path -LiteralPath $decisionFortyThreePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortyThreePath } else { '' }
$decisionFortyFour = if (Test-Path -LiteralPath $decisionFortyFourPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortyFourPath } else { '' }
$decisionFortyFive = if (Test-Path -LiteralPath $decisionFortyFivePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortyFivePath } else { '' }
$decisionFortySix = if (Test-Path -LiteralPath $decisionFortySixPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortySixPath } else { '' }
$decisionFortySeven = if (Test-Path -LiteralPath $decisionFortySevenPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortySevenPath } else { '' }
$decisionFortyEight = if (Test-Path -LiteralPath $decisionFortyEightPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortyEightPath } else { '' }
$decisionFortyNine = if (Test-Path -LiteralPath $decisionFortyNinePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFortyNinePath } else { '' }
$decisionFifty = if (Test-Path -LiteralPath $decisionFiftyPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFiftyPath } else { '' }
$decisionFiftyOne = if (Test-Path -LiteralPath $decisionFiftyOnePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFiftyOnePath } else { '' }
$decisionFiftyTwo = if (Test-Path -LiteralPath $decisionFiftyTwoPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFiftyTwoPath } else { '' }
$decisionFiftyThree = if (Test-Path -LiteralPath $decisionFiftyThreePath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFiftyThreePath } else { '' }
$decisionFiftyFour = if (Test-Path -LiteralPath $decisionFiftyFourPath -PathType Leaf) { Get-Content -Raw -LiteralPath $decisionFiftyFourPath } else { '' }
$expiryMatch = [regex]::Match($decisionTwo, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$expiryDate = if ($expiryMatch.Success) { [DateTime]::ParseExact($expiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThreeExpiryMatch = [regex]::Match($decisionThree, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThreeExpiryDate = if ($decisionThreeExpiryMatch.Success) { [DateTime]::ParseExact($decisionThreeExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFiveExpiryMatch = [regex]::Match($decisionFive, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFiveExpiryDate = if ($decisionFiveExpiryMatch.Success) { [DateTime]::ParseExact($decisionFiveExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionSixExpiryMatch = [regex]::Match($decisionSix, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionSixExpiryDate = if ($decisionSixExpiryMatch.Success) { [DateTime]::ParseExact($decisionSixExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionSevenExpiryMatch = [regex]::Match($decisionSeven, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionSevenExpiryDate = if ($decisionSevenExpiryMatch.Success) { [DateTime]::ParseExact($decisionSevenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionEightExpiryMatch = [regex]::Match($decisionEight, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionEightExpiryDate = if ($decisionEightExpiryMatch.Success) { [DateTime]::ParseExact($decisionEightExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionNineExpiryMatch = [regex]::Match($decisionNine, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionNineExpiryDate = if ($decisionNineExpiryMatch.Success) { [DateTime]::ParseExact($decisionNineExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTenExpiryMatch = [regex]::Match($decisionTen, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTenExpiryDate = if ($decisionTenExpiryMatch.Success) { [DateTime]::ParseExact($decisionTenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionElevenExpiryMatch = [regex]::Match($decisionEleven, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionElevenExpiryDate = if ($decisionElevenExpiryMatch.Success) { [DateTime]::ParseExact($decisionElevenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwelveExpiryMatch = [regex]::Match($decisionTwelve, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwelveExpiryDate = if ($decisionTwelveExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwelveExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirteenExpiryMatch = [regex]::Match($decisionThirteen, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirteenExpiryDate = if ($decisionThirteenExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirteenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFourteenExpiryMatch = [regex]::Match($decisionFourteen, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFourteenExpiryDate = if ($decisionFourteenExpiryMatch.Success) { [DateTime]::ParseExact($decisionFourteenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFifteenExpiryMatch = [regex]::Match($decisionFifteen, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFifteenExpiryDate = if ($decisionFifteenExpiryMatch.Success) { [DateTime]::ParseExact($decisionFifteenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionSixteenExpiryMatch = [regex]::Match($decisionSixteen, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionSixteenExpiryDate = if ($decisionSixteenExpiryMatch.Success) { [DateTime]::ParseExact($decisionSixteenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionSeventeenExpiryMatch = [regex]::Match($decisionSeventeen, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionSeventeenExpiryDate = if ($decisionSeventeenExpiryMatch.Success) { [DateTime]::ParseExact($decisionSeventeenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionEighteenExpiryMatch = [regex]::Match($decisionEighteen, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionEighteenExpiryDate = if ($decisionEighteenExpiryMatch.Success) { [DateTime]::ParseExact($decisionEighteenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionNineteenExpiryMatch = [regex]::Match($decisionNineteen, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionNineteenExpiryDate = if ($decisionNineteenExpiryMatch.Success) { [DateTime]::ParseExact($decisionNineteenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentyExpiryMatch = [regex]::Match($decisionTwenty, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentyExpiryDate = if ($decisionTwentyExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentyExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentyOneExpiryMatch = [regex]::Match($decisionTwentyOne, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentyOneExpiryDate = if ($decisionTwentyOneExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentyOneExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentyTwoExpiryMatch = [regex]::Match($decisionTwentyTwo, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentyTwoExpiryDate = if ($decisionTwentyTwoExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentyTwoExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentyThreeExpiryMatch = [regex]::Match($decisionTwentyThree, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentyThreeExpiryDate = if ($decisionTwentyThreeExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentyThreeExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentyFourExpiryMatch = [regex]::Match($decisionTwentyFour, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentyFourExpiryDate = if ($decisionTwentyFourExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentyFourExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentyFiveExpiryMatch = [regex]::Match($decisionTwentyFive, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentyFiveExpiryDate = if ($decisionTwentyFiveExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentyFiveExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentySixExpiryMatch = [regex]::Match($decisionTwentySix, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentySixExpiryDate = if ($decisionTwentySixExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentySixExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentySevenExpiryMatch = [regex]::Match($decisionTwentySeven, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentySevenExpiryDate = if ($decisionTwentySevenExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentySevenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentyEightExpiryMatch = [regex]::Match($decisionTwentyEight, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentyEightExpiryDate = if ($decisionTwentyEightExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentyEightExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionTwentyNineExpiryMatch = [regex]::Match($decisionTwentyNine, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionTwentyNineExpiryDate = if ($decisionTwentyNineExpiryMatch.Success) { [DateTime]::ParseExact($decisionTwentyNineExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtyExpiryMatch = [regex]::Match($decisionThirty, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtyExpiryDate = if ($decisionThirtyExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtyExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtyOneExpiryMatch = [regex]::Match($decisionThirtyOne, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtyOneExpiryDate = if ($decisionThirtyOneExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtyOneExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtyTwoExpiryMatch = [regex]::Match($decisionThirtyTwo, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtyTwoExpiryDate = if ($decisionThirtyTwoExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtyTwoExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtyThreeExpiryMatch = [regex]::Match($decisionThirtyThree, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtyThreeExpiryDate = if ($decisionThirtyThreeExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtyThreeExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtyFourExpiryMatch = [regex]::Match($decisionThirtyFour, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtyFourExpiryDate = if ($decisionThirtyFourExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtyFourExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtyFiveExpiryMatch = [regex]::Match($decisionThirtyFive, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtyFiveExpiryDate = if ($decisionThirtyFiveExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtyFiveExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtySixExpiryMatch = [regex]::Match($decisionThirtySix, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtySixExpiryDate = if ($decisionThirtySixExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtySixExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtySevenExpiryMatch = [regex]::Match($decisionThirtySeven, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtySevenExpiryDate = if ($decisionThirtySevenExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtySevenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtyEightExpiryMatch = [regex]::Match($decisionThirtyEight, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtyEightExpiryDate = if ($decisionThirtyEightExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtyEightExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionThirtyNineExpiryMatch = [regex]::Match($decisionThirtyNine, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionThirtyNineExpiryDate = if ($decisionThirtyNineExpiryMatch.Success) { [DateTime]::ParseExact($decisionThirtyNineExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortyExpiryMatch = [regex]::Match($decisionForty, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortyExpiryDate = if ($decisionFortyExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortyExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortyOneExpiryMatch = [regex]::Match($decisionFortyOne, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortyOneExpiryDate = if ($decisionFortyOneExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortyOneExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortyTwoExpiryMatch = [regex]::Match($decisionFortyTwo, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortyTwoExpiryDate = if ($decisionFortyTwoExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortyTwoExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortyThreeExpiryMatch = [regex]::Match($decisionFortyThree, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortyThreeExpiryDate = if ($decisionFortyThreeExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortyThreeExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortyFourExpiryMatch = [regex]::Match($decisionFortyFour, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortyFourExpiryDate = if ($decisionFortyFourExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortyFourExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortyFiveExpiryMatch = [regex]::Match($decisionFortyFive, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortyFiveExpiryDate = if ($decisionFortyFiveExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortyFiveExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortySixExpiryMatch = [regex]::Match($decisionFortySix, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortySixExpiryDate = if ($decisionFortySixExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortySixExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortySevenExpiryMatch = [regex]::Match($decisionFortySeven, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortySevenExpiryDate = if ($decisionFortySevenExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortySevenExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortyEightExpiryMatch = [regex]::Match($decisionFortyEight, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortyEightExpiryDate = if ($decisionFortyEightExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortyEightExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFortyNineExpiryMatch = [regex]::Match($decisionFortyNine, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFortyNineExpiryDate = if ($decisionFortyNineExpiryMatch.Success) { [DateTime]::ParseExact($decisionFortyNineExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFiftyExpiryMatch = [regex]::Match($decisionFifty, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFiftyExpiryDate = if ($decisionFiftyExpiryMatch.Success) { [DateTime]::ParseExact($decisionFiftyExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFiftyOneExpiryMatch = [regex]::Match($decisionFiftyOne, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFiftyOneExpiryDate = if ($decisionFiftyOneExpiryMatch.Success) { [DateTime]::ParseExact($decisionFiftyOneExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFiftyTwoExpiryMatch = [regex]::Match($decisionFiftyTwo, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFiftyTwoExpiryDate = if ($decisionFiftyTwoExpiryMatch.Success) { [DateTime]::ParseExact($decisionFiftyTwoExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFiftyThreeExpiryMatch = [regex]::Match($decisionFiftyThree, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFiftyThreeExpiryDate = if ($decisionFiftyThreeExpiryMatch.Success) { [DateTime]::ParseExact($decisionFiftyThreeExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
$decisionFiftyFourExpiryMatch = [regex]::Match($decisionFiftyFour, '(?m)^Review/expiry:\s*(\d{4}-\d{2}-\d{2})')
$decisionFiftyFourExpiryDate = if ($decisionFiftyFourExpiryMatch.Success) { [DateTime]::ParseExact($decisionFiftyFourExpiryMatch.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) } else { [DateTime]::MinValue }
if ($TestMutation -eq 'ExpireDecision') {
    $expiryDate = [DateTime]::MinValue
    $decisionThreeExpiryDate = [DateTime]::MinValue
    $decisionFiveExpiryDate = [DateTime]::MinValue
    $decisionSixExpiryDate = [DateTime]::MinValue
    $decisionSevenExpiryDate = [DateTime]::MinValue
    $decisionEightExpiryDate = [DateTime]::MinValue
    $decisionNineExpiryDate = [DateTime]::MinValue
    $decisionTenExpiryDate = [DateTime]::MinValue
    $decisionElevenExpiryDate = [DateTime]::MinValue
    $decisionTwelveExpiryDate = [DateTime]::MinValue
    $decisionThirteenExpiryDate = [DateTime]::MinValue
    $decisionFourteenExpiryDate = [DateTime]::MinValue
    $decisionFifteenExpiryDate = [DateTime]::MinValue
    $decisionSixteenExpiryDate = [DateTime]::MinValue
    $decisionSeventeenExpiryDate = [DateTime]::MinValue
    $decisionEighteenExpiryDate = [DateTime]::MinValue
    $decisionNineteenExpiryDate = [DateTime]::MinValue
    $decisionTwentyExpiryDate = [DateTime]::MinValue
    $decisionTwentyOneExpiryDate = [DateTime]::MinValue
    $decisionTwentyTwoExpiryDate = [DateTime]::MinValue
    $decisionTwentyThreeExpiryDate = [DateTime]::MinValue
    $decisionTwentyFourExpiryDate = [DateTime]::MinValue
    $decisionTwentyFiveExpiryDate = [DateTime]::MinValue
    $decisionTwentySixExpiryDate = [DateTime]::MinValue
    $decisionTwentySevenExpiryDate = [DateTime]::MinValue
    $decisionTwentyEightExpiryDate = [DateTime]::MinValue
    $decisionTwentyNineExpiryDate = [DateTime]::MinValue
    $decisionThirtyExpiryDate = [DateTime]::MinValue
    $decisionThirtyOneExpiryDate = [DateTime]::MinValue
    $decisionThirtyTwoExpiryDate = [DateTime]::MinValue
    $decisionThirtyThreeExpiryDate = [DateTime]::MinValue
    $decisionThirtyFourExpiryDate = [DateTime]::MinValue
    $decisionThirtyFiveExpiryDate = [DateTime]::MinValue
    $decisionThirtySixExpiryDate = [DateTime]::MinValue
    $decisionThirtySevenExpiryDate = [DateTime]::MinValue
    $decisionThirtyEightExpiryDate = [DateTime]::MinValue
    $decisionThirtyNineExpiryDate = [DateTime]::MinValue
    $decisionFortyExpiryDate = [DateTime]::MinValue
    $decisionFortyOneExpiryDate = [DateTime]::MinValue
    $decisionFortyTwoExpiryDate = [DateTime]::MinValue
    $decisionFortyThreeExpiryDate = [DateTime]::MinValue
    $decisionFortyFourExpiryDate = [DateTime]::MinValue
    $decisionFortyFiveExpiryDate = [DateTime]::MinValue
    $decisionFortySixExpiryDate = [DateTime]::MinValue
    $decisionFortySevenExpiryDate = [DateTime]::MinValue
    $decisionFortyEightExpiryDate = [DateTime]::MinValue
    $decisionFortyNineExpiryDate = [DateTime]::MinValue
    $decisionFiftyExpiryDate = [DateTime]::MinValue
    $decisionFiftyOneExpiryDate = [DateTime]::MinValue
    $decisionFiftyTwoExpiryDate = [DateTime]::MinValue
    $decisionFiftyThreeExpiryDate = [DateTime]::MinValue
    $decisionFiftyFourExpiryDate = [DateTime]::MinValue
}
Add-ValidationCheck -Name 'G0 planning baseline remains approved' -Passed ($decisionOne -match '(?m)^Status: Approved for development planning\s*$') -Details 'TH-DEC-0001'
Add-ValidationCheck -Name 'Decision 0002 is approved, bounded, owned, and unexpired' -Passed (
    $decisionTwo -match '(?m)^Status: Approved — active for the exact scoped verification change\s*$' -and
    $decisionTwo -notmatch '(?m)^Implementation owner: To be named' -and
    $decisionTwo -match 'No other Phase 2 gate, recommendation, packet or finding is closed' -and
    $expiryMatch.Success -and [DateTime]::UtcNow.Date -le $expiryDate.Date
) -Details @{ decision = 'TH-DEC-0002'; expires = if ($expiryMatch.Success) { $expiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0003 is approved, bounded, owned, and unexpired' -Passed (
    $decisionThree -match '(?m)^Status: Approved — active for the exact disabled synthetic Sprint 1 scope\s*$' -and
    $decisionThree -match '(?m)^Approved date: 2026-08-26\s*$' -and
    $decisionThree -match 'I approve all Decisions' -and
    $decisionThree -match 'This decision does not authorize:' -and
    $decisionThreeExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThreeExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0003'; expires = if ($decisionThreeExpiryMatch.Success) { $decisionThreeExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0004 approved the exact generated-bootstrap reconciliation without production authority' -Passed (
    $decisionFour -match '(?m)^Status: Approved — active for deterministic bootstrap regeneration and review\s*$' -and
    $decisionFour -match '(?m)^Approved date: 2026-08-26\s*$' -and
    $decisionFour -match 'permission to modify the generated bootstrap file' -and
    $decisionFour -match 'does not authorize production enablement, live patient care, destructive data changes, unexplained schema drift'
) -Details @{ decision = 'TH-DEC-0004'; scope = 'generated-bootstrap-reconciliation' }
Add-ValidationCheck -Name 'Decision 0005 approves only bounded synthetic established-patient readiness and remains unexpired' -Passed (
    $decisionFive -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFive -match '(?m)^Approved date: 2026-08-26\s*$' -and
    $decisionFive -match 'Eligibility and network participation remain separate statuses and evidence' -and
    $decisionFive -match 'does not authorize:' -and
    $decisionFive -match 'permission to modify the generated bootstrap file' -and
    $decisionFiveExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFiveExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0005'; expires = if ($decisionFiveExpiryMatch.Success) { $decisionFiveExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0006 approves only bounded synthetic patient queue transparency and remains unexpired' -Passed (
    $decisionSix -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionSix -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionSix -match 'position is a point-in-time, practice/facility-scoped count' -and
    $decisionSix -match 'SignalR is not introduced in this slice' -and
    $decisionSix -match 'does not authorize:' -and
    $decisionSixExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionSixExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0006'; expires = if ($decisionSixExpiryMatch.Success) { $decisionSixExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0007 approves only bounded synthetic prospective-patient identity separation and remains unexpired' -Passed (
    $decisionSeven -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionSeven -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionSeven -match 'creates a \*\*prospective applicant\*\*, not a patient' -and
    $decisionSeven -match 'Contact verification and identity proofing are distinct' -and
    $decisionSeven -match 'does not authorize:' -and
    $decisionSevenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionSevenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0007'; expires = if ($decisionSevenExpiryMatch.Success) { $decisionSevenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0008 approves only the bounded synthetic connection-room shell and remains unexpired' -Passed (
    $decisionEight -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionEight -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionEight -match 'provider-neutral, synthetic-only connection-room boundary' -and
    $decisionEight -match 'does not transport media or clinically start a consultation' -and
    $decisionEight -match 'This decision does not authorize:' -and
    $decisionEightExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionEightExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0008'; expires = if ($decisionEightExpiryMatch.Success) { $decisionEightExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0009 approves only the bounded synthetic consultation-start handoff and remains unexpired' -Passed (
    $decisionNine -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionNine -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionNine -match 'transactionally linked, synthetic-only handoff' -and
    $decisionNine -match 'opaque consultation ID, never the sequential encounter key' -and
    $decisionNine -match 'This decision does not authorize:' -and
    $decisionNineExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionNineExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0009'; expires = if ($decisionNineExpiryMatch.Success) { $decisionNineExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0010 approves only the bounded read-only consultation workspace and remains unexpired' -Passed (
    $decisionTen -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTen -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTen -match 'least-privilege, read-only workspace projection' -and
    $decisionTen -match 'explicit allowlist' -and
    $decisionTen -match 'This decision does not authorize:' -and
    $decisionTenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0010'; expires = if ($decisionTenExpiryMatch.Success) { $decisionTenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0011 approves only the bounded consultation documentation draft and remains unexpired' -Passed (
    $decisionEleven -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionEleven -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionEleven -match 'explicitly save a bounded SOAP documentation draft' -and
    $decisionEleven -match 'reuses canonical `clinical_notes`' -and
    $decisionEleven -match 'This decision does not authorize:' -and
    $decisionElevenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionElevenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0011'; expires = if ($decisionElevenExpiryMatch.Success) { $decisionElevenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0012 approves only the bounded unfinished consultation wrap-up handoff and remains unexpired' -Passed (
    $decisionTwelve -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwelve -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTwelve -match 'move the unfinished visit into physician-owned wrap-up' -and
    $decisionTwelve -match 'appointment remains in progress' -and
    $decisionTwelve -match 'This decision does not authorize:' -and
    $decisionTwelveExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwelveExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0012'; expires = if ($decisionTwelveExpiryMatch.Success) { $decisionTwelveExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0013 approves only neutral synthetic pharmacy search and a patient-confirmed destination draft and remains unexpired' -Passed (
    $decisionThirteen -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirteen -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionThirteen -match 'search a deterministic non-production pharmacy directory' -and
    $decisionThirteen -match 'unsigned consultation planning draft' -and
    $decisionThirteen -match 'This decision does not authorize:' -and
    $decisionThirteenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirteenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0013'; expires = if ($decisionThirteenExpiryMatch.Success) { $decisionThirteenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0014 approves only the conditional physician-authored safety-disposition draft and remains unexpired' -Passed (
    $decisionFourteen -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFourteen -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionFourteen -match 'structured, physician-authored safety-disposition draft' -and
    $decisionFourteen -match 'The application never chooses a disposition or supplies clinical instructions' -and
    $decisionFourteen -match 'This decision does not authorize' -and
    $decisionFourteenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFourteenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0014'; expires = if ($decisionFourteenExpiryMatch.Success) { $decisionFourteenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0015 approves only the owner-bound minimized completion-prerequisites review and remains unexpired' -Passed (
    $decisionFifteen -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFifteen -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionFifteen -match 'minimized, server-derived review of evidence relevant to eventual finalization' -and
    $decisionFifteen -match 'SOAP section contains nonblank content but may not assert' -and
    $decisionFifteen -match '`signingEnabled`, `completionEnabled`, `patientDeliveryEnabled`, and `downstreamCreationEnabled` are always false' -and
    $decisionFifteen -match 'This decision does not authorize' -and
    $decisionFifteenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFifteenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0015'; expires = if ($decisionFifteenExpiryMatch.Success) { $decisionFifteenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0016 approves only the synthetic prescription-preparation draft without legal effect and remains unexpired' -Passed (
    $decisionSixteen -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionSixteen -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionSixteen -match 'append one versioned prescription-preparation draft' -and
    $decisionSixteen -match 'Unknown catalog codes and every nonblank controlled-substance schedule fail closed' -and
    $decisionSixteen -match '`legalEffect`, `signed`, `safetyChecked`, `transmissionQueued`, `transmitted`, `patientDelivered`, and `completionEnabled` remain false' -and
    $decisionSixteen -match 'This decision does not authorize' -and
    $decisionSixteenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionSixteenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0016'; expires = if ($decisionSixteenExpiryMatch.Success) { $decisionSixteenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0017 approves only the staff-governed prospective-applicant identity review without patient promotion and remains unexpired' -Passed (
    $decisionSeventeen -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionSeventeen -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionSeventeen -match 'append exactly one bounded identity-review decision' -and
    $decisionSeventeen -match 'It is not identity proofing' -and
    $decisionSeventeen -match 'This decision does not authorize' -and
    $decisionSeventeenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionSeventeenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0017'; expires = if ($decisionSeventeenExpiryMatch.Success) { $decisionSeventeenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0018 approves only one applicant-owned emergency-first prospective safety screen without care authorization and remains unexpired' -Passed (
    $decisionEighteen -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionEighteen -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionEighteen -match 'submit one emergency-first universal safety screen' -and
    $decisionEighteen -match 'Emergency > UrgentInPerson > InPersonRequired > ClinicalReview > TelehealthEligible' -and
    $decisionEighteen -match 'This decision does not authorize' -and
    $decisionEighteenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionEighteenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0018'; expires = if ($decisionEighteenExpiryMatch.Success) { $decisionEighteenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0019 approves only controlled applicant-owned visit-purpose classification without clinical eligibility and remains unexpired' -Passed (
    $decisionNineteen -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionNineteen -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionNineteen -match 'classify the visit purpose as exactly `migraine` or `sleep`' -and
    $decisionNineteen -match 'This is navigation and intake classification only' -and
    $decisionNineteen -match 'This decision does not authorize' -and
    $decisionNineteenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionNineteenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0019'; expires = if ($decisionNineteenExpiryMatch.Success) { $decisionNineteenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0020 approves only applicant-owned synthetic practice-plan discovery without individual eligibility or coverage and remains unexpired' -Passed (
    $decisionTwenty -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwenty -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTwenty -match 'record one practice-level network precheck' -and
    $decisionTwenty -match 'This is plan discovery only' -and
    $decisionTwenty -match 'Future individual eligibility must use a separately approved standards-oriented adapter modeling HIPAA-adopted X12 270/271 semantics' -and
    $decisionTwenty -match 'This decision does not authorize' -and
    $decisionTwentyExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentyExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0020'; expires = if ($decisionTwentyExpiryMatch.Success) { $decisionTwentyExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0021 approves only protected synthetic member-insurance capture without matching, eligibility, coverage, or care and remains unexpired' -Passed (
    $decisionTwentyOne -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentyOne -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTwentyOne -match 'stored only inside an opaque ASP.NET Core Data Protection payload' -and
    $decisionTwentyOne -match 'This is protected demonstration-data capture only' -and
    $decisionTwentyOne -match 'Future eligibility must use a separately approved, standards-oriented X12 270/271 adapter' -and
    $decisionTwentyOne -match 'This decision does not authorize' -and
    $decisionTwentyOneExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentyOneExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0021'; expires = if ($decisionTwentyOneExpiryMatch.Success) { $decisionTwentyOneExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0022 approves only a synthetic normalized eligibility result without exact network, canonical coverage, or care and remains unexpired' -Passed (
    $decisionTwentyTwo -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentyTwo -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTwentyTwo -match 'ASC_X12N_270_271_005010X279A1' -and
    $decisionTwentyTwo -match 'separately represents transport outcome' -and
    $decisionTwentyTwo -match 'This is adapter-contract evidence only' -and
    $decisionTwentyTwo -match 'This decision does not authorize' -and
    $decisionTwentyTwoExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentyTwoExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0022'; expires = if ($decisionTwentyTwoExpiryMatch.Success) { $decisionTwentyTwoExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0023 approves only a synthetic practice-network determination without rendering-physician, coverage, or care consequence and remains unexpired' -Passed (
    $decisionTwentyThree -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentyThree -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTwentyThree -match 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
    $decisionTwentyThree -match 'receives no member/subscriber fields' -and
    $decisionTwentyThree -match 'rendering-physician participation check' -and
    $decisionTwentyThree -match 'This decision does not authorize' -and
    $decisionTwentyThreeExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentyThreeExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0023'; expires = if ($decisionTwentyThreeExpiryMatch.Success) { $decisionTwentyThreeExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0024 approves only an opaque synthetic identity-proofing process without real evidence, assurance, promotion, or care and remains unexpired' -Passed (
    $decisionTwentyFour -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentyFour -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTwentyFour -match 'NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY' -and
    $decisionTwentyFour -match 'receives no legal name, birth date, contact value, address, insurance value, government identifier, image, video, biometric, or raw evidence' -and
    $decisionTwentyFour -match 'assuranceLevelAchieved=None' -and
    $decisionTwentyFour -match 'This decision does not authorize' -and
    $decisionTwentyFourExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentyFourExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0024'; expires = if ($decisionTwentyFourExpiryMatch.Success) { $decisionTwentyFourExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0025 approves only a staff-governed synthetic promotion authorization without patient creation, assurance, request, queue, or care and remains unexpired' -Passed (
    $decisionTwentyFive -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentyFive -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTwentyFive -match 'AuthorizedForSyntheticPromotion' -and
    $decisionTwentyFive -match 'assuranceLevelAchieved=None' -and
    $decisionTwentyFive -match 'The applicant remains prospective' -and
    $decisionTwentyFive -match 'This decision does not authorize' -and
    $decisionTwentyFiveExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentyFiveExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0025'; expires = if ($decisionTwentyFiveExpiryMatch.Success) { $decisionTwentyFiveExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0026 approves only atomic synthetic patient-shell promotion or privacy-safe duplicate blocking without portal, linkage, request, queue, or care and remains unexpired' -Passed (
    $decisionTwentySix -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentySix -match '(?m)^Approved date: 2026-08-27\s*$' -and
    $decisionTwentySix -match 'PromoteAuthorizedSyntheticApplicant' -and
    $decisionTwentySix -match 'BlockedPossiblePatientMatch' -and
    $decisionTwentySix -match 'TH-PAT-<applicant-guid-n>' -and
    $decisionTwentySix -match 'portal_enabled=false' -and
    $decisionTwentySix -match 'This decision does not authorize' -and
    $decisionTwentySixExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentySixExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0026'; expires = if ($decisionTwentySixExpiryMatch.Success) { $decisionTwentySixExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0027 approves only applicant-owned state-specific synthetic notice acknowledgment without legal consent, portal, request, queue, or care and remains unexpired' -Passed (
    $decisionTwentySeven -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentySeven -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionTwentySeven -match 'SyntheticTelehealthNoticeAcknowledged' -and
    $decisionTwentySeven -match 'legalConsentEstablished=false' -and
    $decisionTwentySeven -match 'clinicianConsentDocumented=false' -and
    $decisionTwentySeven -match 'server selects the notice by the passing safety screen' -and
    $decisionTwentySeven -match 'This decision does not authorize' -and
    $decisionTwentySevenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentySevenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0027'; expires = if ($decisionTwentySevenExpiryMatch.Success) { $decisionTwentySevenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0028 approves only applicant-owned no-edit minimum registration-details confirmation without identity, patient mutation, insurance, request, queue, or care and remains unexpired' -Passed (
    $decisionTwentyEight -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentyEight -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionTwentyEight -match 'SyntheticMinimumRegistrationDetailsConfirmed' -and
    $decisionTwentyEight -match 'identityAssuranceEstablished=false' -and
    $decisionTwentyEight -match 'insuranceConfirmed=false' -and
    $decisionTwentyEight -match 'never edits the prospective applicant or canonical patient shell' -and
    $decisionTwentyEight -match 'This decision does not authorize' -and
    $decisionTwentyEightExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentyEightExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0028'; expires = if ($decisionTwentyEightExpiryMatch.Success) { $decisionTwentyEightExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0029 approves only applicant-owned no-edit synthetic insurance handoff confirmation without canonical coverage, rendering-physician verification, request, queue, or care and remains unexpired' -Passed (
    $decisionTwentyNine -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionTwentyNine -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionTwentyNine -match 'SyntheticInsuranceDetailsConfirmed' -and
    $decisionTwentyNine -match 'renderingPhysicianNetworkChecked=false' -and
    $decisionTwentyNine -match 'canonicalCoverageCreated=false' -and
    $decisionTwentyNine -match 'never converts stale evidence into current evidence' -and
    $decisionTwentyNine -match 'This decision does not authorize' -and
    $decisionTwentyNineExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionTwentyNineExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0029'; expires = if ($decisionTwentyNineExpiryMatch.Success) { $decisionTwentyNineExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0030 approves only applicant-owned synthetic communication/access readiness without arranging services, technology readiness, patient mutation, request, queue, or care and remains unexpired' -Passed (
    $decisionThirty -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirty -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirty -match 'SyntheticCommunicationAccessReadinessRecorded' -and
    $decisionThirty -match 'interpreterAssigned=false' -and
    $decisionThirty -match 'communicationArrangementCompleted=false' -and
    $decisionThirty -match 'does not arrange an interpreter or accommodation' -and
    $decisionThirty -match 'This decision does not authorize' -and
    $decisionThirtyExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtyExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0030'; expires = if ($decisionThirtyExpiryMatch.Success) { $decisionThirtyExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0031 approves only applicant-owned coarse synthetic device preparation without media, technology readiness, waiting room, request, queue, or care and remains unexpired' -Passed (
    $decisionThirtyOne -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtyOne -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtyOne -match 'SyntheticDevicePreparationRecorded' -and
    $decisionThirtyOne -match 'must stop every acquired track immediately' -and
    $decisionThirtyOne -match 'technologyReady=false' -and
    $decisionThirtyOne -match 'mediaSessionCreated=false' -and
    $decisionThirtyOne -match 'This decision does not authorize' -and
    $decisionThirtyOneExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtyOneExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0031'; expires = if ($decisionThirtyOneExpiryMatch.Success) { $decisionThirtyOneExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0032 approves only applicant-owned coarse synthetic clinical-information inventory without clinical details, reconciliation, clinician review, request, queue, care, or prescribing and remains unexpired' -Passed (
    $decisionThirtyTwo -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtyTwo -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtyTwo -match 'SyntheticClinicalInformationInventoryRecorded' -and
    $decisionThirtyTwo -match 'PatientReportsNone' -and
    $decisionThirtyTwo -match 'PendingClinicianReconciliation' -and
    $decisionThirtyTwo -match 'medicationListReconciled=false' -and
    $decisionThirtyTwo -match 'clinicianReviewCreated=false' -and
    $decisionThirtyTwo -match 'prescribingEnabled=false' -and
    $decisionThirtyTwo -match 'This decision does not authorize' -and
    $decisionThirtyTwoExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtyTwoExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0032'; expires = if ($decisionThirtyTwoExpiryMatch.Success) { $decisionThirtyTwoExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0033 approves only applicant-owned bounded synthetic medication information without canonical medication, reconciliation, interaction checking, clinician review, request, queue, care, or prescribing and remains unexpired' -Passed (
    $decisionThirtyThree -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtyThree -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtyThree -match 'SyntheticMedicationInformationRecorded' -and
    $decisionThirtyThree -match 'LOCAL_SYNTHETIC_ONLY' -and
    $decisionThirtyThree -match 'rxNormMapped=false' -and
    $decisionThirtyThree -match 'interactionCheckPerformed=false' -and
    $decisionThirtyThree -match 'clinicianReviewCreated=false' -and
    $decisionThirtyThree -match 'prescribingEnabled=false' -and
    $decisionThirtyThree -match 'This decision does not authorize' -and
    $decisionThirtyThreeExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtyThreeExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0033'; expires = if ($decisionThirtyThreeExpiryMatch.Success) { $decisionThirtyThreeExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0034 approves only applicant-owned bounded synthetic allergy information without canonical allergy, confirmed negation, reconciliation, contraindication checking, alert, clinician review, request, queue, care, or prescribing and remains unexpired' -Passed (
    $decisionThirtyFour -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtyFour -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtyFour -match 'SyntheticAllergyInformationRecorded' -and
    $decisionThirtyFour -match 'LOCAL_SYNTHETIC_ONLY' -and
    $decisionThirtyFour -match 'snomedCtMapped=false' -and
    $decisionThirtyFour -match 'allergyIntoleranceCreated=false' -and
    $decisionThirtyFour -match 'contraindicationCheckPerformed=false' -and
    $decisionThirtyFour -match 'prescribingEnabled=false' -and
    $decisionThirtyFour -match 'This decision does not authorize' -and
    $decisionThirtyFourExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtyFourExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0034'; expires = if ($decisionThirtyFourExpiryMatch.Success) { $decisionThirtyFourExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0035 approves only applicant-owned broad synthetic health-history topics without diagnosis, canonical clinical resources, reconciliation, risk evaluation, triage change, clinician review, request, queue, care, or prescribing and remains unexpired' -Passed (
    $decisionThirtyFive -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtyFive -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtyFive -match 'SyntheticHealthHistoryInformationRecorded' -and
    $decisionThirtyFive -match 'LOCAL_SYNTHETIC_ONLY' -and
    $decisionThirtyFive -match 'icd10CmMapped=false' -and
    $decisionThirtyFive -match 'conditionCreated=false' -and
    $decisionThirtyFive -match 'riskModifierEvaluated=false' -and
    $decisionThirtyFive -match 'clinicalTriageChanged=false' -and
    $decisionThirtyFive -match 'prescribingEnabled=false' -and
    $decisionThirtyFive -match 'This decision does not authorize' -and
    $decisionThirtyFiveExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtyFiveExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0035'; expires = if ($decisionThirtyFiveExpiryMatch.Success) { $decisionThirtyFiveExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0036 approves only applicant-owned no-edit synthetic clinical-information summary confirmation without new detail, confirmed negative, reconciliation, QuestionnaireResponse, clinician review, completed intake, eligibility, request, queue, care, or prescribing and remains unexpired' -Passed (
    $decisionThirtySix -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtySix -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtySix -match 'SyntheticClinicalInformationSummaryConfirmed' -and
    $decisionThirtySix -match 'questionnaireResponseCreated=false' -and
    $decisionThirtySix -match 'confirmedNegativeEstablished=false' -and
    $decisionThirtySix -match 'clinicianReviewCreated=false' -and
    $decisionThirtySix -match 'clinicalIntakeCompleted=false' -and
    $decisionThirtySix -match 'prescribingEnabled=false' -and
    $decisionThirtySix -match 'This decision does not authorize' -and
    $decisionThirtySixExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtySixExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0036'; expires = if ($decisionThirtySixExpiryMatch.Success) { $decisionThirtySixExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0037 approves only applicant-owned no-edit synthetic pre-request readiness acknowledgment without assurance, fulfilled support, technology readiness, reconciliation, task, acceptance, request, queue, financial, integration, or care consequence and remains unexpired' -Passed (
    $decisionThirtySeven -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtySeven -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtySeven -match 'SyntheticPreRequestReadinessAcknowledged' -and
    $decisionThirtySeven -match 'identityAssuranceEstablished=false' -and
    $decisionThirtySeven -match 'interpreterOrAccommodationArranged=false' -and
    $decisionThirtySeven -match 'staffReviewCreated=false' -and
    $decisionThirtySeven -match 'requestCreated=false' -and
    $decisionThirtySeven -match 'billingEnabled=false' -and
    $decisionThirtySeven -match 'externalCallPerformed=false' -and
    $decisionThirtySeven -match 'This decision does not authorize' -and
    $decisionThirtySevenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtySevenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0037'; expires = if ($decisionThirtySevenExpiryMatch.Success) { $decisionThirtySevenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0038 approves only one applicant-owned synthetic practice-review work item without acceptance, request, patient or clinician care queue, appointment, care, financial, integration, or external consequence and remains unexpired' -Passed (
    $decisionThirtyEight -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtyEight -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtyEight -match 'SyntheticPracticeReviewSubmitted' -and
    $decisionThirtyEight -match 'PendingPracticeReview' -and
    $decisionThirtyEight -match 'staffReviewCreated=true' -and
    $decisionThirtyEight -match 'telehealthRequestCreated=false' -and
    $decisionThirtyEight -match 'patientCareQueueEntered=false' -and
    $decisionThirtyEight -match 'clinicianQueueEntered=false' -and
    $decisionThirtyEight -match 'careAuthorized=false' -and
    $decisionThirtyEight -match 'externalCallPerformed=false' -and
    $decisionThirtyEight -match 'This decision does not authorize' -and
    $decisionThirtyEightExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtyEightExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0038'; expires = if ($decisionThirtyEightExpiryMatch.Success) { $decisionThirtyEightExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0039 approves only a GET-only minimized staff practice-review inbox without assignment, priority, action, request, care queue, financial, integration, or external consequence and remains unexpired' -Passed (
    $decisionThirtyNine -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionThirtyNine -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionThirtyNine -match 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX' -and
    $decisionThirtyNine -match 'GET' -and
    $decisionThirtyNine -match 'staffReviewWorkItemExists=true' -and
    $decisionThirtyNine -match 'staffActionTaken=false' -and
    $decisionThirtyNine -match 'telehealthRequestCreated=false' -and
    $decisionThirtyNine -match 'patientCareQueueEntered=false' -and
    $decisionThirtyNine -match 'careAuthorized=false' -and
    $decisionThirtyNine -match 'externalCallPerformed=false' -and
    $decisionThirtyNine -match 'This decision does not authorize' -and
    $decisionThirtyNineExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionThirtyNineExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0039'; expires = if ($decisionThirtyNineExpiryMatch.Success) { $decisionThirtyNineExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0040 approves only a 120-second immutable staff duplicate-work claim without priority, disposition, contact, request, care queue, financial, integration, or external consequence and remains unexpired' -Passed (
    $decisionForty -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionForty -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionForty -match 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM' -and
    $decisionForty -match '120 seconds' -and
    $decisionForty -match 'first writer' -and
    $decisionForty -match 'staffActionTaken=true' -and
    $decisionForty -match 'assigned=true' -and
    $decisionForty -match 'priorityAssigned=false' -and
    $decisionForty -match 'telehealthRequestCreated=false' -and
    $decisionForty -match 'careAuthorized=false' -and
    $decisionForty -match 'externalCallPerformed=false' -and
    $decisionForty -match 'This decision does not authorize' -and
    $decisionFortyExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortyExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0040'; expires = if ($decisionFortyExpiryMatch.Success) { $decisionFortyExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0041 approves only a current-claimant read-only minimized operational packet without lease extension, chart or source detail, disposition, contact, request, care queue, financial, integration, or external consequence and remains unexpired' -Passed (
    $decisionFortyOne -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortyOne -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionFortyOne -match 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET' -and
    $decisionFortyOne -match 'Current claimant only' -and
    $decisionFortyOne -match 'does not renew, extend, release, replace, or otherwise mutate the claim' -and
    $decisionFortyOne -match 'staffActionTaken=true' -and
    $decisionFortyOne -match 'assignedToCurrentUser=true' -and
    $decisionFortyOne -match 'priorityAssigned=false' -and
    $decisionFortyOne -match 'telehealthRequestCreated=false' -and
    $decisionFortyOne -match 'careAuthorized=false' -and
    $decisionFortyOne -match 'externalCallPerformed=false' -and
    $decisionFortyOne -match 'This decision does not authorize' -and
    $decisionFortyOneExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortyOneExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0041'; expires = if ($decisionFortyOneExpiryMatch.Success) { $decisionFortyOneExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0042 approves only a current-claimant positive operational authorization for a separately gated future synthetic request step and remains unexpired' -Passed (
    $decisionFortyTwo -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortyTwo -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionFortyTwo -match 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION' -and
    $decisionFortyTwo -match 'AuthorizedForSyntheticRequestCreation' -and
    $decisionFortyTwo -match 'OperationalPrerequisitesReviewed' -and
    $decisionFortyTwo -match 'Three independent acknowledgments are mandatory' -and
    $decisionFortyTwo -match 'requestCreationAuthorized=true' -and
    $decisionFortyTwo -match 'telehealthRequestCreated=false' -and
    $decisionFortyTwo -match 'patientCareQueueEntered=false' -and
    $decisionFortyTwo -match 'clinicianQueueEntered=false' -and
    $decisionFortyTwo -match 'careAuthorized=false' -and
    $decisionFortyTwo -match 'externalCallPerformed=false' -and
    $decisionFortyTwo -match 'This decision does not authorize' -and
    $decisionFortyTwoExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortyTwoExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0042'; expires = if ($decisionFortyTwoExpiryMatch.Success) { $decisionFortyTwoExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0043 approves only one applicant-bound authorization-gated Draft request without contact, doctor search, queue, appointment, encounter, consent, care, financial, integration, or external consequence and remains unexpired' -Passed (
    $decisionFortyThree -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortyThree -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionFortyThree -match 'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION' -and
    $decisionFortyThree -match 'SyntheticPracticeReviewAuthorized' -and
    $decisionFortyThree -match 'SyntheticRequestCreated' -and
    $decisionFortyThree -match 'telehealthRequestCreated=true' -and
    $decisionFortyThree -match 'patientCareQueueEntered=false' -and
    $decisionFortyThree -match 'clinicianQueueEntered=false' -and
    $decisionFortyThree -match 'doctorSearchStarted=false' -and
    $decisionFortyThree -match 'queuePositionAssigned=false' -and
    $decisionFortyThree -match 'careAuthorized=false' -and
    $decisionFortyThree -match 'externalCallPerformed=false' -and
    $decisionFortyThree -match 'This decision does not authorize' -and
    $decisionFortyThreeExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortyThreeExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0043'; expires = if ($decisionFortyThreeExpiryMatch.Success) { $decisionFortyThreeExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0044 approves only applicant-owned current-location and masked-callback confirmation with a Draft-to-LocationConfirmed request transition and no triage, queue, care, financial, integration, or external consequence and remains unexpired' -Passed (
    $decisionFortyFour -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortyFour -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionFortyFour -match 'SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION' -and
    $decisionFortyFour -match 'SyntheticRequestCreated' -and
    $decisionFortyFour -match 'LocationConfirmed' -and
    $decisionFortyFour -match 'Four independent confirmations are mandatory' -and
    $decisionFortyFour -match 'changed location fails closed' -and
    $decisionFortyFour -match 'one append-only `telehealth_patient_locations` row' -and
    $decisionFortyFour -match 'triage result or clinical review' -and
    $decisionFortyFour -match 'patientCareQueueEntered=false|patient (?:or clinician )?care-?queue|either care queue' -and
    $decisionFortyFour -match 'doctor search' -and
    $decisionFortyFour -match 'external action|external communication' -and
    $decisionFortyFour -match 'This decision does not authorize' -and
    $decisionFortyFourExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortyFourExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0044'; expires = if ($decisionFortyFourExpiryMatch.Success) { $decisionFortyFourExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0045 approves only the applicant-owned request universal safety assessment, preserves complaint-specific triage and clinical review work as later gates, and remains unexpired' -Passed (
    $decisionFortyFive -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortyFive -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionFortyFive -match 'SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT' -and
    $decisionFortyFive -match 'four-answer universal safety screen' -and
    $decisionFortyFive -match 'EmergencyRedirected' -and
    $decisionFortyFive -match 'InPersonRecommended' -and
    $decisionFortyFive -match 'ClinicalReview' -and
    $decisionFortyFive -match 'SafetyScreening' -and
    $decisionFortyFive -match 'complaint-specific triage' -and
    $decisionFortyFive -match 'no reviewer assignment or review action' -and
    $decisionFortyFive -match 'no answer or result persistence in browser storage' -and
    $decisionFortyFive -match 'This decision does not authorize' -and
    $decisionFortyFiveExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortyFiveExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0045'; expires = if ($decisionFortyFiveExpiryMatch.Success) { $decisionFortyFiveExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0046 approves only deterministic unapproved synthetic migraine or sleep complaint triage, requires a false-only publication gate, and remains unexpired' -Passed (
    $decisionFortySix -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortySix -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionFortySix -match 'SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE' -and
    $decisionFortySix -match 'migraine' -and
    $decisionFortySix -match 'sleep' -and
    $decisionFortySix -match 'ordered fired rules' -and
    $decisionFortySix -match 'NotSure' -and
    $decisionFortySix -match 'Unsupported' -and
    $decisionFortySix -match 'UNAPPROVED_SYNTHETIC' -and
    $decisionFortySix -match 'medical-director approval, golden-case approval, and production publication are all false' -and
    $decisionFortySix -match 'no answer/result persistence in browser storage' -and
    $decisionFortySix -match 'This decision does not authorize' -and
    $decisionFortySixExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortySixExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0046'; expires = if ($decisionFortySixExpiryMatch.Success) { $decisionFortySixExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0047 approves only one applicant-owned no-free-text intake snapshot and request-only Intake version 4 to pending Verification version 5 transition while every later gate remains closed' -Passed (
    $decisionFortySeven -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortySeven -match '(?m)^Approved date: 2026-08-28\s*$' -and
    $decisionFortySeven -match 'SYNTHETIC_APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION' -and
    $decisionFortySeven -match '`Intake` version 4' -and
    $decisionFortySeven -match '`Verification` version 5' -and
    $decisionFortySeven -match 'eight explicit confirmations' -and
    $decisionFortySeven -match 'client cannot submit free text' -and
    $decisionFortySeven -match 'UNAPPROVED_SYNTHETIC' -and
    $decisionFortySeven -match 'medical-director approval, clinical golden-case approval, and production publication remain false' -and
    $decisionFortySeven -match 'No patient confirmation row' -and
    $decisionFortySeven -match 'doctor search' -and
    $decisionFortySeven -match 'This decision does not authorize' -and
    $decisionFortySevenExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortySevenExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0047'; expires = if ($decisionFortySevenExpiryMatch.Success) { $decisionFortySevenExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0048 approves only one masked request insurance-source receipt and request-only pending Verification version 5 to 6 advance while historical-result reuse and every current verification or downstream gate remain closed' -Passed (
    $decisionFortyEight -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortyEight -match '(?m)^Approved date: 2026-08-29\s*$' -and
    $decisionFortyEight -match 'SYNTHETIC_APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION' -and
    $decisionFortyEight -match '`Verification` version 5' -and
    $decisionFortyEight -match '`Verification` version 6' -and
    $decisionFortyEight -match 'seven explicit confirmations' -and
    $decisionFortyEight -match 'historical provenance only' -and
    $decisionFortyEight -match 'fresh_verification_requested' -and
    $decisionFortyEight -match 'prior_result_reused' -and
    $decisionFortyEight -match 'does not decrypt or duplicate the protected payload' -and
    $decisionFortyEight -match 'rendering-physician participation' -and
    $decisionFortyEight -match 'No canonical insurance record' -and
    $decisionFortyEight -match 'doctor search' -and
    $decisionFortyEight -match 'This decision does not authorize' -and
    $decisionFortyEightExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortyEightExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0048'; expires = if ($decisionFortyEightExpiryMatch.Success) { $decisionFortyEightExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0049 approves only one fresh bounded request eligibility result and request-only pending Verification version 6 to 7 advance while exact network and every downstream gate remain closed' -Passed (
    $decisionFortyNine -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFortyNine -match '(?m)^Approved date: 2026-08-29\s*$' -and
    $decisionFortyNine -match 'SYNTHETIC_APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION' -and
    $decisionFortyNine -match '`Verification` version 6' -and
    $decisionFortyNine -match '`Verification` version 7' -and
    $decisionFortyNine -match 'two explicit true values' -and
    $decisionFortyNine -match 'decrypts the existing protected synthetic member payload only in server memory' -and
    $decisionFortyNine -match 'ASC_X12N_270_271_005010X279A1' -and
    $decisionFortyNine -match 'creates no X12 payload' -and
    $decisionFortyNine -match 'rendering-physician participation' -and
    $decisionFortyNine -match 'No prior request-time eligibility result' -and
    $decisionFortyNine -match 'doctor search' -and
    $decisionFortyNine -match 'This decision does not authorize' -and
    $decisionFortyNineExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFortyNineExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0049'; expires = if ($decisionFortyNineExpiryMatch.Success) { $decisionFortyNineExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0050 approves only one fresh practice-level network result and request-only pending Verification version 7 to 8 advance while exact physician participation and every downstream gate remain closed' -Passed (
    $decisionFifty -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFifty -match '(?m)^Approved date: 2026-08-29\s*$' -and
    $decisionFifty -match 'SYNTHETIC_APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION' -and
    $decisionFifty -match '`Verification` version 7' -and
    $decisionFifty -match '`Verification` version 8' -and
    $decisionFifty -match 'three explicit true values' -and
    $decisionFifty -match 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
    $decisionFifty -match 'creates no FHIR resource' -and
    $decisionFifty -match 'no rendering physician' -and
    $decisionFifty -match 'No prior request practice-network result' -and
    $decisionFifty -match 'doctor search' -and
    $decisionFifty -match 'This decision does not authorize' -and
    $decisionFiftyExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFiftyExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0050'; expires = if ($decisionFiftyExpiryMatch.Success) { $decisionFiftyExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0051 approves only one server-owned rendering candidate and request-only pending Verification version 8 to 9 advance while assignment, network, and every downstream gate remain closed' -Passed (
    $decisionFiftyOne -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFiftyOne -match '(?m)^Approved date: 2026-08-29\s*$' -and
    $decisionFiftyOne -match 'SYNTHETIC_APPLICANT_REQUEST_RENDERING_CANDIDATE_SELECTION' -and
    $decisionFiftyOne -match '`Verification` version 8' -and
    $decisionFiftyOne -match '`Verification` version 9' -and
    $decisionFiftyOne -match 'four explicit true values' -and
    $decisionFiftyOne -match 'server-owned' -and
    $decisionFiftyOne -match 'candidate for a future exact network evaluation' -and
    $decisionFiftyOne -match 'not clinician assignment' -and
    $decisionFiftyOne -match 'masked provider reference' -and
    $decisionFiftyOne -match 'This decision does not authorize' -and
    $decisionFiftyOneExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFiftyOneExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0051'; expires = if ($decisionFiftyOneExpiryMatch.Success) { $decisionFiftyOneExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0052 approves only one server-owned effective-dated participation prerequisite context and request-only pending Verification version 9 to 10 advance while real verification, exact network, and every downstream gate remain closed' -Passed (
    $decisionFiftyTwo -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFiftyTwo -match '(?m)^Approved date: 2026-08-29\s*$' -and
    $decisionFiftyTwo -match 'SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_CONTEXT' -and
    $decisionFiftyTwo -match '`Verification` version 9' -and
    $decisionFiftyTwo -match '`Verification` version 10' -and
    $decisionFiftyTwo -match 'four explicit true values' -and
    $decisionFiftyTwo -match 'server owns every prerequisite reference and effective date' -and
    $decisionFiftyTwo -match 'NPI remains an identifier rather than proof of licensure or credentialing' -and
    $decisionFiftyTwo -match 'does not serialize FHIR' -and
    $decisionFiftyTwo -match 'masked provider/billing references' -and
    $decisionFiftyTwo -match 'This decision does not authorize' -and
    $decisionFiftyTwoExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFiftyTwoExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0052'; expires = if ($decisionFiftyTwoExpiryMatch.Success) { $decisionFiftyTwoExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0053 approves only one exact server-owned synthetic participation tuple and request-only pending Verification version 10 to 11 advance while real verification and every downstream gate remain closed' -Passed (
    $decisionFiftyThree -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFiftyThree -match '(?m)^Approved date: 2026-08-29\s*$' -and
    $decisionFiftyThree -match 'SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_EVALUATION' -and
    $decisionFiftyThree -match '`Verification` version 10' -and
    $decisionFiftyThree -match '`Verification` version 11' -and
    $decisionFiftyThree -match 'four explicit true values' -and
    $decisionFiftyThree -match 'new-patient acceptance tuple' -and
    $decisionFiftyThree -match 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
    $decisionFiftyThree -match 'does not serialize FHIR' -and
    $decisionFiftyThree -match 'masked provider/billing references' -and
    $decisionFiftyThree -match 'This decision does not authorize' -and
    $decisionFiftyThreeExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFiftyThreeExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0053'; expires = if ($decisionFiftyThreeExpiryMatch.Success) { $decisionFiftyThreeExpiryMatch.Groups[1].Value } else { $null } }
Add-ValidationCheck -Name 'Decision 0054 approves only applicant submission to staff operational review and request transition from Verification version 11 to OperationalReview version 12 while acceptance and every downstream gate remain closed' -Passed (
    $decisionFiftyFour -match '(?m)^Status: Approved — active for the exact disabled synthetic slice below\s*$' -and
    $decisionFiftyFour -match '(?m)^Approved date: 2026-08-29\s*$' -and
    $decisionFiftyFour -match 'SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION' -and
    $decisionFiftyFour -match '`Verification` version 11' -and
    $decisionFiftyFour -match '`OperationalReview` version 12' -and
    $decisionFiftyFour -match 'four explicit true values' -and
    $decisionFiftyFour -match 'existing practice-scoped administrator operational-review projection' -and
    $decisionFiftyFour -match 'not practice acceptance' -and
    $decisionFiftyFour -match 'No canonical coverage' -and
    $decisionFiftyFour -match 'This decision does not authorize' -and
    $decisionFiftyFourExpiryMatch.Success -and [DateTime]::UtcNow.Date -le $decisionFiftyFourExpiryDate.Date
) -Details @{ decision = 'TH-DEC-0054'; expires = if ($decisionFiftyFourExpiryMatch.Success) { $decisionFiftyFourExpiryMatch.Groups[1].Value } else { $null } }

$workflow = if (Test-Path -LiteralPath $workflowPath -PathType Leaf) { Get-Content -Raw -LiteralPath $workflowPath } else { '' }
$workflowInvocationPattern = '(?m)^\s*run:\s*pwsh\s+-NoProfile\s+-File\s+\./scripts/Test-TelehealthPlanningArtifacts\.ps1\s*$'
Add-ValidationCheck -Name 'Existing verification workflow invokes this validator as a mandatory step' -Passed ($workflow -match $workflowInvocationPattern) -Details '.github/workflows/verify.yml'

$artifactPaths = @($specificationFiles.FullName) + @(
    $backlogPath,
    $safeguardsPath,
    $wireframePath,
    $decisionOnePath,
    $decisionTwoPath,
    $decisionThreePath,
    $decisionFourPath,
    $decisionFivePath,
    $decisionSixPath,
    $decisionSevenPath,
    $decisionEightPath,
    $decisionNinePath,
    $decisionTenPath,
    $decisionElevenPath,
    $decisionTwelvePath,
    $decisionThirteenPath,
    $decisionFourteenPath,
    $decisionFifteenPath,
    $decisionSixteenPath,
    $decisionSeventeenPath,
    $decisionEighteenPath,
    $decisionNineteenPath,
    $decisionTwentyPath,
    $decisionTwentyOnePath,
    $decisionTwentyTwoPath,
    $decisionTwentyThreePath,
    $decisionTwentyFourPath,
    $decisionTwentyFivePath,
    $decisionTwentySixPath,
    $decisionTwentySevenPath,
    $decisionTwentyEightPath,
    $decisionTwentyNinePath,
    $decisionThirtyPath,
    $decisionThirtyOnePath,
    $decisionThirtyTwoPath,
    $decisionThirtyThreePath,
    $decisionThirtyFourPath,
    $decisionThirtyFivePath,
    $decisionThirtySixPath,
    $decisionThirtySevenPath,
    $decisionThirtyEightPath,
    $decisionThirtyNinePath,
    $decisionFortyPath,
    $decisionFortyOnePath,
    $decisionFortyTwoPath,
    $decisionFortyThreePath,
    $decisionFortyFourPath,
    $decisionFortyFivePath,
    $decisionFortySixPath,
    $decisionFortySevenPath,
    $decisionFortyEightPath,
    $decisionFortyNinePath,
    $decisionFiftyPath,
    $decisionFiftyOnePath,
    $decisionFiftyTwoPath,
    $decisionFiftyThreePath,
    $decisionFiftyFourPath,
    $sprintTwentyNinePath,
    $sprintTwentyNineEvidencePath,
    $sprintThirtyPath,
    $sprintThirtyEvidencePath,
    $sprintThirtyOnePath,
    $sprintThirtyOneEvidencePath,
    $sprintThirtyTwoPath,
    $sprintThirtyTwoEvidencePath,
    $sprintThirtyThreePath,
    $sprintThirtyThreeEvidencePath,
    $sprintThirtyFourPath,
    $sprintThirtyFourEvidencePath,
    $sprintThirtyFivePath,
    $sprintThirtyFiveEvidencePath,
    $sprintThirtySixPath,
    $sprintThirtySixEvidencePath,
    $sprintThirtySevenPath,
    $sprintThirtySevenEvidencePath,
    $sprintThirtyEightPath,
    $sprintThirtyEightEvidencePath,
    $sprintThirtyNinePath,
    $sprintThirtyNineEvidencePath,
    $sprintFortyPath,
    $sprintFortyEvidencePath,
    $sprintFortyOnePath,
    $sprintFortyOneEvidencePath,
    $sprintFortyTwoPath,
    $sprintFortyTwoEvidencePath,
    $sprintFortyThreePath,
    $sprintFortyThreeEvidencePath,
    $sprintFortyFourPath,
    $sprintFortyFourEvidencePath,
    $sprintFortyFivePath,
    $sprintFortyFiveEvidencePath,
    $sprintFortySixPath,
    $sprintFortySixEvidencePath,
    $sprintFortySevenPath,
    $sprintFortySevenEvidencePath,
    $sprintFortyEightPath,
    $sprintFortyEightEvidencePath,
    $sprintFortyNinePath,
    $sprintFortyNineEvidencePath,
    $sprintFiftyPath,
    $sprintFiftyEvidencePath,
    $sprintFiftyOnePath,
    $sprintFiftyOneEvidencePath,
    (Join-Path $telehealthRoot 'README.md'),
    (Join-Path $telehealthRoot 'backlog/validation-report.md')
)
$artifactChecksums = @($artifactPaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Sort-Object -Unique | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    [ordered]@{
        path = Get-RepositoryRelativePath -Root $resolvedRoot -Path $_
        sha256 = $hash.Hash.ToLowerInvariant()
    }
})

$gitCommit = $null
try {
    $commitOutput = @(& git -C $resolvedRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -eq 0 -and $commitOutput.Count -gt 0) {
        $gitCommit = $commitOutput[0].Trim()
    }
}
catch {
    $gitCommit = $null
}

$completedAt = [DateTime]::UtcNow
$result = [ordered]@{
    schemaVersion = 1
    validator = 'Test-TelehealthPlanningArtifacts.ps1'
    validatorVersion = $validatorVersion
    claim = 'Telehealth planning artifacts are structurally consistent; this is not clinical, legal, security, accessibility, interoperability, implementation, or production-readiness evidence.'
    status = if ($failures.Count -eq 0) { 'passed' } else { 'failed' }
    commit = $gitCommit
    environment = [ordered]@{
        powerShell = $PSVersionTable.PSVersion.ToString()
        os = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        testMutation = $appliedTestMutation
    }
    startedAtUtc = $startedAt.ToString('o')
    completedAtUtc = $completedAt.ToString('o')
    durationMilliseconds = [math]::Round(($completedAt - $startedAt).TotalMilliseconds)
    checks = @($checks)
    failures = @($failures)
    artifactChecksums = $artifactChecksums
}

$result | ConvertTo-Json -Depth 8

if ($failures.Count -gt 0) {
    throw "Telehealth planning-artifact validation failed: $($failures -join '; ')"
}
