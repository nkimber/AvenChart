# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth prospective visit-purpose proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-prospective-visit-purpose.json'
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
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL prospective visit-purpose proof query failed.' }
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
  'visitPurposes',(select count(*) from telehealth_applicant_visit_purposes));
"@) | ConvertFrom-Json
}

function Login-Staff {
    Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='admin';password='pass'} | ConvertTo-Json) -TimeoutSec 30
}

function New-SafetyPassedApplicant([string]$Label,[hashtable]$AdminHeaders,[string]$Unique) {
    $secret = New-Secret
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp16-create')} `
        -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            legalFirstName='Purpose';legalLastName=$Label;dateOfBirth='1990-03-14'
            email="sp16-$($Label.ToLowerInvariant())-$Unique@example.test";phone='404-555-0199'
            residenceStateCode='GA';postalCode='30301';syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp16-verify')
        } -ContentType 'application/json' -TimeoutSec 30 `
        -Body (@{expectedVersion=$created.version;verificationCode='246810'} | ConvertTo-Json)
    $reviewHeaders = $AdminHeaders.Clone()
    $reviewHeaders['X-Idempotency-Key'] = New-Key 'sp16-review'
    $reviewed = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" `
        -Method Put -Headers $reviewHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$verified.version
            decision='ApprovedForProspectiveIntake'
            reason="Synthetic $Label visit-purpose fixture reviewed with no candidate."
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $safety = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/safety-triage" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp16-safety')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$reviewed.applicantVersion
            currentLocationStateCode='GA';currentLocationConfirmed=$true
            hasEmergencyWarning=$false;severeOrWorsening=$false
            requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    [pscustomobject]@{Secret=$secret;Created=$created;Reviewed=$reviewed;Safety=$safety}
}

function New-PurposeBody([int]$ExpectedVersion,[string]$Category) {
    @{expectedVersion=$ExpectedVersion;purposeCategory=$Category;syntheticDataConfirmed=$true}
}

function Invoke-Purpose([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/visit-purpose" `
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
    $migraineApplicant = New-SafetyPassedApplicant 'Migraine' $adminHeaders $unique
    $sleepApplicant = New-SafetyPassedApplicant 'Sleep' $adminHeaders $unique
    $concurrentApplicant = New-SafetyPassedApplicant 'Concurrent' $adminHeaders $unique
    $applicants = @($migraineApplicant,$sleepApplicant,$concurrentApplicant)
    Add-Check 'Only no-candidate staff-approved applicants with a passing universal safety screen reach purpose selection' (
        @($applicants | Where-Object {
            $_.Reviewed.applicantStatus -eq 'IdentityReviewApproved' -and
            $_.Safety.applicantStatus -eq 'SafetyScreenPassed' -and
            $_.Safety.safetyDisposition -eq 'ContinueProspectiveIntake' -and
            $_.Safety.identityProofed -eq $false -and
            $_.Safety.canonicalPatientCreated -eq $false
        }).Count -eq 3)

    $migraineId = [string]$migraineApplicant.Created.applicantId
    $migraineVersion = [int]$migraineApplicant.Safety.applicantVersion
    $arbitraryBody = New-PurposeBody $migraineVersion 'migraine with narrative'
    Add-Check 'Arbitrary or free-text-like purpose content fails closed without evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$migraineId/visit-purpose" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$migraineApplicant.Secret
            'X-Idempotency-Key'=(New-Key 'sp16-arbitrary')
        } $arbitraryBody) -eq 400 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_visit_purposes where applicant_id='$migraineId';") -eq 0)

    $validMigraineBody = New-PurposeBody $migraineVersion 'migraine'
    Add-Check 'Unknown applicant access key cannot record a visit purpose' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$migraineId/visit-purpose" @{
            'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)
            'X-Idempotency-Key'=(New-Key 'sp16-denied')
        } $validMigraineBody) -eq 404)
    $staleBody = New-PurposeBody ($migraineVersion - 1) 'migraine'
    Add-Check 'Stale aggregate version fails before purpose evidence is written' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$migraineId/visit-purpose" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$migraineApplicant.Secret
            'X-Idempotency-Key'=(New-Key 'sp16-stale')
        } $staleBody) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_visit_purposes where applicant_id='$migraineId';") -eq 0)

    $migraineKey = New-Key 'sp16-migraine'
    $migraineResponse = Invoke-Purpose $migraineApplicant $validMigraineBody $migraineKey
    $migraineReplay = Invoke-Purpose $migraineApplicant $validMigraineBody $migraineKey
    Add-Check 'Migraine category has a fixed label and exact replay is stable' (
        $migraineResponse.purposeCategory -eq 'migraine' -and
        $migraineResponse.purposeDisplayLabel -eq 'Headache or known migraine pattern' -and
        $migraineResponse.applicantStatus -eq 'VisitPurposeRecorded' -and
        $migraineResponse.purposeId -eq $migraineReplay.purposeId -and
        $migraineResponse.applicantVersion -eq $migraineReplay.applicantVersion)
    $changedMigraineBody = New-PurposeBody $migraineVersion 'sleep'
    Add-Check 'Changed content cannot reuse a purpose idempotency key and no second command is allowed' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$migraineId/visit-purpose" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$migraineApplicant.Secret
            'X-Idempotency-Key'=$migraineKey
        } $changedMigraineBody) -eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$migraineId/visit-purpose" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$migraineApplicant.Secret
            'X-Idempotency-Key'=(New-Key 'sp16-second')
        } (New-PurposeBody ([int]$migraineResponse.applicantVersion) 'sleep')) -eq 409)

    $sleepBody = New-PurposeBody ([int]$sleepApplicant.Safety.applicantVersion) 'sleep'
    $sleepResponse = Invoke-Purpose $sleepApplicant $sleepBody (New-Key 'sp16-sleep')
    Add-Check 'Sleep category has the only other fixed label' (
        $sleepResponse.purposeCategory -eq 'sleep' -and
        $sleepResponse.purposeDisplayLabel -eq 'Sleep difficulty' -and
        $sleepResponse.applicantStatus -eq 'VisitPurposeRecorded')

    $concurrentId = [string]$concurrentApplicant.Created.applicantId
    $concurrentVersion = [int]$concurrentApplicant.Safety.applicantVersion
    $concurrentSecret = [string]$concurrentApplicant.Secret
    $concurrentBaseUrl = $ApiBaseUrl
    $concurrentStatuses = 1..12 | ForEach-Object -Parallel {
        $headers = @{
            'X-AvenChart-Telehealth-Applicant-Key'=$using:concurrentSecret
            'X-Idempotency-Key'="sp16-concurrent-$($_)-$([Guid]::NewGuid().ToString('N'))"
        }
        try {
            [int](Invoke-WebRequest "$using:concurrentBaseUrl/api/telehealth/v1/applicants/$using:concurrentId/visit-purpose" `
                -Method Post -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
                    expectedVersion=$using:concurrentVersion
                    purposeCategory='migraine'
                    syntheticDataConfirmed=$true
                } | ConvertTo-Json)).StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
            else { throw }
        }
    } -ThrottleLimit 12
    Add-Check 'Concurrent first purpose decisions have one winner and one immutable purpose/event' (
        @($concurrentStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($concurrentStatuses | Where-Object { $_ -eq 409 }).Count -eq 11 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_visit_purposes where applicant_id='$concurrentId';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$concurrentId' and action='prospective-visit-purpose-recorded';") -eq 1)

    $publicResponse = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$migraineId" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$migraineApplicant.Secret} -TimeoutSec 30
    $public = $publicResponse.Content | ConvertFrom-Json
    $publicJson = $public | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant resume is private and coarse without clinical, staff, or fingerprint evidence' (
        $publicResponse.Headers.'Cache-Control' -match 'no-store' -and
        $public.status -eq 'VisitPurposeRecorded' -and
        $public.canonicalPatientCreated -eq $false -and
        $publicJson -notmatch 'commandFingerprint|clinicalEligibilityDetermined|identityReviewDecisionId|safetyTriageEvaluationId|decidedBy|reviewReason')

    $allResponses = @($migraineResponse,$sleepResponse)
    Add-Check 'Every purpose result explicitly denies protocols, eligibility, identity, care, queue, and downstream consequences' (
        @($allResponses | Where-Object {
            $_.clinicalProtocolPublished -eq $false -and $_.clinicalEligibilityDetermined -eq $false -and
            $_.identityProofed -eq $false -and $_.canonicalPatientCreated -eq $false -and
            $_.chartLinked -eq $false -and $_.prospectiveIntakeCompleted -eq $false -and
            $_.coverageChecked -eq $false -and $_.requestCreated -eq $false -and
            $_.queueEnabled -eq $false -and $_.careEnabled -eq $false
        }).Count -eq 2)

    Add-Check 'Purpose evidence and correlated applicant event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_visit_purposes set purpose_category='sleep' where purpose_id='$($migraineResponse.purposeId)';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$migraineId' and action='prospective-visit-purpose-recorded';"))

    $after = Get-Counts
    $unchanged = @('patients','portalAccounts','insuranceRecords','requests','queueEntries',
        'intakeSnapshots','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    $allUnchanged = $true
    foreach ($name in $unchanged) {
        if ([int]$after.$name -ne [int]$before.$name) { $allUnchanged = $false }
    }
    Add-Check 'Purpose classification creates no canonical patient, intake, coverage, care, queue, financial, or prescribing row' (
        $allUnchanged -and
        [int]$after.applicants -eq ([int]$before.applicants + 3) -and
        [int]$after.reviewDecisions -eq ([int]$before.reviewDecisions + 3) -and
        [int]$after.safetyEvaluations -eq ([int]$before.safetyEvaluations + 3) -and
        [int]$after.visitPurposes -eq ([int]$before.visitPurposes + 3)) @{
            applicantDelta=[int]$after.applicants-[int]$before.applicants
            reviewDecisionDelta=[int]$after.reviewDecisions-[int]$before.reviewDecisions
            safetyEvaluationDelta=[int]$after.safetyEvaluations-[int]$before.safetyEvaluations
            visitPurposeDelta=[int]$after.visitPurposes-[int]$before.visitPurposes
            canonicalAndDownstreamUnchanged=$allUnchanged
        }
}
catch {
    Add-Check 'Telehealth prospective visit-purpose execution' $false @{
        message=$_.Exception.Message
        stack=$_.ScriptStackTrace
    }
}
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0007','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019')
        dataClassification='synthetic-only-no-secrets-no-free-text-output'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}
if (-not $passed) { exit 1 }
