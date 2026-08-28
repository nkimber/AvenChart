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
    throw 'Telehealth applicant registration-details proof is local-only.'
}

# Reuse the sealed Sprint 24 proof in-process. It leaves one Georgia, one
# California, and one Florida applicant at the acknowledged-notice gate with
# portal-disabled promoted patient shells created through the public API.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantNoticeAcknowledgment.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-registration-details-confirmation.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-RegistrationDetailsCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function New-RegistrationDetailsBody([int]$Version,[string]$Fingerprint) {
    @{
        expectedVersion=$Version
        detailsFingerprint=$Fingerprint
        legalNameAndBirthDateConfirmed=$true
        contactChannelsConfirmed=$true
        residenceRegionConfirmed=$true
        noCorrectionsNeededConfirmed=$true
        syntheticDataConfirmed=$true
    }
}
function Get-RegistrationDetails([object]$Applicant) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/registration-details" `
        -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret} -TimeoutSec 30
}
function Invoke-RegistrationDetails([object]$Applicant,[hashtable]$Body,[string]$Key) {
    Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$($Applicant.Created.applicantId)/registration-details/confirmation" `
        -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$Applicant.Secret
            'X-Idempotency-Key'=$Key
        } -ContentType 'application/json' -TimeoutSec 30 -Body ($Body|ConvertTo-Json -Depth 8)
}
function Get-RegistrationDetailsCounts {
    (Invoke-Scalar @"
select json_build_object(
  'confirmations',(select count(*) from telehealth_applicant_registration_details_confirmations),
  'patients',(select count(*) from patients),
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
    $gaId=[string]$success.Created.applicantId
    $caId=[string]$race.Created.applicantId
    $flId=[string]$fl.Created.applicantId
    $before=Get-RegistrationDetailsCounts
    $patientHashesBefore=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable

    $gaWeb=Invoke-WebRequest "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId/registration-details" -Headers @{
        'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret
    } -TimeoutSec 30
    $gaDetails=$gaWeb.Content|ConvertFrom-Json
    $gaApplicant=(Invoke-Scalar "select json_build_object('email',email,'phone',phone) from telehealth_prospective_applicants where applicant_id='$gaId';")|ConvertFrom-Json
    $gaJson=$gaDetails|ConvertTo-Json -Depth 12 -Compress
    Add-RegistrationDetailsCheck 'Applicant-owned read exposes only the exact copied minimum details with masked contacts and no canonical identifier' (
        $gaWeb.Headers['Cache-Control']-match'no-store' -and
        $gaDetails.applicantStatus-eq'SyntheticTelehealthNoticeAcknowledged' -and [int]$gaDetails.applicantVersion-eq 13 -and
        $gaDetails.legalFirstName -and $gaDetails.legalLastName -and $gaDetails.dateOfBirth -and
        $gaDetails.maskedEmail-ne$gaApplicant.email -and $gaDetails.maskedPhone-ne$gaApplicant.phone -and
        $gaDetails.residenceStateCode-eq'GA' -and $gaDetails.postalCode -and
        ([string]$gaDetails.detailsFingerprint)-match'^[0-9a-f]{64}$' -and
        $gaDetails.policyKey-eq'SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION' -and
        -not$gaDetails.confirmed -and -not$gaDetails.identityAssuranceEstablished -and
        -not$gaDetails.patientRecordChanged -and -not$gaDetails.correctionCompleted -and
        -not$gaDetails.intakeCompleted -and -not$gaDetails.legalConsentEstablished -and
        -not$gaDetails.practiceAccepted -and -not$gaDetails.insuranceConfirmed -and
        -not$gaDetails.coverageCreated -and -not$gaDetails.requestCreated -and
        -not$gaDetails.queueEnabled -and -not$gaDetails.careEnabled -and
        $gaJson-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|noticeAcknowledgmentId|portalSession|commandFingerprint' -and
        $gaJson-notmatch[regex]::Escape([string]$gaApplicant.email) -and
        $gaJson-notmatch[regex]::Escape([string]$gaApplicant.phone))

    $gaBody=New-RegistrationDetailsBody 13 ([string]$gaDetails.detailsFingerprint)
    Add-RegistrationDetailsCheck 'Anonymous, wrong-key, partial, stale-version, and stale-snapshot commands fail before persistence' (
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/registration-details")-eq 401 -and
        (Invoke-Status 'GET' "/api/telehealth/v1/applicants/$gaId/registration-details" @{'X-AvenChart-Telehealth-Applicant-Key'=(New-Secret)})-eq 404 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/registration-details/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp25-partial')} (Merge-Map $gaBody @{contactChannelsConfirmed=$false}))-eq 400 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/registration-details/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp25-stale')} (Merge-Map $gaBody @{expectedVersion=12}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/registration-details/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp25-fingerprint')} (Merge-Map $gaBody @{detailsFingerprint=('0'*64)}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_registration_details_confirmations where applicant_id='$gaId';")-eq 0)

    $flPatientId=Invoke-Scalar "select canonical_patient_id from telehealth_applicant_synthetic_promotions where applicant_id='$flId';"
    Invoke-Scalar "update patients set portal_enabled=true where canonical_id='$flPatientId';" | Out-Null
    $portalStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/registration-details" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar "update patients set portal_enabled=false where canonical_id='$flPatientId';" | Out-Null
    Invoke-Scalar "update patients set last_name=last_name||'Drift' where canonical_id='$flPatientId';" | Out-Null
    $driftStatus=Invoke-Status 'GET' "/api/telehealth/v1/applicants/$flId/registration-details" @{'X-AvenChart-Telehealth-Applicant-Key'=$fl.Secret}
    Invoke-Scalar @"
update patients p set last_name=a.legal_last_name
from telehealth_applicant_synthetic_promotions promotion
join telehealth_prospective_applicants a on a.applicant_id=promotion.applicant_id
where p.canonical_id=promotion.canonical_patient_id and a.applicant_id='$flId';
"@ | Out-Null
    Add-RegistrationDetailsCheck 'Portal-enabled or applicant-to-patient data-drift conditions fail closed before confirmation' (
        $portalStatus-eq 409 -and $driftStatus-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_registration_details_confirmations where applicant_id='$flId';")-eq 0)

    $gaKey=New-Key 'sp25-ga'
    $gaRecorded=Invoke-RegistrationDetails $success $gaBody $gaKey
    $gaReplay=Invoke-RegistrationDetails $success $gaBody $gaKey
    $gaRecordedJson=$gaRecorded|ConvertTo-Json -Depth 12 -Compress
    Add-RegistrationDetailsCheck 'Georgia confirmation records one no-edit, no-consequence result and exact replay converges' (
        $gaRecorded.applicantStatus-eq'SyntheticMinimumRegistrationDetailsConfirmed' -and [int]$gaRecorded.applicantVersion-eq 14 -and
        $gaRecorded.confirmed -and $null-ne$gaRecorded.confirmedAt -and
        -not$gaRecorded.identityAssuranceEstablished -and -not$gaRecorded.patientRecordChanged -and
        -not$gaRecorded.correctionCompleted -and -not$gaRecorded.intakeCompleted -and
        -not$gaRecorded.legalConsentEstablished -and -not$gaRecorded.practiceAccepted -and
        -not$gaRecorded.insuranceConfirmed -and -not$gaRecorded.coverageCreated -and
        -not$gaRecorded.requestCreated -and -not$gaRecorded.queueEnabled -and -not$gaRecorded.careEnabled -and
        $gaReplay.confirmedAt-eq$gaRecorded.confirmedAt -and
        $gaRecordedJson-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|promotionId|noticeAcknowledgmentId|portalSession|commandFingerprint')
    Add-RegistrationDetailsCheck 'Changed idempotency reuse and a second confirmation fail closed without duplicate evidence' (
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/registration-details/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=$gaKey} (Merge-Map $gaBody @{expectedVersion=14}))-eq 409 -and
        (Invoke-Status 'POST' "/api/telehealth/v1/applicants/$gaId/registration-details/confirmation" @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret;'X-Idempotency-Key'=(New-Key 'sp25-second')} (Merge-Map $gaBody @{expectedVersion=14}))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_registration_details_confirmations where applicant_id='$gaId';")-eq 1)

    $caDetails=Get-RegistrationDetails $race
    $caBody=New-RegistrationDetailsBody 13 ([string]$caDetails.detailsFingerprint)
    $caJsonBody=$caBody|ConvertTo-Json -Depth 8 -Compress
    $caRaceKey=New-Key 'sp25-ca-race'
    $jobs=1..8|ForEach-Object{Start-ThreadJob -ScriptBlock {
        param($base,$id,$secret,$body,$key)
        try{[int](Invoke-WebRequest "$base/api/telehealth/v1/applicants/$id/registration-details/confirmation" -Method Post -Headers @{
            'X-AvenChart-Telehealth-Applicant-Key'=$secret;'X-Idempotency-Key'=$key
        } -ContentType 'application/json' -TimeoutSec 30 -Body $body).StatusCode}catch{if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
    } -ArgumentList $ApiBaseUrl,$caId,$race.Secret,$caJsonBody,$caRaceKey}
    $statuses=@($jobs|Wait-Job|Receive-Job);$jobs|Remove-Job -Force
    Add-RegistrationDetailsCheck 'Eight concurrent exact California retries converge on one confirmation and one aggregate event' (
        @($statuses|Where-Object{$_-eq 200}).Count-eq 8 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_registration_details_confirmations where applicant_id='$caId';")-eq 1 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_events where applicant_id='$caId' and action='prospective-minimum-registration-details-confirmed';")-eq 1) @{statuses=$statuses}

    $flDetails=Get-RegistrationDetails $fl
    $flBody=New-RegistrationDetailsBody 13 ([string]$flDetails.detailsFingerprint)
    $flRecorded=Invoke-RegistrationDetails $fl $flBody (New-Key 'sp25-fl')
    Add-RegistrationDetailsCheck 'Florida follows the same bounded minimum-details policy without creating a state-specific clinical or insurance conclusion' (
        $flRecorded.applicantStatus-eq'SyntheticMinimumRegistrationDetailsConfirmed' -and
        -not$flRecorded.identityAssuranceEstablished -and -not$flRecorded.insuranceConfirmed -and
        -not$flRecorded.requestCreated -and -not$flRecorded.careEnabled)

    $gaResume=Invoke-RestMethod "$ApiBaseUrl/api/telehealth/v1/applicants/$gaId" -Headers @{'X-AvenChart-Telehealth-Applicant-Key'=$success.Secret} -TimeoutSec 30
    $gaReload=Get-RegistrationDetails $success
    Add-RegistrationDetailsCheck 'Applicant resume and detail reload expose confirmed state without a portal or canonical patient identifier' (
        $gaResume.status-eq'SyntheticMinimumRegistrationDetailsConfirmed' -and $gaResume.canonicalPatientCreated -and
        $gaReload.confirmed -and $gaReload.applicantStatus-eq'SyntheticMinimumRegistrationDetailsConfirmed' -and
        (($gaResume|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession') -and
        (($gaReload|ConvertTo-Json -Depth 10 -Compress)-notmatch'canonicalPatientId|legacyPid|pubpid|TH-PAT-|portalSession'))

    $evidence=(Invoke-Scalar @"
select json_agg(json_build_object(
  'status',c.resulting_applicant_status,'version',c.resulting_applicant_version,
  'policy',c.policy_key,'policyVersion',c.policy_version,'evidenceType',c.evidence_type,
  'nameDob',c.legal_name_birth_date_confirmed,'contacts',c.contact_channels_confirmed,
  'residence',c.residence_region_confirmed,'noCorrections',c.no_corrections_needed_confirmed,
  'synthetic',c.synthetic_data_confirmed,'identity',c.identity_assurance_established,
  'patientChanged',c.patient_record_changed,'correction',c.correction_completed,
  'intake',c.intake_completed,'consent',c.legal_consent_established,
  'practice',c.practice_accepted,'insurance',c.insurance_confirmed,'coverage',c.coverage_created,
  'financial',c.financial_record_created,'request',c.request_created,'queue',c.queue_enabled,
  'appointment',c.appointment_created,'encounter',c.encounter_created,'care',c.care_enabled,
  'prescribing',c.prescribing_enabled,'claim',c.claim_created,'communication',c.communication_enabled,
  'integration',c.integration_enabled,'externalCall',c.external_call_performed,
  'portal',p.portal_enabled,'merged',p.merged_into_patient_id,
  'noticeVersion',n.resulting_applicant_version,'promotionOutcome',promotion.outcome))
from telehealth_applicant_registration_details_confirmations c
join telehealth_applicant_notice_acknowledgments n on n.acknowledgment_id=c.notice_acknowledgment_id
join telehealth_applicant_synthetic_promotions promotion on promotion.promotion_id=c.promotion_id
join patients p on p.canonical_id=c.canonical_patient_id
where c.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json
    Add-RegistrationDetailsCheck 'Database provenance binds all confirmations to acknowledged notices and portal-disabled unmerged promoted shells with every consequential flag false' (
        @($evidence).Count-eq 3 -and
        @($evidence|Where-Object{
            $_.status-eq'SyntheticMinimumRegistrationDetailsConfirmed'-and[int]$_.version-eq 14-and
            $_.policy-eq'SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION'-and[int]$_.policyVersion-eq 1-and
            $_.evidenceType-eq'PROMOTED_PATIENT_MINIMUM_DETAILS_NO_EDIT_CONFIRMATION'-and
            $_.nameDob-and$_.contacts-and$_.residence-and$_.noCorrections-and$_.synthetic-and
            -not$_.identity-and-not$_.patientChanged-and-not$_.correction-and-not$_.intake-and-not$_.consent-and
            -not$_.practice-and-not$_.insurance-and-not$_.coverage-and-not$_.financial-and-not$_.request-and
            -not$_.queue-and-not$_.appointment-and-not$_.encounter-and-not$_.care-and-not$_.prescribing-and
            -not$_.claim-and-not$_.communication-and-not$_.integration-and-not$_.externalCall-and
            -not$_.portal-and$null-eq$_.merged-and[int]$_.noticeVersion-eq 13-and$_.promotionOutcome-eq'SyntheticPatientCreated'
        }).Count-eq 3)

    $gaConfirmationId=Invoke-Scalar "select confirmation_id from telehealth_applicant_registration_details_confirmations where applicant_id='$gaId';"
    Add-RegistrationDetailsCheck 'Confirmation evidence and aggregate event are append-only' (
        (Test-MutationRejected "update telehealth_applicant_registration_details_confirmations set policy_version=2 where confirmation_id='$gaConfirmationId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_registration_details_confirmations where confirmation_id='$gaConfirmationId';") -and
        (Test-MutationRejected "delete from telehealth_applicant_events where applicant_id='$gaId' and action='prospective-minimum-registration-details-confirmed';"))
    Add-RegistrationDetailsCheck 'Confirmation evidence stores no raw registration, portal credential, clinical, insurance, or external payload columns' (
        [int](Invoke-Scalar @"
select count(*) from information_schema.columns
where table_schema='public' and table_name='telehealth_applicant_registration_details_confirmations'
  and column_name in ('legal_first_name','legal_last_name','date_of_birth','email','phone','postal_code','address','portal_username',
    'password_hash','member_id','group_number','complaint','diagnosis','medication','prescription','raw_request','raw_response');
"@)-eq 0)

    $patientHashesAfter=(Invoke-Scalar @"
select json_object_agg(p.canonical_id,md5(row_to_json(p)::text))
from patients p
join telehealth_applicant_synthetic_promotions promotion on promotion.canonical_patient_id=p.canonical_id
where promotion.applicant_id in ('$gaId','$caId','$flId');
"@)|ConvertFrom-Json -AsHashtable
    $after=Get-RegistrationDetailsCounts
    $downstream=@('patients','portalAccounts','portalIdentityMappings','insuranceRecords','requests','queueEntries','intakeSnapshots','patientConfirmations','patientLocations','coverageSelections','coverageVerifications','appointments','encounters','claims','prescriptions')
    Add-RegistrationDetailsCheck 'Three confirmations add only three immutable receipts with zero patient-record or downstream delta' (
        [long]$after.confirmations-[long]$before.confirmations-eq 3 -and
        @($downstream|Where-Object{[long]$before.$_-ne[long]$after.$_}).Count-eq 0 -and
        @($patientHashesBefore.Keys|Where-Object{$patientHashesBefore[$_]-ne$patientHashesAfter[$_]}).Count-eq 0) @{before=$before;after=$after}
}
catch { Add-RegistrationDetailsCheck 'Applicant registration-details proof execution' $false $_.Exception.Message }
finally {
    $result=[ordered]@{status=$(if($passed){'passed'}else{'failed'});generatedAtUtc=(Get-Date).ToUniversalTime().ToString('O');decision='TH-DEC-0028';checks=$checks}
    $result|ConvertTo-Json -Depth 14|Set-Content -Encoding utf8 $resultPath
    if(-not$passed){throw "Applicant registration-details proof failed. See $resultPath"}
    Write-Host "Applicant registration-details proof passed ($($checks.Count) checks). Artifact: $resultPath"
}
