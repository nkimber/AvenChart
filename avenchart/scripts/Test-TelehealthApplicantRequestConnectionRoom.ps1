# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5001',
    [ValidatePattern('^[a-z][a-z0-9_]{2,62}$')]
    [string]$DatabaseName = 'avenchart',
    [ValidateRange(20,100)]
    [int]$CallerCount = 20
)

$ErrorActionPreference = 'Stop'
if (([Uri]$ApiBaseUrl).Host -notin @('localhost','127.0.0.1','::1')) {
    throw 'Telehealth applicant connection-room proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestClinicianReservation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName -CallerCount $CallerCount

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-connection-room.json'
$connectionChecks = [System.Collections.Generic.List[object]]::new()
$connectionPassed = $true

function Add-ConnectionCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:connectionChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:connectionPassed=$false}
}
function Connection-Path([object]$Applicant,[string]$RequestId) {
    "/api/telehealth/v1/applicants/$($Applicant.Id)/telehealth-request/$RequestId/connection-grants"
}
function Connection-Status([string]$Path,[hashtable]$Headers,[object]$Body) {
    try {
        [int](Invoke-WebRequest "$ApiBaseUrl$Path" -Method Post -Headers $Headers -ContentType 'application/json' -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40).StatusCode
    }
    catch {if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
}

try {
    $activeReservation=$recovered
    $requestId=[string]$activeReservation.requestId
    $path=Connection-Path $reservedApplicant $requestId
    $body=[ordered]@{
        expectedVersion=[int]$activeReservation.requestVersion
        browserSupported=$true
        cameraAvailable=$true
        microphoneAvailable=$true
        speakerAvailable=$true
        networkQuality='good'
        syntheticDataConfirmed=$true
    }
    $commandKey="sp55-connect-$([Guid]::NewGuid().ToString('N'))"
    $ownerHeaders=@{'X-AvenChart-Telehealth-Applicant-Key'=$reservedApplicant.Secret;'X-Idempotency-Key'=$commandKey}
    $foreignApplicant=@($queueAuthorizationApplicants|Where-Object{$_.Id-ne$reservedApplicant.Id})|Select-Object -First 1
    $foreignHeaders=@{'X-AvenChart-Telehealth-Applicant-Key'=$foreignApplicant.Secret;'X-Idempotency-Key'="sp55-foreign-$([Guid]::NewGuid().ToString('N'))"}
    $wrongHeaders=@{'X-AvenChart-Telehealth-Applicant-Key'=('a'*64);'X-Idempotency-Key'="sp55-wrong-$([Guid]::NewGuid().ToString('N'))"}
    $absentStatus=Connection-Status $path @{'X-Idempotency-Key'='sp55-absent-proof'} $body
    $wrongStatus=Connection-Status $path $wrongHeaders $body
    $foreignStatus=Connection-Status (Connection-Path $foreignApplicant $requestId) $foreignHeaders $body
    Add-ConnectionCheck 'Absent, wrong, and foreign applicant credentials cannot prepare the reserved request connection room' (
        $absentStatus-eq 401 -and $wrongStatus-in@(401,404) -and $foreignStatus-in@(401,404,409)) `
        @{absent=$absentStatus;wrong=$wrongStatus;foreign=$foreignStatus}

    $parallelBaseUrl=$ApiBaseUrl;$parallelPath=$path;$parallelBody=($body|ConvertTo-Json -Compress);$parallelKey=$reservedApplicant.Secret;$parallelCommand=$commandKey
    $outcomes=1..$CallerCount|ForEach-Object -Parallel {
        $headers=@{'X-AvenChart-Telehealth-Applicant-Key'=$using:parallelKey;'X-Idempotency-Key'=$using:parallelCommand}
        try {
            $response=Invoke-WebRequest "$using:parallelBaseUrl$using:parallelPath" -Method Post -Headers $headers -ContentType 'application/json' -Body $using:parallelBody -TimeoutSec 60
            [pscustomobject]@{status=[int]$response.StatusCode;body=$response.Content}
        }
        catch {
            $status=if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}
            [pscustomobject]@{status=$status;body=$null}
        }
    } -ThrottleLimit $CallerCount
    $successful=@($outcomes|Where-Object{$_.status-eq 200})
    $grantIds=@($successful|ForEach-Object{($_.body|ConvertFrom-Json).grantId}|Sort-Object -Unique)
    $sessionIds=@($successful|ForEach-Object{($_.body|ConvertFrom-Json).sessionId}|Sort-Object -Unique)
    Add-ConnectionCheck "$CallerCount concurrent unchanged owner commands converge on one connection-room result" (
        $successful.Count-eq$CallerCount -and $grantIds.Count-eq 1 -and $sessionIds.Count-eq 1) `
        @{statuses=@($outcomes|Group-Object status|ForEach-Object{"$($_.Name):$($_.Count)"});grantResultCount=$grantIds.Count;sessionResultCount=$sessionIds.Count}

    $grant=$successful[0].body|ConvertFrom-Json
    $facts=(Invoke-Scalar "select json_build_object(
      'requestStatus',(select status from telehealth_requests where request_id='$requestId'::uuid),
      'requestVersion',(select version from telehealth_requests where request_id='$requestId'::uuid),
      'queueStatus',(select status from telehealth_queue_entries where request_id='$requestId'::uuid),
      'appointmentStatus',(select appointment.status from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$requestId'::uuid),
      'sessionCount',(select count(*) from telehealth_video_sessions where request_id='$requestId'::uuid),
      'sessionValid',(select count(*) from telehealth_video_sessions where request_id='$requestId'::uuid and adapter_mode='NON_PRODUCTION' and status='WaitingRoom' and not recording_enabled and not transcription_enabled and not media_transport_enabled),
      'preflightCount',(select count(*) from telehealth_video_preflights preflight join telehealth_video_sessions session on session.session_id=preflight.session_id where session.request_id='$requestId'::uuid and preflight.participant_role='patient'),
      'grantCount',(select count(*) from telehealth_video_participant_grants grant_record join telehealth_video_sessions session on session.session_id=grant_record.session_id where session.request_id='$requestId'::uuid and grant_record.participant_role='patient'),
      'activeGrantCount',(select count(*) from telehealth_video_participant_grants grant_record join telehealth_video_sessions session on session.session_id=grant_record.session_id where session.request_id='$requestId'::uuid and grant_record.participant_role='patient' and grant_record.status='Issued' and grant_record.expires_at>now()),
      'credentialHashValid',(select bool_and(credential_hash~'^[0-9a-f]{64}$') from telehealth_video_participant_grants grant_record join telehealth_video_sessions session on session.session_id=grant_record.session_id where session.request_id='$requestId'::uuid),
      'subjectHashValid',(select bool_and(participant_subject_hash~'^[0-9a-f]{64}$' and participant_subject_hash<>'$($reservedApplicant.Id)' and participant_subject_hash<>'$($reservedApplicant.Secret)') from telehealth_video_preflights preflight join telehealth_video_sessions session on session.session_id=preflight.session_id where session.request_id='$requestId'::uuid and preflight.participant_role='patient'),
      'requestEventCount',(select count(*) from telehealth_request_events where request_id='$requestId'::uuid and action='connection-room-entered' and from_status='Reserved' and to_status='Connecting'),
      'videoEventCount',(select count(*) from telehealth_video_events event join telehealth_video_sessions session on session.session_id=event.session_id where session.request_id='$requestId'::uuid and event.actor_type='patient'),
      'encounterCount',(select count(*) from encounters encounter join telehealth_requests request on request.patient_id=encounter.patient_id where request.request_id='$requestId'::uuid)
    )::text;")|ConvertFrom-Json
    Add-ConnectionCheck 'Connection transaction atomically creates one capture-disabled waiting room, preflight, hashed grant, transitions, and events' (
        $facts.requestStatus-eq'Connecting' -and [int]$facts.requestVersion-eq([int]$activeReservation.requestVersion+1) -and
        $facts.queueStatus-eq'Reserved' -and $facts.appointmentStatus-eq'@' -and
        [int]$facts.sessionCount-eq 1 -and [int]$facts.sessionValid-eq 1 -and
        [int]$facts.preflightCount-eq 1 -and [int]$facts.grantCount-eq 1 -and [int]$facts.activeGrantCount-eq 1 -and
        $facts.credentialHashValid -and $facts.subjectHashValid -and
        [int]$facts.requestEventCount-eq 1 -and [int]$facts.videoEventCount-eq 1 -and [int]$facts.encounterCount-eq 0) $facts

    $persistedSecretCount=[int](Invoke-Scalar "select (select count(*) from telehealth_video_participant_grants where credential_hash='$($grant.joinCredential)' or participant_subject_hash='$($grant.joinCredential)')+(select count(*) from telehealth_video_preflights where participant_subject_hash='$($grant.joinCredential)');")
    Add-ConnectionCheck 'Plaintext join credential is returned only to the caller and is absent from persisted evidence' (
        -not[string]::IsNullOrWhiteSpace([string]$grant.joinCredential) -and $persistedSecretCount-eq 0 -and
        [string]$grant.participantRole-eq'patient' -and [string]$grant.adapterMode-eq'NON_PRODUCTION') `
        @{credentialReturned=(-not[string]::IsNullOrWhiteSpace([string]$grant.joinCredential));persistedSecretCount=$persistedSecretCount;participantRole=$grant.participantRole;adapterMode=$grant.adapterMode}

    $replay=Invoke-RestMethod "$ApiBaseUrl$path" -Method Post -Headers $ownerHeaders -ContentType 'application/json' -Body ($body|ConvertTo-Json -Compress) -TimeoutSec 40
    $changed=[ordered]@{};foreach($entry in $body.GetEnumerator()){$changed[$entry.Key]=$entry.Value};$changed.networkQuality='limited'
    Add-ConnectionCheck 'Unchanged replay is stable and changed content under the same command key conflicts without new evidence' (
        $replay.grantId-eq$grant.grantId -and $replay.sessionId-eq$grant.sessionId -and
        (Connection-Status $path $ownerHeaders $changed)-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_video_preflights preflight join telehealth_video_sessions session on session.session_id=preflight.session_id where session.request_id='$requestId'::uuid;")-eq 1)

    $status=Get-Applicant-Queue-Status $reservedApplicant
    $statusJson=$status|ConvertTo-Json -Depth 8 -Compress
    Add-ConnectionCheck 'Applicant status proves the private waiting room while media, communication, identity, position, wait, consent, encounter, and care remain absent' (
        $status.requestStatus-eq'Connecting' -and $status.phase-eq'ConnectionRoom' -and
        $status.connectionRoomCreated -and $status.patientWaitingRoomEntered -and
        -not$status.mediaSessionCreated -and -not$status.communicationStarted -and
        $status.renderingPhysicianAssigned -and -not$status.renderingPhysicianIdentityDisclosed -and
        -not$status.realRenderingPhysicianNetworkConfirmed -and -not$status.exactQueuePositionAssigned -and
        -not$status.waitEstimateAvailable -and -not$status.coverageVerified -and -not$status.consentCreated -and
        -not$status.careAuthorized -and -not$status.integrationEnabled -and -not$status.externalCallPerformed -and
        $statusJson-notmatch'"(applicantId|patientId|providerId|physicianId|physicianName|npi|memberId|policyNumber|joinCredential|credentialHash)"\s*:') $status

    Add-ConnectionCheck 'No media, communication, consultation, prescription, claim, integration, or external consequence is created' (
        [int](Invoke-Scalar "select (select count(*) from telehealth_consultation_contexts consultation where consultation.request_id='$requestId'::uuid)+(select count(*) from encounters encounter join telehealth_requests request on request.patient_id=encounter.patient_id where request.request_id='$requestId'::uuid);")-eq 0)
}
catch {
    Add-ConnectionCheck 'Applicant connection-room proof execution' $false @{message=$_.Exception.Message;stack=$_.ScriptStackTrace}
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$connectionPassed;checkCount=$connectionChecks.Count;checks=$connectionChecks}
    $report|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$connectionPassed){throw "Telehealth applicant connection-room proof failed. See $resultPath"}
Write-Host "Telehealth applicant connection-room proof passed $($connectionChecks.Count) checks."
