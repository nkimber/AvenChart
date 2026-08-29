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
    throw 'Telehealth applicant clinician-reservation proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestQueueAuthorization.ps1') `
    -ApiBaseUrl $ApiBaseUrl `
    -DatabaseName $DatabaseName `
    -VerifyApplicantQueueStatus

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-clinician-reservation.json'
$reservationChecks = [System.Collections.Generic.List[object]]::new()
$reservationPassed = $true
$proofPhysicianAccountCreated = $false

function Add-ReservationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:reservationChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:reservationPassed=$false}
}
function Reservation-Key([string]$Prefix) { "$Prefix-$([Guid]::NewGuid().ToString('N'))" }
function Reservation-Status([string]$Path,[hashtable]$Headers) {
    try {[int](Invoke-WebRequest "$ApiBaseUrl$Path" -Method Post -Headers $Headers -TimeoutSec 40).StatusCode}
    catch {if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
}

try {
    $requestIds=@($queueAuthorizationApplicants|ForEach-Object{[string]$_.SubmissionReady.requestId})
    $allRequestIdsSql=@($requestIds|ForEach-Object{"'$_'::uuid"})-join','
    $selectedQueue=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/queue" -Headers $providerHeaders -TimeoutSec 30
    $selectedCandidateRequestCount=[int](Invoke-Scalar "select count(*) from telehealth_applicant_request_queue_authorizations authorization_record join auth_accounts account on account.staff_id=authorization_record.candidate_staff_id where authorization_record.request_id in ($allRequestIdsSql) and account.username='gold-provider-01';")
    Add-ReservationCheck 'Exact selected physician sees only their current applicant-originated request without protected identifiers' (
        $selectedCandidateRequestCount-eq 1 -and
        @($selectedQueue.requests|Where-Object{$_.requestId-in$requestIds-and$_.applicantOriginated}).Count-eq$selectedCandidateRequestCount -and
        (($selectedQueue|ConvertTo-Json -Depth 8 -Compress)-notmatch'applicantId|patientId|candidateStaffId|providerId|npi|member|policy|group'))

    $existingProofPhysicianCount=[int](Invoke-Scalar "select count(*) from auth_accounts where username='gold-provider-02';")
    if($existingProofPhysicianCount-eq 0) {
        $null=Invoke-Scalar @"
insert into auth_accounts(username,display_name,role,staff_id,active,password_salt,password_hash)
select 'gold-provider-02','Casey Walker','provider',110,true,password_salt,password_hash
from auth_accounts where username='gold-provider-01';
insert into access_user_memberships(user_value,user_name,group_value,group_name,staff_id)
select 'gold-provider-02','Casey Walker','clin','Clinicians',110
where not exists(select 1 from access_user_memberships where user_value='gold-provider-02' and group_value='clin');
insert into auth_principal_facility_grants(username,facility_id,is_default,active,granted_by,updated_by)
values('gold-provider-02',10,true,true,'telehealth-reservation-proof','telehealth-reservation-proof')
on conflict(username,facility_id) do update set active=true,is_default=true,updated_at=now(),updated_by='telehealth-reservation-proof';
insert into auth_principal_purpose_of_use_grants(username,purpose_of_use,active,granted_by,updated_by)
values('gold-provider-02','treatment',true,'telehealth-reservation-proof','telehealth-reservation-proof')
on conflict(username,purpose_of_use) do update set active=true,updated_at=now(),updated_by='telehealth-reservation-proof';
select 'ok';
"@
        $proofPhysicianAccountCreated=$true
    }
    $otherPhysician=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username='gold-provider-02';password='pass'}|ConvertTo-Json) -TimeoutSec 20
    $otherHeaders=@{'X-AvenChart-Session'=$otherPhysician.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='treatment'}
    $otherQueue=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/queue" -Headers $otherHeaders -TimeoutSec 30
    $otherShiftHeaders=$otherHeaders.Clone();$otherShiftHeaders['X-Idempotency-Key']=Reservation-Key 'sp54-other-shift'
    $otherShift=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/shifts" -Method Post -Headers $otherShiftHeaders -TimeoutSec 30
    $otherReserveHeaders=$otherHeaders.Clone();$otherReserveHeaders['X-Idempotency-Key']=Reservation-Key 'sp54-other-reserve'
    Add-ReservationCheck 'A different physician cannot see or reserve candidate-bound applicant requests' (
        @($otherQueue.requests|Where-Object{$_.requestId-in$requestIds}).Count-eq 0 -and
        $otherShift.status-eq'Active' -and
        (Reservation-Status '/api/telehealth/v1/clinician/reservations/reserve-next' $otherReserveHeaders)-eq 204 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations where request_id in ($allRequestIdsSql);")-eq 0)

    $shiftHeaders=$providerHeaders.Clone();$shiftHeaders['X-Idempotency-Key']=Reservation-Key 'sp54-shift'
    $shift=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/shifts" -Method Post -Headers $shiftHeaders -TimeoutSec 30
    $parallelBaseUrl=$ApiBaseUrl;$parallelHeaders=$providerHeaders
    $outcomes=1..$CallerCount|ForEach-Object -Parallel {
        $sourceHeaders=$using:parallelHeaders
        $headers=@{};foreach($entry in $sourceHeaders.GetEnumerator()){$headers[$entry.Key]=$entry.Value}
        $key="sp54-reserve-$($_)-$([Guid]::NewGuid().ToString('N'))";$headers['X-Idempotency-Key']=$key
        try {
            $response=Invoke-WebRequest "$using:parallelBaseUrl/api/telehealth/v1/clinician/reservations/reserve-next" -Method Post -Headers $headers -TimeoutSec 40
            [pscustomobject]@{status=[int]$response.StatusCode;key=$key;body=$response.Content}
        }
        catch {
            $status=if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}
            [pscustomobject]@{status=$status;key=$key;body=$null}
        }
    } -ThrottleLimit $CallerCount
    $winner=@($outcomes|Where-Object{$_.status-eq 200})
    Add-ReservationCheck "$CallerCount concurrent exact-candidate callers produce one reservation winner" (
        $shift.status-eq'Active' -and $winner.Count-eq 1 -and
        @($outcomes|Where-Object{$_.status-eq 409}).Count-eq($CallerCount-1)) $outcomes

    $reservation=$winner[0].body|ConvertFrom-Json
    $reservedApplicant=@($queueAuthorizationApplicants|Where-Object{$_.SubmissionReady.requestId-eq$reservation.requestId})|Select-Object -First 1
    if($null-eq$reservedApplicant){throw 'The winning reservation was not bound to a known applicant request.'}
    $facts=(Invoke-Scalar "select json_build_object(
      'activeReservationCount',(select count(*) from telehealth_reservations where request_id='$($reservation.requestId)'::uuid and status='Active'),
      'requestStatus',(select status from telehealth_requests where request_id='$($reservation.requestId)'::uuid),
      'requestVersion',(select version from telehealth_requests where request_id='$($reservation.requestId)'::uuid),
      'queueStatus',(select status from telehealth_queue_entries where request_id='$($reservation.requestId)'::uuid),
      'appointmentProvider',(select appointment.provider_id from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$($reservation.requestId)'::uuid),
      'candidateStaff',(select candidate_staff_id from telehealth_applicant_request_queue_authorizations where request_id='$($reservation.requestId)'::uuid),
      'reservationStaff',(select clinician_staff_id from telehealth_reservations where request_id='$($reservation.requestId)'::uuid and status='Active'),
      'freshAtReservation',(select reservation.reserved_at<queue_authorization.result_valid_through from telehealth_reservations reservation join telehealth_applicant_request_queue_authorizations queue_authorization on queue_authorization.request_id=reservation.request_id where reservation.request_id='$($reservation.requestId)'::uuid and reservation.status='Active'),
      'eventCount',(select count(*) from telehealth_request_events where request_id='$($reservation.requestId)'::uuid and action='request-reserved' and from_status='Queued' and to_status='Reserved'),
      'encounterCount',(select count(*) from encounters encounter join telehealth_requests request on request.patient_id=encounter.patient_id where request.request_id='$($reservation.requestId)'::uuid)
    )::text;")|ConvertFrom-Json
    Add-ReservationCheck 'Reservation atomically binds request, queue, appointment, event, lease owner, and exact synthetic candidate' (
        $reservation.applicantOriginated -and [int]$reservation.requestVersion-eq 14 -and
        [int]$facts.activeReservationCount-eq 1 -and $facts.requestStatus-eq'Reserved' -and
        [int]$facts.requestVersion-eq 14 -and $facts.queueStatus-eq'Reserved' -and
        [int]$facts.appointmentProvider-eq[int]$facts.reservationStaff -and
        [int]$facts.candidateStaff-eq[int]$facts.reservationStaff -and $facts.freshAtReservation -and
        [int]$facts.eventCount-eq 1 -and [int]$facts.encounterCount-eq 0) $facts

    $replayHeaders=$providerHeaders.Clone();$replayHeaders['X-Idempotency-Key']=$winner[0].key
    $replay=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/reservations/reserve-next" -Method Post -Headers $replayHeaders -TimeoutSec 30
    Add-ReservationCheck 'Reservation replay returns the original applicant-bound lease without another mutation' (
        $replay.reservationId-eq$reservation.reservationId -and $replay.requestId-eq$reservation.requestId -and
        $replay.requestVersion-eq$reservation.requestVersion -and $replay.applicantOriginated -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations where request_id='$($reservation.requestId)'::uuid;")-eq 1)

    $applicantStatus=Get-Applicant-Queue-Status $reservedApplicant
    $statusJson=$applicantStatus|ConvertTo-Json -Depth 8 -Compress
    Add-ReservationCheck 'Applicant sees physician preparation without identity, real-network, position, wait, or care claims' (
        $applicantStatus.requestStatus-eq'Reserved' -and [int]$applicantStatus.requestVersion-eq 14 -and
        $applicantStatus.phase-eq'PhysicianPreparing' -and $applicantStatus.renderingPhysicianAssigned -and
        $applicantStatus.syntheticRenderingCandidateMatched -and -not$applicantStatus.renderingPhysicianIdentityDisclosed -and
        -not$applicantStatus.realRenderingPhysicianNetworkConfirmed -and $null-eq$applicantStatus.approximateRequestsAhead -and
        -not$applicantStatus.exactQueuePositionAssigned -and -not$applicantStatus.waitEstimateAvailable -and
        -not$applicantStatus.coverageVerified -and -not$applicantStatus.consentCreated -and
        -not$applicantStatus.careAuthorized -and -not$applicantStatus.integrationEnabled -and
        $statusJson-notmatch'gold-provider|physicianStaffId|providerId|npi|patientId|applicantId|memberId|policyNumber|groupNumber') `
        $applicantStatus

    $otherQueueAfter=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/queue" -Headers $otherHeaders -TimeoutSec 30
    Add-ReservationCheck 'Unreserved applicant work remains hidden from the unmatched physician after another request is reserved' (
        @($otherQueueAfter.requests|Where-Object{$_.requestId-in$requestIds}).Count-eq 0)

    $null=Invoke-Scalar "update telehealth_reservations set lease_expires_at=now()-interval '1 second' where reservation_id='$($reservation.reservationId)'::uuid and status='Active'; select 'ok';"
    $recoveryHeaders=$providerHeaders.Clone();$recoveryHeaders['X-Idempotency-Key']=Reservation-Key 'sp54-recover'
    $recovered=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/reservations/reserve-next" -Method Post -Headers $recoveryHeaders -TimeoutSec 30
    $recoveryFacts=(Invoke-Scalar "select json_build_object(
      'expiredReservationCount',(select count(*) from telehealth_reservations where reservation_id='$($reservation.reservationId)'::uuid and status='Expired'),
      'activeReservationCount',(select count(*) from telehealth_reservations where request_id='$($reservation.requestId)'::uuid and status='Active'),
      'requestStatus',(select status from telehealth_requests where request_id='$($reservation.requestId)'::uuid),
      'requestVersion',(select version from telehealth_requests where request_id='$($reservation.requestId)'::uuid),
      'queueStatus',(select status from telehealth_queue_entries where request_id='$($reservation.requestId)'::uuid),
      'appointmentProvider',(select appointment.provider_id from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$($reservation.requestId)'::uuid),
      'candidateStaff',(select candidate_staff_id from telehealth_applicant_request_queue_authorizations where request_id='$($reservation.requestId)'::uuid),
      'reservationStaff',(select clinician_staff_id from telehealth_reservations where reservation_id='$($recovered.reservationId)'::uuid and status='Active'),
      'expiryEventCount',(select count(*) from telehealth_request_events where request_id='$($reservation.requestId)'::uuid and action='reservation-expired' and from_status='Reserved' and to_status='Queued'),
      'reservationEventCount',(select count(*) from telehealth_request_events where request_id='$($reservation.requestId)'::uuid and action='request-reserved' and from_status='Queued' and to_status='Reserved')
    )::text;")|ConvertFrom-Json
    Add-ReservationCheck 'Expired applicant lease requeues without evidence loss and re-reservation remains exact-candidate-bound' (
        $recovered.applicantOriginated -and $recovered.requestId-eq$reservation.requestId -and
        $recovered.reservationId-ne$reservation.reservationId -and [int]$recovered.requestVersion-eq 16 -and
        [int]$recoveryFacts.expiredReservationCount-eq 1 -and [int]$recoveryFacts.activeReservationCount-eq 1 -and
        $recoveryFacts.requestStatus-eq'Reserved' -and [int]$recoveryFacts.requestVersion-eq 16 -and
        $recoveryFacts.queueStatus-eq'Reserved' -and
        [int]$recoveryFacts.appointmentProvider-eq[int]$recoveryFacts.candidateStaff -and
        [int]$recoveryFacts.reservationStaff-eq[int]$recoveryFacts.candidateStaff -and
        [int]$recoveryFacts.expiryEventCount-eq 1 -and [int]$recoveryFacts.reservationEventCount-eq 2) $recoveryFacts
}
catch {
    Add-ReservationCheck 'Applicant clinician-reservation proof execution' $false @{message=$_.Exception.Message;stack=$_.ScriptStackTrace}
}
finally {
    if($proofPhysicianAccountCreated) {
        try {
            $null=Invoke-Scalar "delete from auth_sessions where username='gold-provider-02'; delete from access_user_memberships where user_value='gold-provider-02' and group_value='clin'; delete from auth_accounts where username='gold-provider-02'; select 'ok';"
            Add-ReservationCheck 'Synthetic unmatched physician account is removed after the proof' ([int](Invoke-Scalar "select count(*) from auth_accounts where username='gold-provider-02';")-eq 0)
        }
        catch {Add-ReservationCheck 'Synthetic unmatched physician account is removed after the proof' $false $_.Exception.Message}
    }
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');apiBaseUrl=$ApiBaseUrl;database=$DatabaseName;passed=$reservationPassed;checkCount=$reservationChecks.Count;checks=$reservationChecks}
    $report|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$reservationPassed){throw "Telehealth applicant clinician-reservation proof failed. See $resultPath"}
Write-Host "Telehealth applicant clinician-reservation proof passed $($reservationChecks.Count) checks."
