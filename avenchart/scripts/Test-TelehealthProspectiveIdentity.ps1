# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth prospective-identity proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-prospective-identity.json'
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
    return [Convert]::ToHexString($bytes).ToLowerInvariant()
}

function New-CommandKey([string]$Prefix) { "$Prefix-$([Guid]::NewGuid().ToString('N'))" }

function Invoke-Status([string]$Method,[string]$Path,[hashtable]$Headers=@{},[object]$Body=$null) {
    $parameters = @{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=$Headers;TimeoutSec=20}
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 8
    }
    try { return [int](Invoke-WebRequest @parameters).StatusCode }
    catch {
        if ($null -ne $_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}

function Invoke-Scalar([string]$Sql) {
    Push-Location $solutionRoot
    try {
        $value = docker compose exec -T postgres psql -X -U avenchart -d avenchart -t -A -v ON_ERROR_STOP=1 -c $Sql
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL prospective-identity proof query failed.' }
        return ($value | Select-Object -Last 1).Trim()
    }
    finally { Pop-Location }
}

function Get-CanonicalCounts {
    (Invoke-Scalar @"
select json_build_object(
  'patients',(select count(*) from patients),
  'portalAccounts',(select count(*) from patient_portal_accounts),
  'insuranceRecords',(select count(*) from insurance_records),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'applicants',(select count(*) from telehealth_prospective_applicants));
"@) | ConvertFrom-Json
}

function Test-MutationRejected([string]$Sql) {
    Push-Location $solutionRoot
    try {
        docker compose exec -T postgres psql -X -U avenchart -d avenchart -v ON_ERROR_STOP=1 -c $Sql *> $null
        return $LASTEXITCODE -ne 0
    }
    finally { Pop-Location }
}

try {
    $before = Get-CanonicalCounts
    $unique = [Guid]::NewGuid().ToString('N')
    $secret = New-Secret
    $createKey = New-CommandKey 'sp4-create'
    $createHeaders = @{
        'X-AvenChart-Telehealth-Applicant-Key' = $secret
        'X-Idempotency-Key' = $createKey
    }
    $noCandidateBody = [ordered]@{
        legalFirstName = 'Avery'
        legalLastName = 'SyntheticOnly'
        dateOfBirth = '1990-03-14'
        email = "sp4-$unique@example.test"
        phone = '404-555-0199'
        residenceStateCode = 'GA'
        postalCode = '30301'
        syntheticDataConfirmed = $true
    }
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers $createHeaders -ContentType 'application/json' -Body ($noCandidateBody | ConvertTo-Json) -TimeoutSec 20
    $replay = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers $createHeaders -ContentType 'application/json' -Body ($noCandidateBody | ConvertTo-Json) -TimeoutSec 20
    Add-Check 'Applicant create is exact-replay idempotent and remains contact-verification pending' (
        $created.status -eq 'ContactVerificationPending' -and
        $created.applicantId -eq $replay.applicantId -and
        $created.canonicalPatientCreated -eq $false -and
        [int]$created.verificationAttemptsRemaining -eq 5)

    $conflictingBody = [ordered]@{} + $noCandidateBody
    $conflictingBody.legalLastName = 'ChangedCommand'
    Add-Check 'Conflicting create idempotency reuse is rejected' (
        (Invoke-Status 'POST' '/api/telehealth/v1/applicants' $createHeaders $conflictingBody) -eq 409)
    Add-Check 'Applicant reads require a separate applicant access key' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$($created.applicantId)") -eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$($created.applicantId)" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)}) -eq 404)
    $wrongHostHeaders = $createHeaders.Clone()
    $wrongHostHeaders.Host = 'unconfigured.example.test'
    Add-Check 'Applicant create rejects an unconfigured practice host' (
        (Invoke-Status 'POST' '/api/telehealth/v1/applicants' $wrongHostHeaders $noCandidateBody) -eq 404)

    $wrongAttemptKey = New-CommandKey 'sp4-wrong'
    $wrongHeaders = @{
        'X-AvenChart-Telehealth-Applicant-Key' = $secret
        'X-Idempotency-Key' = $wrongAttemptKey
    }
    $wrongBody = @{expectedVersion=1;verificationCode='000000'}
    $wrongFirst = Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" $wrongHeaders $wrongBody
    $wrongReplay = Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" $wrongHeaders $wrongBody
    $attemptCount = [int](Invoke-Scalar "select count(*) from telehealth_applicant_verification_attempts where applicant_id='$($created.applicantId)';")
    Add-Check 'Rejected verification replay is stable and consumes one attempt only' (
        $wrongFirst -eq 400 -and $wrongReplay -eq 400 -and $attemptCount -eq 1)

    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-CommandKey 'sp4-verify')} `
        -ContentType 'application/json' `
        -Body (@{expectedVersion=1;verificationCode='246810'} | ConvertTo-Json) -TimeoutSec 20
    Add-Check 'Successful contact control stops at identity review with no-candidate disposition' (
        $verified.status -eq 'IdentityReviewPending' -and
        $verified.identityAssurance -eq 'ContactControlOnly' -and
        $verified.duplicateDisposition -eq 'NoCandidate' -and
        $verified.canonicalPatientCreated -eq $false -and
        $null -eq $verified.demonstrationVerificationCode)

    $matchSecret = New-Secret
    $matchCreated = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$matchSecret;'X-Idempotency-Key'=(New-CommandKey 'sp4-match-create')} `
        -ContentType 'application/json' -TimeoutSec 20 -Body (@{
            legalFirstName='Arjun';legalLastName='Patel';dateOfBirth='1994-01-13'
            email='mod-pat-0012@example.test';phone='619-555-1012';residenceStateCode='CA'
            postalCode='91950';syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $matchVerified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($matchCreated.applicantId)/contact-verification" `
        -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$matchSecret;'X-Idempotency-Key'=(New-CommandKey 'sp4-match-verify')} `
        -ContentType 'application/json' -TimeoutSec 20 `
        -Body (@{expectedVersion=$matchCreated.version;verificationCode='246810'} | ConvertTo-Json)
    $publicMatchJson = $matchVerified | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Possible duplicate returns only a coarse manual-review disposition' (
        $matchVerified.status -eq 'IdentityReviewPending' -and
        $matchVerified.duplicateDisposition -eq 'PossibleMatchManualReview' -and
        $publicMatchJson -notmatch 'MOD-PAT|Arjun|Patel|candidateId|canonicalId|matchScore|matchReasons|totalCandidates')

    $lockSecret = New-Secret
    $lockCreated = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$lockSecret;'X-Idempotency-Key'=(New-CommandKey 'sp4-lock-create')} `
        -ContentType 'application/json' -TimeoutSec 20 -Body (@{
            legalFirstName='Morgan';legalLastName='AttemptLimit';dateOfBirth='1988-01-02'
            email="sp4-lock-$unique@example.test";phone='305-555-0188';residenceStateCode='FL'
            postalCode='33101';syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $lockBaseUrl = $ApiBaseUrl
    $lockApplicantId = $lockCreated.applicantId
    $lockStatuses = 1..10 | ForEach-Object -Parallel {
        $targetBaseUrl = $using:lockBaseUrl
        $targetApplicantId = $using:lockApplicantId
        $targetSecret = $using:lockSecret
        $targetKey = "sp4-lock-$($_)-$([Guid]::NewGuid().ToString('N'))"
        try {
            [int](Invoke-WebRequest `
                -Uri "$targetBaseUrl/api/telehealth/v1/applicants/$targetApplicantId/contact-verification" `
                -Method Post `
                -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$targetSecret;'X-Idempotency-Key'=$targetKey} `
                -ContentType 'application/json' `
                -Body (@{expectedVersion=1;verificationCode='000000'} | ConvertTo-Json) `
                -TimeoutSec 30).StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
            else { throw }
        }
    } -ThrottleLimit 10
    $lastLockKey = New-CommandKey 'sp4-lock-replay'
    $lockReplay = Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($lockCreated.applicantId)/contact-verification" `
        @{'X-AvenChart-Telehealth-Applicant-Key'=$lockSecret;'X-Idempotency-Key'=$lastLockKey} `
        @{expectedVersion=1;verificationCode='000000'}
    $lockAttemptCount = [int](Invoke-Scalar "select count(*) from telehealth_applicant_verification_attempts where applicant_id='$($lockCreated.applicantId)';")
    $lockStatus = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($lockCreated.applicantId)" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$lockSecret} -TimeoutSec 20
    Add-Check 'Ten concurrent rejected commands record five attempts, lock once, and never produce a server error' (
        @($lockStatuses | Where-Object { $_ -eq 400 }).Count -eq 4 -and
        @($lockStatuses | Where-Object { $_ -eq 410 }).Count -eq 6 -and
        @($lockStatuses | Where-Object { $_ -notin @(400,410) }).Count -eq 0 -and
        $lockReplay -eq 410 -and
        $lockAttemptCount -eq 5 -and $lockStatus.status -eq 'VerificationLocked')

    Add-Check 'Access keys and challenge verifiers are stored only as constrained hashes' ([int](Invoke-Scalar @"
select count(*)
from telehealth_prospective_applicants applicant
join telehealth_applicant_contact_challenges challenge using(applicant_id)
where applicant_id in ('$($created.applicantId)','$($matchCreated.applicantId)','$($lockCreated.applicantId)')
  and applicant.access_key_hash ~ '^[0-9a-f]{64}$'
  and challenge.verifier_hash ~ '^[0-9a-f]{64}$'
  and challenge.verifier_hash <> '246810';
"@) -eq 3)
    Add-Check 'Applicant evidence and aggregates reject destructive mutation' (
        (Test-MutationRejected "update telehealth_applicant_verification_attempts set result='Accepted' where applicant_id='$($created.applicantId)';") -and
        (Test-MutationRejected "delete from telehealth_prospective_applicants where applicant_id='$($created.applicantId)';"))

    $after = Get-CanonicalCounts
    Add-Check 'Prospective identity paths create no canonical patient, portal, insurance, request, or queue row' (
        [int]$after.patients -eq [int]$before.patients -and
        [int]$after.portalAccounts -eq [int]$before.portalAccounts -and
        [int]$after.insuranceRecords -eq [int]$before.insuranceRecords -and
        [int]$after.requests -eq [int]$before.requests -and
        [int]$after.queueEntries -eq [int]$before.queueEntries -and
        [int]$after.applicants -eq ([int]$before.applicants + 3)) @{
            patientDelta=[int]$after.patients-[int]$before.patients
            portalDelta=[int]$after.portalAccounts-[int]$before.portalAccounts
            insuranceDelta=[int]$after.insuranceRecords-[int]$before.insuranceRecords
            requestDelta=[int]$after.requests-[int]$before.requests
            queueDelta=[int]$after.queueEntries-[int]$before.queueEntries
            applicantDelta=[int]$after.applicants-[int]$before.applicants
        }
}
catch {
    Add-Check 'Telehealth prospective-identity execution' $false @{
        message=$_.Exception.Message
        stack=$_.ScriptStackTrace
    }
}
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0005','TH-DEC-0006','TH-DEC-0007')
        dataClassification='synthetic-only-no-secrets-no-candidate-records'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}
if (-not $passed) { exit 1 }
