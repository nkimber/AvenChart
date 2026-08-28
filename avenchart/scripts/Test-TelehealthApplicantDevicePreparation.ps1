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
    throw 'Telehealth applicant device-preparation proof is local-only.'
}

# Reuse the sealed Sprint 27 chain. It leaves one Georgia, one California, and
# one Florida applicant at the communication/access-readiness gate.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantCommunicationAccessReadiness.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-device-preparation.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-DevicePreparationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-DevicePreparationBody([int]$Version,[string]$Fingerprint,[string]$NetworkQuality='Good') {
    @{
        expectedVersion=$Version
        preparationSnapshotFingerprint=$Fingerprint
        browserSupported=$true
        cameraAvailable=$true
        microphoneAvailable=$true
        speakerAvailable=$true
        networkQuality=$NetworkQuality
        clientReportedResultAcknowledged=$true
        noReadinessGuaranteeAcknowledged=$true
        recheckBeforeConsultationAcknowledged=$true
    }
}
function Get-DevicePreparation([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/device-preparation" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Invoke-DevicePreparation([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/device-preparation" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 8)
}
function Get-DevicePreparationCounts {
    (Invoke-Scalar @"
select json_build_object(
  'preparations',(select count(*) from telehealth_applicant_device_preparations),
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
    $before=Get-DevicePreparationCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/device-preparation" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaContext=$gaWeb.Content|ConvertFrom-Json
    $gaJson=$gaContext|ConvertTo-Json -Depth 12 -Compress
    Add-DevicePreparationCheck 'Applicant-owned read returns only a coarse preparation policy and no media or identity data' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaContext.applicantStatus-eq'SyntheticCommunicationAccessReadinessRecorded' -and
        [int]$gaContext.applicantVersion-eq 16 -and
        @($gaContext.supportedNetworkQualities).Count-eq 2 -and
        $gaContext.supportedNetworkQualities[0]-eq'Unknown' -and
        $gaContext.supportedNetworkQualities[1]-eq'Good' -and
        -not $gaContext.preparationRecorded -and $null-eq$gaContext.browserSupported -and
        $null-eq$gaContext.cameraAvailable -and $null-eq$gaContext.microphoneAvailable -and
        $null-eq$gaContext.speakerAvailable -and $null-eq$gaContext.networkQuality -and
        -not $gaContext.technologyReady -and -not $gaContext.waitingRoomCreated -and
        -not $gaContext.mediaSessionCreated -and -not $gaContext.communicationStarted -and
        ([string]$gaContext.preparationSnapshotFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaContext.policyKey-eq'SYNTHETIC_APPLICANT_DEVICE_PREPARATION' -and
        $gaJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|deviceId|deviceLabel|userAgent|ipAddress|iceCandidate|sdp|codec|resolution|bandwidthMeasurement|audioPayload|videoPayload|recordingPayload|transcript)"\s*:|TH-PAT-|4045550199')

    $gaBody=New-DevicePreparationBody 16 ([string]$gaContext.preparationSnapshotFingerprint)
    Add-DevicePreparationCheck 'Anonymous, wrong-key, partial, unsupported, limited-network, stale-version, and stale-context commands fail before persistence' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/device-preparation")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp28-partial')} (Merge-Map $gaBody @{cameraAvailable=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp28-ack')} (Merge-Map $gaBody @{noReadinessGuaranteeAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp28-limited')} (Merge-Map $gaBody @{networkQuality='Limited'}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp28-stale')} (Merge-Map $gaBody @{expectedVersion=15}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp28-fingerprint')} (Merge-Map $gaBody @{preparationSnapshotFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_device_preparations where applicant_id='$gaId';")-eq 0)

    $flPatient=(Invoke-Scalar @"
select json_build_object('canonicalId',p.canonical_id,'pid',p.legacy_pid)
from patients p join telehealth_applicant_synthetic_promotions promotion
  on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id='$flId';
"@)|ConvertFrom-Json
    $temporaryInsuranceId="INS-SP28-$([Guid]::NewGuid().ToString('N'))"
    Invoke-Scalar "insert into insurance_records(id,patient_id,pid,type,provider,plan_name,policy_number) values('$temporaryInsuranceId','$($flPatient.canonicalId)',$($flPatient.pid),'primary','Synthetic conflict fixture','Synthetic','SYN-TEMP');" | Out-Null
    $canonicalCoverageStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "delete from insurance_records where id='$temporaryInsuranceId';" | Out-Null
    Invoke-Scalar "update patients set portal_enabled=true where canonical_id='$($flPatient.canonicalId)';" | Out-Null
    $portalStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "update patients set portal_enabled=false where canonical_id='$($flPatient.canonicalId)';" | Out-Null
    Add-DevicePreparationCheck 'Canonical coverage or portal enablement fails closed before preparation recording' (
        $canonicalCoverageStatus-eq 409 -and $portalStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_device_preparations where applicant_id='$flId';")-eq 0)

    $gaKey=New-Key 'sp28-ga'
    $gaRecorded=Invoke-DevicePreparation $success $gaBody $gaKey
    $gaReplay=Invoke-DevicePreparation $success $gaBody $gaKey
    $gaRecordedJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-DevicePreparationCheck 'Georgia records one coarse receipt and exact replay converges without enabling technology or care' (
        $gaRecorded.applicantStatus-eq'SyntheticDevicePreparationRecorded' -and
        [int]$gaRecorded.applicantVersion-eq 17 -and $gaRecorded.preparationRecorded -and
        $null-ne$gaRecorded.recordedAt -and $gaRecorded.browserSupported -and
        $gaRecorded.cameraAvailable -and $gaRecorded.microphoneAvailable -and
        $gaRecorded.speakerAvailable -and $gaRecorded.networkQuality-eq'Good' -and
        $gaRecorded.clientReportedResultAcknowledged -and
        $gaRecorded.noReadinessGuaranteeAcknowledged -and
        $gaRecorded.recheckBeforeConsultationAcknowledged -and
        -not $gaRecorded.technologyReady -and -not $gaRecorded.waitingRoomCreated -and
        -not $gaRecorded.mediaSessionCreated -and -not $gaRecorded.communicationStarted -and
        -not $gaRecorded.supportArrangementCompleted -and -not $gaRecorded.patientRecordChanged -and
        -not $gaRecorded.intakeCompleted -and -not $gaRecorded.legalConsentEstablished -and
        -not $gaRecorded.practiceAccepted -and -not $gaRecorded.requestCreated -and
        -not $gaRecorded.queueEntered -and -not $gaRecorded.careAuthorized -and
        $gaReplay.recordedAt-eq$gaRecorded.recordedAt -and
        $gaRecordedJson-notmatch'"(canonicalPatientId|legacyPid|pubpid|deviceId|deviceLabel|userAgent|ipAddress|iceCandidate|sdp|codec|resolution|bandwidthMeasurement|audioPayload|videoPayload|recordingPayload|transcript)"\s*:|TH-PAT-|4045550199')
    Add-DevicePreparationCheck 'Changed idempotency reuse and a second receipt fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{networkQuality='Unknown'}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/device-preparation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp28-second')} (Merge-Map $gaBody @{expectedVersion=17}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_device_preparations where applicant_id='$gaId';")-eq 1)

    $caContext=Get-DevicePreparation $race
    $caBody=New-DevicePreparationBody 16 ([string]$caContext.preparationSnapshotFingerprint) 'Unknown'
    $caJsonBody=$caBody|ConvertTo-Json -Depth 8 -Compress
    $caRaceKey=New-Key 'sp28-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/device-preparation" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    Add-DevicePreparationCheck 'Eight concurrent exact California retries converge on one receipt and one event' (
        @($statuses|Where-Object{$_-eq 200}).Count-eq 8 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_device_preparations where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-device-preparation-recorded';")-eq 1) @{statuses=$statuses}

    $flContext=Get-DevicePreparation $fl
    $flBody=New-DevicePreparationBody 16 ([string]$flContext.preparationSnapshotFingerprint) 'Good'
    $flRecorded=Invoke-DevicePreparation $fl $flBody (New-Key 'sp28-fl')
    Add-DevicePreparationCheck 'Florida follows the same bounded policy without state-specific certification or service arrangement' (
        $flRecorded.applicantStatus-eq'SyntheticDevicePreparationRecorded' -and
        $flRecorded.networkQuality-eq'Good' -and -not$flRecorded.technologyReady -and
        -not$flRecorded.waitingRoomCreated -and -not$flRecorded.mediaSessionCreated -and
        -not$flRecorded.communicationStarted -and -not$flRecorded.supportArrangementCompleted -and
        -not$flRecorded.requestCreated -and -not$flRecorded.queueEntered -and -not$flRecorded.careAuthorized)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaReload=Get-DevicePreparation $success
    Add-DevicePreparationCheck 'Applicant resume and reload expose the receipt without portal or canonical identity' (
        $gaResume.status-eq'SyntheticDevicePreparationRecorded' -and $gaResume.canonicalPatientCreated -and
        $gaReload.preparationRecorded -and $gaReload.applicantStatus-eq'SyntheticDevicePreparationRecorded' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',d.resulting_applicant_status,'version',d.resulting_applicant_version,
  'policy',d.policy_key,'policyVersion',d.policy_version,'evidenceType',d.evidence_type,
  'network',d.network_quality,'browser',d.browser_supported,'camera',d.camera_available,
  'microphone',d.microphone_available,'speaker',d.speaker_available,
  'clientReported',d.client_reported_result_acknowledged,
  'noGuarantee',d.no_readiness_guarantee_acknowledged,
  'recheck',d.recheck_before_consultation_acknowledged,
  'technology',d.technology_ready,'waitingRoom',d.waiting_room_created,
  'mediaSession',d.media_session_created,'communication',d.communication_started,
  'support',d.support_arrangement_completed,'patientChanged',d.patient_record_changed,
  'portal',d.portal_access_enabled,'intake',d.intake_completed,'consent',d.legal_consent_established,
  'practice',d.practice_accepted,'financial',d.financial_record_created,'request',d.request_created,
  'queue',d.queue_entered,'appointment',d.appointment_created,'encounter',d.encounter_created,
  'care',d.care_authorized,'prescribing',d.prescribing_enabled,'billing',d.billing_enabled,
  'claim',d.claim_created,'integration',d.integration_enabled,'externalCall',d.external_call_performed,
  'readinessVersion',readiness.resulting_applicant_version,'handoffVersion',handoff.resulting_applicant_version,
  'registrationVersion',registration.resulting_applicant_version,'safety',safety.outcome,
  'state',safety.current_location_state_code,'patientPortal',p.portal_enabled,'merged',p.merged_into_patient_id))
from telehealth_applicant_device_preparations d
join telehealth_applicant_communication_access_readiness readiness
  on readiness.readiness_id=d.communication_access_readiness_id
join telehealth_applicant_insurance_handoff_confirmations handoff
  on handoff.confirmation_id=d.insurance_handoff_confirmation_id
join telehealth_applicant_registration_details_confirmations registration
  on registration.confirmation_id=d.registration_details_confirmation_id
join telehealth_applicant_safety_triage_evaluations safety on safety.evaluation_id=d.safety_evaluation_id
join patients p on p.canonical_id=d.canonical_patient_id
where d.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    Add-DevicePreparationCheck 'Database provenance binds three receipts to every prior gate with coarse passes, acknowledgments, and every consequence false' (
        @($evidence).Count-eq 3 -and @($evidence|Where-Object{
            $_.status-eq'SyntheticDevicePreparationRecorded'-and[int]$_.version-eq 17-and
            $_.policy-eq'SYNTHETIC_APPLICANT_DEVICE_PREPARATION'-and[int]$_.policyVersion-eq 1-and
            $_.evidenceType-eq'PROMOTED_PATIENT_DEVICE_PREPARATION_RECEIPT'-and
            $_.network-in @('Unknown','Good')-and$_.browser-and$_.camera-and$_.microphone-and$_.speaker-and
            $_.clientReported-and$_.noGuarantee-and$_.recheck-and-not$_.technology-and-not$_.waitingRoom-and
            -not$_.mediaSession-and-not$_.communication-and-not$_.support-and-not$_.patientChanged-and
            -not$_.portal-and-not$_.intake-and-not$_.consent-and-not$_.practice-and-not$_.financial-and
            -not$_.request-and-not$_.queue-and-not$_.appointment-and-not$_.encounter-and-not$_.care-and
            -not$_.prescribing-and-not$_.billing-and-not$_.claim-and-not$_.integration-and-not$_.externalCall-and
            [int]$_.readinessVersion-eq 16-and[int]$_.handoffVersion-eq 15-and
            [int]$_.registrationVersion-eq 14-and$_.safety-eq'TelehealthEligible'-and
            -not$_.patientPortal-and$null-eq$_.merged
        }).Count-eq 3 -and @($evidence|ForEach-Object{$_.state}|Sort-Object)-join',' -eq 'CA,FL,GA')

    $gaPreparationId=Invoke-Scalar "select preparation_id from telehealth_applicant_device_preparations where applicant_id='$gaId';"
    Add-DevicePreparationCheck 'Device-preparation evidence and aggregate event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_device_preparations set policy_version=2 where preparation_id='$gaPreparationId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_device_preparations where preparation_id='$gaPreparationId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$gaId' and action='prospective-device-preparation-recorded';"))
    Add-DevicePreparationCheck 'Receipt schema excludes media, identifiers, browser details, network diagnostics, clinical content, and external payloads' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_device_preparations'
  and column_name in ('device_id','device_label','group_id','user_agent','ip_address','ice_candidate','sdp','codec',
    'resolution','bandwidth','media','audio','video','recording','transcript','phone','email','legal_first_name',
    'legal_last_name','date_of_birth','postal_code','address','member_id','group_number','complaint','diagnosis',
    'medication','prescription','raw_request','raw_response','protected_payload','notes','free_text');
"@)-eq 0)

    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    $after=Get-DevicePreparationCounts
    $downstream=@('patients','portalAccounts','portalIdentityMappings','insuranceRecords','requests','queueEntries','intakeSnapshots','patientConfirmations','patientLocations','coverageSelections','coverageVerifications','videoSessions','appointments','encounters','claims','prescriptions')
    Add-DevicePreparationCheck 'Three recordings add only three immutable receipts with zero patient-record or downstream delta' (
        [long]$after.preparations-[long]$before.preparations-eq 3 -and
        @($downstream|Where-Object{[long]$before.$_-ne[long]$after.$_}).Count-eq 0 -and
        @($patientHashesBefore.Keys|Where-Object{$patientHashesBefore[$_]-ne$patientHashesAfter[$_]}).Count-eq 0) @{before=$before;after=$after}
}
catch { Add-DevicePreparationCheck 'Applicant device-preparation proof execution' $false $_.Exception.Message }
finally {
    $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decision='TH-DEC-0031';checks=$checks}
    $result|ConvertTo-Json -Depth 14|Set-Content -Encoding utf8 $resultPath
    if(-not$passed){throw "Applicant device-preparation proof failed. See $resultPath"}
    Write-Host "Applicant device-preparation proof passed ($($checks.Count) checks). Artifact: $resultPath"
}
