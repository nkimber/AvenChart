# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth applicant identity-review proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-identity-review.json'
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
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL applicant identity-review proof query failed.' }
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
  'reviewDecisions',(select count(*) from telehealth_applicant_identity_review_decisions));
"@) | ConvertFrom-Json
}

function New-VerifiedApplicant(
    [string]$FirstName,
    [string]$LastName,
    [string]$DateOfBirth,
    [string]$Email,
    [string]$Phone,
    [string]$State,
    [string]$PostalCode) {
    $secret = New-Secret
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp14-create')} `
        -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            legalFirstName=$FirstName;legalLastName=$LastName;dateOfBirth=$DateOfBirth
            email=$Email;phone=$Phone;residenceStateCode=$State;postalCode=$PostalCode
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp14-verify')
        } -ContentType 'application/json' -TimeoutSec 30 `
        -Body (@{expectedVersion=$created.version;verificationCode='246810'} | ConvertTo-Json)
    [pscustomobject]@{Secret=$secret;Created=$created;Verified=$verified}
}

function Login-Staff([string]$Username) {
    Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username=$Username;password='pass'} | ConvertTo-Json) -TimeoutSec 30
}

try {
    $before = Get-Counts
    $unique = [Guid]::NewGuid().ToString('N')
    $approvedApplicant = New-VerifiedApplicant 'Avery' 'ReviewApproved' '1990-03-14' `
        "sp14-approved-$unique@example.test" '404-555-0199' 'GA' '30301'
    $manualApplicant = New-VerifiedApplicant 'Arjun' 'Patel' '1994-01-13' `
        'mod-pat-0012@example.test' '619-555-1012' 'CA' '91950'
    $concurrentApplicant = New-VerifiedApplicant 'Morgan' 'ConcurrentReview' '1988-01-02' `
        "sp14-concurrent-$unique@example.test" '305-555-0188' 'FL' '33101'
    $staleApplicant = New-VerifiedApplicant 'Taylor' 'StaleReview' '1991-04-05' `
        "sp14-stale-$unique@example.test" '470-555-0187' 'GA' '30303'

    Add-Check 'Synthetic applicants reach only the prospective identity-review state' (
        $approvedApplicant.Verified.status -eq 'IdentityReviewPending' -and
        $approvedApplicant.Verified.duplicateDisposition -eq 'NoCandidate' -and
        $manualApplicant.Verified.status -eq 'IdentityReviewPending' -and
        $manualApplicant.Verified.duplicateDisposition -eq 'PossibleMatchManualReview')

    $admin = Login-Staff 'admin'
    $provider = Login-Staff 'gold-provider-01'
    $adminHeaders = @{
        'X-AvenChart-Session'=$admin.sessionId
        'X-AvenChart-Facility-Id'='10'
        'X-AvenChart-Purpose-Of-Use'='treatment'
    }
    $providerHeaders = @{
        'X-AvenChart-Session'=$provider.sessionId
        'X-AvenChart-Facility-Id'='10'
        'X-AvenChart-Purpose-Of-Use'='treatment'
    }
    Add-Check 'Review queue requires an administrator session, configured facility, and purpose' (
        (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-identity-review') -eq 401 -and
        (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-identity-review' $providerHeaders) -eq 403 -and
        (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-identity-review' @{
            'X-AvenChart-Session'=$admin.sessionId
            'X-AvenChart-Facility-Id'='11'
            'X-AvenChart-Purpose-Of-Use'='treatment'
        }) -in @(403,404) -and
        (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-identity-review' @{
            'X-AvenChart-Session'=$admin.sessionId
            'X-AvenChart-Facility-Id'='10'
        }) -eq 403)

    $queueResponse = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/admin/applicant-identity-review" `
        -Headers $adminHeaders -TimeoutSec 30
    $queue = $queueResponse.Content | ConvertFrom-Json
    $queueJson = $queue | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Review queue is private, bounded, deterministic, and candidate-safe' (
        $queueResponse.Headers.'Cache-Control' -match 'no-store' -and
        @($queue.applicants).Count -ge 4 -and
        @($queue.applicants | Where-Object {
            $_.applicantId -eq $approvedApplicant.Verified.applicantId -and
            $_.allowedDecision -eq 'ApprovedForProspectiveIntake'
        }).Count -eq 1 -and
        @($queue.applicants | Where-Object {
            $_.applicantId -eq $manualApplicant.Verified.applicantId -and
            $_.allowedDecision -eq 'ManualReviewRequired'
        }).Count -eq 1 -and
        $queueJson -notmatch 'candidateId|canonicalPatientId|matchScore|matchReasons|accessKey|evidenceFingerprint')

    $mismatchHeaders = $adminHeaders.Clone()
    $mismatchHeaders['X-Idempotency-Key'] = New-Key 'sp14-mismatch'
    Add-Check 'No-candidate evidence rejects a staff-selected manual-review override' (
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$($approvedApplicant.Verified.applicantId)/identity-review-decision" `
            $mismatchHeaders @{expectedVersion=$approvedApplicant.Verified.version;decision='ManualReviewRequired';reason='Synthetic mismatch must fail closed.';syntheticDataConfirmed=$true}) -eq 409)

    $approveKey = New-Key 'sp14-approve'
    $approveHeaders = $adminHeaders.Clone(); $approveHeaders['X-Idempotency-Key'] = $approveKey
    $approveBody = @{
        expectedVersion=$approvedApplicant.Verified.version
        decision='ApprovedForProspectiveIntake'
        reason='Synthetic contact-control and no-candidate disposition reviewed.'
        syntheticDataConfirmed=$true
    }
    $approved = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($approvedApplicant.Verified.applicantId)/identity-review-decision" `
        -Method Put -Headers $approveHeaders -ContentType 'application/json' -Body ($approveBody | ConvertTo-Json) -TimeoutSec 30
    $approvedReplay = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($approvedApplicant.Verified.applicantId)/identity-review-decision" `
        -Method Put -Headers $approveHeaders -ContentType 'application/json' -Body ($approveBody | ConvertTo-Json) -TimeoutSec 30
    Add-Check 'No-candidate decision is exact-replay idempotent and asserts no identity or promotion' (
        $approved.decisionId -eq $approvedReplay.decisionId -and
        $approved.applicantStatus -eq 'IdentityReviewApproved' -and
        $approved.applicantVersion -eq ($approvedApplicant.Verified.version + 1) -and
        $approved.identityProofed -eq $false -and
        $approved.canonicalPatientCreated -eq $false -and $approved.chartLinked -eq $false -and
        $approved.prospectiveIntakeCompleted -eq $false -and
        $approved.requestCreated -eq $false -and $approved.queueEnabled -eq $false)

    $changedApproveBody = $approveBody.Clone(); $changedApproveBody.reason = 'Changed synthetic reason must conflict with exact replay.'
    Add-Check 'Changed content cannot reuse a review idempotency key' (
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$($approvedApplicant.Verified.applicantId)/identity-review-decision" `
            $approveHeaders $changedApproveBody) -eq 409)

    $manualMismatchHeaders = $adminHeaders.Clone(); $manualMismatchHeaders['X-Idempotency-Key'] = New-Key 'sp14-manual-mismatch'
    Add-Check 'Possible-match evidence rejects prospective-intake approval' (
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$($manualApplicant.Verified.applicantId)/identity-review-decision" `
            $manualMismatchHeaders @{expectedVersion=$manualApplicant.Verified.version;decision='ApprovedForProspectiveIntake';reason='Synthetic possible match cannot be approved here.';syntheticDataConfirmed=$true}) -eq 409)
    $manualHeaders = $adminHeaders.Clone(); $manualHeaders['X-Idempotency-Key'] = New-Key 'sp14-manual'
    $manual = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($manualApplicant.Verified.applicantId)/identity-review-decision" `
        -Method Put -Headers $manualHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$manualApplicant.Verified.version
            decision='ManualReviewRequired'
            reason='Synthetic possible-match disposition requires separate matching review.'
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    Add-Check 'Possible-match decision terminates in prospective manual-review state only' (
        $manual.applicantStatus -eq 'ManualReviewRequired' -and
        $manual.identityProofed -eq $false -and $manual.canonicalPatientCreated -eq $false)

    $staleHeaders = $adminHeaders.Clone(); $staleHeaders['X-Idempotency-Key'] = New-Key 'sp14-stale'
    Add-Check 'Stale applicant version fails before decision evidence is written' (
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$($staleApplicant.Verified.applicantId)/identity-review-decision" `
            $staleHeaders @{expectedVersion=1;decision='ApprovedForProspectiveIntake';reason='Synthetic stale version must fail closed.';syntheticDataConfirmed=$true}) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_identity_review_decisions where applicant_id='$($staleApplicant.Verified.applicantId)';") -eq 0)

    $concurrentBaseUrl = $ApiBaseUrl
    $concurrentId = $concurrentApplicant.Verified.applicantId
    $concurrentVersion = [int]$concurrentApplicant.Verified.version
    $adminSessionId = [string]$admin.sessionId
    $concurrentStatuses = 1..12 | ForEach-Object -Parallel {
        $headers = @{
            'X-AvenChart-Session'=$using:adminSessionId
            'X-AvenChart-Facility-Id'='10'
            'X-AvenChart-Purpose-Of-Use'='treatment'
            'X-Idempotency-Key'="sp14-concurrent-$($_)-$([Guid]::NewGuid().ToString('N'))"
        }
        try {
            [int](Invoke-WebRequest "$using:concurrentBaseUrl/api/telehealth/v1/admin/applicants/$using:concurrentId/identity-review-decision" `
                -Method Put -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
                    expectedVersion=$using:concurrentVersion
                    decision='ApprovedForProspectiveIntake'
                    reason='Synthetic concurrent decision evidence reviewed.'
                    syntheticDataConfirmed=$true
                } | ConvertTo-Json)).StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
            else { throw }
        }
    } -ThrottleLimit 12
    Add-Check 'Concurrent first decisions have one winner and one immutable decision/event' (
        @($concurrentStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($concurrentStatuses | Where-Object { $_ -eq 409 }).Count -eq 11 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_identity_review_decisions where applicant_id='$concurrentId';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$concurrentId' and action='identity-review-recorded';") -eq 1)

    $publicApproved = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($approvedApplicant.Verified.applicantId)" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$approvedApplicant.Secret} -TimeoutSec 30
    $publicJson = $publicApproved | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant sees only coarse terminal state and no staff/reason/candidate evidence' (
        $publicApproved.status -eq 'IdentityReviewApproved' -and
        $publicApproved.canonicalPatientCreated -eq $false -and
        $publicJson -notmatch 'decidedBy|reviewReason|candidateId|canonicalPatientId|evidenceFingerprint')

    Add-Check 'Review decision and review event evidence are append-only' (
        (Test-MutationRejected "update telehealth_applicant_identity_review_decisions set reason='changed evidence' where decision_id='$($approved.decisionId)';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$($approvedApplicant.Verified.applicantId)' and action='identity-review-recorded';"))

    $auditCount = [int](Invoke-Scalar @"
select count(*) from phi_access_audit_events
where resource_type='TelehealthApplicantIdentityReview'
  and resource_id in ('queue','$($approvedApplicant.Verified.applicantId)')
  and facility_id=10 and purpose_of_use='treatment';
"@)
    Add-Check 'Queue reads and decisions are resource-correlated in PHI audit' ($auditCount -ge 2) @{auditCount=$auditCount}

    $after = Get-Counts
    $unchanged = @('patients','portalAccounts','insuranceRecords','requests','queueEntries',
        'intakeSnapshots','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    $allUnchanged = $true
    foreach ($name in $unchanged) {
        if ([int]$after.$name -ne [int]$before.$name) { $allUnchanged = $false }
    }
    Add-Check 'Identity review creates no canonical patient, intake, coverage, care, queue, financial, or prescribing row' (
        $allUnchanged -and
        [int]$after.applicants -eq ([int]$before.applicants + 4) -and
        [int]$after.reviewDecisions -eq ([int]$before.reviewDecisions + 3)) @{
            applicantDelta=[int]$after.applicants-[int]$before.applicants
            reviewDecisionDelta=[int]$after.reviewDecisions-[int]$before.reviewDecisions
            canonicalAndDownstreamUnchanged=$allUnchanged
        }
}
catch {
    Add-Check 'Telehealth applicant identity-review execution' $false @{
        message=$_.Exception.Message
        stack=$_.ScriptStackTrace
    }
}
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0007','TH-DEC-0017')
        dataClassification='synthetic-only-no-secrets-no-candidate-records'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}
if (-not $passed) { exit 1 }
