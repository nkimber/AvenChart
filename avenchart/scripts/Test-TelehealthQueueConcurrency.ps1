# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
  [string]$ApiBaseUrl = 'http://127.0.0.1:5001',
  [int]$CallerCount = 20,
  [ValidatePattern('^avenchart(?:_test_[a-z0-9_]+)?$')]
  [string]$DatabaseName = 'avenchart'
)

$ErrorActionPreference='Stop'
if(([Uri]$ApiBaseUrl).Host-notin@('localhost','127.0.0.1','::1')){throw 'Telehealth concurrency proof is local-only.'}
if($CallerCount-lt 20-or$CallerCount-gt 100){throw 'CallerCount must be between 20 and 100.'}
. (Join-Path $PSScriptRoot 'AvenChartStaffAccessContext.ps1')
$solutionRoot=Resolve-Path(Join-Path $PSScriptRoot '..');$artifactsRoot=Join-Path $solutionRoot 'artifacts/telehealth';New-Item -ItemType Directory -Force $artifactsRoot|Out-Null
$resultPath=Join-Path $artifactsRoot 'latest-telehealth-queue-concurrency.json';$checks=[System.Collections.Generic.List[object]]::new();$passed=$true
$originalCoverageGroup=$null
$originalPatientDob=$null
$reservationId=$null
$proofPhysicianAccountCreated=$false
function Add-Check([string]$Name,[bool]$Result,[object]$Details=$null){$script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details});if(-not$Result){$script:passed=$false}}
function Key([string]$Prefix){"$Prefix-$([Guid]::NewGuid().ToString('N'))"}
function Post([string]$Path,[hashtable]$Headers,[object]$Body=$null){$p=@{Uri="$ApiBaseUrl$Path";Method='Post';Headers=$Headers;TimeoutSec=30};if($null-ne$Body){$p.ContentType='application/json';$p.Body=$Body|ConvertTo-Json -Depth 8};Invoke-RestMethod @p}
function Status([string]$Path,[hashtable]$Headers,[object]$Body=$null){$p=@{Uri="$ApiBaseUrl$Path";Method='Post';Headers=$Headers;TimeoutSec=30};if($null-ne$Body){$p.ContentType='application/json';$p.Body=$Body|ConvertTo-Json -Depth 8};try{[int](Invoke-WebRequest @p).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}}
function Put([string]$Path,[hashtable]$Headers,[object]$Body){Invoke-RestMethod "$ApiBaseUrl$Path" -Method Put -Headers $Headers -ContentType 'application/json' -Body ($Body|ConvertTo-Json -Depth 8) -TimeoutSec 30}
function Put-Status([string]$Path,[hashtable]$Headers,[object]$Body){try{[int](Invoke-WebRequest "$ApiBaseUrl$Path" -Method Put -Headers $Headers -ContentType 'application/json' -Body ($Body|ConvertTo-Json -Depth 8) -TimeoutSec 30).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}}
function Get-Status([string]$Path,[hashtable]$Headers){try{[int](Invoke-WebRequest "$ApiBaseUrl$Path" -Headers $Headers -TimeoutSec 30).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}}
function Staff([string]$Username){Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username=$Username;password='pass'}|ConvertTo-Json) -TimeoutSec 20}
function Scalar([string]$Sql){Push-Location $solutionRoot;try{$v=docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -t -A -v ON_ERROR_STOP=1 -c $Sql;if($LASTEXITCODE-ne 0){throw 'PostgreSQL concurrency assertion failed.'};($v|Select-Object -Last 1).Trim()}finally{Pop-Location}}
function Sql-Fails([string]$Sql){try{$null=Scalar $Sql;return $false}catch{return $true}}
try{
  $portal=Invoke-RestMethod "$ApiBaseUrl/api/patient-portal/login" -Method Post -ContentType 'application/json' -Body (@{username='mod-pat-0012@example.test';password='PortalPass207!'}|ConvertTo-Json) -TimeoutSec 20
  $originalPatientDob=Scalar "select date_of_birth::text from patients where canonical_id='MOD-PAT-0012';"
  $requestCountBeforeAdultGate=[int](Scalar "select count(*) from telehealth_requests where patient_id='MOD-PAT-0012';")
  $null=Scalar "update patients set date_of_birth=(current_date - interval '17 years')::date where canonical_id='MOD-PAT-0012'; select 'ok';"
  $minorHeaders=@{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId;'X-Idempotency-Key'=(Key 'th-minor-create')}
  $minorStatus=Status '/api/telehealth/v1/patient/requests' $minorHeaders @{complaintCategory='sleep'}
  $requestCountAfterMinorGate=[int](Scalar "select count(*) from telehealth_requests where patient_id='MOD-PAT-0012';")
  $null=Scalar "update patients set date_of_birth=(current_date - interval '121 years')::date where canonical_id='MOD-PAT-0012'; select 'ok';"
  $overageHeaders=@{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId;'X-Idempotency-Key'=(Key 'th-overage-create')}
  $overageStatus=Status '/api/telehealth/v1/patient/requests' $overageHeaders @{complaintCategory='sleep'}
  $requestCountAfterOverageGate=[int](Scalar "select count(*) from telehealth_requests where patient_id='MOD-PAT-0012';")
  $null=Scalar "update patients set date_of_birth='$originalPatientDob'::date where canonical_id='MOD-PAT-0012'; select 'ok';"
  Add-Check 'Established-patient request creation rejects patients younger than 18 without persisting a request' (
    $minorStatus-eq 404-and$requestCountAfterMinorGate-eq$requestCountBeforeAdultGate) @{status=$minorStatus;before=$requestCountBeforeAdultGate;after=$requestCountAfterMinorGate}
  Add-Check 'Established-patient request creation rejects patients older than 120 without persisting a request' (
    $overageStatus-eq 404-and$requestCountAfterOverageGate-eq$requestCountBeforeAdultGate) @{status=$overageStatus;before=$requestCountBeforeAdultGate;after=$requestCountAfterOverageGate}
  $portalHeaders=@{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId;'X-Idempotency-Key'=(Key 'th-create')}
  $request=Post '/api/telehealth/v1/patient/requests' $portalHeaders @{complaintCategory='sleep'}
  $createReplay=Post '/api/telehealth/v1/patient/requests' $portalHeaders @{complaintCategory='sleep'}
  Add-Check 'Patient create command replays idempotently' ($createReplay.requestId-eq$request.requestId-and$createReplay.version-eq$request.version) @{requestId=$request.requestId}
  Add-Check 'Idempotency-key reuse with different create content is rejected' ((Status '/api/telehealth/v1/patient/requests' $portalHeaders @{complaintCategory='migraine'}) -eq 409)
  $portalHeaders['X-Idempotency-Key']=Key 'th-location';$request=Post "/api/telehealth/v1/patient/requests/$($request.requestId)/location" $portalHeaders @{stateCode='GA';expectedVersion=$request.version}
  $staleHeaders=$portalHeaders.Clone();$staleHeaders['X-Idempotency-Key']=Key 'th-stale'
  Add-Check 'Stale expected version cannot evaluate triage' ((Status "/api/telehealth/v1/patient/requests/$($request.requestId)/triage" $staleHeaders @{hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false;expectedVersion=1}) -eq 409)
  $portalHeaders['X-Idempotency-Key']=Key 'th-triage';$request=Post "/api/telehealth/v1/patient/requests/$($request.requestId)/triage" $portalHeaders @{hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false;expectedVersion=$request.version}
  Add-Check 'Eligible synthetic request enters patient-owned Intake after location and triage' ($request.status-eq'Intake'-and$request.triageOutcome-eq'TelehealthEligible') @{requestId=$request.requestId;version=$request.version}
  $readiness=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests/$($request.requestId)/readiness" -Headers @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId} -TimeoutSec 20
  $coverage=@($readiness.coverageOptions|Where-Object{$_.provider-eq'Harbor Mutual'-and$_.planName-eq'High Deductible'})|Select-Object -First 1
  if($null-eq$coverage){throw 'The exact synthetic confirmed-coverage fixture was not available.'}
  Add-Check 'Readiness projection masks policy and group identifiers' ($coverage.maskedPolicyNumber-match'^••••'-and$coverage.maskedPolicyNumber-notmatch'POL100012'-and$coverage.maskedGroupNumber-notmatch'GRP111') @{policy=$coverage.maskedPolicyNumber;group=$coverage.maskedGroupNumber}
  $readinessJson=$readiness|ConvertTo-Json -Depth 10 -Compress
  Add-Check 'Readiness projection uses an opaque request-bound coverage token and hides the internal insurance key' (
    $coverage.coverageToken-match'^[0-9a-f]{64}$'-and$readinessJson-notmatch'INS-MOD-PAT-0012') @{tokenLength=$coverage.coverageToken.Length}
  $portalHeaders['X-Idempotency-Key']=Key 'th-readiness'
  $readinessBody=@{
    expectedVersion=$readiness.requestVersion
    demographicsFingerprint=$readiness.patientDetails.fingerprint
    clinicalSummaryFingerprint=$readiness.clinicalSummary.fingerprint
    demographicsConfirmed=$true;contactConfirmed=$true;clinicalSummaryConfirmed=$true
    complaintSummary='Synthetic sleep difficulty demonstration';symptomDuration='1-3-days';syntheticDataConfirmed=$true
    coverageToken=$coverage.coverageToken;coverageFingerprint=$coverage.fingerprint;coverageConfirmed=$true
    acknowledgmentPackageKey=$readiness.acknowledgment.packageKey
    acknowledgmentPackageVersion=$readiness.acknowledgment.packageVersion
    acknowledgmentContentHash=$readiness.acknowledgment.contentHash
    acknowledgmentAccepted=$true
  }
  $request=Post "/api/telehealth/v1/patient/requests/$($request.requestId)/readiness" $portalHeaders $readinessBody
  Add-Check 'Exact patient confirmations and bounded intake enter Verification' ($request.status-eq'Verification'-and$request.version-eq 4) @{version=$request.version}
  $readinessReplay=Post "/api/telehealth/v1/patient/requests/$($request.requestId)/readiness" $portalHeaders $readinessBody
  Add-Check 'Patient readiness command replays idempotently without duplicate version' ($readinessReplay.status-eq'Verification'-and$readinessReplay.version-eq$request.version)
  $readinessConflict=$readinessBody.Clone();$readinessConflict.complaintSummary='Different synthetic complaint with reused key'
  Add-Check 'Readiness idempotency key rejects changed content' ((Status "/api/telehealth/v1/patient/requests/$($request.requestId)/readiness" $portalHeaders $readinessConflict) -eq 409)
  $portalHeaders['X-Idempotency-Key']=Key 'th-coverage-verify'
  $coverageBody=@{expectedVersion=$request.version}
  $request=Post "/api/telehealth/v1/patient/requests/$($request.requestId)/coverage/verify" $portalHeaders $coverageBody
  Add-Check 'Separate confirmed synthetic eligibility and network gates reach OperationalReview' (
    $request.status-eq'OperationalReview'-and$request.coverage.adapterMode-eq'NON_PRODUCTION'-and
    $request.coverage.eligibilityStatus-eq'Active'-and$request.coverage.networkStatus-eq'ConfirmedInNetwork') $request.coverage
  $coverageReplay=Post "/api/telehealth/v1/patient/requests/$($request.requestId)/coverage/verify" $portalHeaders $coverageBody
  Add-Check 'Coverage verification command replays idempotently without a second evidence version' ($coverageReplay.status-eq'OperationalReview'-and$coverageReplay.version-eq$request.version)
  Add-Check 'Coverage verification key rejects a changed expected version' ((Status "/api/telehealth/v1/patient/requests/$($request.requestId)/coverage/verify" $portalHeaders @{expectedVersion=($coverageBody.expectedVersion+1)}) -eq 409)

  $portalHeaders['X-Idempotency-Key']=Key 'th-pending-create';$pendingNetwork=Post '/api/telehealth/v1/patient/requests' $portalHeaders @{complaintCategory='migraine'}
  $portalHeaders['X-Idempotency-Key']=Key 'th-pending-location';$pendingNetwork=Post "/api/telehealth/v1/patient/requests/$($pendingNetwork.requestId)/location" $portalHeaders @{stateCode='CA';expectedVersion=$pendingNetwork.version}
  $portalHeaders['X-Idempotency-Key']=Key 'th-pending-triage';$pendingNetwork=Post "/api/telehealth/v1/patient/requests/$($pendingNetwork.requestId)/triage" $portalHeaders @{hasEmergencyWarning=$false;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false;expectedVersion=$pendingNetwork.version}
  $pendingReadiness=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests/$($pendingNetwork.requestId)/readiness" -Headers @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId} -TimeoutSec 20
  $pendingCoverage=@($pendingReadiness.coverageOptions|Where-Object{$_.provider-eq'Blue Valley Health'})|Select-Object -First 1
  if($null-eq$pendingCoverage){throw 'The synthetic network-pending fixture was not available.'}
  $portalHeaders['X-Idempotency-Key']=Key 'th-pending-readiness'
  $pendingNetwork=Post "/api/telehealth/v1/patient/requests/$($pendingNetwork.requestId)/readiness" $portalHeaders @{
    expectedVersion=$pendingReadiness.requestVersion
    demographicsFingerprint=$pendingReadiness.patientDetails.fingerprint
    clinicalSummaryFingerprint=$pendingReadiness.clinicalSummary.fingerprint
    demographicsConfirmed=$true;contactConfirmed=$true;clinicalSummaryConfirmed=$true
    complaintSummary='Synthetic migraine purpose demonstration';symptomDuration='4-14-days';syntheticDataConfirmed=$true
    coverageToken=$pendingCoverage.coverageToken;coverageFingerprint=$pendingCoverage.fingerprint;coverageConfirmed=$true
    acknowledgmentPackageKey=$pendingReadiness.acknowledgment.packageKey
    acknowledgmentPackageVersion=$pendingReadiness.acknowledgment.packageVersion
    acknowledgmentContentHash=$pendingReadiness.acknowledgment.contentHash
    acknowledgmentAccepted=$true
  }
  $portalHeaders['X-Idempotency-Key']=Key 'th-pending-verify';$pendingNetwork=Post "/api/telehealth/v1/patient/requests/$($pendingNetwork.requestId)/coverage/verify" $portalHeaders @{expectedVersion=$pendingNetwork.version}
  Add-Check 'Active coverage with unknown exact network remains in Verification' (
    $pendingNetwork.status-eq'Verification'-and$pendingNetwork.coverage.eligibilityStatus-eq'Active'-and
    $pendingNetwork.coverage.networkStatus-eq'Unknown'-and$pendingNetwork.coverage.financialRoute-eq'CoverageActiveNetworkPending') $pendingNetwork.coverage

  $portalHeaders['X-Idempotency-Key']=Key 'th-emergency-create';$unsafe=Post '/api/telehealth/v1/patient/requests' $portalHeaders @{complaintCategory='migraine'}
  $portalHeaders['X-Idempotency-Key']=Key 'th-emergency-location';$unsafe=Post "/api/telehealth/v1/patient/requests/$($unsafe.requestId)/location" $portalHeaders @{stateCode='GA';expectedVersion=$unsafe.version}
  $portalHeaders['X-Idempotency-Key']=Key 'th-emergency-triage';$unsafe=Post "/api/telehealth/v1/patient/requests/$($unsafe.requestId)/triage" $portalHeaders @{hasEmergencyWarning=$true;severeOrWorsening=$false;requiresHandsOnExam=$false;unsure=$false;expectedVersion=$unsafe.version}
  Add-Check 'Emergency synthetic outcome terminates before operational review' ($unsafe.status-eq'Redirected'-and$unsafe.triageOutcome-eq'Emergency') @{requestId=$unsafe.requestId}

  $admin=Staff 'admin'
  if (@($admin.accessContext.facilities | Where-Object { $_.facilityId -eq 10 }).Count -ne 1 -or @($admin.accessContext.purposes) -notcontains 'healthcare-operations') { throw 'Synthetic administrator lacks the required access context.' }
  $adminHeaders=@{'X-AvenChart-Session'=$admin.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='healthcare-operations';'X-Idempotency-Key'=(Key 'th-authorize')}
  Add-Check 'Administrator cannot queue an emergency-redirected request' ((Status "/api/telehealth/v1/admin/requests/$($unsafe.requestId)/authorize" $adminHeaders @{expectedVersion=$unsafe.version}) -eq 409)
  $reviewItems=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/admin/operational-review" -Headers $adminHeaders -TimeoutSec 20
  Add-Check 'Network-unknown request is absent from administrator operational review' (@($reviewItems.requests|Where-Object{$_.requestId-eq$pendingNetwork.requestId}).Count -eq 0)
  $adminHeaders['X-Idempotency-Key']=Key 'th-pending-authorize'
  Add-Check 'Administrator cannot override an unknown exact-network result' ((Status "/api/telehealth/v1/admin/requests/$($pendingNetwork.requestId)/authorize" $adminHeaders @{expectedVersion=$pendingNetwork.version}) -eq 409)

  $originalCoverageGroup=Scalar "select group_number from insurance_records where id='INS-MOD-PAT-0012-P' and patient_id='MOD-PAT-0012';"
  $changedCoverageGroup="GRP-CHANGE-$([Guid]::NewGuid().ToString('N').Substring(0,12))"
  $null=Scalar "update insurance_records set group_number='$changedCoverageGroup' where id='INS-MOD-PAT-0012-P' and patient_id='MOD-PAT-0012';"
  $changedGroup=Scalar "select group_number from insurance_records where id='INS-MOD-PAT-0012-P' and patient_id='MOD-PAT-0012';"
  $adminHeaders['X-Idempotency-Key']=Key 'th-stale-coverage-authorize'
  Add-Check 'Administrator final gate rejects coverage changed after verification' (
    $changedGroup-eq$changedCoverageGroup-and$changedGroup-ne$originalCoverageGroup-and
    (Status "/api/telehealth/v1/admin/requests/$($request.requestId)/authorize" $adminHeaders @{expectedVersion=$request.version}) -eq 409)
  $refreshedReadiness=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests/$($request.requestId)/readiness" -Headers @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId} -TimeoutSec 20
  $refreshedCoverage=@($refreshedReadiness.coverageOptions|Where-Object{$_.provider-eq'Harbor Mutual'})|Select-Object -First 1
  $portalHeaders['X-Idempotency-Key']=Key 'th-refresh-readiness'
  $request=Post "/api/telehealth/v1/patient/requests/$($request.requestId)/readiness" $portalHeaders @{
    expectedVersion=$refreshedReadiness.requestVersion
    demographicsFingerprint=$refreshedReadiness.patientDetails.fingerprint
    clinicalSummaryFingerprint=$refreshedReadiness.clinicalSummary.fingerprint
    demographicsConfirmed=$true;contactConfirmed=$true;clinicalSummaryConfirmed=$true
    complaintSummary='Synthetic refreshed sleep difficulty demonstration';symptomDuration='1-3-days';syntheticDataConfirmed=$true
    coverageToken=$refreshedCoverage.coverageToken;coverageFingerprint=$refreshedCoverage.fingerprint;coverageConfirmed=$true
    acknowledgmentPackageKey=$refreshedReadiness.acknowledgment.packageKey
    acknowledgmentPackageVersion=$refreshedReadiness.acknowledgment.packageVersion
    acknowledgmentContentHash=$refreshedReadiness.acknowledgment.contentHash
    acknowledgmentAccepted=$true
  }
  $portalHeaders['X-Idempotency-Key']=Key 'th-refresh-coverage';$request=Post "/api/telehealth/v1/patient/requests/$($request.requestId)/coverage/verify" $portalHeaders @{expectedVersion=$request.version}
  Add-Check 'Patient can reconfirm changed source data and restore current separate coverage gates' (
    $request.status-eq'OperationalReview'-and$request.coverage.eligibilityStatus-eq'Active'-and$request.coverage.networkStatus-eq'ConfirmedInNetwork') @{version=$request.version}
  $adminHeaders['X-Idempotency-Key']=Key 'th-authorize'
  $authorizedExpectedVersion=$request.version
  $request=Post "/api/telehealth/v1/admin/requests/$($request.requestId)/authorize" $adminHeaders @{expectedVersion=$request.version}
  Add-Check 'Authorized administrator moves eligible request atomically to Queued' ($request.status-eq'Queued') @{version=$request.version}
  $appointmentAtAuthorization=(Scalar "select json_build_object('count',(select count(*) from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$($request.requestId)'),'status',(select appointment.status from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$($request.requestId)'),'provider',(select appointment.provider_id from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$($request.requestId)'))::text;")|ConvertFrom-Json
  Add-Check 'Operational authorization creates exactly one scheduled unassigned appointment linkage' (
    [int]$appointmentAtAuthorization.count-eq 1-and$appointmentAtAuthorization.status-eq'-'-and$null-eq$appointmentAtAuthorization.provider) $appointmentAtAuthorization
  $null=Scalar "update telehealth_requests set ready_at=now()-interval '100 years' where request_id='$($request.requestId)'; update telehealth_queue_entries set ready_at=now()-interval '100 years' where request_id='$($request.requestId)'; select 'ok';"
  $sameFacilityRequest=[Guid]::NewGuid();$sameFacilityQueue=[Guid]::NewGuid();$otherFacilityRequest=[Guid]::NewGuid();$otherFacilityQueue=[Guid]::NewGuid();$positionHash='f'*64
  $null=Scalar @"
insert into telehealth_requests(request_id,practice_id,facility_id,patient_id,status,complaint_category,triage_outcome,version,create_idempotency_key,create_fingerprint,ready_at)
select '$sameFacilityRequest',practice_id,facility_id,'MOD-PAT-0024','Queued','migraine','TelehealthEligible',1,'th-position-same-$sameFacilityRequest','$positionHash',ready_at-interval '2 minutes'
from telehealth_requests where request_id='$($request.requestId)';
insert into telehealth_queue_entries(queue_entry_id,request_id,practice_id,facility_id,status,ready_at,authorized_by_actor_id)
select '$sameFacilityQueue',request_id,practice_id,facility_id,'Ready',ready_at,'synthetic-position-proof'
from telehealth_requests where request_id='$sameFacilityRequest';
insert into telehealth_requests(request_id,practice_id,facility_id,patient_id,status,complaint_category,triage_outcome,version,create_idempotency_key,create_fingerprint,ready_at)
select '$otherFacilityRequest',practice_id,11,'MOD-PAT-0024','Queued','migraine','TelehealthEligible',1,'th-position-other-$otherFacilityRequest','$positionHash',ready_at-interval '3 minutes'
from telehealth_requests where request_id='$($request.requestId)';
insert into telehealth_queue_entries(queue_entry_id,request_id,practice_id,facility_id,status,ready_at,authorized_by_actor_id)
select '$otherFacilityQueue',request_id,practice_id,facility_id,'Ready',ready_at,'synthetic-position-proof'
from telehealth_requests where request_id='$otherFacilityRequest';
select 'ok';
"@
  $scopedPositionStatus=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests/$($request.requestId)/status" -Headers @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId} -TimeoutSec 20
  Add-Check 'Approximate position counts an earlier same-facility request and excludes an earlier different-facility request' (
    $scopedPositionStatus.approximateRequestsAhead-eq 1-and$scopedPositionStatus.positionIsApproximate-eq$true) @{requestsAhead=$scopedPositionStatus.approximateRequestsAhead}
  $null=Scalar "update telehealth_queue_entries set status='Removed',updated_at=now() where request_id in ('$sameFacilityRequest','$otherFacilityRequest'); update telehealth_requests set status='Redirected',updated_at=now() where request_id in ('$sameFacilityRequest','$otherFacilityRequest'); select 'ok';"
  $queuedStatus=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests/$($request.requestId)/status" -Headers @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId} -TimeoutSec 20
  Add-Check 'Patient queue status is authoritative, approximate, privacy-bounded, and makes no wait promise' (
    $queuedStatus.requestStatus-eq'Queued'-and$queuedStatus.requestVersion-eq$request.version-and
    $queuedStatus.approximateRequestsAhead-eq 0-and$queuedStatus.positionIsApproximate-eq$true-and
    $queuedStatus.waitEstimateAvailable-eq$false-and$queuedStatus.realtimeAvailable-eq$false-and
    $queuedStatus.refreshAfterSeconds-ge 2-and$queuedStatus.refreshAfterSeconds-le 30-and
    $queuedStatus.detail-match'approximate'-and$queuedStatus.waitEstimateMessage-match'not available') @{
      requestsAhead=$queuedStatus.approximateRequestsAhead
      snapshotAt=$queuedStatus.snapshotAt
      refreshAfterSeconds=$queuedStatus.refreshAfterSeconds
    }
  $authorizeReplay=Post "/api/telehealth/v1/admin/requests/$($request.requestId)/authorize" $adminHeaders @{expectedVersion=$authorizedExpectedVersion}
  Add-Check 'Administrator authorization replays idempotently' ($authorizeReplay.status-eq'Queued'-and$authorizeReplay.version-eq$request.version)
  Add-Check 'Authorization key reuse with changed version is rejected' ((Status "/api/telehealth/v1/admin/requests/$($request.requestId)/authorize" $adminHeaders @{expectedVersion=($authorizedExpectedVersion+1)}) -eq 409)

  $physician=Staff 'gold-provider-01'
  if (@($physician.accessContext.facilities | Where-Object { $_.facilityId -eq 10 }).Count -ne 1 -or @($physician.accessContext.purposes) -notcontains 'treatment') { throw 'Synthetic physician lacks the required access context.' }
  $physicianHeaders=@{'X-AvenChart-Session'=$physician.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='treatment';'X-Idempotency-Key'=(Key 'th-shift')}
  $shift=Post '/api/telehealth/v1/clinician/shifts' $physicianHeaders
  Add-Check 'Eligible physician starts an active scoped shift' ($shift.status-eq'Active') @{shiftId=$shift.shiftId}
  $shiftConsultationCountBefore=[int](Scalar "select count(*) from telehealth_consultation_contexts where shift_id='$($shift.shiftId)';")

  $baseUrl=$ApiBaseUrl;$headers=$physicianHeaders;$path='/api/telehealth/v1/clinician/reservations/reserve-next'
  $statuses=1..$CallerCount|ForEach-Object -Parallel {
    $sourceHeaders=$using:headers;$targetBaseUrl=$using:baseUrl;$targetPath=$using:path
    $h=@{};foreach($entry in $sourceHeaders.GetEnumerator()){$h[$entry.Key]=$entry.Value};$h['X-Idempotency-Key']="th-reserve-$($_)-$([Guid]::NewGuid().ToString('N'))"
    try{[int](Invoke-WebRequest -Uri "$targetBaseUrl$targetPath" -Method Post -Headers $h -TimeoutSec 40).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
  } -ThrottleLimit $CallerCount
  $successes=@($statuses|Where-Object{$_-eq 200}).Count;$conflicts=@($statuses|Where-Object{$_-eq 409}).Count
  Add-Check "$CallerCount concurrent reserve-next callers produce one winner" ($successes-eq 1-and$conflicts-eq($CallerCount-1)) @{statuses=$statuses;successes=$successes;conflicts=$conflicts}

  $requestId=[Guid]$request.requestId
  $facts=(Scalar "select json_build_object('activeForRequest',(select count(*) from telehealth_reservations where request_id='$requestId' and status='Active'),'activeForClinician',(select count(*) from telehealth_reservations where clinician_staff_id=$($physician.staffId) and status='Active'),'requestStatus',(select status from telehealth_requests where request_id='$requestId'),'queueStatus',(select status from telehealth_queue_entries where request_id='$requestId'),'appointmentStatus',(select appointment.status from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$requestId'),'appointmentProvider',(select appointment.provider_id from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$requestId'))::text;")|ConvertFrom-Json
  Add-Check 'Reservation invariants assign the scheduled appointment to the one winning physician' ([int]$facts.activeForRequest-eq 1-and[int]$facts.activeForClinician-eq 1-and$facts.requestStatus-eq'Reserved'-and$facts.queueStatus-eq'Reserved'-and$facts.appointmentStatus-eq'-'-and[int]$facts.appointmentProvider-eq[int]$physician.staffId) $facts
  $reservedStatus=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests/$($request.requestId)/status" -Headers @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId} -TimeoutSec 20
  Add-Check 'Patient status reconciles reservation without exposing queue position or physician identity' (
    $reservedStatus.requestStatus-eq'Reserved'-and$reservedStatus.phase-eq'PhysicianPreparing'-and
    $null-eq$reservedStatus.approximateRequestsAhead-and$reservedStatus.positionIsApproximate-eq$false-and
    $reservedStatus.headline-eq'A physician is getting ready'-and
    (($reservedStatus|ConvertTo-Json -Depth 8)-notmatch'gold-provider-01')) @{snapshotAt=$reservedStatus.snapshotAt}

  $reservationFacts=(Scalar "select json_build_object('reservationId',(select reservation_id from telehealth_reservations where request_id='$requestId' and status='Active'),'encounterCount',(select count(*) from encounters))::text;")|ConvertFrom-Json
  $reservationId=[Guid]$reservationFacts.reservationId
  $encounterCountBefore=[int]$reservationFacts.encounterCount
  $preflightBody=@{
    expectedVersion=$reservedStatus.requestVersion
    browserSupported=$true;cameraAvailable=$true;microphoneAvailable=$true;speakerAvailable=$true
    networkQuality='good';syntheticDataConfirmed=$true
  }
  $failedPreflight=$preflightBody.Clone();$failedPreflight.cameraAvailable=$false
  $portalHeaders['X-Idempotency-Key']=Key 'th-video-preflight-failed'
  Add-Check 'A failed device preflight cannot issue a connection grant' ((Status "/api/telehealth/v1/patient/requests/$requestId/connection-grants" $portalHeaders $failedPreflight) -eq 400)

  $otherPortal=Invoke-RestMethod "$ApiBaseUrl/api/patient-portal/login" -Method Post -ContentType 'application/json' -Body (@{username='mod-pat-0024@example.test';password='PortalPass207!'}|ConvertTo-Json) -TimeoutSec 20
  $otherPortalHeaders=@{'X-AvenChart-Patient-Portal-Session'=$otherPortal.sessionId;'X-Idempotency-Key'=(Key 'th-video-cross-patient')}
  Add-Check 'Another patient cannot issue a grant for the reserved request' ((Status "/api/telehealth/v1/patient/requests/$requestId/connection-grants" $otherPortalHeaders $preflightBody) -eq 404)

  $portalHeaders['X-Idempotency-Key']=Key 'th-video-patient'
  $patientVideoKey=$portalHeaders['X-Idempotency-Key']
  $patientGrant=Post "/api/telehealth/v1/patient/requests/$requestId/connection-grants" $portalHeaders $preflightBody
  Add-Check 'Patient receives a short-lived opaque NON_PRODUCTION grant and request enters Connecting' (
    $patientGrant.requestId-eq$requestId-and$patientGrant.requestStatus-eq'Connecting'-and
    $patientGrant.requestVersion-eq($reservedStatus.requestVersion+1)-and
    $patientGrant.participantRole-eq'patient'-and$patientGrant.adapterMode-eq'NON_PRODUCTION'-and
    $patientGrant.joinCredential-match'^[A-Za-z0-9_-]{43}$'-and
    $patientGrant.recordingEnabled-eq$false-and$patientGrant.transcriptionEnabled-eq$false-and
    $patientGrant.mediaTransportEnabled-eq$false) @{sessionId=$patientGrant.sessionId;grantId=$patientGrant.grantId;expiresAt=$patientGrant.expiresAt}
  $patientReplay=Post "/api/telehealth/v1/patient/requests/$requestId/connection-grants" $portalHeaders $preflightBody
  Add-Check 'Patient connection command replays with the same ephemeral credential' (
    $patientReplay.grantId-eq$patientGrant.grantId-and$patientReplay.joinCredential-eq$patientGrant.joinCredential-and
    $patientReplay.requestVersion-eq$patientGrant.requestVersion)
  $changedPreflight=$preflightBody.Clone();$changedPreflight.networkQuality='limited'
  Add-Check 'Connection idempotency key rejects changed preflight content' ((Status "/api/telehealth/v1/patient/requests/$requestId/connection-grants" $portalHeaders $changedPreflight) -eq 409)

  $adminHeaders['X-Idempotency-Key']=Key 'th-video-nonphysician'
  Add-Check 'A non-physician staff identity cannot issue the reservation-owner grant' ((Status "/api/telehealth/v1/clinician/reservations/$reservationId/connection-grants" $adminHeaders $preflightBody) -eq 403)
  $physicianHeaders['X-Idempotency-Key']=Key 'th-video-physician'
  $physicianGrant=Post "/api/telehealth/v1/clinician/reservations/$reservationId/connection-grants" $physicianHeaders $preflightBody
  Add-Check 'Reservation-owning physician receives a distinct role-scoped grant for the same opaque session' (
    $physicianGrant.sessionId-eq$patientGrant.sessionId-and$physicianGrant.participantRole-eq'physician'-and
    $physicianGrant.grantId-ne$patientGrant.grantId-and$physicianGrant.joinCredential-ne$patientGrant.joinCredential-and
    $physicianGrant.requestStatus-eq'Connecting'-and$physicianGrant.requestVersion-eq$patientGrant.requestVersion) @{grantId=$physicianGrant.grantId}

  $credentialBytes=[Text.Encoding]::UTF8.GetBytes([string]$patientGrant.joinCredential)
  $credentialHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($credentialBytes)).ToLowerInvariant()
  $videoFacts=(Scalar @"
select json_build_object(
  'sessionCount',(select count(*) from telehealth_video_sessions where request_id='$requestId'),
  'grantCount',(select count(*) from telehealth_video_participant_grants grant_row join telehealth_video_sessions session_row using(session_id) where session_row.request_id='$requestId' and grant_row.status='Issued'),
  'patientHashCount',(select count(*) from telehealth_video_participant_grants where grant_id='$($patientGrant.grantId)' and credential_hash='$credentialHash'),
  'captureDisabled',(select not recording_enabled and not transcription_enabled and not media_transport_enabled from telehealth_video_sessions where request_id='$requestId'),
  'requestEventCount',(select count(*) from telehealth_request_events where request_id='$requestId' and action='connection-room-entered'),
  'videoEventCount',(select count(*) from telehealth_video_events where session_id='$($patientGrant.sessionId)'),
  'encounterCount',(select count(*) from encounters))::text;
"@)|ConvertFrom-Json
  Add-Check 'Database stores only the returned credential hash and one isolated session with two active participants' (
    [int]$videoFacts.sessionCount-eq 1-and[int]$videoFacts.grantCount-eq 2-and
    [int]$videoFacts.patientHashCount-eq 1-and$videoFacts.captureDisabled-eq$true) $videoFacts
  Add-Check 'Connecting is recorded once without creating a clinical encounter' (
    [int]$videoFacts.requestEventCount-eq 1-and[int]$videoFacts.videoEventCount-eq 2-and
    [int]$videoFacts.encounterCount-eq$encounterCountBefore) @{requestEvents=$videoFacts.requestEventCount;videoEvents=$videoFacts.videoEventCount;encounterDelta=([int]$videoFacts.encounterCount-$encounterCountBefore)}
  $arrivedAppointmentStatus=Scalar "select appointment.status from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$requestId';"
  Add-Check 'Patient waiting-room entry marks the linked appointment arrived' ($arrivedAppointmentStatus-eq'@') @{appointmentStatus=$arrivedAppointmentStatus}

  $connectingStatus=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests/$requestId/status" -Headers @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId} -TimeoutSec 20
  Add-Check 'Patient polling reconciles Connecting without participant or credential disclosure' (
    $connectingStatus.requestStatus-eq'Connecting'-and$connectingStatus.phase-eq'ConnectionRoom'-and
    $connectingStatus.headline-match'connection room'-and
    (($connectingStatus|ConvertTo-Json -Depth 8)-notmatch'gold-provider-01|joinCredential|grantId')) @{phase=$connectingStatus.phase;version=$connectingStatus.requestVersion}

  $consultationBody=@{
    expectedVersion=$connectingStatus.requestVersion;patientLocationState='GA'
    patientIdentityDiscussed=$true;callbackConfirmed=$true;privacyConfirmed=$true
    consentDiscussed=$true;noConcerningSymptomChange=$true;emergencyPlanConfirmed=$true
    communicationSufficient=$true;syntheticDataConfirmed=$true
  }
  $incompleteConsultation=$consultationBody.Clone();$incompleteConsultation.communicationSufficient=$false
  $physicianHeaders['X-Idempotency-Key']=Key 'th-consultation-incomplete'
  Add-Check 'Consultation start rejects any incomplete affirmative start gate' ((Status "/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start" $physicianHeaders $incompleteConsultation) -eq 400)
  $wrongStateConsultation=$consultationBody.Clone();$wrongStateConsultation.patientLocationState='CA'
  $physicianHeaders['X-Idempotency-Key']=Key 'th-consultation-wrong-state'
  Add-Check 'Consultation start rejects a state that does not match fresh patient location evidence' ((Status "/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start" $physicianHeaders $wrongStateConsultation) -eq 409)
  $adminHeaders['X-Idempotency-Key']=Key 'th-consultation-nonphysician'
  Add-Check 'A non-physician cannot start the reserved consultation lifecycle' ((Status "/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start" $adminHeaders $consultationBody) -eq 403)

  $clinicalCountsBefore=(Scalar "select json_build_object('encounters',(select count(*) from encounters),'notes',(select count(*) from clinical_notes),'signatures',(select count(*) from encounter_signatures),'prescriptions',(select count(*) from prescriptions),'billing',(select count(*) from billing),'claims',(select count(*) from claims))::text;")|ConvertFrom-Json
  $startBaseUrl=$ApiBaseUrl;$startHeaders=$physicianHeaders;$startPath="/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start";$startBody=$consultationBody|ConvertTo-Json -Compress
  $startResults=1..$CallerCount|ForEach-Object -Parallel {
    $sourceHeaders=$using:startHeaders;$targetBaseUrl=$using:startBaseUrl;$targetPath=$using:startPath;$targetBody=$using:startBody
    $h=@{};foreach($entry in $sourceHeaders.GetEnumerator()){$h[$entry.Key]=$entry.Value};$key="th-consultation-start-$($_)-$([Guid]::NewGuid().ToString('N'))";$h['X-Idempotency-Key']=$key
    try{$response=Invoke-WebRequest -Uri "$targetBaseUrl$targetPath" -Method Post -Headers $h -ContentType 'application/json' -Body $targetBody -TimeoutSec 40;[pscustomobject]@{status=[int]$response.StatusCode;key=$key;body=$response.Content}}
    catch{if($null-ne$_.Exception.Response){[pscustomobject]@{status=[int]$_.Exception.Response.StatusCode;key=$key;body=$null}}else{throw}}
  } -ThrottleLimit $CallerCount
  $startWinners=@($startResults|Where-Object{$_.status-eq 200});$boundedStartFailures=@($startResults|Where-Object{$_.status-in@(404,409)})
  Add-Check "$CallerCount concurrent consultation-start commands create one winner" ($startWinners.Count-eq 1-and$boundedStartFailures.Count-eq($CallerCount-1)) @{statuses=@($startResults.status);successes=$startWinners.Count;boundedFailures=$boundedStartFailures.Count}
  if($startWinners.Count-ne 1){throw 'Consultation-start concurrency did not produce exactly one winner.'}
  $consultationStart=$startWinners[0].body|ConvertFrom-Json
  Add-Check 'Consultation response is opaque and exposes no sequential encounter key or downstream capability' (
    $consultationStart.consultationId-match'^[0-9a-f-]{36}$'-and$consultationStart.requestStatus-eq'InConsultation'-and
    $consultationStart.appointmentStatus-eq'>'-and$consultationStart.modality-eq'SYNTHETIC_VIDEO'-and
    $consultationStart.legalEffect-eq$false-and$consultationStart.chartAccessEnabled-eq$true-and
    $consultationStart.documentationEnabled-eq$true-and$consultationStart.prescribingEnabled-eq$false-and
    $consultationStart.claimsEnabled-eq$false-and(($consultationStart|ConvertTo-Json -Depth 8)-notmatch'encounterId|joinCredential')) $consultationStart
  $physicianHeaders['X-Idempotency-Key']=$startWinners[0].key
  $consultationReplay=Post "/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start" $physicianHeaders $consultationBody
  Add-Check 'Exact consultation-start replay returns the same opaque consultation and version' (
    $consultationReplay.consultationId-eq$consultationStart.consultationId-and$consultationReplay.requestVersion-eq$consultationStart.requestVersion)
  $changedConsultation=$consultationBody.Clone();$changedConsultation.patientLocationState='CA'
  Add-Check 'Consultation idempotency key reuse with changed content is rejected' ((Status "/api/telehealth/v1/clinician/reservations/$reservationId/consultations/start" $physicianHeaders $changedConsultation) -eq 409)

  $consultationFacts=(Scalar @"
select json_build_object(
  'contextCount',(select count(*) from telehealth_consultation_contexts where request_id='$requestId'),
  'eventCount',(select count(*) from telehealth_consultation_events where request_id='$requestId' and action='consultation-started'),
  'requestEventCount',(select count(*) from telehealth_request_events where request_id='$requestId' and action='consultation-started'),
  'requestStatus',(select status from telehealth_requests where request_id='$requestId'),
  'appointmentStatus',(select appointment.status from appointments appointment join telehealth_requests request on request.appointment_id=appointment.id where request.request_id='$requestId'),
  'encounterCount',(select count(*) from encounters encounter join telehealth_consultation_contexts context on context.encounter_id=encounter.encounter where context.request_id='$requestId'),
  'encounterAppointmentMatch',(select encounter.source_appointment_id=context.appointment_id from encounters encounter join telehealth_consultation_contexts context on context.encounter_id=encounter.encounter where context.request_id='$requestId'),
  'encounterProviderMatch',(select encounter.provider_id=context.physician_staff_id from encounters encounter join telehealth_consultation_contexts context on context.encounter_id=encounter.encounter where context.request_id='$requestId'),
  'reservationStatus',(select status from telehealth_reservations where reservation_id='$reservationId'),
  'shiftStatus',(select status from telehealth_clinician_shifts where shift_id='$($shift.shiftId)'),
  'shiftConsultationCount',(select count(*) from telehealth_consultation_contexts where shift_id='$($shift.shiftId)'),
  'sessionStatus',(select status from telehealth_video_sessions where session_id='$($patientGrant.sessionId)'),
  'issuedGrantCount',(select count(*) from telehealth_video_participant_grants where session_id='$($patientGrant.sessionId)' and status='Issued'),
  'encountersTotal',(select count(*) from encounters),'notesTotal',(select count(*) from clinical_notes),
  'signaturesTotal',(select count(*) from encounter_signatures),'prescriptionsTotal',(select count(*) from prescriptions),
  'billingTotal',(select count(*) from billing),'claimsTotal',(select count(*) from claims))::text;
"@)|ConvertFrom-Json
  Add-Check 'One transaction starts the existing encounter/appointment lifecycle and closes queue access' (
    [int]$consultationFacts.contextCount-eq 1-and[int]$consultationFacts.eventCount-eq 1-and[int]$consultationFacts.requestEventCount-eq 1-and
    $consultationFacts.requestStatus-eq'InConsultation'-and$consultationFacts.appointmentStatus-eq'>'-and
    [int]$consultationFacts.encounterCount-eq 1-and$consultationFacts.encounterAppointmentMatch-eq$true-and$consultationFacts.encounterProviderMatch-eq$true-and
    $consultationFacts.reservationStatus-eq'Released'-and$consultationFacts.shiftStatus-eq'Busy'-and
    $consultationFacts.sessionStatus-eq'Ended'-and[int]$consultationFacts.issuedGrantCount-eq 0) $consultationFacts
  Add-Check 'The current shift appends exactly one consultation and remains reusable for sequential patients' (
    [int]$consultationFacts.shiftConsultationCount-eq($shiftConsultationCountBefore+1)) @{
      before=$shiftConsultationCountBefore;after=[int]$consultationFacts.shiftConsultationCount;shiftId=$shift.shiftId
    }
  Add-Check 'Consultation start creates only one encounter and zero notes, signatures, prescriptions, billing, or claims' (
    [int]$consultationFacts.encountersTotal-eq([int]$clinicalCountsBefore.encounters+1)-and
    [int]$consultationFacts.notesTotal-eq[int]$clinicalCountsBefore.notes-and
    [int]$consultationFacts.signaturesTotal-eq[int]$clinicalCountsBefore.signatures-and
    [int]$consultationFacts.prescriptionsTotal-eq[int]$clinicalCountsBefore.prescriptions-and
    [int]$consultationFacts.billingTotal-eq[int]$clinicalCountsBefore.billing-and
    [int]$consultationFacts.claimsTotal-eq[int]$clinicalCountsBefore.claims) $consultationFacts
  Add-Check 'Consultation context and start event reject destructive evidence mutation' (
    (Sql-Fails "update telehealth_consultation_contexts set version=version+1 where request_id='$requestId';") -and
    (Sql-Fails "delete from telehealth_consultation_events where request_id='$requestId';"))

  $workspaceHeaders=$physicianHeaders.Clone();$workspaceHeaders.Remove('X-Idempotency-Key')
  $workspaceHttp=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/workspace" -Headers $workspaceHeaders -TimeoutSec 20
  $workspace=$workspaceHttp.Content|ConvertFrom-Json
  $workspaceJson=$workspace|ConvertTo-Json -Depth 10 -Compress
  Add-Check 'Owning physician receives the bounded active-consultation projection and empty unsigned draft' (
    [int]$workspaceHttp.StatusCode-eq 200-and$workspace.consultationId-eq$consultationStart.consultationId-and
    $workspace.consultationStatus-eq'InConsultation'-and[int]$workspace.consultationVersion-eq 1-and$null-eq$workspace.mediaEndedAt-and
    $workspace.modality-eq'SYNTHETIC_VIDEO'-and$workspace.readOnly-eq$true-and
    $workspace.patient.displayName-and$workspace.patient.dateOfBirth-and[int]$workspace.patient.age-ge 18-and[int]$workspace.patient.age-le 120-and
    $workspace.visit.patientLocationState-eq'GA'-and$workspace.visit.complaintCategory-eq'sleep'-and
    @($workspace.allergies).Count-le 20-and@($workspace.medications).Count-le 20-and@($workspace.problems).Count-le 20-and
    $workspace.documentationEnabled-eq$true-and[int]$workspace.documentation.version-eq 0-and
    $null-eq$workspace.documentation.savedAt-and$workspace.documentation.isLocked-eq$false-and
    $workspace.documentation.isSigned-eq$false-and$workspace.documentation.isFinal-eq$false-and
    $workspace.prescribingEnabled-eq$false-and$workspace.claimsEnabled-eq$false-and$workspace.completionEnabled-eq$false) @{
      allergyCount=@($workspace.allergies).Count;medicationCount=@($workspace.medications).Count;problemCount=@($workspace.problems).Count;readOnly=$workspace.readOnly
    }
  Add-Check 'Workspace response omits canonical keys and excluded identity, insurance, financial, and general-chart fields' (
    $workspaceJson-notmatch '"(patientId|encounterId|appointmentId|requestId|insurance|policyNumber|groupNumber|email|streetAddress|employer|guardian|careTeam|document|message|laboratory|priorNote|diagnoses|comments)"') @{responseBytes=[Text.Encoding]::UTF8.GetByteCount($workspaceJson)}
  $cacheControl=($workspaceHttp.Headers['Cache-Control'] -join ',')
  $pragma=($workspaceHttp.Headers['Pragma'] -join ',')
  Add-Check 'Workspace response is explicitly non-cacheable' (
    $cacheControl-match'no-store'-and$cacheControl-match'private'-and$pragma-match'no-cache'-and($workspaceHttp.Headers['Expires'] -join ',')-eq'0') @{cacheControl=$cacheControl;pragma=$pragma}

  $existingProofPhysicianCount=[int](Scalar "select count(*) from auth_accounts where username='gold-provider-02';")
  if($existingProofPhysicianCount-eq 0){
    $null=Scalar @"
insert into auth_accounts(username,display_name,role,staff_id,active,password_salt,password_hash)
select 'gold-provider-02','Jordan Morris','provider',102,true,password_salt,password_hash
from auth_accounts where username='gold-provider-01';
insert into access_user_memberships(user_value,user_name,group_value,group_name,staff_id)
select 'gold-provider-02','Jordan Morris','clin','Clinicians',102
where not exists(select 1 from access_user_memberships where user_value='gold-provider-02' and group_value='clin');
insert into auth_principal_facility_grants(username,facility_id,is_default,active,granted_by,updated_by)
values('gold-provider-02',10,true,true,'telehealth-runtime-proof','telehealth-runtime-proof')
on conflict(username,facility_id) do update set active=true,is_default=true,updated_at=now(),updated_by='telehealth-runtime-proof';
insert into auth_principal_purpose_of_use_grants(username,purpose_of_use,active,granted_by,updated_by)
values('gold-provider-02','treatment',true,'telehealth-runtime-proof','telehealth-runtime-proof')
on conflict(username,purpose_of_use) do update set active=true,updated_at=now(),updated_by='telehealth-runtime-proof';
select 'ok';
"@
    $proofPhysicianAccountCreated=$true
  }
  $otherPhysician=Staff 'gold-provider-02'
  $otherPhysicianHeaders=@{'X-AvenChart-Session'=$otherPhysician.sessionId;'X-AvenChart-Facility-Id'='10';'X-AvenChart-Purpose-Of-Use'='treatment'}
  $otherPhysicianStatus=try{[int](Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/workspace" -Headers $otherPhysicianHeaders -TimeoutSec 20).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
  Add-Check 'A different physician receives opaque not-found for another physician consultation' ($otherPhysicianStatus-eq 404) @{status=$otherPhysicianStatus}

  $draftPath="/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/documentation/draft"
  $noteCountBeforeDraft=[int](Scalar "select count(*) from clinical_notes;")
  $firstDraft=@{expectedVersion=0;subjective='Synthetic physician-entered sleep history.';objective=$null;assessment=$null;plan=$null}
  Add-Check 'A different physician receives opaque not-found and cannot create another physician draft' (
    (Put-Status $draftPath $otherPhysicianHeaders $firstDraft)-eq 404-and[int](Scalar "select count(*) from clinical_notes;")-eq$noteCountBeforeDraft)
  Add-Check 'An empty SOAP draft is rejected without a canonical note delta' (
    (Put-Status $draftPath $workspaceHeaders @{expectedVersion=0;subjective=' ';objective=$null;assessment=$null;plan=$null})-eq 400-and
    [int](Scalar "select count(*) from clinical_notes;")-eq$noteCountBeforeDraft)
  $savedDraft1=Put $draftPath $workspaceHeaders $firstDraft
  $savedDraft1Json=$savedDraft1|ConvertTo-Json -Depth 8 -Compress
  Add-Check 'Owning physician appends canonical unsigned draft version one with server author and time' (
    [int]$savedDraft1.version-eq 1-and$savedDraft1.savedAt-and$savedDraft1.savedBy-eq'gold-provider-01'-and
    $savedDraft1.isLocked-eq$false-and$savedDraft1.isSigned-eq$false-and$savedDraft1.isFinal-eq$false-and
    $savedDraft1.subjective-eq$firstDraft.subjective-and
    [int](Scalar "select count(*) from clinical_notes;")-eq($noteCountBeforeDraft+1)-and
    $savedDraft1Json-notmatch'patientId|encounterId|appointmentId|requestId|noteId|supersedes|versions') @{version=$savedDraft1.version;savedBy=$savedDraft1.savedBy}
  Add-Check 'A stale expected version conflicts without overwriting or appending' (
    (Put-Status $draftPath $workspaceHeaders @{expectedVersion=0;subjective='Stale synthetic overwrite.';objective=$null;assessment=$null;plan=$null})-eq 409-and
    [int](Scalar "select count(*) from clinical_notes;")-eq($noteCountBeforeDraft+1))
  $secondDraft=@{expectedVersion=1;subjective='Synthetic physician-entered sleep history.';objective='Synthetic remote observation limitation documented.';assessment=$null;plan=$null}
  $savedDraft2=Put $draftPath $workspaceHeaders $secondDraft
  $canonicalDraftFacts=(Scalar @"
select json_build_object(
  'count',count(*),'versions',json_agg(version order by version),
  'supersedes',json_agg(supersedes_note_id order by version),
  'authors',json_agg(saved_by order by version),
  'encounters',count(distinct encounter)
)::text from clinical_notes where encounter=(select encounter_id from telehealth_consultation_contexts where consultation_id='$($consultationStart.consultationId)');
"@)|ConvertFrom-Json
  Add-Check 'Second explicit save appends a linked canonical version without a second chart' (
    [int]$savedDraft2.version-eq 2-and[int]$canonicalDraftFacts.count-eq 2-and
    (@($canonicalDraftFacts.versions)-join',')-eq'1,2'-and$null-eq$canonicalDraftFacts.supersedes[0]-and
    $null-ne$canonicalDraftFacts.supersedes[1]-and[int]$canonicalDraftFacts.encounters-eq 1-and
    @($canonicalDraftFacts.authors|Where-Object{$_-ne'gold-provider-01'}).Count-eq 0) $canonicalDraftFacts
  $workspaceAfterDraft=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/workspace" -Headers $workspaceHeaders -TimeoutSec 20
  Add-Check 'Workspace reload returns only the current bounded draft and no prior-version content' (
    [int]$workspaceAfterDraft.documentation.version-eq 2-and
    $workspaceAfterDraft.documentation.objective-eq$secondDraft.objective-and
    (($workspaceAfterDraft.documentation|ConvertTo-Json -Depth 8)-notmatch'"(id|versions|supersedes|history)"\s*:'))

  $wrapUpPath="/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/wrap-up"
  $wrapUpBody=@{expectedVersion=1;syntheticSessionEndedConfirmed=$true;documentationStillIncompleteAcknowledged=$true;wrapUpResponsibilityAcknowledged=$true}
  $wrapUpEventsBefore=[int](Scalar "select count(*) from telehealth_consultation_events where consultation_id='$($consultationStart.consultationId)' and action='consultation-wrap-up-entered';")
  $wrapUpNoteCountBefore=[int](Scalar "select count(*) from clinical_notes;")
  $otherWrapHeaders=$otherPhysicianHeaders.Clone();$otherWrapHeaders['X-Idempotency-Key']=Key 'th-wrap-other'
  Add-Check 'A different physician receives opaque not-found and cannot enter another physician wrap-up' (
    (Status $wrapUpPath $otherWrapHeaders $wrapUpBody)-eq 404-and
    [int](Scalar "select count(*) from telehealth_consultation_events where consultation_id='$($consultationStart.consultationId)' and action='consultation-wrap-up-entered';")-eq$wrapUpEventsBefore)
  $incompleteWrapHeaders=$workspaceHeaders.Clone();$incompleteWrapHeaders['X-Idempotency-Key']=Key 'th-wrap-incomplete'
  Add-Check 'Wrap-up requires every unfinished-work acknowledgment without a lifecycle delta' (
    (Status $wrapUpPath $incompleteWrapHeaders @{expectedVersion=1;syntheticSessionEndedConfirmed=$true;documentationStillIncompleteAcknowledged=$false;wrapUpResponsibilityAcknowledged=$true})-eq 400-and
    [int](Scalar "select count(*) from telehealth_consultation_events where consultation_id='$($consultationStart.consultationId)' and action='consultation-wrap-up-entered';")-eq$wrapUpEventsBefore)

  $wrapUpHeaders=$workspaceHeaders.Clone();$wrapUpHeaders['X-Idempotency-Key']=Key 'th-wrap-up'
  $wrapBaseUrl=$ApiBaseUrl;$wrapHeaders=$wrapUpHeaders;$wrapPath=$wrapUpPath;$wrapBody=$wrapUpBody|ConvertTo-Json -Compress
  $wrapStatuses=1..$CallerCount|ForEach-Object -Parallel {
    $sourceHeaders=$using:wrapHeaders;$targetBaseUrl=$using:wrapBaseUrl;$targetPath=$using:wrapPath;$body=$using:wrapBody
    $h=@{};foreach($entry in $sourceHeaders.GetEnumerator()){$h[$entry.Key]=$entry.Value}
    try{[int](Invoke-WebRequest -Uri "$targetBaseUrl$targetPath" -Method Post -Headers $h -ContentType 'application/json' -Body $body -TimeoutSec 40).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
  } -ThrottleLimit $CallerCount
  Add-Check "$CallerCount concurrent exact wrap-up replays all succeed through one transition" (
    @($wrapStatuses|Where-Object{$_-eq 200}).Count-eq$CallerCount) @{statuses=$wrapStatuses}
  $wrapUpReplayHttp=Invoke-WebRequest -Uri "$ApiBaseUrl$wrapUpPath" -Method Post -Headers $wrapUpHeaders -ContentType 'application/json' -Body ($wrapUpBody|ConvertTo-Json -Compress) -TimeoutSec 40
  $wrapUpReplay=$wrapUpReplayHttp.Content|ConvertFrom-Json
  Add-Check 'Wrap-up exact replay returns the original bounded unfinished state' (
    [int]$wrapUpReplay.version-eq 2-and$wrapUpReplay.consultationStatus-eq'MediaEnded'-and
    $wrapUpReplay.requestStatus-eq'WrapUp'-and$wrapUpReplay.shiftStatus-eq'WrapUp'-and
    $wrapUpReplay.appointmentStatus-eq'>'-and$wrapUpReplay.documentationEnabled-eq$true-and
    $wrapUpReplay.completionEnabled-eq$false-and$wrapUpReplay.clinicianAvailableForNewWork-eq$false-and
    (($wrapUpReplay|ConvertTo-Json -Depth 8)-notmatch'"(requestId|shiftId|appointmentId|encounterId|patientId|disposition)"\s*:')) $wrapUpReplay
  $wrapUpCacheControl=($wrapUpReplayHttp.Headers['Cache-Control'] -join ',')
  Add-Check 'Wrap-up response is explicitly non-cacheable' (
    $wrapUpCacheControl-match'no-store'-and$wrapUpCacheControl-match'private'-and
    ($wrapUpReplayHttp.Headers['Pragma'] -join ',')-match'no-cache'-and
    ($wrapUpReplayHttp.Headers['Expires'] -join ',')-eq'0') @{cacheControl=$wrapUpCacheControl}
  $wrapUpFacts=(Scalar @"
select json_build_object(
  'consultationStatus',context.status,'consultationVersion',context.version,'mediaEndedAt',context.media_ended_at,
  'requestStatus',request.status,'requestVersion',request.version,'shiftStatus',shift.status,
  'reservationStatus',reservation.status,'sessionStatus',session.status,'appointmentStatus',appointment.status,
  'consultationEvents',(select count(*) from telehealth_consultation_events event where event.consultation_id=context.consultation_id and event.action='consultation-wrap-up-entered'),
  'requestEvents',(select count(*) from telehealth_request_events event where event.request_id=context.request_id and event.action='consultation-wrap-up-entered')
)::text
from telehealth_consultation_contexts context
join telehealth_requests request on request.request_id=context.request_id
join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
join telehealth_video_sessions session on session.session_id=context.session_id
join appointments appointment on appointment.id=context.appointment_id
where context.consultation_id='$($consultationStart.consultationId)';
"@)|ConvertFrom-Json
  Add-Check 'One atomic wrap-up transition retains unfinished appointment, encounter ownership, and released room state' (
    $wrapUpFacts.consultationStatus-eq'MediaEnded'-and[int]$wrapUpFacts.consultationVersion-eq 2-and$wrapUpFacts.mediaEndedAt-and
    $wrapUpFacts.requestStatus-eq'WrapUp'-and$wrapUpFacts.shiftStatus-eq'WrapUp'-and
    $wrapUpFacts.reservationStatus-eq'Released'-and$wrapUpFacts.sessionStatus-eq'Ended'-and$wrapUpFacts.appointmentStatus-eq'>'-and
    [int]$wrapUpFacts.consultationEvents-eq 1-and[int]$wrapUpFacts.requestEvents-eq 1-and
    [int](Scalar "select count(*) from clinical_notes;")-eq$wrapUpNoteCountBefore) $wrapUpFacts
  Add-Check 'Wrap-up preserves immutable start facts and append-only lifecycle events' (
    (Sql-Fails "update telehealth_consultation_contexts set patient_location_state='FL' where consultation_id='$($consultationStart.consultationId)';")-and
    (Sql-Fails "update telehealth_consultation_contexts set status='Started',version=version+1,media_ended_at=null where consultation_id='$($consultationStart.consultationId)';")-and
    (Sql-Fails "delete from telehealth_consultation_events where consultation_id='$($consultationStart.consultationId)' and action='consultation-wrap-up-entered';"))
  $changedWrapHeaders=$workspaceHeaders.Clone();$changedWrapHeaders['X-Idempotency-Key']=$wrapUpHeaders['X-Idempotency-Key']
  Add-Check 'Wrap-up idempotency key rejects changed content without another event' (
    (Status $wrapUpPath $changedWrapHeaders (@{expectedVersion=2;syntheticSessionEndedConfirmed=$true;documentationStillIncompleteAcknowledged=$true;wrapUpResponsibilityAcknowledged=$true}))-eq 409-and
    [int](Scalar "select count(*) from telehealth_consultation_events where consultation_id='$($consultationStart.consultationId)' and action='consultation-wrap-up-entered';")-eq 1)
  $staleWrapHeaders=$workspaceHeaders.Clone();$staleWrapHeaders['X-Idempotency-Key']=Key 'th-wrap-stale'
  Add-Check 'A stale competing wrap-up command cannot repeat or partially change the transition' (
    (Status $wrapUpPath $staleWrapHeaders $wrapUpBody)-eq 409-and
    [int](Scalar "select count(*) from telehealth_consultation_events where consultation_id='$($consultationStart.consultationId)' and action='consultation-wrap-up-entered';")-eq 1)
  $reserveWhileWrapHeaders=$physicianHeaders.Clone();$reserveWhileWrapHeaders['X-Idempotency-Key']=Key 'th-reserve-wrap'
  Add-Check 'A physician in wrap-up cannot reserve new work' ((Status '/api/telehealth/v1/clinician/reservations/reserve-next' $reserveWhileWrapHeaders)-eq 409)
  $wrapWorkspace=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/workspace" -Headers $workspaceHeaders -TimeoutSec 20
  Add-Check 'Owning physician retains workspace and unsigned draft access during unfinished wrap-up' (
    $wrapWorkspace.consultationStatus-eq'WrapUp'-and[int]$wrapWorkspace.consultationVersion-eq 2-and$wrapWorkspace.mediaEndedAt-and
    [int]$wrapWorkspace.documentation.version-eq 2-and$wrapWorkspace.completionEnabled-eq$false)
  $thirdDraft=@{expectedVersion=2;subjective='Synthetic physician-entered sleep history.';objective='Synthetic remote observation limitation documented.';assessment=$null;plan='Synthetic unfinished wrap-up note.'}
  $savedDraft3=Put $draftPath $workspaceHeaders $thirdDraft
  Add-Check 'Owning physician can append another canonical unsigned draft during wrap-up' (
    [int]$savedDraft3.version-eq 3-and$savedDraft3.plan-eq$thirdDraft.plan-and
    [int](Scalar "select count(*) from clinical_notes;")-eq($wrapUpNoteCountBefore+1)) @{version=$savedDraft3.version}

  $completionPath="/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/completion-prerequisites"
  $initialCompletionHttp=Invoke-WebRequest "$ApiBaseUrl$completionPath" -Headers $workspaceHeaders -TimeoutSec 30
  $initialCompletion=$initialCompletionHttp.Content|ConvertFrom-Json
  Add-Check 'Initial completion review reports only structural presence and stable product blockers' (
    [int]$initialCompletionHttp.StatusCode-eq 200-and[int]$initialCompletion.documentation.version-eq 3-and
    $initialCompletion.documentation.hasAnyContent-eq$true-and$initialCompletion.documentation.subjectivePresent-eq$true-and
    $null-eq$initialCompletion.safetyDisposition-and$null-eq$initialCompletion.pharmacyChoice-and
    $initialCompletion.structuralEvidencePresent-eq$false-and
    @($initialCompletion.productBlockers)-contains'SAFETY_DISPOSITION_DRAFT_MISSING'-and
    @($initialCompletion.productBlockers)-contains'FINAL_CLINICAL_REVIEW_NOT_RECORDED'-and
    @($initialCompletion.productBlockers)-contains'SIGNATURE_FINALIZATION_NOT_IMPLEMENTED'-and
    @($initialCompletion.productBlockers)-contains'ATOMIC_DOWNSTREAM_OWNERSHIP_NOT_IMPLEMENTED'-and
    $initialCompletion.signingEnabled-eq$false-and$initialCompletion.completionEnabled-eq$false-and
    $initialCompletion.patientDeliveryEnabled-eq$false-and$initialCompletion.downstreamCreationEnabled-eq$false-and
    (($initialCompletion|ConvertTo-Json -Depth 12)-notmatch'Synthetic physician-entered sleep history|Synthetic unfinished wrap-up note|patientId|encounterId|appointmentId|requestId')) @{blockers=@($initialCompletion.productBlockers)}
  $completionCacheControl=($initialCompletionHttp.Headers['Cache-Control']-join',')
  Add-Check 'Completion-prerequisites review is private, non-cacheable, and owner-only' (
    $completionCacheControl-match'no-store'-and$completionCacheControl-match'private'-and
    ($initialCompletionHttp.Headers['Pragma']-join',')-match'no-cache'-and($initialCompletionHttp.Headers['Expires']-join',')-eq'0'-and
    (Get-Status $completionPath $otherPhysicianHeaders)-eq 404) @{cacheControl=$completionCacheControl}

  $pharmacyPath="/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/pharmacy-choices"
  $choicePath="/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/pharmacy-choice"
  $preferredEntryId='00000000-0000-4000-8000-000000001001'
  $changedEntryId='00000000-0000-4000-8000-000000001002'
  $preferenceId=[Guid]::NewGuid()
  $null=Scalar @"
insert into telehealth_patient_pharmacy_preferences(
 preference_id,practice_id,facility_id,patient_id,directory_entry_id,directory_source,directory_version,
 preference_status,recorded_at,recorded_by_actor_id)
select '$preferenceId','avenchart-synthetic-practice',10,request.patient_id,'$preferredEntryId',
       'avenchart-synthetic-pharmacy-directory','2026.08.27.1','Added',now(),'telehealth-runtime-proof'
from telehealth_consultation_contexts context
join telehealth_requests request on request.request_id=context.request_id
where context.consultation_id='$($consultationStart.consultationId)';
select 'ok';
"@
  Add-Check 'Approximate-distance search requires an affirmative entered-origin acknowledgment' (
    (Get-Status "$pharmacyPath`?state=GA&originPostalCode=30303&limit=25" $workspaceHeaders)-eq 400)
  $pharmacyHttp=Invoke-WebRequest "$ApiBaseUrl$pharmacyPath`?state=GA&originPostalCode=30303&locationSearchAcknowledged=true&limit=25" -Headers $workspaceHeaders -TimeoutSec 30
  $pharmacyWorkspace=$pharmacyHttp.Content|ConvertFrom-Json
  $preferredPharmacy=@($pharmacyWorkspace.pharmacies|Where-Object{$_.directoryEntryId-eq$preferredEntryId})|Select-Object -First 1
  $pharmacyJson=$pharmacyWorkspace|ConvertTo-Json -Depth 12 -Compress
  Add-Check 'Owning physician receives neutral synthetic directory facts, associated chart preference, and approximate postal distance' (
    [int]$pharmacyHttp.StatusCode-eq 200-and$pharmacyWorkspace.adapterMode-eq'NON_PRODUCTION'-and
    $pharmacyWorkspace.datasetId-eq'avenchart-synthetic-pharmacy-directory'-and
    $pharmacyWorkspace.datasetVersion-eq'2026.08.27.1'-and[int]$pharmacyWorkspace.chartPreferenceCount-eq 1-and
    $null-ne$preferredPharmacy-and$preferredPharmacy.isChartPreferred-eq$true-and
    $null-ne$preferredPharmacy.approximateDistanceMiles-and$preferredPharmacy.electronicRoutingCapability-eq'NON_PRODUCTION_ONLY'-and
    $pharmacyWorkspace.prescriptionEnabled-eq$false-and$pharmacyWorkspace.transmissionEnabled-eq$false-and
    $pharmacyJson-notmatch'latitude|longitude|patientId|encounterId|appointmentId|requestId') @{count=@($pharmacyWorkspace.pharmacies).Count;distance=$preferredPharmacy.approximateDistanceMiles}
  $pharmacyCacheControl=($pharmacyHttp.Headers['Cache-Control']-join',')
  Add-Check 'Pharmacy-choice workspace is explicitly non-cacheable' (
    $pharmacyCacheControl-match'no-store'-and$pharmacyCacheControl-match'private'-and
    ($pharmacyHttp.Headers['Pragma']-join',')-match'no-cache'-and($pharmacyHttp.Headers['Expires']-join',')-eq'0') @{cacheControl=$pharmacyCacheControl}
  Add-Check 'A different physician receives opaque not-found for pharmacy choices' (
    (Get-Status "$pharmacyPath`?state=GA&limit=25" $otherPhysicianHeaders)-eq 404)

  $downstreamBefore=(Scalar "select json_build_object('prescriptions',(select count(*) from prescriptions),'medications',(select count(*) from medications),'signatures',(select count(*) from encounter_signatures),'billing',(select count(*) from billing),'claims',(select count(*) from claims),'messages',(select count(*) from messages),'portalMailbox',(select count(*) from portal_mailbox_messages),'integrationOutbox',(select count(*) from integration_outbox),'integrationInbox',(select count(*) from integration_inbox))::text;")|ConvertFrom-Json
  $choiceHeaders=$workspaceHeaders.Clone();$choiceHeaders['X-Idempotency-Key']=Key 'th-pharmacy-choice'
  $choiceBody=@{expectedVersion=0;directoryEntryId=$preferredEntryId;patientChoiceConfirmed=$true;syntheticDataConfirmed=$true}
  $choiceBaseUrl=$ApiBaseUrl;$choiceParallelHeaders=$choiceHeaders;$choiceParallelPath=$choicePath;$choiceParallelBody=$choiceBody|ConvertTo-Json -Compress
  $choiceStatuses=1..$CallerCount|ForEach-Object -Parallel {
    $sourceHeaders=$using:choiceParallelHeaders;$targetBaseUrl=$using:choiceBaseUrl;$targetPath=$using:choiceParallelPath;$body=$using:choiceParallelBody
    $headers=@{};foreach($entry in $sourceHeaders.GetEnumerator()){$headers[$entry.Key]=$entry.Value}
    try{[int](Invoke-WebRequest -Uri "$targetBaseUrl$targetPath" -Method Put -Headers $headers -ContentType 'application/json' -Body $body -TimeoutSec 40).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
  } -ThrottleLimit $CallerCount
  Add-Check "$CallerCount concurrent exact pharmacy-choice replays all return one recorded version" (
    @($choiceStatuses|Where-Object{$_-eq 200}).Count-eq$CallerCount) @{statuses=$choiceStatuses}
  $choiceReplay=Put $choicePath $choiceHeaders $choiceBody
  Add-Check 'Exact pharmacy-choice replay returns the original unsigned non-transmitted destination' (
    [int]$choiceReplay.version-eq 1-and$choiceReplay.directoryEntryId-eq$preferredEntryId-and
    $choiceReplay.patientChoiceConfirmed-eq$true-and$choiceReplay.prescriptionCreated-eq$false-and$choiceReplay.transmitted-eq$false-and
    (($choiceReplay|ConvertTo-Json -Depth 10)-notmatch'patientId|encounterId|requestId|actorId|medication|drug|claim')) $choiceReplay
  Add-Check 'Pharmacy-choice idempotency key rejects changed content without another version' (
    (Put-Status $choicePath $choiceHeaders @{expectedVersion=1;directoryEntryId=$changedEntryId;patientChoiceConfirmed=$true;syntheticDataConfirmed=$true})-eq 409-and
    [int](Scalar "select count(*) from telehealth_consultation_pharmacy_choice_versions where consultation_id='$($consultationStart.consultationId)';")-eq 1)
  $staleChoiceHeaders=$workspaceHeaders.Clone();$staleChoiceHeaders['X-Idempotency-Key']=Key 'th-pharmacy-stale'
  Add-Check 'A stale pharmacy-choice writer cannot append a partial destination or event' (
    (Put-Status $choicePath $staleChoiceHeaders $choiceBody)-eq 409-and
    [int](Scalar "select count(*) from telehealth_consultation_pharmacy_choice_events where consultation_id='$($consultationStart.consultationId)';")-eq 1)
  $changedChoiceHeaders=$workspaceHeaders.Clone();$changedChoiceHeaders['X-Idempotency-Key']=Key 'th-pharmacy-change'
  $changedChoice=Put $choicePath $changedChoiceHeaders @{expectedVersion=1;directoryEntryId=$changedEntryId;patientChoiceConfirmed=$true;syntheticDataConfirmed=$true}
  $choiceFacts=(Scalar @"
select json_build_object(
 'versions',(select json_agg(version order by version) from telehealth_consultation_pharmacy_choice_versions where consultation_id='$($consultationStart.consultationId)'),
 'events',(select json_agg(action order by aggregate_version) from telehealth_consultation_pharmacy_choice_events where consultation_id='$($consultationStart.consultationId)'),
 'sources',(select count(distinct directory_source||':'||directory_version) from telehealth_consultation_pharmacy_choice_versions where consultation_id='$($consultationStart.consultationId)'),
 'confirmed',(select bool_and(patient_choice_confirmed) from telehealth_consultation_pharmacy_choice_versions where consultation_id='$($consultationStart.consultationId)'),
 'consultationStatus',(select status from telehealth_consultation_contexts where consultation_id='$($consultationStart.consultationId)'),
 'requestStatus',(select request.status from telehealth_requests request join telehealth_consultation_contexts context on context.request_id=request.request_id where context.consultation_id='$($consultationStart.consultationId)')
)::text;
"@)|ConvertFrom-Json
  Add-Check 'A patient-confirmed change appends immutable versioned provenance without completing the visit' (
    [int]$changedChoice.version-eq 2-and(@($choiceFacts.versions)-join',')-eq'1,2'-and
    (@($choiceFacts.events)-join',')-eq'DestinationRecorded,DestinationChanged'-and[int]$choiceFacts.sources-eq 1-and
    $choiceFacts.confirmed-eq$true-and$choiceFacts.consultationStatus-eq'MediaEnded'-and$choiceFacts.requestStatus-eq'WrapUp') $choiceFacts
  Add-Check 'Pharmacy preference, choice versions, and choice events reject destructive mutation' (
    (Sql-Fails "delete from telehealth_patient_pharmacy_preferences where preference_id='$preferenceId';")-and
    (Sql-Fails "update telehealth_consultation_pharmacy_choice_versions set pharmacy_name='Changed' where consultation_id='$($consultationStart.consultationId)';")-and
    (Sql-Fails "delete from telehealth_consultation_pharmacy_choice_events where consultation_id='$($consultationStart.consultationId)';"))
  $downstreamAfter=(Scalar "select json_build_object('prescriptions',(select count(*) from prescriptions),'medications',(select count(*) from medications),'signatures',(select count(*) from encounter_signatures),'billing',(select count(*) from billing),'claims',(select count(*) from claims),'messages',(select count(*) from messages),'portalMailbox',(select count(*) from portal_mailbox_messages),'integrationOutbox',(select count(*) from integration_outbox),'integrationInbox',(select count(*) from integration_inbox))::text;")|ConvertFrom-Json
  Add-Check 'Directory search and destination drafts create no medication, prescription, signature, billing, claim, communication, or integration delta' (
    ($downstreamBefore|ConvertTo-Json -Compress)-eq($downstreamAfter|ConvertTo-Json -Compress)) @{before=$downstreamBefore;after=$downstreamAfter}

  $prescriptionPath="/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/prescription-preparation-draft"
  $prescriptionHttp=Invoke-WebRequest "$ApiBaseUrl$prescriptionPath" -Headers $workspaceHeaders -TimeoutSec 30
  $prescriptionWorkspace=$prescriptionHttp.Content|ConvertFrom-Json
  $prescriptionJson=$prescriptionWorkspace|ConvertTo-Json -Depth 12 -Compress
  Add-Check 'Owning physician receives an empty, neutral prescription-preparation workspace with every consequential capability disabled' (
    [int]$prescriptionHttp.StatusCode-eq 200-and$prescriptionWorkspace.consultationStatus-eq'MediaEnded'-and
    [int]$prescriptionWorkspace.currentPharmacyChoiceVersion-eq 2-and$null-eq$prescriptionWorkspace.currentDraft-and
    @($prescriptionWorkspace.catalogResults).Count-eq 0-and$prescriptionWorkspace.adapterMode-eq'NON_PRODUCTION'-and
    $prescriptionWorkspace.canonicalModelVersion-eq'AVENCHART_ERX_PREPARATION_V1'-and
    $prescriptionWorkspace.intendedStandard-eq'NCPDP_SCRIPT_2017071'-and
    $prescriptionWorkspace.safetyCheckEnabled-eq$false-and$prescriptionWorkspace.signingEnabled-eq$false-and
    $prescriptionWorkspace.prescriptionCreationEnabled-eq$false-and$prescriptionWorkspace.transmissionEnabled-eq$false-and
    $prescriptionWorkspace.patientDeliveryEnabled-eq$false-and$prescriptionWorkspace.completionEnabled-eq$false-and
    $prescriptionJson-notmatch'patientId|encounterId|appointmentId|requestId|signatureId|claimId') $prescriptionWorkspace
  $prescriptionCacheControl=($prescriptionHttp.Headers['Cache-Control']-join',')
  Add-Check 'Prescription-preparation workspace is explicitly non-cacheable and owner-only' (
    $prescriptionCacheControl-match'no-store'-and$prescriptionCacheControl-match'private'-and
    ($prescriptionHttp.Headers['Pragma']-join',')-match'no-cache'-and($prescriptionHttp.Headers['Expires']-join',')-eq'0'-and
    (Get-Status "$prescriptionPath`?query=metformin" $otherPhysicianHeaders)-eq 404) @{cacheControl=$prescriptionCacheControl}
  Add-Check 'Medication search rejects short input and excludes a controlled catalog result' (
    (Get-Status "$prescriptionPath`?query=x" $workspaceHeaders)-eq 400-and
    @((Invoke-RestMethod "$ApiBaseUrl$prescriptionPath`?query=oxycodone" -Headers $workspaceHeaders -TimeoutSec 30).catalogResults).Count-eq 0)
  $catalogWorkspace=Invoke-RestMethod "$ApiBaseUrl$prescriptionPath`?query=metformin" -Headers $workspaceHeaders -TimeoutSec 30
  $catalogItem=@($catalogWorkspace.catalogResults|Where-Object{$_.rxNormCode-eq'860975'})|Select-Object -First 1
  Add-Check 'Neutral search returns the selected non-controlled catalog fact without dose or direction recommendations' (
    $null-ne$catalogItem-and$catalogItem.displayName-eq'Metformin 500 mg tablet'-and
    (($catalogWorkspace.catalogResults|ConvertTo-Json -Depth 8 -Compress)-notmatch'doseAmount|frequency|quantityValue|durationDays|directions|recommended')) $catalogItem

  $prescriptionDownstreamBefore=$downstreamAfter
  $prescriptionBody=@{expectedVersion=0;rxNormCode='860975';doseAmount=1;doseUnit='tablet';frequency='once daily (synthetic)';quantityValue=7;quantityUnit='tablet';durationDays=7;refills=0;indication='Synthetic indication authored by physician.';directions='Take one synthetic tablet once daily for seven days.';medicationListReviewed=$true;allergyListReviewed=$true;adequateEvaluationCompleted=$true;syntheticDataConfirmed=$true}
  $invalidPrescriptionHeaders=$workspaceHeaders.Clone();$invalidPrescriptionHeaders['X-Idempotency-Key']=Key 'th-prescription-review'
  $invalidPrescriptionBody=$prescriptionBody.Clone();$invalidPrescriptionBody.medicationListReviewed=$false
  Add-Check 'Prescription preparation rejects missing medication review without persisting evidence' (
    (Put-Status $prescriptionPath $invalidPrescriptionHeaders $invalidPrescriptionBody)-eq 400-and
    [int](Scalar "select count(*) from telehealth_consultation_prescription_draft_versions where consultation_id='$($consultationStart.consultationId)';")-eq 0)
  $controlledPrescriptionHeaders=$workspaceHeaders.Clone();$controlledPrescriptionHeaders['X-Idempotency-Key']=Key 'th-prescription-controlled'
  $controlledPrescriptionBody=$prescriptionBody.Clone();$controlledPrescriptionBody.rxNormCode='1049621'
  Add-Check 'Prescription preparation rejects a controlled catalog code without persisting evidence' (
    (Put-Status $prescriptionPath $controlledPrescriptionHeaders $controlledPrescriptionBody)-eq 400-and
    [int](Scalar "select count(*) from telehealth_consultation_prescription_draft_events where consultation_id='$($consultationStart.consultationId)';")-eq 0)
  $prescriptionHeaders=$workspaceHeaders.Clone();$prescriptionHeaders['X-Idempotency-Key']=Key 'th-prescription-draft'
  $prescriptionBaseUrl=$ApiBaseUrl;$prescriptionParallelHeaders=$prescriptionHeaders;$prescriptionParallelPath=$prescriptionPath;$prescriptionParallelBody=$prescriptionBody|ConvertTo-Json -Compress
  $prescriptionStatuses=1..$CallerCount|ForEach-Object -Parallel {
    $sourceHeaders=$using:prescriptionParallelHeaders;$targetBaseUrl=$using:prescriptionBaseUrl;$targetPath=$using:prescriptionParallelPath;$body=$using:prescriptionParallelBody
    $headers=@{};foreach($entry in $sourceHeaders.GetEnumerator()){$headers[$entry.Key]=$entry.Value}
    try{[int](Invoke-WebRequest -Uri "$targetBaseUrl$targetPath" -Method Put -Headers $headers -ContentType 'application/json' -Body $body -TimeoutSec 40).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
  } -ThrottleLimit $CallerCount
  Add-Check "$CallerCount concurrent exact prescription-preparation replays all return one recorded version" (
    @($prescriptionStatuses|Where-Object{$_-eq 200}).Count-eq$CallerCount) @{statuses=$prescriptionStatuses}
  $prescriptionReplay=Put $prescriptionPath $prescriptionHeaders $prescriptionBody
  Add-Check 'Exact prescription-preparation replay returns the original unchecked, unsigned, non-transmitted draft' (
    [int]$prescriptionReplay.version-eq 1-and$prescriptionReplay.rxNormCode-eq'860975'-and
    $prescriptionReplay.legalEffect-eq$false-and$prescriptionReplay.safetyChecked-eq$false-and
    $prescriptionReplay.signed-eq$false-and$prescriptionReplay.transmissionQueued-eq$false-and
    $prescriptionReplay.transmitted-eq$false-and$prescriptionReplay.patientDelivered-eq$false-and
    (($prescriptionReplay|ConvertTo-Json -Depth 10)-notmatch'patientId|encounterId|appointmentId|requestId|signatureId|claimId')) $prescriptionReplay
  $changedPrescriptionBody=$prescriptionBody.Clone();$changedPrescriptionBody.expectedVersion=1;$changedPrescriptionBody.directions='Changed synthetic directions.'
  Add-Check 'Prescription-preparation idempotency key rejects changed content without another version' (
    (Put-Status $prescriptionPath $prescriptionHeaders $changedPrescriptionBody)-eq 409-and
    [int](Scalar "select count(*) from telehealth_consultation_prescription_draft_versions where consultation_id='$($consultationStart.consultationId)';")-eq 1)
  $stalePrescriptionHeaders=$workspaceHeaders.Clone();$stalePrescriptionHeaders['X-Idempotency-Key']=Key 'th-prescription-stale'
  Add-Check 'A stale prescription-preparation writer cannot append a partial draft or event' (
    (Put-Status $prescriptionPath $stalePrescriptionHeaders $prescriptionBody)-eq 409-and
    [int](Scalar "select count(*) from telehealth_consultation_prescription_draft_events where consultation_id='$($consultationStart.consultationId)';")-eq 1)
  $revisedPrescriptionHeaders=$workspaceHeaders.Clone();$revisedPrescriptionHeaders['X-Idempotency-Key']=Key 'th-prescription-revision'
  $revisedPrescriptionBody=$prescriptionBody.Clone();$revisedPrescriptionBody.expectedVersion=1;$revisedPrescriptionBody.doseAmount=2;$revisedPrescriptionBody.directions='Take two synthetic tablets once daily for seven days.'
  $revisedPrescription=Put $prescriptionPath $revisedPrescriptionHeaders $revisedPrescriptionBody
  $prescriptionFacts=(Scalar @"
select json_build_object(
 'versions',(select json_agg(version order by version) from telehealth_consultation_prescription_draft_versions where consultation_id='$($consultationStart.consultationId)'),
 'events',(select json_agg(action order by aggregate_version) from telehealth_consultation_prescription_draft_events where consultation_id='$($consultationStart.consultationId)'),
 'allNonLegal',(select bool_and(not legal_effect and not safety_checked and not signed and not transmission_queued and not transmitted and not patient_delivered) from telehealth_consultation_prescription_draft_versions where consultation_id='$($consultationStart.consultationId)'),
 'pharmacyVersions',(select json_agg(pharmacy_choice_version order by version) from telehealth_consultation_prescription_draft_versions where consultation_id='$($consultationStart.consultationId)'),
 'catalogCodes',(select count(distinct rx_norm_code) from telehealth_consultation_prescription_draft_versions where consultation_id='$($consultationStart.consultationId)'),
 'consultationStatus',(select status from telehealth_consultation_contexts where consultation_id='$($consultationStart.consultationId)'),
 'requestStatus',(select request.status from telehealth_requests request join telehealth_consultation_contexts context on context.request_id=request.request_id where context.consultation_id='$($consultationStart.consultationId)')
)::text;
"@)|ConvertFrom-Json
  Add-Check 'Physician-authored revision appends immutable catalog and pharmacy-bound evidence without legal or lifecycle effect' (
    [int]$revisedPrescription.version-eq 2-and[decimal]$revisedPrescription.doseAmount-eq 2-and
    (@($prescriptionFacts.versions)-join',')-eq'1,2'-and(@($prescriptionFacts.events)-join',')-eq'DraftRecorded,DraftRevised'-and
    (@($prescriptionFacts.pharmacyVersions)-join',')-eq'2,2'-and[int]$prescriptionFacts.catalogCodes-eq 1-and
    $prescriptionFacts.allNonLegal-eq$true-and$prescriptionFacts.consultationStatus-eq'MediaEnded'-and$prescriptionFacts.requestStatus-eq'WrapUp') $prescriptionFacts
  Add-Check 'Prescription-preparation versions and events reject destructive mutation' (
    (Sql-Fails "update telehealth_consultation_prescription_draft_versions set directions='Changed' where consultation_id='$($consultationStart.consultationId)';")-and
    (Sql-Fails "delete from telehealth_consultation_prescription_draft_events where consultation_id='$($consultationStart.consultationId)';"))
  $prescriptionDownstreamAfter=(Scalar "select json_build_object('prescriptions',(select count(*) from prescriptions),'medications',(select count(*) from medications),'signatures',(select count(*) from encounter_signatures),'billing',(select count(*) from billing),'claims',(select count(*) from claims),'messages',(select count(*) from messages),'portalMailbox',(select count(*) from portal_mailbox_messages),'integrationOutbox',(select count(*) from integration_outbox),'integrationInbox',(select count(*) from integration_inbox))::text;")|ConvertFrom-Json
  Add-Check 'Prescription-preparation drafts create no canonical medication, prescription, signature, billing, claim, communication, or integration delta' (
    ($prescriptionDownstreamBefore|ConvertTo-Json -Compress)-eq($prescriptionDownstreamAfter|ConvertTo-Json -Compress)) @{before=$prescriptionDownstreamBefore;after=$prescriptionDownstreamAfter}

  $dispositionPath="/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/safety-disposition-draft"
  $dispositionHttp=Invoke-WebRequest "$ApiBaseUrl$dispositionPath" -Headers $workspaceHeaders -TimeoutSec 30
  $dispositionWorkspace=$dispositionHttp.Content|ConvertFrom-Json
  Add-Check 'Owning physician receives the bounded empty safety-disposition workspace in unfinished wrap-up' (
    [int]$dispositionHttp.StatusCode-eq 200-and$dispositionWorkspace.consultationStatus-eq'MediaEnded'-and
    $null-eq$dispositionWorkspace.currentDraft-and@($dispositionWorkspace.dispositions).Count-eq 8-and
    $dispositionWorkspace.signingEnabled-eq$false-and$dispositionWorkspace.patientDeliveryEnabled-eq$false-and
    $dispositionWorkspace.completionEnabled-eq$false-and
    (($dispositionWorkspace|ConvertTo-Json -Depth 12)-notmatch'patientId|encounterId|appointmentId|requestId|signatureId')) @{dispositions=@($dispositionWorkspace.dispositions).code}
  $dispositionCacheControl=($dispositionHttp.Headers['Cache-Control']-join',')
  Add-Check 'Safety-disposition workspace is explicitly non-cacheable' (
    $dispositionCacheControl-match'no-store'-and$dispositionCacheControl-match'private'-and
    ($dispositionHttp.Headers['Pragma']-join',')-match'no-cache'-and($dispositionHttp.Headers['Expires']-join',')-eq'0') @{cacheControl=$dispositionCacheControl}
  Add-Check 'A different physician receives opaque not-found for the safety-disposition workspace' (
    (Get-Status $dispositionPath $otherPhysicianHeaders)-eq 404)

  $invalidDispositionHeaders=$workspaceHeaders.Clone();$invalidDispositionHeaders['X-Idempotency-Key']=Key 'th-disposition-evaluation'
  Add-Check 'A clinically completed disposition rejects an unconfirmed adequate evaluation without evidence' (
    (Put-Status $dispositionPath $invalidDispositionHeaders @{expectedVersion=0;dispositionCode='TreatedTelehealth';adequateEvaluationCompleted=$false;followUpOwner='Patient';followUpTimeframe='within two synthetic days';nextStepInstructions='Physician-authored synthetic next step.';warningEscalationInstructions='Physician-authored synthetic warning and escalation instruction.';communicationMethod='DiscussedDuringSyntheticConsultation';communicationCompleted=$true;locationCallbackReconfirmed=$false;emergencyInstructionProvided=$false;emergencyHandoffStatus=$null;contactAttemptSummary=$null;syntheticDataConfirmed=$true})-eq 400-and
    [int](Scalar "select count(*) from telehealth_consultation_disposition_draft_versions where consultation_id='$($consultationStart.consultationId)';")-eq 0)
  $invalidEmergencyHeaders=$workspaceHeaders.Clone();$invalidEmergencyHeaders['X-Idempotency-Key']=Key 'th-disposition-emergency'
  Add-Check 'Emergency disposition rejects missing location, instruction, and handoff facts without evidence' (
    (Put-Status $dispositionPath $invalidEmergencyHeaders @{expectedVersion=0;dispositionCode='EmergencyTransferRecommended';adequateEvaluationCompleted=$true;followUpOwner='EmergencyServices';followUpTimeframe='now';nextStepInstructions='Physician-authored synthetic next step.';warningEscalationInstructions='Physician-authored synthetic warning and escalation instruction.';communicationMethod='NotYetCommunicated';communicationCompleted=$false;locationCallbackReconfirmed=$false;emergencyInstructionProvided=$false;emergencyHandoffStatus=$null;contactAttemptSummary=$null;syntheticDataConfirmed=$true})-eq 400-and
    [int](Scalar "select count(*) from telehealth_consultation_disposition_draft_events where consultation_id='$($consultationStart.consultationId)';")-eq 0)
  $invalidInterruptedHeaders=$workspaceHeaders.Clone();$invalidInterruptedHeaders['X-Idempotency-Key']=Key 'th-disposition-interrupted'
  Add-Check 'Interrupted disposition rejects a missing contact-and-safety-attempt summary without evidence' (
    (Put-Status $dispositionPath $invalidInterruptedHeaders @{expectedVersion=0;dispositionCode='TechnicalAbort';adequateEvaluationCompleted=$false;followUpOwner='Practice';followUpTimeframe='prompt synthetic follow-up';nextStepInstructions='Physician-authored synthetic next step.';warningEscalationInstructions='Physician-authored synthetic warning and escalation instruction.';communicationMethod='NotYetCommunicated';communicationCompleted=$false;locationCallbackReconfirmed=$false;emergencyInstructionProvided=$false;emergencyHandoffStatus=$null;contactAttemptSummary=$null;syntheticDataConfirmed=$true})-eq 400-and
    [int](Scalar "select count(*) from telehealth_consultation_disposition_draft_events where consultation_id='$($consultationStart.consultationId)';")-eq 0)

  $dispositionDownstreamBefore=$downstreamAfter
  $dispositionHeaders=$workspaceHeaders.Clone();$dispositionHeaders['X-Idempotency-Key']=Key 'th-disposition-draft'
  $dispositionBody=@{expectedVersion=0;dispositionCode='TreatedTelehealth';adequateEvaluationCompleted=$true;followUpOwner='Patient';followUpTimeframe='within two synthetic days';nextStepInstructions='Physician-authored synthetic next step.';warningEscalationInstructions='Physician-authored synthetic warning and escalation instruction.';communicationMethod='DiscussedDuringSyntheticConsultation';communicationCompleted=$true;locationCallbackReconfirmed=$false;emergencyInstructionProvided=$false;emergencyHandoffStatus=$null;contactAttemptSummary=$null;syntheticDataConfirmed=$true}
  $dispositionBaseUrl=$ApiBaseUrl;$dispositionParallelHeaders=$dispositionHeaders;$dispositionParallelPath=$dispositionPath;$dispositionParallelBody=$dispositionBody|ConvertTo-Json -Compress
  $dispositionStatuses=1..$CallerCount|ForEach-Object -Parallel {
    $sourceHeaders=$using:dispositionParallelHeaders;$targetBaseUrl=$using:dispositionBaseUrl;$targetPath=$using:dispositionParallelPath;$body=$using:dispositionParallelBody
    $headers=@{};foreach($entry in $sourceHeaders.GetEnumerator()){$headers[$entry.Key]=$entry.Value}
    try{[int](Invoke-WebRequest -Uri "$targetBaseUrl$targetPath" -Method Put -Headers $headers -ContentType 'application/json' -Body $body -TimeoutSec 40).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
  } -ThrottleLimit $CallerCount
  Add-Check "$CallerCount concurrent exact safety-disposition replays all return one recorded version" (
    @($dispositionStatuses|Where-Object{$_-eq 200}).Count-eq$CallerCount) @{statuses=$dispositionStatuses}
  $dispositionReplay=Put $dispositionPath $dispositionHeaders $dispositionBody
  Add-Check 'Exact safety-disposition replay returns the original unsigned, undelivered, non-final draft' (
    [int]$dispositionReplay.version-eq 1-and$dispositionReplay.dispositionCode-eq'TreatedTelehealth'-and
    $dispositionReplay.legalEffect-eq$false-and$dispositionReplay.signed-eq$false-and
    $dispositionReplay.finalized-eq$false-and$dispositionReplay.patientDelivered-eq$false-and
    (($dispositionReplay|ConvertTo-Json -Depth 10)-notmatch'patientId|encounterId|appointmentId|requestId|signatureId')) $dispositionReplay
  Add-Check 'Safety-disposition idempotency key rejects changed content without another version' (
    (Put-Status $dispositionPath $dispositionHeaders @{expectedVersion=1;dispositionCode='NoTreatmentNeeded';adequateEvaluationCompleted=$true;followUpOwner='NoneClinicallyRequired';followUpTimeframe='none clinically required';nextStepInstructions='Changed synthetic instruction.';warningEscalationInstructions='Changed synthetic warning.';communicationMethod='DiscussedDuringSyntheticConsultation';communicationCompleted=$true;locationCallbackReconfirmed=$false;emergencyInstructionProvided=$false;emergencyHandoffStatus=$null;contactAttemptSummary=$null;syntheticDataConfirmed=$true})-eq 409-and
    [int](Scalar "select count(*) from telehealth_consultation_disposition_draft_versions where consultation_id='$($consultationStart.consultationId)';")-eq 1)
  $staleDispositionHeaders=$workspaceHeaders.Clone();$staleDispositionHeaders['X-Idempotency-Key']=Key 'th-disposition-stale'
  Add-Check 'A stale safety-disposition writer cannot append a partial draft or event' (
    (Put-Status $dispositionPath $staleDispositionHeaders $dispositionBody)-eq 409-and
    [int](Scalar "select count(*) from telehealth_consultation_disposition_draft_events where consultation_id='$($consultationStart.consultationId)';")-eq 1)
  $revisedDispositionHeaders=$workspaceHeaders.Clone();$revisedDispositionHeaders['X-Idempotency-Key']=Key 'th-disposition-revision'
  $revisedDisposition=Put $dispositionPath $revisedDispositionHeaders @{expectedVersion=1;dispositionCode='EmergencyTransferRecommended';adequateEvaluationCompleted=$true;followUpOwner='EmergencyServices';followUpTimeframe='immediate synthetic escalation';nextStepInstructions='Physician-authored synthetic emergency next step.';warningEscalationInstructions='Physician-authored synthetic emergency warning and escalation instruction.';communicationMethod='NotYetCommunicated';communicationCompleted=$false;locationCallbackReconfirmed=$true;emergencyInstructionProvided=$true;emergencyHandoffStatus='UnableToConfirm';contactAttemptSummary=$null;syntheticDataConfirmed=$true}
  $dispositionFacts=(Scalar @"
select json_build_object(
 'versions',(select json_agg(version order by version) from telehealth_consultation_disposition_draft_versions where consultation_id='$($consultationStart.consultationId)'),
 'events',(select json_agg(action order by aggregate_version) from telehealth_consultation_disposition_draft_events where consultation_id='$($consultationStart.consultationId)'),
 'allNonLegal',(select bool_and(not legal_effect) from telehealth_consultation_disposition_draft_versions where consultation_id='$($consultationStart.consultationId)'),
 'consultationStatus',(select status from telehealth_consultation_contexts where consultation_id='$($consultationStart.consultationId)'),
 'requestStatus',(select request.status from telehealth_requests request join telehealth_consultation_contexts context on context.request_id=request.request_id where context.consultation_id='$($consultationStart.consultationId)'),
 'shiftStatus',(select shift.status from telehealth_clinician_shifts shift join telehealth_consultation_contexts context on context.shift_id=shift.shift_id where context.consultation_id='$($consultationStart.consultationId)'),
 'appointmentStatus',(select appointment.status from appointments appointment join telehealth_consultation_contexts context on context.appointment_id=appointment.id where context.consultation_id='$($consultationStart.consultationId)')
)::text;
"@)|ConvertFrom-Json
  Add-Check 'Emergency revision appends immutable conditional safety facts without completing or releasing the visit' (
    [int]$revisedDisposition.version-eq 2-and$revisedDisposition.locationCallbackReconfirmed-eq$true-and
    $revisedDisposition.emergencyInstructionProvided-eq$true-and$revisedDisposition.emergencyHandoffStatus-eq'UnableToConfirm'-and
    (@($dispositionFacts.versions)-join',')-eq'1,2'-and(@($dispositionFacts.events)-join',')-eq'DraftRecorded,DraftRevised'-and
    $dispositionFacts.allNonLegal-eq$true-and$dispositionFacts.consultationStatus-eq'MediaEnded'-and
    $dispositionFacts.requestStatus-eq'WrapUp'-and$dispositionFacts.shiftStatus-eq'WrapUp'-and$dispositionFacts.appointmentStatus-eq'>') $dispositionFacts
  Add-Check 'Safety-disposition versions and events reject destructive mutation' (
    (Sql-Fails "update telehealth_consultation_disposition_draft_versions set next_step_instructions='Changed' where consultation_id='$($consultationStart.consultationId)';")-and
    (Sql-Fails "delete from telehealth_consultation_disposition_draft_events where consultation_id='$($consultationStart.consultationId)';"))
  $dispositionDownstreamAfter=(Scalar "select json_build_object('prescriptions',(select count(*) from prescriptions),'medications',(select count(*) from medications),'signatures',(select count(*) from encounter_signatures),'billing',(select count(*) from billing),'claims',(select count(*) from claims),'messages',(select count(*) from messages),'portalMailbox',(select count(*) from portal_mailbox_messages),'integrationOutbox',(select count(*) from integration_outbox),'integrationInbox',(select count(*) from integration_inbox))::text;")|ConvertFrom-Json
  Add-Check 'Safety-disposition drafts create no medication, prescription, signature, billing, claim, communication, or integration delta' (
    ($dispositionDownstreamBefore|ConvertTo-Json -Compress)-eq($dispositionDownstreamAfter|ConvertTo-Json -Compress)) @{before=$dispositionDownstreamBefore;after=$dispositionDownstreamAfter}

  $reviewCountsBefore=Scalar "select json_build_object('notes',(select count(*) from clinical_notes),'dispositions',(select count(*) from telehealth_consultation_disposition_draft_versions),'pharmacyChoices',(select count(*) from telehealth_consultation_pharmacy_choice_versions),'preparationDrafts',(select count(*) from telehealth_consultation_prescription_draft_versions),'signatures',(select count(*) from encounter_signatures),'prescriptions',(select count(*) from prescriptions),'billing',(select count(*) from billing),'claims',(select count(*) from claims),'outbox',(select count(*) from integration_outbox))::text;"
  $completedStructuralHttp=Invoke-WebRequest "$ApiBaseUrl$completionPath" -Headers $workspaceHeaders -TimeoutSec 30
  $completedStructural=$completedStructuralHttp.Content|ConvertFrom-Json
  $completedStructuralReplay=Invoke-RestMethod "$ApiBaseUrl$completionPath" -Headers $workspaceHeaders -TimeoutSec 30
  $reviewCountsAfter=Scalar "select json_build_object('notes',(select count(*) from clinical_notes),'dispositions',(select count(*) from telehealth_consultation_disposition_draft_versions),'pharmacyChoices',(select count(*) from telehealth_consultation_pharmacy_choice_versions),'preparationDrafts',(select count(*) from telehealth_consultation_prescription_draft_versions),'signatures',(select count(*) from encounter_signatures),'prescriptions',(select count(*) from prescriptions),'billing',(select count(*) from billing),'claims',(select count(*) from claims),'outbox',(select count(*) from integration_outbox))::text;"
  $completedStructuralJson=$completedStructural|ConvertTo-Json -Depth 12 -Compress
  Add-Check 'Recorded drafts produce a minimized structural review while every consequential capability remains disabled' (
    $completedStructural.structuralEvidencePresent-eq$true-and[int]$completedStructural.documentation.version-eq 3-and
    [int]$completedStructural.safetyDisposition.version-eq 2-and$completedStructural.safetyDisposition.dispositionCode-eq'EmergencyTransferRecommended'-and
    $completedStructural.safetyDisposition.followUpOwnerPresent-eq$true-and$completedStructural.safetyDisposition.nextStepInstructionsPresent-eq$true-and
    [int]$completedStructural.pharmacyChoice.version-eq 2-and$completedStructural.pharmacyChoice.patientChoiceConfirmed-eq$true-and
    (@($completedStructural.productBlockers)-join',')-eq'FINAL_CLINICAL_REVIEW_NOT_RECORDED,SIGNATURE_FINALIZATION_NOT_IMPLEMENTED,ATOMIC_DOWNSTREAM_OWNERSHIP_NOT_IMPLEMENTED'-and
    $completedStructural.signingEnabled-eq$false-and$completedStructural.completionEnabled-eq$false-and
    $completedStructural.patientDeliveryEnabled-eq$false-and$completedStructural.downstreamCreationEnabled-eq$false-and
    $completedStructuralJson-notmatch'Physician-authored synthetic|immediate synthetic escalation|pharmacy_name|address|ncpdp|npi|patientId|encounterId|appointmentId|requestId') @{blockers=@($completedStructural.productBlockers)}
  Add-Check 'Repeated completion-prerequisites reads are side-effect-free and return stable structural evidence' (
    $reviewCountsBefore-eq$reviewCountsAfter-and
    ($completedStructural.documentation|ConvertTo-Json -Compress)-eq($completedStructuralReplay.documentation|ConvertTo-Json -Compress)-and
    ($completedStructural.safetyDisposition|ConvertTo-Json -Compress)-eq($completedStructuralReplay.safetyDisposition|ConvertTo-Json -Compress)-and
    ($completedStructural.pharmacyChoice|ConvertTo-Json -Compress)-eq($completedStructuralReplay.pharmacyChoice|ConvertTo-Json -Compress)) @{before=$reviewCountsBefore;after=$reviewCountsAfter}

  $encounterId=[int](Scalar "select encounter_id from telehealth_consultation_contexts where consultation_id='$($consultationStart.consultationId)';")
  $signStatus=Put-Status "/api/encounters/$encounterId/sign" $workspaceHeaders @{isLock=$true;amendment='Synthetic runtime proof of canonical signature locking.'}
  $noteCountBeforeLockedAttempt=[int](Scalar "select count(*) from clinical_notes;")
  $lockedDraftStatus=Put-Status $draftPath $workspaceHeaders @{expectedVersion=3;subjective='Must not save after lock.';objective=$null;assessment=$null;plan=$null}
  $lockedWorkspace=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/clinician/consultations/$($consultationStart.consultationId)/workspace" -Headers $workspaceHeaders -TimeoutSec 20
  Add-Check 'Canonical locking signature blocks telehealth draft writes without a note delta' (
    $signStatus-eq 200-and$lockedDraftStatus-eq 409-and$lockedWorkspace.documentation.isLocked-eq$true-and
    [int](Scalar "select count(*) from clinical_notes;")-eq$noteCountBeforeLockedAttempt) @{signStatus=$signStatus;draftStatus=$lockedDraftStatus}
  $lockedDispositionHeaders=$workspaceHeaders.Clone();$lockedDispositionHeaders['X-Idempotency-Key']=Key 'th-disposition-locked'
  Add-Check 'Canonical locking signature removes safety-disposition write eligibility without another draft version' (
    (Put-Status $dispositionPath $lockedDispositionHeaders @{expectedVersion=2;dispositionCode='NoTreatmentNeeded';adequateEvaluationCompleted=$true;followUpOwner='NoneClinicallyRequired';followUpTimeframe='none clinically required';nextStepInstructions='Must not save after lock.';warningEscalationInstructions='Must not save after lock.';communicationMethod='DiscussedDuringSyntheticConsultation';communicationCompleted=$true;locationCallbackReconfirmed=$false;emergencyInstructionProvided=$false;emergencyHandoffStatus=$null;contactAttemptSummary=$null;syntheticDataConfirmed=$true})-eq 404-and
    [int](Scalar "select count(*) from telehealth_consultation_disposition_draft_versions where consultation_id='$($consultationStart.consultationId)';")-eq 2)
  $lockedPrescriptionHeaders=$workspaceHeaders.Clone();$lockedPrescriptionHeaders['X-Idempotency-Key']=Key 'th-prescription-locked'
  $lockedPrescriptionBody=$revisedPrescriptionBody.Clone();$lockedPrescriptionBody.expectedVersion=2;$lockedPrescriptionBody.directions='Must not save after lock.'
  Add-Check 'Canonical locking signature removes prescription-preparation read and write eligibility without another draft version' (
    (Get-Status $prescriptionPath $workspaceHeaders)-eq 404-and
    (Put-Status $prescriptionPath $lockedPrescriptionHeaders $lockedPrescriptionBody)-eq 404-and
    [int](Scalar "select count(*) from telehealth_consultation_prescription_draft_versions where consultation_id='$($consultationStart.consultationId)';")-eq 2)
  Add-Check 'Canonical locking signature also removes the unfinished completion-prerequisites review' (
    (Get-Status $completionPath $workspaceHeaders)-eq 404)

  $workspaceAudit=(Scalar @"
select json_build_object(
  'count',count(*),
  'permissions',coalesce(json_agg(required_permission order by required_permission),'[]'::json)
)::text
from phi_access_audit_events
where endpoint_name like '%/api/telehealth/v1/clinician/consultations/%/workspace%'
  and resource_type='TelehealthConsultation'
  and resource_id='$($consultationStart.consultationId)'
  and authorized=true and response_status=200;
"@)|ConvertFrom-Json
  Add-Check 'Authorized workspace access records consultation-correlated PHI audit evidence for both view permissions' (
    [int]$workspaceAudit.count-ge 2-and
    @($workspaceAudit.permissions|Where-Object{$_-match'^acl\.patients\.demo\.view@'}).Count-ge 1-and
    @($workspaceAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.view@'}).Count-ge 1) @{count=[int]$workspaceAudit.count;permissions=@($workspaceAudit.permissions)}
  $draftAudit=(Scalar @"
select json_build_object('count',count(*),'permissions',coalesce(json_agg(required_permission order by required_permission),'[]'::json))::text
from phi_access_audit_events
where endpoint_name like '%/api/telehealth/v1/clinician/consultations/%/documentation/draft%'
  and resource_type='TelehealthConsultation' and resource_id='$($consultationStart.consultationId)'
  and authorized=true and response_status in (200,400,404,409);
"@)|ConvertFrom-Json
  Add-Check 'Draft attempts record opaque consultation-correlated PHI audit evidence for both views and encounter write' (
    [int]$draftAudit.count-ge 3-and
    @($draftAudit.permissions|Where-Object{$_-match'^acl\.patients\.demo\.view@'}).Count-ge 1-and
    @($draftAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.view@'}).Count-ge 1-and
    @($draftAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.write@'}).Count-ge 1) @{count=[int]$draftAudit.count;permissions=@($draftAudit.permissions)}
  $wrapUpAudit=(Scalar @"
select json_build_object('count',count(*),'permissions',coalesce(json_agg(required_permission order by required_permission),'[]'::json))::text
from phi_access_audit_events
where endpoint_name like '%/api/telehealth/v1/clinician/consultations/%/wrap-up%'
  and resource_type='TelehealthConsultation' and resource_id='$($consultationStart.consultationId)'
  and authorized=true and response_status in (200,400,404,409);
"@)|ConvertFrom-Json
  Add-Check 'Wrap-up attempts record opaque consultation-correlated PHI audit evidence for both views and encounter write' (
    [int]$wrapUpAudit.count-ge 3-and
    @($wrapUpAudit.permissions|Where-Object{$_-match'^acl\.patients\.demo\.view@'}).Count-ge 1-and
    @($wrapUpAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.view@'}).Count-ge 1-and
    @($wrapUpAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.write@'}).Count-ge 1) @{count=[int]$wrapUpAudit.count;permissions=@($wrapUpAudit.permissions)}
  $pharmacySearchAudit=(Scalar @"
select json_build_object('count',count(*),'permissions',coalesce(json_agg(required_permission order by required_permission),'[]'::json))::text
from phi_access_audit_events
where endpoint_name like '%/pharmacy-choices =>%'
  and resource_type='TelehealthConsultation' and resource_id='$($consultationStart.consultationId)'
  and authorized=true and response_status in (200,400,404);
"@)|ConvertFrom-Json
  Add-Check 'Pharmacy search attempts record opaque consultation-correlated PHI audit evidence for both view permissions' (
    [int]$pharmacySearchAudit.count-ge 4-and
    @($pharmacySearchAudit.permissions|Where-Object{$_-match'^acl\.patients\.demo\.view@'}).Count-ge 1-and
    @($pharmacySearchAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.view@'}).Count-ge 1) @{count=[int]$pharmacySearchAudit.count;permissions=@($pharmacySearchAudit.permissions)}
  $pharmacyChoiceAudit=(Scalar @"
select json_build_object('count',count(*),'permissions',coalesce(json_agg(required_permission order by required_permission),'[]'::json))::text
from phi_access_audit_events
where endpoint_name like '%/pharmacy-choice =>%'
  and resource_type='TelehealthConsultation' and resource_id='$($consultationStart.consultationId)'
  and authorized=true and response_status in (200,400,404,409);
"@)|ConvertFrom-Json
  Add-Check 'Pharmacy destination attempts record opaque consultation-correlated PHI audit evidence for both views and encounter write' (
    [int]$pharmacyChoiceAudit.count-ge 3-and
    @($pharmacyChoiceAudit.permissions|Where-Object{$_-match'^acl\.patients\.demo\.view@'}).Count-ge 1-and
    @($pharmacyChoiceAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.view@'}).Count-ge 1-and
    @($pharmacyChoiceAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.write@'}).Count-ge 1) @{count=[int]$pharmacyChoiceAudit.count;permissions=@($pharmacyChoiceAudit.permissions)}
  $prescriptionAudit=(Scalar @"
select json_build_object('count',count(*),'permissions',coalesce(json_agg(required_permission order by required_permission),'[]'::json))::text
from phi_access_audit_events
where endpoint_name like '%/prescription-preparation-draft =>%'
  and resource_type='TelehealthConsultation' and resource_id='$($consultationStart.consultationId)'
  and authorized=true and response_status in (200,400,404,409);
"@)|ConvertFrom-Json
  Add-Check 'Prescription-preparation reads and writes record opaque consultation-correlated PHI audit evidence for medication and encounter permissions' (
    [int]$prescriptionAudit.count-ge 7-and
    @($prescriptionAudit.permissions|Where-Object{$_-match'^acl\.patients\.demo\.view@'}).Count-ge 1-and
    @($prescriptionAudit.permissions|Where-Object{$_-match'^acl\.patients\.med\.view@'}).Count-ge 1-and
    @($prescriptionAudit.permissions|Where-Object{$_-match'^acl\.patients\.med\.write@'}).Count-ge 1-and
    @($prescriptionAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.view@'}).Count-ge 1-and
    @($prescriptionAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.write@'}).Count-ge 1) @{count=[int]$prescriptionAudit.count;permissions=@($prescriptionAudit.permissions)}
  $dispositionAudit=(Scalar @"
select json_build_object('count',count(*),'permissions',coalesce(json_agg(required_permission order by required_permission),'[]'::json))::text
from phi_access_audit_events
where endpoint_name like '%/safety-disposition-draft =>%'
  and resource_type='TelehealthConsultation' and resource_id='$($consultationStart.consultationId)'
  and authorized=true and response_status in (200,400,404,409);
"@)|ConvertFrom-Json
  Add-Check 'Safety-disposition reads and writes record opaque consultation-correlated PHI audit evidence for both views and encounter write' (
    [int]$dispositionAudit.count-ge 4-and
    @($dispositionAudit.permissions|Where-Object{$_-match'^acl\.patients\.demo\.view@'}).Count-ge 1-and
    @($dispositionAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.view@'}).Count-ge 1-and
    @($dispositionAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.write@'}).Count-ge 1) @{count=[int]$dispositionAudit.count;permissions=@($dispositionAudit.permissions)}
  $completionAudit=(Scalar @"
select json_build_object('count',count(*),'permissions',coalesce(json_agg(required_permission order by required_permission),'[]'::json))::text
from phi_access_audit_events
where endpoint_name like '%/completion-prerequisites =>%'
  and resource_type='TelehealthConsultation' and resource_id='$($consultationStart.consultationId)'
  and authorized=true and response_status in (200,404);
"@)|ConvertFrom-Json
  Add-Check 'Completion-prerequisites reads record opaque consultation-correlated PHI audit evidence for view permissions only' (
    [int]$completionAudit.count-ge 4-and
    @($completionAudit.permissions|Where-Object{$_-match'^acl\.patients\.demo\.view@'}).Count-ge 1-and
    @($completionAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.view@'}).Count-ge 1-and
    @($completionAudit.permissions|Where-Object{$_-match'^acl\.encounters\.auth\.write@'}).Count-eq 0) @{count=[int]$completionAudit.count;permissions=@($completionAudit.permissions)}

  $wrapUpPatientStatus=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/patient/requests/$requestId/status" -Headers @{'X-AvenChart-Patient-Portal-Session'=$portal.sessionId} -TimeoutSec 20
  Add-Check 'Patient sees an honest unfinished WrapUp projection without clinician, encounter, or draft identity' (
    $wrapUpPatientStatus.requestStatus-eq'WrapUp'-and$wrapUpPatientStatus.phase-eq'WrapUp'-and
    $wrapUpPatientStatus.headline-match'finishing'-and$wrapUpPatientStatus.detail-match'not complete'-and
    (($wrapUpPatientStatus|ConvertTo-Json -Depth 8)-notmatch'gold-provider-01|physicianStaffId|encounterId|joinCredential|documentation')) @{phase=$wrapUpPatientStatus.phase;version=$wrapUpPatientStatus.requestVersion}
}
catch {
  Add-Check 'Telehealth queue concurrency execution' $false @{
    message = $_.Exception.Message
    stack = $_.ScriptStackTrace
  }
}
finally{
  if($proofPhysicianAccountCreated){
    try{
      $null=Scalar "delete from auth_sessions where username='gold-provider-02'; delete from access_user_memberships where user_value='gold-provider-02' and group_value='clin'; delete from auth_accounts where username='gold-provider-02'; select 'ok';"
      $proofPhysicianAccountCount=[int](Scalar "select count(*) from auth_accounts where username='gold-provider-02';")
      Add-Check 'Synthetic non-owner physician identity is removed after the authorization proof' ($proofPhysicianAccountCount-eq 0)
    }
    catch{Add-Check 'Synthetic non-owner physician identity is removed after the authorization proof' $false $_.Exception.Message}
  }
  if($null-ne$originalPatientDob){
    try{
      $null=Scalar "update patients set date_of_birth='$originalPatientDob'::date where canonical_id='MOD-PAT-0012'; select 'ok';"
      $restoredPatientDob=Scalar "select date_of_birth::text from patients where canonical_id='MOD-PAT-0012';"
      Add-Check 'Established-patient adult-gate fixture is restored after the mutation proof' ($restoredPatientDob-eq$originalPatientDob)
    }
    catch{Add-Check 'Established-patient adult-gate fixture is restored after the mutation proof' $false $_.Exception.Message}
  }
  if($null-ne$originalCoverageGroup){
    try{
      $escapedCoverageGroup=$originalCoverageGroup.Replace("'","''")
      $null=Scalar "update insurance_records set group_number='$escapedCoverageGroup' where id='INS-MOD-PAT-0012-P' and patient_id='MOD-PAT-0012';"
      $restoredCoverageGroup=Scalar "select group_number from insurance_records where id='INS-MOD-PAT-0012-P' and patient_id='MOD-PAT-0012';"
      Add-Check 'Coverage source fixture is restored after the mutation proof' ($restoredCoverageGroup-eq$originalCoverageGroup)
    }
    catch{Add-Check 'Coverage source fixture is restored after the mutation proof' $false $_.Exception.Message}
  }
  if($null-ne$reservationId){
    try{
      $null=Scalar "update telehealth_video_participant_grants set status='Revoked' where session_id in (select session_id from telehealth_video_sessions where reservation_id='$reservationId') and status='Issued'; update telehealth_video_sessions set status='Ended' where reservation_id='$reservationId' and status in ('Prepared','WaitingRoom'); update telehealth_reservations set status='Released',version=version+1 where reservation_id='$reservationId' and status='Active'; select 'ok';"
      $releasedReservationCount=[int](Scalar "select count(*) from telehealth_reservations where reservation_id='$reservationId' and status='Released';")
      Add-Check 'Synthetic proof releases its active reservation and room access after assertions' ($releasedReservationCount-eq 1)
    }
    catch{Add-Check 'Synthetic proof releases its active reservation and room access after assertions' $false $_.Exception.Message}
  }
  if($null-ne$shift){
    try{
      $null=Scalar "update telehealth_clinician_shifts set status='Active',version=version+1 where shift_id='$($shift.shiftId)' and status in ('Busy','WrapUp'); select 'ok';"
      $restoredShiftStatus=Scalar "select status from telehealth_clinician_shifts where shift_id='$($shift.shiftId)';"
      Add-Check 'Synthetic proof restores its busy clinician shift for repeatable evidence runs' ($restoredShiftStatus-eq'Active')
    }
    catch{Add-Check 'Synthetic proof restores its busy clinician shift for repeatable evidence runs' $false $_.Exception.Message}
  }
  $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decisions=@('TH-DEC-0003','TH-DEC-0005','TH-DEC-0006','TH-DEC-0008','TH-DEC-0009','TH-DEC-0010','TH-DEC-0011','TH-DEC-0012','TH-DEC-0013','TH-DEC-0014','TH-DEC-0015','TH-DEC-0016','TH-DEC-0017','TH-DEC-0018','TH-DEC-0019');callerCount=$CallerCount;checks=$checks};$result|ConvertTo-Json -Depth 10|Set-Content $resultPath -Encoding utf8;$result|ConvertTo-Json -Depth 10
}
if(-not$passed){exit 1}
