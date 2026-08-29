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
    throw 'Telehealth applicant request universal-safety proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestLocation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName
$firstApplicants = @(
    [pscustomobject]@{Name='Georgia emergency';Id=$gaId;Secret=$gaSecret;State='GA';Emergency=$true;Severe=$false;HandsOn=$false;Unsure=$false;Outcome='Emergency';Status='EmergencyRedirected';Disposition='EmergencyCareNow';Passed=$false;Complaint=$false;Review=$false;Terminal=$true},
    [pscustomobject]@{Name='California urgent';Id=$caId;Secret=$caSecret;State='CA';Emergency=$false;Severe=$true;HandsOn=$false;Unsure=$false;Outcome='UrgentInPerson';Status='InPersonRecommended';Disposition='PromptInPersonCare';Passed=$false;Complaint=$false;Review=$false;Terminal=$true},
    [pscustomobject]@{Name='Florida hands-on';Id=$flId;Secret=$flSecret;State='FL';Emergency=$false;Severe=$false;HandsOn=$true;Unsure=$false;Outcome='InPersonRequired';Status='InPersonRecommended';Disposition='InPersonCareRequired';Passed=$false;Complaint=$false;Review=$false;Terminal=$true}
)

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestLocation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName
$secondApplicants = @(
    [pscustomobject]@{Name='Georgia review';Id=$gaId;Secret=$gaSecret;State='GA';Emergency=$false;Severe=$false;HandsOn=$false;Unsure=$true;Outcome='ClinicalReview';Status='ClinicalReview';Disposition='ClinicalReviewRequired';Passed=$false;Complaint=$false;Review=$true;Terminal=$false},
    [pscustomobject]@{Name='California universal pass';Id=$caId;Secret=$caSecret;State='CA';Emergency=$false;Severe=$false;HandsOn=$false;Unsure=$false;Outcome='TelehealthEligible';Status='SafetyScreening';Disposition='UniversalSafetyPassed';Passed=$true;Complaint=$true;Review=$false;Terminal=$false}
)
$spare = [pscustomobject]@{Name='Florida contention';Id=$flId;Secret=$flSecret;State='FL';Emergency=$false;Severe=$false;HandsOn=$false;Unsure=$false;Outcome='TelehealthEligible';Status='SafetyScreening';Disposition='UniversalSafetyPassed';Passed=$true;Complaint=$true;Review=$false;Terminal=$false}
$cases = @($firstApplicants + $secondApplicants)

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-universal-safety.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true
$expiresAtToRestore = $null
$expiresApplicantToRestore = $null
$patientPhoneToRestore = $null
$patientIdToRestore = $null

function Add-RequestSafetyCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:passed=$false}
}
function Request-SafetyPath([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/safety"
}
function Invoke-RequestSafety([object]$Case,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl$(Request-SafetyPath $Case.Id)" -Method Post `
        -Headers (Applicant-Headers $Case.Secret $Key) -ContentType 'application/json' `
        -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40
}
function Get-RequestSafety([object]$Case) {
    Invoke-RestMethod "$ApiBaseUrl$(Request-SafetyPath $Case.Id)" `
        -Headers (Applicant-Headers $Case.Secret) -TimeoutSec 30
}
function Get-RequestSafetyStatus(
    [string]$Method,[object]$Case,[string]$Secret,[hashtable]$Body=$null,[string]$Key='') {
    try {
        $parameters=@{Uri="$ApiBaseUrl$(Request-SafetyPath $Case.Id)";Method=$Method;Headers=(Applicant-Headers $Secret $Key);TimeoutSec=30}
        if($null-ne$Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Compress}
        $null=Invoke-WebRequest @parameters
        200
    }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}
function New-RequestSafetyBody([object]$Case,[object]$Ready) {
    @{
        expectedRequestVersion=2
        contextSnapshotFingerprint=[string]$Ready.contextSnapshotFingerprint
        currentLocationStateCode=[string]$Case.State
        currentLocationConfirmed=$true
        callbackNumberConfirmed=$true
        syntheticDataConfirmed=$true
        hasEmergencyWarning=[bool]$Case.Emergency
        severeOrWorsening=[bool]$Case.Severe
        requiresHandsOnExam=[bool]$Case.HandsOn
        unsure=[bool]$Case.Unsure
    }
}
function Test-RequestSafetyMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Get-RequestSafetySourceFingerprint([string]$ApplicantIdsSql) {
    Invoke-Scalar @"
select md5(concat_ws('|',
  coalesce((select jsonb_agg(to_jsonb(a) order by a.applicant_id)::text
            from telehealth_prospective_applicants a where a.applicant_id in ($ApplicantIdsSql)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(c) order by c.creation_id)::text
            from telehealth_applicant_request_creations c where c.applicant_id in ($ApplicantIdsSql)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(x) order by x.confirmation_id)::text
            from telehealth_applicant_request_location_confirmations x where x.applicant_id in ($ApplicantIdsSql)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(x) order by x.evaluation_id)::text
            from telehealth_applicant_safety_triage_evaluations x where x.applicant_id in ($ApplicantIdsSql)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(x) order by x.purpose_id)::text
            from telehealth_applicant_visit_purposes x where x.applicant_id in ($ApplicantIdsSql)),'[]')));
"@
}

try {
    $allCases = @($cases + $spare)
    $readyById = @{}
    foreach($case in $allCases){$readyById[$case.Id]=Get-RequestSafety $case}
    Add-RequestSafetyCheck 'Georgia, California, and Florida location-confirmed requests receive private masked safety-ready projections' (
        @($allCases|Where-Object{
            $ready=$readyById[$_.Id]
            $ready.applicantStatus-eq'SyntheticRequestCreated' -and $ready.applicantVersion-eq 26 -and
            $ready.requestStatus-eq'LocationConfirmed' -and $ready.requestVersion-eq 2 -and
            $ready.policyKey-eq'SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT' -and
            $ready.protocolKey-eq'synthetic-universal-safety' -and $ready.assessmentReady -and
            -not$ready.assessmentCreated -and $null-eq$ready.outcome -and
            $ready.maskedCallbackPhone-match'^\*{3}-\*{3}-[0-9]{4}$' -and
            -not$ready.patientCareQueueEntered -and -not$ready.clinicianQueueEntered -and
            -not$ready.doctorSearchStarted -and -not$ready.appointmentCreated
        }).Count-eq 6 -and
        @($allCases.State|Sort-Object -Unique)-join',' -eq 'CA,FL,GA')

    $spareReady=$readyById[$spare.Id]
    $spareBody=New-RequestSafetyBody $spare $spareReady
    $missing=$spareBody.Clone();$missing.hasEmergencyWarning=$null
    $changedState=$spareBody.Clone();$changedState.currentLocationStateCode='GA'
    $stale=$spareBody.Clone();$stale.expectedRequestVersion=3
    $badSnapshot=$spareBody.Clone();$badSnapshot.contextSnapshotFingerprint=('0'*64)
    Add-RequestSafetyCheck 'Missing or foreign access, missing answer, changed state, stale version, and snapshot drift fail before evidence' (
        (Get-RequestSafetyStatus 'GET' $spare '')-eq 401 -and
        (Get-RequestSafetyStatus 'GET' $spare (New-Secret))-eq 404 -and
        (Get-RequestSafetyStatus 'POST' $spare $spare.Secret $missing (New-Key 'sp42-missing'))-eq 400 -and
        (Get-RequestSafetyStatus 'POST' $spare $spare.Secret $changedState (New-Key 'sp42-state'))-eq 409 -and
        (Get-RequestSafetyStatus 'POST' $spare $spare.Secret $stale (New-Key 'sp42-stale'))-eq 409 -and
        (Get-RequestSafetyStatus 'POST' $spare $spare.Secret $badSnapshot (New-Key 'sp42-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_universal_safety_assessments where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $expiresApplicantToRestore=$spare.Id
    $expiresAtToRestore=Invoke-Scalar "select expires_at::text from telehealth_prospective_applicants where applicant_id='$($spare.Id)'::uuid;"
    $null=Invoke-Scalar "update telehealth_prospective_applicants set expires_at=now()-interval '1 minute' where applicant_id='$($spare.Id)'::uuid; select 'expired';"
    $expiredStatus=Get-RequestSafetyStatus 'POST' $spare $spare.Secret $spareBody (New-Key 'sp42-expired')
    $null=Invoke-Scalar "update telehealth_prospective_applicants set expires_at='$expiresAtToRestore'::timestamptz where applicant_id='$($spare.Id)'::uuid; select 'restored';"
    $expiresAtToRestore=$null
    $patientIdToRestore=Invoke-Scalar "select canonical_patient_id from telehealth_applicant_synthetic_promotions where applicant_id='$($spare.Id)'::uuid;"
    $patientPhoneToRestore=Invoke-Scalar "select phone from patients where canonical_id='$patientIdToRestore';"
    $escapedPatientId=$patientIdToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set phone='4045550000',phone_cell='4045550000',phone_home='4045550000' where canonical_id='$escapedPatientId'; select 'drifted';"
    $driftStatus=Get-RequestSafetyStatus 'POST' $spare $spare.Secret $spareBody (New-Key 'sp42-drift')
    $escapedPhone=$patientPhoneToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set phone='$escapedPhone',phone_cell='$escapedPhone',phone_home='$escapedPhone' where canonical_id='$escapedPatientId'; select 'restored';"
    $patientPhoneToRestore=$null
    Add-RequestSafetyCheck 'Database-clock expiry and copied patient callback drift fail closed and restore without evidence' (
        $expiredStatus-eq 410 -and $driftStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_universal_safety_assessments where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $idsSql=@($allCases|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    $sourceBefore=Get-RequestSafetySourceFingerprint $idsSql
    $resultsById=@{}
    $keysById=@{}
    foreach($case in $cases){
        $body=New-RequestSafetyBody $case $readyById[$case.Id]
        $key=New-Key 'sp42-outcome'
        $keysById[$case.Id]=$key
        $resultsById[$case.Id]=Invoke-RequestSafety $case $body $key
    }
    Add-RequestSafetyCheck 'All five deterministic outcomes map to their exact bounded state and public disposition' (
        @($cases|Where-Object{
            $result=$resultsById[$_.Id]
            $result.outcome-eq$_.Outcome -and $result.requestStatus-eq$_.Status -and
            $result.requestVersion-eq 3 -and $result.publicDisposition-eq$_.Disposition -and
            $result.universalSafetyPassed-eq$_.Passed -and
            $result.complaintSpecificTriageRequired-eq$_.Complaint -and
            $result.clinicalReviewRequired-eq$_.Review -and
            $result.terminalForTelehealth-eq$_.Terminal -and $result.assessmentCreated -and
            -not$result.assessmentReady -and -not$result.complaintSpecificTriageCreated -and
            -not$result.clinicalReviewCreated -and -not$result.patientContacted -and
            -not$result.patientCareQueueEntered -and -not$result.clinicianQueueEntered -and
            -not$result.doctorSearchStarted -and -not$result.queuePositionAssigned -and
            -not$result.appointmentCreated -and -not$result.encounterCreated -and
            -not$result.consentCreated -and -not$result.careAuthorized -and
            -not$result.prescribingEnabled -and -not$result.billingEnabled -and
            -not$result.claimCreated -and -not$result.integrationEnabled -and
            -not$result.externalCallPerformed
        }).Count-eq 5) ($cases|ForEach-Object{$resultsById[$_.Id]})

    $first=$cases[0]
    $firstBody=New-RequestSafetyBody $first $readyById[$first.Id]
    $firstReplay=Invoke-RequestSafety $first $firstBody $keysById[$first.Id]
    $changed=$firstBody.Clone();$changed.unsure=$true
    $durable=Get-RequestSafety $first
    $historicalLocation=Get-RequestLocation $first.Id $first.Secret
    Add-RequestSafetyCheck 'Exact replay and durable GET are stable, location receipt remains historical, and changed or second commands fail closed' (
        ($firstReplay|ConvertTo-Json -Depth 12 -Compress)-eq($resultsById[$first.Id]|ConvertTo-Json -Depth 12 -Compress) -and
        ($durable|ConvertTo-Json -Depth 12 -Compress)-eq($resultsById[$first.Id]|ConvertTo-Json -Depth 12 -Compress) -and
        $historicalLocation.requestStatus-eq'LocationConfirmed' -and $historicalLocation.requestVersion-eq 2 -and
        $historicalLocation.locationConfirmed -and
        (Get-RequestSafetyStatus 'POST' $first $first.Secret $changed $keysById[$first.Id])-eq 409 -and
        (Get-RequestSafetyStatus 'POST' $first $first.Secret $firstBody (New-Key 'sp42-second'))-eq 409)

    $spareJson=$spareBody|ConvertTo-Json -Compress
    $raceKeyPrefix='sp42-race'
    $raceResults=1..8|ForEach-Object -Parallel {
        $key="$using:raceKeyPrefix-$($_)-$([Guid]::NewGuid().ToString('N'))"
        $headers=@{'X-AvenChart-Telehealth-Applicant-Key'=$using:spare.Secret;'X-Idempotency-Key'=$key}
        try {
            $response=Invoke-RestMethod "$using:ApiBaseUrl/api/telehealth/v1/applicants/$($using:spare.Id)/telehealth-request/safety" `
                -Method Post -Headers $headers -ContentType 'application/json' -Body $using:spareJson -TimeoutSec 40
            [pscustomobject]@{key=$key;status=200;response=$response}
        }
        catch {$status=if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw};[pscustomobject]@{key=$key;status=$status;response=$null}}
    } -ThrottleLimit 8
    Add-RequestSafetyCheck 'Eight concurrent Florida assessments produce exactly one safety receipt, generic assessment, event, and version transition' (
        @($raceResults|Where-Object{$_.status-eq 200}).Count-eq 1 -and
        @($raceResults|Where-Object{$_.status-notin@(200,409)}).Count-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_universal_safety_assessments where applicant_id='$($spare.Id)'::uuid;")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_triage_assessments t join telehealth_requests r on r.request_id=t.request_id where r.source_applicant_id='$($spare.Id)'::uuid;")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where source_applicant_id='$($spare.Id)'::uuid and status='SafetyScreening' and version=3;")-eq 1) $raceResults

    Add-RequestSafetyCheck 'Six immutable receipts preserve exact protocol, source context, answer flags, outcomes, and false downstream evidence' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_request_universal_safety_assessments
where applicant_id in ($idsSql) and applicant_version=26 and source_request_version=2
  and resulting_request_version=3 and current_location_confirmed and callback_number_confirmed
  and synthetic_data_confirmed and protocol_key='synthetic-universal-safety' and protocol_version=1
  and policy_key='SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT' and policy_version=1
  and evidence_type='APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT'
  and not complaint_specific_triage_created and not clinical_review_created
  and not patient_contacted and not patient_care_queue_entered and not clinician_queue_entered
  and not doctor_search_started and not queue_position_assigned and not appointment_created
  and not encounter_created and not consent_created and not care_authorized
  and not prescribing_enabled and not billing_enabled and not claim_created
  and not integration_enabled and not external_call_performed;
"@)-eq 6 -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_request_events e join telehealth_requests r on r.request_id=e.request_id
where r.source_applicant_id in ($idsSql) and e.aggregate_version=3
  and e.action='applicant-universal-safety-evaluated' and e.from_status='LocationConfirmed'
  and e.actor_type='applicant';
"@)-eq 6 -and
        [int](Invoke-Scalar "select count(*) from telehealth_protocol_versions where protocol_key='synthetic-universal-safety' and protocol_version=1 and is_synthetic;")-eq 1)

    $firstReceiptId=Invoke-Scalar "select receipt_id from telehealth_applicant_request_universal_safety_assessments where applicant_id='$($first.Id)'::uuid;"
    $firstAssessmentId=Invoke-Scalar "select assessment_id from telehealth_applicant_request_universal_safety_assessments where applicant_id='$($first.Id)'::uuid;"
    Add-RequestSafetyCheck 'Safety receipts and generic assessments are append-only while every prior source remains byte-stable' (
        (Test-RequestSafetyMutationRejected "update telehealth_applicant_request_universal_safety_assessments set doctor_search_started=true where receipt_id='$firstReceiptId'::uuid;") -and
        (Test-RequestSafetyMutationRejected "delete from telehealth_applicant_request_universal_safety_assessments where receipt_id='$firstReceiptId'::uuid;") -and
        (Test-RequestSafetyMutationRejected "update telehealth_triage_assessments set outcome='ClinicalReview' where assessment_id='$firstAssessmentId'::uuid;") -and
        $sourceBefore-eq(Get-RequestSafetySourceFingerprint $idsSql)) @{before=$sourceBefore;after=(Get-RequestSafetySourceFingerprint $idsSql)}

    Add-RequestSafetyCheck 'Every request stops before contact, care queues, doctor search, appointment, encounter, consent, care, prescribing, financial, integration, or external work' (
        [int](Invoke-Scalar "select count(*) from telehealth_requests where source_applicant_id in ($idsSql) and version=3 and ready_at is null and appointment_id is null;")-eq 6 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries q join telehealth_requests r on r.request_id=q.request_id where r.source_applicant_id in ($idsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations x join telehealth_requests r on r.request_id=x.request_id where r.source_applicant_id in ($idsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_video_sessions x join telehealth_requests r on r.request_id=x.request_id where r.source_applicant_id in ($idsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_consultation_contexts x join telehealth_requests r on r.request_id=x.request_id where r.source_applicant_id in ($idsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from appointments x join telehealth_requests r on r.appointment_id=x.id where r.source_applicant_id in ($idsSql);")-eq 0)
}
catch {
    $passed=$false
    Add-RequestSafetyCheck 'Applicant request universal-safety proof execution' $false "$($_.Exception.Message) | $($_.ScriptStackTrace)"
}
finally {
    if($null-ne$expiresAtToRestore -and $null-ne$expiresApplicantToRestore){
        try{$null=Invoke-Scalar "update telehealth_prospective_applicants set expires_at='$expiresAtToRestore'::timestamptz where applicant_id='$expiresApplicantToRestore'::uuid; select 'restored';"}
        catch{Add-RequestSafetyCheck 'Request-safety proof restores applicant expiry' $false $_.Exception.Message}
    }
    if($null-ne$patientPhoneToRestore -and $null-ne$patientIdToRestore){
        try{
            $escapedPatientId=$patientIdToRestore.Replace("'","''")
            $escapedPhone=$patientPhoneToRestore.Replace("'","''")
            $null=Invoke-Scalar "update patients set phone='$escapedPhone',phone_cell='$escapedPhone',phone_home='$escapedPhone' where canonical_id='$escapedPatientId'; select 'restored';"
        }
        catch{Add-RequestSafetyCheck 'Request-safety proof restores patient callback fixture' $false $_.Exception.Message}
    }
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0045'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant request universal-safety proof failed. See $resultPath"}
Write-Host "Applicant request universal-safety proof passed ($($checks.Count) checks). Artifact: $resultPath"
