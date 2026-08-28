# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth prospective safety-triage proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-prospective-safety-triage.json'
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
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL prospective safety-triage proof query failed.' }
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
  'safetyEvaluations',(select count(*) from telehealth_applicant_safety_triage_evaluations));
"@) | ConvertFrom-Json
}

function Login-Staff([string]$Username) {
    Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username=$Username;password='pass'} | ConvertTo-Json) -TimeoutSec 30
}

function New-ApprovedApplicant([string]$Label,[hashtable]$AdminHeaders,[string]$Unique) {
    $secret = New-Secret
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp15-create')} `
        -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            legalFirstName='Safety';legalLastName=$Label;dateOfBirth='1990-03-14'
            email="sp15-$($Label.ToLowerInvariant())-$Unique@example.test";phone='404-555-0199'
            residenceStateCode='GA';postalCode='30301';syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp15-verify')
        } -ContentType 'application/json' -TimeoutSec 30 `
        -Body (@{expectedVersion=$created.version;verificationCode='246810'} | ConvertTo-Json)
    $reviewHeaders = $AdminHeaders.Clone()
    $reviewHeaders['X-Idempotency-Key'] = New-Key 'sp15-review'
    $reviewed = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" `
        -Method Put -Headers $reviewHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$verified.version
            decision='ApprovedForProspectiveIntake'
            reason="Synthetic $Label safety-triage fixture reviewed with no candidate."
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    [pscustomobject]@{Secret=$secret;Created=$created;Verified=$verified;Reviewed=$reviewed}
}

function New-TriageBody(
    [int]$ExpectedVersion,
    [bool]$Emergency,
    [bool]$Severe,
    [bool]$HandsOn,
    [bool]$Unsure) {
    @{
        expectedVersion=$ExpectedVersion
        currentLocationStateCode='GA'
        currentLocationConfirmed=$true
        hasEmergencyWarning=$Emergency
        severeOrWorsening=$Severe
        requiresHandsOnExam=$HandsOn
        unsure=$Unsure
        syntheticDataConfirmed=$true
    }
}

function Invoke-Triage([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/safety-triage" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body | ConvertTo-Json)
}

try {
    $before = Get-Counts
    $unique = [Guid]::NewGuid().ToString('N')
    $admin = Login-Staff 'admin'
    $adminHeaders = @{
        'X-AvenChart-Session'=$admin.sessionId
        'X-AvenChart-Facility-Id'='10'
        'X-AvenChart-Purpose-Of-Use'='treatment'
    }
    $emergencyApplicant = New-ApprovedApplicant 'Emergency' $adminHeaders $unique
    $urgentApplicant = New-ApprovedApplicant 'Urgent' $adminHeaders $unique
    $inPersonApplicant = New-ApprovedApplicant 'InPerson' $adminHeaders $unique
    $clinicalApplicant = New-ApprovedApplicant 'ClinicalReview' $adminHeaders $unique
    $eligibleApplicant = New-ApprovedApplicant 'Eligible' $adminHeaders $unique

    $approvedApplicants = @($emergencyApplicant,$urgentApplicant,$inPersonApplicant,$clinicalApplicant,$eligibleApplicant)
    Add-Check 'Only staff-approved no-candidate synthetic applicants reach the safety boundary' (
        @($approvedApplicants | Where-Object {
            $_.Reviewed.applicantStatus -eq 'IdentityReviewApproved' -and
            $_.Reviewed.identityProofed -eq $false -and
            $_.Reviewed.canonicalPatientCreated -eq $false
        }).Count -eq 5)

    $emergencyVersion = [int]$emergencyApplicant.Reviewed.applicantVersion
    $missingBody = New-TriageBody $emergencyVersion $false $false $false $false
    $missingBody.Remove('requiresHandsOnExam')
    $missingKey = New-Key 'sp15-missing'
    Add-Check 'Missing answers fail closed without evaluation evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($emergencyApplicant.Created.applicantId)/safety-triage" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$emergencyApplicant.Secret
            'X-Idempotency-Key'=$missingKey
        } $missingBody) -eq 400 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_safety_triage_evaluations where applicant_id='$($emergencyApplicant.Created.applicantId)';") -eq 0)

    $validEmergencyBody = New-TriageBody $emergencyVersion $true $true $true $true
    Add-Check 'Unknown applicant access key cannot evaluate an approved applicant' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($emergencyApplicant.Created.applicantId)/safety-triage" @{
            'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)
            'X-Idempotency-Key'=(New-Key 'sp15-denied')
        } $validEmergencyBody) -eq 404)
    $staleBody = New-TriageBody ($emergencyVersion - 1) $true $true $true $true
    Add-Check 'Stale aggregate version fails before safety evidence is written' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($emergencyApplicant.Created.applicantId)/safety-triage" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$emergencyApplicant.Secret
            'X-Idempotency-Key'=(New-Key 'sp15-stale')
        } $staleBody) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_safety_triage_evaluations where applicant_id='$($emergencyApplicant.Created.applicantId)';") -eq 0)

    $emergencyKey = New-Key 'sp15-emergency'
    $emergencyResponse = Invoke-Triage $emergencyApplicant $validEmergencyBody $emergencyKey
    $emergencyReplay = Invoke-Triage $emergencyApplicant $validEmergencyBody $emergencyKey
    Add-Check 'Emergency dominates every lower-priority answer and exact replay is stable' (
        $emergencyResponse.safetyDisposition -eq 'EmergencyCareNow' -and
        $emergencyResponse.applicantStatus -eq 'SafetyEmergencyRedirect' -and
        $emergencyResponse.evaluationId -eq $emergencyReplay.evaluationId -and
        $emergencyResponse.applicantVersion -eq $emergencyReplay.applicantVersion -and
        $emergencyResponse.direction -match '911')
    $changedEmergencyBody = $validEmergencyBody.Clone(); $changedEmergencyBody.unsure = $false
    Add-Check 'Changed content cannot reuse a safety-triage idempotency key' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($emergencyApplicant.Created.applicantId)/safety-triage" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$emergencyApplicant.Secret
            'X-Idempotency-Key'=$emergencyKey
        } $changedEmergencyBody) -eq 409)

    $urgentBody = New-TriageBody ([int]$urgentApplicant.Reviewed.applicantVersion) $false $true $true $true
    $urgentResponse = Invoke-Triage $urgentApplicant $urgentBody (New-Key 'sp15-urgent')
    $inPersonBody = New-TriageBody ([int]$inPersonApplicant.Reviewed.applicantVersion) $false $false $true $true
    $inPersonResponse = Invoke-Triage $inPersonApplicant $inPersonBody (New-Key 'sp15-inperson')
    $clinicalBody = New-TriageBody ([int]$clinicalApplicant.Reviewed.applicantVersion) $false $false $false $true
    $clinicalResponse = Invoke-Triage $clinicalApplicant $clinicalBody (New-Key 'sp15-clinical')
    Add-Check 'Priority deterministically separates urgent, hands-on, and uncertain outcomes' (
        $urgentResponse.safetyDisposition -eq 'PromptInPersonCare' -and
        $urgentResponse.applicantStatus -eq 'SafetyInPersonRequired' -and
        $inPersonResponse.safetyDisposition -eq 'InPersonCareRequired' -and
        $inPersonResponse.applicantStatus -eq 'SafetyInPersonRequired' -and
        $clinicalResponse.safetyDisposition -eq 'ClinicalReviewRequired' -and
        $clinicalResponse.applicantStatus -eq 'SafetyClinicalReviewRequired')

    $eligibleId = [string]$eligibleApplicant.Created.applicantId
    $eligibleVersion = [int]$eligibleApplicant.Reviewed.applicantVersion
    $eligibleSecret = [string]$eligibleApplicant.Secret
    $concurrentBaseUrl = $ApiBaseUrl
    $concurrentStatuses = 1..12 | ForEach-Object -Parallel {
        $headers = @{
            'X-AvenChart-Telehealth-Applicant-Key'=$using:eligibleSecret
            'X-Idempotency-Key'="sp15-concurrent-$($_)-$([Guid]::NewGuid().ToString('N'))"
        }
        try {
            [int](Invoke-WebRequest "$using:concurrentBaseUrl/api/telehealth/v1/applicants/$using:eligibleId/safety-triage" `
                -Method Post -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
                    expectedVersion=$using:eligibleVersion
                    currentLocationStateCode='GA'
                    currentLocationConfirmed=$true
                    hasEmergencyWarning=$false
                    severeOrWorsening=$false
                    requiresHandsOnExam=$false
                    unsure=$false
                    syntheticDataConfirmed=$true
                } | ConvertTo-Json)).StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
            else { throw }
        }
    } -ThrottleLimit 12
    Add-Check 'Concurrent first safety decisions have one winner and one immutable evaluation/event' (
        @($concurrentStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($concurrentStatuses | Where-Object { $_ -eq 409 }).Count -eq 11 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_safety_triage_evaluations where applicant_id='$eligibleId' and outcome='TelehealthEligible';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$eligibleId' and action='prospective-safety-triage-evaluated';") -eq 1)

    $eligiblePublicResponse = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$eligibleId" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$eligibleSecret} -TimeoutSec 30
    $eligiblePublic = $eligiblePublicResponse.Content | ConvertFrom-Json
    $eligiblePublicJson = $eligiblePublic | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant sees a private coarse result without raw clinical or staff evidence' (
        $eligiblePublicResponse.Headers.'Cache-Control' -match 'no-store' -and
        $eligiblePublic.status -eq 'SafetyScreenPassed' -and
        $eligiblePublic.canonicalPatientCreated -eq $false -and
        $eligiblePublicJson -notmatch 'hasEmergencyWarning|severeOrWorsening|requiresHandsOnExam|unsure|protocolContentHash|answersFingerprint|decidedBy|reviewReason')

    $allResponses = @($emergencyResponse,$urgentResponse,$inPersonResponse,$clinicalResponse)
    Add-Check 'Every result explicitly denies identity, clinical review, care, queue, and downstream consequences' (
        @($allResponses | Where-Object {
            $_.identityProofed -eq $false -and $_.clinicalReviewPerformed -eq $false -and
            $_.canonicalPatientCreated -eq $false -and $_.chartLinked -eq $false -and
            $_.prospectiveIntakeCompleted -eq $false -and $_.coverageChecked -eq $false -and
            $_.requestCreated -eq $false -and $_.queueEnabled -eq $false -and
            $_.careEnabled -eq $false
        }).Count -eq 4)

    Add-Check 'Safety evaluation and correlated aggregate event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_safety_triage_evaluations set outcome='TelehealthEligible' where evaluation_id='$($emergencyResponse.evaluationId)';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$($emergencyApplicant.Created.applicantId)' and action='prospective-safety-triage-evaluated';"))

    $after = Get-Counts
    $unchanged = @('patients','portalAccounts','insuranceRecords','requests','queueEntries',
        'intakeSnapshots','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    $allUnchanged = $true
    foreach ($name in $unchanged) {
        if ([int]$after.$name -ne [int]$before.$name) { $allUnchanged = $false }
    }
    Add-Check 'Safety triage creates no canonical patient, intake, coverage, care, queue, financial, or prescribing row' (
        $allUnchanged -and
        [int]$after.applicants -eq ([int]$before.applicants + 5) -and
        [int]$after.reviewDecisions -eq ([int]$before.reviewDecisions + 5) -and
        [int]$after.safetyEvaluations -eq ([int]$before.safetyEvaluations + 5)) @{
            applicantDelta=[int]$after.applicants-[int]$before.applicants
            reviewDecisionDelta=[int]$after.reviewDecisions-[int]$before.reviewDecisions
            safetyEvaluationDelta=[int]$after.safetyEvaluations-[int]$before.safetyEvaluations
            canonicalAndDownstreamUnchanged=$allUnchanged
        }
}
catch {
    Add-Check 'Telehealth prospective safety-triage execution' $false @{
        message=$_.Exception.Message
        stack=$_.ScriptStackTrace
    }
}
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0007','TH-DEC-0017','TH-DEC-0018')
        dataClassification='synthetic-only-no-secrets-no-raw-answer-output'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}
if (-not $passed) { exit 1 }
