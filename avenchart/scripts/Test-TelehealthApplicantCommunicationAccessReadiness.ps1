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
    throw 'Telehealth applicant communication/access-readiness proof is local-only.'
}

# Reuse the sealed Sprint 26 chain. It leaves one Georgia, one California, and
# one Florida applicant at the confirmed synthetic insurance-handoff gate.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantInsuranceHandoffConfirmation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-communication-access-readiness.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-CommunicationAccessCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-CommunicationAccessBody(
    [int]$Version,
    [string]$Fingerprint,
    [string]$Language,
    [bool]$Interpreter,
    [bool]$Accessibility) {
    @{
        expectedVersion=$Version
        contextSnapshotFingerprint=$Fingerprint
        preferredSpokenLanguage=$Language
        interpreterRequested=$Interpreter
        accessibilitySupportRequested=$Accessibility
        currentLocationConfirmed=$true
        callbackNumberConfirmed=$true
        safeAndPrivateToCommunicateConfirmed=$true
        disconnectionAndEmergencyPlanAcknowledged=$true
        syntheticDataConfirmed=$true
    }
}
function Get-CommunicationAccess([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/communication-access-readiness" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Invoke-CommunicationAccess([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/communication-access-readiness" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 8)
}
function Get-CommunicationAccessCounts {
    (Invoke-Scalar @"
select json_build_object(
  'readinessReceipts',(select count(*) from telehealth_applicant_communication_access_readiness),
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
  'videoSessions',(select count(*) from telehealth_video_sessions),
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
    $before=Get-CommunicationAccessCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/communication-access-readiness" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaContext=$gaWeb.Content|ConvertFrom-Json
    $gaJson=$gaContext|ConvertTo-Json -Depth 12 -Compress
    Add-CommunicationAccessCheck 'Applicant-owned read returns masked, server-bound context and explicit preference-only semantics' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaContext.applicantStatus-eq'SyntheticInsuranceDetailsConfirmed' -and [int]$gaContext.applicantVersion-eq 15 -and
        $gaContext.currentLocationStateCode-eq'GA' -and $gaContext.maskedCallbackPhone-eq$success.Created.maskedPhone -and
        @($gaContext.supportedSpokenLanguages).Count-eq 2 -and
        $gaContext.supportedSpokenLanguages[0]-eq'English' -and $gaContext.supportedSpokenLanguages[1]-eq'Spanish' -and
        $null-eq$gaContext.preferredSpokenLanguage -and $null-eq$gaContext.interpreterRequested -and
        $null-eq$gaContext.accessibilitySupportRequested -and -not$gaContext.readinessRecorded -and
        -not$gaContext.interpreterAssigned -and -not$gaContext.accessibilityAccommodationArranged -and
        -not$gaContext.communicationArrangementCompleted -and -not$gaContext.supportRequestCreated -and
        -not$gaContext.technologyReadinessCompleted -and
        ([string]$gaContext.contextSnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaContext.policyKey-eq'SYNTHETIC_COMMUNICATION_ACCESS_READINESS' -and
        $gaJson-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|safetyEvaluationId|insuranceHandoffConfirmationId|commandFingerprint|4045550199')

    $gaBody=New-CommunicationAccessBody 15 ([string]$gaContext.contextSnapshotFingerprint) 'Spanish' $true $true
    Add-CommunicationAccessCheck 'Anonymous, wrong-key, partial, unsupported-language, stale-version, and stale-context commands fail before persistence' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/communication-access-readiness")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp27-partial')} (Merge-Map $gaBody @{safeAndPrivateToCommunicateConfirmed=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp27-language')} (Merge-Map $gaBody @{preferredSpokenLanguage='French'}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp27-stale')} (Merge-Map $gaBody @{expectedVersion=14}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp27-fingerprint')} (Merge-Map $gaBody @{contextSnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_communication_access_readiness where applicant_id='$gaId';")-eq 0)

    $flPatient=(Invoke-Scalar @"
select json_build_object('canonicalId',p.canonical_id,'pid',p.legacy_pid)
from patients p join telehealth_applicant_synthetic_promotions promotion
  on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id='$flId';
"@)|ConvertFrom-Json
    $temporaryInsuranceId="INS-SP27-$([Guid]::NewGuid().ToString('N'))"
    Invoke-Scalar "insert into insurance_records(id,patient_id,pid,type,provider,plan_name,policy_number) values('$temporaryInsuranceId','$($flPatient.canonicalId)',$($flPatient.pid),'primary','Synthetic conflict fixture','Synthetic','SYN-TEMP');" | Out-Null
    $canonicalCoverageStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "delete from insurance_records where id='$temporaryInsuranceId';" | Out-Null
    Invoke-Scalar "update patients set portal_enabled=true where canonical_id='$($flPatient.canonicalId)';" | Out-Null
    $portalStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "update patients set portal_enabled=false where canonical_id='$($flPatient.canonicalId)';" | Out-Null
    Add-CommunicationAccessCheck 'Canonical coverage or portal enablement fails closed before readiness recording' (
        $canonicalCoverageStatus-eq 409 -and $portalStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_communication_access_readiness where applicant_id='$flId';")-eq 0)

    $gaKey=New-Key 'sp27-ga'
    $gaRecorded=Invoke-CommunicationAccess $success $gaBody $gaKey
    $gaReplay=Invoke-CommunicationAccess $success $gaBody $gaKey
    $gaRecordedJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-CommunicationAccessCheck 'Georgia records one preference-only receipt and exact replay converges' (
        $gaRecorded.applicantStatus-eq'SyntheticCommunicationAccessReadinessRecorded' -and [int]$gaRecorded.applicantVersion-eq 16 -and
        $gaRecorded.readinessRecorded -and $null-ne$gaRecorded.recordedAt -and
        $gaRecorded.preferredSpokenLanguage-eq'Spanish' -and $gaRecorded.interpreterRequested -and
        $gaRecorded.accessibilitySupportRequested -and $gaRecorded.safeAndPrivateToCommunicateConfirmed -and
        -not$gaRecorded.interpreterAssigned -and -not$gaRecorded.accessibilityAccommodationArranged -and
        -not$gaRecorded.communicationArrangementCompleted -and -not$gaRecorded.supportRequestCreated -and
        -not$gaRecorded.technologyReadinessCompleted -and -not$gaRecorded.patientRecordChanged -and
        -not$gaRecorded.intakeCompleted -and -not$gaRecorded.legalConsentEstablished -and
        -not$gaRecorded.practiceAccepted -and -not$gaRecorded.requestCreated -and
        -not$gaRecorded.queueEnabled -and -not$gaRecorded.careEnabled -and
        $gaReplay.recordedAt-eq$gaRecorded.recordedAt -and
        $gaRecordedJson-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|safetyEvaluationId|insuranceHandoffConfirmationId|commandFingerprint|4045550199')
    Add-CommunicationAccessCheck 'Changed idempotency reuse and a second receipt fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{interpreterRequested=$false}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/communication-access-readiness" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp27-second')} (Merge-Map $gaBody @{expectedVersion=16}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_communication_access_readiness where applicant_id='$gaId';")-eq 1)

    $caContext=Get-CommunicationAccess $race
    $caBody=New-CommunicationAccessBody 15 ([string]$caContext.contextSnapshotFingerprint) 'English' $false $true
    $caJsonBody=$caBody|ConvertTo-Json -Depth 8 -Compress
    $caRaceKey=New-Key 'sp27-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/communication-access-readiness" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    Add-CommunicationAccessCheck 'Eight concurrent exact California retries converge on one receipt and one event' (
        @($statuses|Where-Object{$_-eq 200}).Count-eq 8 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_communication_access_readiness where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-communication-access-readiness-recorded';")-eq 1) @{statuses=$statuses}

    $flContext=Get-CommunicationAccess $fl
    $flBody=New-CommunicationAccessBody 15 ([string]$flContext.contextSnapshotFingerprint) 'Spanish' $true $false
    $flRecorded=Invoke-CommunicationAccess $fl $flBody (New-Key 'sp27-fl')
    Add-CommunicationAccessCheck 'Florida follows the same bounded policy without a state-specific service arrangement' (
        $flRecorded.applicantStatus-eq'SyntheticCommunicationAccessReadinessRecorded' -and
        $flRecorded.currentLocationStateCode-eq'FL' -and $flRecorded.preferredSpokenLanguage-eq'Spanish' -and
        $flRecorded.interpreterRequested -and -not$flRecorded.accessibilitySupportRequested -and
        -not$flRecorded.interpreterAssigned -and -not$flRecorded.accessibilityAccommodationArranged -and
        -not$flRecorded.supportRequestCreated -and -not$flRecorded.requestCreated -and -not$flRecorded.careEnabled)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaReload=Get-CommunicationAccess $success
    Add-CommunicationAccessCheck 'Applicant resume and reload expose the recorded receipt without portal or canonical identity' (
        $gaResume.status-eq'SyntheticCommunicationAccessReadinessRecorded' -and $gaResume.canonicalPatientCreated -and
        $gaReload.readinessRecorded -and $gaReload.applicantStatus-eq'SyntheticCommunicationAccessReadinessRecorded' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',r.resulting_applicant_status,'version',r.resulting_applicant_version,
  'policy',r.policy_key,'policyVersion',r.policy_version,'evidenceType',r.evidence_type,
  'state',r.current_location_state_code,'language',r.preferred_spoken_language,
  'location',r.current_location_confirmed,'callback',r.callback_number_confirmed,
  'safePrivate',r.safe_private_communication_confirmed,'disconnect',r.disconnection_emergency_plan_acknowledged,
  'synthetic',r.synthetic_data_confirmed,'interpreterAssigned',r.interpreter_assigned,
  'accommodation',r.accessibility_accommodation_arranged,'arrangement',r.communication_arrangement_completed,
  'support',r.support_request_created,'technology',r.technology_readiness_completed,
  'patientChanged',r.patient_record_changed,'portal',r.portal_access_enabled,'intake',r.intake_completed,
  'consent',r.legal_consent_established,'practice',r.practice_accepted,'financial',r.financial_record_created,
  'request',r.request_created,'queue',r.queue_enabled,'appointment',r.appointment_created,
  'encounter',r.encounter_created,'care',r.care_enabled,'prescribing',r.prescribing_enabled,
  'billing',r.billing_enabled,'claim',r.claim_created,'communication',r.communication_enabled,
  'integration',r.integration_enabled,'externalCall',r.external_call_performed,
  'handoffVersion',handoff.resulting_applicant_version,'registrationVersion',registration.resulting_applicant_version,
  'safety',safety.outcome,'patientPortal',p.portal_enabled,'merged',p.merged_into_patient_id))
from telehealth_applicant_communication_access_readiness r
join telehealth_applicant_insurance_handoff_confirmations handoff
  on handoff.confirmation_id=r.insurance_handoff_confirmation_id
join telehealth_applicant_registration_details_confirmations registration
  on registration.confirmation_id=r.registration_details_confirmation_id
join telehealth_applicant_safety_triage_evaluations safety on safety.evaluation_id=r.safety_evaluation_id
join patients p on p.canonical_id=r.canonical_patient_id
where r.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    Add-CommunicationAccessCheck 'Database provenance binds three receipts to prior gates with required affirmations and every consequence false' (
        @($evidence).Count-eq 3 -and @($evidence|Where-Object{
            $_.status-eq'SyntheticCommunicationAccessReadinessRecorded'-and[int]$_.version-eq 16-and
            $_.policy-eq'SYNTHETIC_COMMUNICATION_ACCESS_READINESS'-and[int]$_.policyVersion-eq 1-and
            $_.evidenceType-eq'PROMOTED_PATIENT_COMMUNICATION_ACCESS_READINESS_RECEIPT'-and
            $_.location-and$_.callback-and$_.safePrivate-and$_.disconnect-and$_.synthetic-and
            -not$_.interpreterAssigned-and-not$_.accommodation-and-not$_.arrangement-and-not$_.support-and
            -not$_.technology-and-not$_.patientChanged-and-not$_.portal-and-not$_.intake-and-not$_.consent-and
            -not$_.practice-and-not$_.financial-and-not$_.request-and-not$_.queue-and-not$_.appointment-and
            -not$_.encounter-and-not$_.care-and-not$_.prescribing-and-not$_.billing-and-not$_.claim-and
            -not$_.communication-and-not$_.integration-and-not$_.externalCall-and
            [int]$_.handoffVersion-eq 15-and[int]$_.registrationVersion-eq 14-and
            $_.safety-eq'TelehealthEligible'-and-not$_.patientPortal-and$null-eq$_.merged
        }).Count-eq 3 -and
        @($evidence|ForEach-Object{$_.state}|Sort-Object)-join',' -eq 'CA,FL,GA')

    $gaReadinessId=Invoke-Scalar "select readiness_id from telehealth_applicant_communication_access_readiness where applicant_id='$gaId';"
    Add-CommunicationAccessCheck 'Readiness evidence and aggregate event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_communication_access_readiness set policy_version=2 where readiness_id='$gaReadinessId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_communication_access_readiness where readiness_id='$gaReadinessId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$gaId' and action='prospective-communication-access-readiness-recorded';"))
    Add-CommunicationAccessCheck 'Receipt schema stores only bounded preferences, acknowledgments, masks, and provenance—not raw identity, insurance, clinical, or external payloads' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_communication_access_readiness'
  and column_name in ('phone','email','legal_first_name','legal_last_name','date_of_birth','postal_code','address',
    'member_id','group_number','subscriber_first_name','subscriber_last_name','subscriber_date_of_birth',
    'complaint','diagnosis','medication','prescription','interpreter_name','accommodation_details',
    'raw_request','raw_response','protected_payload');
"@)-eq 0)

    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    $after=Get-CommunicationAccessCounts
    $downstream=@('patients','portalAccounts','portalIdentityMappings','insuranceRecords','requests','queueEntries','intakeSnapshots','patientConfirmations','patientLocations','coverageSelections','coverageVerifications','videoSessions','appointments','encounters','claims','prescriptions')
    Add-CommunicationAccessCheck 'Three recordings add only three immutable receipts with zero patient-record or downstream delta' (
        [long]$after.readinessReceipts-[long]$before.readinessReceipts-eq 3 -and
        @($downstream|Where-Object{[long]$before.$_-ne[long]$after.$_}).Count-eq 0 -and
        @($patientHashesBefore.Keys|Where-Object{$patientHashesBefore[$_]-ne$patientHashesAfter[$_]}).Count-eq 0) @{before=$before;after=$after}
}
catch { Add-CommunicationAccessCheck 'Applicant communication/access-readiness proof execution' $false $_.Exception.Message }
finally {
    $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decision='TH-DEC-0030';checks=$checks}
    $result|ConvertTo-Json -Depth 14|Set-Content -Encoding utf8 $resultPath
    if(-not$passed){throw "Applicant communication/access-readiness proof failed. See $resultPath"}
    Write-Host "Applicant communication/access-readiness proof passed ($($checks.Count) checks). Artifact: $resultPath"
}
