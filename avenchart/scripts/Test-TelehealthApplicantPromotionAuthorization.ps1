# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5001',
    [ValidatePattern('^[a-z][a-z0-9_]{2,62}$')]
    [string]$DatabaseName = 'avenchart'
)

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth applicant promotion-authorization proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-promotion-authorization.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-Check([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-Secret { $bytes=[byte[]]::new(32);[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes);[Convert]::ToHexString($bytes).ToLowerInvariant() }
function New-Key([string]$Prefix) { "$Prefix-$([Guid]::NewGuid().ToString('N'))" }
function Invoke-Status([string]$Method,[string]$Path,[hashtable]$Headers=@{},[object]$Body=$null) {
    $parameters=@{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=$Headers;TimeoutSec=30}
    if($null-ne$Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Depth 8}
    try{[int](Invoke-WebRequest @parameters).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
}
function Invoke-Scalar([string]$Sql) {
    Push-Location $solutionRoot
    try {
        $value=docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -t -A -v ON_ERROR_STOP=1 -c $Sql
        if($LASTEXITCODE-ne 0){throw 'PostgreSQL promotion-authorization proof query failed.'}
        ($value|Select-Object -Last 1).Trim()
    } finally { Pop-Location }
}
function Test-MutationRejected([string]$Sql) {
    Push-Location $solutionRoot
    try { docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -v ON_ERROR_STOP=1 -c $Sql *> $null; $LASTEXITCODE-ne 0 }
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
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims),
  'prescriptions',(select count(*) from prescriptions),
  'applicants',(select count(*) from telehealth_prospective_applicants),
  'promotionDecisions',(select count(*) from telehealth_applicant_promotion_authorization_decisions));
"@)|ConvertFrom-Json
}

function New-ProofedApplicant([string]$Label,[string]$StateCode,[hashtable]$AdminHeaders,[string]$Unique) {
    $secret=New-Secret
    $created=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-create')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        legalFirstName='Promote';legalLastName=$Label;dateOfBirth='1991-05-16'
        email="sp22-$($Label.ToLowerInvariant())-$Unique@example.test";phone='404-555-0188'
        residenceStateCode=$StateCode;postalCode=$(if($StateCode-eq'CA'){'94105'}elseif($StateCode-eq'FL'){'33101'}else{'30301'})
        syntheticDataConfirmed=$true
    }|ConvertTo-Json)
    $verified=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-verify')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$created.version;verificationCode='246810'}|ConvertTo-Json)
    $headers=$AdminHeaders.Clone();$headers['X-Idempotency-Key']=New-Key 'sp22-review'
    $review=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" -Method Put -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$verified.version;decision='ApprovedForProspectiveIntake';reason="Synthetic $Label promotion fixture review completed.";syntheticDataConfirmed=$true
    }|ConvertTo-Json)
    $safety=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/safety-triage" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-safety')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$review.applicantVersion;currentLocationStateCode=$StateCode;currentLocationConfirmed=$true
        hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true
    }|ConvertTo-Json)
    $purpose=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/visit-purpose" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-purpose')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$safety.applicantVersion;purposeCategory='migraine';syntheticDataConfirmed=$true}|ConvertTo-Json)
    $precheck=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-precheck" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-precheck')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$purpose.applicantVersion;planKey='harbor-mutual-hd';syntheticDataConfirmed=$true}|ConvertTo-Json)
    $details=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/member-insurance-details" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-details')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$precheck.applicantVersion;memberId='SYN-HM-1001';groupNumber='SYN-GROUP-01';subscriberRelationship='Self'
        subscriberFirstName=$null;subscriberLastName=$null;subscriberDateOfBirth=$null;detailsConfirmed=$true;syntheticDataConfirmed=$true
    }|ConvertTo-Json -Depth 8)
    $eligibility=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/eligibility" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-eligibility')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$details.applicantVersion;syntheticDataConfirmed=$true}|ConvertTo-Json)
    $network=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-determination" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-network')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$eligibility.applicantVersion;syntheticDataConfirmed=$true}|ConvertTo-Json)
    $proofing=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/identity-proofing" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp22-proofing')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$network.applicantVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}|ConvertTo-Json)
    [pscustomobject]@{Secret=$secret;Created=$created;Proofing=$proofing}
}

function Invoke-Decision([object]$Applicant,[hashtable]$AdminHeaders,[string]$Key,[string]$Decision='AuthorizedForSyntheticPromotion',[int]$ExpectedVersion=$Applicant.Proofing.applicantVersion) {
    $headers=$AdminHeaders.Clone();$headers['X-Idempotency-Key']=$Key
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($Applicant.Created.applicantId)/promotion-authorization-decision" -Method Put -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$ExpectedVersion;decision=$Decision;reason="Synthetic promotion governance decision for $Decision."
        noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true
    }|ConvertTo-Json)
}

try {
    $before=Get-Counts
    $unique=[Guid]::NewGuid().ToString('N')
    $admin=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username='admin';password='pass'}|ConvertTo-Json) -TimeoutSec 30
    $adminHeaders=@{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations'}
    $ga=New-ProofedApplicant 'Georgia' 'GA' $adminHeaders $unique
    $ca=New-ProofedApplicant 'California' 'CA' $adminHeaders $unique
    $fl=New-ProofedApplicant 'Florida' 'FL' $adminHeaders $unique
    $race=New-ProofedApplicant 'Concurrent' 'GA' $adminHeaders $unique

    Add-Check 'GA, CA, FL, and contention fixtures reach only version 10 synthetic process evidence' (
        @($ga,$ca,$fl,$race|Where-Object{$_.Proofing.applicantStatus-eq'SyntheticIdentityProofingRecorded'-and[int]$_.Proofing.applicantVersion-eq 10-and$_.Proofing.assuranceLevelAchieved-eq'None'-and-not$_.Proofing.identityProofed}).Count-eq 4)

    $queueWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/admin/applicant-promotion-authorization" -Headers $adminHeaders -TimeoutSec 30
    $queue=$queueWeb.Content|ConvertFrom-Json
    $queueJson=$queue|ConvertTo-Json -Depth 12 -Compress
    $ids=@($ga,$ca,$fl,$race|ForEach-Object{[string]$_.Created.applicantId})
    $queued=@($queue.applicants|Where-Object{$ids-contains[string]$_.applicantId})
    Add-Check 'Private staff queue contains each complete fixture with normalized evidence and both server-derived decisions' (
        $queueWeb.Headers['Cache-Control']-match'no-store'-and$queued.Count-eq 4-and
        @($queued|Where-Object{$_.eligibilityBusinessOutcome-eq'EligibleBenefitsReported'-and$_.networkBusinessOutcome-eq'PracticeInNetworkAcceptingNewPatients'-and$_.proofingBusinessOutcome-eq'SyntheticProofingPassed'-and$_.assuranceLevelAchieved-eq'None'-and-not$_.identityProofed-and@($_.allowedDecisions).Count-eq 2}).Count-eq 4)
    Add-Check 'Staff queue is masked and excludes raw insurance, provider references, canonical identifiers, and proofing payloads' (
        $queueJson-notmatch'"email":|"phone":|"memberId":|"groupNumber":|subscriberFirst|subscriberLast|protectedPayload|requestTrace|responseTrace|proofingSessionReference|evidencePackageReference|identityProofingResultId|patientId|canonicalPatientId|governmentIdentifier|biometricData|biometricTemplate')

    $gaId=[string]$ga.Created.applicantId;$gaVersion=[int]$ga.Proofing.applicantVersion
    Add-Check 'Missing acknowledgments, invalid decision, short reason, and stale version fail before persistence' (
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$gaId/promotion-authorization-decision" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp22-noack')}) @{expectedVersion=$gaVersion;decision='AuthorizedForSyntheticPromotion';reason='Synthetic promotion review complete.';noneAssuranceAcknowledged=$false;syntheticDataConfirmed=$true})-eq 400-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$gaId/promotion-authorization-decision" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp22-invalid')}) @{expectedVersion=$gaVersion;decision='CreatePatient';reason='Synthetic promotion review complete.';noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true})-eq 400-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$gaId/promotion-authorization-decision" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp22-short')}) @{expectedVersion=$gaVersion;decision='AuthorizedForSyntheticPromotion';reason='short';noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true})-eq 400-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$gaId/promotion-authorization-decision" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp22-stale')}) @{expectedVersion=($gaVersion-1);decision='AuthorizedForSyntheticPromotion';reason='Synthetic promotion review complete.';noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true})-eq 409-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_promotion_authorization_decisions where applicant_id='$gaId';")-eq 0)

    $gaKey=New-Key 'sp22-authorize';$gaDecision=Invoke-Decision $ga $adminHeaders $gaKey
    $gaReplay=Invoke-Decision $ga $adminHeaders $gaKey
    Add-Check 'Authorization reaches prospective version 11 with explicit policy and every real consequence false' (
        $gaDecision.applicantStatus-eq'SyntheticPromotionAuthorized'-and[int]$gaDecision.applicantVersion-eq 11-and
        $gaDecision.policyKey-eq'SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION'-and$gaDecision.evidenceType-eq'COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY'-and
        $gaDecision.noneAssuranceAcknowledged-and-not$gaDecision.realIdentityProofed-and-not$gaDecision.canonicalPatientCreated-and-not$gaDecision.chartLinked-and-not$gaDecision.portalAccountCreated-and-not$gaDecision.prospectiveIntakeCompleted-and-not$gaDecision.consentCreated-and-not$gaDecision.practiceAccepted-and-not$gaDecision.requestCreated-and-not$gaDecision.queueEnabled)
    Add-Check 'Exact replay converges while changed reuse and a second command fail closed' (
        $gaReplay.decisionId-eq$gaDecision.decisionId-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$gaId/promotion-authorization-decision" ($adminHeaders+@{'X-Idempotency-Key'=$gaKey}) @{expectedVersion=$gaVersion;decision='DeniedForSyntheticPromotion';reason='Synthetic promotion governance decision for DeniedForSyntheticPromotion.';noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true})-eq 409-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$gaId/promotion-authorization-decision" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp22-second')}) @{expectedVersion=$gaVersion;decision='AuthorizedForSyntheticPromotion';reason='Synthetic promotion review complete.';noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true})-eq 409-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_promotion_authorization_decisions where applicant_id='$gaId';")-eq 1)

    $resume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$ga.Secret} -TimeoutSec 30
    $resumeJson=$resume|ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant resume exposes only the coarse staff governance status and preserves every unavailable consequence' (
        $resume.status-eq'SyntheticPromotionAuthorized'-and$resume.contactVerified-and$resume.identityAssurance-eq'ContactControlOnly'-and
        -not$resume.canonicalPatientCreated-and
        $resumeJson-notmatch'decisionId|decisionReason|policyKey|staffId|actorId|proofingResult|assuranceLevelAchieved|SyntheticProofingPassed|evidencePackage|requestTrace|responseTrace|portalAccountCreated|requestCreated|queueEnabled')

    $caDecision=Invoke-Decision $ca $adminHeaders (New-Key 'sp22-deny') 'DeniedForSyntheticPromotion'
    $flDecision=Invoke-Decision $fl $adminHeaders (New-Key 'sp22-state')
    Add-Check 'Authorized and denied decisions stay prospective across Georgia, California, and Florida' (
        $caDecision.applicantStatus-eq'SyntheticPromotionDenied'-and$flDecision.applicantStatus-eq'SyntheticPromotionAuthorized'-and
        [int](Invoke-Scalar "select count(distinct location_state_code) from telehealth_applicant_promotion_authorization_decisions where applicant_id in ('$gaId','$($ca.Created.applicantId)','$($fl.Created.applicantId)');")-eq 3)

    $raceId=[string]$race.Created.applicantId
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$session,$ordinal)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/admin/applicants/$id/promotion-authorization-decision" -Method Put -Headers @{
            'X-AvenChart-Session'=$session;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations';'X-Idempotency-Key'="sp22-race-$ordinal-$([Guid]::NewGuid().ToString('N'))"
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=10;decision='AuthorizedForSyntheticPromotion';reason="Synthetic concurrent governance decision $ordinal.";noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true}|ConvertTo-Json)).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$raceId,$admin.sessionId,$_}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    Add-Check 'Eight concurrent staff writers produce one immutable decision and one event' (
        @($statuses|Where-Object{$_-eq 200}).Count-eq 1-and@($statuses|Where-Object{$_-eq 409}).Count-eq 7-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_promotion_authorization_decisions where applicant_id='$raceId';")-eq 1-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$raceId' and action='prospective-synthetic-promotion-authorization-recorded';")-eq 1)

    Add-Check 'Promotion decision and aggregate event evidence are append-only' (
        (Test-MutationRejected "update telehealth_applicant_promotion_authorization_decisions set reason='Changed synthetic reason.' where decision_id='$($gaDecision.decisionId)';")-and
        (Test-MutationRejected "delete from telehealth_applicant_promotion_authorization_decisions where decision_id='$($gaDecision.decisionId)';")-and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$gaId' and action='prospective-synthetic-promotion-authorization-recorded';"))

    Add-Check 'Persisted decision has no raw person, insurance, proofing payload, or canonical identifier columns' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_promotion_authorization_decisions'
  and column_name in ('legal_first_name','legal_last_name','date_of_birth','email','phone','postal_code',
    'member_id','group_number','subscriber_first_name','subscriber_last_name','protected_payload',
    'request_trace_token','response_trace_token','proofing_session_reference','evidence_package_reference',
    'patient_id','canonical_patient_id','government_identifier','biometric_template','raw_request','raw_response');
"@)-eq 0)

    $after=Get-Counts
    $canonical=@('patients','portalAccounts','insuranceRecords','requests','queueEntries','intakeSnapshots','appointments','encounters','claims','prescriptions')
    Add-Check 'Governance decisions change only prospective evidence and cause zero canonical or downstream delta' (
        @($canonical|Where-Object{[long]$before.$_-ne[long]$after.$_}).Count-eq 0-and
        [long]$after.applicants-gt[long]$before.applicants-and[long]$after.promotionDecisions-gt[long]$before.promotionDecisions) @{before=$before;after=$after}
}
catch { Add-Check 'Applicant promotion-authorization proof execution' $false $_.Exception.Message }
finally {
    $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decision='TH-DEC-0025';checks=$checks}
    $result|ConvertTo-Json -Depth 14|Set-Content -Encoding utf8 $resultPath
    if(-not$passed){throw "Applicant promotion-authorization proof failed. See $resultPath"}
    Write-Host "Applicant promotion-authorization proof passed ($($checks.Count) checks). Artifact: $resultPath"
}
