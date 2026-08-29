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
    throw 'Telehealth applicant request intake proof is local-only.'
}

# Reuse and re-prove the complete Sprint 43 source path before creating a
# fresh GA/CA/FL candidate set for this slice.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestComplaintTriage.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$priorPurposeCategory = $env:AVENCHART_TELEHEALTH_TEST_PURPOSE_CATEGORY
$env:AVENCHART_TELEHEALTH_TEST_PURPOSE_CATEGORY = 'migraine'
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestLocation.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName
$intakeApplicants = @(
    [pscustomobject]@{Id=$gaId;Secret=$gaSecret;State='GA'},
    [pscustomobject]@{Id=$caId;Secret=$caSecret;State='CA'},
    [pscustomobject]@{Id=$flId;Secret=$flSecret;State='FL'}
)
if ($null -eq $priorPurposeCategory) {
    Remove-Item Env:AVENCHART_TELEHEALTH_TEST_PURPOSE_CATEGORY -ErrorAction SilentlyContinue
}
else {
    $env:AVENCHART_TELEHEALTH_TEST_PURPOSE_CATEGORY = $priorPurposeCategory
}

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-intake.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-IntakeCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:passed=$false}
}
function Intake-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/intake"
}
function New-IntakeBody([object]$Applicant,[object]$Ready,[string]$Duration='1-3-days') {
    [ordered]@{
        expectedRequestVersion=4
        contextSnapshotFingerprint=[string]$Ready.contextSnapshotFingerprint
        currentLocationStateCode=[string]$Applicant.State
        symptomDuration=$Duration
        currentLocationConfirmed=$true
        callbackNumberConfirmed=$true
        priorInformationReviewed=$true
        insuranceLimitationsAcknowledged=$true
        pendingConsentAcknowledged=$true
        pendingVerificationAcknowledged=$true
        complaintResultAcknowledged=$true
        syntheticDataConfirmed=$true
    }
}
function Copy-IntakeBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-IntakeMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedIntakePosts([object]$Applicant,[object]$Body) {
    $url="$ApiBaseUrl$(Intake-Path $Applicant.Id)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $headers=Applicant-Headers $Applicant.Secret (New-Key "sp44-contention-$ordinal")
        $jobs+=Start-Job -ScriptBlock {
            param($RequestUrl,$RequestHeaders,$RequestBody)
            try {
                $response=Invoke-WebRequest -Uri $RequestUrl -Method Post -Headers $RequestHeaders `
                    -ContentType 'application/json' -Body $RequestBody -TimeoutSec 60
                [int]$response.StatusCode
            }
            catch {
                if($null-ne$_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}
            }
        } -ArgumentList $url,$headers,$bodyJson
    }
    try {
        $jobs|Wait-Job -Timeout 90|Out-Null
        @($jobs|Receive-Job)|Sort-Object
    }
    finally {
        $jobs|Remove-Job -Force -ErrorAction SilentlyContinue
    }
}

try {
    foreach($applicant in $intakeApplicants) {
        $safetyReady=Get-ApplicantStep $applicant (Safety-Path $applicant.Id)
        $null=Post-ApplicantStep $applicant (Safety-Path $applicant.Id) `
            (New-UniversalPassBody $safetyReady) (New-Key 'sp44-universal-pass')
        $complaintReady=Get-ApplicantStep $applicant (Complaint-Path $applicant.Id)
        $null=Post-ApplicantStep $applicant (Complaint-Path $applicant.Id) `
            (New-ComplaintBody $applicant $complaintReady 'TelehealthEligible') `
            (New-Key 'sp44-complaint-pass')
        $applicant | Add-Member -NotePropertyName Ready -NotePropertyValue (
            Get-ApplicantStep $applicant (Intake-Path $applicant.Id))
    }

    Add-IntakeCheck 'GA, CA, and FL projections are private, masked, no-free-text, publication-blocked, and explicitly pending' (
        @($intakeApplicants|Where-Object{
            $_.Ready.requestStatus-eq'Intake' -and $_.Ready.requestVersion-eq 4 -and
            $_.Ready.snapshotReady -and -not$_.Ready.snapshotCreated -and
            $_.Ready.complaintCategory-eq'migraine' -and
            $_.Ready.complaintDisplayLabel-eq'Migraine' -and
            $_.Ready.clinicalContentStatus-eq'UNAPPROVED_SYNTHETIC' -and
            -not$_.Ready.medicalDirectorApprovalRecorded -and
            -not$_.Ready.clinicalGoldenCasePackApproved -and
            -not$_.Ready.productionPublicationAllowed -and
            $_.Ready.verificationPending -and $_.Ready.consentPending -and
            -not$_.Ready.coverageRecordCreated -and -not$_.Ready.coverageVerified -and
            -not$_.Ready.exactNetworkConfirmed -and -not$_.Ready.operationalReviewCreated -and
            @($_.Ready.supportedSymptomDurations).Count-eq 4 -and
            @($_.Ready.sections).Count-eq 9 -and
            $_.Ready.maskedCallbackPhone-match'^\*{3}-\*{3}-[0-9]{4}$' -and
            $null-eq$_.Ready.PSObject.Properties['complaintSummary'] -and
            $null-eq$_.Ready.PSObject.Properties['sourceComplaintContextFingerprint'] -and
            $null-eq$_.Ready.PSObject.Properties['patientId']
        }).Count-eq 3)

    $spare=$intakeApplicants[2]
    $validSpare=New-IntakeBody $spare $spare.Ready
    $missing=Copy-IntakeBody $validSpare;$missing.PSObject.Properties.Remove('complaintResultAcknowledged')
    $falseConfirmation=Copy-IntakeBody $validSpare;$falseConfirmation.pendingConsentAcknowledged=$false
    $badDuration=Copy-IntakeBody $validSpare;$badDuration.symptomDuration='about-a-week'
    $stale=Copy-IntakeBody $validSpare;$stale.expectedRequestVersion=5
    $badSnapshot=Copy-IntakeBody $validSpare;$badSnapshot.contextSnapshotFingerprint=('0'*64)
    Add-IntakeCheck 'Missing/foreign access, missing or false confirmation, uncontrolled duration, stale version, and source snapshot drift fail before evidence' (
        (Get-StepStatus 'GET' $spare (Intake-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (Intake-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (Intake-Path $spare.Id) $spare.Secret $missing (New-Key 'sp44-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Intake-Path $spare.Id) $spare.Secret $falseConfirmation (New-Key 'sp44-false'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Intake-Path $spare.Id) $spare.Secret $badDuration (New-Key 'sp44-duration'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Intake-Path $spare.Id) $spare.Secret $stale (New-Key 'sp44-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Intake-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp44-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_intake_snapshots where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $results=@{}
    $keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$intakeApplicants[$index]
        $body=New-IntakeBody $applicant $applicant.Ready $(if($index-eq 0){'less-than-day'}else{'more-than-14-days'})
        $key=New-Key 'sp44-intake'
        $keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-ApplicantStep $applicant `
            (Intake-Path $applicant.Id) $body $key
    }
    $contended=$intakeApplicants[1]
    $contentionStatuses=Invoke-ContendedIntakePosts $contended (New-IntakeBody $contended $contended.Ready '4-14-days')
    $results[$contended.Id]=Get-ApplicantStep $contended (Intake-Path $contended.Id)
    Add-IntakeCheck 'First-writer contention creates one result and every GA, CA, and FL request advances only to Verification version 5' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($intakeApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'Verification' -and $result.requestVersion-eq 5 -and
            $result.snapshotCreated -and -not$result.snapshotReady -and
            $result.verificationPending -and $result.consentPending -and
            -not$result.coverageRecordCreated -and -not$result.coverageVerified -and
            -not$result.exactNetworkConfirmed -and -not$result.operationalReviewCreated -and
            -not$result.practiceAccepted -and -not$result.patientContacted -and
            -not$result.patientCareQueueEntered -and -not$result.clinicianQueueEntered -and
            -not$result.doctorSearchStarted -and -not$result.queuePositionAssigned -and
            -not$result.appointmentCreated -and -not$result.encounterCreated -and
            -not$result.consentCreated -and -not$result.careAuthorized -and
            -not$result.prescribingEnabled -and -not$result.billingEnabled -and
            -not$result.claimCreated -and -not$result.integrationEnabled -and
            -not$result.externalCallPerformed
        }).Count-eq 3)

    $first=$intakeApplicants[0]
    $firstBody=New-IntakeBody $first $first.Ready 'less-than-day'
    $replay=Post-ApplicantStep $first (Intake-Path $first.Id) $firstBody $keys[$first.Id]
    $changed=Copy-IntakeBody $firstBody;$changed.symptomDuration='1-3-days'
    Add-IntakeCheck 'Exact semantic replay is stable while changed-key reuse and a second command fail closed' (
        $replay.capturedAt-eq$results[$first.Id].capturedAt -and
        $replay.symptomDuration-eq'less-than-day' -and
        (Get-StepStatus 'POST' $first (Intake-Path $first.Id) $first.Secret $changed $keys[$first.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first (Intake-Path $first.Id) $first.Secret $firstBody (New-Key 'sp44-second'))-eq 409)

    $requestIdsSql=@($intakeApplicants|ForEach-Object{"'$($_.Ready.requestId)'::uuid"})-join','
    $applicantIdsSql=@($intakeApplicants|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    Add-IntakeCheck 'Database evidence contains exactly one fixed-summary intake, protected receipt, and version-5 event per request with upstream aggregates unchanged' (
        [int](Invoke-Scalar "select count(*) from telehealth_intake_snapshots where request_id in ($requestIdsSql);")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_intake_snapshots where request_id in ($requestIdsSql);")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_intake_snapshots where request_id in ($requestIdsSql) and complaint_summary='Synthetic migraine intake demonstration' and clinical_content_status='UNAPPROVED_SYNTHETIC' and not medical_director_approval_recorded and not clinical_golden_case_pack_approved and not production_publication_allowed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=5 and action='applicant-intake-snapshot-confirmed';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='Verification' and version=5 and triage_outcome='TelehealthEligible' and ready_at is null and appointment_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id in ($applicantIdsSql) and status='SyntheticRequestCreated' and version=26;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_triage_assessments where request_id in ($requestIdsSql);")-eq 6 -and
        [int](Invoke-Scalar "select count(*) from telehealth_patient_confirmations where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_demonstration_acknowledgments where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_selections where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_verifications where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_video_sessions where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_consultation_contexts where request_id in ($requestIdsSql);")-eq 0)

    $firstReceipt=Invoke-Scalar "select receipt_id from telehealth_applicant_request_intake_snapshots where applicant_id='$($first.Id)'::uuid;"
    $firstIntake=Invoke-Scalar "select intake_id from telehealth_applicant_request_intake_snapshots where receipt_id='$firstReceipt'::uuid;"
    Add-IntakeCheck 'Protected and generic intake evidence is append-only and false publication/downstream gates reject mutation' (
        (Test-IntakeMutationRejected "update telehealth_applicant_request_intake_snapshots set production_publication_allowed=true where receipt_id='$firstReceipt'::uuid; select 'bad';") -and
        (Test-IntakeMutationRejected "update telehealth_applicant_request_intake_snapshots set coverage_verified=true where receipt_id='$firstReceipt'::uuid; select 'bad';") -and
        (Test-IntakeMutationRejected "delete from telehealth_applicant_request_intake_snapshots where receipt_id='$firstReceipt'::uuid; select 'bad';") -and
        (Test-IntakeMutationRejected "update telehealth_intake_snapshots set symptom_duration='1-3-days' where intake_id='$firstIntake'::uuid; select 'bad';") -and
        (Test-IntakeMutationRejected "update telehealth_applicant_request_complaint_triage_assessments set clinical_content_status='APPROVED' where applicant_id='$($first.Id)'::uuid; select 'bad';"))
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{
        generatedAt=(Get-Date).ToUniversalTime().ToString('o')
        apiBaseUrl=$ApiBaseUrl
        database=$DatabaseName
        passed=$passed
        checkCount=$checks.Count
        checks=$checks
    }
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$passed){throw "Telehealth applicant request intake proof failed. See $resultPath"}
Write-Host "Telehealth applicant request intake proof passed $($checks.Count) checks."
