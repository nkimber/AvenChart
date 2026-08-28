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
    throw 'Telehealth practice-review claim proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantPracticeReviewInbox.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-practice-review-claim.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-ClaimCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function Claim-Path([string]$CaseId) { "/api/telehealth/v1/admin/applicant-practice-review/$CaseId/claim" }
function Invoke-Claim([string]$CaseId,[hashtable]$Body,[string]$Key,[hashtable]$Headers=$adminHeaders) {
    $claimHeaders=$Headers.Clone();$claimHeaders['X-Idempotency-Key']=$Key
    Invoke-RestMethod "$ApiBaseUrl$(Claim-Path $CaseId)" -Method Post -Headers $claimHeaders `
        -ContentType 'application/json' -Body ($Body|ConvertTo-Json -Compress) -TimeoutSec 40
}
function Get-ClaimStatus([string]$CaseId,[hashtable]$Body,[string]$Key,[hashtable]$Headers=$adminHeaders) {
    try { $null=Invoke-Claim $CaseId $Body $Key $Headers; 200 }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}
function Test-ClaimMutationRejected([string]$Sql) {
    try { $null=Invoke-Scalar $Sql; $false } catch { $true }
}
function Get-ClaimProductFingerprint {
    Invoke-Scalar @"
select md5(concat_ws('|',
  coalesce((select jsonb_agg(to_jsonb(c) order by c.case_id)::text from telehealth_prospective_practice_review_cases c),'[]'),
  coalesce((select jsonb_agg(to_jsonb(s) order by s.submission_id)::text from telehealth_applicant_practice_review_submissions s),'[]'),
  coalesce((select jsonb_agg(to_jsonb(a) order by a.applicant_id)::text from telehealth_prospective_applicants a
    where a.applicant_id in ('$($success.Created.applicantId)','$($race.Created.applicantId)','$($fl.Created.applicantId)')),'[]'),
  (select count(*)::text from telehealth_requests),
  (select count(*)::text from telehealth_queue_entries),
  (select count(*)::text from appointments),
  (select count(*)::text from encounters),
  (select count(*)::text from prescriptions),
  (select count(*)::text from claims)));
"@
}

try {
    # The baseline front-desk fixture is intentionally scoped to facility 11/treatment.
    # Extend only this disposable proof database so the same real front-desk principal
    # can exercise the governed facility 10/healthcare-operations path.
    $null=Invoke-Scalar @"
insert into auth_principal_facility_grants
  (username,facility_id,is_default,active,granted_by,updated_by)
values ('gold-frontdesk-01',10,false,true,'telehealth-claim-proof','telehealth-claim-proof')
on conflict (username,facility_id) do update
set active=true,updated_at=now(),updated_by='telehealth-claim-proof';
insert into auth_principal_purpose_of_use_grants
  (username,purpose_of_use,active,granted_by,updated_by)
values ('gold-frontdesk-01','healthcare-operations',true,'telehealth-claim-proof','telehealth-claim-proof')
on conflict (username,purpose_of_use) do update
set active=true,updated_at=now(),updated_by='telehealth-claim-proof';
select 1;
"@
    $frontdesk=Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='gold-frontdesk-01';password='pass'}|ConvertTo-Json) -TimeoutSec 30
    $frontdeskHeaders=@{
        'X-AvenChart-Session'=$frontdesk.sessionId
        'X-AvenChart-Facility-Id'='10'
        'X-AvenChart-Purpose-Of-Use'='healthcare-operations'
    }
    $gaCase=[string]$gaResult.practiceReviewCaseId
    $caCase=[string]$raceReplay.practiceReviewCaseId
    $flCase=[string]$flResult.practiceReviewCaseId
    $gaBody=@{
        expectedApplicantVersion=[int]$gaResult.applicantVersion
        inboxPolicyVersion=1
        noDecisionAcknowledged=$true
        noPatientContactAcknowledged=$true
        noRequestOrCareQueueAcknowledged=$true
    }
    $productBefore=Get-ClaimProductFingerprint
    $auditBefore=[int](Invoke-Scalar "select count(*) from phi_access_audit_events where resource_type='TelehealthApplicantPracticeReviewClaim' and facility_id=10 and required_permission like 'acl.patients.demo.write@%' and authorized;")

    $missing=$gaBody.Clone();$missing.noDecisionAcknowledged=$false
    $stale=$gaBody.Clone();$stale.expectedApplicantVersion=[int]$gaBody.expectedApplicantVersion-1
    Add-ClaimCheck 'Incomplete acknowledgment and stale version fail before claim evidence' (
        (Get-ClaimStatus $gaCase $missing "claim-missing-$([Guid]::NewGuid().ToString('N'))")-eq 400 -and
        (Get-ClaimStatus $gaCase $stale "claim-stale-$([Guid]::NewGuid().ToString('N'))")-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_practice_review_claims where case_id='$gaCase'::uuid;")-eq 0)

    $gaKey="claim-ga-$([Guid]::NewGuid().ToString('N'))"
    $gaClaim=Invoke-Claim $gaCase $gaBody $gaKey
    Add-ClaimCheck 'Authorized administrator creates one exact 120-second bounded claim' (
        $gaClaim.practiceReviewCaseId-eq$gaCase -and $gaClaim.applicantVersion-eq$gaBody.expectedApplicantVersion -and
        $gaClaim.policyKey-eq'SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM' -and $gaClaim.policyVersion-eq 1 -and
        $gaClaim.assigned -and $gaClaim.assignedToCurrentUser -and $gaClaim.staffActionTaken -and
        ([DateTimeOffset]$gaClaim.assignmentExpiresAt-[DateTimeOffset]$gaClaim.assignedAt).TotalSeconds-eq 120)

    $gaReplay=Invoke-Claim $gaCase $gaBody $gaKey
    $changed=$gaBody.Clone();$changed.inboxPolicyVersion=2
    Add-ClaimCheck 'Exact replay is stable while changed-key reuse and a second active claim conflict' (
        $gaReplay.claimId-eq$gaClaim.claimId -and
        (Get-ClaimStatus $gaCase $changed $gaKey)-eq 409 -and
        (Get-ClaimStatus $gaCase $gaBody "claim-ga-second-$([Guid]::NewGuid().ToString('N'))")-eq 409)

    $adminInbox=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review" -Headers $adminHeaders -TimeoutSec 30
    $frontdeskInbox=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review" -Headers $frontdeskHeaders -TimeoutSec 30
    $adminItem=@($adminInbox.items|Where-Object practiceReviewCaseId -eq $gaCase)[0]
    $frontdeskItem=@($frontdeskInbox.items|Where-Object practiceReviewCaseId -eq $gaCase)[0]
    Add-ClaimCheck 'Inbox shows mine versus another reviewer without exposing staff identity' (
        $adminItem.assigned -and $adminItem.assignedToCurrentUser -and $adminItem.assignmentExpiresAt -eq $gaClaim.assignmentExpiresAt -and
        $frontdeskItem.assigned -and -not$frontdeskItem.assignedToCurrentUser -and
        ($frontdeskInbox|ConvertTo-Json -Depth 20 -Compress)-notmatch '"assignedToActor(Id)?"|"assigned(To)?Staff(Id)?"|"username"|"displayName"')

    $caBody=$gaBody.Clone();$caBody.expectedApplicantVersion=[int]$raceReplay.applicantVersion
    $caJson=$caBody|ConvertTo-Json -Compress
    $raceStatuses=1..8|ForEach-Object -Parallel {
        $sourceHeaders=$using:adminHeaders;$headers=$sourceHeaders.Clone();$headers['X-Idempotency-Key']="claim-race-$($_)-$([Guid]::NewGuid().ToString('N'))"
        try { [int](Invoke-WebRequest "$using:ApiBaseUrl/api/telehealth/v1/admin/applicant-practice-review/$using:caCase/claim" -Method Post -Headers $headers -ContentType 'application/json' -Body $using:caJson -TimeoutSec 40).StatusCode }
        catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
    } -ThrottleLimit 8
    Add-ClaimCheck 'Eight distinct concurrent claim attempts produce exactly one first writer' (
        @($raceStatuses|Where-Object{$_-eq 200}).Count-eq 1 -and
        @($raceStatuses|Where-Object{$_-eq 409}).Count-eq 7 -and
        [int](Invoke-Scalar "select count(*) from telehealth_practice_review_claims where case_id='$caCase'::uuid;")-eq 1) $raceStatuses

    $expiredId=[Guid]::NewGuid();$expiredKey="expired-seed-$([Guid]::NewGuid().ToString('N'))";$expiredHash=('a'*64)
    $null=Invoke-Scalar @"
insert into telehealth_practice_review_claims(
 claim_id,case_id,practice_id,facility_id,expected_applicant_version,
 assigned_to_actor_id,assigned_to_role,assigned_at,lease_expires_at,
 no_decision_acknowledged,no_patient_contact_acknowledged,no_request_or_care_queue_acknowledged,
 policy_key,policy_version,evidence_type,idempotency_key,command_fingerprint)
values('$expiredId','$flCase','avenchart-synthetic-practice',10,$([int]$flResult.applicantVersion),
 'expired-proof-actor','administrator',now()-interval '121 seconds',now()-interval '1 second',
 true,true,true,'SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM',1,
 'PENDING_PRACTICE_REVIEW_SHORT_LEASE_RECEIPT','$expiredKey','$expiredHash');
select 'inserted';
"@
    $flBody=$gaBody.Clone();$flBody.expectedApplicantVersion=[int]$flResult.applicantVersion
    $flClaim=Invoke-Claim $flCase $flBody "claim-fl-$([Guid]::NewGuid().ToString('N'))"
    Add-ClaimCheck 'An expired immutable claim does not block a new active claim' (
        $flClaim.assigned -and
        [int](Invoke-Scalar "select count(*) from telehealth_practice_review_claims where case_id='$flCase'::uuid;")-eq 2)

    Add-ClaimCheck 'Claim receipts are append-only and preserve every false consequence' (
        (Test-ClaimMutationRejected "update telehealth_practice_review_claims set priority_assigned=true where claim_id='$($gaClaim.claimId)'::uuid;") -and
        (Test-ClaimMutationRejected "delete from telehealth_practice_review_claims where claim_id='$($gaClaim.claimId)'::uuid;") -and
        [int](Invoke-Scalar @"
select count(*) from telehealth_practice_review_claims where
 case_id in ('$gaCase'::uuid,'$caCase'::uuid,'$flCase'::uuid)
 and staff_review_work_item_exists and staff_action_taken and assigned and not priority_assigned
 and not practice_accepted and not practice_declined and not patient_contacted
 and not clinician_review_created and not telehealth_request_created
 and not patient_care_queue_entered and not clinician_queue_entered
 and not appointment_created and not encounter_created and not care_authorized
 and not prescribing_enabled and not billing_enabled and not claim_created
 and not integration_enabled and not external_call_performed;
"@)-eq 4)

    Add-ClaimCheck 'Only authorized in-scope practice operations staff can claim' (
        (Get-ClaimStatus $gaCase $gaBody "claim-provider-$([Guid]::NewGuid().ToString('N'))" $providerHeaders)-eq 403 -and
        (Get-ClaimStatus $gaCase $gaBody "claim-cross-$([Guid]::NewGuid().ToString('N'))" $crossFacility)-in@(403,404))

    $productAfter=Get-ClaimProductFingerprint
    Add-ClaimCheck 'Claims add only immutable claim and audit evidence without product-state mutation' ($productBefore-eq$productAfter) @{before=$productBefore;after=$productAfter}
    $auditAfter=[int](Invoke-Scalar "select count(*) from phi_access_audit_events where resource_type='TelehealthApplicantPracticeReviewClaim' and facility_id=10 and required_permission like 'acl.patients.demo.write@%' and authorized;")
    Add-ClaimCheck 'Successful claims are case-correlated to healthcare-operations write audit evidence' ($auditAfter-ge($auditBefore+3)) @{before=$auditBefore;after=$auditAfter}
}
catch {
    $passed=$false
    Add-ClaimCheck 'Practice-review claim proof execution' $false "$($_.Exception.Message) | $($_.ScriptStackTrace)"
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0040'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Practice-review claim proof failed. See $resultPath"}
Write-Host "Practice-review claim proof passed ($($checks.Count) checks). Artifact: $resultPath"
