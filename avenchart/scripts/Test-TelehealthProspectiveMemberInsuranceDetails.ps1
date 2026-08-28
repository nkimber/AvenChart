# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5001')

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth prospective member-insurance details proof is local-only.'
}
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-prospective-member-insurance-details.json'
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
        if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL prospective member-details proof query failed.' }
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
  'memberDetails',(select count(*) from telehealth_applicant_member_insurance_details));
"@) | ConvertFrom-Json
}

function Login-Staff {
    Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='admin';password='pass'} | ConvertTo-Json) -TimeoutSec 30
}

function New-PrecheckedApplicant(
    [string]$Label,
    [string]$StateCode,
    [string]$PurposeCategory,
    [string]$PlanKey,
    [hashtable]$AdminHeaders,
    [string]$Unique) {
    $secret = New-Secret
    $created = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants" -Method Post `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=(New-Key 'sp18-create')} `
        -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            legalFirstName='Member';legalLastName=$Label;dateOfBirth='1990-03-14'
            email="sp18-$($Label.ToLowerInvariant())-$Unique@example.test";phone='404-555-0199'
            residenceStateCode=$StateCode;postalCode=$(if($StateCode -eq 'CA'){'94105'}elseif($StateCode -eq 'FL'){'33101'}else{'30301'})
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $verified = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/contact-verification" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp18-verify')
        } -ContentType 'application/json' -TimeoutSec 30 `
        -Body (@{expectedVersion=$created.version;verificationCode='246810'} | ConvertTo-Json)
    $reviewHeaders = $AdminHeaders.Clone()
    $reviewHeaders['X-Idempotency-Key'] = New-Key 'sp18-review'
    $reviewed = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicants/$($created.applicantId)/identity-review-decision" `
        -Method Put -Headers $reviewHeaders -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$verified.version;decision='ApprovedForProspectiveIntake'
            reason="Synthetic $Label protected member-detail fixture reviewed with no candidate."
            syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $safety = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/safety-triage" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp18-safety')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$reviewed.applicantVersion;currentLocationStateCode=$StateCode
            currentLocationConfirmed=$true;hasEmergencyWarning=$false;severeOrWorsening=$false
            requiresHandsOnExam=$false;unsure=$false;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $purpose = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/visit-purpose" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp18-purpose')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$safety.applicantVersion;purposeCategory=$PurposeCategory;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    $precheck = Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($created.applicantId)/practice-network-precheck" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret
            'X-Idempotency-Key'=(New-Key 'sp18-precheck')
        } -ContentType 'application/json' -TimeoutSec 30 -Body (@{
            expectedVersion=$purpose.applicantVersion;planKey=$PlanKey;syntheticDataConfirmed=$true
        } | ConvertTo-Json)
    [pscustomobject]@{Secret=$secret;Created=$created;Reviewed=$reviewed;Safety=$safety;Purpose=$purpose;Precheck=$precheck}
}

function New-MemberBody(
    [int]$ExpectedVersion,
    [string]$MemberId,
    [string]$GroupNumber,
    [string]$Relationship='Self') {
    @{
        expectedVersion=$ExpectedVersion;memberId=$MemberId;groupNumber=$GroupNumber
        subscriberRelationship=$Relationship;subscriberFirstName=$null
        subscriberLastName=$null;subscriberDateOfBirth=$null
        detailsConfirmed=$true;syntheticDataConfirmed=$true
    }
}

function Invoke-MemberDetails([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/member-insurance-details" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body | ConvertTo-Json -Depth 8)
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
    $selfApplicant = New-PrecheckedApplicant 'Self' 'GA' 'migraine' 'harbor-mutual-hd' $adminHeaders $unique
    $spouseApplicant = New-PrecheckedApplicant 'Spouse' 'CA' 'sleep' 'blue-valley-standard' $adminHeaders $unique
    $concurrentApplicant = New-PrecheckedApplicant 'Concurrent' 'FL' 'migraine' 'pine-state-choice' $adminHeaders $unique
    $applicants = @($selfApplicant,$spouseApplicant,$concurrentApplicant)
    $selfId = [string]$selfApplicant.Created.applicantId
    $spouseId = [string]$spouseApplicant.Created.applicantId
    Add-Check 'Only approved GA, CA, and FL applicants with passing purpose and immutable precheck provenance reach member-detail confirmation' (
        @($applicants | Where-Object {
            $_.Reviewed.applicantStatus -eq 'IdentityReviewApproved' -and
            $_.Safety.applicantStatus -eq 'SafetyScreenPassed' -and
            $_.Purpose.applicantStatus -eq 'VisitPurposeRecorded' -and
            $_.Precheck.applicantStatus -eq 'PracticeNetworkPrecheckRecorded' -and
            $_.Reviewed.identityProofed -eq $false -and
            $_.Precheck.coverageVerified -eq $false
        }).Count -eq 3)

    Add-Check 'Database snapshot guard rejects a receipt bound to another applicant review decision' (Test-MutationRejected @"
begin;
update telehealth_prospective_applicants
set status='MemberInsuranceDetailsRecorded',version=version+1
where applicant_id='$selfId';
insert into telehealth_applicant_member_insurance_details(
  details_id,applicant_id,practice_id,facility_id,
  identity_review_decision_id,safety_triage_evaluation_id,visit_purpose_id,
  practice_network_precheck_id,resulting_applicant_version,resulting_applicant_status,
  location_state_code,purpose_category,plan_key,payer_display_name,
  product_display_name,practice_network_status,subscriber_relationship,
  coverage_priority,member_id_last4,group_number_present,group_number_last4,
  details_confirmed,synthetic_data_confirmed,protected_payload,protection_scheme,
  protection_purpose,protection_version,idempotency_key,command_fingerprint)
select gen_random_uuid(),a.applicant_id,a.practice_id,a.facility_id,
  (select decision_id from telehealth_applicant_identity_review_decisions where applicant_id='$spouseId'),
  s.evaluation_id,p.purpose_id,n.precheck_id,a.version,a.status,
  n.location_state_code,n.purpose_category,n.plan_key,n.payer_display_name,
  n.product_display_name,n.practice_network_status,'Self','Primary','BAD1',false,null,
  true,true,repeat('x',64),'ASP.NET_CORE_DATA_PROTECTION',
  'AvenChart.Telehealth.ProspectiveMemberInsuranceDetails.v1',1,
  'sp18-adversarial-provenance',repeat('a',64)
from telehealth_prospective_applicants a
join telehealth_applicant_safety_triage_evaluations s on s.applicant_id=a.applicant_id
join telehealth_applicant_visit_purposes p on p.applicant_id=a.applicant_id
join telehealth_applicant_practice_network_prechecks n on n.applicant_id=a.applicant_id
where a.applicant_id='$selfId';
rollback;
"@)

    $selfVersion = [int]$selfApplicant.Precheck.applicantVersion
    $selfBody = New-MemberBody $selfVersion 'SYN-HM-1001' 'SYN-GROUP-01'
    $invalidSelfBody = $selfBody.Clone()
    $invalidSelfBody.subscriberFirstName = 'ShouldNot'
    $invalidSelfBody.subscriberLastName = 'Appear'
    $invalidSelfBody.subscriberDateOfBirth = '1980-02-03'
    Add-Check 'Unknown access, non-SYN identifiers, invalid conditional subscriber data, and stale versions fail without receipt evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$selfId/member-insurance-details" @{
            'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret);'X-Idempotency-Key'=(New-Key 'sp18-denied')
        } $selfBody) -eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$selfId/member-insurance-details" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$selfApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp18-real')
        } (New-MemberBody $selfVersion 'REAL-MEMBER-1001' 'SYN-GROUP-01')) -eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$selfId/member-insurance-details" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$selfApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp18-self')
        } $invalidSelfBody) -eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$selfId/member-insurance-details" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$selfApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp18-nonself')
        } (New-MemberBody $selfVersion 'SYN-HM-1001' 'SYN-GROUP-01' 'Spouse')) -eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$selfId/member-insurance-details" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$selfApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp18-stale')
        } (New-MemberBody ($selfVersion - 1) 'SYN-HM-1001' 'SYN-GROUP-01')) -eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_member_insurance_details where applicant_id='$selfId';") -eq 0)

    $selfKey = New-Key 'sp18-self'
    $selfResponse = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$selfId/member-insurance-details" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$selfApplicant.Secret
            'X-Idempotency-Key'=$selfKey
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($selfBody | ConvertTo-Json -Depth 8)
    $self = $selfResponse.Content | ConvertFrom-Json
    $selfReplay = Invoke-MemberDetails $selfApplicant $selfBody $selfKey
    $selfJson = $self | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Self-subscriber receipt is private, mask-only, plan-bound, and exact replay is stable' (
        $selfResponse.Headers.'Cache-Control' -match 'no-store' -and
        $self.applicantStatus -eq 'MemberInsuranceDetailsRecorded' -and
        $self.payerDisplayName -eq 'Harbor Mutual' -and
        $self.productDisplayName -eq 'High Deductible' -and
        $self.memberIdMask -eq '••••1001' -and
        $self.groupNumberMask -eq '••••P-01' -and
        $self.subscriberRelationship -eq 'Self' -and
        $self.coveragePriority -eq 'Primary' -and
        $self.protectionScheme -eq 'ASP.NET_CORE_DATA_PROTECTION' -and
        [int]$self.protectionVersion -eq 1 -and
        $self.detailsId -eq $selfReplay.detailsId -and
        $self.applicantVersion -eq $selfReplay.applicantVersion -and
        $selfJson -notmatch 'SYN-HM-1001|SYN-GROUP-01|Member Self|1990-03-14|protectedPayload|protectionPurpose')

    $atRest = (Invoke-Scalar @"
select json_build_object(
  'memberLast4',member_id_last4,
  'groupLast4',group_number_last4,
  'relationship',subscriber_relationship,
  'scheme',protection_scheme,
  'purpose',protection_purpose,
  'payload',protected_payload)
from telehealth_applicant_member_insurance_details where applicant_id='$selfId';
"@) | ConvertFrom-Json
    Add-Check 'At-rest receipt keeps only masks and an opaque purpose-isolated protected payload' (
        $atRest.memberLast4 -eq '1001' -and $atRest.groupLast4 -eq 'P-01' -and
        $atRest.relationship -eq 'Self' -and
        $atRest.scheme -eq 'ASP.NET_CORE_DATA_PROTECTION' -and
        $atRest.purpose -eq 'AvenChart.Telehealth.ProspectiveMemberInsuranceDetails.v1' -and
        ([string]$atRest.payload).Length -ge 64 -and
        [string]$atRest.payload -notmatch 'SYN-HM-1001|SYN-GROUP-01|Member|Self|1990-03-14')

    Add-Check 'Same-mask changed content cannot reuse the key and no second semantic receipt is allowed' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$selfId/member-insurance-details" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$selfApplicant.Secret;'X-Idempotency-Key'=$selfKey
        } (New-MemberBody $selfVersion 'SYN-ALT-1001' 'SYN-GROUP-01')) -eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$selfId/member-insurance-details" @{
            'X-AvenChart-Telehealth-Applicant-Key'=$selfApplicant.Secret;'X-Idempotency-Key'=(New-Key 'sp18-second')
        } (New-MemberBody ([int]$self.applicantVersion) 'SYN-HM-1001' 'SYN-GROUP-01')) -eq 409)

    $spouseBody = New-MemberBody ([int]$spouseApplicant.Precheck.applicantVersion) 'SYN-BV-2002' 'SYN-GROUP-02' 'Spouse'
    $spouseBody.subscriberFirstName = 'Morgan'
    $spouseBody.subscriberLastName = 'Example'
    $spouseBody.subscriberDateOfBirth = '1980-02-03'
    $spouse = Invoke-MemberDetails $spouseApplicant $spouseBody (New-Key 'sp18-spouse')
    $spouseJson = $spouse | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Non-self subscriber identity is conditionally accepted but never returned' (
        $spouse.subscriberRelationship -eq 'Spouse' -and
        $spouse.memberIdMask -eq '••••2002' -and
        $spouse.payerDisplayName -eq 'Blue Valley Health' -and
        $spouseJson -notmatch 'Morgan|Example|1980-02-03|SYN-BV-2002|SYN-GROUP-02')

    $concurrentId = [string]$concurrentApplicant.Created.applicantId
    $concurrentVersion = [int]$concurrentApplicant.Precheck.applicantVersion
    $concurrentSecret = [string]$concurrentApplicant.Secret
    $concurrentBaseUrl = $ApiBaseUrl
    $concurrentStatuses = 1..12 | ForEach-Object -Parallel {
        $headers = @{
            'X-AvenChart-Telehealth-Applicant-Key'=$using:concurrentSecret
            'X-Idempotency-Key'="sp18-concurrent-$($_)-$([Guid]::NewGuid().ToString('N'))"
        }
        try {
            [int](Invoke-WebRequest "$using:concurrentBaseUrl/api/telehealth/v1/applicants/$using:concurrentId/member-insurance-details" `
                -Method Post -Headers $headers -ContentType 'application/json' -TimeoutSec 30 -Body (@{
                    expectedVersion=$using:concurrentVersion;memberId='SYN-PS-3003';groupNumber=$null
                    subscriberRelationship='Self';subscriberFirstName=$null;subscriberLastName=$null
                    subscriberDateOfBirth=$null;detailsConfirmed=$true;syntheticDataConfirmed=$true
                } | ConvertTo-Json -Depth 8)).StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
            else { throw }
        }
    } -ThrottleLimit 12
    Add-Check 'Concurrent first receipts have one winner and one immutable receipt/event' (
        @($concurrentStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($concurrentStatuses | Where-Object { $_ -eq 409 }).Count -eq 11 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_member_insurance_details where applicant_id='$concurrentId';") -eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$concurrentId' and action='prospective-member-insurance-details-recorded';") -eq 1)

    $falseProperties = @(
        'memberMatched','memberEligibilityChecked','memberBenefitsChecked','renderingPhysicianNetworkChecked',
        'coverageVerified','exactNetworkConfirmed','identityProofed','canonicalPatientCreated',
        'chartLinked','portalAccountCreated','prospectiveIntakeCompleted','consentCreated',
        'practiceAccepted','coverageRecordCreated','estimateCreated','financialAcknowledgmentCreated',
        'requestCreated','queueEnabled','appointmentCreated','encounterCreated','careEnabled',
        'prescribingEnabled','billingEnabled','claimCreated','communicationEnabled',
        'integrationEnabled','externalCallPerformed')
    $allFalse = $true
    foreach ($response in @($self,$spouse)) {
        foreach ($property in $falseProperties) {
            if ($response.$property -ne $false) { $allFalse = $false }
        }
    }
    Add-Check 'Every receipt explicitly denies matching, eligibility, exact network, coverage, identity, financial, care, integration, and external consequences' $allFalse
    Add-Check 'Database hard-false flags reject every consequential member-detail state' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_member_insurance_details where
member_matched or member_eligibility_checked or member_benefits_checked or rendering_physician_network_checked or
coverage_verified or exact_network_confirmed or identity_proofed or canonical_patient_created or
chart_linked or portal_account_created or prospective_intake_completed or consent_created or
practice_accepted or coverage_record_created or estimate_created or financial_acknowledgment_created or
request_created or queue_enabled or appointment_created or encounter_created or care_enabled or
prescribing_enabled or billing_enabled or claim_created or communication_enabled or
integration_enabled or external_call_performed;
"@) -eq 0)

    $publicResponse = Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$selfId" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$selfApplicant.Secret} -TimeoutSec 30
    $public = $publicResponse.Content | ConvertFrom-Json
    $publicJson = $public | ConvertTo-Json -Depth 8 -Compress
    Add-Check 'Applicant resume is private and coarse without plan, mask, subscriber, protected payload, safety, staff, or fingerprint evidence' (
        $publicResponse.Headers.'Cache-Control' -match 'no-store' -and
        $public.status -eq 'MemberInsuranceDetailsRecorded' -and
        $public.canonicalPatientCreated -eq $false -and
        $publicJson -notmatch 'planKey|payerDisplay|productDisplay|practiceNetworkStatus|memberId|groupNumber|subscriber|protectedPayload|protectionScheme|commandFingerprint|identityReviewDecisionId|safetyTriageEvaluationId|decidedBy|reviewReason')

    Add-Check 'Member-detail receipt and correlated applicant event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_member_insurance_details set member_id_last4='9999' where details_id='$($self.detailsId)';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$selfId' and action='prospective-member-insurance-details-recorded';"))

    $after = Get-Counts
    $unchanged = @('patients','portalAccounts','insuranceRecords','requests','queueEntries',
        'intakeSnapshots','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    $allUnchanged = $true
    foreach ($name in $unchanged) {
        if ([int]$after.$name -ne [int]$before.$name) { $allUnchanged = $false }
    }
    Add-Check 'Protected member-detail capture creates no canonical insurance, patient, intake, coverage, financial, request, queue, clinical, prescribing, or downstream row' (
        $allUnchanged -and
        [int]$after.applicants -eq ([int]$before.applicants + 3) -and
        [int]$after.reviewDecisions -eq ([int]$before.reviewDecisions + 3) -and
        [int]$after.safetyEvaluations -eq ([int]$before.safetyEvaluations + 3) -and
        [int]$after.visitPurposes -eq ([int]$before.visitPurposes + 3) -and
        [int]$after.networkPrechecks -eq ([int]$before.networkPrechecks + 3) -and
        [int]$after.memberDetails -eq ([int]$before.memberDetails + 3)) @{
            applicantDelta=[int]$after.applicants-[int]$before.applicants
            reviewDecisionDelta=[int]$after.reviewDecisions-[int]$before.reviewDecisions
            safetyEvaluationDelta=[int]$after.safetyEvaluations-[int]$before.safetyEvaluations
            visitPurposeDelta=[int]$after.visitPurposes-[int]$before.visitPurposes
            networkPrecheckDelta=[int]$after.networkPrechecks-[int]$before.networkPrechecks
            memberDetailsDelta=[int]$after.memberDetails-[int]$before.memberDetails
            canonicalAndDownstreamUnchanged=$allUnchanged
        }
}
catch {
    Add-Check 'Telehealth prospective member-insurance details execution' $false @{
        message=$_.Exception.Message
        stack=$_.ScriptStackTrace
    }
}
finally {
    $result = [ordered]@{
        status=$(if($passed){'passed'}else{'failed'})
        generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O')
        decisions=@('TH-DEC-0003','TH-DEC-0007','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019','TH-DEC-0020','TH-DEC-0021')
        dataClassification='synthetic-only-purpose-protected-no-raw-member-or-subscriber-output'
        checks=$checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 10
}
if (-not $passed) { exit 1 }
