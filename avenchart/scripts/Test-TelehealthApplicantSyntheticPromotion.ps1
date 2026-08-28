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
    throw 'Telehealth applicant synthetic-promotion proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-synthetic-promotion.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-Check([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-Secret {
    $bytes=[byte[]]::new(32)
    $generator=[System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) }
    finally { $generator.Dispose() }
    [BitConverter]::ToString($bytes).Replace('-','').ToLowerInvariant()
}
function New-Key([string]$Prefix) { "$Prefix-$([Guid]::NewGuid().ToString('N'))" }
function Merge-Map([hashtable]$Base,[hashtable]$Overrides) {
    $copy=$Base.Clone()
    foreach($key in $Overrides.Keys){$copy[$key]=$Overrides[$key]}
    $copy
}
function Invoke-Status([string]$Method,[string]$Path,[hashtable]$Headers=@{},[object]$Body=$null) {
    $parameters=@{Uri="$ApiBaseUrl$Path";Method=$Method;Headers=$Headers;TimeoutSec=30}
    if($null-ne$Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Depth 8}
    try{[int](Invoke-WebRequest @parameters).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
}
function Invoke-Scalar([string]$Sql) {
    Push-Location $solutionRoot
    try {
        $value=docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -t -A -v ON_ERROR_STOP=1 -c $Sql
        if($LASTEXITCODE-ne 0){throw 'PostgreSQL synthetic-promotion proof query failed.'}
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
  'portalIdentityMappings',(select count(*) from patient_portal_external_identity_mappings),
  'insuranceRecords',(select count(*) from insurance_records),
  'requests',(select count(*) from telehealth_requests),
  'queueEntries',(select count(*) from telehealth_queue_entries),
  'intakeSnapshots',(select count(*) from telehealth_intake_snapshots),
  'patientConfirmations',(select count(*) from telehealth_patient_confirmations),
  'patientLocations',(select count(*) from telehealth_patient_locations),
  'coverageSelections',(select count(*) from telehealth_coverage_selections),
  'coverageVerifications',(select count(*) from telehealth_coverage_verifications),
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims),
  'prescriptions',(select count(*) from prescriptions),
  'promotions',(select count(*) from telehealth_applicant_synthetic_promotions));
"@)|ConvertFrom-Json
}

function New-AuthorizedApplicant(
    [string]$Label,
    [string]$StateCode,
    [string]$DateOfBirth,
    [string]$Phone,
    [hashtable]$AdminHeaders,
    [string]$Unique,
    [bool]$Authorize=$true) {
    $secret=New-Secret
    $created=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-create')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        legalFirstName='Atomic';legalLastName=$Label;dateOfBirth=$DateOfBirth
        email="sp23-$($Label.ToLowerInvariant())-$Unique@example.test";phone=$Phone
        residenceStateCode=$StateCode;postalCode=$(if($StateCode-eq'CA'){'94105'}elseif($StateCode-eq'FL'){'33101'}else{'30301'})
        syntheticDataConfirmed=$true
    }|ConvertTo-Json)
    $verified=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-verify')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$created.version;verificationCode='246810'}|ConvertTo-Json)
    $headers=$AdminHeaders.Clone();$headers['X-Idempotency-Key']=New-Key 'sp23-review'
    $review=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" -Method Put -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$verified.version;decision='ApprovedForProspectiveIntake';reason="Synthetic $Label atomic promotion fixture review completed.";syntheticDataConfirmed=$true
    }|ConvertTo-Json)
    $safety=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/safety-triage" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-safety')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$review.applicantVersion;currentLocationStateCode=$StateCode;currentLocationConfirmed=$true
        hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true
    }|ConvertTo-Json)
    $purpose=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/visit-purpose" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-purpose')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$safety.applicantVersion;purposeCategory='migraine';syntheticDataConfirmed=$true}|ConvertTo-Json)
    $precheck=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-precheck" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-precheck')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$purpose.applicantVersion;planKey='harbor-mutual-hd';syntheticDataConfirmed=$true}|ConvertTo-Json)
    $details=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/member-insurance-details" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-details')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$precheck.applicantVersion;memberId='SYN-HM-1001';groupNumber='SYN-GROUP-01';subscriberRelationship='Self'
        subscriberFirstName=$null;subscriberLastName=$null;subscriberDateOfBirth=$null;detailsConfirmed=$true;syntheticDataConfirmed=$true
    }|ConvertTo-Json -Depth 8)
    $eligibility=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/eligibility" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-eligibility')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$details.applicantVersion;syntheticDataConfirmed=$true}|ConvertTo-Json)
    $network=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-determination" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-network')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$eligibility.applicantVersion;syntheticDataConfirmed=$true}|ConvertTo-Json)
    $proofing=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/identity-proofing" -Method Post -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp23-proofing')
    } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=$network.applicantVersion;privacyNoticeAcknowledged=$true;syntheticDataConfirmed=$true}|ConvertTo-Json)
    $decisionHeaders=$AdminHeaders.Clone();$decisionHeaders['X-Idempotency-Key']=New-Key 'sp23-authorize'
    $decision=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/promotion-authorization-decision" -Method Put -Headers $decisionHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$proofing.applicantVersion
        decision=$(if($Authorize){'AuthorizedForSyntheticPromotion'}else{'DeniedForSyntheticPromotion'})
        reason="Synthetic $Label governance decision for the atomic promotion proof."
        noneAssuranceAcknowledged=$true;syntheticDataConfirmed=$true
    }|ConvertTo-Json)
    [pscustomobject]@{Secret=$secret;Created=$created;Decision=$decision;Label=$Label}
}

function Invoke-Promotion([object]$Applicant,[hashtable]$AdminHeaders,[string]$Key,[string]$Reason="Synthetic atomic patient-shell promotion was explicitly approved.") {
    $headers=$AdminHeaders.Clone();$headers['X-Idempotency-Key']=$Key
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($Applicant.Created.applicantId)/synthetic-promotion" -Method Put -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
        expectedVersion=$Applicant.Decision.applicantVersion;command='PromoteAuthorizedSyntheticApplicant';reason=$Reason
        canonicalPatientCreationAcknowledged=$true;noPortalNoCareAcknowledged=$true
    }|ConvertTo-Json)
}

try {
    $unique=[Guid]::NewGuid().ToString('N').Substring(0,12)
    $labelToken=-join(1..10|ForEach-Object{[char](Get-Random -Minimum 65 -Maximum 91)})
    $phoneSuffix=Get-Random -Minimum 1000 -Maximum 9999
    $racePhoneSuffix=Get-Random -Minimum 1000 -Maximum 9999
    while($racePhoneSuffix-eq$phoneSuffix){$racePhoneSuffix=Get-Random -Minimum 1000 -Maximum 9999}
    $admin=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username='admin';password='pass'}|ConvertTo-Json) -TimeoutSec 30
    $frontdesk=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username='gold-frontdesk-01';password='pass'}|ConvertTo-Json) -TimeoutSec 30
    $adminHeaders=@{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations'}
    $frontdeskHeaders=@{'X-AvenChart-Session'=$frontdesk.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations'}

    $success=New-AuthorizedApplicant "Success$labelToken" 'GA' '1984-12-03' "+1404555$phoneSuffix" $adminHeaders $unique
    $blocked=New-AuthorizedApplicant "Blocked$labelToken" 'FL' '1984-12-03' "+1404555$phoneSuffix" $adminHeaders $unique
    $race=New-AuthorizedApplicant "Race$labelToken" 'CA' '1986-04-19' "+1415555$racePhoneSuffix" $adminHeaders $unique
    $denied=New-AuthorizedApplicant "Denied$labelToken" 'GA' '1989-07-22' "+1678555$phoneSuffix" $adminHeaders $unique $false
    $successId=[string]$success.Created.applicantId
    $blockedId=[string]$blocked.Created.applicantId
    $raceId=[string]$race.Created.applicantId
    $deniedId=[string]$denied.Created.applicantId

    $queueWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/admin/applicant-synthetic-promotion" -Headers $adminHeaders -TimeoutSec 30
    $queue=$queueWeb.Content|ConvertFrom-Json
    $queueJson=$queue|ConvertTo-Json -Depth 12 -Compress
    $fixtureIds=@($successId,$blockedId,$raceId)
    $queued=@($queue.applicants|Where-Object{$fixtureIds-contains[string]$_.applicantId})
    Add-Check 'Private administrator queue contains only authorized current evidence from GA, CA, and FL' (
        $queueWeb.Headers['Cache-Control']-match'no-store'-and$queued.Count-eq 3-and
        @($queued|Where-Object{$_.status-eq'SyntheticPromotionAuthorized'-and[int]$_.version-eq 11-and$_.authorizationDecision-eq'AuthorizedForSyntheticPromotion'-and$_.assuranceLevelAchieved-eq'None'-and-not$_.identityProofed-and$_.allowedCommand-eq'PromoteAuthorizedSyntheticApplicant'}).Count-eq 3-and
        @($queue.applicants|Where-Object{[string]$_.applicantId-eq$deniedId}).Count-eq 0)
    Add-Check 'Promotion queue masks contacts and discloses no candidate, canonical, insurance, or proofing identifiers' (
        $queueJson-notmatch 'sp23-|\+1[0-9]{10}|canonicalPatient|patientId|legacyPid|memberId|groupNumber|subscriber|protectedPayload|requestTrace|responseTrace|identityProofingResultId|governmentIdentifier|biometric')

    $validBody=@{expectedVersion=11;command='PromoteAuthorizedSyntheticApplicant';reason='Synthetic atomic patient-shell promotion was explicitly approved.';canonicalPatientCreationAcknowledged=$true;noPortalNoCareAcknowledged=$true}
    $wrongFacilityHeaders=$adminHeaders.Clone()
    $wrongFacilityHeaders['X-AvenChart-Facility-Id']='20'
    $wrongFacilityHeaders['X-Idempotency-Key']=New-Key 'sp23-facility'
    Add-Check 'Anonymous, front-desk, wrong-facility, and denied-applicant promotion attempts fail closed' (
        (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-synthetic-promotion')-eq 401-and
        (Invoke-Status 'GET' '/api/telehealth/v1/admin/applicant-synthetic-promotion' $frontdeskHeaders)-eq 403-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" ($frontdeskHeaders+@{'X-Idempotency-Key'=(New-Key 'sp23-front')}) $validBody)-eq 403-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" $wrongFacilityHeaders $validBody)-in @(403,404)-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$deniedId/synthetic-promotion" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp23-denied')}) $validBody)-eq 409)

    Add-Check 'Invalid command, missing acknowledgments, short reason, and stale version fail before persistence' (
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp23-command')}) (Merge-Map $validBody @{command='CreatePatient'}))-eq 400-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp23-ack1')}) (Merge-Map $validBody @{canonicalPatientCreationAcknowledged=$false}))-eq 400-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp23-ack2')}) (Merge-Map $validBody @{noPortalNoCareAcknowledged=$false}))-eq 400-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp23-reason')}) (Merge-Map $validBody @{reason='short'}))-eq 400-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp23-stale')}) (Merge-Map $validBody @{expectedVersion=10}))-eq 409-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_synthetic_promotions where applicant_id='$successId';")-eq 0)

    $before=Get-Counts
    $successKey=New-Key 'sp23-success'
    $created=Invoke-Promotion $success $adminHeaders $successKey
    $replay=Invoke-Promotion $success $adminHeaders $successKey
    $createdJson=$created|ConvertTo-Json -Depth 10 -Compress
    Add-Check 'No-match promotion atomically creates one portal-disabled synthetic patient shell' (
        $created.outcome-eq'SyntheticPatientCreated'-and$created.applicantStatus-eq'SyntheticPatientPromoted'-and[int]$created.applicantVersion-eq 12-and
        -not$created.possibleMatchDetected-and$created.canonicalPatientCreated-and$created.policyKey-eq'SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION'-and
        [int]$created.policyVersion-eq 1-and$created.evidenceType-eq'AUTHORIZED_SYNTHETIC_APPLICANT_AND_CURRENT_DUPLICATE_RECHECK'-and
        -not$created.portalAccountCreated-and-not$created.prospectiveIntakeCompleted-and-not$created.consentCreated-and-not$created.practiceAccepted-and
        -not$created.insuranceCreated-and-not$created.requestCreated-and-not$created.queueEnabled-and-not$created.careEnabled-and
        $createdJson-notmatch 'canonicalPatientId|legacyPid|pubpid|TH-PAT-')
    Add-Check 'Exact replay converges while changed reuse and a second command fail closed' (
        $replay.promotionId-eq$created.promotionId-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" ($adminHeaders+@{'X-Idempotency-Key'=$successKey}) (Merge-Map $validBody @{reason='A different synthetic atomic promotion reason was supplied.'}))-eq 409-and
        (Invoke-Status 'PUT' "/api/telehealth/v1/admin/applicants/$successId/synthetic-promotion" ($adminHeaders+@{'X-Idempotency-Key'=(New-Key 'sp23-second')}) $validBody)-eq 409-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_synthetic_promotions where applicant_id='$successId';")-eq 1)

    $patient=(Invoke-Scalar @"
select json_build_object(
  'canonicalId',p.canonical_id,'pubpid',p.pubpid,'legacyPid',p.legacy_pid,
  'firstName',p.first_name,'lastName',p.last_name,'dateOfBirth',p.date_of_birth,
  'email',p.email,'phone',p.phone,'state',p.state,'postalCode',p.postal_code,
  'facilityId',p.facility_id,'providerId',p.provider_id,'portalEnabled',p.portal_enabled,
  'purpose',p.purpose,
  'portalAccounts',(select count(*) from patient_portal_accounts a where a.patient_id=p.canonical_id),
  'portalMappings',(select count(*) from patient_portal_external_identity_mappings m where m.patient_id=p.canonical_id),
  'insuranceRecords',(select count(*) from insurance_records i where i.patient_id=p.canonical_id))
from telehealth_applicant_synthetic_promotions x
join patients p on p.canonical_id=x.canonical_patient_id
where x.promotion_id='$($created.promotionId)';
"@)|ConvertFrom-Json
    $expectedPatientId="TH-PAT-$($successId.Replace('-','').ToUpperInvariant())"
    Add-Check 'Canonical patient mapping is deterministic, minimal, facility-bound, and portal-disabled' (
        $patient.canonicalId-eq$expectedPatientId-and$patient.pubpid-eq$expectedPatientId-and[long]$patient.legacyPid-gt 0-and
        $patient.firstName-eq'Atomic'-and$patient.lastName-eq$success.Label-and$patient.dateOfBirth-eq'1984-12-03'-and
        $patient.state-eq'GA'-and$patient.postalCode-eq'30301'-and[int]$patient.facilityId-eq 10-and$null-eq$patient.providerId-and
        -not$patient.portalEnabled-and$patient.purpose-eq'synthetic telehealth prospective promotion'-and
        [int]$patient.portalAccounts-eq 0-and[int]$patient.portalMappings-eq 0-and[int]$patient.insuranceRecords-eq 0)

    $blockedResult=Invoke-Promotion $blocked $adminHeaders (New-Key 'sp23-blocked')
    $blockedJson=$blockedResult|ConvertTo-Json -Depth 10 -Compress
    Add-Check 'Current possible match blocks creation without linking or identifying a patient' (
        $blockedResult.outcome-eq'BlockedPossiblePatientMatch'-and$blockedResult.applicantStatus-eq'SyntheticPromotionBlockedPossibleMatch'-and
        $blockedResult.possibleMatchDetected-and-not$blockedResult.canonicalPatientCreated-and
        $blockedJson-notmatch 'canonicalPatientId|legacyPid|pubpid|TH-PAT-'-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_synthetic_promotions where applicant_id='$blockedId' and possible_match_detected and canonical_patient_id is null and canonical_legacy_pid is null;")-eq 1-and
        [int](Invoke-Scalar "select count(*) from patients where canonical_id='TH-PAT-$($blockedId.Replace('-','').ToUpperInvariant())';")-eq 0)

    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$session,$ordinal)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/admin/applicants/$id/synthetic-promotion" -Method Put -Headers @{
            'X-AvenChart-Session'=$session;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations';'X-Idempotency-Key'="sp23-race-$ordinal-$([Guid]::NewGuid().ToString('N'))"
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{expectedVersion=11;command='PromoteAuthorizedSyntheticApplicant';reason="Synthetic concurrent atomic promotion decision $ordinal.";canonicalPatientCreationAcknowledged=$true;noPortalNoCareAcknowledged=$true}|ConvertTo-Json)).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$raceId,$admin.sessionId,$_}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    Add-Check 'Eight concurrent administrators create one patient, one promotion, and one aggregate event' (
        @($statuses|Where-Object{$_-eq 200}).Count-eq 1-and@($statuses|Where-Object{$_-eq 409}).Count-eq 7-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_synthetic_promotions where applicant_id='$raceId';")-eq 1-and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$raceId' and action='prospective-synthetic-patient-promotion-recorded';")-eq 1-and
        [int](Invoke-Scalar "select count(*) from patients where canonical_id='TH-PAT-$($raceId.Replace('-','').ToUpperInvariant())';")-eq 1) @{statuses=$statuses}

    $resume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$successId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $resumeJson=$resume|ConvertTo-Json -Depth 10 -Compress
    Add-Check 'Applicant resume exposes only a coarse promoted status and no canonical patient linkage' (
        $resume.status-eq'SyntheticPatientPromoted'-and$resume.contactVerified-and$resume.identityAssurance-eq'ContactControlOnly'-and$resume.canonicalPatientCreated-and
        $resumeJson-notmatch 'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|staffId|actorId|decisionReason')

    Add-Check 'Promotion evidence and aggregate event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_synthetic_promotions set reason='Changed synthetic reason.' where promotion_id='$($created.promotionId)';")-and
        (Test-MutationRejected "delete from telehealth_applicant_synthetic_promotions where promotion_id='$($created.promotionId)';")-and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$successId' and action='prospective-synthetic-patient-promotion-recorded';"))

    Add-Check 'Promotion evidence stores no duplicate candidate or raw external payload columns' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_synthetic_promotions'
  and column_name in ('matched_patient_id','candidate_patient_id','candidate_name','candidate_email','candidate_phone',
    'member_id','group_number','protected_payload','request_trace_token','response_trace_token',
    'government_identifier','biometric_template','raw_request','raw_response');
"@)-eq 0)

    $after=Get-Counts
    $downstream=@('portalAccounts','portalIdentityMappings','insuranceRecords','requests','queueEntries','intakeSnapshots','patientConfirmations','patientLocations','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    Add-Check 'Atomic outcomes add only two patient shells and three promotion records with zero downstream delta' (
        [long]$after.patients-[long]$before.patients-eq 2-and
        [long]$after.promotions-[long]$before.promotions-eq 3-and
        @($downstream|Where-Object{[long]$before.$_-ne[long]$after.$_}).Count-eq 0) @{before=$before;after=$after}
}
catch { Add-Check 'Applicant synthetic-promotion proof execution' $false "$($_.Exception.Message) | $($_.ScriptStackTrace)" }
finally {
    $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decision='TH-DEC-0026';checks=$checks}
    $result|ConvertTo-Json -Depth 14|Set-Content -Encoding utf8 $resultPath
    if(-not$passed){throw "Applicant synthetic-promotion proof failed. See $resultPath"}
    Write-Host "Applicant synthetic-promotion proof passed ($($checks.Count) checks). Artifact: $resultPath"
}
