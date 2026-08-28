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
    throw 'Telehealth practice-review authorization proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantPracticeReviewClaim.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-practice-review-authorization.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true
$patientEmailToRestore = $null
$patientIdToRestore = $null

function Add-AuthorizationCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function Authorization-Path([string]$CaseId) {
    "/api/telehealth/v1/admin/applicant-practice-review/$CaseId/authorization"
}
function Invoke-Authorization([string]$CaseId,[hashtable]$Body,[string]$Key,[hashtable]$Headers=$adminHeaders) {
    $authorizationHeaders=$Headers.Clone()
    $authorizationHeaders['X-Idempotency-Key']=$Key
    Invoke-RestMethod "$ApiBaseUrl$(Authorization-Path $CaseId)" -Method Post `
        -Headers $authorizationHeaders -ContentType 'application/json' `
        -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40
}
function Get-AuthorizationStatus([string]$CaseId,[hashtable]$Body,[string]$Key,[hashtable]$Headers=$adminHeaders) {
    try { $null=Invoke-Authorization $CaseId $Body $Key $Headers; 200 }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}
function Get-PacketStatusForAuthorization([string]$CaseId,[hashtable]$Headers=$adminHeaders) {
    try {
        $null=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review/$CaseId" `
            -Headers $Headers -TimeoutSec 30
        200
    }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}
function Test-AuthorizationMutationRejected([string]$Sql) {
    try { $null=Invoke-Scalar $Sql; $false } catch { $true }
}
function Get-AuthorizationDownstreamFingerprint {
    Invoke-Scalar @"
select md5(concat_ws('|',
  (select count(*)::text from telehealth_requests),
  (select count(*)::text from telehealth_queue_entries),
  (select count(*)::text from appointments),
  (select count(*)::text from encounters),
  (select count(*)::text from prescriptions),
  (select count(*)::text from claims),
  (select count(*)::text from insurance_records)));
"@
}
function Get-AuthorizationSourceFingerprint {
    Invoke-Scalar @"
select md5(concat_ws('|',
  coalesce((select jsonb_agg(to_jsonb(c) order by c.case_id)::text
            from telehealth_prospective_practice_review_cases c
            where c.case_id in ('$gaCase'::uuid,'$caCase'::uuid,'$flCase'::uuid)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(s) order by s.submission_id)::text
            from telehealth_applicant_practice_review_submissions s
            where s.case_id in ('$gaCase'::uuid,'$caCase'::uuid,'$flCase'::uuid)),'[]'),
  coalesce((select jsonb_agg(to_jsonb(c) order by c.claim_id)::text
            from telehealth_practice_review_claims c
            where c.case_id in ('$gaCase'::uuid,'$caCase'::uuid,'$flCase'::uuid)),'[]')));
"@
}

try {
    $gaCase=[string]$gaResult.practiceReviewCaseId
    $caCase=[string]$raceReplay.practiceReviewCaseId
    $flCase=[string]$flResult.practiceReviewCaseId
    $gaBody=@{
        expectedApplicantVersion=[int]$gaResult.applicantVersion
        packetPolicyVersion=1
        decision='AuthorizedForSyntheticRequestCreation'
        rationaleCode='OperationalPrerequisitesReviewed'
        noClinicalEligibilityAcknowledged=$true
        noCoverageGuaranteeAcknowledged=$true
        noRequestOrQueueAcknowledged=$true
    }
    $caBody=$gaBody.Clone();$caBody.expectedApplicantVersion=[int]$raceReplay.applicantVersion
    $flBody=$gaBody.Clone();$flBody.expectedApplicantVersion=[int]$flResult.applicantVersion
    $sourceBefore=Get-AuthorizationSourceFingerprint
    $downstreamBefore=Get-AuthorizationDownstreamFingerprint
    $auditBefore=[int](Invoke-Scalar "select count(*) from phi_access_audit_events where resource_type='TelehealthApplicantPracticeReviewAuthorization' and facility_id=10 and required_permission like 'acl.patients.demo.write@%' and authorized;")

    $packets=@(
        Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review/$gaCase" -Headers $adminHeaders -TimeoutSec 30
        Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review/$caCase" -Headers $adminHeaders -TimeoutSec 30
        Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review/$flCase" -Headers $adminHeaders -TimeoutSec 30
    )
    Add-AuthorizationCheck 'Current claimant can review all three minimized state packets before authorization' (
        $packets.Count-eq 3 -and @($packets|Where-Object{
            $_.policyKey-eq'SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET' -and
            $_.policyVersion-eq 1 -and $_.assignedToCurrentUser
        }).Count-eq 3)

    $missing=$gaBody.Clone();$missing.noClinicalEligibilityAcknowledged=$false
    $changedPolicy=$gaBody.Clone();$changedPolicy.packetPolicyVersion=2
    Add-AuthorizationCheck 'Incomplete acknowledgment and a changed packet policy fail before evidence' (
        (Get-AuthorizationStatus $gaCase $missing "authorization-missing-$([Guid]::NewGuid().ToString('N'))")-eq 400 -and
        (Get-AuthorizationStatus $gaCase $changedPolicy "authorization-policy-$([Guid]::NewGuid().ToString('N'))")-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_practice_review_authorizations where case_id='$gaCase'::uuid;")-eq 0)

    Add-AuthorizationCheck 'Another staff member, a provider, and a cross-facility context cannot authorize the claimant case' (
        (Get-AuthorizationStatus $gaCase $gaBody "authorization-frontdesk-$([Guid]::NewGuid().ToString('N'))" $frontdeskHeaders)-eq 404 -and
        (Get-AuthorizationStatus $gaCase $gaBody "authorization-provider-$([Guid]::NewGuid().ToString('N'))" $providerHeaders)-eq 403 -and
        (Get-AuthorizationStatus $gaCase $gaBody "authorization-cross-$([Guid]::NewGuid().ToString('N'))" $crossFacility)-in@(403,404))

    $gaKey="authorization-ga-$([Guid]::NewGuid().ToString('N'))"
    $gaAuthorization=Invoke-Authorization $gaCase $gaBody $gaKey
    Add-AuthorizationCheck 'Administrator records the exact positive-only Georgia authorization' (
        $gaAuthorization.practiceReviewCaseId-eq$gaCase -and
        $gaAuthorization.applicantVersion-eq([int]$gaBody.expectedApplicantVersion+1) -and
        $gaAuthorization.applicantStatus-eq'SyntheticPracticeReviewAuthorized' -and
        $gaAuthorization.decision-eq'AuthorizedForSyntheticRequestCreation' -and
        $gaAuthorization.rationaleCode-eq'OperationalPrerequisitesReviewed' -and
        $gaAuthorization.policyKey-eq'SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION' -and
        $gaAuthorization.policyVersion-eq 1 -and $gaAuthorization.requestCreationAuthorized -and
        -not$gaAuthorization.practiceAccepted -and -not$gaAuthorization.patientContacted -and
        -not$gaAuthorization.clinicianReviewCreated -and -not$gaAuthorization.telehealthRequestCreated -and
        -not$gaAuthorization.patientCareQueueEntered -and -not$gaAuthorization.clinicianQueueEntered -and
        -not$gaAuthorization.appointmentCreated -and -not$gaAuthorization.encounterCreated -and
        -not$gaAuthorization.consentCreated -and -not$gaAuthorization.careAuthorized -and
        -not$gaAuthorization.prescribingEnabled -and -not$gaAuthorization.billingEnabled -and
        -not$gaAuthorization.claimCreated -and -not$gaAuthorization.integrationEnabled -and
        -not$gaAuthorization.externalCallPerformed)

    $gaReplay=Invoke-Authorization $gaCase $gaBody $gaKey
    $changedReplay=$gaBody.Clone();$changedReplay.expectedApplicantVersion=[int]$gaBody.expectedApplicantVersion+1
    Add-AuthorizationCheck 'Exact retry is stable while changed-content key reuse conflicts' (
        ($gaReplay|ConvertTo-Json -Depth 10 -Compress)-eq($gaAuthorization|ConvertTo-Json -Depth 10 -Compress) -and
        (Get-AuthorizationStatus $gaCase $changedReplay $gaKey)-eq 409)

    $adminInbox=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review" -Headers $adminHeaders -TimeoutSec 30
    Add-AuthorizationCheck 'Authorized case leaves the pending inbox and its old packet fails closed' (
        @($adminInbox.items|Where-Object practiceReviewCaseId -eq $gaCase).Count-eq 0 -and
        (Get-PacketStatusForAuthorization $gaCase)-eq 404)

    $caJson=$caBody|ConvertTo-Json -Compress
    $raceStatuses=1..8|ForEach-Object -Parallel {
        $sourceHeaders=$using:adminHeaders
        $headers=$sourceHeaders.Clone()
        $headers['X-Idempotency-Key']="authorization-race-$($_)-$([Guid]::NewGuid().ToString('N'))"
        try {
            [int](Invoke-WebRequest "$using:ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review/$using:caCase/authorization" `
                -Method Post -Headers $headers -ContentType 'application/json' -Body $using:caJson -TimeoutSec 40).StatusCode
        }
        catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
    } -ThrottleLimit 8
    Add-AuthorizationCheck 'Eight concurrent authorization attempts produce exactly one immutable first writer' (
        @($raceStatuses|Where-Object{$_-eq 200}).Count-eq 1 -and
        @($raceStatuses|Where-Object{$_-notin@(404,409)}).Count-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_practice_review_authorizations where case_id='$caCase'::uuid;")-eq 1) $raceStatuses

    $patientIdToRestore=Invoke-Scalar "select canonical_patient_id from telehealth_prospective_practice_review_cases where case_id='$flCase'::uuid;"
    $patientEmailToRestore=Invoke-Scalar "select email from patients where canonical_id='$patientIdToRestore';"
    $escapedPatientId=$patientIdToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set email=email||'.drift' where canonical_id='$escapedPatientId'; select 'drifted';"
    $driftStatus=Get-AuthorizationStatus $flCase $flBody "authorization-fl-drift-$([Guid]::NewGuid().ToString('N'))"
    $escapedEmail=$patientEmailToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set email='$escapedEmail' where canonical_id='$escapedPatientId'; select 'restored';"
    $patientEmailToRestore=$null
    $flAuthorization=Invoke-Authorization $flCase $flBody "authorization-fl-$([Guid]::NewGuid().ToString('N'))"
    Add-AuthorizationCheck 'Copied patient-shell drift fails closed and restoration permits Florida authorization' (
        $driftStatus-eq 404 -and $flAuthorization.applicantStatus-eq'SyntheticPracticeReviewAuthorized')

    Add-AuthorizationCheck 'Authorization receipts and applicant events preserve exact controlled evidence and false consequences' (
        [int](Invoke-Scalar @"
select count(*) from telehealth_practice_review_authorizations
where case_id in ('$gaCase'::uuid,'$caCase'::uuid,'$flCase'::uuid)
  and source_applicant_version=24 and resulting_applicant_version=25
  and resulting_applicant_status='SyntheticPracticeReviewAuthorized'
  and decision='AuthorizedForSyntheticRequestCreation'
  and rationale_code='OperationalPrerequisitesReviewed'
  and packet_policy_key='SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET' and packet_policy_version=1
  and policy_key='SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION' and policy_version=1
  and request_creation_authorized and not practice_accepted and not patient_contacted
  and not clinician_review_created and not telehealth_request_created
  and not patient_care_queue_entered and not clinician_queue_entered
  and not appointment_created and not encounter_created and not consent_created
  and not care_authorized and not prescribing_enabled and not billing_enabled
  and not claim_created and not integration_enabled and not external_call_performed;
"@)-eq 3 -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_applicant_events e
join telehealth_prospective_practice_review_cases c on c.applicant_id=e.applicant_id
where c.case_id in ('$gaCase'::uuid,'$caCase'::uuid,'$flCase'::uuid)
  and e.action='prospective-practice-review-authorized'
  and e.from_status='SyntheticPracticeReviewSubmitted'
  and e.to_status='SyntheticPracticeReviewAuthorized';
"@)-eq 3)

    Add-AuthorizationCheck 'Authorization evidence is append-only at the database boundary' (
        (Test-AuthorizationMutationRejected "update telehealth_practice_review_authorizations set request_creation_authorized=false where case_id='$gaCase'::uuid;") -and
        (Test-AuthorizationMutationRejected "delete from telehealth_practice_review_authorizations where case_id='$gaCase'::uuid;"))

    $sourceAfter=Get-AuthorizationSourceFingerprint
    $downstreamAfter=Get-AuthorizationDownstreamFingerprint
    Add-AuthorizationCheck 'Only applicant status/version and immutable authorization/event/audit evidence change' (
        $sourceBefore-eq$sourceAfter -and $downstreamBefore-eq$downstreamAfter -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_prospective_applicants a
join telehealth_prospective_practice_review_cases c on c.applicant_id=a.applicant_id
where c.case_id in ('$gaCase'::uuid,'$caCase'::uuid,'$flCase'::uuid)
  and a.status='SyntheticPracticeReviewAuthorized' and a.version=25;
"@)-eq 3) @{sourceBefore=$sourceBefore;sourceAfter=$sourceAfter;downstreamBefore=$downstreamBefore;downstreamAfter=$downstreamAfter}

    $auditAfter=[int](Invoke-Scalar "select count(*) from phi_access_audit_events where resource_type='TelehealthApplicantPracticeReviewAuthorization' and facility_id=10 and required_permission like 'acl.patients.demo.write@%' and authorized;")
    Add-AuthorizationCheck 'Successful authorizations create case-correlated healthcare-operations write audit evidence' (
        $auditAfter-ge($auditBefore+4) -and
        [int](Invoke-Scalar "select count(*) from phi_access_audit_events where resource_type='TelehealthApplicantPracticeReviewAuthorization' and resource_id='$gaCase' and facility_id=10 and purpose_of_use='healthcare-operations' and authorized;")-ge 2) @{before=$auditBefore;after=$auditAfter}
}
catch {
    $passed=$false
    Add-AuthorizationCheck 'Practice-review authorization proof execution' $false "$($_.Exception.Message) | $($_.ScriptStackTrace)"
}
finally {
    if($null-ne$patientEmailToRestore -and $null-ne$patientIdToRestore){
        try {
            $escapedPatientId=$patientIdToRestore.Replace("'","''")
            $escapedEmail=$patientEmailToRestore.Replace("'","''")
            $null=Invoke-Scalar "update patients set email='$escapedEmail' where canonical_id='$escapedPatientId'; select 'restored';"
        }
        catch { Add-AuthorizationCheck 'Practice-review authorization proof restores the copied patient-shell fixture' $false $_.Exception.Message }
    }
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0042'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Practice-review authorization proof failed. See $resultPath"}
Write-Host "Practice-review authorization proof passed ($($checks.Count) checks). Artifact: $resultPath"
