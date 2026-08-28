# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth prospective practice-network precheck proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-prospective-practice-network-precheck.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-Check([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}

function New-Secret {
    $bytes = [byte[]]::new(32)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    [Convert]::ToHexString($bytes).ToLowerInvariant()
}

function New-Key([string]$Prefix) { "$Prefix-$([Guid]::NewGuid().ToString('N'))" }

function Invoke-Status([string]$Method,[string]$Path,[hashtable]$Headers=@{},[object]$Body=$null) {
    $parameters = @{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=$Headers;TimeoutSec=30}
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 8
    }
    try { [int](Invoke-WebRequest @parameters).StatusCode }
    catch {
        if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
        else { throw }
    }
}

function Invoke-Scalar([string]$Sql) {
    Push-Location $solutionRoot
    try {
        $value = docker compose exec -T postgres psql -X -U avenchart -d avenchart -t -A -v ON_ERROR_STOP=1 -c $Sql
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL prospective practice-network proof query failed.' }
        ($value | Select-Object -Last 1).Trim()
    }
    finally { Pop-Location }
}

function Test-MutationRejected([string]$Sql) {
    Push-Location $solutionRoot
    try {
        docker compose exec -T postgres psql -X -U avenchart -d avenchart -v ON_ERROR_STOP=1 -c $Sql *> $null
        $LASTEXITCODE -ne 0
    }
    finally { Pop-Location }
}

function Get-Counts {
    (Invoke-Scalar @"
select json_build_object(
  'patients',(select count(*) from patients),
  'portalAccounts',(select count(*) from patient_portal_accounts),
  'insuranceRecords',(select count(*) from insurance_records),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'intakeSnapshots',(select count(*) from telehealth_intake_snapshots),
  'coverageSelections',(select count(*) from telehealth_coverage_selections),
  'coverageVerifications',(select count(*) from telehealth_coverage_verifications),
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims),
  'prescriptions',(select count(*) from prescriptions),
  'applicants',(select count(*) from telehealth_prospective_applicants),
  'reviewDecisions',(select count(*) from telehealth_applicant_identity_review_decisions),
  'safetyEvaluations',(select count(*) from telehealth_applicant_safety_triage_evaluations),
  'visitPurposes',(select count(*) from telehealth_applicant_visit_purposes),
  'networkPrechecks',(select count(*) from telehealth_applicant_practice_network_prechecks));
"@) | ConvertFrom-Json
}

function Login-Staff {
    Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='admin';password='pass'} | ConvertTo-Json) -TimeoutSec 30
}

function New-VisitPurposeApplicant(
    [string]$Label,
    [string]$StateCode,
    [string]$PurposeCategory,
    [hashtable]$AdminHeaders,
    [string]$Unique) {
    $secret = New-Secret
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp17-create')} `
        -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            legalFirstName='Network';legalLastName=$Label;dateOfBirth='1990-03-14'
            email="sp17-$($Label.ToLowerInvariant())-$Unique@example.test";phone='404-555-0199'
            residenceStateCode=$StateCode;postalCode=$(if($StateCode -eq 'CA'){'94105'}elseif($StateCode -eq 'FL'){'33101'}else{'30301'})
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp17-verify')
        } -ContentType 'application/json' -TimeoutSec 30 `
        -Body (@{expectedVersion=$created.version;verificationCode='246810'} | ConvertTo-Json)
    $reviewHeaders = $AdminHeaders.Clone()
    $reviewHeaders['X-Idempotency-Key'] = New-Key 'sp17-review'
    $reviewed = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" `
        -Method Put -Headers $reviewHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$verified.version
            decision='ApprovedForProspectiveIntake'
            reason="Synthetic $Label plan-discovery fixture reviewed with no candidate."
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $safety = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/safety-triage" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp17-safety')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$reviewed.applicantVersion
            currentLocationStateCode=$StateCode;currentLocationConfirmed=$true
            hasEmergencyWarning=$false;severeOrWorsening=$false
            requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $purpose = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/visit-purpose" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp17-purpose')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$safety.applicantVersion
            purposeCategory=$PurposeCategory;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    [pscustomobject]@{Secret=$secret;Created=$created;Reviewed=$reviewed;Safety=$safety;Purpose=$purpose}
}

function New-PrecheckBody([int]$ExpectedVersion,[string]$PlanKey) {
    @{expectedVersion=$ExpectedVersion;planKey=$PlanKey;syntheticDataConfirmed=$true}
}

function Invoke-Precheck([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/practice-network-precheck" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body | ConvertTo-Json)
}

try {
    $before = Get-Counts
    $unique = [Guid]::NewGuid().ToString('N')
    $admin = Login-Staff
    $adminHeaders = @{
        'X-AvenChart-Session'=$admin.sessionId
        'X-AvenChart-Facility-Id'='10'
        'X-AvenChart-Purpose-Of-Use'='treatment'
    }
    $confirmedApplicant = New-VisitPurposeApplicant 'Confirmed' 'GA' 'migraine' $adminHeaders $unique
    $unknownApplicant = New-VisitPurposeApplicant 'Unknown' 'CA' 'sleep' $adminHeaders $unique
    $outApplicant = New-VisitPurposeApplicant 'Out' 'FL' 'migraine' $adminHeaders $unique
    $concurrentApplicant = New-VisitPurposeApplicant 'Concurrent' 'GA' 'sleep' $adminHeaders $unique
    $applicants = @($confirmedApplicant,$unknownApplicant,$outApplicant,$concurrentApplicant)
    Add-Check 'Only approved passing-purpose GA, CA, and FL applicants reach plan discovery without identity proofing or patient promotion' (
        @($applicants | Where-Object {
            $_.Reviewed.applicantStatus -eq 'IdentityReviewApproved' -and
            $_.Safety.applicantStatus -eq 'SafetyScreenPassed' -and
            $_.Purpose.applicantStatus -eq 'VisitPurposeRecorded' -and
            $_.Reviewed.identityProofed -eq $false -and
            $_.Purpose.canonicalPatientCreated -eq $false
        }).Count -eq 4)

    $confirmedId = [string]$confirmedApplicant.Created.applicantId
    $confirmedVersion = [int]$confirmedApplicant.Purpose.applicantVersion
    $optionsResponse = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$confirmedId/practice-network-precheck/options" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$confirmedApplicant.Secret} -TimeoutSec 30
    $options = $optionsResponse.Content | ConvertFrom-Json
    Add-Check 'Private options expose exactly three server-owned NON_PRODUCTION fixtures and no individual eligibility claim' (
        $optionsResponse.Headers.'Cache-Control' -match 'no-store' -and
        $options.adapterMode -eq 'NON_PRODUCTION' -and
        $options.catalogKey -eq 'avenchart-synthetic-prospective-practice-network-2026-08' -and
        [int]$options.catalogVersion -eq 1 -and
        @($options.plans).Count -eq 3 -and
        (@($options.plans.planKey) -join ',') -eq 'harbor-mutual-hd,blue-valley-standard,pine-state-choice' -and
        $options.memberEligibilityChecked -eq $false -and
        $options.memberBenefitsChecked -eq $false -and
        $options.renderingPhysicianNetworkChecked -eq $false -and
        $options.coverageVerified -eq $false -and
        $options.exactNetworkConfirmed -eq $false)

    Add-Check 'Unknown applicant access key cannot list or record a practice-network precheck' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$confirmedId/practice-network-precheck/options" @{
            'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)
        }) -eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$confirmedId/practice-network-precheck" @{
            'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)
            'X-Idempotency-Key'=(New-Key 'sp17-denied')
        } (New-PrecheckBody $confirmedVersion 'harbor-mutual-hd')) -eq 404)

    Add-Check 'Arbitrary plan content and stale versions fail without precheck evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$confirmedId/practice-network-precheck" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$confirmedApplicant.Secret
            'X-Idempotency-Key'=(New-Key 'sp17-arbitrary')
        } (New-PrecheckBody $confirmedVersion 'arbitrary-plan')) -eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$confirmedId/practice-network-precheck" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$confirmedApplicant.Secret
            'X-Idempotency-Key'=(New-Key 'sp17-stale')
        } (New-PrecheckBody ($confirmedVersion - 1) 'harbor-mutual-hd')) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_practice_network_prechecks where applicant_id='$confirmedId';") -eq 0)

    $confirmedKey = New-Key 'sp17-confirmed'
    $confirmedBody = New-PrecheckBody $confirmedVersion 'harbor-mutual-hd'
    $confirmed = Invoke-Precheck $confirmedApplicant $confirmedBody $confirmedKey
    $confirmedReplay = Invoke-Precheck $confirmedApplicant $confirmedBody $confirmedKey
    Add-Check 'Practice-confirmed fixture is coarse and exact replay is stable' (
        $confirmed.practiceNetworkStatus -eq 'PracticeNetworkConfirmedFixture' -and
        $confirmed.payerDisplayName -eq 'Harbor Mutual' -and
        $confirmed.productDisplayName -eq 'High Deductible' -and
        $confirmed.applicantStatus -eq 'PracticeNetworkPrecheckRecorded' -and
        $confirmed.precheckId -eq $confirmedReplay.precheckId -and
        $confirmed.applicantVersion -eq $confirmedReplay.applicantVersion)
    Add-Check 'Changed content cannot reuse the precheck key and no second semantic command is allowed' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$confirmedId/practice-network-precheck" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$confirmedApplicant.Secret
            'X-Idempotency-Key'=$confirmedKey
        } (New-PrecheckBody $confirmedVersion 'blue-valley-standard')) -eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$confirmedId/practice-network-precheck" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$confirmedApplicant.Secret
            'X-Idempotency-Key'=(New-Key 'sp17-second')
        } (New-PrecheckBody ([int]$confirmed.applicantVersion) 'pine-state-choice')) -eq 409)

    $unknown = Invoke-Precheck $unknownApplicant `
        (New-PrecheckBody ([int]$unknownApplicant.Purpose.applicantVersion) 'blue-valley-standard') `
        (New-Key 'sp17-unknown')
    $out = Invoke-Precheck $outApplicant `
        (New-PrecheckBody ([int]$outApplicant.Purpose.applicantVersion) 'pine-state-choice') `
        (New-Key 'sp17-out')
    Add-Check 'Unknown and out-of-network fixtures remain distinct without financial consequences' (
        $unknown.practiceNetworkStatus -eq 'NetworkUnknown' -and
        $out.practiceNetworkStatus -eq 'PracticeOutOfNetworkFixture' -and
        $unknown.coverageVerified -eq $false -and $out.coverageVerified -eq $false -and
        $unknown.estimateCreated -eq $false -and $out.estimateCreated -eq $false -and
        $unknown.financialAcknowledgmentCreated -eq $false -and $out.financialAcknowledgmentCreated -eq $false)

    $concurrentId = [string]$concurrentApplicant.Created.applicantId
    $concurrentVersion = [int]$concurrentApplicant.Purpose.applicantVersion
    $concurrentSecret = [string]$concurrentApplicant.Secret
    $concurrentBaseUrl = $ApiBaseUrl
    $concurrentStatuses = 1..12 | ForEach-Object -Parallel {
        $headers = @{
            'X-AvenChart-Telehealth-Applicant-Key'=$using:concurrentSecret
            'X-Idempotency-Key'="sp17-concurrent-$($_)-$([Guid]::NewGuid().ToString('N'))"
        }
        try {
            [int](Invoke-WebRequest "$using:concurrentBaseUrl/api/telehealth/v1/applicants/$using:concurrentId/practice-network-precheck" `
                -Method Post -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
                    expectedVersion=$using:concurrentVersion
                    planKey='harbor-mutual-hd'
                    syntheticDataConfirmed=$true
                } | ConvertTo-Json)).StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
            else { throw }
        }
    } -ThrottleLimit 12
    Add-Check 'Concurrent first prechecks have one winner and one immutable precheck/event' (
        @($concurrentStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($concurrentStatuses | Where-Object { $_ -eq 409 }).Count -eq 11 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_practice_network_prechecks where applicant_id='$concurrentId';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$concurrentId' and action='prospective-practice-network-precheck-recorded';") -eq 1)

    $falseProperties = @(
        'memberEligibilityChecked','memberBenefitsChecked','renderingPhysicianNetworkChecked',
        'coverageVerified','exactNetworkConfirmed','identityProofed','canonicalPatientCreated',
        'chartLinked','portalAccountCreated','prospectiveIntakeCompleted','consentCreated',
        'practiceAccepted','coverageRecordCreated','estimateCreated','financialAcknowledgmentCreated',
        'requestCreated','queueEnabled','appointmentCreated','encounterCreated','careEnabled',
        'prescribingEnabled','billingEnabled','claimCreated','communicationEnabled',
        'integrationEnabled','externalCallPerformed')
    $allFalse = $true
    foreach ($response in @($confirmed,$unknown,$out)) {
        foreach ($property in $falseProperties) {
            if ($response.$property -ne $false) { $allFalse = $false }
        }
    }
    Add-Check 'Every result explicitly denies member, physician, coverage, identity, financial, care, integration, and external consequences' $allFalse
    Add-Check 'Database hard-false flags reject every consequential precheck state' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_practice_network_prechecks where
member_eligibility_checked or member_benefits_checked or rendering_physician_network_checked or
coverage_verified or exact_network_confirmed or identity_proofed or canonical_patient_created or
chart_linked or portal_account_created or prospective_intake_completed or consent_created or
practice_accepted or coverage_record_created or estimate_created or financial_acknowledgment_created or
request_created or queue_enabled or appointment_created or encounter_created or care_enabled or
prescribing_enabled or billing_enabled or claim_created or communication_enabled or
integration_enabled or external_call_performed;
"@) -eq 0)

    $publicResponse = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$confirmedId" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$confirmedApplicant.Secret} -TimeoutSec 30
    $public = $publicResponse.Content | ConvertFrom-Json
    $publicJson = $public | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant resume is private and coarse without plan, member, safety, staff, or fingerprint evidence' (
        $publicResponse.Headers.'Cache-Control' -match 'no-store' -and
        $public.status -eq 'PracticeNetworkPrecheckRecorded' -and
        $public.canonicalPatientCreated -eq $false -and
        $publicJson -notmatch 'planKey|payerDisplay|productDisplay|practiceNetworkStatus|memberId|subscriberId|policyNumber|groupNumber|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId|decidedBy|reviewReason')

    Add-Check 'Precheck evidence and correlated applicant event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_practice_network_prechecks set practice_network_status='NetworkUnknown' where precheck_id='$($confirmed.precheckId)';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$confirmedId' and action='prospective-practice-network-precheck-recorded';"))

    $after = Get-Counts
    $unchanged = @('patients','portalAccounts','insuranceRecords','requests','queueEntries',
        'intakeSnapshots','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    $allUnchanged = $true
    foreach ($name in $unchanged) {
        if ([int]$after.$name -ne [int]$before.$name) { $allUnchanged = $false }
    }
    Add-Check 'Practice-network discovery creates no insurance, patient, intake, coverage, financial, request, queue, clinical, prescribing, or downstream row' (
        $allUnchanged -and
        [int]$after.applicants -eq ([int]$before.applicants + 4) -and
        [int]$after.reviewDecisions -eq ([int]$before.reviewDecisions + 4) -and
        [int]$after.safetyEvaluations -eq ([int]$before.safetyEvaluations + 4) -and
        [int]$after.visitPurposes -eq ([int]$before.visitPurposes + 4) -and
        [int]$after.networkPrechecks -eq ([int]$before.networkPrechecks + 4)) @{
            applicantDelta=[int]$after.applicants-[int]$before.applicants
            reviewDecisionDelta=[int]$after.reviewDecisions-[int]$before.reviewDecisions
            safetyEvaluationDelta=[int]$after.safetyEvaluations-[int]$before.safetyEvaluations
            visitPurposeDelta=[int]$after.visitPurposes-[int]$before.visitPurposes
            networkPrecheckDelta=[int]$after.networkPrechecks-[int]$before.networkPrechecks
            canonicalAndDownstreamUnchanged=$allUnchanged
        }
}
catch {
    Add-Check 'Telehealth prospective practice-network precheck execution' $false @{
        message=$_.Exception.Message
        stack=$_.ScriptStackTrace
    }
}
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0007','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019','TH-DEC-0020')
        dataClassification='synthetic-only-no-secrets-no-member-identifiers-no-free-text-output'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}
if (-not $passed) { exit 1 }
