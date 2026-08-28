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
    throw 'Telehealth applicant request-location proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestCreation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-location.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true
$expiresAtToRestore = $null
$expiresApplicantToRestore = $null
$patientPhoneToRestore = $null
$patientIdToRestore = $null

function Add-RequestLocationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:passed=$false}
}
function Request-LocationPath([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/location"
}
function Invoke-RequestLocation([string]$ApplicantId,[string]$Secret,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl$(Request-LocationPath $ApplicantId)" -Method Post `
        -Headers (Applicant-Headers $Secret $Key) -ContentType 'application/json' `
        -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40
}
function Get-RequestLocation([string]$ApplicantId,[string]$Secret) {
    Invoke-RestMethod "$ApiBaseUrl$(Request-LocationPath $ApplicantId)" `
        -Headers (Applicant-Headers $Secret) -TimeoutSec 30
}
function Get-RequestLocationStatus(
    [string]$Method,[string]$ApplicantId,[string]$Secret,[hashtable]$Body=$null,[string]$Key='') {
    try {
        $parameters=@{Uri="$ApiBaseUrl$(Request-LocationPath $ApplicantId)";Method=$Method;Headers=(Applicant-Headers $Secret $Key);TimeoutSec=30}
        if($null-ne$Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Compress}
        $null=Invoke-WebRequest @parameters
        200
    }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}
function Test-RequestLocationMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function New-RequestLocationBody([object]$Ready) {
    @{
        expectedRequestVersion=1
        contextSnapshotFingerprint=[string]$Ready.contextSnapshotFingerprint
        currentLocationStateCode=[string]$Ready.currentLocationStateCode
        currentLocationConfirmed=$true
        callbackNumberConfirmed=$true
        changedLocationRequiresRestartAcknowledged=$true
        urgentOrWorseningSymptomsRequireImmediateActionAcknowledged=$true
    }
}
function Get-RequestLocationSourceFingerprint {
    Invoke-Scalar @"
select md5(concat_ws('|',
  coalesce((select jsonb_agg(to_jsonb(a) order by a.applicant_id)::text
            from telehealth_prospective_applicants a
            where a.applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(c) order by c.creation_id)::text
            from telehealth_applicant_request_creations c
            where c.applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(x) order by x.readiness_id)::text
            from telehealth_applicant_communication_access_readiness x
            where x.applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(p) order by p.canonical_id)::text
            from patients p join telehealth_applicant_synthetic_promotions promotion
              on promotion.canonical_patient_id=p.canonical_id
            where promotion.applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)),'[]')));
"@
}

try {
    $gaSecret=[string]$success.Secret
    $caSecret=[string]$race.Secret
    $flSecret=[string]$fl.Secret
    $gaWeb=Invoke-WebRequest "$ApiBaseUrl$(Request-LocationPath $gaId)" -Headers (Applicant-Headers $gaSecret) -TimeoutSec 30
    $gaReady=$gaWeb.Content|ConvertFrom-Json
    $caReady=Get-RequestLocation $caId $caSecret
    $flReady=Get-RequestLocation $flId $flSecret
    $sourceBefore=Get-RequestLocationSourceFingerprint
    Add-RequestLocationCheck 'Georgia, California, and Florida Drafts receive private masked ready projections with no downstream capability' (
        $gaWeb.StatusCode-eq 200 -and [string]$gaWeb.Headers['Cache-Control']-match'no-store' -and
        [string]$gaWeb.Headers['Vary']-match'X-AvenChart-Telehealth-Applicant-Key' -and
        @($gaReady,$caReady,$flReady|Where-Object{
          $_.applicantStatus-eq'SyntheticRequestCreated' -and $_.requestStatus-eq'Draft' -and
          $_.requestVersion-eq 1 -and $_.policyKey-eq'SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION' -and
          $_.policyVersion-eq 1 -and $_.confirmationReady -and -not$_.locationConfirmed -and
          $_.maskedCallbackPhone-match'^\*{3}-\*{3}-[0-9]{4}$' -and
          -not$_.triageAssessmentCreated -and -not$_.patientCareQueueEntered -and
          -not$_.clinicianQueueEntered -and -not$_.doctorSearchStarted
        }).Count-eq 3 -and
        @($gaReady.currentLocationStateCode,$caReady.currentLocationStateCode,$flReady.currentLocationStateCode|Sort-Object)-join',' -eq 'CA,FL,GA')

    $gaBody=New-RequestLocationBody $gaReady
    $caBody=New-RequestLocationBody $caReady
    $flBody=New-RequestLocationBody $flReady
    $missing=$flBody.Clone();$missing.callbackNumberConfirmed=$false
    $changedState=$flBody.Clone();$changedState.currentLocationStateCode='GA'
    $stale=$flBody.Clone();$stale.expectedRequestVersion=2
    $badSnapshot=$flBody.Clone();$badSnapshot.contextSnapshotFingerprint=('0'*64)
    Add-RequestLocationCheck 'Missing or foreign access, incomplete confirmation, changed state, stale version, and snapshot drift fail before evidence' (
        (Get-RequestLocationStatus 'GET' $flId '')-eq 401 -and
        (Get-RequestLocationStatus 'GET' $flId (New-Secret))-eq 404 -and
        (Get-RequestLocationStatus 'POST' $flId $flSecret $missing (New-Key 'sp41-missing'))-eq 400 -and
        (Get-RequestLocationStatus 'POST' $flId $flSecret $changedState (New-Key 'sp41-state'))-eq 409 -and
        (Get-RequestLocationStatus 'POST' $flId $flSecret $stale (New-Key 'sp41-stale'))-eq 409 -and
        (Get-RequestLocationStatus 'POST' $flId $flSecret $badSnapshot (New-Key 'sp41-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_location_confirmations where applicant_id='$flId'::uuid;")-eq 0)

    $expiresApplicantToRestore=$flId
    $expiresAtToRestore=Invoke-Scalar "select expires_at::text from telehealth_prospective_applicants where applicant_id='$flId'::uuid;"
    $null=Invoke-Scalar "update telehealth_prospective_applicants set expires_at=now()-interval '1 minute' where applicant_id='$flId'::uuid; select 'expired';"
    $expiredStatus=Get-RequestLocationStatus 'POST' $flId $flSecret $flBody (New-Key 'sp41-expired')
    $null=Invoke-Scalar "update telehealth_prospective_applicants set expires_at='$expiresAtToRestore'::timestamptz where applicant_id='$flId'::uuid; select 'restored';"
    $expiresAtToRestore=$null
    $patientIdToRestore=Invoke-Scalar "select canonical_patient_id from telehealth_applicant_synthetic_promotions where applicant_id='$flId'::uuid;"
    $patientPhoneToRestore=Invoke-Scalar "select phone from patients where canonical_id='$patientIdToRestore';"
    $escapedPatientId=$patientIdToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set phone='4045550000',phone_cell='4045550000',phone_home='4045550000' where canonical_id='$escapedPatientId'; select 'drifted';"
    $driftStatus=Get-RequestLocationStatus 'POST' $flId $flSecret $flBody (New-Key 'sp41-drift')
    $escapedPhone=$patientPhoneToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set phone='$escapedPhone',phone_cell='$escapedPhone',phone_home='$escapedPhone' where canonical_id='$escapedPatientId'; select 'restored';"
    $patientPhoneToRestore=$null
    Add-RequestLocationCheck 'Database-clock expiry and copied patient callback drift fail closed and restore without evidence' (
        $expiredStatus-eq 410 -and $driftStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_location_confirmations where applicant_id='$flId'::uuid;")-eq 0)

    $gaJson=$gaBody|ConvertTo-Json -Compress
    $raceKeyPrefix='sp41-race'
    $raceResults=1..8|ForEach-Object -Parallel {
        $key="$using:raceKeyPrefix-$($_)-$([Guid]::NewGuid().ToString('N'))"
        $headers=@{'X-AvenChart-Telehealth-Applicant-Key'=$using:gaSecret;'X-Idempotency-Key'=$key}
        try {
            $status=[int](Invoke-WebRequest "$using:ApiBaseUrl/api/telehealth/v1/applicants/$using:gaId/telehealth-request/location" `
                -Method Post -Headers $headers -ContentType 'application/json' -Body $using:gaJson -TimeoutSec 40).StatusCode
        }
        catch {$status=if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
        [pscustomobject]@{key=$key;status=$status}
    } -ThrottleLimit 8
    $gaWinningKey=[string](@($raceResults|Where-Object{$_.status-eq 200})[0].key)
    Add-RequestLocationCheck 'Eight concurrent Georgia confirmations produce exactly one location, receipt, and request version transition' (
        @($raceResults|Where-Object{$_.status-eq 200}).Count-eq 1 -and
        @($raceResults|Where-Object{$_.status-notin@(200,409)}).Count-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_location_confirmations where applicant_id='$gaId'::uuid;")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_patient_locations l join telehealth_requests r on r.request_id=l.request_id where r.source_applicant_id='$gaId'::uuid;")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where source_applicant_id='$gaId'::uuid and status='LocationConfirmed' and version=2;")-eq 1) $raceResults

    $caKey=New-Key 'sp41-ca'
    $caConfirmed=Invoke-RequestLocation $caId $caSecret $caBody $caKey
    Add-RequestLocationCheck 'California advances only the request to LocationConfirmed version 2 with location confirmed and all downstream flags false' (
        $caConfirmed.applicantVersion-eq 26 -and $caConfirmed.applicantStatus-eq'SyntheticRequestCreated' -and
        $caConfirmed.requestStatus-eq'LocationConfirmed' -and $caConfirmed.requestVersion-eq 2 -and
        $caConfirmed.locationConfirmed -and -not$caConfirmed.confirmationReady -and
        -not$caConfirmed.triageAssessmentCreated -and -not$caConfirmed.clinicalReviewCreated -and
        -not$caConfirmed.patientContacted -and -not$caConfirmed.patientCareQueueEntered -and
        -not$caConfirmed.clinicianQueueEntered -and -not$caConfirmed.doctorSearchStarted -and
        -not$caConfirmed.queuePositionAssigned -and -not$caConfirmed.appointmentCreated -and
        -not$caConfirmed.encounterCreated -and -not$caConfirmed.consentCreated -and
        -not$caConfirmed.careAuthorized -and -not$caConfirmed.prescribingEnabled -and
        -not$caConfirmed.billingEnabled -and -not$caConfirmed.claimCreated -and
        -not$caConfirmed.integrationEnabled -and -not$caConfirmed.externalCallPerformed)

    $caReplay=Invoke-RequestLocation $caId $caSecret $caBody $caKey
    $caChanged=$caBody.Clone();$caChanged.currentLocationStateCode='GA'
    $gaReplay=Invoke-RequestLocation $gaId $gaSecret $gaBody $gaWinningKey
    Add-RequestLocationCheck 'Exact replay and durable GET are stable while changed-content reuse and a second command fail closed' (
        ($caReplay|ConvertTo-Json -Depth 10 -Compress)-eq($caConfirmed|ConvertTo-Json -Depth 10 -Compress) -and
        ((Get-RequestLocation $caId $caSecret)|ConvertTo-Json -Depth 10 -Compress)-eq($caConfirmed|ConvertTo-Json -Depth 10 -Compress) -and
        $gaReplay.locationConfirmed -and $gaReplay.requestVersion-eq 2 -and
        (Get-RequestLocationStatus 'POST' $caId $caSecret $caChanged $caKey)-eq 409 -and
        (Get-RequestLocationStatus 'POST' $caId $caSecret $caBody (New-Key 'sp41-second'))-eq 409)

    $flConfirmed=Invoke-RequestLocation $flId $flSecret $flBody (New-Key 'sp41-fl')
    Add-RequestLocationCheck 'Florida confirmation succeeds after safe denial recovery without changing applicant state' (
        $flConfirmed.currentLocationStateCode-eq'FL' -and $flConfirmed.requestStatus-eq'LocationConfirmed' -and
        $flConfirmed.requestVersion-eq 2 -and $flConfirmed.locationConfirmed -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id='$flId'::uuid and status='SyntheticRequestCreated' and version=26;")-eq 1)

    Add-RequestLocationCheck 'Three receipts, locations, and applicant-authored request events preserve exact policy and false-consequence evidence' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_request_location_confirmations
where applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)
  and applicant_version=26 and source_request_version=1 and resulting_request_version=2
  and resulting_request_status='LocationConfirmed' and current_location_confirmed
  and callback_number_confirmed and changed_location_requires_restart_acknowledged
  and urgent_or_worsening_action_acknowledged
  and policy_key='SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION' and policy_version=1
  and location_confirmed and not triage_assessment_created and not clinical_review_created
  and not patient_contacted and not patient_care_queue_entered and not clinician_queue_entered
  and not doctor_search_started and not queue_position_assigned and not appointment_created
  and not encounter_created and not consent_created and not care_authorized
  and not prescribing_enabled and not billing_enabled and not claim_created
  and not integration_enabled and not external_call_performed;
"@)-eq 3 -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_request_events e join telehealth_requests r on r.request_id=e.request_id
where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)
  and e.aggregate_version=2 and e.action='location-confirmed' and e.from_status='Draft'
  and e.to_status='LocationConfirmed' and e.actor_type='applicant';
"@)-eq 3)

    Add-RequestLocationCheck 'Location confirmations are append-only and every prior applicant, creation, readiness, and patient source remains byte-stable' (
        (Test-RequestLocationMutationRejected "update telehealth_applicant_request_location_confirmations set doctor_search_started=true where applicant_id='$gaId'::uuid;") -and
        (Test-RequestLocationMutationRejected "delete from telehealth_applicant_request_location_confirmations where applicant_id='$gaId'::uuid;") -and
        (Test-RequestLocationMutationRejected "update telehealth_patient_locations set state_code='FL' where request_id='$($gaReady.requestId)'::uuid;") -and
        $sourceBefore-eq(Get-RequestLocationSourceFingerprint)) @{before=$sourceBefore;after=(Get-RequestLocationSourceFingerprint)}

    Add-RequestLocationCheck 'All three requests stop before triage, clinical review, contact, queue, appointment, encounter, consent, care, prescribing, financial, integration, or external work' (
        [int](Invoke-Scalar "select count(*) from telehealth_requests where source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid) and status='LocationConfirmed' and version=2 and triage_outcome is null and ready_at is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_triage_assessments t join telehealth_requests r on r.request_id=t.request_id where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries q join telehealth_requests r on r.request_id=q.request_id where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations x join telehealth_requests r on r.request_id=x.request_id where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid);")-eq 0)
}
catch {
    $passed=$false
    Add-RequestLocationCheck 'Applicant request-location proof execution' $false "$($_.Exception.Message) | $($_.ScriptStackTrace)"
}
finally {
    if($null-ne$expiresAtToRestore -and $null-ne$expiresApplicantToRestore){
        try{$null=Invoke-Scalar "update telehealth_prospective_applicants set expires_at='$expiresAtToRestore'::timestamptz where applicant_id='$expiresApplicantToRestore'::uuid; select 'restored';"}
        catch{Add-RequestLocationCheck 'Request-location proof restores applicant expiry' $false $_.Exception.Message}
    }
    if($null-ne$patientPhoneToRestore -and $null-ne$patientIdToRestore){
        try{
            $escapedPatientId=$patientIdToRestore.Replace("'","''")
            $escapedPhone=$patientPhoneToRestore.Replace("'","''")
            $null=Invoke-Scalar "update patients set phone='$escapedPhone',phone_cell='$escapedPhone',phone_home='$escapedPhone' where canonical_id='$escapedPatientId'; select 'restored';"
        }
        catch{Add-RequestLocationCheck 'Request-location proof restores patient callback fixture' $false $_.Exception.Message}
    }
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0044'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant request-location proof failed. See $resultPath"}
Write-Host "Applicant request-location proof passed ($($checks.Count) checks). Artifact: $resultPath"
