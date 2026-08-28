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
    throw 'Telehealth applicant request-creation proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantPracticeReviewAuthorization.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-creation.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true
$patientEmailToRestore = $null
$patientIdToRestore = $null

function Add-RequestCreationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function Request-CreationPath([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request"
}
function Applicant-Headers([string]$Secret,[string]$Key='') {
    $headers=@{'X-AvenChart-Telehealth-Applicant-Key'=$Secret}
    if($Key){$headers['X-Idempotency-Key']=$Key}
    $headers
}
function Invoke-RequestCreation([string]$ApplicantId,[string]$Secret,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl$(Request-CreationPath $ApplicantId)" -Method Post `
        -Headers (Applicant-Headers $Secret $Key) -ContentType 'application/json' `
        -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40
}
function Get-RequestCreation([string]$ApplicantId,[string]$Secret) {
    Invoke-RestMethod "$ApiBaseUrl$(Request-CreationPath $ApplicantId)" `
        -Headers (Applicant-Headers $Secret) -TimeoutSec 30
}
function Get-RequestCreationStatus(
    [string]$Method,[string]$ApplicantId,[string]$Secret,[hashtable]$Body=$null,[string]$Key='') {
    try {
        $parameters=@{
            Uri="$ApiBaseUrl$(Request-CreationPath $ApplicantId)"
            Method=$Method
            Headers=(Applicant-Headers $Secret $Key)
            TimeoutSec=30
        }
        if($null-ne$Body){$parameters.ContentType='application/json';$parameters.Body=$Body|ConvertTo-Json -Compress}
        $null=Invoke-WebRequest @parameters
        200
    }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}
function Test-RequestCreationMutationRejected([string]$Sql) {
    try { $null=Invoke-Scalar $Sql; $false } catch { $true }
}
function Get-RequestCreationSourceFingerprint {
    Invoke-Scalar @"
select md5(concat_ws('|',
  coalesce((select jsonb_agg(to_jsonb(a) order by a.authorization_id)::text
            from telehealth_practice_review_authorizations a
            where a.applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(c) order by c.case_id)::text
            from telehealth_prospective_practice_review_cases c
            where c.applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(s) order by s.submission_id)::text
            from telehealth_applicant_practice_review_submissions s
            where s.applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)),'[]'),
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
    $gaBody=@{
        expectedApplicantVersion=25
        authorizationPolicyVersion=1
        requestCreationConfirmed=$true
        noQueueOrCareAcknowledged=$true
        urgentOrWorseningSymptomsRequireImmediateActionAcknowledged=$true
    }
    $caBody=$gaBody.Clone();$flBody=$gaBody.Clone()
    $sourceBefore=Get-RequestCreationSourceFingerprint

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl$(Request-CreationPath $gaId)" `
        -Headers (Applicant-Headers $gaSecret) -TimeoutSec 30
    $gaReady=$gaWeb.Content|ConvertFrom-Json
    Add-RequestCreationCheck 'Authorized Georgia applicant receives a private minimized ready projection' (
        $gaWeb.StatusCode-eq 200 -and
        [string]$gaWeb.Headers['Cache-Control']-match'no-store' -and
        [string]$gaWeb.Headers['Vary']-match'X-AvenChart-Telehealth-Applicant-Key' -and
        $gaReady.applicantStatus-eq'SyntheticPracticeReviewAuthorized' -and
        $gaReady.policyKey-eq'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION' -and
        $gaReady.policyVersion-eq 1 -and $gaReady.authorizationPolicyVersion-eq 1 -and
        $gaReady.requestCreationReady -and -not$gaReady.requestCreated -and
        $gaReady.complaintCategory-eq'migraine' -and -not$gaReady.requestId -and
        -not$gaReady.patientCareQueueEntered -and -not$gaReady.clinicianQueueEntered -and
        -not$gaReady.doctorSearchStarted -and -not$gaReady.queuePositionAssigned)

    $missing=$gaBody.Clone();$missing.noQueueOrCareAcknowledged=$false
    $wrongPolicy=$gaBody.Clone();$wrongPolicy.authorizationPolicyVersion=2
    $stale=$gaBody.Clone();$stale.expectedApplicantVersion=24
    $foreignSecret=New-Secret
    Add-RequestCreationCheck 'Missing or foreign access, incomplete confirmation, wrong policy, and stale version fail before evidence' (
        (Get-RequestCreationStatus 'GET' $gaId '')-eq 401 -and
        (Get-RequestCreationStatus 'GET' $gaId $foreignSecret)-eq 404 -and
        (Get-RequestCreationStatus 'POST' $gaId $gaSecret $missing (New-Key 'sp40-missing'))-eq 400 -and
        (Get-RequestCreationStatus 'POST' $gaId $gaSecret $wrongPolicy (New-Key 'sp40-policy'))-eq 400 -and
        (Get-RequestCreationStatus 'POST' $gaId $gaSecret $stale (New-Key 'sp40-stale'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_creations where applicant_id='$gaId'::uuid;")-eq 0)

    $gaKey=New-Key 'sp40-ga'
    $gaCreated=Invoke-RequestCreation $gaId $gaSecret $gaBody $gaKey
    Add-RequestCreationCheck 'Applicant creates the exact Georgia Draft request and no downstream capability' (
        $gaCreated.applicantId-eq$gaId -and $gaCreated.applicantVersion-eq 26 -and
        $gaCreated.applicantStatus-eq'SyntheticRequestCreated' -and
        $gaCreated.requestCreated -and $gaCreated.telehealthRequestCreated -and
        $gaCreated.requestStatus-eq'Draft' -and $gaCreated.requestVersion-eq 1 -and
        $gaCreated.complaintCategory-eq'migraine' -and $gaCreated.requestId -and
        -not$gaCreated.patientContacted -and -not$gaCreated.patientCareQueueEntered -and
        -not$gaCreated.clinicianQueueEntered -and -not$gaCreated.doctorSearchStarted -and
        -not$gaCreated.queuePositionAssigned -and -not$gaCreated.appointmentCreated -and
        -not$gaCreated.encounterCreated -and -not$gaCreated.consentCreated -and
        -not$gaCreated.careAuthorized -and -not$gaCreated.prescribingEnabled -and
        -not$gaCreated.billingEnabled -and -not$gaCreated.claimCreated -and
        -not$gaCreated.integrationEnabled -and -not$gaCreated.externalCallPerformed)

    $gaReplay=Invoke-RequestCreation $gaId $gaSecret $gaBody $gaKey
    $changed=$gaBody.Clone();$changed.expectedApplicantVersion=26
    Add-RequestCreationCheck 'Exact retry and durable GET are stable while changed-key and second-command reuse fail closed' (
        ($gaReplay|ConvertTo-Json -Depth 10 -Compress)-eq($gaCreated|ConvertTo-Json -Depth 10 -Compress) -and
        ((Get-RequestCreation $gaId $gaSecret)|ConvertTo-Json -Depth 10 -Compress)-eq($gaCreated|ConvertTo-Json -Depth 10 -Compress) -and
        (Get-RequestCreationStatus 'POST' $gaId $gaSecret $changed $gaKey)-eq 409 -and
        (Get-RequestCreationStatus 'POST' $gaId $gaSecret $changed (New-Key 'sp40-second'))-eq 409)

    $caJson=$caBody|ConvertTo-Json -Compress
    $raceKeyPrefix='sp40-race'
    $raceStatuses=1..8|ForEach-Object -Parallel {
        $headers=@{
          'X-AvenChart-Telehealth-Applicant-Key'=$using:caSecret
          'X-Idempotency-Key'="$using:raceKeyPrefix-$($_)-$([Guid]::NewGuid().ToString('N'))"
        }
        try {
            [int](Invoke-WebRequest "$using:ApiBaseUrl/api/telehealth/v1/applicants/$using:caId/telehealth-request" `
                -Method Post -Headers $headers -ContentType 'application/json' -Body $using:caJson -TimeoutSec 40).StatusCode
        }
        catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
    } -ThrottleLimit 8
    Add-RequestCreationCheck 'Eight concurrent California commands produce exactly one Draft request and receipt' (
        @($raceStatuses|Where-Object{$_-eq 201}).Count-eq 1 -and
        @($raceStatuses|Where-Object{$_-notin@(201,409)}).Count-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_creations where applicant_id='$caId'::uuid;")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where source_applicant_id='$caId'::uuid;")-eq 1) $raceStatuses

    $patientIdToRestore=Invoke-Scalar "select canonical_patient_id from telehealth_applicant_synthetic_promotions where applicant_id='$flId'::uuid;"
    $patientEmailToRestore=Invoke-Scalar "select email from patients where canonical_id='$patientIdToRestore';"
    $escapedPatientId=$patientIdToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set email=email||'.drift' where canonical_id='$escapedPatientId'; select 'drifted';"
    $driftStatus=Get-RequestCreationStatus 'POST' $flId $flSecret $flBody (New-Key 'sp40-drift')
    $escapedEmail=$patientEmailToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set email='$escapedEmail' where canonical_id='$escapedPatientId'; select 'restored';"
    $patientEmailToRestore=$null
    $flCreated=Invoke-RequestCreation $flId $flSecret $flBody (New-Key 'sp40-fl')
    Add-RequestCreationCheck 'Copied patient-shell drift fails closed and restoration permits the Florida Draft request' (
        $driftStatus-eq 409 -and $flCreated.applicantStatus-eq'SyntheticRequestCreated' -and
        $flCreated.complaintCategory-eq'migraine')

    Add-RequestCreationCheck 'Three receipts, request events, and applicant events preserve controlled evidence and false consequences' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_request_creations
where applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)
  and source_applicant_version=25 and resulting_applicant_version=26
  and resulting_applicant_status='SyntheticRequestCreated'
  and request_status='Draft' and request_version=1
  and authorization_policy_version=1
  and request_creation_confirmed and no_queue_or_care_acknowledged
  and urgent_or_worsening_action_acknowledged
  and policy_key='SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION' and policy_version=1
  and telehealth_request_created and not patient_contacted
  and not patient_care_queue_entered and not clinician_queue_entered
  and not doctor_search_started and not queue_position_assigned
  and not appointment_created and not encounter_created and not consent_created
  and not care_authorized and not prescribing_enabled and not billing_enabled
  and not claim_created and not integration_enabled and not external_call_performed;
"@)-eq 3 -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_request_events e
join telehealth_requests r on r.request_id=e.request_id
where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)
  and e.aggregate_version=1 and e.action='applicant-request-created'
  and e.from_status is null and e.to_status='Draft' and e.actor_type='applicant';
"@)-eq 3 -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_events
where applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)
  and aggregate_version=26 and action='prospective-telehealth-request-created'
  and from_status='SyntheticPracticeReviewAuthorized'
  and to_status='SyntheticRequestCreated' and actor_type='applicant';
"@)-eq 3)

    Add-RequestCreationCheck 'Receipt and request source provenance are immutable at the database boundary' (
        (Test-RequestCreationMutationRejected "update telehealth_applicant_request_creations set doctor_search_started=true where applicant_id='$gaId'::uuid;") -and
        (Test-RequestCreationMutationRejected "delete from telehealth_applicant_request_creations where applicant_id='$gaId'::uuid;") -and
        (Test-RequestCreationMutationRejected "update telehealth_requests set source_applicant_id=null where source_applicant_id='$gaId'::uuid;") -and
        (Test-RequestCreationMutationRejected "delete from telehealth_requests where source_applicant_id='$gaId'::uuid;"))

    $sourceAfter=Get-RequestCreationSourceFingerprint
    Add-RequestCreationCheck 'Prior authorization, case, submission, and patient-shell evidence remains byte-stable' (
        $sourceBefore-eq$sourceAfter) @{before=$sourceBefore;after=$sourceAfter}

    Add-RequestCreationCheck 'Request creation stops before every queue, appointment, encounter, consent, care, prescribing, financial, integration, or external table action' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_requests r
where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid)
  and r.status='Draft' and r.version=1 and r.triage_outcome is null and r.ready_at is null;
"@)-eq 3 -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_queue_entries q
join telehealth_requests r on r.request_id=q.request_id
where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid);
"@)-eq 0 -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_patient_locations l
join telehealth_requests r on r.request_id=l.request_id
where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid);
"@)-eq 0 -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_triage_assessments t
join telehealth_requests r on r.request_id=t.request_id
where r.source_applicant_id in ('$gaId'::uuid,'$caId'::uuid,'$flId'::uuid);
"@)-eq 0)
}
catch {
    $passed=$false
    Add-RequestCreationCheck 'Applicant request-creation proof execution' $false "$($_.Exception.Message) | $($_.ScriptStackTrace)"
}
finally {
    if($null-ne$patientEmailToRestore -and $null-ne$patientIdToRestore){
        try {
            $escapedPatientId=$patientIdToRestore.Replace("'","''")
            $escapedEmail=$patientEmailToRestore.Replace("'","''")
            $null=Invoke-Scalar "update patients set email='$escapedEmail' where canonical_id='$escapedPatientId'; select 'restored';"
        }
        catch { Add-RequestCreationCheck 'Request-creation proof restores the copied patient-shell fixture' $false $_.Exception.Message }
    }
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0043'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Applicant request-creation proof failed. See $resultPath"}
Write-Host "Applicant request-creation proof passed ($($checks.Count) checks). Artifact: $resultPath"
