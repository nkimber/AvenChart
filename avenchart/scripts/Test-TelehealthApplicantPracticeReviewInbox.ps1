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
    throw 'Telehealth applicant practice-review inbox proof is local-only.'
}

. (Join-Path $PSScriptRoot 'Test-TelehealthApplicantPracticeReviewSubmission.ps1') `
    -ApiBaseUrl $ApiBaseUrl -DatabaseName $DatabaseName

$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifactsRoot = Join-Path $solutionRoot 'artifacts/telehealth'
$resultPath = Join-Path $artifactsRoot 'latest-telehealth-applicant-practice-review-inbox.json'
New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
$passed = $true
$path = '/api/telehealth/v1/admin/applicant-practice-review'
$driftPatientId = $null
$originalDriftPostalCode = $null

function Add-InboxCheck([string]$Name,[bool]$Result,[object]$Details=$null) {
    $script:checks.Add([ordered]@{name=$Name;status=$(if($Result){'passed'}else{'failed'});details=$Details})
    if (-not $Result) { $script:passed = $false }
}
function Get-Status([string]$Method,[hashtable]$Headers=@{}) {
    try { [int](Invoke-WebRequest "$ApiBaseUrl$path" -Method $Method -Headers $Headers -TimeoutSec 30).StatusCode }
    catch {
        if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode }
        else { throw }
    }
}
function Get-StateFingerprint {
    Invoke-Scalar @"
select md5(concat_ws('|',
  coalesce((select jsonb_agg(to_jsonb(c) order by c.case_id)::text
    from telehealth_prospective_practice_review_cases c),'[]'),
  coalesce((select jsonb_agg(to_jsonb(s) order by s.submission_id)::text
    from telehealth_applicant_practice_review_submissions s),'[]'),
  coalesce((select jsonb_agg(to_jsonb(p) order by p.canonical_id)::text
    from patients p
    join telehealth_applicant_synthetic_promotions promotion
      on promotion.canonical_patient_id=p.canonical_id
    where promotion.applicant_id in (
      '$($success.Created.applicantId)','$($race.Created.applicantId)','$($fl.Created.applicantId)')),'[]'),
  (select count(*)::text from telehealth_requests),
  (select count(*)::text from telehealth_queue_entries),
  (select count(*)::text from appointments),
  (select count(*)::text from encounters),
  (select count(*)::text from prescriptions),
  (select count(*)::text from claims)));
"@
}

try {
    $admin = Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='admin';password='pass'} | ConvertTo-Json) -TimeoutSec 30
    $adminHeaders = @{
        'X-AvenChart-Session'=$admin.sessionId
        'X-AvenChart-Facility-Id'='10'
        'X-AvenChart-Purpose-Of-Use'='healthcare-operations'
    }
    $provider = Invoke-RestMethod "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{username='gold-provider-01';password='pass'} | ConvertTo-Json) -TimeoutSec 30
    $providerHeaders = @{
        'X-AvenChart-Session'=$provider.sessionId
        'X-AvenChart-Facility-Id'='10'
        'X-AvenChart-Purpose-Of-Use'='treatment'
    }
    $missingPurpose = @{
        'X-AvenChart-Session'=$admin.sessionId
        'X-AvenChart-Facility-Id'='10'
    }
    $crossFacility = @{
        'X-AvenChart-Session'=$admin.sessionId
        'X-AvenChart-Facility-Id'='11'
        'X-AvenChart-Purpose-Of-Use'='healthcare-operations'
    }

    $auditBefore = [int](Invoke-Scalar @"
select count(*) from phi_access_audit_events
where resource_type='TelehealthApplicantPracticeReviewInbox'
  and resource_id='queue' and facility_id=10
  and purpose_of_use='healthcare-operations'
  and required_permission like 'acl.patients.demo.view@%' and authorized;
"@)
    $stateBefore = Get-StateFingerprint
    $response = Invoke-WebRequest "$ApiBaseUrl$path" -Headers $adminHeaders -TimeoutSec 30
    $inbox = $response.Content | ConvertFrom-Json
    $cohortCaseIds = @(
        [string]$gaResult.practiceReviewCaseId,
        [string]$flResult.practiceReviewCaseId,
        [string]$raceReplay.practiceReviewCaseId)
    $items = @($inbox.items | Where-Object { [string]$_.practiceReviewCaseId -in $cohortCaseIds })

    $rootNames = @($inbox.PSObject.Properties.Name)
    $expectedRootNames = @('policyKey','policyVersion','practiceDisplayName','serverTime','items','limitations')
    Add-InboxCheck 'Inbox root is minimized to the approved stable contract' (
        @(Compare-Object ($expectedRootNames|Sort-Object) ($rootNames|Sort-Object)).Count -eq 0 -and
        $inbox.policyKey -eq 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX' -and
        $inbox.policyVersion -eq 1 -and @($inbox.limitations).Count -eq 3) $rootNames

    $expectedItemNames = @(
        'practiceReviewCaseId','applicantVersion','applicantStatus','reviewStatus','legalFirstName',
        'legalLastName','dateOfBirth','maskedEmail','maskedPhone','residenceStateCode','postalCode',
        'purposeCategory','purposeDisplayLabel','safetyOutcome','reviewRoute','sections','submittedAt',
        'staffReviewWorkItemExists','staffActionTaken','assigned','priorityAssigned','practiceAccepted',
        'assignedToCurrentUser','assignmentExpiresAt',
        'practiceDeclined','patientContacted','clinicianReviewCreated','telehealthRequestCreated',
        'patientCareQueueEntered','clinicianQueueEntered','appointmentCreated','encounterCreated',
        'careAuthorized','prescribingEnabled','billingEnabled','claimCreated','integrationEnabled',
        'externalCallPerformed')
    $itemNames = @($items[0].PSObject.Properties.Name)
    $serialized = $inbox | ConvertTo-Json -Depth 20 -Compress
    Add-InboxCheck 'Items expose only the approved identity, routing, receipt, and consequence fields' (
        @(Compare-Object ($expectedItemNames|Sort-Object) ($itemNames|Sort-Object)).Count -eq 0 -and
        $serialized -notmatch 'applicantId|patientId|canonicalPatientId|memberId|groupNumber|payer|diagnosis|dose|directions|reaction|criticality|freeText|narrative|accessKey|fingerprint') $itemNames

    $states = @($items | ForEach-Object { $_.residenceStateCode } | Sort-Object)
    Add-InboxCheck 'Georgia, California, and Florida review receipts are present once each' (
        $items.Count -eq 3 -and
        ($states -join ',') -eq 'CA,FL,GA' -and
        @($items.practiceReviewCaseId | Sort-Object -Unique).Count -eq 3) $states

    $allowedReviewRoutes = @(
        'AdditionalClinicalInformationRequired','AssistedPreRequestSupportRequired',
        'PendingPracticePreRequestReview')
    $expectedSections = @('ClinicalInformation','CommunicationAccess','DevicePreparation','Insurance','Registration')
    $invalidItems = @($items | Where-Object {
        $_.applicantStatus -ne 'SyntheticPracticeReviewSubmitted' -or
        $_.reviewStatus -ne 'PendingPracticeReview' -or
        $_.purposeCategory -notin @('migraine','sleep') -or
        $_.safetyOutcome -ne 'TelehealthEligible' -or
        $_.reviewRoute -notin $allowedReviewRoutes -or
        @($_.sections).Count -ne 5 -or
        ((@($_.sections.sectionKey | Sort-Object) -join ',') -ne ($expectedSections -join ',')) -or
        @($_.sections | Where-Object { [string]::IsNullOrWhiteSpace($_.receiptState) -or [string]::IsNullOrWhiteSpace($_.outstandingRoute) }).Count -ne 0
    })
    Add-InboxCheck 'Every item remains an eligible controlled-purpose receipt with five coarse sections' ($invalidItems.Count -eq 0) $invalidItems

    $falseFlags = @(
        'staffActionTaken','assigned','assignedToCurrentUser','priorityAssigned','practiceAccepted','practiceDeclined',
        'patientContacted','clinicianReviewCreated','telehealthRequestCreated','patientCareQueueEntered',
        'clinicianQueueEntered','appointmentCreated','encounterCreated','careAuthorized',
        'prescribingEnabled','billingEnabled','claimCreated','integrationEnabled','externalCallPerformed')
    # PowerShell property lookup is kept explicit so a missing or truthy capability fails closed.
    $invalidConsequences = @($items | Where-Object {
        $item = $_
        (-not $item.staffReviewWorkItemExists) -or $item.assignmentExpiresAt -ne $null -or
        @($falseFlags | Where-Object {
            $property = $item.PSObject.Properties[$_]
            $null -eq $property -or [bool]$property.Value
        }).Count -ne 0
    })
    Add-InboxCheck 'Inbox offers no action, assignment, priority, care, financial, or external consequence' ($invalidConsequences.Count -eq 0) $invalidConsequences

    $unmasked = @($items | Where-Object {
        $_.maskedEmail -notmatch '[\*•]' -or $_.maskedPhone -notmatch '[\*•]' -or
        $_.maskedEmail -match '^sp[0-9a-z-]+@' -or $_.maskedPhone -match '^\d{3}-\d{3}'
    })
    Add-InboxCheck 'Contact details are masked while review identity and region stay usable' ($unmasked.Count -eq 0) $unmasked

    Add-InboxCheck 'Authorized response is private and non-cacheable' (
        [string]$response.Headers['Cache-Control'] -match 'no-store' -and
        [string]$response.Headers['Cache-Control'] -match 'private' -and
        [string]$response.Headers['Pragma'] -match 'no-cache') @{
            cacheControl=[string]$response.Headers['Cache-Control'];pragma=[string]$response.Headers['Pragma']}

    Add-InboxCheck 'Only an authorized practice operator can read the inbox' (
        (Get-Status 'GET') -eq 401 -and
        (Get-Status 'GET' $providerHeaders) -eq 403 -and
        (Get-Status 'GET' $missingPurpose) -eq 403 -and
        (Get-Status 'GET' $crossFacility) -in @(403,404))
    Add-InboxCheck 'The inbox route has no write counterpart' ((Get-Status 'POST' $adminHeaders) -eq 405)

    $driftRecord = (Invoke-Scalar @"
select c.canonical_patient_id||'|'||p.postal_code
from telehealth_prospective_practice_review_cases c
join patients p on p.canonical_id=c.canonical_patient_id
where c.case_id='$($gaResult.practiceReviewCaseId)'::uuid;
"@).Split('|',2)
    $driftPatientId = $driftRecord[0]
    $originalDriftPostalCode = $driftRecord[1]
    $null = Invoke-Scalar "update patients set postal_code='99999' where canonical_id='$driftPatientId'; select 'updated';"
    $driftedInbox = Invoke-RestMethod "$ApiBaseUrl$path" -Headers $adminHeaders -TimeoutSec 30
    $driftedIds = @($driftedInbox.items.practiceReviewCaseId)
    Add-InboxCheck 'A drifted promoted patient shell is excluded without disclosing the case' (
        [string]$gaResult.practiceReviewCaseId -notin $driftedIds -and
        [string]$flResult.practiceReviewCaseId -in $driftedIds -and
        [string]$raceReplay.practiceReviewCaseId -in $driftedIds) $driftedIds
    $null = Invoke-Scalar "update patients set postal_code='$originalDriftPostalCode' where canonical_id='$driftPatientId'; select 'restored';"
    $driftPatientId = $null
    $originalDriftPostalCode = $null

    $second = Invoke-RestMethod "$ApiBaseUrl$path" -Headers $adminHeaders -TimeoutSec 30
    $firstIds = @($inbox.items.practiceReviewCaseId)
    $secondIds = @($second.items.practiceReviewCaseId)
    Add-InboxCheck 'Repeated reads preserve deterministic item identity and ordering' (
        ($firstIds -join ',') -eq ($secondIds -join ',')) @{first=$firstIds;second=$secondIds}

    $stateAfter = Get-StateFingerprint
    Add-InboxCheck 'Inbox reads do not mutate review, patient, queue, care, prescription, or claim state' ($stateBefore -eq $stateAfter) @{
        before=$stateBefore;after=$stateAfter}

    $auditAfter = [int](Invoke-Scalar @"
select count(*) from phi_access_audit_events
where resource_type='TelehealthApplicantPracticeReviewInbox'
  and resource_id='queue' and facility_id=10
  and purpose_of_use='healthcare-operations'
  and required_permission like 'acl.patients.demo.view@%' and authorized;
"@)
    Add-InboxCheck 'Authorized queue reads are purpose- and facility-correlated in PHI audit' (
        $auditAfter -ge ($auditBefore + 2)) @{before=$auditBefore;after=$auditAfter}
}
catch {
    $passed = $false
    Add-InboxCheck 'Applicant practice-review inbox proof execution' $false (
        "$($_.Exception.Message) | $($_.ScriptStackTrace)")
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($driftPatientId) -and
        -not [string]::IsNullOrWhiteSpace($originalDriftPostalCode)) {
        try {
            $null = Invoke-Scalar "update patients set postal_code='$originalDriftPostalCode' where canonical_id='$driftPatientId'; select 'restored';"
        }
        catch {
            $passed = $false
            Add-InboxCheck 'Drifted promoted patient-shell fixture is restored' $false $_.Exception.Message
        }
    }
}

$artifact = [ordered]@{
    status=$(if($passed){'passed'}else{'failed'})
    generatedAtUtc=(Get-Date).ToUniversalTime().ToString('o')
    decision='TH-DEC-0039'
    checks=$checks
}
$artifact | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resultPath
if (-not $passed) { throw "Applicant practice-review inbox proof failed. See $resultPath" }
Write-Host "Applicant practice-review inbox proof passed ($($checks.Count) checks). Artifact: $resultPath"
