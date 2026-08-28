# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth prospective identity-proofing proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-prospective-identity-proofing.json'
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
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL prospective identity-proofing proof query failed.' }
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
  'identityProofingResults',(select count(*) from telehealth_applicant_identity_proofing_results));
"@) | ConvertFrom-Json
}

function Login-Staff {
    Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='admin';password='pass'} | ConvertTo-Json) -TimeoutSec 30
}

function New-NetworkedApplicant(
    [string]$Label,
    [string]$StateCode,
    [string]$PlanKey,
    [string]$MemberId,
    [hashtable]$AdminHeaders,
    [string]$Unique) {
    $secret = New-Secret
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp21-create')} `
        -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            legalFirstName='Proof';legalLastName=$Label;dateOfBirth='1991-05-16'
            email="sp21-$($Label.ToLowerInvariant())-$Unique@example.test";phone='404-555-0188'
            residenceStateCode=$StateCode;postalCode=$(if($StateCode -eq 'CA'){'94105'}elseif($StateCode -eq 'FL'){'33101'}else{'30301'})
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp21-verify')
        } -ContentType 'application/json' -TimeoutSec 30 `
        -Body (@{expectedVersion=$created.version;verificationCode='246810'} | ConvertTo-Json)
    $reviewHeaders = $AdminHeaders.Clone(); $reviewHeaders['X-Idempotency-Key'] = New-Key 'sp21-review'
    $reviewed = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" `
        -Method Put -Headers $reviewHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$verified.version;decision='ApprovedForProspectiveIntake'
            reason="Synthetic $Label identity-process fixture reviewed with no candidate."
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $safety = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/safety-triage" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp21-safety')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$reviewed.applicantVersion;currentLocationStateCode=$StateCode
            currentLocationConfirmed=$true;hasEmergencyWarning=$false;severeOrWorsening=$false
            requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $purpose = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/visit-purpose" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp21-purpose')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$safety.applicantVersion;purposeCategory='migraine';syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $precheck = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-precheck" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp21-precheck')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$purpose.applicantVersion;planKey=$PlanKey;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $details = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/member-insurance-details" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp21-details')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$precheck.applicantVersion;memberId=$MemberId;groupNumber='SYN-GROUP-01'
            subscriberRelationship='Self';subscriberFirstName=$null;subscriberLastName=$null
            subscriberDateOfBirth=$null;detailsConfirmed=$true;syntheticDataConfirmed=$true
        } | ConvertTo-Json -Depth 8)
    $eligibility = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/eligibility" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp21-eligibility')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$details.applicantVersion;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $network = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-determination" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp21-network')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$eligibility.applicantVersion;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    [pscustomobject]@{Secret=$secret;Created=$created;Eligibility=$eligibility;Network=$network}
}

function Invoke-Proofing([object]$Applicant,[string]$Key,[int]$ExpectedVersion=$Applicant.Network.applicantVersion) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/identity-proofing" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret;'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$ExpectedVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
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

    $ga = New-NetworkedApplicant 'Georgia' 'GA' 'harbor-mutual-hd' 'SYN-HM-1001' $adminHeaders $unique
    $ca = New-NetworkedApplicant 'California' 'CA' 'harbor-mutual-hd' 'SYN-HM-1001' $adminHeaders $unique
    $fl = New-NetworkedApplicant 'Florida' 'FL' 'harbor-mutual-hd' 'SYN-HM-1001' $adminHeaders $unique
    $inactive = New-NetworkedApplicant 'Inactive' 'CA' 'blue-valley-standard' 'SYN-BV-2002' $adminHeaders $unique
    $outOfNetwork = New-NetworkedApplicant 'OutOfNetwork' 'FL' 'pine-state-choice' 'SYN-PS-3003' $adminHeaders $unique
    $concurrent = New-NetworkedApplicant 'Concurrent' 'GA' 'harbor-mutual-hd' 'SYN-HM-1001' $adminHeaders $unique

    Add-Check 'GA, CA, and FL positive fixtures reach the bounded proofing boundary only after version 9 upstream evidence' (
        @(@($ga,$ca,$fl,$concurrent) | Where-Object {
            $_.Eligibility.eligibilityStatus -eq 'Active' -and
            $_.Eligibility.businessOutcome -eq 'EligibleBenefitsReported' -and
            $_.Network.applicantStatus -eq 'SyntheticPracticeNetworkRecorded' -and
            $_.Network.businessOutcome -eq 'PracticeInNetworkAcceptingNewPatients' -and
            [int]$_.Network.applicantVersion -eq 9
        }).Count -eq 4)

    Add-Check 'Inactive and out-of-network upstream evidence fail closed before any proofing result' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($inactive.Created.applicantId)/identity-proofing" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$inactive.Secret;'X-Idempotency-Key'=(New-Key 'sp21-inactive')
        } @{expectedVersion=$inactive.Network.applicantVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$($outOfNetwork.Created.applicantId)/identity-proofing" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$outOfNetwork.Secret;'X-Idempotency-Key'=(New-Key 'sp21-ooc')
        } @{expectedVersion=$outOfNetwork.Network.applicantVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_identity_proofing_results where applicant_id in ('$($inactive.Created.applicantId)','$($outOfNetwork.Created.applicantId)');") -eq 0)

    $gaId = [string]$ga.Created.applicantId; $gaVersion = [int]$ga.Network.applicantVersion
    Add-Check 'Unknown access, missing notice, missing synthetic confirmation, and stale version fail before evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/identity-proofing" @{
            'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret);'X-Idempotency-Key'=(New-Key 'sp21-denied')
        } @{expectedVersion=$gaVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/identity-proofing" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$ga.Secret;'X-Idempotency-Key'=(New-Key 'sp21-notice')
        } @{expectedVersion=$gaVersion;privacyNoticeAcknowledged=$false;syntheticDataConfirmed=$true}) -eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/identity-proofing" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$ga.Secret;'X-Idempotency-Key'=(New-Key 'sp21-synthetic')
        } @{expectedVersion=$gaVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$false}) -eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/identity-proofing" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$ga.Secret;'X-Idempotency-Key'=(New-Key 'sp21-stale')
        } @{expectedVersion=($gaVersion-1);privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_identity_proofing_results where applicant_id='$gaId';") -eq 0)

    $gaKey = New-Key 'sp21-ga'
    $gaWeb = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/identity-proofing" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$ga.Secret;'X-Idempotency-Key'=$gaKey
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$gaVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $gaResult = $gaWeb.Content | ConvertFrom-Json
    $gaReplay = Invoke-Proofing $ga $gaKey $gaVersion
    $gaJson = $gaResult | ConvertTo-Json -Depth 10 -Compress

    Add-Check 'Positive fixture separates every normalized process stage and reaches version 10' (
        $gaResult.applicantStatus -eq 'SyntheticIdentityProofingRecorded' -and
        [int]$gaResult.applicantVersion -eq 10 -and
        $gaResult.transportOutcome -eq 'SimulatedCompleted' -and
        $gaResult.evidenceCollectionStatus -eq 'FixtureReferenceAccepted' -and
        $gaResult.evidenceValidationStatus -eq 'ValidatedFixture' -and
        $gaResult.attributeValidationStatus -eq 'ValidatedFixture' -and
        $gaResult.applicantVerificationStatus -eq 'VerifiedFixture' -and
        $gaResult.fraudCheckStatus -eq 'NoIndicatorFixture' -and
        $gaResult.businessOutcome -eq 'SyntheticProofingPassed')

    Add-Check 'Compatibility metadata is process-concepts-only and never claims an assurance level' (
        $gaResult.adapterMode -eq 'NON_PRODUCTION' -and
        $gaResult.compatibilityTarget -eq 'NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY' -and
        $gaResult.practiceStatementKey -eq 'SYNTHETIC_IDENTITY_PRACTICE_STATEMENT' -and
        $gaResult.proofingMethod -eq 'SYNTHETIC_REMOTE_UNATTENDED_NON_BIOMETRIC' -and
        $gaResult.assuranceLevelAchieved -eq 'None' -and
        -not $gaResult.identityProofed)

    Add-Check 'Public response is private/no-store and contains no demographic, contact, insurance, document, government, biometric, or authoritative payload' (
        $gaWeb.Headers['Cache-Control'] -match 'no-store' -and
        $gaWeb.Headers['Pragma'] -match 'no-cache' -and
        $gaJson -notmatch '"legalFirstName":|"legalLastName":|"dateOfBirth":|"email":|"phone":|"postalCode":|"memberId":|"groupNumber":|"subscriberFirstName":|"subscriberLastName":|"subscriberDateOfBirth":|"documentImage":|"governmentIdentifier":|"biometricTemplate":|"authoritativeResponse":|"protectedPayload":|"rawRequest":|"rawResponse":')

    Add-Check 'Every real proofing, identity, patient, account, consent, request, queue, care, and external consequence stays false' (
        -not $gaResult.identityEvidenceCollected -and -not $gaResult.governmentIdentifierCollected -and
        -not $gaResult.biometricDataCollected -and -not $gaResult.authoritativeSourceQueried -and
        -not $gaResult.proofingNotificationSent -and -not $gaResult.redressCaseCreated -and
        -not $gaResult.authenticatorBound -and -not $gaResult.identityProofed -and
        -not $gaResult.canonicalPatientCreated -and -not $gaResult.chartLinked -and
        -not $gaResult.portalAccountCreated -and -not $gaResult.consentCreated -and
        -not $gaResult.requestCreated -and -not $gaResult.queueEnabled -and
        -not $gaResult.appointmentCreated -and -not $gaResult.encounterCreated -and
        -not $gaResult.careEnabled -and -not $gaResult.externalCallPerformed)

    Add-Check 'Exact replay returns immutable evidence while changed reuse and second commands fail' (
        $gaReplay.identityProofingResultId -eq $gaResult.identityProofingResultId -and
        $gaReplay.requestTraceToken -eq $gaResult.requestTraceToken -and
        $gaReplay.proofingSessionReference -eq $gaResult.proofingSessionReference -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/identity-proofing" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$ga.Secret;'X-Idempotency-Key'=$gaKey
        } @{expectedVersion=($gaVersion+1);privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/identity-proofing" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$ga.Secret;'X-Idempotency-Key'=(New-Key 'sp21-second')
        } @{expectedVersion=$gaVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_identity_proofing_results where applicant_id='$gaId';") -eq 1)

    foreach ($applicant in @($ca,$fl)) { $null = Invoke-Proofing $applicant (New-Key 'sp21-state') }
    Add-Check 'The same bounded process contract applies in Georgia, California, and Florida without state-derived proofing outcomes' (
        [int](Invoke-Scalar "select count(distinct location_state_code) from telehealth_applicant_identity_proofing_results where applicant_id in ('$($ga.Created.applicantId)','$($ca.Created.applicantId)','$($fl.Created.applicantId)');") -eq 3 -and
        [int](Invoke-Scalar "select count(distinct business_outcome) from telehealth_applicant_identity_proofing_results where applicant_id in ('$($ga.Created.applicantId)','$($ca.Created.applicantId)','$($fl.Created.applicantId)');") -eq 1)

    $concurrentId = [string]$concurrent.Created.applicantId
    $jobs = 1..12 | ForEach-Object {
        Start-ThreadJob -ScriptBlock {
            param($base,$id,$secret,$version,$ordinal)
            try {
                [int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/identity-proofing" `
                    -Method Post -Headers @{
                        'X-AvenChart-Telehealth-Applicant-Key'=$secret
                        'X-Idempotency-Key'="sp21-race-$ordinal-$([Guid]::NewGuid().ToString('N'))"
                    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
                        expectedVersion=$version;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true
                    } | ConvertTo-Json)).StatusCode
            }
            catch { if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { throw } }
        } -ArgumentList $ApiBaseUrl,$concurrentId,$concurrent.Secret,$concurrent.Network.applicantVersion,$_
    }
    $raceStatuses = @($jobs | Wait-Job | Receive-Job); $jobs | Remove-Job -Force
    Add-Check 'Twelve concurrent first writers produce exactly one immutable result and event' (
        @($raceStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($raceStatuses | Where-Object { $_ -eq 409 }).Count -eq 11 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_identity_proofing_results where applicant_id='$concurrentId';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$concurrentId' and action='prospective-synthetic-identity-proofing-recorded';") -eq 1)

    Add-Check 'Identity-proofing result and event evidence are append-only' (
        (Test-MutationRejected "update telehealth_applicant_identity_proofing_results set fraud_check_status='Changed' where identity_proofing_result_id='$($gaResult.identityProofingResultId)';") -and
        (Test-MutationRejected "delete from telehealth_applicant_identity_proofing_results where identity_proofing_result_id='$($gaResult.identityProofingResultId)';") -and
        (Test-MutationRejected "update telehealth_applicant_events set actor_type='system' where applicant_id='$gaId' and action='prospective-synthetic-identity-proofing-recorded';"))

    Add-Check 'Persisted proofing table has no raw person, insurance, document, government, biometric, or authoritative-response columns' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_identity_proofing_results'
  and column_name in ('legal_first_name','legal_last_name','date_of_birth','email','phone',
    'address','postal_code','member_id','group_number','subscriber_first_name',
    'subscriber_last_name','subscriber_date_of_birth','document_image','video_recording',
    'biometric_template','government_identifier','ssn','authoritative_response',
    'raw_request','raw_response','payload');
"@) -eq 0)

    $public = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$ga.Secret
    } -TimeoutSec 30
    $publicJson = $public | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant resume remains coarse and exposes no process result, reference, assurance, or evidence facts' (
        $public.status -eq 'SyntheticIdentityProofingRecorded' -and
        $public.identityAssurance -eq 'ContactControlOnly' -and
        -not $public.canonicalPatientCreated -and
        $publicJson -notmatch 'ValidatedFixture|VerifiedFixture|SyntheticProofingPassed|proof-session|evidence-|requestTrace|responseTrace|government|biometric|authoritative')

    $after = Get-Counts
    $canonicalNames = @('patients','portalAccounts','insuranceRecords','requests','queueEntries','intakeSnapshots','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    Add-Check 'Synthetic proofing changes only prospective evidence and causes zero canonical or downstream delta' (
        @($canonicalNames | Where-Object { [long]$before.$_ -ne [long]$after.$_ }).Count -eq 0 -and
        [long]$after.applicants -gt [long]$before.applicants -and
        [long]$after.identityProofingResults -gt [long]$before.identityProofingResults) @{
            before=$before;after=$after
        }
}
catch { Add-Check 'Prospective identity-proofing proof execution' $false $_.Exception.Message }
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decision='TH-DEC-0024'
        adapterMode='NON_PRODUCTION'
        compatibilityTarget='NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 14 | Set-Content -Encoding utf8 $resultPath
    if(-not $passed){throw "Prospective identity-proofing proof failed. See $resultPath"}
    Write-Host "Prospective identity-proofing proof passed ($($checks.Count) checks). Artifact: $resultPath"
}
