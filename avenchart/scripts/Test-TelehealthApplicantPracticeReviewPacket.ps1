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
    throw 'Telehealth practice-review packet proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantPracticeReviewClaim.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-practice-review-packet.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true
$patientEmailToRestore = $null
$patientIdToRestore = $null

function Add-PacketCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function Packet-Path([string]$CaseId) { "/api/telehealth/v1/admin/applicant-practice-review/$CaseId" }
function Get-Packet([string]$CaseId,[hashtable]$Headers=$adminHeaders) {
    Invoke-RestMethod "$ApiBaseUrl$(Packet-Path $CaseId)" -Headers $Headers -TimeoutSec 40
}
function Get-PacketStatus([string]$CaseId,[hashtable]$Headers=$adminHeaders) {
    try { $null=Get-Packet $CaseId $Headers; 200 }
    catch { if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw} }
}
function Get-ClaimFingerprint {
    Invoke-Scalar @"
select md5(coalesce(jsonb_agg(to_jsonb(claim) order by claim.claim_id)::text,'[]'))
from telehealth_practice_review_claims claim
where claim.case_id in ('$gaCase'::uuid,'$caCase'::uuid,'$flCase'::uuid);
"@
}

try {
    $auditBefore=[int](Invoke-Scalar "select count(*) from phi_access_audit_events where resource_type='TelehealthApplicantPracticeReviewPacket' and facility_id=10 and required_permission like 'acl.patients.demo.view@%' and authorized;")
    $claimFingerprintBefore=Get-ClaimFingerprint
    $productBefore=Get-ClaimProductFingerprint

    $packets=@(
        @{state='GA';case=$gaCase;packet=(Get-Packet $gaCase)},
        @{state='CA';case=$caCase;packet=(Get-Packet $caCase)},
        @{state='FL';case=$flCase;packet=(Get-Packet $flCase)}
    )
    Add-PacketCheck 'Current claimant can read the exact Georgia, California, and Florida packets' (
        @($packets|Where-Object{
            $_.packet.practiceReviewCaseId-eq$_.case -and
            $_.packet.residenceStateCode-eq$_.state -and
            $_.packet.policyKey-eq'SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET' -and
            $_.packet.policyVersion-eq 1 -and $_.packet.assigned -and
            $_.packet.assignedToCurrentUser -and $_.packet.staffActionTaken
        }).Count-eq 3)

    Add-PacketCheck 'Packets contain only masked operational registration and synthetic insurance evidence' (
        @($packets|Where-Object{
            $_.packet.maskedEmail-match'[•*]' -and $_.packet.maskedPhone-match'[•*]' -and
            $_.packet.insurance.memberIdMask-match'^••••[A-Z0-9-]{4}$' -and
            ($null-eq$_.packet.insurance.groupNumberMask -or $_.packet.insurance.groupNumberMask-match'^••••[A-Z0-9-]{4}$') -and
            $_.packet.insurance.eligibilityBusinessOutcome-eq'EligibleBenefitsReported' -and
            $_.packet.insurance.practiceNetworkBusinessOutcome-eq'PracticeInNetworkAcceptingNewPatients' -and
            -not$_.packet.insurance.renderingPhysicianNetworkChecked -and
            -not$_.packet.insurance.coverageVerified -and -not$_.packet.insurance.exactNetworkConfirmed
        }).Count-eq 3)

    Add-PacketCheck 'Communication and device facts remain client-reported preparation without completed arrangements or readiness' (
        @($packets|Where-Object{
            $_.packet.communicationAccess.safePrivateCommunicationConfirmed -and
            -not$_.packet.communicationAccess.interpreterAssigned -and
            -not$_.packet.communicationAccess.accessibilityAccommodationArranged -and
            -not$_.packet.communicationAccess.communicationArrangementCompleted -and
            $_.packet.devicePreparation.browserSupported -and $_.packet.devicePreparation.cameraAvailable -and
            $_.packet.devicePreparation.microphoneAvailable -and $_.packet.devicePreparation.speakerAvailable -and
            -not$_.packet.devicePreparation.technologyReady -and -not$_.packet.devicePreparation.waitingRoomCreated -and
            -not$_.packet.devicePreparation.mediaSessionCreated
        }).Count-eq 3)

    $packetJson=$packets.packet|ConvertTo-Json -Depth 20 -Compress
    Add-PacketCheck 'Packet minimization excludes source IDs, claimant identity, chart links, clinical selections, and free text' (
        $packetJson-notmatch '"(applicantId|patientId|canonicalPatientId|claimId|assignedToActorId|assignedToStaffId|promotionId|confirmationId|readinessId|accessKey|fingerprint|traceToken|medication|allergy|healthHistory|selectedItemCount|additionalItems|narrative|freeText|streetAddress|employer|guardian|clinicianId|providerId)"' -and
        $packetJson-notmatch 'patient chart link|open chart') @{responseBytes=[Text.Encoding]::UTF8.GetByteCount($packetJson)}

    Add-PacketCheck 'Every decision, contact, request, queue, care, financial, integration, and external consequence remains false' (
        @($packets|Where-Object{
            -not$_.packet.priorityAssigned -and -not$_.packet.practiceAccepted -and
            -not$_.packet.practiceDeclined -and -not$_.packet.patientContacted -and
            -not$_.packet.clinicianReviewCreated -and -not$_.packet.telehealthRequestCreated -and
            -not$_.packet.patientCareQueueEntered -and -not$_.packet.clinicianQueueEntered -and
            -not$_.packet.appointmentCreated -and -not$_.packet.encounterCreated -and
            -not$_.packet.careAuthorized -and -not$_.packet.prescribingEnabled -and
            -not$_.packet.billingEnabled -and -not$_.packet.claimCreated -and
            -not$_.packet.integrationEnabled -and -not$_.packet.externalCallPerformed
        }).Count-eq 3)

    Add-PacketCheck 'Another staff member, a provider, and a cross-facility context cannot read the owner packet' (
        (Get-PacketStatus $gaCase $frontdeskHeaders)-eq 404 -and
        (Get-PacketStatus $gaCase $providerHeaders)-eq 403 -and
        (Get-PacketStatus $gaCase $crossFacility)-in@(403,404))

    $patientIdToRestore=Invoke-Scalar "select canonical_patient_id from telehealth_prospective_practice_review_cases where case_id='$gaCase'::uuid;"
    $patientEmailToRestore=Invoke-Scalar "select email from patients where canonical_id='$patientIdToRestore';"
    $escapedPatientId=$patientIdToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set email=email||'.drift' where canonical_id='$escapedPatientId'; select 'drifted';"
    $driftStatus=Get-PacketStatus $gaCase
    $escapedEmail=$patientEmailToRestore.Replace("'","''")
    $null=Invoke-Scalar "update patients set email='$escapedEmail' where canonical_id='$escapedPatientId'; select 'restored';"
    $patientEmailToRestore=$null
    Add-PacketCheck 'Copied patient-shell drift fails closed and exact restoration recovers the packet' (
        $driftStatus-eq 404 -and (Get-PacketStatus $gaCase)-eq 200)

    $claimFingerprintAfter=Get-ClaimFingerprint
    $productAfter=Get-ClaimProductFingerprint
    Add-PacketCheck 'Packet reads do not extend claims or mutate product state' (
        $claimFingerprintBefore-eq$claimFingerprintAfter -and $productBefore-eq$productAfter) @{
            claimBefore=$claimFingerprintBefore;claimAfter=$claimFingerprintAfter
            productBefore=$productBefore;productAfter=$productAfter
        }

    $auditAfter=[int](Invoke-Scalar "select count(*) from phi_access_audit_events where resource_type='TelehealthApplicantPracticeReviewPacket' and facility_id=10 and required_permission like 'acl.patients.demo.view@%' and authorized;")
    Add-PacketCheck 'Successful packet reads create case-correlated healthcare-operations view audit evidence' (
        $auditAfter-ge($auditBefore+4) -and
        [int](Invoke-Scalar "select count(*) from phi_access_audit_events where resource_type='TelehealthApplicantPracticeReviewPacket' and resource_id='$gaCase' and facility_id=10 and purpose_of_use='healthcare-operations' and authorized;")-ge 2) @{before=$auditBefore;after=$auditAfter}

    $waitSeconds=[Math]::Max(0,[Math]::Ceiling(([DateTimeOffset]$packets[0].packet.assignmentExpiresAt-[DateTimeOffset]::UtcNow).TotalSeconds)+2)
    if($waitSeconds-gt 0){Start-Sleep -Seconds $waitSeconds}
    Add-PacketCheck 'Expired claims fail closed without deleting immutable claim history' (
        (Get-PacketStatus $gaCase)-eq 404 -and
        [int](Invoke-Scalar "select count(*) from telehealth_practice_review_claims where case_id='$gaCase'::uuid;")-eq 1) @{waitedSeconds=$waitSeconds}
}
catch {
    $passed=$false
    Add-PacketCheck 'Practice-review packet proof execution' $false "$($_.Exception.Message) | $($_.ScriptStackTrace)"
}
finally {
    if($null-ne$patientEmailToRestore -and $null-ne$patientIdToRestore){
        try {
            $escapedPatientId=$patientIdToRestore.Replace("'","''")
            $escapedEmail=$patientEmailToRestore.Replace("'","''")
            $null=Invoke-Scalar "update patients set email='$escapedEmail' where canonical_id='$escapedPatientId'; select 'restored';"
        } catch { Add-PacketCheck 'Practice-review packet proof restores the copied patient-shell fixture' $false $_.Exception.Message }
    }
}

$artifact=[ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0041'
    checks=$checks
}
$artifact|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
if(-not$passed){throw "Practice-review packet proof failed. See $resultPath"}
Write-Host "Practice-review packet proof passed ($($checks.Count) checks). Artifact: $resultPath"
