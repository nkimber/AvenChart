# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth prospective practice-network proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-prospective-practice-network.json'
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
  'networkPrechecks',(select count(*) from telehealth_applicant_practice_network_prechecks),
  'memberDetails',(select count(*) from telehealth_applicant_member_insurance_details),
  'eligibilityResults',(select count(*) from telehealth_applicant_eligibility_results),
  'networkDeterminations',(select count(*) from telehealth_applicant_practice_network_determinations));
"@) | ConvertFrom-Json
}

function Login-Staff {
    Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='admin';password='pass'} | ConvertTo-Json) -TimeoutSec 30
}

function New-NetworkReadyApplicant(
    [string]$Label,
    [string]$StateCode,
    [string]$PurposeCategory,
    [string]$PlanKey,
    [string]$MemberId,
    [hashtable]$AdminHeaders,
    [string]$Unique) {
    $secret = New-Secret
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp20-create')} `
        -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            legalFirstName='Network';legalLastName=$Label;dateOfBirth='1990-03-14'
            email="sp20-$($Label.ToLowerInvariant())-$Unique@example.test";phone='404-555-0199'
            residenceStateCode=$StateCode;postalCode=$(if($StateCode -eq 'CA'){'94105'}elseif($StateCode -eq 'FL'){'33101'}else{'30301'})
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp20-verify')
        } -ContentType 'application/json' -TimeoutSec 30 `
        -Body (@{expectedVersion=$created.version;verificationCode='246810'} | ConvertTo-Json)
    $reviewHeaders = $AdminHeaders.Clone()
    $reviewHeaders['X-Idempotency-Key'] = New-Key 'sp20-review'
    $reviewed = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" `
        -Method Put -Headers $reviewHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$verified.version;decision='ApprovedForProspectiveIntake'
            reason="Synthetic $Label practice-network fixture reviewed with no candidate."
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $safety = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/safety-triage" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp20-safety')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$reviewed.applicantVersion;currentLocationStateCode=$StateCode
            currentLocationConfirmed=$true;hasEmergencyWarning=$false;severeOrWorsening=$false
            requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $purpose = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/visit-purpose" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp20-purpose')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$safety.applicantVersion;purposeCategory=$PurposeCategory;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $precheck = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-precheck" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp20-precheck')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$purpose.applicantVersion;planKey=$PlanKey;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $details = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/member-insurance-details" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp20-details')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$precheck.applicantVersion;memberId=$MemberId;groupNumber='SYN-GROUP-01'
            subscriberRelationship='Self';subscriberFirstName=$null;subscriberLastName=$null
            subscriberDateOfBirth=$null;detailsConfirmed=$true;syntheticDataConfirmed=$true
        } | ConvertTo-Json -Depth 8)
    $eligibility = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/eligibility" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp20-eligibility')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$details.applicantVersion;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    [pscustomobject]@{
        Secret=$secret;Created=$created;Reviewed=$reviewed;Safety=$safety
        Purpose=$purpose;Precheck=$precheck;Details=$details;Eligibility=$eligibility
    }
}

function Invoke-Network([object]$Applicant,[string]$Key,[int]$ExpectedVersion=$Applicant.Eligibility.applicantVersion) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/practice-network-determination" `
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
    $harborApplicant = New-NetworkReadyApplicant 'Harbor' 'GA' 'migraine' 'harbor-mutual-hd' 'SYN-HM-1001' $adminHeaders $unique
    $blueApplicant = New-NetworkReadyApplicant 'Blue' 'CA' 'sleep' 'blue-valley-standard' 'SYN-BV-2002' $adminHeaders $unique
    $pineApplicant = New-NetworkReadyApplicant 'Pine' 'FL' 'migraine' 'pine-state-choice' 'SYN-PS-3003' $adminHeaders $unique
    $concurrentApplicant = New-NetworkReadyApplicant 'Concurrent' 'CA' 'migraine' 'blue-valley-standard' 'SYN-BV-2002' $adminHeaders $unique
    $applicants = @($harborApplicant,$blueApplicant,$pineApplicant,$concurrentApplicant)

    Add-Check 'Only fully provenance-bound GA, CA, and FL synthetic applicants with fresh eligibility reach the practice-network boundary' (
        @($applicants | Where-Object {
            $_.Reviewed.applicantStatus -eq 'IdentityReviewApproved' -and
            $_.Safety.applicantStatus -eq 'SafetyScreenPassed' -and
            $_.Purpose.applicantStatus -eq 'VisitPurposeRecorded' -and
            $_.Precheck.applicantStatus -eq 'PracticeNetworkPrecheckRecorded' -and
            $_.Details.applicantStatus -eq 'MemberInsuranceDetailsRecorded' -and
            $_.Eligibility.applicantStatus -eq 'SyntheticEligibilityRecorded' -and
            ([DateTimeOffset]$_.Eligibility.expiresAt) -gt ([DateTimeOffset]$_.Eligibility.checkedAt)
        }).Count -eq 4)

    $harborId = [string]$harborApplicant.Created.applicantId
    $harborVersion = [int]$harborApplicant.Eligibility.applicantVersion
    Add-Check 'Unknown access, missing synthetic confirmation, and stale versions fail before practice-network evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$harborId/practice-network-determination" @{
            'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret);'X-Idempotency-Key'=(New-Key 'sp20-denied')
        } @{expectedVersion=$harborVersion;syntheticDataConfirmed=$true}) -eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$harborId/practice-network-determination" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$harborApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp20-unconfirmed')
        } @{expectedVersion=$harborVersion;syntheticDataConfirmed=$false}) -eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$harborId/practice-network-determination" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$harborApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp20-stale')
        } @{expectedVersion=($harborVersion - 1);syntheticDataConfirmed=$true}) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_practice_network_determinations where applicant_id='$harborId';") -eq 0)

    $harborKey = New-Key 'sp20-harbor'
    $harborWeb = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$harborId/practice-network-determination" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$harborApplicant.Secret
            'X-Idempotency-Key'=$harborKey
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$harborVersion;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $harbor = $harborWeb.Content | ConvertFrom-Json
    $harborReplay = Invoke-Network $harborApplicant $harborKey $harborVersion
    $harborJson = $harbor | ConvertTo-Json -Depth 10 -Compress
    Add-Check 'Harbor fixture is private, normalized, short-lived, exactly replayable, and contains no member or physician identifier' (
        $harborWeb.Headers.'Cache-Control' -match 'no-store' -and
        $harbor.applicantStatus -eq 'SyntheticPracticeNetworkRecorded' -and
        $harbor.eligibilityStatus -eq 'Active' -and
        $harbor.eligibilityBusinessOutcome -eq 'EligibleBenefitsReported' -and
        $harbor.transportOutcome -eq 'SimulatedAvailable' -and
        $harbor.planNetworkMatchStatus -eq 'Matched' -and
        $harbor.practiceAffiliationStatus -eq 'InNetwork' -and
        $harbor.serviceAvailabilityStatus -eq 'Included' -and
        $harbor.newPatientAcceptanceStatus -eq 'Accepting' -and
        $harbor.businessOutcome -eq 'PracticeInNetworkAcceptingNewPatients' -and
        $harbor.practiceNetworkChecked -eq $true -and $harbor.practiceInNetwork -eq $true -and
        $harbor.newPatientsAccepted -eq $true -and
        $harbor.networkDeterminationId -eq $harborReplay.networkDeterminationId -and
        $harbor.requestTraceToken -eq $harborReplay.requestTraceToken -and
        $harbor.responseTraceToken -eq $harborReplay.responseTraceToken -and
        (([DateTimeOffset]$harbor.expiresAt)-([DateTimeOffset]$harbor.checkedAt)).TotalMinutes -eq 15 -and
        $harborJson -notmatch 'SYN-HM-1001|SYN-GROUP-01|"memberId":|"groupNumber":|subscriberFirstName|subscriberLastName|subscriberDateOfBirth|protectedPayload|physicianId|renderingPhysicianId|"npi":')

    Add-Check 'Reused key with changed command and a second semantic practice-network command are rejected' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$harborId/practice-network-determination" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$harborApplicant.Secret;'X-Idempotency-Key'=$harborKey
        } @{expectedVersion=999;syntheticDataConfirmed=$true}) -eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$harborId/practice-network-determination" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$harborApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp20-second')
        } @{expectedVersion=$harbor.applicantVersion;syntheticDataConfirmed=$true}) -eq 409)

    $blue = Invoke-Network $blueApplicant (New-Key 'sp20-blue')
    Add-Check 'Blue fixture preserves inactive eligibility separately from an unavailable practice-directory result' (
        $blue.eligibilityStatus -eq 'Inactive' -and
        $blue.eligibilityBusinessOutcome -eq 'CoverageInactive' -and
        $blue.transportOutcome -eq 'SimulatedUnavailable' -and
        $blue.planNetworkMatchStatus -eq 'Unknown' -and
        $blue.practiceAffiliationStatus -eq 'Unknown' -and
        $blue.serviceAvailabilityStatus -eq 'Unknown' -and
        $blue.newPatientAcceptanceStatus -eq 'Unknown' -and
        $blue.businessOutcome -eq 'UnableToDetermine' -and
        $blue.practiceNetworkChecked -eq $false -and $blue.practiceInNetwork -eq $false -and
        $blue.newPatientsAccepted -eq $false)

    $pine = Invoke-Network $pineApplicant (New-Key 'sp20-pine')
    Add-Check 'Pine fixture preserves subscriber-not-found eligibility separately from an out-of-network practice result' (
        $pine.eligibilityStatus -eq 'Unknown' -and
        $pine.eligibilityBusinessOutcome -eq 'SubscriberNotFound' -and
        $pine.transportOutcome -eq 'SimulatedAvailable' -and
        $pine.planNetworkMatchStatus -eq 'Matched' -and
        $pine.practiceAffiliationStatus -eq 'OutOfNetwork' -and
        $pine.serviceAvailabilityStatus -eq 'Excluded' -and
        $pine.newPatientAcceptanceStatus -eq 'Unknown' -and
        $pine.businessOutcome -eq 'PracticeOutOfNetwork' -and
        $pine.practiceNetworkChecked -eq $true -and $pine.practiceInNetwork -eq $false -and
        $pine.newPatientsAccepted -eq $false)

    $normalized = @($harbor,$blue,$pine)
    Add-Check 'All results carry fixed NON_PRODUCTION Plan-Net compatibility metadata and distinct opaque trace tokens without claiming a FHIR resource' (
        @($normalized | Where-Object {
            $_.adapterMode -eq 'NON_PRODUCTION' -and
            $_.compatibilityTarget -eq 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
            $_.datasetKey -eq 'avenchart-synthetic-practice-network-directory-2026-08' -and
            [int]$_.datasetVersion -eq 1 -and
            $_.serviceCategory -eq 'ProfessionalTelehealthConsultation' -and
            $_.requestTraceToken -ne $_.responseTraceToken -and
            $_.requestTraceToken -ne '00000000-0000-0000-0000-000000000000' -and
            $_.responseTraceToken -ne '00000000-0000-0000-0000-000000000000' -and
            $_.fhirResourceCreated -eq $false -and $_.liveDirectoryQueried -eq $false
        }).Count -eq 3)

    $guardId = [string]$concurrentApplicant.Created.applicantId
    Add-Check 'Database snapshot guard rejects practice-network evidence bound to another applicant provenance chain' (Test-MutationRejected @"
begin;
update telehealth_prospective_applicants
set status='SyntheticPracticeNetworkRecorded',version=version+1
where applicant_id='$guardId';
insert into telehealth_applicant_practice_network_determinations
select (jsonb_populate_record(
  null::telehealth_applicant_practice_network_determinations,
  to_jsonb(d) || jsonb_build_object(
    'network_determination_id',gen_random_uuid(),
    'applicant_id','$guardId',
    'resulting_applicant_version',9,
    'resulting_applicant_status','SyntheticPracticeNetworkRecorded',
    'idempotency_key','sp20-adversarial',
    'command_fingerprint',repeat('a',64)))).*
from telehealth_applicant_practice_network_determinations d
where d.applicant_id='$harborId';
rollback;
"@)

    $concurrentVersion = [int]$concurrentApplicant.Eligibility.applicantVersion
    $concurrentSecret = [string]$concurrentApplicant.Secret
    $concurrentBaseUrl = $ApiBaseUrl
    $concurrentStatuses = 1..12 | ForEach-Object -Parallel {
        try {
            [int](Invoke-WebRequest "$using:concurrentBaseUrl/api/telehealth/v1/applicants/$using:guardId/practice-network-determination" `
                -Method Post -Headers @{
                    'X-AvenChart-Telehealth-Applicant-Key'=$using:concurrentSecret
                    'X-Idempotency-Key'="sp20-concurrent-$($_)-$([Guid]::NewGuid().ToString('N'))"
                } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
                    expectedVersion=$using:concurrentVersion;syntheticDataConfirmed=$true
                } | ConvertTo-Json)).StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
            else { throw }
        }
    } -ThrottleLimit 12
    Add-Check 'Concurrent first practice-network commands have one winner and one immutable determination/event' (
        @($concurrentStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($concurrentStatuses | Where-Object { $_ -eq 409 }).Count -eq 11 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_practice_network_determinations where applicant_id='$guardId';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$guardId' and action='prospective-synthetic-practice-network-recorded';") -eq 1)

    $hardFalseProperties = @(
        'fhirResourceCreated','liveDirectoryQueried','renderingPhysicianNetworkChecked',
        'exactNetworkConfirmed','coverageVerified','identityProofed','canonicalPatientCreated',
        'chartLinked','portalAccountCreated','prospectiveIntakeCompleted','consentCreated',
        'practiceAccepted','coverageRecordCreated','estimateCreated','financialAcknowledgmentCreated',
        'requestCreated','queueEnabled','appointmentCreated','encounterCreated','careEnabled',
        'prescribingEnabled','billingEnabled','claimCreated','communicationEnabled','integrationEnabled',
        'externalCallPerformed')
    $allHardFalse = $true
    foreach ($response in $normalized) {
        foreach ($property in $hardFalseProperties) {
            if ($response.$property -ne $false) { $allHardFalse = $false }
        }
    }
    Add-Check 'Every response denies FHIR, live-directory, physician, exact-network, coverage, identity, financial, care, integration, and external consequences' $allHardFalse
    Add-Check 'Database hard-false flags reject all consequential practice-network states' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_practice_network_determinations where
fhir_resource_created or live_directory_queried or rendering_physician_network_checked or
exact_network_confirmed or coverage_verified or canonical_patient_created or identity_proofed or
chart_linked or portal_account_created or prospective_intake_completed or consent_created or
practice_accepted or coverage_record_created or estimate_created or financial_acknowledgment_created or
request_created or queue_enabled or appointment_created or encounter_created or care_enabled or
prescribing_enabled or billing_enabled or claim_created or communication_enabled or integration_enabled or
external_call_performed;
"@) -eq 0)

    Add-Check 'Practice-network schema stores only normalized directory evidence without member, subscriber, physician, raw payload, or FHIR payload columns' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_practice_network_determinations'
and column_name in ('protected_payload','member_id','member_id_last4','group_number','group_number_last4',
'subscriber_first_name','subscriber_last_name','subscriber_date_of_birth','physician_id','npi',
'raw_request','raw_response','fhir_resource','fhir_bundle');
"@) -eq 0)

    $publicWeb = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$harborId" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$harborApplicant.Secret} -TimeoutSec 30
    $public = $publicWeb.Content | ConvertFrom-Json
    $publicJson = $public | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant resume remains private and coarse without eligibility, network, member, trace, or provenance evidence' (
        $publicWeb.Headers.'Cache-Control' -match 'no-store' -and
        $public.status -eq 'SyntheticPracticeNetworkRecorded' -and
        $public.canonicalPatientCreated -eq $false -and
        $publicJson -notmatch 'eligibilityStatus|businessOutcome|benefitInformation|planKey|payerDisplay|practiceAffiliation|networkReference|memberId|groupNumber|traceToken|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId')

    Add-Check 'Practice-network determination and correlated applicant event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_practice_network_determinations set practice_affiliation_status='Unknown' where network_determination_id='$($harbor.networkDeterminationId)';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$harborId' and action='prospective-synthetic-practice-network-recorded';"))

    $after = Get-Counts
    $unchanged = @('patients','portalAccounts','insuranceRecords','requests','queueEntries',
        'intakeSnapshots','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    $allUnchanged = $true
    foreach ($name in $unchanged) {
        if ([int]$after.$name -ne [int]$before.$name) { $allUnchanged = $false }
    }
    Add-Check 'Synthetic practice-network determination creates no canonical insurance, patient, intake, financial, request, queue, clinical, prescribing, or downstream row' (
        $allUnchanged -and
        [int]$after.applicants -eq ([int]$before.applicants + 4) -and
        [int]$after.reviewDecisions -eq ([int]$before.reviewDecisions + 4) -and
        [int]$after.safetyEvaluations -eq ([int]$before.safetyEvaluations + 4) -and
        [int]$after.visitPurposes -eq ([int]$before.visitPurposes + 4) -and
        [int]$after.networkPrechecks -eq ([int]$before.networkPrechecks + 4) -and
        [int]$after.memberDetails -eq ([int]$before.memberDetails + 4) -and
        [int]$after.eligibilityResults -eq ([int]$before.eligibilityResults + 4) -and
        [int]$after.networkDeterminations -eq ([int]$before.networkDeterminations + 4)) @{
            applicantDelta=[int]$after.applicants-[int]$before.applicants
            eligibilityResultDelta=[int]$after.eligibilityResults-[int]$before.eligibilityResults
            networkDeterminationDelta=[int]$after.networkDeterminations-[int]$before.networkDeterminations
            canonicalAndDownstreamUnchanged=$allUnchanged
        }
}
catch {
    Add-Check 'Telehealth prospective practice-network execution' $false @{
        message=$_.Exception.Message
        stack=$_.ScriptStackTrace
    }
}
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0007','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019','TH-DEC-0020','TH-DEC-0021','TH-DEC-0022','TH-DEC-0023')
        dataClassification='synthetic-only-normalized-no-member-or-physician-input-no-fhir-resource-no-external-call'
        compatibilityTarget='HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}
if (-not $passed) { exit 1 }
