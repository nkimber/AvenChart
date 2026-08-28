# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth prospective eligibility proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-prospective-eligibility.json'
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
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL prospective eligibility proof query failed.' }
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
  'networkPrechecks',(select count(*) from telehealth_applicant_practice_network_prechecks),
  'memberDetails',(select count(*) from telehealth_applicant_member_insurance_details),
  'eligibilityResults',(select count(*) from telehealth_applicant_eligibility_results));
"@) | ConvertFrom-Json
}

function Login-Staff {
    Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='admin';password='pass'} | ConvertTo-Json) -TimeoutSec 30
}

function New-EligibilityReadyApplicant(
    [string]$Label,
    [string]$StateCode,
    [string]$PurposeCategory,
    [string]$PlanKey,
    [string]$MemberId,
    [hashtable]$AdminHeaders,
    [string]$Unique) {
    $secret = New-Secret
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp19-create')} `
        -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            legalFirstName='Eligibility';legalLastName=$Label;dateOfBirth='1990-03-14'
            email="sp19-$($Label.ToLowerInvariant())-$Unique@example.test";phone='404-555-0199'
            residenceStateCode=$StateCode;postalCode=$(if($StateCode -eq 'CA'){'94105'}elseif($StateCode -eq 'FL'){'33101'}else{'30301'})
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp19-verify')
        } -ContentType 'application/json' -TimeoutSec 30 `
        -Body (@{expectedVersion=$created.version;verificationCode='246810'} | ConvertTo-Json)
    $reviewHeaders = $AdminHeaders.Clone()
    $reviewHeaders['X-Idempotency-Key'] = New-Key 'sp19-review'
    $reviewed = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" `
        -Method Put -Headers $reviewHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$verified.version;decision='ApprovedForProspectiveIntake'
            reason="Synthetic $Label eligibility fixture reviewed with no candidate."
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $safety = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/safety-triage" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp19-safety')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$reviewed.applicantVersion;currentLocationStateCode=$StateCode
            currentLocationConfirmed=$true;hasEmergencyWarning=$false;severeOrWorsening=$false
            requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $purpose = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/visit-purpose" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp19-purpose')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$safety.applicantVersion;purposeCategory=$PurposeCategory;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $precheck = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-precheck" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp19-precheck')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$purpose.applicantVersion;planKey=$PlanKey;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $details = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/member-insurance-details" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp19-details')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$precheck.applicantVersion;memberId=$MemberId;groupNumber='SYN-GROUP-01'
            subscriberRelationship='Self';subscriberFirstName=$null;subscriberLastName=$null
            subscriberDateOfBirth=$null;detailsConfirmed=$true;syntheticDataConfirmed=$true
        } | ConvertTo-Json -Depth 8)
    [pscustomobject]@{
        Secret=$secret;Created=$created;Reviewed=$reviewed;Safety=$safety
        Purpose=$purpose;Precheck=$precheck;Details=$details;RawMemberId=$MemberId
    }
}

function Invoke-Eligibility([object]$Applicant,[string]$Key,[int]$ExpectedVersion=$Applicant.Details.applicantVersion) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/eligibility" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$ExpectedVersion;syntheticDataConfirmed=$true
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
    $activeApplicant = New-EligibilityReadyApplicant 'Active' 'GA' 'migraine' 'harbor-mutual-hd' 'SYN-HM-1001' $adminHeaders $unique
    $inactiveApplicant = New-EligibilityReadyApplicant 'Inactive' 'CA' 'sleep' 'blue-valley-standard' 'SYN-BV-2002' $adminHeaders $unique
    $notFoundApplicant = New-EligibilityReadyApplicant 'Notfound' 'FL' 'migraine' 'pine-state-choice' 'SYN-PS-3003' $adminHeaders $unique
    $unknownApplicant = New-EligibilityReadyApplicant 'Unknown' 'GA' 'sleep' 'harbor-mutual-hd' 'SYN-HM-9999' $adminHeaders $unique
    $concurrentApplicant = New-EligibilityReadyApplicant 'Concurrent' 'CA' 'migraine' 'blue-valley-standard' 'SYN-BV-2002' $adminHeaders $unique
    $applicants = @($activeApplicant,$inactiveApplicant,$notFoundApplicant,$unknownApplicant,$concurrentApplicant)

    Add-Check 'Only fully provenance-bound GA, CA, and FL synthetic applicants reach the eligibility boundary' (
        @($applicants | Where-Object {
            $_.Reviewed.applicantStatus -eq 'IdentityReviewApproved' -and
            $_.Safety.applicantStatus -eq 'SafetyScreenPassed' -and
            $_.Purpose.applicantStatus -eq 'VisitPurposeRecorded' -and
            $_.Precheck.applicantStatus -eq 'PracticeNetworkPrecheckRecorded' -and
            $_.Details.applicantStatus -eq 'MemberInsuranceDetailsRecorded' -and
            $_.Details.coverageVerified -eq $false
        }).Count -eq 5)

    $guardId = [string]$concurrentApplicant.Created.applicantId
    $otherReviewId = [string](Invoke-Scalar "select decision_id from telehealth_applicant_identity_review_decisions where applicant_id='$($activeApplicant.Created.applicantId)';")
    Add-Check 'Database snapshot guard rejects eligibility evidence bound to another applicant review decision' (Test-MutationRejected @"
begin;
update telehealth_prospective_applicants
set status='SyntheticEligibilityRecorded',version=version+1
where applicant_id='$guardId';
insert into telehealth_applicant_eligibility_results(
  eligibility_result_id,applicant_id,practice_id,facility_id,
  identity_review_decision_id,safety_triage_evaluation_id,visit_purpose_id,
  practice_network_precheck_id,member_insurance_details_id,
  resulting_applicant_version,resulting_applicant_status,location_state_code,
  purpose_category,plan_key,payer_display_name,product_display_name,
  practice_network_status,member_id_last4,group_number_present,group_number_last4,
  subscriber_relationship,coverage_priority,date_of_service,service_category,
  adapter_mode,compatibility_target,dataset_key,dataset_version,
  dataset_effective_from,dataset_effective_through,inquiry_trace_token,
  response_trace_token,transport_outcome,member_match_status,eligibility_status,
  benefit_information_status,business_outcome,member_matched,
  member_eligibility_checked,member_benefits_checked,checked_at,expires_at,
  idempotency_key,command_fingerprint)
select gen_random_uuid(),a.applicant_id,a.practice_id,a.facility_id,
  '$otherReviewId'::uuid,s.evaluation_id,p.purpose_id,n.precheck_id,m.details_id,
  a.version,a.status,m.location_state_code,m.purpose_category,m.plan_key,
  m.payer_display_name,m.product_display_name,m.practice_network_status,
  m.member_id_last4,m.group_number_present,m.group_number_last4,
  m.subscriber_relationship,m.coverage_priority,
  (now() at time zone 'UTC')::date,'ProfessionalTelehealthConsultation',
  'NON_PRODUCTION','ASC_X12N_270_271_005010X279A1',
  'avenchart-synthetic-prospective-eligibility-2026-08',1,
  '2026-08-27T00:00:00Z','2026-10-31T23:59:59Z',gen_random_uuid(),gen_random_uuid(),
  'SimulatedAccepted','Matched','Inactive','NotReported','CoverageInactive',
  true,true,false,now(),now()+interval '15 minutes','sp19-adversarial',repeat('a',64)
from telehealth_prospective_applicants a
join telehealth_applicant_safety_triage_evaluations s on s.applicant_id=a.applicant_id
join telehealth_applicant_visit_purposes p on p.applicant_id=a.applicant_id
join telehealth_applicant_practice_network_prechecks n on n.applicant_id=a.applicant_id
join telehealth_applicant_member_insurance_details m on m.applicant_id=a.applicant_id
where a.applicant_id='$guardId';
rollback;
"@)

    $activeId = [string]$activeApplicant.Created.applicantId
    $activeVersion = [int]$activeApplicant.Details.applicantVersion
    Add-Check 'Unknown access, missing synthetic confirmation, and stale versions fail before eligibility evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$activeId/eligibility" @{
            'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret);'X-Idempotency-Key'=(New-Key 'sp19-denied')
        } @{expectedVersion=$activeVersion;syntheticDataConfirmed=$true}) -eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$activeId/eligibility" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$activeApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp19-unconfirmed')
        } @{expectedVersion=$activeVersion;syntheticDataConfirmed=$false}) -eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$activeId/eligibility" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$activeApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp19-stale')
        } @{expectedVersion=($activeVersion - 1);syntheticDataConfirmed=$true}) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_eligibility_results where applicant_id='$activeId';") -eq 0)

    $activeKey = New-Key 'sp19-active'
    $activeWeb = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$activeId/eligibility" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$activeApplicant.Secret
            'X-Idempotency-Key'=$activeKey
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$activeVersion;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $active = $activeWeb.Content | ConvertFrom-Json
    $activeReplay = Invoke-Eligibility $activeApplicant $activeKey $activeVersion
    $activeJson = $active | ConvertTo-Json -Depth 10 -Compress
    Add-Check 'Active fixture is private, normalized, mask-only, short-lived, and exactly replayable' (
        $activeWeb.Headers.'Cache-Control' -match 'no-store' -and
        $active.applicantStatus -eq 'SyntheticEligibilityRecorded' -and
        $active.transportOutcome -eq 'SimulatedAccepted' -and
        $active.memberMatchStatus -eq 'Matched' -and
        $active.eligibilityStatus -eq 'Active' -and
        $active.benefitInformationStatus -eq 'Reported' -and
        $active.businessOutcome -eq 'EligibleBenefitsReported' -and
        $active.memberMatched -eq $true -and $active.memberEligibilityChecked -eq $true -and
        $active.memberBenefitsChecked -eq $true -and
        $active.memberIdMask -eq '••••1001' -and
        $active.eligibilityResultId -eq $activeReplay.eligibilityResultId -and
        $active.inquiryTraceToken -eq $activeReplay.inquiryTraceToken -and
        $active.responseTraceToken -eq $activeReplay.responseTraceToken -and
        (([DateTimeOffset]$active.expiresAt)-([DateTimeOffset]$active.checkedAt)).TotalMinutes -eq 15 -and
        $activeJson -notmatch 'SYN-HM-1001|SYN-GROUP-01|protectedPayload|subscriberFirstName|subscriberLastName|subscriberDateOfBirth')

    Add-Check 'Reused key with changed command and a second semantic eligibility command are rejected' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$activeId/eligibility" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$activeApplicant.Secret;'X-Idempotency-Key'=$activeKey
        } @{expectedVersion=999;syntheticDataConfirmed=$true}) -eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$activeId/eligibility" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$activeApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp19-second')
        } @{expectedVersion=$active.applicantVersion;syntheticDataConfirmed=$true}) -eq 409)

    $inactive = Invoke-Eligibility $inactiveApplicant (New-Key 'sp19-inactive')
    Add-Check 'Inactive fixture separates successful transport and member match from inactive eligibility' (
        $inactive.transportOutcome -eq 'SimulatedAccepted' -and
        $inactive.memberMatchStatus -eq 'Matched' -and
        $inactive.eligibilityStatus -eq 'Inactive' -and
        $inactive.benefitInformationStatus -eq 'NotReported' -and
        $inactive.businessOutcome -eq 'CoverageInactive' -and
        $inactive.memberMatched -eq $true -and $inactive.memberEligibilityChecked -eq $true -and
        $inactive.memberBenefitsChecked -eq $false)

    $notFound = Invoke-Eligibility $notFoundApplicant (New-Key 'sp19-notfound')
    Add-Check 'Subscriber-not-found fixture separates accepted transport from a failed member match' (
        $notFound.transportOutcome -eq 'SimulatedAccepted' -and
        $notFound.memberMatchStatus -eq 'NotMatched' -and
        $notFound.eligibilityStatus -eq 'Unknown' -and
        $notFound.benefitInformationStatus -eq 'NotReported' -and
        $notFound.businessOutcome -eq 'SubscriberNotFound' -and
        $notFound.memberMatched -eq $false -and $notFound.memberEligibilityChecked -eq $true -and
        $notFound.memberBenefitsChecked -eq $false)

    $unknown = Invoke-Eligibility $unknownApplicant (New-Key 'sp19-unknown')
    Add-Check 'Unmapped fixture fails closed without implying a member, eligibility, or benefit check' (
        $unknown.transportOutcome -eq 'SimulatedUnavailable' -and
        $unknown.memberMatchStatus -eq 'Unknown' -and
        $unknown.eligibilityStatus -eq 'Unknown' -and
        $unknown.benefitInformationStatus -eq 'Unknown' -and
        $unknown.businessOutcome -eq 'UnableToDetermine' -and
        $unknown.memberMatched -eq $false -and $unknown.memberEligibilityChecked -eq $false -and
        $unknown.memberBenefitsChecked -eq $false)

    $normalized = @($active,$inactive,$notFound,$unknown)
    Add-Check 'All results carry fixed NON_PRODUCTION compatibility metadata and distinct opaque trace tokens' (
        @($normalized | Where-Object {
            $_.adapterMode -eq 'NON_PRODUCTION' -and
            $_.compatibilityTarget -eq 'ASC_X12N_270_271_005010X279A1' -and
            $_.datasetKey -eq 'avenchart-synthetic-prospective-eligibility-2026-08' -and
            [int]$_.datasetVersion -eq 1 -and
            $_.serviceCategory -eq 'ProfessionalTelehealthConsultation' -and
            $_.inquiryTraceToken -ne $_.responseTraceToken -and
            $_.inquiryTraceToken -ne '00000000-0000-0000-0000-000000000000' -and
            $_.responseTraceToken -ne '00000000-0000-0000-0000-000000000000'
        }).Count -eq 4)

    $concurrentId = [string]$concurrentApplicant.Created.applicantId
    $concurrentVersion = [int]$concurrentApplicant.Details.applicantVersion
    $concurrentSecret = [string]$concurrentApplicant.Secret
    $concurrentBaseUrl = $ApiBaseUrl
    $concurrentStatuses = 1..12 | ForEach-Object -Parallel {
        try {
            [int](Invoke-WebRequest "$using:concurrentBaseUrl/api/telehealth/v1/applicants/$using:concurrentId/eligibility" `
                -Method Post -Headers @{
                    'X-AvenChart-Telehealth-Applicant-Key'=$using:concurrentSecret
                    'X-Idempotency-Key'="sp19-concurrent-$($_)-$([Guid]::NewGuid().ToString('N'))"
                } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
                    expectedVersion=$using:concurrentVersion;syntheticDataConfirmed=$true
                } | ConvertTo-Json)).StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
            else { throw }
        }
    } -ThrottleLimit 12
    Add-Check 'Concurrent first eligibility commands have one winner and one immutable result/event' (
        @($concurrentStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($concurrentStatuses | Where-Object { $_ -eq 409 }).Count -eq 11 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_eligibility_results where applicant_id='$concurrentId';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$concurrentId' and action='prospective-synthetic-eligibility-recorded';") -eq 1)

    $hardFalseProperties = @(
        'rawTransactionCreated','renderingPhysicianNetworkChecked','coverageVerified','exactNetworkConfirmed',
        'identityProofed','canonicalPatientCreated','chartLinked','portalAccountCreated',
        'prospectiveIntakeCompleted','consentCreated','practiceAccepted','coverageRecordCreated',
        'estimateCreated','financialAcknowledgmentCreated','requestCreated','queueEnabled',
        'appointmentCreated','encounterCreated','careEnabled','prescribingEnabled','billingEnabled',
        'claimCreated','communicationEnabled','integrationEnabled','externalCallPerformed')
    $allHardFalse = $true
    foreach ($response in $normalized) {
        foreach ($property in $hardFalseProperties) {
            if ($response.$property -ne $false) { $allHardFalse = $false }
        }
    }
    Add-Check 'Every response denies raw transaction, exact network, coverage, identity, financial, care, integration, and external consequences' $allHardFalse
    Add-Check 'Database hard-false flags reject all consequential eligibility states' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_eligibility_results where
raw_transaction_created or exact_network_confirmed or coverage_verified or canonical_patient_created or
identity_proofed or chart_linked or portal_account_created or prospective_intake_completed or
consent_created or practice_accepted or coverage_record_created or estimate_created or
financial_acknowledgment_created or request_created or queue_enabled or appointment_created or
encounter_created or care_enabled or prescribing_enabled or billing_enabled or claim_created or
communication_enabled or integration_enabled or external_call_performed;
"@) -eq 0)

    Add-Check 'Eligibility result schema stores normalized masks and outcomes without protected or raw member, subscriber, request, or response payload columns' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_eligibility_results'
and column_name in ('protected_payload','member_id','group_number','subscriber_first_name',
'subscriber_last_name','subscriber_date_of_birth','raw_request','raw_response','x12_payload');
"@) -eq 0)

    $publicWeb = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$activeId" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$activeApplicant.Secret} -TimeoutSec 30
    $public = $publicWeb.Content | ConvertFrom-Json
    $publicJson = $public | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant resume remains private and coarse without eligibility, network, member, trace, or provenance evidence' (
        $publicWeb.Headers.'Cache-Control' -match 'no-store' -and
        $public.status -eq 'SyntheticEligibilityRecorded' -and
        $public.canonicalPatientCreated -eq $false -and
        $publicJson -notmatch 'eligibilityStatus|businessOutcome|benefitInformation|planKey|payerDisplay|practiceNetworkStatus|memberId|groupNumber|traceToken|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId')

    Add-Check 'Eligibility result and correlated applicant event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_eligibility_results set eligibility_status='Unknown' where eligibility_result_id='$($active.eligibilityResultId)';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$activeId' and action='prospective-synthetic-eligibility-recorded';"))

    $after = Get-Counts
    $unchanged = @('patients','portalAccounts','insuranceRecords','requests','queueEntries',
        'intakeSnapshots','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    $allUnchanged = $true
    foreach ($name in $unchanged) {
        if ([int]$after.$name -ne [int]$before.$name) { $allUnchanged = $false }
    }
    Add-Check 'Synthetic eligibility creates no canonical insurance, patient, intake, financial, request, queue, clinical, prescribing, or downstream row' (
        $allUnchanged -and
        [int]$after.applicants -eq ([int]$before.applicants + 5) -and
        [int]$after.reviewDecisions -eq ([int]$before.reviewDecisions + 5) -and
        [int]$after.safetyEvaluations -eq ([int]$before.safetyEvaluations + 5) -and
        [int]$after.visitPurposes -eq ([int]$before.visitPurposes + 5) -and
        [int]$after.networkPrechecks -eq ([int]$before.networkPrechecks + 5) -and
        [int]$after.memberDetails -eq ([int]$before.memberDetails + 5) -and
        [int]$after.eligibilityResults -eq ([int]$before.eligibilityResults + 5)) @{
            applicantDelta=[int]$after.applicants-[int]$before.applicants
            memberDetailsDelta=[int]$after.memberDetails-[int]$before.memberDetails
            eligibilityResultDelta=[int]$after.eligibilityResults-[int]$before.eligibilityResults
            canonicalAndDownstreamUnchanged=$allUnchanged
        }
}
catch {
    Add-Check 'Telehealth prospective eligibility execution' $false @{
        message=$_.Exception.Message
        stack=$_.ScriptStackTrace
    }
}
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0007','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019','TH-DEC-0020','TH-DEC-0021','TH-DEC-0022')
        dataClassification='synthetic-only-normalized-no-raw-transaction-no-external-call'
        compatibilityTarget='ASC_X12N_270_271_005010X279A1'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}
if (-not $passed) { exit 1 }
