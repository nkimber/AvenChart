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
    throw 'Telehealth applicant insurance-handoff proof is local-only.'
}

# Reuse the sealed Sprint 25 chain. It leaves one Georgia, one California, and
# one Florida applicant at the confirmed minimum-registration-details gate.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRegistrationDetailsConfirmation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-insurance-handoff-confirmation.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-InsuranceHandoffCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-InsuranceHandoffBody([int]$Version,[string]$Fingerprint) {
    @{
        expectedVersion=$Version
        insuranceSnapshotFingerprint=$Fingerprint
        payerAndProductConfirmed=$true
        maskedMemberDetailsConfirmed=$true
        subscriberRelationshipConfirmed=$true
        evidenceLimitationsAcknowledged=$true
        syntheticDataConfirmed=$true
    }
}
function Get-InsuranceHandoff([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/insurance-handoff" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Invoke-InsuranceHandoff([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/insurance-handoff/confirmation" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 8)
}
function Get-InsuranceHandoffCounts {
    (Invoke-Scalar @"
select json_build_object(
  'confirmations',(select count(*) from telehealth_applicant_insurance_handoff_confirmations),
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
  'prescriptions',(select count(*) from prescriptions));
"@)|ConvertFrom-Json
}

try {
    $gaId=[string]$success.Created.applicantId
    $caId=[string]$race.Created.applicantId
    $flId=[string]$fl.Created.applicantId
    $before=Get-InsuranceHandoffCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/insurance-handoff" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaHandoff=$gaWeb.Content|ConvertFrom-Json
    $gaMember=(Invoke-Scalar @"
select json_build_object('memberLast4',member_id_last4,'groupLast4',group_number_last4)
from telehealth_applicant_member_insurance_details where applicant_id='$gaId';
"@)|ConvertFrom-Json
    $gaJson=$gaHandoff|ConvertTo-Json -Depth 12 -Compress
    Add-InsuranceHandoffCheck 'Applicant-owned read exposes one masked insurance handoff and explicit non-coverage semantics' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaHandoff.applicantStatus-eq'SyntheticMinimumRegistrationDetailsConfirmed' -and [int]$gaHandoff.applicantVersion-eq 14 -and
        $gaHandoff.payerDisplayName-eq'Harbor Mutual' -and $gaHandoff.productDisplayName-eq'High Deductible' -and
        $gaHandoff.memberIdMask-eq("••••"+$gaMember.memberLast4) -and
        $gaHandoff.groupNumberMask-eq("••••"+$gaMember.groupLast4) -and
        $gaHandoff.subscriberRelationship-eq'Self' -and $gaHandoff.coveragePriority-eq'Primary' -and
        $gaHandoff.eligibilityBusinessOutcome-eq'EligibleBenefitsReported' -and $gaHandoff.eligibilityEvidenceCurrent -and
        $gaHandoff.practiceNetworkBusinessOutcome-eq'PracticeInNetworkAcceptingNewPatients' -and $gaHandoff.practiceNetworkEvidenceCurrent -and
        -not$gaHandoff.renderingPhysicianNetworkChecked -and -not$gaHandoff.coverageVerified -and
        -not$gaHandoff.exactNetworkConfirmed -and -not$gaHandoff.canonicalCoverageCreated -and
        ([string]$gaHandoff.insuranceSnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaHandoff.policyKey-eq'SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION' -and
        $gaJson-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|memberInsuranceDetailsId|eligibilityResultId|networkDeterminationId|commandFingerprint|protectedPayload|inquiryTraceToken|responseTraceToken')

    $gaBody=New-InsuranceHandoffBody 14 ([string]$gaHandoff.insuranceSnapshotFingerprint)
    Add-InsuranceHandoffCheck 'Anonymous, wrong-key, partial, stale-version, and stale-snapshot commands fail before persistence' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/insurance-handoff")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/insurance-handoff" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/insurance-handoff/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp26-partial')} (Merge-Map $gaBody @{evidenceLimitationsAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/insurance-handoff/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp26-stale')} (Merge-Map $gaBody @{expectedVersion=13}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/insurance-handoff/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp26-fingerprint')} (Merge-Map $gaBody @{insuranceSnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_insurance_handoff_confirmations where applicant_id='$gaId';")-eq 0)

    $flPatient=(Invoke-Scalar @"
select json_build_object('canonicalId',p.canonical_id,'pid',p.legacy_pid)
from patients p join telehealth_applicant_synthetic_promotions promotion
  on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id='$flId';
"@)|ConvertFrom-Json
    $temporaryInsuranceId="INS-SP26-$([Guid]::NewGuid().ToString('N'))"
    Invoke-Scalar "insert into insurance_records(id,patient_id,pid,type,provider,plan_name,policy_number) values('$temporaryInsuranceId','$($flPatient.canonicalId)',$($flPatient.pid),'primary','Synthetic conflict fixture','Synthetic','SYN-TEMP');" | Out-Null
    $canonicalCoverageStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/insurance-handoff" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "delete from insurance_records where id='$temporaryInsuranceId';" | Out-Null
    Invoke-Scalar "update patients set portal_enabled=true where canonical_id='$($flPatient.canonicalId)';" | Out-Null
    $portalStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/insurance-handoff" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "update patients set portal_enabled=false where canonical_id='$($flPatient.canonicalId)';" | Out-Null
    Add-InsuranceHandoffCheck 'Existing canonical coverage or portal enablement fails closed before handoff confirmation' (
        $canonicalCoverageStatus-eq 409 -and $portalStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_insurance_handoff_confirmations where applicant_id='$flId';")-eq 0)

    $gaKey=New-Key 'sp26-ga'
    $gaRecorded=Invoke-InsuranceHandoff $success $gaBody $gaKey
    $gaReplay=Invoke-InsuranceHandoff $success $gaBody $gaKey
    $gaRecordedJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-InsuranceHandoffCheck 'Georgia confirmation records one no-edit, no-consequence result and exact replay converges' (
        $gaRecorded.applicantStatus-eq'SyntheticInsuranceDetailsConfirmed' -and [int]$gaRecorded.applicantVersion-eq 15 -and
        $gaRecorded.insuranceDetailsConfirmed -and $null-ne$gaRecorded.confirmedAt -and
        -not$gaRecorded.renderingPhysicianNetworkChecked -and -not$gaRecorded.coverageVerified -and
        -not$gaRecorded.exactNetworkConfirmed -and -not$gaRecorded.canonicalCoverageCreated -and
        -not$gaRecorded.patientRecordChanged -and -not$gaRecorded.portalAccessEnabled -and
        -not$gaRecorded.intakeCompleted -and -not$gaRecorded.legalConsentEstablished -and
        -not$gaRecorded.practiceAccepted -and -not$gaRecorded.requestCreated -and
        -not$gaRecorded.queueEnabled -and -not$gaRecorded.careEnabled -and
        $gaReplay.confirmedAt-eq$gaRecorded.confirmedAt -and
        $gaRecordedJson-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|memberInsuranceDetailsId|eligibilityResultId|networkDeterminationId|commandFingerprint')
    Add-InsuranceHandoffCheck 'Changed idempotency reuse and a second confirmation fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/insurance-handoff/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{expectedVersion=15}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/insurance-handoff/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp26-second')} (Merge-Map $gaBody @{expectedVersion=15}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_insurance_handoff_confirmations where applicant_id='$gaId';")-eq 1)

    $caHandoff=Get-InsuranceHandoff $race
    $caBody=New-InsuranceHandoffBody 14 ([string]$caHandoff.insuranceSnapshotFingerprint)
    $caJsonBody=$caBody|ConvertTo-Json -Depth 8 -Compress
    $caRaceKey=New-Key 'sp26-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/insurance-handoff/confirmation" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    Add-InsuranceHandoffCheck 'Eight concurrent exact California retries converge on one confirmation and one aggregate event' (
        @($statuses|Where-Object{$_-eq 200}).Count-eq 8 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_insurance_handoff_confirmations where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-insurance-handoff-confirmed';")-eq 1) @{statuses=$statuses}

    $flHandoff=Get-InsuranceHandoff $fl
    $flBody=New-InsuranceHandoffBody 14 ([string]$flHandoff.insuranceSnapshotFingerprint)
    $flRecorded=Invoke-InsuranceHandoff $fl $flBody (New-Key 'sp26-fl')
    Add-InsuranceHandoffCheck 'Florida follows the same bounded handoff policy without a state-specific coverage or physician-network conclusion' (
        $flRecorded.applicantStatus-eq'SyntheticInsuranceDetailsConfirmed' -and
        -not$flRecorded.renderingPhysicianNetworkChecked -and -not$flRecorded.coverageVerified -and
        -not$flRecorded.exactNetworkConfirmed -and -not$flRecorded.canonicalCoverageCreated -and
        -not$flRecorded.requestCreated -and -not$flRecorded.careEnabled)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaReload=Get-InsuranceHandoff $success
    Add-InsuranceHandoffCheck 'Applicant resume and handoff reload expose confirmed state without portal or canonical patient identity' (
        $gaResume.status-eq'SyntheticInsuranceDetailsConfirmed' -and $gaResume.canonicalPatientCreated -and
        $gaReload.insuranceDetailsConfirmed -and $gaReload.applicantStatus-eq'SyntheticInsuranceDetailsConfirmed' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',c.resulting_applicant_status,'version',c.resulting_applicant_version,
  'policy',c.policy_key,'policyVersion',c.policy_version,'evidenceType',c.evidence_type,
  'payer',c.payer_product_confirmed,'member',c.masked_member_details_confirmed,
  'subscriber',c.subscriber_relationship_confirmed,'limitations',c.evidence_limitations_acknowledged,
  'synthetic',c.synthetic_data_confirmed,'rendering',c.rendering_physician_network_checked,
  'coverage',c.coverage_verified,'exactNetwork',c.exact_network_confirmed,
  'canonicalCoverage',c.canonical_coverage_created,'patientChanged',c.patient_record_changed,
  'portal',c.portal_access_enabled,'intake',c.intake_completed,'consent',c.legal_consent_established,
  'practice',c.practice_accepted,'financial',c.financial_record_created,'request',c.request_created,
  'queue',c.queue_enabled,'appointment',c.appointment_created,'encounter',c.encounter_created,
  'care',c.care_enabled,'prescribing',c.prescribing_enabled,'billing',c.billing_enabled,
  'claim',c.claim_created,'communication',c.communication_enabled,
  'integration',c.integration_enabled,'externalCall',c.external_call_performed,
  'registrationVersion',registration.resulting_applicant_version,
  'eligibility',eligibility.business_outcome,'network',network.business_outcome,
  'patientPortal',p.portal_enabled,'merged',p.merged_into_patient_id))
from telehealth_applicant_insurance_handoff_confirmations c
join telehealth_applicant_registration_details_confirmations registration
  on registration.confirmation_id=c.registration_details_confirmation_id
join telehealth_applicant_eligibility_results eligibility
  on eligibility.eligibility_result_id=c.eligibility_result_id
join telehealth_applicant_practice_network_determinations network
  on network.network_determination_id=c.network_determination_id
join patients p on p.canonical_id=c.canonical_patient_id
where c.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    Add-InsuranceHandoffCheck 'Database provenance binds three confirmations to registration, positive synthetic fixtures, and portal-disabled unmerged shells with every consequential flag false' (
        @($evidence).Count-eq 3 -and
        @($evidence|Where-Object{
            $_.status-eq'SyntheticInsuranceDetailsConfirmed'-and[int]$_.version-eq 15-and
            $_.policy-eq'SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'-and[int]$_.policyVersion-eq 1-and
            $_.evidenceType-eq'PROMOTED_PATIENT_INSURANCE_HANDOFF_NO_EDIT_CONFIRMATION'-and
            $_.payer-and$_.member-and$_.subscriber-and$_.limitations-and$_.synthetic-and
            -not$_.rendering-and-not$_.coverage-and-not$_.exactNetwork-and-not$_.canonicalCoverage-and
            -not$_.patientChanged-and-not$_.portal-and-not$_.intake-and-not$_.consent-and-not$_.practice-and
            -not$_.financial-and-not$_.request-and-not$_.queue-and-not$_.appointment-and-not$_.encounter-and
            -not$_.care-and-not$_.prescribing-and-not$_.billing-and-not$_.claim-and-not$_.communication-and
            -not$_.integration-and-not$_.externalCall-and[int]$_.registrationVersion-eq 14-and
            $_.eligibility-eq'EligibleBenefitsReported'-and$_.network-eq'PracticeInNetworkAcceptingNewPatients'-and
            -not$_.patientPortal-and$null-eq$_.merged
        }).Count-eq 3)

    $gaConfirmationId=Invoke-Scalar "select confirmation_id from telehealth_applicant_insurance_handoff_confirmations where applicant_id='$gaId';"
    Add-InsuranceHandoffCheck 'Insurance handoff evidence and aggregate event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_insurance_handoff_confirmations set policy_version=2 where confirmation_id='$gaConfirmationId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_insurance_handoff_confirmations where confirmation_id='$gaConfirmationId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$gaId' and action='prospective-insurance-handoff-confirmed';"))
    Add-InsuranceHandoffCheck 'Handoff evidence stores only masks and normalized outcomes, never raw member, subscriber, patient, clinical, or external payload values' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_insurance_handoff_confirmations'
  and column_name in ('member_id','group_number','subscriber_first_name','subscriber_last_name','subscriber_date_of_birth',
    'legal_first_name','legal_last_name','date_of_birth','email','phone','postal_code','address','portal_username',
    'password_hash','complaint','diagnosis','medication','prescription','raw_request','raw_response','protected_payload');
"@)-eq 0)

    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    $after=Get-InsuranceHandoffCounts
    $downstream=@('patients','portalAccounts','portalIdentityMappings','insuranceRecords','requests','queueEntries','intakeSnapshots','patientConfirmations','patientLocations','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    Add-InsuranceHandoffCheck 'Three confirmations add only three immutable receipts with zero patient-record or downstream delta' (
        [long]$after.confirmations-[long]$before.confirmations-eq 3 -and
        @($downstream|Where-Object{[long]$before.$_-ne[long]$after.$_}).Count-eq 0 -and
        @($patientHashesBefore.Keys|Where-Object{$patientHashesBefore[$_]-ne$patientHashesAfter[$_]}).Count-eq 0) @{before=$before;after=$after}
}
catch { Add-InsuranceHandoffCheck 'Applicant insurance-handoff proof execution' $false $_.Exception.Message }
finally {
    $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decision='TH-DEC-0029';checks=$checks}
    $result|ConvertTo-Json -Depth 14|Set-Content -Encoding utf8 $resultPath
    if(-not$passed){throw "Applicant insurance-handoff proof failed. See $resultPath"}
    Write-Host "Applicant insurance-handoff proof passed ($($checks.Count) checks). Artifact: $resultPath"
}
