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
    throw 'Telehealth applicant request practice-network proof is local-only.'
}

# Re-prove Sprint 46 and retain its GA/CA/FL requests at Verification v7.
. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantRequestEligibility.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$practiceNetworkApplicants = @($eligibilityApplicants)
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-request-practice-network.json'
$practiceNetworkChecks = [System.Collections.Generic.List[object]]::new()
$practiceNetworkPassed = $true

function Add-PracticeNetworkCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:practiceNetworkChecks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if(-not$Result){$script:practiceNetworkPassed=$false}
}
function PracticeNetwork-Path([string]$ApplicantId) {
    "/api/telehealth/v1/applicants/$ApplicantId/telehealth-request/practice-network"
}
function New-PracticeNetworkBody([object]$Ready) {
    [ordered]@{
        expectedRequestVersion=7
        networkSnapshotFingerprint=[string]$Ready.networkSnapshotFingerprint
        syntheticDataConfirmed=$true
        practiceOnlyScopeAcknowledged=$true
        noGuaranteeAcknowledged=$true
    }
}
function Copy-PracticeNetworkBody([object]$Body) {
    $Body | ConvertTo-Json -Compress -Depth 8 | ConvertFrom-Json
}
function Test-PracticeNetworkMutationRejected([string]$Sql) {
    try{$null=Invoke-Scalar $Sql;$false}catch{$true}
}
function Invoke-ContendedPracticeNetworkPosts([object]$Applicant,[object]$Body) {
    $url="$ApiBaseUrl$(PracticeNetwork-Path $Applicant.Id)"
    $bodyJson=$Body|ConvertTo-Json -Compress -Depth 8
    $jobs=@()
    foreach($ordinal in 1..2) {
        $headers=Applicant-Headers $Applicant.Secret (New-Key "sp47-contention-$ordinal")
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
    foreach($applicant in $practiceNetworkApplicants) {
        $applicant | Add-Member -NotePropertyName PracticeNetworkReady -NotePropertyValue (
            Get-ApplicantStep $applicant (PracticeNetwork-Path $applicant.Id))
    }

    Add-PracticeNetworkCheck 'GA, CA, and FL projections bind current positive eligibility to a practice-only network check' (
        @($practiceNetworkApplicants|Where-Object{
            $_.PracticeNetworkReady.requestStatus-eq'Verification' -and
            $_.PracticeNetworkReady.requestVersion-eq 7 -and
            $_.PracticeNetworkReady.verificationReady -and -not$_.PracticeNetworkReady.verificationCompleted -and
            $_.PracticeNetworkReady.practiceDisplayName-eq'AvenChart Synthetic Practice' -and
            $_.PracticeNetworkReady.currentLocationStateCode -in @('GA','CA','FL') -and
            $_.PracticeNetworkReady.eligibilityBusinessOutcome-eq'EligibleBenefitsReported' -and
            $_.PracticeNetworkReady.currentEligibilityEvidenceReusedAsContext -and
            -not$_.PracticeNetworkReady.practiceNetworkVerificationCreated -and
            -not$_.PracticeNetworkReady.renderingPhysicianSelected -and
            -not$_.PracticeNetworkReady.renderingPhysicianNetworkChecked -and
            -not$_.PracticeNetworkReady.exactNetworkConfirmed -and
            $null-eq$_.PracticeNetworkReady.PSObject.Properties['memberId'] -and
            $null-eq$_.PracticeNetworkReady.PSObject.Properties['maskedMemberId'] -and
            $null-eq$_.PracticeNetworkReady.PSObject.Properties['canonicalPatientId'] -and
            $null-eq$_.PracticeNetworkReady.PSObject.Properties['networkReference']
        }).Count-eq 3)

    $spare=$practiceNetworkApplicants[2]
    $validSpare=New-PracticeNetworkBody $spare.PracticeNetworkReady
    $missing=Copy-PracticeNetworkBody $validSpare;$missing.PSObject.Properties.Remove('practiceOnlyScopeAcknowledged')
    $falseAcknowledgment=Copy-PracticeNetworkBody $validSpare;$falseAcknowledgment.noGuaranteeAcknowledged=$false
    $stale=Copy-PracticeNetworkBody $validSpare;$stale.expectedRequestVersion=8
    $badSnapshot=Copy-PracticeNetworkBody $validSpare;$badSnapshot.networkSnapshotFingerprint=('0'*64)
    Add-PracticeNetworkCheck 'Missing/foreign access, missing or false acknowledgment, stale version, and changed snapshot fail before evidence' (
        (Get-StepStatus 'GET' $spare (PracticeNetwork-Path $spare.Id) '')-eq 401 -and
        (Get-StepStatus 'GET' $spare (PracticeNetwork-Path $spare.Id) (New-Secret))-eq 404 -and
        (Get-StepStatus 'POST' $spare (PracticeNetwork-Path $spare.Id) $spare.Secret $missing (New-Key 'sp47-missing'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (PracticeNetwork-Path $spare.Id) $spare.Secret $falseAcknowledgment (New-Key 'sp47-false'))-eq 400 -and
        (Get-StepStatus 'POST' $spare (PracticeNetwork-Path $spare.Id) $spare.Secret $stale (New-Key 'sp47-stale'))-eq 409 -and
        (Get-StepStatus 'POST' $spare (PracticeNetwork-Path $spare.Id) $spare.Secret $badSnapshot (New-Key 'sp47-snapshot'))-eq 409 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_practice_network_verifications where applicant_id='$($spare.Id)'::uuid;")-eq 0)

    $applicantIdsSql=@($practiceNetworkApplicants|ForEach-Object{"'$($_.Id)'::uuid"})-join','
    $eligibilityFingerprintBefore=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by applicant_id) from telehealth_applicant_request_eligibility_verifications v where applicant_id in ($applicantIdsSql);"
    $results=@{}
    $keys=@{}
    foreach($index in @(0,2)) {
        $applicant=$practiceNetworkApplicants[$index]
        $body=New-PracticeNetworkBody $applicant.PracticeNetworkReady
        $key=New-Key 'sp47-practice-network'
        $keys[$applicant.Id]=$key
        $results[$applicant.Id]=Post-ApplicantStep $applicant (PracticeNetwork-Path $applicant.Id) $body $key
    }
    $contended=$practiceNetworkApplicants[1]
    $contentionStatuses=Invoke-ContendedPracticeNetworkPosts $contended (New-PracticeNetworkBody $contended.PracticeNetworkReady)
    $results[$contended.Id]=Get-ApplicantStep $contended (PracticeNetwork-Path $contended.Id)
    Add-PracticeNetworkCheck 'First-writer contention creates one practice result and every request advances only to Verification version 8' (
        @($contentionStatuses).Count-eq 2 -and $contentionStatuses[0]-eq 200 -and $contentionStatuses[1]-eq 409 -and
        @($practiceNetworkApplicants|Where-Object{
            $result=$results[$_.Id]
            $result.requestStatus-eq'Verification' -and $result.requestVersion-eq 8 -and
            $result.verificationCompleted -and -not$result.verificationReady -and
            $result.adapterMode-eq'NON_PRODUCTION' -and
            $result.compatibilityTarget-eq'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0' -and
            $result.businessOutcome-eq'PracticeInNetworkAcceptingNewPatients' -and
            $result.practiceNetworkChecked -and $result.practiceInNetwork -and
            $result.newPatientsAccepted -and $result.practiceNetworkVerificationCreated -and
            -not$result.renderingPhysicianSelected -and
            -not$result.renderingPhysicianNetworkChecked -and -not$result.exactNetworkConfirmed -and
            -not$result.canonicalCoverageCreated -and -not$result.coverageSelected -and
            -not$result.coverageVerified -and -not$result.financialRouteCreated -and
            -not$result.operationalReviewCreated -and -not$result.practiceAccepted -and
            -not$result.patientContacted -and -not$result.patientCareQueueEntered -and
            -not$result.clinicianQueueEntered -and -not$result.doctorSearchStarted -and
            -not$result.queuePositionAssigned -and -not$result.appointmentCreated -and
            -not$result.encounterCreated -and -not$result.consentCreated -and
            -not$result.careAuthorized -and -not$result.integrationEnabled -and
            -not$result.externalCallPerformed
        }).Count-eq 3)

    $first=$practiceNetworkApplicants[0]
    $firstBody=New-PracticeNetworkBody $first.PracticeNetworkReady
    $replay=Post-ApplicantStep $first (PracticeNetwork-Path $first.Id) $firstBody $keys[$first.Id]
    $changed=Copy-PracticeNetworkBody $firstBody;$changed.expectedRequestVersion=8
    Add-PracticeNetworkCheck 'Exact semantic replay is stable while changed-key reuse and a second command fail closed' (
        $replay.verificationId-eq$results[$first.Id].verificationId -and
        $replay.checkedAt-eq$results[$first.Id].checkedAt -and
        (Get-StepStatus 'POST' $first (PracticeNetwork-Path $first.Id) $first.Secret $changed $keys[$first.Id])-eq 409 -and
        (Get-StepStatus 'POST' $first (PracticeNetwork-Path $first.Id) $first.Secret $firstBody (New-Key 'sp47-second'))-eq 409)

    $requestIdsSql=@($practiceNetworkApplicants|ForEach-Object{"'$($_.PracticeNetworkReady.requestId)'::uuid"})-join','
    $eligibilityFingerprintAfter=Invoke-Scalar "select string_agg(md5(row_to_json(v)::text),',' order by applicant_id) from telehealth_applicant_request_eligibility_verifications v where applicant_id in ($applicantIdsSql);"
    Add-PracticeNetworkCheck 'Practice evidence and events are append-only while eligibility, patient, insurance, and downstream state remain unchanged' (
        $eligibilityFingerprintBefore-eq$eligibilityFingerprintAfter -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_practice_network_verifications where request_id in ($requestIdsSql);")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_applicant_request_practice_network_verifications where request_id in ($requestIdsSql) and current_eligibility_evidence_referenced and not eligibility_payload_copied and practice_network_verification_created and not rendering_physician_selected and not rendering_physician_network_checked and not exact_network_confirmed and not coverage_verified and not external_call_performed;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_request_events where request_id in ($requestIdsSql) and aggregate_version=8 and action='applicant-practice-network-verification-recorded';")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from telehealth_requests where request_id in ($requestIdsSql) and status='Verification' and version=8 and triage_outcome='TelehealthEligible' and ready_at is null and appointment_id is null;")-eq 3 -and
        [int](Invoke-Scalar "select count(*) from information_schema.columns where table_schema='public' and table_name='telehealth_applicant_request_practice_network_verifications' and column_name in ('member_id','group_number','protected_payload','rendering_physician_id','rendering_npi','billing_tin');")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from insurance_records where lower(patient_id) in (select lower(canonical_patient_id) from telehealth_applicant_request_practice_network_verifications where request_id in ($requestIdsSql));")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_selections where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_coverage_verifications where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_queue_entries where request_id in ($requestIdsSql);")-eq 0 -and
        [int](Invoke-Scalar "select count(*) from telehealth_reservations where request_id in ($requestIdsSql);")-eq 0)

    $firstVerification=Invoke-Scalar "select verification_id from telehealth_applicant_request_practice_network_verifications where applicant_id='$($first.Id)'::uuid;"
    Add-PracticeNetworkCheck 'Practice-network evidence rejects rendering-physician, exact-network, financial, downstream, and deletion mutation' (
        (Test-PracticeNetworkMutationRejected "update telehealth_applicant_request_practice_network_verifications set rendering_physician_selected=true where verification_id='$firstVerification'::uuid; select 'bad';") -and
        (Test-PracticeNetworkMutationRejected "update telehealth_applicant_request_practice_network_verifications set rendering_physician_network_checked=true where verification_id='$firstVerification'::uuid; select 'bad';") -and
        (Test-PracticeNetworkMutationRejected "update telehealth_applicant_request_practice_network_verifications set exact_network_confirmed=true where verification_id='$firstVerification'::uuid; select 'bad';") -and
        (Test-PracticeNetworkMutationRejected "update telehealth_applicant_request_practice_network_verifications set financial_acknowledgment_created=true where verification_id='$firstVerification'::uuid; select 'bad';") -and
        (Test-PracticeNetworkMutationRejected "delete from telehealth_applicant_request_practice_network_verifications where verification_id='$firstVerification'::uuid; select 'bad';"))
}
finally {
    New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
    $report=[ordered]@{
        generatedAt=(Get-Date).ToUniversalTime().ToString('o')
        apiBaseUrl=$ApiBaseUrl
        database=$DatabaseName
        passed=$practiceNetworkPassed
        checkCount=$practiceNetworkChecks.Count
        checks=$practiceNetworkChecks
    }
    $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $resultPath -Encoding utf8
}

if(-not$practiceNetworkPassed){throw "Telehealth applicant request practice-network proof failed. See $resultPath"}
Write-Host "Telehealth applicant request practice-network proof passed $($practiceNetworkChecks.Count) checks."
