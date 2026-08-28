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
    throw 'Telehealth applicant notice proof is local-only.'
}

# Reuse the sealed Sprint 23 proof in-process so this proof starts from fully
# governed, API-created promoted applicants instead of bypassing earlier gates.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantSyntheticPromotion.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-notice-acknowledgment.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-NoticeCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-NoticeBody([int]$Version,[string]$State,[string]$NoticeKey) {
    @{
        expectedVersion=$Version;noticeKey=$NoticeKey;noticeVersion=1
        currentLocationStateCode=$State;currentLocationConfirmed=$true
        modeOfCareAcknowledged=$true;privacyLimitationsAcknowledged=$true
        emergencyInstructionsAcknowledged=$true;inPersonOptionAcknowledged=$true
        clinicianReconfirmationRequiredAcknowledged=$true;syntheticDataConfirmed=$true
    }
}
function Invoke-Notice([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/telehealth-notice/acknowledgment" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 8)
}
function Get-NoticeCounts {
    (Invoke-Scalar @"
select json_build_object(
  'noticeAcknowledgments',(select count(*) from telehealth_applicant_notice_acknowledgments),
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
  'appointments',(select count(*) from appointments),
  'encounters',(select count(*) from encounters),
  'claims',(select count(*) from claims),
  'prescriptions',(select count(*) from prescriptions));
"@)|ConvertFrom-Json
}

try {
    $flToken=-join(1..10|ForEach-Object{[char](Get-Random -Minimum 65 -Maximum 91)})
    $flPhoneSuffix=Get-Random -Minimum 1000 -Maximum 9999
    $fl=New-AuthorizedApplicant "NoticeFL$flToken" 'FL' '1991-08-12' "+1305555$flPhoneSuffix" $adminHeaders ([Guid]::NewGuid().ToString('N').Substring(0,12))
    $flPromotion=Invoke-Promotion $fl $adminHeaders (New-Key 'sp24-fl-promotion')
    if ($flPromotion.outcome -ne 'SyntheticPatientCreated') { throw 'Florida notice fixture promotion did not create the expected synthetic shell.' }

    $fixtures=@(
        [pscustomobject]@{Applicant=$success;State='GA';NoticeKey='GA_TELEHEALTH_NOTICE_V1';Source='Georgia Composite Medical Board Rule 360-3-.07'},
        [pscustomobject]@{Applicant=$race;State='CA';NoticeKey='CA_TELEHEALTH_NOTICE_V1';Source='California Business and Professions Code § 2290.5'},
        [pscustomobject]@{Applicant=$fl;State='FL';NoticeKey='FL_TELEHEALTH_NOTICE_V1';Source='Florida Statutes § 456.47'}
    )
    $notices=@{}
    foreach($fixture in $fixtures) {
        $web=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$($fixture.Applicant.Created.applicantId)/telehealth-notice" -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$fixture.Applicant.Secret
        } -TimeoutSec 30
        $notice=$web.Content|ConvertFrom-Json
        $notices[$fixture.State]=$notice
        $json=$notice|ConvertTo-Json -Depth 12 -Compress
        Add-NoticeCheck "Server selects the $($fixture.State) notice from prior safety-location evidence" (
            $web.Headers['Cache-Control']-match'no-store' -and
            $notice.applicantStatus-eq'SyntheticPatientPromoted' -and [int]$notice.applicantVersion-eq 12 -and
            $notice.currentLocationStateCode-eq$fixture.State -and $notice.noticeKey-eq$fixture.NoticeKey -and
            [int]$notice.noticeVersion-eq 1 -and $notice.sourceTitle-eq$fixture.Source -and
            $notice.policyKey-eq'SYNTHETIC_TELEHEALTH_NOTICE_ACKNOWLEDGMENT' -and
            $notice.legalReviewStatus-eq'PendingIndependentReview' -and -not$notice.acknowledged -and
            -not$notice.legalConsentEstablished -and -not$notice.clinicianConsentDocumented -and
            $notice.clinicianReconfirmationRequired -and -not$notice.portalAccountCreated -and
            -not$notice.intakeCompleted -and -not$notice.practiceAccepted -and -not$notice.insuranceCreated -and
            -not$notice.requestCreated -and -not$notice.queueEnabled -and -not$notice.careEnabled -and
            $json-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|safetyTriageEvaluationId|staffId|actorId|commandFingerprint')
    }

    $gaId=[string]$success.Created.applicantId
    $gaBody=New-NoticeBody 12 'GA' 'GA_TELEHEALTH_NOTICE_V1'
    Add-NoticeCheck 'Anonymous, wrong-key, partial, mismatched-state, mismatched-version, and stale-version commands fail before persistence' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/telehealth-notice")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/telehealth-notice" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/telehealth-notice/acknowledgment" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp24-partial')} (Merge-Map $gaBody @{privacyLimitationsAcknowledged=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/telehealth-notice/acknowledgment" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp24-state')} (Merge-Map $gaBody @{currentLocationStateCode='CA';noticeKey='CA_TELEHEALTH_NOTICE_V1'}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/telehealth-notice/acknowledgment" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp24-notice')} (Merge-Map $gaBody @{noticeVersion=2}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/telehealth-notice/acknowledgment" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp24-stale')} (Merge-Map $gaBody @{expectedVersion=11}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_notice_acknowledgments where applicant_id='$gaId';")-eq 0)

    $before=Get-NoticeCounts
    $gaKey=New-Key 'sp24-ga'
    $gaRecorded=Invoke-Notice $success $gaBody $gaKey
    $gaReplay=Invoke-Notice $success $gaBody $gaKey
    $gaJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-NoticeCheck 'Georgia acknowledgment records one legally nonfinal, no-consequence result and exact replay converges' (
        $gaRecorded.applicantStatus-eq'SyntheticTelehealthNoticeAcknowledged' -and [int]$gaRecorded.applicantVersion-eq 13 -and
        $gaRecorded.acknowledged -and $null-ne$gaRecorded.acknowledgedAt -and
        -not$gaRecorded.legalConsentEstablished -and -not$gaRecorded.clinicianConsentDocumented -and
        $gaRecorded.clinicianReconfirmationRequired -and -not$gaRecorded.portalAccountCreated -and
        -not$gaRecorded.intakeCompleted -and -not$gaRecorded.practiceAccepted -and -not$gaRecorded.insuranceCreated -and
        -not$gaRecorded.requestCreated -and -not$gaRecorded.queueEnabled -and -not$gaRecorded.careEnabled -and
        $gaReplay.acknowledgedAt-eq$gaRecorded.acknowledgedAt -and
        $gaJson-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|safetyTriageEvaluationId|staffId|actorId|commandFingerprint')
    Add-NoticeCheck 'Changed idempotency reuse and a second acknowledgment fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/telehealth-notice/acknowledgment" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{expectedVersion=13}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/telehealth-notice/acknowledgment" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp24-second')} (Merge-Map $gaBody @{expectedVersion=13}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_notice_acknowledgments where applicant_id='$gaId';")-eq 1)

    $caId=[string]$race.Created.applicantId
    $caBody=New-NoticeBody 12 'CA' 'CA_TELEHEALTH_NOTICE_V1'
    $caJsonBody=$caBody|ConvertTo-Json -Depth 8 -Compress
    $caRaceKey=New-Key 'sp24-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/telehealth-notice/acknowledgment" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    Add-NoticeCheck 'Eight concurrent exact California retries converge on one acknowledgment and one aggregate event' (
        @($statuses|Where-Object{$_-eq 200}).Count-eq 8 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_notice_acknowledgments where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-telehealth-notice-acknowledged';")-eq 1) @{statuses=$statuses}

    $flBody=New-NoticeBody 12 'FL' 'FL_TELEHEALTH_NOTICE_V1'
    $flRecorded=Invoke-Notice $fl $flBody (New-Key 'sp24-fl')
    Add-NoticeCheck 'Florida acknowledgment remains pending independent review and cannot imply a state-specific consent-form rule' (
        $flRecorded.applicantStatus-eq'SyntheticTelehealthNoticeAcknowledged' -and
        $flRecorded.legalReviewStatus-eq'PendingIndependentReview' -and
        -not$flRecorded.legalConsentEstablished -and -not$flRecorded.clinicianConsentDocumented -and
        @($flRecorded.deferredRequirements|Where-Object{$_-match'does not claim that Florida law imposes or waives'}).Count-eq 1)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaNotice=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/telehealth-notice" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    Add-NoticeCheck 'Applicant resume and notice reload expose acknowledged status without a portal or patient identifier' (
        $gaResume.status-eq'SyntheticTelehealthNoticeAcknowledged' -and $gaResume.canonicalPatientCreated -and
        $gaNotice.acknowledged -and $gaNotice.applicantStatus-eq'SyntheticTelehealthNoticeAcknowledged' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaNotice|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'state',n.current_location_state_code,'noticeKey',n.notice_key,'noticeVersion',n.notice_version,
  'sourceTitle',n.notice_source_title,'sourceUrl',n.notice_source_url,
  'legalReviewStatus',n.legal_review_status,'legalConsent',n.legal_consent_established,
  'clinicianConsent',n.clinician_consent_documented,'portal',n.portal_account_created,
  'request',n.request_created,'queue',n.queue_enabled,'care',n.care_enabled,
  'patientPortalEnabled',p.portal_enabled,'merged',p.merged_into_patient_id))
from telehealth_applicant_notice_acknowledgments n
join patients p on p.canonical_id=n.canonical_patient_id
where n.applicant_id in ('$gaId','$caId','$($fl.Created.applicantId)');
"@)|ConvertFrom-Json
    Add-NoticeCheck 'Database evidence binds all three state notices to portal-disabled unmerged promoted shells with every consequential flag false' (
        @($evidence).Count-eq 3 -and
        @($evidence|Where-Object{$_.legalReviewStatus-eq'PendingIndependentReview'-and-not$_.legalConsent-and-not$_.clinicianConsent-and-not$_.portal-and-not$_.request-and-not$_.queue-and-not$_.care-and-not$_.patientPortalEnabled-and$null-eq$_.merged}).Count-eq 3)

    $gaAcknowledgmentId=Invoke-Scalar "select acknowledgment_id from telehealth_applicant_notice_acknowledgments where applicant_id='$gaId';"
    Add-NoticeCheck 'Notice acknowledgment evidence and aggregate event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_notice_acknowledgments set notice_version=2 where acknowledgment_id='$gaAcknowledgmentId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_notice_acknowledgments where acknowledgment_id='$gaAcknowledgmentId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$gaId' and action='prospective-telehealth-notice-acknowledged';"))
    Add-NoticeCheck 'Notice evidence stores no raw patient, portal credential, clinical, insurance, or external payload columns' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_notice_acknowledgments'
  and column_name in ('legal_first_name','legal_last_name','date_of_birth','email','phone','address','portal_username',
    'password_hash','member_id','group_number','complaint','diagnosis','medication','prescription','raw_request','raw_response');
"@)-eq 0)

    $after=Get-NoticeCounts
    $downstream=@('portalAccounts','portalIdentityMappings','insuranceRecords','requests','queueEntries','intakeSnapshots','patientConfirmations','patientLocations','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    Add-NoticeCheck 'Three state acknowledgments add only three notice receipts with zero downstream delta' (
        [long]$after.noticeAcknowledgments-[long]$before.noticeAcknowledgments-eq 3 -and
        @($downstream|Where-Object{[long]$before.$_-ne[long]$after.$_}).Count-eq 0) @{before=$before;after=$after}
}
catch { Add-NoticeCheck 'Applicant state-notice proof execution' $false $_.Exception.Message }
finally {
    $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decision='TH-DEC-0027';checks=$checks}
    $result|ConvertTo-Json -Depth 14|Set-Content -Encoding utf8 $resultPath
    if(-not$passed){throw "Applicant state-notice proof failed. See $resultPath"}
    Write-Host "Applicant state-notice proof passed ($($checks.Count) checks). Artifact: $resultPath"
}
