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
    throw 'Telehealth applicant request insurance-source proof is local-only.'
}

# Re-prove the complete Sprint 44 source path and retain its fresh GA/CA/FL
# request set at Verification version 5 for this bounded confirmation.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestIntake.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$sourceApplicants = @($intakeApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-insurance-source.json'
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true

function Add-SourceCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:passed=$false}
}
function Source-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/insurance-source"
}
function New-SourceBody([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=5
        insuranceSourceSnapshotFingerprint=[string]$Ready.insuranceSourceSnapshotFingerprint
        payerProductConfirmed=$true
        maskedMemberDetailsConfirmed=$true
        subscriberRelationshipConfirmed=$true
        primaryCoverageSourceConfirmed=$true
        freshVerificationRequested=$true
        evidenceLimitationsAcknowledged=$true
        syntheticDataConfirmed=$true
    }
}
function Copy-SourceBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-SourceMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedSourcePosts([object]$Applicant,[object]$Body) {
    $url="$ApiBaseUrl$(Source-Path $Applicant.Id)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $headers=Applicant-Headers $Applicant.Secret (New-Key "sp45-contention-$ordinal")
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
    foreach($applicant in $sourceApplicants) {
        $applicant | Add-Member -NotePropertyName SourceReady -NotePropertyValue (
            Get-ApplicantStep $applicant (Source-Path $applicant.Id))
    }

    Add-SourceCheck 'GA, CA, and FL projections are private, masked, historical-only, and explicitly pending' (
        @($sourceApplicants|Where-Object{
            $_.SourceReady.requestStatus-eq'Verification' -and
            $_.SourceReady.requestVersion-eq 5 -and
            $_.SourceReady.sourceReady -and -not$_.SourceReady.sourceConfirmed -and
            $_.SourceReady.maskedMemberId-match'^••••[A-Z0-9-]{4}$' -and
            ($null-eq$_.SourceReady.maskedGroupNumber -or
                $_.SourceReady.maskedGroupNumber-match'^••••[A-Z0-9-]{4}$') -and
            $_.SourceReady.coveragePriority-eq'Primary' -and
            -not$_.SourceReady.previousRenderingPhysicianNetworkChecked -and
            -not$_.SourceReady.previousResultReusable -and
            $_.SourceReady.protectedPayloadReferenced -and
            -not$_.SourceReady.protectedPayloadCopied -and
            -not$_.SourceReady.protectedPayloadDecrypted -and
            -not$_.SourceReady.freshVerificationRequested -and
            -not$_.SourceReady.coverageVerified -and
            -not$_.SourceReady.exactNetworkConfirmed -and
            -not$_.SourceReady.renderingPhysicianNetworkChecked -and
            $null-eq$_.SourceReady.PSObject.Properties['memberId'] -and
            $null-eq$_.SourceReady.PSObject.Properties['groupNumber'] -and
            $null-eq$_.SourceReady.PSObject.Properties['protectedPayload'] -and
            $null-eq$_.SourceReady.PSObject.Properties['canonicalPatientId']
        }).Count-eq 3)

    $spare=$sourceApplicants[2]
    $validSpare=New-SourceBody $spare.SourceReady
    $missing=Copy-SourceBody $validSpare;$missing.PSObject.Properties.Remove('freshVerificationRequested')
    $falseConfirmation=Copy-SourceBody $validSpare;$falseConfirmation.evidenceLimitationsAcknowledged=$false
    $stale=Copy-SourceBody $validSpare;$stale.expectedRequestVersion=6
    $badSnapshot=Copy-SourceBody $validSpare;$badSnapshot.insuranceSourceSnapshotFingerprint=('0'*64)
    Add-SourceCheck 'Missing/foreign access, missing or false confirmation, stale version, and changed snapshot fail before evidence' (
        (Get-StepStatus 'GET' $spare (Source-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (Source-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (Source-Path $spare.Id) $spare.Secret $missing (New-Key 'sp45-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Source-Path $spare.Id) $spare.Secret $falseConfirmation (New-Key 'sp45-false'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Source-Path $spare.Id) $spare.Secret $stale (New-Key 'sp45-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Source-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp45-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_insurance_source_confirmations where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $applicantIdsSql=@($sourceApplicants|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    $payloadFingerprintBefore=Invoke-Scalar "select string_agg(md5(protected_payload),',' order by applicant_id) from telehealth_applicant_member_insurance_details where applicant_id in ($applicantIdsSql);"
    $results=@{}
    $keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$sourceApplicants[$index]
        $body=New-SourceBody $applicant.SourceReady
        $key=New-Key 'sp45-source'
        $keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-ApplicantStep $applicant (Source-Path $applicant.Id) $body $key
    }
    $contended=$sourceApplicants[1]
    $contentionStatuses=Invoke-ContendedSourcePosts $contended (New-SourceBody $contended.SourceReady)
    $results[$contended.Id]=Get-ApplicantStep $contended (Source-Path $contended.Id)
    Add-SourceCheck 'First-writer contention creates one result and every request advances only to Verification version 6' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($sourceApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'Verification' -and $result.requestVersion-eq 6 -and
            $result.sourceConfirmed -and -not$result.sourceReady -and
            $result.freshVerificationRequested -and -not$result.previousResultReusable -and
            -not$result.canonicalCoverageCreated -and -not$result.coverageSelected -and
            -not$result.eligibilityVerificationCreated -and -not$result.networkVerificationCreated -and
            -not$result.renderingPhysicianNetworkChecked -and -not$result.coverageVerified -and
            -not$result.exactNetworkConfirmed -and -not$result.financialRouteCreated -and
            -not$result.operationalReviewCreated -and -not$result.practiceAccepted -and
            -not$result.patientContacted -and -not$result.patientCareQueueEntered -and
            -not$result.clinicianQueueEntered -and -not$result.doctorSearchStarted -and
            -not$result.queuePositionAssigned -and -not$result.appointmentCreated -and
            -not$result.encounterCreated -and -not$result.consentCreated -and
            -not$result.careAuthorized -and -not$result.integrationEnabled -and
            -not$result.externalCallPerformed
        }).Count-eq 3)

    $first=$sourceApplicants[0]
    $firstBody=New-SourceBody $first.SourceReady
    $replay=Post-ApplicantStep $first (Source-Path $first.Id) $firstBody $keys[$first.Id]
    $changed=Copy-SourceBody $firstBody;$changed.expectedRequestVersion=6
    Add-SourceCheck 'Exact semantic replay is stable while changed-key reuse and a second command fail closed' (
        $replay.confirmedAt-eq$results[$first.Id].confirmedAt -and
        $replay.insuranceSourceSnapshotFingerprint-eq$results[$first.Id].insuranceSourceSnapshotFingerprint -and
        (Get-StepStatus 'POST' $first (Source-Path $first.Id) $first.Secret $changed $keys[$first.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first (Source-Path $first.Id) $first.Secret $firstBody (New-Key 'sp45-second'))-eq 409)

    $requestIdsSql=@($sourceApplicants|ForEach-Object{"'$($_.SourceReady.requestId)'::uuid"})-join','
    $payloadFingerprintAfter=Invoke-Scalar "select string_agg(md5(protected_payload),',' order by applicant_id) from telehealth_applicant_member_insurance_details where applicant_id in ($applicantIdsSql);"
    Add-SourceCheck 'Evidence has one protected-source reference and event per request while payload, patient, insurance, and downstream state remain unchanged' (
        $payloadFingerprintBefore-eq$payloadFingerprintAfter -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_insurance_source_confirmations where request_id in ($requestIdsSql);")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_insurance_source_confirmations where request_id in ($requestIdsSql) and protected_payload_referenced and not protected_payload_copied and not protected_payload_decrypted and not prior_result_reused and fresh_verification_requested and not coverage_verified and not exact_network_confirmed and not external_call_performed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=6 and action='applicant-insurance-source-confirmed';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='Verification' and version=6 and triage_outcome='TelehealthEligible' and ready_at is null and appointment_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_prospective_applicants where applicant_id in ($applicantIdsSql) and status='SyntheticRequestCreated' and version=26;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from information_schema.columns where table_schema='public' and table_name='telehealth_applicant_request_insurance_source_confirmations' and column_name in ('protected_payload','member_id','group_number');")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from insurance_records where lower(patient_id) in (select lower(canonical_patient_id) from telehealth_applicant_request_insurance_source_confirmations where request_id in ($requestIdsSql));")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_selections where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_verifications where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_video_sessions where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_consultation_contexts where request_id in ($requestIdsSql);")-eq 0)

    $firstConfirmation=Invoke-Scalar "select confirmation_id from telehealth_applicant_request_insurance_source_confirmations where applicant_id='$($first.Id)'::uuid;"
    Add-SourceCheck 'The confirmation is append-only and false protection/current/downstream gates reject mutation' (
        (Test-SourceMutationRejected "update telehealth_applicant_request_insurance_source_confirmations set protected_payload_copied=true where confirmation_id='$firstConfirmation'::uuid; select 'bad';") -and
        (Test-SourceMutationRejected "update telehealth_applicant_request_insurance_source_confirmations set prior_result_reused=true where confirmation_id='$firstConfirmation'::uuid; select 'bad';") -and
        (Test-SourceMutationRejected "update telehealth_applicant_request_insurance_source_confirmations set coverage_verified=true where confirmation_id='$firstConfirmation'::uuid; select 'bad';") -and
        (Test-SourceMutationRejected "delete from telehealth_applicant_request_insurance_source_confirmations where confirmation_id='$firstConfirmation'::uuid; select 'bad';") -and
        (Test-SourceMutationRejected "update telehealth_applicant_member_insurance_details set protected_payload='changed' where applicant_id='$($first.Id)'::uuid; select 'bad';"))
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

if(-not$passed){throw "Telehealth applicant request insurance-source proof failed. See $resultPath"}
Write-Host "Telehealth applicant request insurance-source proof passed $($checks.Count) checks."
