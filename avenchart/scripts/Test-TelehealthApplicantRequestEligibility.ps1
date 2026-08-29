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
    throw 'Telehealth applicant request eligibility proof is local-only.'
}

# Re-prove Sprint 45 and retain its GA/CA/FL requests at Verification v6.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestInsuranceSource.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$eligibilityApplicants = @($sourceApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-eligibility.json'
$eligibilityChecks = [System.Collections.Generic.List[object]]::new()
$eligibilityPassed = $true

function Add-EligibilityCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:eligibilityChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:eligibilityPassed=$false}
}
function Eligibility-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/eligibility"
}
function New-EligibilityBody([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=6
        eligibilitySnapshotFingerprint=[string]$Ready.eligibilitySnapshotFingerprint
        syntheticDataConfirmed=$true
        noGuaranteeAcknowledged=$true
    }
}
function Copy-EligibilityBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-EligibilityMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedEligibilityPosts([object]$Applicant,[object]$Body) {
    $url="$ApiBaseUrl$(Eligibility-Path $Applicant.Id)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $headers=Applicant-Headers $Applicant.Secret (New-Key "sp46-contention-$ordinal")
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
    foreach($applicant in $eligibilityApplicants) {
        $applicant | Add-Member -NotePropertyName EligibilityReady -NotePropertyValue (
            Get-ApplicantStep $applicant (Eligibility-Path $applicant.Id))
    }

    Add-EligibilityCheck 'GA, CA, and FL projections are private, masked, request-bound, and network-pending' (
        @($eligibilityApplicants|Where-Object{
            $_.EligibilityReady.requestStatus-eq'Verification' -and
            $_.EligibilityReady.requestVersion-eq 6 -and
            $_.EligibilityReady.verificationReady -and -not$_.EligibilityReady.verificationCompleted -and
            $_.EligibilityReady.maskedMemberId-match'^••••[A-Z0-9-]{4}$' -and
            ($null-eq$_.EligibilityReady.maskedGroupNumber -or
                $_.EligibilityReady.maskedGroupNumber-match'^••••[A-Z0-9-]{4}$') -and
            $_.EligibilityReady.currentLocationStateCode -in @('GA','CA','FL') -and
            $_.EligibilityReady.purposeCategory -in @('migraine','sleep') -and
            $_.EligibilityReady.protectedPayloadReferenced -and
            -not$_.EligibilityReady.protectedPayloadCopied -and
            -not$_.EligibilityReady.protectedPayloadDecryptedInServerMemory -and
            -not$_.EligibilityReady.priorEligibilityResultReused -and
            -not$_.EligibilityReady.currentEligibilityEvidenceCreated -and
            -not$_.EligibilityReady.networkVerificationCreated -and
            -not$_.EligibilityReady.coverageVerified -and
            -not$_.EligibilityReady.exactNetworkConfirmed -and
            $null-eq$_.EligibilityReady.PSObject.Properties['memberId'] -and
            $null-eq$_.EligibilityReady.PSObject.Properties['groupNumber'] -and
            $null-eq$_.EligibilityReady.PSObject.Properties['protectedPayload'] -and
            $null-eq$_.EligibilityReady.PSObject.Properties['canonicalPatientId'] -and
            $null-eq$_.EligibilityReady.PSObject.Properties['inquiryTraceToken']
        }).Count-eq 3)

    $spare=$eligibilityApplicants[2]
    $validSpare=New-EligibilityBody $spare.EligibilityReady
    $missing=Copy-EligibilityBody $validSpare;$missing.PSObject.Properties.Remove('noGuaranteeAcknowledged')
    $falseAcknowledgment=Copy-EligibilityBody $validSpare;$falseAcknowledgment.syntheticDataConfirmed=$false
    $stale=Copy-EligibilityBody $validSpare;$stale.expectedRequestVersion=7
    $badSnapshot=Copy-EligibilityBody $validSpare;$badSnapshot.eligibilitySnapshotFingerprint=('0'*64)
    Add-EligibilityCheck 'Missing/foreign access, missing or false acknowledgment, stale version, and changed snapshot fail before evidence' (
        (Get-StepStatus 'GET' $spare (Eligibility-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (Eligibility-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (Eligibility-Path $spare.Id) $spare.Secret $missing (New-Key 'sp46-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Eligibility-Path $spare.Id) $spare.Secret $falseAcknowledgment (New-Key 'sp46-false'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (Eligibility-Path $spare.Id) $spare.Secret $stale (New-Key 'sp46-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (Eligibility-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp46-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_eligibility_verifications where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $applicantIdsSql=@($eligibilityApplicants|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    $payloadFingerprintBefore=Invoke-Scalar "select string_agg(md5(protected_payload),',' order by applicant_id) from telehealth_applicant_member_insurance_details where applicant_id in ($applicantIdsSql);"
    $results=@{}
    $keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$eligibilityApplicants[$index]
        $body=New-EligibilityBody $applicant.EligibilityReady
        $key=New-Key 'sp46-eligibility'
        $keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-ApplicantStep $applicant (Eligibility-Path $applicant.Id) $body $key
    }
    $contended=$eligibilityApplicants[1]
    $contentionStatuses=Invoke-ContendedEligibilityPosts $contended (New-EligibilityBody $contended.EligibilityReady)
    $results[$contended.Id]=Get-ApplicantStep $contended (Eligibility-Path $contended.Id)
    $businessOutcomes=@($eligibilityApplicants|ForEach-Object{$results[$_.Id].businessOutcome})
    Add-EligibilityCheck 'First-writer contention creates one fresh result and every request advances only to Verification version 7' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($businessOutcomes|Where-Object{$_-ne'EligibleBenefitsReported'}).Count-eq 0 -and
        @($eligibilityApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'Verification' -and $result.requestVersion-eq 7 -and
            $result.verificationCompleted -and -not$result.verificationReady -and
            $result.adapterMode-eq'NON_PRODUCTION' -and
            $result.compatibilityTarget-eq'ASC_X12N_270_271_005010X279A1' -and
            $result.currentEligibilityEvidenceCreated -and
            $result.protectedPayloadDecryptedInServerMemory -and
            -not$result.protectedPayloadCopied -and -not$result.priorEligibilityResultReused -and
            -not$result.rawTransactionCreated -and -not$result.canonicalCoverageCreated -and
            -not$result.coverageSelected -and -not$result.networkVerificationCreated -and
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

    $first=$eligibilityApplicants[0]
    $firstBody=New-EligibilityBody $first.EligibilityReady
    $replay=Post-ApplicantStep $first (Eligibility-Path $first.Id) $firstBody $keys[$first.Id]
    $changed=Copy-EligibilityBody $firstBody;$changed.expectedRequestVersion=7
    Add-EligibilityCheck 'Exact semantic replay is stable while changed-key reuse and a second command fail closed' (
        $replay.verificationId-eq$results[$first.Id].verificationId -and
        $replay.checkedAt-eq$results[$first.Id].checkedAt -and
        (Get-StepStatus 'POST' $first (Eligibility-Path $first.Id) $first.Secret $changed $keys[$first.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first (Eligibility-Path $first.Id) $first.Secret $firstBody (New-Key 'sp46-second'))-eq 409)

    $requestIdsSql=@($eligibilityApplicants|ForEach-Object{"'$($_.EligibilityReady.requestId)'::uuid"})-join','
    $payloadFingerprintAfter=Invoke-Scalar "select string_agg(md5(protected_payload),',' order by applicant_id) from telehealth_applicant_member_insurance_details where applicant_id in ($applicantIdsSql);"
    Add-EligibilityCheck 'Fresh evidence and one event are append-only while protected source, patient, insurance, and downstream state remain unchanged' (
        $payloadFingerprintBefore-eq$payloadFingerprintAfter -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_eligibility_verifications where request_id in ($requestIdsSql);")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_eligibility_verifications where request_id in ($requestIdsSql) and protected_payload_referenced and protected_payload_decrypted_in_server_memory and not protected_payload_copied and not prior_eligibility_result_reused and current_eligibility_evidence_created and not raw_transaction_created and not network_verification_created and not coverage_verified and not exact_network_confirmed and not external_call_performed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=7 and action='applicant-eligibility-verification-recorded';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='Verification' and version=7 and triage_outcome='TelehealthEligible' and ready_at is null and appointment_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from information_schema.columns where table_schema='public' and table_name='telehealth_applicant_request_eligibility_verifications' and column_name in ('protected_payload','member_id','group_number','subscriber_first_name','subscriber_last_name');")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from insurance_records where lower(patient_id) in (select lower(canonical_patient_id) from telehealth_applicant_request_eligibility_verifications where request_id in ($requestIdsSql));")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_selections where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_verifications where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations where request_id in ($requestIdsSql);")-eq 0)

    $firstVerification=Invoke-Scalar "select verification_id from telehealth_applicant_request_eligibility_verifications where applicant_id='$($first.Id)'::uuid;"
    Add-EligibilityCheck 'Current eligibility evidence rejects mutation of protection, network, coverage, downstream, and deletion state' (
        (Test-EligibilityMutationRejected "update telehealth_applicant_request_eligibility_verifications set protected_payload_copied=true where verification_id='$firstVerification'::uuid; select 'bad';") -and
        (Test-EligibilityMutationRejected "update telehealth_applicant_request_eligibility_verifications set prior_eligibility_result_reused=true where verification_id='$firstVerification'::uuid; select 'bad';") -and
        (Test-EligibilityMutationRejected "update telehealth_applicant_request_eligibility_verifications set network_verification_created=true where verification_id='$firstVerification'::uuid; select 'bad';") -and
        (Test-EligibilityMutationRejected "update telehealth_applicant_request_eligibility_verifications set coverage_verified=true where verification_id='$firstVerification'::uuid; select 'bad';") -and
        (Test-EligibilityMutationRejected "delete from telehealth_applicant_request_eligibility_verifications where verification_id='$firstVerification'::uuid; select 'bad';"))
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{
        generatedAt=(Get-Date).ToUniversalTime().ToString('o')
        apiBaseUrl=$ApiBaseUrl
        database=$DatabaseName
        passed=$eligibilityPassed
        checkCount=$eligibilityChecks.Count
        checks=$eligibilityChecks
    }
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$eligibilityPassed){throw "Telehealth applicant request eligibility proof failed. See $resultPath"}
Write-Host "Telehealth applicant request eligibility proof passed $($eligibilityChecks.Count) checks."
