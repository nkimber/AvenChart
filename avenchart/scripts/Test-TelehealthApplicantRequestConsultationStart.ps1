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
    throw 'Telehealth applicant consultation-start proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestConnectionRoom.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName -CallerCount $CallerCount

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-consultation-start.json'
$consultationChecks = [System.Collections.Generic.List[object]]::new()
$consultationPassed = $true

function Add-ConsultationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:consultationChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:consultationPassed=$false}
}
function Consultation-Status([string]$Path,[hashtable]$Headers,[object]$Body) {
    try {
        [int](Invoke-WebRequest "$ApiBaseUrl$Path" -Method Post -Headers $Headers -ContentType 'application/json' -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40).StatusCode
    }
    catch {if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
}

try {
    $activeReservation=$recovered
    $requestId=[string]$activeReservation.requestId
    $reservationId=[string]$activeReservation.reservationId
    $physicianConnectionHeaders=$providerHeaders.Clone()
    $physicianConnectionHeaders['X-Idempotency-Key']="sp56-physician-connect-$([Guid]::NewGuid().ToString('N'))"
    $preflight=[ordered]@{
        expectedVersion=[int]$grant.requestVersion
        browserSupported=$true
        cameraAvailable=$true
        microphoneAvailable=$true
        speakerAvailable=$true
        networkQuality='good'
        syntheticDataConfirmed=$true
    }
    $physicianGrant=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/reservations/$reservationId/connection-grants" `
        -Method Post -Headers $physicianConnectionHeaders -ContentType 'application/json' `
        -Body ($preflight|ConvertTo-Json -Compress) -TimeoutSec 40
    Add-ConsultationCheck 'The exact reservation-owning physician receives a separate short-lived grant for the applicant waiting room' (
        $physicianGrant.sessionId-eq$grant.sessionId -and $physicianGrant.participantRole-eq'physician' -and
        $physicianGrant.grantId-ne$grant.grantId -and $physicianGrant.joinCredential-ne$grant.joinCredential -and
        $physicianGrant.requestStatus-eq'Connecting' -and [int]$physicianGrant.requestVersion-eq[int]$grant.requestVersion -and
        $physicianGrant.adapterMode-eq'NON_PRODUCTION') @{
            sessionId=$physicianGrant.sessionId
            grantId=$physicianGrant.grantId
            requestId=$physicianGrant.requestId
            requestVersion=$physicianGrant.requestVersion
            requestStatus=$physicianGrant.requestStatus
            participantRole=$physicianGrant.participantRole
            adapterMode=$physicianGrant.adapterMode
            credentialReturned=(-not[string]::IsNullOrWhiteSpace([string]$physicianGrant.joinCredential))
            credentialLength=([string]$physicianGrant.joinCredential).Length
            expiresAt=$physicianGrant.expiresAt
            mediaTransportEnabled=$physicianGrant.mediaTransportEnabled
            recordingEnabled=$physicianGrant.recordingEnabled
            transcriptionEnabled=$physicianGrant.transcriptionEnabled
        }

    $state=[string](Invoke-Scalar "select state_code from telehealth_patient_locations where request_id='$requestId'::uuid order by attested_at desc,location_id desc limit 1;")
    $startBody=[ordered]@{
        expectedVersion=[int]$physicianGrant.requestVersion
        patientLocationState=$state
        patientIdentityDiscussed=$true
        callbackConfirmed=$true
        privacyConfirmed=$true
        consentDiscussed=$true
        noConcerningSymptomChange=$true
        emergencyPlanConfirmed=$true
        communicationSufficient=$true
        syntheticDataConfirmed=$true
    }
    $downstreamBefore=(Invoke-Scalar "select json_build_object('prescriptions',(select count(*) from prescriptions),'claims',(select count(*) from claims),'billing',(select count(*) from billing),'outbox',(select count(*) from integration_outbox))::text;")|ConvertFrom-Json
    $incomplete=[ordered]@{};foreach($entry in $startBody.GetEnumerator()){$incomplete[$entry.Key]=$entry.Value};$incomplete.communicationSufficient=$false
    $incompleteHeaders=$providerHeaders.Clone();$incompleteHeaders['X-Idempotency-Key']="sp56-incomplete-$([Guid]::NewGuid().ToString('N'))"
    Add-ConsultationCheck 'An incomplete clinician checklist cannot start the applicant consultation lifecycle' (
        (Consultation-Status "/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start" $incompleteHeaders $incomplete)-eq 400)

    $parallelBaseUrl=$ApiBaseUrl
    $parallelPath="/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start"
    $parallelBody=$startBody|ConvertTo-Json -Compress
    $parallelHeaders=$providerHeaders
    $outcomes=1..$CallerCount|ForEach-Object -Parallel {
        $sourceHeaders=$using:parallelHeaders
        $headers=@{};foreach($entry in $sourceHeaders.GetEnumerator()){$headers[$entry.Key]=$entry.Value}
        $key="sp56-start-$($_)-$([Guid]::NewGuid().ToString('N'))";$headers['X-Idempotency-Key']=$key
        try {
            $response=Invoke-WebRequest "$using:parallelBaseUrl$using:parallelPath" -Method Post -Headers $headers -ContentType 'application/json' -Body $using:parallelBody -TimeoutSec 60
            [pscustomobject]@{status=[int]$response.StatusCode;key=$key;body=$response.Content}
        }
        catch {
            $status=if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}
            [pscustomobject]@{status=$status;key=$key;body=$null}
        }
    } -ThrottleLimit $CallerCount
    $winners=@($outcomes|Where-Object{$_.status-eq 200})
    $consultationIds=@($winners|ForEach-Object{($_.body|ConvertFrom-Json).consultationId}|Sort-Object -Unique)
    Add-ConsultationCheck "$CallerCount concurrent applicant consultation-start commands produce one atomic winner" (
        $winners.Count-eq 1 -and $consultationIds.Count-eq 1 -and
        @($outcomes|Where-Object{$_.status-in@(404,409)}).Count-eq($CallerCount-1)) `
        @{statuses=@($outcomes|Group-Object status|ForEach-Object{"$($_.Name):$($_.Count)"});consultationCount=$consultationIds.Count}

    $consultation=$winners[0].body|ConvertFrom-Json
    $replayHeaders=$providerHeaders.Clone();$replayHeaders['X-Idempotency-Key']=$winners[0].key
    $replay=Invoke-RestMethod "$ApiBaseUrl$parallelPath" -Method Post -Headers $replayHeaders -ContentType 'application/json' -Body ($startBody|ConvertTo-Json -Compress) -TimeoutSec 40
    Add-ConsultationCheck 'The winning consultation command replays without duplicating the encounter or lifecycle evidence' (
        $replay.consultationId-eq$consultation.consultationId -and
        [int](Invoke-Scalar "select count(*) from telehealth_consultation_contexts where request_id='$requestId'::uuid;")-eq 1)

    $facts=(Invoke-Scalar "select json_build_object(
      'requestStatus',(select status from telehealth_requests where request_id='$requestId'::uuid),
      'requestVersion',(select version from telehealth_requests where request_id='$requestId'::uuid),
      'appointmentStatus',(select appointment.status from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$requestId'::uuid),
      'queueStatus',(select status from telehealth_queue_entries where request_id='$requestId'::uuid),
      'reservationStatus',(select status from telehealth_reservations where reservation_id='$reservationId'::uuid),
      'shiftStatus',(select shift.status from telehealth_clinician_shifts shift join telehealth_reservations reservation on reservation.shift_id=shift.shift_id where reservation.reservation_id='$reservationId'::uuid),
      'sessionStatus',(select status from telehealth_video_sessions where request_id='$requestId'::uuid),
      'issuedGrantCount',(select count(*) from telehealth_video_participant_grants participant join telehealth_video_sessions session on session.session_id=participant.session_id where session.request_id='$requestId'::uuid and participant.status='Issued'),
      'consultationCount',(select count(*) from telehealth_consultation_contexts where request_id='$requestId'::uuid and status='Started' and modality='SYNTHETIC_VIDEO' and legal_effect=false),
      'encounterCount',(select count(*) from encounters encounter join telehealth_consultation_contexts context on context.encounter_id=encounter.encounter where context.request_id='$requestId'::uuid),
      'encounterOwnerMatch',(select encounter.provider_id=context.physician_staff_id and encounter.patient_id=request.patient_id and encounter.source_appointment_id=context.appointment_id from telehealth_consultation_contexts context join encounters encounter on encounter.encounter=context.encounter_id join telehealth_requests request on request.request_id=context.request_id where context.request_id='$requestId'::uuid),
      'startEventCount',(select count(*) from telehealth_request_events where request_id='$requestId'::uuid and action='consultation-started' and from_status='Connecting' and to_status='InConsultation'),
      'prescriptionsTotal',(select count(*) from prescriptions),
      'claimsTotal',(select count(*) from claims),
      'billingTotal',(select count(*) from billing),
      'outboxTotal',(select count(*) from integration_outbox)
    )::text;")|ConvertFrom-Json
    Add-ConsultationCheck 'Consultation start atomically owns the synthetic encounter and closes queue, lease, and grant state without prescribing or claims' (
        $facts.requestStatus-eq'InConsultation' -and [int]$facts.requestVersion-eq([int]$physicianGrant.requestVersion+1) -and
        $facts.appointmentStatus-eq'>' -and $facts.queueStatus-eq'Removed' -and $facts.reservationStatus-eq'Released' -and
        $facts.shiftStatus-eq'Busy' -and $facts.sessionStatus-eq'Ended' -and [int]$facts.issuedGrantCount-eq 0 -and
        [int]$facts.consultationCount-eq 1 -and [int]$facts.encounterCount-eq 1 -and $facts.encounterOwnerMatch -and
        [int]$facts.startEventCount-eq 1 -and [int]$facts.prescriptionsTotal-eq[int]$downstreamBefore.prescriptions -and
        [int]$facts.claimsTotal-eq[int]$downstreamBefore.claims -and [int]$facts.billingTotal-eq[int]$downstreamBefore.billing -and
        [int]$facts.outboxTotal-eq[int]$downstreamBefore.outbox) $facts

    $workspaceHeaders=$providerHeaders.Clone()
    $workspace=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/consultations/$($consultation.consultationId)/workspace" -Headers $workspaceHeaders -TimeoutSec 40
    Add-ConsultationCheck 'The owning physician receives only the bounded applicant chart projection and an empty unsigned draft' (
        $workspace.consultationStatus-eq'InConsultation' -and $workspace.readOnly -and
        $workspace.documentationEnabled -and -not$workspace.prescribingEnabled -and -not$workspace.claimsEnabled -and
        -not$workspace.completionEnabled -and [int]$workspace.documentation.version-eq 0 -and
        -not[string]::IsNullOrWhiteSpace([string]$workspace.patient.displayName) -and
        $workspace.visit.patientLocationState-eq$state -and
        (($workspace|ConvertTo-Json -Depth 10 -Compress)-notmatch'patientId|encounterId|appointmentId|memberId|policyNumber|joinCredential')) $workspace

    $status=Get-Applicant-Queue-Status $reservedApplicant
    $statusJson=$status|ConvertTo-Json -Depth 8 -Compress
    Add-ConsultationCheck 'Applicant polling advances to consultation without exposing physician identity, chart data, credentials, or a real coverage claim' (
        $status.requestStatus-eq'InConsultation' -and $status.phase-eq'Consultation' -and
        -not$status.coverageVerified -and -not$status.consentCreated -and -not$status.integrationEnabled -and -not$status.externalCallPerformed -and
        $statusJson-notmatch'gold-provider|physicianStaffId|providerId|npi|patientId|encounterId|joinCredential|documentation|memberId|policyNumber') $status
}
catch {
    Add-ConsultationCheck 'Applicant consultation-start proof execution' $false @{message=$_.Exception.Message;stack=$_.ScriptStackTrace}
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$consultationPassed;checkCount=$consultationChecks.Count;checks=$consultationChecks}
    $report|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$consultationPassed){throw "Telehealth applicant consultation-start proof failed. See $resultPath"}
Write-Host "Telehealth applicant consultation-start proof passed $($consultationChecks.Count) checks."
