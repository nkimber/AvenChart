param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts"
$ResultPath = Join-Path $ArtifactsRoot "latest-modernized-smoke-test.json"
New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null

$checks = New-Object System.Collections.Generic.List[object]
$status = "passed"

function Add-Check {
    param(
        [string]$Name,
        [string]$Result,
        [object]$Details = $null
    )

    $script:checks.Add([ordered]@{
        name = $Name
        status = $Result
        details = $Details
    })

    if ($Result -ne "passed") {
        $script:status = "failed"
    }
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -Method Get -TimeoutSec 15
    Add-Check -Name "api health" -Result $(if ($health.status -eq "healthy") { "passed" } else { "failed" }) -Details $health
}
catch {
    Add-Check -Name "api health" -Result "failed" -Details $_.Exception.Message
}

try {
    $search = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients?search=MOD-PAT-0001&limit=5" -Method Get -TimeoutSec 20
    $anchor = $search.patients | Where-Object { $_.canonicalId -eq "MOD-PAT-0001" } | Select-Object -First 1
    Add-Check -Name "anchor patient search" -Result $(if ($null -ne $anchor) { "passed" } else { "failed" }) -Details @{
        totalMatches = $search.totalMatches
        firstPatient = $search.patients | Select-Object -First 1
    }
}
catch {
    Add-Check -Name "anchor patient search" -Result "failed" -Details $_.Exception.Message
}

try {
    $chart = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0001" -Method Get -TimeoutSec 20
    $chartPassed = $chart.canonicalId -eq "MOD-PAT-0001" -and $chart.legacyPid -eq 100001 -and $chart.displayName -like "Stone,*"
    Add-Check -Name "anchor chart summary" -Result $(if ($chartPassed) { "passed" } else { "failed" }) -Details @{
        canonicalId = $chart.canonicalId
        displayName = $chart.displayName
        counts = $chart.counts
    }
}
catch {
    Add-Check -Name "anchor chart summary" -Result "failed" -Details $_.Exception.Message
}

$demographicsOriginal = $null
try {
    $demographicsOriginal = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010" -Method Get -TimeoutSec 20
    $originalDemographicsBody = @{
        firstName = $demographicsOriginal.firstName
        lastName = $demographicsOriginal.lastName
        preferredName = $demographicsOriginal.preferredName
        sex = $demographicsOriginal.sex
        dateOfBirth = $demographicsOriginal.dateOfBirth
        street = $demographicsOriginal.street
        city = $demographicsOriginal.city
        state = $demographicsOriginal.state
        postalCode = $demographicsOriginal.postalCode
        maritalStatus = $demographicsOriginal.maritalStatus
        occupation = $demographicsOriginal.occupation
    }
    $demographicsBody = @{
        firstName = "Morgan"
        lastName = "Parity"
        preferredName = "Slice36"
        sex = if ($demographicsOriginal.sex -eq "Female") { "Male" } else { "Female" }
        dateOfBirth = "1984-03-12"
        street = "36 Parity Way"
        city = "Bridgeport"
        state = "CT"
        postalCode = "06460"
        maritalStatus = "married"
        occupation = "Workflow Analyst"
    }

    $updatedDemographics = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010/demographics" `
        -Method Put `
        -ContentType "application/json" `
        -Body ($demographicsBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $demographicsMutationPassed = $updatedDemographics.displayName -like "Parity, Morgan*" `
        -and $updatedDemographics.firstName -eq $demographicsBody.firstName `
        -and $updatedDemographics.lastName -eq $demographicsBody.lastName `
        -and $updatedDemographics.preferredName -eq $demographicsBody.preferredName `
        -and $updatedDemographics.sex -eq $demographicsBody.sex `
        -and $updatedDemographics.dateOfBirth -eq $demographicsBody.dateOfBirth `
        -and $updatedDemographics.street -eq $demographicsBody.street `
        -and $updatedDemographics.city -eq $demographicsBody.city `
        -and $updatedDemographics.state -eq $demographicsBody.state `
        -and $updatedDemographics.postalCode -eq $demographicsBody.postalCode `
        -and $updatedDemographics.maritalStatus -eq $demographicsBody.maritalStatus `
        -and $updatedDemographics.occupation -eq $demographicsBody.occupation

    $restoredDemographics = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010/demographics" `
        -Method Put `
        -ContentType "application/json" `
        -Body ($originalDemographicsBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20
    $demographicsOriginal = $null

    $demographicsRestorePassed = $restoredDemographics.firstName -eq $originalDemographicsBody.firstName `
        -and $restoredDemographics.lastName -eq $originalDemographicsBody.lastName `
        -and $restoredDemographics.dateOfBirth -eq $originalDemographicsBody.dateOfBirth

    Add-Check -Name "patient demographics mutation lifecycle" -Result $(if ($demographicsMutationPassed -and $demographicsRestorePassed) { "passed" } else { "failed" }) -Details @{
        updatedDisplayName = $updatedDemographics.displayName
        updatedAddress = "$($updatedDemographics.street), $($updatedDemographics.city) $($updatedDemographics.state) $($updatedDemographics.postalCode)"
        restoredDisplayName = $restoredDemographics.displayName
    }
}
catch {
    Add-Check -Name "patient demographics mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $demographicsOriginal) {
        try {
            $originalDemographicsBody = @{
                firstName = $demographicsOriginal.firstName
                lastName = $demographicsOriginal.lastName
                preferredName = $demographicsOriginal.preferredName
                sex = $demographicsOriginal.sex
                dateOfBirth = $demographicsOriginal.dateOfBirth
                street = $demographicsOriginal.street
                city = $demographicsOriginal.city
                state = $demographicsOriginal.state
                postalCode = $demographicsOriginal.postalCode
                maritalStatus = $demographicsOriginal.maritalStatus
                occupation = $demographicsOriginal.occupation
            }
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010/demographics" `
                -Method Put `
                -ContentType "application/json" `
                -Body ($originalDemographicsBody | ConvertTo-Json -Depth 5) `
                -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$registrationPubpid = $null
try {
    $suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $registrationPubpid = "TMP-PAT-REG-SMK$suffix"
    $registrationBody = @{
        pubpid = $registrationPubpid
        firstName = "Taylor"
        lastName = "Register"
        preferredName = "Slice37"
        sex = "Female"
        dateOfBirth = "1991-04-15"
        street = "37 Registration Way"
        city = "Hartford"
        state = "CT"
        postalCode = "06103"
        maritalStatus = "single"
        occupation = "Smoke Registration Analyst"
        phoneHome = "(860) 555-3710"
        phoneCell = "(860) 555-3711"
        email = "tmp-register-$suffix@example.test"
        hipaaAllowSms = "YES"
        hipaaAllowEmail = "YES"
    }

    $createdPatient = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients" `
        -Method Post `
        -ContentType "application/json" `
        -Body ($registrationBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $loadedPatient = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$registrationPubpid" -Method Get -TimeoutSec 20
    $searchCreatedPatient = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients?search=$registrationPubpid&limit=5" -Method Get -TimeoutSec 20

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$registrationPubpid" -Method Delete -TimeoutSec 20 | Out-Null
    $registrationPubpid = $null

    $deletedLoadFailed = $false
    try {
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$($registrationBody.pubpid)" -Method Get -TimeoutSec 20 | Out-Null
    }
    catch {
        $deletedLoadFailed = $true
    }

    $registrationPassed = $createdPatient.pubpid -eq $registrationBody.pubpid `
        -and $createdPatient.displayName -like "Register, Taylor*" `
        -and $createdPatient.street -eq $registrationBody.street `
        -and $createdPatient.email -eq $registrationBody.email `
        -and $loadedPatient.pubpid -eq $registrationBody.pubpid `
        -and (@($searchCreatedPatient.patients) | Where-Object { $_.pubpid -eq $registrationBody.pubpid } | Select-Object -First 1) `
        -and $deletedLoadFailed

    Add-Check -Name "patient registration lifecycle" -Result $(if ($registrationPassed) { "passed" } else { "failed" }) -Details @{
        pubpid = $registrationBody.pubpid
        createdDisplayName = $createdPatient.displayName
        loadedPid = $loadedPatient.legacyPid
        deletedLoadFailed = $deletedLoadFailed
    }
}
catch {
    if ($registrationPubpid) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$registrationPubpid" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    Add-Check -Name "patient registration lifecycle" -Result "failed" -Details $_.Exception.Message
}

try {
    $coverageChart = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $coverage = @($coverageChart.insurance)
    $primary = $coverage | Where-Object { $_.type -eq "primary" } | Select-Object -First 1
    $secondary = $coverage | Where-Object { $_.type -eq "secondary" } | Select-Object -First 1
    $coveragePassed = $coverageChart.canonicalId -eq "MOD-PAT-0005" `
        -and $coverage.Count -eq 2 `
        -and $primary.provider -eq "Northstar HMO" `
        -and $primary.planName -eq "Medicare Advantage" `
        -and $primary.policyNumber -eq "POL100005" `
        -and $primary.groupNumber -eq "GRP104" `
        -and $secondary.provider -eq "Acme Health" `
        -and $secondary.policyNumber -eq "SEC100005" `
        -and $secondary.groupNumber -eq "GRP204"
    Add-Check -Name "anchor insurance coverage" -Result $(if ($coveragePassed) { "passed" } else { "failed" }) -Details @{
        canonicalId = $coverageChart.canonicalId
        displayName = $coverageChart.displayName
        coverage = $coverage
    }
}
catch {
    Add-Check -Name "anchor insurance coverage" -Result "failed" -Details $_.Exception.Message
}

$insuranceMutationId = $null
try {
    $suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $insuranceCreateBody = @{
        type = "tertiary"
        provider = "Acme Health"
        planName = "Smoke Bridge $suffix"
        policyNumber = "SMK$suffix"
        groupNumber = "SGRP$suffix"
        relationship = "self"
    }
    $createdCoverageChart = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0005/insurance" `
        -Method Post `
        -ContentType "application/json" `
        -Body ($insuranceCreateBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20
    $createdCoverage = @($createdCoverageChart.insurance) | Where-Object { $_.policyNumber -eq $insuranceCreateBody.policyNumber } | Select-Object -First 1
    if ($null -eq $createdCoverage) {
        throw "Created coverage row was not returned in the chart summary."
    }

    $insuranceMutationId = $createdCoverage.id
    $insuranceUpdateBody = @{
        type = "tertiary"
        provider = "Northstar HMO"
        planName = "Smoke Updated $suffix"
        policyNumber = "USMK$suffix"
        groupNumber = "USGRP$suffix"
        relationship = "self"
    }
    $updatedCoverageChart = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients/insurance/$insuranceMutationId" `
        -Method Put `
        -ContentType "application/json" `
        -Body ($insuranceUpdateBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20
    $updatedCoverage = @($updatedCoverageChart.insurance) | Where-Object { $_.id -eq $insuranceMutationId } | Select-Object -First 1

    $deletedCoverageChart = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients/insurance/$insuranceMutationId" `
        -Method Delete `
        -TimeoutSec 20
    $insuranceMutationId = $null
    $deletedCoverage = @($deletedCoverageChart.insurance) | Where-Object { $_.policyNumber -eq $insuranceUpdateBody.policyNumber } | Select-Object -First 1

    $insuranceMutationPassed = $createdCoverage.provider -eq "Acme Health" `
        -and $createdCoverage.planName -eq $insuranceCreateBody.planName `
        -and $updatedCoverage.provider -eq "Northstar HMO" `
        -and $updatedCoverage.planName -eq $insuranceUpdateBody.planName `
        -and $null -eq $deletedCoverage
    Add-Check -Name "patient insurance mutation lifecycle" -Result $(if ($insuranceMutationPassed) { "passed" } else { "failed" }) -Details @{
        created = $createdCoverage
        updated = $updatedCoverage
        deletedPolicyStillVisible = $null -ne $deletedCoverage
    }
}
catch {
    if ($insuranceMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/insurance/$insuranceMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    Add-Check -Name "patient insurance mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}

try {
    $appointments = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-06-18&limit=5" -Method Get -TimeoutSec 20
    $anchorAppointment = $appointments.appointments | Select-Object -First 1
    $appointmentPassed = $null -ne $anchorAppointment -and ([datetime]$anchorAppointment.date) -gt ([datetime]"2026-06-18")
    Add-Check -Name "anchor appointment search" -Result $(if ($appointmentPassed) { "passed" } else { "failed" }) -Details @{
        totalMatches = $appointments.totalMatches
        firstAppointment = $anchorAppointment
    }
}
catch {
    Add-Check -Name "anchor appointment search" -Result "failed" -Details $_.Exception.Message
}

try {
    if ($null -eq $anchorAppointment) {
        throw "Anchor appointment search did not return an appointment."
    }

    $appointmentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$($anchorAppointment.id)" -Method Get -TimeoutSec 20
    $appointmentDetailPassed = $appointmentDetail.patientId -eq "MOD-PAT-0003" -and ([datetime]$appointmentDetail.date) -gt ([datetime]"2026-06-18")
    Add-Check -Name "anchor appointment detail" -Result $(if ($appointmentDetailPassed) { "passed" } else { "failed" }) -Details @{
        id = $appointmentDetail.id
        title = $appointmentDetail.title
        date = $appointmentDetail.date
        status = $appointmentDetail.status
    }
}
catch {
    Add-Check -Name "anchor appointment detail" -Result "failed" -Details $_.Exception.Message
}

$appointmentMutationId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = $null
        title = "Smoke Appointment Mutation"
        date = "2026-10-15"
        startTime = "10:30"
        durationMinutes = 30
        facilityId = $null
        categoryId = 9
        room = "Smoke"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentMutationId = $createdAppointment.id

    $cancelBody = @{
        status = "x"
        title = "Smoke Appointment Mutation Cancelled"
    } | ConvertTo-Json
    $cancelledAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentMutationId/status" -Method Put -ContentType "application/json" -Body $cancelBody -TimeoutSec 20
    $appointmentMutationPassed = $createdAppointment.status -eq "-" -and $cancelledAppointment.status -eq "x" -and $cancelledAppointment.title -eq "Smoke Appointment Mutation Cancelled"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $appointmentMutationId = $null

    Add-Check -Name "appointment mutation lifecycle" -Result $(if ($appointmentMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        createdStatus = $createdAppointment.status
        cancelledStatus = $cancelledAppointment.status
        cancelledTitle = $cancelledAppointment.title
    }
}
catch {
    Add-Check -Name "appointment mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $encounters = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters?patientId=MOD-PAT-0001&from=2026-01-01&limit=5" -Method Get -TimeoutSec 20
    $anchorEncounter = $encounters.encounters | Select-Object -First 1
    $encounterPassed = $null -ne $anchorEncounter -and $anchorEncounter.patientId -eq "MOD-PAT-0001" -and $anchorEncounter.hasVitals -and $anchorEncounter.hasSoapNote
    Add-Check -Name "anchor encounter search" -Result $(if ($encounterPassed) { "passed" } else { "failed" }) -Details @{
        totalMatches = $encounters.totalMatches
        firstEncounter = $anchorEncounter
    }
}
catch {
    Add-Check -Name "anchor encounter search" -Result "failed" -Details $_.Exception.Message
}

try {
    if ($null -eq $anchorEncounter) {
        throw "Anchor encounter search did not return an encounter."
    }

    $encounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$($anchorEncounter.encounter)" -Method Get -TimeoutSec 20
    $encounterDetailPassed = $encounterDetail.patientId -eq "MOD-PAT-0001" -and $null -ne $encounterDetail.vitals -and $null -ne $encounterDetail.soapNote -and $null -ne $encounterDetail.soapNote.assessment
    Add-Check -Name "anchor encounter detail" -Result $(if ($encounterDetailPassed) { "passed" } else { "failed" }) -Details @{
        encounter = $encounterDetail.encounter
        reason = $encounterDetail.reason
        bloodPressure = $encounterDetail.vitals.bloodPressure
        assessment = $encounterDetail.soapNote.assessment
    }
}
catch {
    Add-Check -Name "anchor encounter detail" -Result "failed" -Details $_.Exception.Message
}

$smokeEncounterSignatureId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $signatureSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $signatureNote = "Smoke encounter sign-off $signatureSuffix"
    $signatureBody = @{
        signerUsername = "admin"
        signedAt = "2026-06-18 10:20:00"
        isLock = $false
        amendment = $signatureNote
    } | ConvertTo-Json -Depth 5

    $createdSignature = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/sign" -Method Put -ContentType "application/json" -Body $signatureBody -TimeoutSec 20
    $smokeEncounterSignatureId = $createdSignature.id
    $createdSignatureVisible = @($createdSignature.detail.signatures | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterSignatureId `
            -and $_.signerUsername -eq "admin" `
            -and $_.signedAt -eq "2026-06-18 10:20" `
            -and $_.isLock -eq $false `
            -and $_.amendment -eq $signatureNote
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/signatures/$smokeEncounterSignatureId" -Method Delete -TimeoutSec 20 | Out-Null
    $smokeEncounterSignatureId = $null
    $afterDeleteSignatureDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $deletedSignatureVisible = @($afterDeleteSignatureDetail.signatures | Where-Object { $null -ne $_ }) | Where-Object {
        $_.amendment -eq $signatureNote
    } | Select-Object -First 1

    $encounterSignOffPassed = $null -ne $createdSignatureVisible -and $null -eq $deletedSignatureVisible
    Add-Check -Name "encounter sign-off lifecycle" -Result $(if ($encounterSignOffPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        signatureId = $createdSignature.id
        signature = $createdSignatureVisible
    }
}
catch {
    Add-Check -Name "encounter sign-off lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterSignatureId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/signatures/$smokeEncounterSignatureId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterDocumentId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $documentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $documentName = "Smoke Encounter Attachment $documentSuffix"
    $documentContent = "Smoke encounter attachment content $documentSuffix."
    $documentBody = @{
        categoryId = 3
        name = $documentName
        docDate = "2026-06-18"
        content = $documentContent
        notes = "Created by the smoke encounter document attachment check."
    } | ConvertTo-Json -Depth 5

    $createdEncounterDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $documentBody -TimeoutSec 20
    $smokeEncounterDocumentId = $createdEncounterDocument.id
    $createdEncounterDocumentVisible = @($createdEncounterDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentId `
            -and $_.name -eq $documentName `
            -and $_.categoryName -eq "Medical Record" `
            -and $_.docDate -eq "2026-06-18" `
            -and $_.mimetype -eq "text/plain" `
            -and $_.storageMethod -eq "database" `
            -and $_.contentPreview -like "*$documentContent*"
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentId" -Method Delete -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentId = $null
    $afterDeleteDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $deletedEncounterDocumentVisible = @($afterDeleteDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $documentName
    } | Select-Object -First 1

    $encounterDocumentAttachmentPassed = $null -ne $createdEncounterDocumentVisible -and $null -eq $deletedEncounterDocumentVisible
    Add-Check -Name "encounter document attachment lifecycle" -Result $(if ($encounterDocumentAttachmentPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterDocument.id
        document = $createdEncounterDocumentVisible
    }
}
catch {
    Add-Check -Name "encounter document attachment lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterDocumentId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterBinaryDocumentId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $binaryDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $binaryDocumentName = "Smoke Encounter Binary Attachment $binaryDocumentSuffix.pdf"
    $binaryDocumentPayload = "Smoke binary encounter attachment content $binaryDocumentSuffix."
    $binaryDocumentBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($binaryDocumentPayload))
    $binaryDocumentBody = @{
        categoryId = 3
        name = $binaryDocumentName
        docDate = "2026-06-18"
        fileName = $binaryDocumentName
        mimetype = "application/pdf"
        contentBase64 = $binaryDocumentBase64
        notes = "Created by the smoke encounter binary document attachment check."
    } | ConvertTo-Json -Depth 5

    $createdEncounterBinaryDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/binary" -Method Post -ContentType "application/json" -Body $binaryDocumentBody -TimeoutSec 20
    $smokeEncounterBinaryDocumentId = $createdEncounterBinaryDocument.id
    $createdEncounterBinaryDocumentVisible = @($createdEncounterBinaryDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterBinaryDocumentId `
            -and $_.name -eq $binaryDocumentName `
            -and $_.categoryName -eq "Medical Record" `
            -and $_.docDate -eq "2026-06-18" `
            -and $_.mimetype -eq "application/pdf" `
            -and $_.fileName -eq $binaryDocumentName `
            -and $_.storageMethod -eq "database" `
            -and $_.previewKind -eq "pdf" `
            -and $_.thumbnailLabel -eq "PDF" `
            -and $_.canDownload -eq $true
    } | Select-Object -First 1

    $encounterBinaryContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryDocumentId/content" -Method Get -TimeoutSec 20
    $encounterBinaryDownloadClient = [System.Net.Http.HttpClient]::new()
    try {
        $encounterBinaryDownload = $encounterBinaryDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$smokeEncounterBinaryDocumentId/download").GetAwaiter().GetResult()
        $encounterBinaryDownloadBytes = $encounterBinaryDownload.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $encounterBinaryDownloadContentType = $encounterBinaryDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $encounterBinaryDownloadClient.Dispose()
    }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryDocumentId" -Method Delete -TimeoutSec 20 | Out-Null
    $smokeEncounterBinaryDocumentId = $null
    $afterDeleteBinaryDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $deletedEncounterBinaryDocumentVisible = @($afterDeleteBinaryDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $binaryDocumentName
    } | Select-Object -First 1

    $encounterBinaryDocumentAttachmentPassed = $null -ne $createdEncounterBinaryDocumentVisible `
        -and $encounterBinaryContent.contentBase64 -eq $binaryDocumentBase64 `
        -and $encounterBinaryDownload.IsSuccessStatusCode `
        -and $encounterBinaryDownloadContentType -eq "application/pdf" `
        -and [Convert]::ToBase64String($encounterBinaryDownloadBytes) -eq $binaryDocumentBase64 `
        -and $null -eq $deletedEncounterBinaryDocumentVisible
    Add-Check -Name "encounter binary document attachment lifecycle" -Result $(if ($encounterBinaryDocumentAttachmentPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterBinaryDocument.id
        document = $createdEncounterBinaryDocumentVisible
        downloadContentType = $encounterBinaryDownloadContentType
    }
}
catch {
    Add-Check -Name "encounter binary document attachment lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterBinaryDocumentId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryDocumentId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterDocumentSignOffId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $signedDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $signedDocumentName = "Smoke Encounter Signed Document $signedDocumentSuffix"
    $signedDocumentBody = @{
        categoryId = 3
        name = $signedDocumentName
        docDate = "2026-06-18"
        content = "Smoke encounter document sign-off content $signedDocumentSuffix."
        notes = "Created by the smoke encounter document sign-off check."
    } | ConvertTo-Json

    $createdEncounterSignedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $signedDocumentBody -TimeoutSec 20
    $smokeEncounterDocumentSignOffId = $createdEncounterSignedDocument.id
    $createdSignedDocumentVisible = @($createdEncounterSignedDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentSignOffId `
            -and $_.name -eq $signedDocumentName `
            -and $_.reviewStatus -eq "pending" `
            -and [string]::IsNullOrWhiteSpace($_.reviewedBy)
    } | Select-Object -First 1

    $signBody = @{
        reviewStatus = "approved"
        reviewedBy = "admin"
    } | ConvertTo-Json
    $signedEncounterDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentSignOffId/sign" -Method Put -ContentType "application/json" -Body $signBody -TimeoutSec 20
    $signedEncounterDocumentVisible = @($signedEncounterDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentSignOffId `
            -and $_.name -eq $signedDocumentName `
            -and $_.reviewStatus -eq "approved" `
            -and $_.reviewedBy -eq "admin" `
            -and -not [string]::IsNullOrWhiteSpace($_.reviewedAt)
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentSignOffId" -Method Delete -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentSignOffId = $null
    $afterDeleteSignedDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $deletedSignedDocumentVisible = @($afterDeleteSignedDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $signedDocumentName
    } | Select-Object -First 1

    $encounterDocumentSignOffPassed = $null -ne $createdSignedDocumentVisible `
        -and $null -ne $signedEncounterDocumentVisible `
        -and $null -eq $deletedSignedDocumentVisible
    Add-Check -Name "encounter document sign-off lifecycle" -Result $(if ($encounterDocumentSignOffPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterSignedDocument.id
        document = $signedEncounterDocumentVisible
    }
}
catch {
    Add-Check -Name "encounter document sign-off lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterDocumentSignOffId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentSignOffId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $encounterBillingLines = if ($null -eq $encounterDetail.billingLines) { @() } else { @($encounterDetail.billingLines) }
    $officeVisitLine = $encounterBillingLines | Where-Object { $_.codeType -eq "CPT4" -and $_.code -eq "99214" -and $_.codeText -eq "Established patient office visit" -and $_.fee -eq 168.00 -and $_.justify -eq "E78.5" } | Select-Object -First 1
    $venipunctureLine = $encounterBillingLines | Where-Object { $_.codeType -eq "CPT4" -and $_.code -eq "36415" -and $_.codeText -eq "Routine venipuncture" -and $_.fee -eq 18.00 -and $_.justify -eq "E78.5" } | Select-Object -First 1
    $encounterBillingPassed = $encounterDetail.encounter -eq 1000013 `
        -and $encounterDetail.billingLineCount -eq 2 `
        -and $encounterBillingLines.Count -eq 2 `
        -and $null -ne $officeVisitLine `
        -and $null -ne $venipunctureLine
    Add-Check -Name "anchor encounter billing linkage" -Result $(if ($encounterBillingPassed) { "passed" } else { "failed" }) -Details @{
        encounter = $encounterDetail.encounter
        billingLineCount = $encounterDetail.billingLineCount
        linkedBillingLines = $encounterBillingLines | Select-Object id, codeType, code, codeText, fee, justify, units, billed, activity
    }
}
catch {
    Add-Check -Name "anchor encounter billing linkage" -Result "failed" -Details $_.Exception.Message
}

try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $encounterClaims = @($encounterDetail.claims | Where-Object { $null -ne $_ })
    $linkedClaim = $encounterClaims | Where-Object { $_.id -eq "CLAIM-1000013-1" -and $_.payerName -eq "Acme Health" -and $_.statusLabel -eq "Marked as cleared" -and $_.target -eq "HCFA" } | Select-Object -First 1
    $encounterClaimsPassed = $encounterDetail.encounter -eq 1000013 `
        -and $encounterClaims.Count -eq 1 `
        -and $null -ne $linkedClaim `
        -and $linkedClaim.version -eq 1 `
        -and $linkedClaim.payerType -eq 1 `
        -and $linkedClaim.status -eq 3 `
        -and $linkedClaim.billProcess -eq 0
    Add-Check -Name "anchor encounter claim linkage" -Result $(if ($encounterClaimsPassed) { "passed" } else { "failed" }) -Details @{
        encounter = $encounterDetail.encounter
        claimCount = $encounterClaims.Count
        linkedClaims = $encounterClaims | Select-Object id, version, payerName, payerType, status, statusLabel, billProcess, billTime, processFile, target
    }
}
catch {
    Add-Check -Name "anchor encounter claim linkage" -Result "failed" -Details $_.Exception.Message
}

try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $procedureDiagnosisDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000011" -Method Get -TimeoutSec 20
    $encounterDiagnoses = @($encounterDetail.diagnosisCodes | Where-Object { $null -ne $_ })
    $procedureEncounterDiagnoses = @($procedureDiagnosisDetail.diagnosisCodes | Where-Object { $null -ne $_ })
    $hyperlipidemiaDiagnosis = $encounterDiagnoses | Where-Object { $_.code -eq "E78.5" } | Select-Object -First 1
    $diabetesDiagnosis = $procedureEncounterDiagnoses | Where-Object { $_.code -eq "E11.9" } | Select-Object -First 1
    $hyperlipidemiaSources = if ($null -eq $hyperlipidemiaDiagnosis) { @() } else { @($hyperlipidemiaDiagnosis.sources) }
    $hyperlipidemiaSupportingCodes = if ($null -eq $hyperlipidemiaDiagnosis) { @() } else { @($hyperlipidemiaDiagnosis.supportingBillingCodes) }
    $diabetesSources = if ($null -eq $diabetesDiagnosis) { @() } else { @($diabetesDiagnosis.sources) }
    $encounterDiagnosisPassed = $encounterDetail.encounter -eq 1000013 `
        -and $null -ne $hyperlipidemiaDiagnosis `
        -and $hyperlipidemiaDiagnosis.description -eq "Hyperlipidemia, unspecified" `
        -and $hyperlipidemiaDiagnosis.billingLineCount -eq 2 `
        -and $hyperlipidemiaDiagnosis.procedureOrderCount -eq 0 `
        -and ($hyperlipidemiaSources -contains "Encounter diagnosis") `
        -and ($hyperlipidemiaSources -contains "Fee sheet justification") `
        -and ($hyperlipidemiaSupportingCodes -contains "CPT4 99214") `
        -and ($hyperlipidemiaSupportingCodes -contains "CPT4 36415") `
        -and $procedureDiagnosisDetail.encounter -eq 1000011 `
        -and $null -ne $diabetesDiagnosis `
        -and $diabetesDiagnosis.description -eq "Type 2 diabetes mellitus without complications" `
        -and $diabetesDiagnosis.procedureOrderCount -eq 1 `
        -and ($diabetesSources -contains "Encounter diagnosis") `
        -and ($diabetesSources -contains "Procedure order diagnosis")
    Add-Check -Name "anchor encounter diagnosis coding linkage" -Result $(if ($encounterDiagnosisPassed) { "passed" } else { "failed" }) -Details @{
        billingEncounter = $encounterDetail.encounter
        billingDiagnosis = $hyperlipidemiaDiagnosis
        procedureEncounter = $procedureDiagnosisDetail.encounter
        procedureDiagnosis = $diabetesDiagnosis
    }
}
catch {
    Add-Check -Name "anchor encounter diagnosis coding linkage" -Result "failed" -Details $_.Exception.Message
}

try {
    $procedureEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000011" -Method Get -TimeoutSec 20
    if ($null -eq $procedureEncounterDetail) {
        throw "Procedure-linked encounter detail did not load."
    }

    $encounterProcedureOrders = @($procedureEncounterDetail.procedureOrders | Where-Object { $null -ne $_ })
    $linkedProcedureOrder = $encounterProcedureOrders | Where-Object {
        $_.id -eq 5000001 `
            -and $_.code -eq "83036" `
            -and $_.name -eq "Hemoglobin A1c" `
            -and $_.orderStatus -eq "complete" `
            -and $_.diagnosis -eq "E11.9"
    } | Select-Object -First 1
    $linkedProcedureReport = if ($null -ne $linkedProcedureOrder) {
        @($linkedProcedureOrder.reports | Where-Object { $null -ne $_ }) | Where-Object {
            $_.id -eq 6000001 -and $_.status -eq "complete" -and $_.reviewStatus -eq "reviewed"
        } | Select-Object -First 1
    } else {
        $null
    }
    $linkedProcedureResults = if ($null -ne $linkedProcedureReport) {
        @($linkedProcedureReport.results | Where-Object { $null -ne $_ })
    } else {
        @()
    }
    $hemoglobinA1cResult = $linkedProcedureResults | Where-Object {
        $_.code -eq "4548-4" -and $_.text -eq "Hemoglobin A1c" -and $_.result -eq "5.7" -and $_.units -eq "%"
    } | Select-Object -First 1
    $linkedProcedureReportCount = if ($null -eq $linkedProcedureOrder) { 0 } else { @($linkedProcedureOrder.reports | Where-Object { $null -ne $_ }).Count }
    $encounterProcedurePassed = $procedureEncounterDetail.encounter -eq 1000011 `
        -and $encounterProcedureOrders.Count -eq 1 `
        -and $null -ne $linkedProcedureOrder `
        -and $null -ne $linkedProcedureReport `
        -and $linkedProcedureResults.Count -eq 4 `
        -and $null -ne $hemoglobinA1cResult
    Add-Check -Name "anchor encounter procedure order linkage" -Result $(if ($encounterProcedurePassed) { "passed" } else { "failed" }) -Details @{
        encounter = $procedureEncounterDetail.encounter
        procedureOrderCount = $encounterProcedureOrders.Count
        linkedProcedureOrders = $encounterProcedureOrders | Select-Object id, encounter, orderDate, orderStatus, orderPriority, code, name, procedureType, diagnosis
        reportCount = $linkedProcedureReportCount
        resultCount = $linkedProcedureResults.Count
        hemoglobinA1cResult = $hemoglobinA1cResult
    }
}
catch {
    Add-Check -Name "anchor encounter procedure order linkage" -Result "failed" -Details $_.Exception.Message
}

$smokeEncounterProcedureOrderId = $null
try {
    $procedureOrderSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $procedureOrderName = "Smoke Encounter Procedure Order $procedureOrderSuffix"
    $procedureOrderBody = @{
        patientId = "MOD-PAT-0001"
        providerId = $null
        encounterId = 1000013
        dateOrdered = "2026-06-18"
        priority = "routine"
        status = "pending"
        procedureCode = "80053"
        procedureName = $procedureOrderName
        procedureType = "laboratory"
        diagnosis = "E78.5"
        instructions = "Created by the smoke encounter procedure order entry check."
    } | ConvertTo-Json -Depth 5

    $createdEncounterProcedureOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders" -Method Post -ContentType "application/json" -Body $procedureOrderBody -TimeoutSec 20
    $smokeEncounterProcedureOrderId = $createdEncounterProcedureOrder.id
    $refreshedEncounterProcedureDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $createdEncounterProcedureOrderRow = @($refreshedEncounterProcedureDetail.procedureOrders | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterProcedureOrderId `
            -and $_.name -eq $procedureOrderName `
            -and $_.code -eq "80053" `
            -and $_.diagnosis -eq "E78.5" `
            -and $_.orderStatus -eq "pending" `
            -and $_.orderPriority -eq "routine" `
            -and @($_.reports).Count -eq 0
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeEncounterProcedureOrderId" -Method Delete -TimeoutSec 20 | Out-Null
    $smokeEncounterProcedureOrderId = $null
    $afterDeleteEncounterProcedureDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $deletedEncounterProcedureOrderRow = @($afterDeleteEncounterProcedureDetail.procedureOrders | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $procedureOrderName
    } | Select-Object -First 1

    $encounterProcedureOrderEntryPassed = $null -ne $createdEncounterProcedureOrderRow -and $null -eq $deletedEncounterProcedureOrderRow
    Add-Check -Name "encounter procedure order entry lifecycle" -Result $(if ($encounterProcedureOrderEntryPassed) { "passed" } else { "failed" }) -Details @{
        createdOrderId = $createdEncounterProcedureOrder.id
        encounter = $refreshedEncounterProcedureDetail.encounter
        order = $createdEncounterProcedureOrderRow
    }
}
catch {
    Add-Check -Name "encounter procedure order entry lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterProcedureOrderId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeEncounterProcedureOrderId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterProcedureResultOrderId = $null
try {
    $procedureResultSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $procedureResultOrderName = "Smoke Encounter Procedure Result $procedureResultSuffix"
    $procedureResultText = "Smoke Encounter Glucose $procedureResultSuffix"
    $procedureResultOrderBody = @{
        patientId = "MOD-PAT-0001"
        providerId = $null
        encounterId = 1000013
        dateOrdered = "2026-06-18"
        priority = "routine"
        status = "pending"
        procedureCode = "80053"
        procedureName = $procedureResultOrderName
        procedureType = "laboratory"
        diagnosis = "E78.5"
        instructions = "Created by the smoke encounter procedure result entry check."
    } | ConvertTo-Json -Depth 5

    $createdEncounterProcedureResultOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders" -Method Post -ContentType "application/json" -Body $procedureResultOrderBody -TimeoutSec 20
    $smokeEncounterProcedureResultOrderId = $createdEncounterProcedureResultOrder.id

    $procedureResultReportBody = @{
        orderId = $smokeEncounterProcedureResultOrderId
        dateCollected = "2026-06-18 12:30:00"
        dateReport = "2026-06-18 13:00:00"
        specimenNumber = "SMOKE-ENC-PROC"
        reportStatus = "final"
        reviewStatus = "reviewed"
        notes = "Created by the smoke encounter procedure result entry check."
    } | ConvertTo-Json -Depth 5
    $createdEncounterProcedureResultReport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/reports" -Method Post -ContentType "application/json" -Body $procedureResultReportBody -TimeoutSec 20

    $procedureResultBody = @{
        reportId = $createdEncounterProcedureResultReport.id
        resultCode = "2345-7"
        resultText = $procedureResultText
        dateTime = "2026-06-18 13:05:00"
        facility = "Legacy EHR Modernization Clinic"
        units = "mg/dL"
        result = "104"
        range = "70-99"
        abnormal = "high"
        comments = "Created by the smoke encounter procedure result entry check."
        status = "final"
    } | ConvertTo-Json -Depth 5
    $createdEncounterProcedureResult = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/results" -Method Post -ContentType "application/json" -Body $procedureResultBody -TimeoutSec 20

    $refreshedEncounterProcedureResultDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $createdEncounterProcedureResultOrderRow = @($refreshedEncounterProcedureResultDetail.procedureOrders | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterProcedureResultOrderId `
            -and $_.name -eq $procedureResultOrderName `
            -and $_.code -eq "80053" `
            -and $_.diagnosis -eq "E78.5" `
            -and $_.orderStatus -eq "pending"
    } | Select-Object -First 1
    $createdEncounterProcedureResultReportRow = if ($null -ne $createdEncounterProcedureResultOrderRow) {
        @($createdEncounterProcedureResultOrderRow.reports | Where-Object { $null -ne $_ }) | Where-Object {
            $_.id -eq $createdEncounterProcedureResultReport.id -and $_.status -eq "final" -and $_.reviewStatus -eq "reviewed"
        } | Select-Object -First 1
    } else {
        $null
    }
    $createdEncounterProcedureResultRow = if ($null -ne $createdEncounterProcedureResultReportRow) {
        @($createdEncounterProcedureResultReportRow.results | Where-Object { $null -ne $_ }) | Where-Object {
            $_.id -eq $createdEncounterProcedureResult.id `
                -and $_.text -eq $procedureResultText `
                -and $_.result -eq "104" `
                -and $_.units -eq "mg/dL" `
                -and $_.abnormal -eq "high" `
                -and $_.resultStatus -eq "final"
        } | Select-Object -First 1
    } else {
        $null
    }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeEncounterProcedureResultOrderId" -Method Delete -TimeoutSec 20 | Out-Null
    $smokeEncounterProcedureResultOrderId = $null
    $afterDeleteEncounterProcedureResultDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $deletedEncounterProcedureResultOrderRow = @($afterDeleteEncounterProcedureResultDetail.procedureOrders | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $procedureResultOrderName
    } | Select-Object -First 1

    $encounterProcedureResultEntryPassed = $null -ne $createdEncounterProcedureResultOrderRow `
        -and $null -ne $createdEncounterProcedureResultReportRow `
        -and $null -ne $createdEncounterProcedureResultRow `
        -and $null -eq $deletedEncounterProcedureResultOrderRow
    Add-Check -Name "encounter procedure result entry lifecycle" -Result $(if ($encounterProcedureResultEntryPassed) { "passed" } else { "failed" }) -Details @{
        createdOrderId = $createdEncounterProcedureResultOrder.id
        reportId = $createdEncounterProcedureResultReport.id
        resultId = $createdEncounterProcedureResult.id
        encounter = $refreshedEncounterProcedureResultDetail.encounter
        order = $createdEncounterProcedureResultOrderRow
        report = $createdEncounterProcedureResultReportRow
        result = $createdEncounterProcedureResultRow
    }
}
catch {
    Add-Check -Name "encounter procedure result entry lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterProcedureResultOrderId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeEncounterProcedureResultOrderId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $attachedDocuments = if ($null -eq $encounterDetail.documents) { @() } else { @($encounterDetail.documents) }
    $intakePacketAttachment = $attachedDocuments | Where-Object { $_.name -eq "Primary care intake packet" -and $_.categoryName -eq "Medical Record" } | Select-Object -First 1
    $advanceDirectiveAttachment = $attachedDocuments | Where-Object { $_.name -eq "Advance directive acknowledgement" -and $_.categoryName -eq "Advance Directive" } | Select-Object -First 1
    $encounterDocumentsPassed = $encounterDetail.encounter -eq 1000013 `
        -and $attachedDocuments.Count -eq 2 `
        -and $null -ne $intakePacketAttachment `
        -and $intakePacketAttachment.documentKey -eq "DOC-MOD-PAT-0001-1" `
        -and $intakePacketAttachment.previewKind -eq "text" `
        -and $intakePacketAttachment.thumbnailLabel -eq "TXT" `
        -and $null -ne $advanceDirectiveAttachment `
        -and $advanceDirectiveAttachment.documentKey -eq "DOC-MOD-PAT-0001-2" `
        -and $advanceDirectiveAttachment.previewKind -eq "text" `
        -and $advanceDirectiveAttachment.thumbnailLabel -eq "TXT"
    Add-Check -Name "anchor encounter document attachments" -Result $(if ($encounterDocumentsPassed) { "passed" } else { "failed" }) -Details @{
        encounter = $encounterDetail.encounter
        documentCount = $attachedDocuments.Count
        documents = $attachedDocuments | Select-Object name, documentKey, categoryName, docDate, previewKind, thumbnailLabel
    }
}
catch {
    Add-Check -Name "anchor encounter document attachments" -Result "failed" -Details $_.Exception.Message
}

$encounterMutationId = $null
try {
    $createEncounterBody = @{
        patientId = "MOD-PAT-0002"
        providerId = $null
        dateTime = "2026-06-18 10:00:00"
        reason = "Smoke Encounter Mutation"
        facilityId = $null
        billingFacilityId = $null
        billingNote = "Created by the smoke encounter mutation check."
    } | ConvertTo-Json
    $createdEncounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters" -Method Post -ContentType "application/json" -Body $createEncounterBody -TimeoutSec 20
    $encounterMutationId = $createdEncounter.encounter

    $vitalsBody = @{
        dateTime = "2026-06-18 10:05:00"
        systolic = 128
        diastolic = 76
        weight = 186
        height = 70
        pulse = 72
        oxygenSaturation = 98
        note = "Smoke vitals detail."
    } | ConvertTo-Json
    $createdVitals = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId/vitals" -Method Post -ContentType "application/json" -Body $vitalsBody -TimeoutSec 20

    $soapBody = @{
        dateTime = "2026-06-18 10:10:00"
        subjective = "Patient reports smoke workflow symptoms are stable."
        objective = "Vitals reviewed during smoke workflow."
        assessment = "Stable smoke workflow condition."
        plan = "Continue smoke workflow validation."
    } | ConvertTo-Json
    $createdSoap = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId/soap-notes" -Method Post -ContentType "application/json" -Body $soapBody -TimeoutSec 20

    $updateBody = @{
        reason = "Smoke Encounter Mutation Updated"
        billingNote = "Updated by the smoke encounter mutation check."
    } | ConvertTo-Json
    $updatedEncounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId" -Method Put -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $encounterMutationPassed = $createdEncounter.reason -eq "Smoke Encounter Mutation" `
        -and $createdVitals.id -gt 0 `
        -and $createdSoap.id -gt 0 `
        -and $updatedEncounter.reason -eq "Smoke Encounter Mutation Updated" `
        -and $updatedEncounter.billingNote -eq "Updated by the smoke encounter mutation check." `
        -and $updatedEncounter.vitals.bloodPressure -eq "128/76" `
        -and $updatedEncounter.soapNote.assessment -eq "Stable smoke workflow condition."

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $encounterMutationId = $null

    Add-Check -Name "encounter mutation lifecycle" -Result $(if ($encounterMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdEncounter = $createdEncounter.encounter
        vitalsId = $createdVitals.id
        soapId = $createdSoap.id
        updatedReason = $updatedEncounter.reason
        bloodPressure = $updatedEncounter.vitals.bloodPressure
        assessment = $updatedEncounter.soapNote.assessment
    }
}
catch {
    Add-Check -Name "encounter mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $encounterMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$encounterMetadataMutationId = $null
try {
    $metadataSuffix = Get-Random -Minimum 10000 -Maximum 99999
    $createMetadataBody = @{
        patientId = "MOD-PAT-0002"
        providerId = $null
        dateTime = "2026-06-18 11:00:00"
        reason = "Smoke Encounter Metadata $metadataSuffix"
        facilityId = $null
        billingFacilityId = $null
        sensitivity = "normal"
        referralSource = "self"
        externalId = "EXT-$metadataSuffix"
        posCode = 11
        billingNote = "Created by the smoke encounter metadata check."
    } | ConvertTo-Json
    $createdMetadataEncounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters" -Method Post -ContentType "application/json" -Body $createMetadataBody -TimeoutSec 20
    $encounterMetadataMutationId = $createdMetadataEncounter.encounter

    $updateMetadataBody = @{
        reason = "Smoke Encounter Metadata $metadataSuffix Updated"
        sensitivity = "high"
        referralSource = "physician"
        externalId = "UPD-$metadataSuffix"
        posCode = 22
        billingNote = "Updated by the smoke encounter metadata check."
    } | ConvertTo-Json
    $updatedMetadataEncounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMetadataMutationId" -Method Put -ContentType "application/json" -Body $updateMetadataBody -TimeoutSec 20
    $encounterMetadataPassed = $createdMetadataEncounter.sensitivity -eq "normal" `
        -and $createdMetadataEncounter.referralSource -eq "self" `
        -and $createdMetadataEncounter.externalId -eq "EXT-$metadataSuffix" `
        -and $createdMetadataEncounter.posCode -eq 11 `
        -and $updatedMetadataEncounter.sensitivity -eq "high" `
        -and $updatedMetadataEncounter.referralSource -eq "physician" `
        -and $updatedMetadataEncounter.externalId -eq "UPD-$metadataSuffix" `
        -and $updatedMetadataEncounter.posCode -eq 22 `
        -and $updatedMetadataEncounter.billingNote -eq "Updated by the smoke encounter metadata check."

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMetadataMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $encounterMetadataMutationId = $null

    Add-Check -Name "encounter metadata mutation lifecycle" -Result $(if ($encounterMetadataPassed) { "passed" } else { "failed" }) -Details @{
        createdEncounter = $createdMetadataEncounter.encounter
        createdSensitivity = $createdMetadataEncounter.sensitivity
        updatedSensitivity = $updatedMetadataEncounter.sensitivity
        updatedReferralSource = $updatedMetadataEncounter.referralSource
        updatedExternalId = $updatedMetadataEncounter.externalId
        updatedPosCode = $updatedMetadataEncounter.posCode
    }
}
catch {
    Add-Check -Name "encounter metadata mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $encounterMetadataMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMetadataMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $clinicalLists = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/MOD-PAT-0001" -Method Get -TimeoutSec 20
    $problem = $clinicalLists.problems | Where-Object { $_.title -like "*diabetes*" } | Select-Object -First 1
    $allergy = $clinicalLists.allergies | Where-Object { $_.title -eq "Penicillin" } | Select-Object -First 1
    $medication = $clinicalLists.medications | Where-Object { $_.title -like "Metformin*" } | Select-Object -First 1
    $prescription = $clinicalLists.prescriptions | Where-Object { $_.drug -eq "Metformin" } | Select-Object -First 1
    $clinicalListsPassed = $clinicalLists.patientId -eq "MOD-PAT-0001" -and $null -ne $problem -and $null -ne $allergy -and $null -ne $medication -and $null -ne $prescription
    Add-Check -Name "anchor clinical lists" -Result $(if ($clinicalListsPassed) { "passed" } else { "failed" }) -Details @{
        problems = $clinicalLists.problems.Count
        allergies = $clinicalLists.allergies.Count
        medications = $clinicalLists.medications.Count
        immunizations = $clinicalLists.immunizations.Count
        prescriptions = $clinicalLists.prescriptions.Count
        problem = $problem
        allergy = $allergy
        medication = $medication
        prescription = $prescription
    }
}
catch {
    Add-Check -Name "anchor clinical lists" -Result "failed" -Details $_.Exception.Message
}

try {
    $immunizationLists = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/MOD-PAT-0007" -Method Get -TimeoutSec 20
    $influenza = $immunizationLists.immunizations | Where-Object { $_.vaccine -eq "Influenza, seasonal, injectable" -and $_.cvxCode -eq "141" } | Select-Object -First 1
    $hepatitisA = $immunizationLists.immunizations | Where-Object { $_.vaccine -eq "Hep A, ped/adol, 2 dose" -and $_.manufacturer -eq "GlaxoSmithKline" } | Select-Object -First 1
    $immunizationsPassed = $immunizationLists.patientId -eq "MOD-PAT-0007" -and $immunizationLists.immunizations.Count -ge 8 -and $null -ne $influenza -and $null -ne $hepatitisA
    Add-Check -Name "anchor immunizations" -Result $(if ($immunizationsPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $immunizationLists.patientId
        immunizations = $immunizationLists.immunizations.Count
        influenza = $influenza
        hepatitisA = $hepatitisA
    }
}
catch {
    Add-Check -Name "anchor immunizations" -Result "failed" -Details $_.Exception.Message
}

$clinicalAllergyMutationId = $null
try {
    $allergyTitle = "Smoke Allergy Mutation"
    $createAllergyBody = @{
        patientId = "MOD-PAT-0006"
        title = $allergyTitle
        dateTime = "2026-06-18 09:00:00"
        comments = "Created by the smoke clinical-list mutation check."
        reaction = "Rash"
        severity = "mild"
        listOptionId = "parity-allergy"
    } | ConvertTo-Json
    $createdAllergy = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/allergies" -Method Post -ContentType "application/json" -Body $createAllergyBody -TimeoutSec 20
    $clinicalAllergyMutationId = $createdAllergy.id
    $createdVisible = $createdAllergy.detail.allergies | Where-Object { $_.title -eq $allergyTitle -and $_.reaction -eq "Rash" -and $_.severity -eq "mild" } | Select-Object -First 1

    $deactivateBody = @{
        comments = "Deactivated by the smoke clinical-list mutation check."
    } | ConvertTo-Json
    $deactivatedAllergy = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/allergies/$clinicalAllergyMutationId/deactivate" -Method Put -ContentType "application/json" -Body $deactivateBody -TimeoutSec 20
    $inactiveVisible = $deactivatedAllergy.detail.allergies | Where-Object { $_.title -eq $allergyTitle } | Select-Object -First 1
    $clinicalAllergyMutationPassed = $null -ne $createdVisible -and $null -eq $inactiveVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/allergies/$clinicalAllergyMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $clinicalAllergyMutationId = $null

    Add-Check -Name "clinical allergy mutation lifecycle" -Result $(if ($clinicalAllergyMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAllergy.id
        createdVisible = $createdVisible
        inactiveVisible = $inactiveVisible
    }
}
catch {
    Add-Check -Name "clinical allergy mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $clinicalAllergyMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/allergies/$clinicalAllergyMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$clinicalProblemMutationId = $null
try {
    $problemTitle = "Smoke Problem Mutation"
    $createProblemBody = @{
        patientId = "MOD-PAT-0006"
        title = $problemTitle
        dateTime = "2026-06-18 09:00:00"
        diagnosis = "ICD10:Z00.00"
        comments = "Created by the smoke problem-list mutation check."
    } | ConvertTo-Json
    $createdProblem = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems" -Method Post -ContentType "application/json" -Body $createProblemBody -TimeoutSec 20
    $clinicalProblemMutationId = $createdProblem.id
    $createdProblemVisible = $createdProblem.detail.problems | Where-Object { $_.title -eq $problemTitle -and $_.diagnosis -eq "ICD10:Z00.00" -and $_.activity -eq 1 } | Select-Object -First 1

    $deactivateProblemBody = @{
        comments = "Deactivated by the smoke problem-list mutation check."
    } | ConvertTo-Json
    $deactivatedProblem = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$clinicalProblemMutationId/deactivate" -Method Put -ContentType "application/json" -Body $deactivateProblemBody -TimeoutSec 20
    $inactiveProblemVisible = $deactivatedProblem.detail.problems | Where-Object { $_.title -eq $problemTitle } | Select-Object -First 1
    $clinicalProblemMutationPassed = $null -ne $createdProblemVisible -and $null -eq $inactiveProblemVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$clinicalProblemMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $clinicalProblemMutationId = $null

    Add-Check -Name "clinical problem mutation lifecycle" -Result $(if ($clinicalProblemMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdProblem.id
        createdVisible = $createdProblemVisible
        inactiveVisible = $inactiveProblemVisible
    }
}
catch {
    Add-Check -Name "clinical problem mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $clinicalProblemMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$clinicalProblemMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$clinicalMedicationMutationId = $null
try {
    $medicationTitle = "Smoke Medication List Mutation"
    $createMedicationBody = @{
        patientId = "MOD-PAT-0006"
        title = $medicationTitle
        dateTime = "2026-07-15 09:00:00"
        diagnosis = "ICD10:Z00.00"
        comments = "Created by the smoke medication-list mutation check."
    } | ConvertTo-Json
    $createdMedication = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications" -Method Post -ContentType "application/json" -Body $createMedicationBody -TimeoutSec 20
    $clinicalMedicationMutationId = $createdMedication.id
    $createdMedicationVisible = $createdMedication.detail.medications | Where-Object { $_.title -eq $medicationTitle -and $_.diagnosis -eq "ICD10:Z00.00" -and $_.activity -eq 1 } | Select-Object -First 1

    $deactivateMedicationBody = @{
        comments = "Deactivated by the smoke medication-list mutation check."
    } | ConvertTo-Json
    $deactivatedMedication = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$clinicalMedicationMutationId/deactivate" -Method Put -ContentType "application/json" -Body $deactivateMedicationBody -TimeoutSec 20
    $inactiveMedicationVisible = $deactivatedMedication.detail.medications | Where-Object { $_.title -eq $medicationTitle } | Select-Object -First 1
    $clinicalMedicationMutationPassed = $null -ne $createdMedicationVisible -and $null -eq $inactiveMedicationVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$clinicalMedicationMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $clinicalMedicationMutationId = $null

    Add-Check -Name "clinical medication mutation lifecycle" -Result $(if ($clinicalMedicationMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdMedication.id
        createdVisible = $createdMedicationVisible
        inactiveVisible = $inactiveMedicationVisible
    }
}
catch {
    Add-Check -Name "clinical medication mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $clinicalMedicationMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$clinicalMedicationMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$clinicalPrescriptionMutationId = $null
try {
    $prescriptionDrug = "Smoke Prescription Mutation"
    $createPrescriptionBody = @{
        patientId = "MOD-PAT-0008"
        providerId = $null
        startDate = "2026-07-15"
        drug = $prescriptionDrug
        rxNormCode = "1049502"
        dosage = "1 tablet daily"
        quantity = "30"
        route = "oral"
        refills = 1
        note = "Created by the smoke prescription mutation check."
        diagnosis = "Z00.00"
    } | ConvertTo-Json
    $createdPrescription = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions" -Method Post -ContentType "application/json" -Body $createPrescriptionBody -TimeoutSec 20
    $clinicalPrescriptionMutationId = $createdPrescription.id
    $createdPrescriptionVisible = $createdPrescription.detail.prescriptions | Where-Object { $_.drug -eq $prescriptionDrug -and $_.dosage -eq "1 tablet daily" -and $_.active -eq 1 } | Select-Object -First 1

    $deactivatePrescriptionBody = @{
        endDate = "2026-08-15"
        note = "Deactivated by the smoke prescription mutation check."
    } | ConvertTo-Json
    $deactivatedPrescription = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$clinicalPrescriptionMutationId/deactivate" -Method Put -ContentType "application/json" -Body $deactivatePrescriptionBody -TimeoutSec 20
    $inactivePrescriptionVisible = $deactivatedPrescription.detail.prescriptions | Where-Object { $_.drug -eq $prescriptionDrug } | Select-Object -First 1
    $clinicalPrescriptionMutationPassed = $null -ne $createdPrescriptionVisible -and $null -eq $inactivePrescriptionVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$clinicalPrescriptionMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $clinicalPrescriptionMutationId = $null

    Add-Check -Name "clinical prescription mutation lifecycle" -Result $(if ($clinicalPrescriptionMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdPrescription.id
        createdVisible = $createdPrescriptionVisible
        inactiveVisible = $inactivePrescriptionVisible
    }
}
catch {
    Add-Check -Name "clinical prescription mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $clinicalPrescriptionMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$clinicalPrescriptionMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$clinicalImmunizationMutationId = $null
try {
    $immunizationLot = "SMOKE-IMM-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
    $createImmunizationBody = @{
        patientId = "MOD-PAT-0007"
        encounter = $null
        immunizationId = 30
        cvxCode = "141"
        vaccine = "Influenza, seasonal, injectable"
        administeredAt = "2026-09-10 10:30:00"
        manufacturer = "Sanofi Pasteur"
        lotNumber = $immunizationLot
        administeredById = $null
        administeredBy = "admin"
        educationDate = "2026-09-10"
        visDate = "2026-08-01"
        amountAdministered = 0.5
        amountAdministeredUnit = "mL"
        expirationDate = "2027-06-30"
        route = "intramuscular"
        administrationSite = "left deltoid"
        completionStatus = "completed"
        informationSource = "new_immunization_record"
        note = "Created by the smoke immunization mutation check."
    } | ConvertTo-Json
    $createdImmunization = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/immunizations" -Method Post -ContentType "application/json" -Body $createImmunizationBody -TimeoutSec 20
    $clinicalImmunizationMutationId = $createdImmunization.id
    $createdImmunizationVisible = $createdImmunization.detail.immunizations | Where-Object { $_.lotNumber -eq $immunizationLot -and $_.cvxCode -eq "141" } | Select-Object -First 1

    $enteredInErrorBody = @{
        note = "Marked entered in error by the smoke immunization mutation check."
    } | ConvertTo-Json
    $enteredInErrorImmunization = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/immunizations/$clinicalImmunizationMutationId/entered-in-error" -Method Put -ContentType "application/json" -Body $enteredInErrorBody -TimeoutSec 20
    $enteredInErrorVisible = $enteredInErrorImmunization.detail.immunizations | Where-Object { $_.lotNumber -eq $immunizationLot } | Select-Object -First 1
    $clinicalImmunizationMutationPassed = $null -ne $createdImmunizationVisible -and $null -eq $enteredInErrorVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/immunizations/$clinicalImmunizationMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $clinicalImmunizationMutationId = $null

    Add-Check -Name "clinical immunization mutation lifecycle" -Result $(if ($clinicalImmunizationMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdImmunization.id
        createdVisible = $createdImmunizationVisible
        enteredInErrorVisible = $enteredInErrorVisible
    }
}
catch {
    Add-Check -Name "clinical immunization mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $clinicalImmunizationMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/immunizations/$clinicalImmunizationMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $messages = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/MOD-PAT-0004" -Method Get -TimeoutSec 20
    $careTeamMessage = $messages.messages | Where-Object { $_.title -eq "Care team follow-up" -and $_.status -eq "New" } | Select-Object -First 1
    $portalMessage = $messages.messages | Where-Object { $_.title -eq "Portal message" -and $_.status -eq "Done" } | Select-Object -First 1
    $messagesPassed = $messages.patientId -eq "MOD-PAT-0004" -and $messages.portalEnabled -and $null -ne $careTeamMessage -and $null -ne $portalMessage
    Add-Check -Name "anchor patient messages" -Result $(if ($messagesPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $messages.patientId
        portalEnabled = $messages.portalEnabled
        messageCount = $messages.messages.Count
        careTeamMessage = $careTeamMessage
        portalMessage = $portalMessage
    }
}
catch {
    Add-Check -Name "anchor patient messages" -Result "failed" -Details $_.Exception.Message
}

try {
    $documents = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" -Method Get -TimeoutSec 20
    $intakePacket = $documents.documents | Where-Object { $_.name -eq "Primary care intake packet" -and $_.categoryName -eq "Medical Record" } | Select-Object -First 1
    $advanceDirective = $documents.documents | Where-Object { $_.name -eq "Advance directive acknowledgement" -and $_.categoryName -eq "Advance Directive" } | Select-Object -First 1
    $documentsPassed = $documents.patientId -eq "MOD-PAT-0001" `
        -and $documents.count -eq 2 `
        -and $null -ne $intakePacket `
        -and $null -ne $advanceDirective `
        -and $intakePacket.contentPreview.Contains("Gold synthetic document DOC-MOD-PAT-0001-1")
    Add-Check -Name "anchor patient documents" -Result $(if ($documentsPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $documents.patientId
        documentCount = $documents.count
        intakePacket = $intakePacket
        advanceDirective = $advanceDirective
    }
}
catch {
    Add-Check -Name "anchor patient documents" -Result "failed" -Details $_.Exception.Message
}

try {
    $documents = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" -Method Get -TimeoutSec 20
    $intakePacket = $documents.documents | Where-Object { $_.name -eq "Primary care intake packet" } | Select-Object -First 1
    $advanceDirective = $documents.documents | Where-Object { $_.name -eq "Advance directive acknowledgement" } | Select-Object -First 1
    $documentPreviewPassed = $null -ne $intakePacket `
        -and $null -ne $advanceDirective `
        -and $intakePacket.previewKind -eq "text" `
        -and $intakePacket.previewStatus -eq "Inline text preview" `
        -and $intakePacket.thumbnailLabel -eq "TXT" `
        -and $intakePacket.thumbnailText.Contains("Gold synthetic document DOC-MOD-PAT-0001-1") `
        -and $intakePacket.canPreviewInline `
        -and $intakePacket.canDownload `
        -and $advanceDirective.previewKind -eq "text" `
        -and $advanceDirective.thumbnailLabel -eq "TXT" `
        -and $advanceDirective.thumbnailText.Contains("Gold synthetic document DOC-MOD-PAT-0001-2")
    Add-Check -Name "anchor patient document preview readiness" -Result $(if ($documentPreviewPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $documents.patientId
        intakePreview = if ($null -ne $intakePacket) { @{
            previewKind = $intakePacket.previewKind
            previewStatus = $intakePacket.previewStatus
            thumbnailLabel = $intakePacket.thumbnailLabel
            thumbnailText = $intakePacket.thumbnailText
            canPreviewInline = $intakePacket.canPreviewInline
            canDownload = $intakePacket.canDownload
        } } else { $null }
        advanceDirectivePreview = if ($null -ne $advanceDirective) { @{
            previewKind = $advanceDirective.previewKind
            previewStatus = $advanceDirective.previewStatus
            thumbnailLabel = $advanceDirective.thumbnailLabel
            thumbnailText = $advanceDirective.thumbnailText
            canPreviewInline = $advanceDirective.canPreviewInline
            canDownload = $advanceDirective.canDownload
        } } else { $null }
    }
}
catch {
    Add-Check -Name "anchor patient document preview readiness" -Result "failed" -Details $_.Exception.Message
}

try {
    $documents = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" -Method Get -TimeoutSec 20
    $intakePacket = $documents.documents | Where-Object { $_.name -eq "Primary care intake packet" } | Select-Object -First 1
    $advanceDirective = $documents.documents | Where-Object { $_.name -eq "Advance directive acknowledgement" } | Select-Object -First 1
    $documentRevisionPassed = $null -ne $intakePacket `
        -and $null -ne $advanceDirective `
        -and $intakePacket.currentVersion -eq 1 `
        -and $intakePacket.versionLabel -eq "Version 1" `
        -and $intakePacket.versionStatus -eq "Current version" `
        -and $intakePacket.versionHistoryCount -eq 1 `
        -and -not $intakePacket.hasPriorVersions `
        -and $intakePacket.revisionAt -eq "2026-06-10 14:30:00" `
        -and $intakePacket.revisionHash -eq $intakePacket.hash `
        -and $advanceDirective.currentVersion -eq 1 `
        -and $advanceDirective.versionLabel -eq "Version 1" `
        -and $advanceDirective.versionStatus -eq "Current version" `
        -and $advanceDirective.revisionAt -eq "2026-06-12 15:00:00" `
        -and $advanceDirective.revisionHash -eq $advanceDirective.hash
    Add-Check -Name "anchor patient document revision readiness" -Result $(if ($documentRevisionPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $documents.patientId
        intakeRevision = if ($null -ne $intakePacket) { @{
            currentVersion = $intakePacket.currentVersion
            versionLabel = $intakePacket.versionLabel
            versionStatus = $intakePacket.versionStatus
            revisionAt = $intakePacket.revisionAt
            versionHistoryCount = $intakePacket.versionHistoryCount
            hasPriorVersions = $intakePacket.hasPriorVersions
            revisionHash = $intakePacket.revisionHash
        } } else { $null }
        advanceDirectiveRevision = if ($null -ne $advanceDirective) { @{
            currentVersion = $advanceDirective.currentVersion
            versionLabel = $advanceDirective.versionLabel
            versionStatus = $advanceDirective.versionStatus
            revisionAt = $advanceDirective.revisionAt
            versionHistoryCount = $advanceDirective.versionHistoryCount
            hasPriorVersions = $advanceDirective.hasPriorVersions
            revisionHash = $advanceDirective.revisionHash
        } } else { $null }
    }
}
catch {
    Add-Check -Name "anchor patient document revision readiness" -Result "failed" -Details $_.Exception.Message
}

try {
    $documents = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" -Method Get -TimeoutSec 20
    $intakePacket = $documents.documents | Where-Object { $_.name -eq "Primary care intake packet" -and $_.categoryName -eq "Medical Record" } | Select-Object -First 1
    if ($null -eq $intakePacket) {
        throw "Primary care intake packet document was not found."
    }

    $documentContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$($intakePacket.id)/content" -Method Get -TimeoutSec 20
    $downloadClient = [System.Net.Http.HttpClient]::new()
    try {
        $documentDownload = $downloadClient.GetAsync("$ApiBaseUrl/api/documents/$($intakePacket.id)/download").GetAwaiter().GetResult()
        $documentDownloadBody = $documentDownload.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $documentDownloadContentType = $documentDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $downloadClient.Dispose()
    }

    $documentContentPassed = $documentContent.name -eq "Primary care intake packet" `
        -and $documentContent.fileName -eq "Primary care intake packet.txt" `
        -and $documentContent.content.Contains("Gold synthetic document DOC-MOD-PAT-0001-1") `
        -and $documentContent.content.Contains("Purpose: Stable search and demographics navigation") `
        -and $documentDownload.IsSuccessStatusCode `
        -and $documentDownloadContentType -eq "text/plain" `
        -and $documentDownloadBody.Contains("Gold synthetic document DOC-MOD-PAT-0001-1")
    Add-Check -Name "anchor patient document content" -Result $(if ($documentContentPassed) { "passed" } else { "failed" }) -Details @{
        documentId = $intakePacket.id
        fileName = $documentContent.fileName
        mimetype = $documentContent.mimetype
        downloadStatus = [int]$documentDownload.StatusCode
        downloadContentType = $documentDownloadContentType
    }
}
catch {
    Add-Check -Name "anchor patient document content" -Result "failed" -Details $_.Exception.Message
}

$patientDocumentMutationId = $null
try {
    $documentName = "Smoke Patient Document Mutation"
    $documentBody = "Created by the smoke patient-document mutation check."
    $createDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $documentName
        docDate = "2026-06-18"
        encounter = 1000013
        content = $documentBody
        notes = "Created by the smoke patient-document mutation check."
    } | ConvertTo-Json
    $createdDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createDocumentBody -TimeoutSec 20
    $patientDocumentMutationId = $createdDocument.id
    $createdVisible = $createdDocument.detail.documents | Where-Object { $_.name -eq $documentName -and $_.categoryName -eq "Medical Record" -and $_.contentPreview -and $_.contentPreview.Contains($documentBody) } | Select-Object -First 1

    $signDocumentBody = @{
        reviewStatus = "approved"
        reviewedBy = "admin"
    } | ConvertTo-Json
    $signedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId/sign" -Method Put -ContentType "application/json" -Body $signDocumentBody -TimeoutSec 20
    $signedVisible = $signedDocument.detail.documents | Where-Object { $_.name -eq $documentName -and $_.reviewStatus -eq "approved" -and $_.reviewedBy -eq "admin" } | Select-Object -First 1

    $archivedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId/soft-delete" -Method Put -TimeoutSec 20
    $archivedVisible = $archivedDocument.detail.documents | Where-Object { $_.name -eq $documentName } | Select-Object -First 1
    $patientDocumentMutationPassed = $null -ne $createdVisible -and $null -ne $signedVisible -and $null -eq $archivedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientDocumentMutationId = $null

    Add-Check -Name "patient document mutation lifecycle" -Result $(if ($patientDocumentMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdDocument.id
        createdVisible = $createdVisible
        signedVisible = $signedVisible
        archivedVisible = $archivedVisible
    }
}
catch {
    Add-Check -Name "patient document mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientDocumentMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientDocumentDenialMutationId = $null
try {
    $deniedDocumentName = "Smoke Denied Patient Document"
    $deniedDocumentBody = "Created by the smoke patient-document denial check."
    $createDeniedDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $deniedDocumentName
        docDate = "2026-06-18"
        encounter = 1000013
        content = $deniedDocumentBody
        notes = "Created by the smoke patient-document denial check."
    } | ConvertTo-Json
    $createdDeniedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createDeniedDocumentBody -TimeoutSec 20
    $patientDocumentDenialMutationId = $createdDeniedDocument.id
    $createdDeniedVisible = $createdDeniedDocument.detail.documents | Where-Object { $_.name -eq $deniedDocumentName -and $_.reviewStatus -eq "pending" } | Select-Object -First 1

    $denyDocumentBody = @{
        reviewStatus = "denied"
        reviewedBy = "admin"
    } | ConvertTo-Json
    $deniedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentDenialMutationId/sign" -Method Put -ContentType "application/json" -Body $denyDocumentBody -TimeoutSec 20
    $deniedVisible = $deniedDocument.detail.documents | Where-Object { $_.name -eq $deniedDocumentName -and $_.reviewStatus -eq "denied" -and $_.reviewedBy -eq "admin" } | Select-Object -First 1

    $archivedDeniedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentDenialMutationId/soft-delete" -Method Put -TimeoutSec 20
    $archivedDeniedVisible = $archivedDeniedDocument.detail.documents | Where-Object { $_.name -eq $deniedDocumentName } | Select-Object -First 1
    $patientDocumentDenialPassed = $null -ne $createdDeniedVisible -and $null -ne $deniedVisible -and $null -eq $archivedDeniedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentDenialMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientDocumentDenialMutationId = $null

    Add-Check -Name "patient document denial lifecycle" -Result $(if ($patientDocumentDenialPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdDeniedDocument.id
        createdVisible = $createdDeniedVisible
        deniedVisible = $deniedVisible
        archivedVisible = $archivedDeniedVisible
    }
}
catch {
    Add-Check -Name "patient document denial lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientDocumentDenialMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentDenialMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientDocumentMetadataMutationId = $null
try {
    $metadataDocumentName = "Smoke Metadata Patient Document"
    $metadataDocumentUpdatedName = "Smoke Refiled Advance Directive"
    $metadataDocumentBody = "Created by the smoke patient-document metadata check."
    $metadataDocumentUpdatedNotes = "Updated by the smoke patient-document metadata check."
    $createMetadataDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $metadataDocumentName
        docDate = "2026-06-18"
        encounter = 1000013
        content = $metadataDocumentBody
        notes = "Created by the smoke patient-document metadata check."
    } | ConvertTo-Json
    $createdMetadataDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createMetadataDocumentBody -TimeoutSec 20
    $patientDocumentMetadataMutationId = $createdMetadataDocument.id
    $createdMetadataVisible = $createdMetadataDocument.detail.documents | Where-Object { $_.name -eq $metadataDocumentName -and $_.categoryName -eq "Medical Record" } | Select-Object -First 1

    $updateMetadataDocumentBody = @{
        categoryId = 6
        name = $metadataDocumentUpdatedName
        docDate = "2026-06-19"
        encounter = 1000014
        notes = $metadataDocumentUpdatedNotes
    } | ConvertTo-Json
    $updatedMetadataDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId/metadata" -Method Put -ContentType "application/json" -Body $updateMetadataDocumentBody -TimeoutSec 20
    $updatedMetadataVisible = $updatedMetadataDocument.detail.documents | Where-Object { $_.name -eq $metadataDocumentUpdatedName -and $_.categoryName -eq "Advance Directive" -and $_.docDate -eq "2026-06-19" -and $_.encounter -eq 1000014 -and $_.notes -eq $metadataDocumentUpdatedNotes } | Select-Object -First 1
    $metadataContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId/content" -Method Get -TimeoutSec 20

    $archivedMetadataDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId/soft-delete" -Method Put -TimeoutSec 20
    $archivedMetadataVisible = $archivedMetadataDocument.detail.documents | Where-Object { $_.name -eq $metadataDocumentUpdatedName } | Select-Object -First 1
    $patientDocumentMetadataPassed = $null -ne $createdMetadataVisible `
        -and $null -ne $updatedMetadataVisible `
        -and $metadataContent.name -eq $metadataDocumentUpdatedName `
        -and $metadataContent.categoryName -eq "Advance Directive" `
        -and $metadataContent.docDate -eq "2026-06-19" `
        -and $metadataContent.encounter -eq 1000014 `
        -and $metadataContent.notes -eq $metadataDocumentUpdatedNotes `
        -and $metadataContent.content.Contains($metadataDocumentBody) `
        -and $null -eq $archivedMetadataVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientDocumentMetadataMutationId = $null

    Add-Check -Name "patient document metadata lifecycle" -Result $(if ($patientDocumentMetadataPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdMetadataDocument.id
        createdVisible = $createdMetadataVisible
        updatedVisible = $updatedMetadataVisible
        content = $metadataContent
        archivedVisible = $archivedMetadataVisible
    }
}
catch {
    Add-Check -Name "patient document metadata lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientDocumentMetadataMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientDocumentContentReplaceId = $null
try {
    $replaceDocumentName = "Smoke Replace Content Patient Document"
    $replaceDocumentOriginalBody = "Created by the smoke patient-document content replacement check."
    $replaceDocumentUpdatedBody = "Updated by the smoke patient-document content replacement check."
    $replaceDocumentFileName = "$replaceDocumentName.txt"
    $createReplaceDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $replaceDocumentName
        docDate = "2026-06-19"
        encounter = 1000013
        content = $replaceDocumentOriginalBody
        notes = "Created by the smoke patient-document content replacement check."
    } | ConvertTo-Json
    $createdReplaceDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createReplaceDocumentBody -TimeoutSec 20
    $patientDocumentContentReplaceId = $createdReplaceDocument.id
    $createdReplaceVisible = $createdReplaceDocument.detail.documents | Where-Object { $_.name -eq $replaceDocumentName -and $_.contentPreview -and $_.contentPreview.Contains($replaceDocumentOriginalBody) } | Select-Object -First 1
    $createdReplaceContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/content" -Method Get -TimeoutSec 20
    Start-Sleep -Seconds 1

    $replaceContentBody = @{
        fileName = $replaceDocumentFileName
        content = $replaceDocumentUpdatedBody
    } | ConvertTo-Json
    $replacedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/content" -Method Put -ContentType "application/json" -Body $replaceContentBody -TimeoutSec 20
    $replacedVisible = $replacedDocument.detail.documents | Where-Object { $_.name -eq $replaceDocumentName -and $_.fileName -eq $replaceDocumentFileName -and $_.contentPreview -and $_.contentPreview.Contains($replaceDocumentUpdatedBody) } | Select-Object -First 1
    $replacedContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/content" -Method Get -TimeoutSec 20
    $patientDocumentReplacementRevisionPassed = $null -ne $createdReplaceContent `
        -and $null -ne $replacedContent `
        -and $createdReplaceContent.currentVersion -eq 1 `
        -and $replacedContent.currentVersion -eq 1 `
        -and $replacedContent.versionLabel -eq "Version 1" `
        -and $replacedContent.versionStatus -eq "Current version" `
        -and $replacedContent.versionHistoryCount -eq 1 `
        -and -not $replacedContent.hasPriorVersions `
        -and $createdReplaceContent.revisionHash -eq $createdReplaceContent.hash `
        -and $replacedContent.revisionHash -eq $replacedContent.hash `
        -and $replacedContent.hash -ne $createdReplaceContent.hash `
        -and $replacedContent.revisionAt -ne $createdReplaceContent.revisionAt `
        -and $replacedContent.content.Contains($replaceDocumentUpdatedBody) `
        -and -not $replacedContent.content.Contains($replaceDocumentOriginalBody)

    $replaceDownloadClient = [System.Net.Http.HttpClient]::new()
    try {
        $replaceDownload = $replaceDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/download").GetAwaiter().GetResult()
        $replaceDownloadBody = $replaceDownload.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $replaceDownloadContentType = $replaceDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $replaceDownloadClient.Dispose()
    }

    $archivedReplacedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/soft-delete" -Method Put -TimeoutSec 20
    $archivedReplacedVisible = $archivedReplacedDocument.detail.documents | Where-Object { $_.name -eq $replaceDocumentName } | Select-Object -First 1
    $patientDocumentContentReplacePassed = $null -ne $createdReplaceVisible `
        -and $null -ne $replacedVisible `
        -and $replacedContent.name -eq $replaceDocumentName `
        -and $replacedContent.fileName -eq $replaceDocumentFileName `
        -and $replacedContent.mimetype -eq "text/plain" `
        -and -not $replacedContent.isBinary `
        -and $replacedContent.content.Contains($replaceDocumentUpdatedBody) `
        -and -not $replacedContent.content.Contains($replaceDocumentOriginalBody) `
        -and $replaceDownload.IsSuccessStatusCode `
        -and $replaceDownloadContentType -eq "text/plain" `
        -and $replaceDownloadBody.Contains($replaceDocumentUpdatedBody) `
        -and $null -eq $archivedReplacedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientDocumentContentReplaceId = $null

    Add-Check -Name "patient document content replacement lifecycle" -Result $(if ($patientDocumentContentReplacePassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdReplaceDocument.id
        createdVisible = $createdReplaceVisible
        replacedVisible = $replacedVisible
        content = $replacedContent
        downloadStatus = [int]$replaceDownload.StatusCode
        downloadContentType = $replaceDownloadContentType
        archivedVisible = $archivedReplacedVisible
    }
    Add-Check -Name "patient document replacement revision lifecycle" -Result $(if ($patientDocumentReplacementRevisionPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdReplaceDocument.id
        createdRevisionAt = $createdReplaceContent.revisionAt
        createdRevisionHash = $createdReplaceContent.revisionHash
        replacedRevisionAt = $replacedContent.revisionAt
        replacedRevisionHash = $replacedContent.revisionHash
        replacedVersionLabel = $replacedContent.versionLabel
        replacedVersionHistoryCount = $replacedContent.versionHistoryCount
        replacedHasPriorVersions = $replacedContent.hasPriorVersions
    }
}
catch {
    Add-Check -Name "patient document content replacement lifecycle" -Result "failed" -Details $_.Exception.Message
    Add-Check -Name "patient document replacement revision lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientDocumentContentReplaceId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientDocumentArchiveRestoreId = $null
try {
    $archiveDocumentName = "Smoke Archive Restore Patient Document"
    $archiveDocumentBody = "Created by the smoke patient-document archive restore check."
    $createArchiveDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $archiveDocumentName
        docDate = "2026-06-19"
        encounter = 1000013
        content = $archiveDocumentBody
        notes = "Created by the smoke patient-document archive restore check."
    } | ConvertTo-Json
    $createdArchiveDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createArchiveDocumentBody -TimeoutSec 20
    $patientDocumentArchiveRestoreId = $createdArchiveDocument.id
    $createdArchiveVisible = $createdArchiveDocument.detail.documents | Where-Object { $_.name -eq $archiveDocumentName -and $_.deleted -eq 0 } | Select-Object -First 1

    $archivedArchiveDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId/soft-delete" -Method Put -TimeoutSec 20
    $archivedDefaultVisible = $archivedArchiveDocument.detail.documents | Where-Object { $_.name -eq $archiveDocumentName } | Select-Object -First 1
    $archivedList = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001?includeArchived=true" -Method Get -TimeoutSec 20
    $archivedIncludeVisible = $archivedList.documents | Where-Object { $_.name -eq $archiveDocumentName -and $_.deleted -eq 1 } | Select-Object -First 1

    $archiveContentClient = [System.Net.Http.HttpClient]::new()
    try {
        $archivedContent = $archiveContentClient.GetAsync("$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId/content").GetAwaiter().GetResult()
        $archivedContentStatus = [int]$archivedContent.StatusCode
    }
    finally {
        $archiveContentClient.Dispose()
    }

    $restoredArchiveDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId/restore" -Method Put -TimeoutSec 20
    $restoredArchiveVisible = $restoredArchiveDocument.detail.documents | Where-Object { $_.name -eq $archiveDocumentName -and $_.deleted -eq 0 } | Select-Object -First 1
    $restoredArchiveContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId/content" -Method Get -TimeoutSec 20

    $patientDocumentArchiveRestorePassed = $null -ne $createdArchiveVisible `
        -and $null -eq $archivedDefaultVisible `
        -and $null -ne $archivedIncludeVisible `
        -and $archivedContentStatus -eq 404 `
        -and $null -ne $restoredArchiveVisible `
        -and $restoredArchiveContent.content.Contains($archiveDocumentBody)

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientDocumentArchiveRestoreId = $null

    Add-Check -Name "patient document archive restore lifecycle" -Result $(if ($patientDocumentArchiveRestorePassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdArchiveDocument.id
        createdVisible = $createdArchiveVisible
        archivedDefaultVisible = $archivedDefaultVisible
        archivedIncludeVisible = $archivedIncludeVisible
        archivedContentStatus = $archivedContentStatus
        restoredVisible = $restoredArchiveVisible
    }
}
catch {
    Add-Check -Name "patient document archive restore lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientDocumentArchiveRestoreId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientBinaryDocumentMutationId = $null
try {
    $binaryDocumentName = "Smoke Binary Patient Document.pdf"
    $binaryDocumentBody = "%PDF-1.4`n1 0 obj`n<< /Type /Catalog >>`nendobj`n% Smoke binary patient document check.`n%%EOF`n"
    $binaryDocumentBase64 = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($binaryDocumentBody))
    $createBinaryDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $binaryDocumentName
        docDate = "2026-06-18"
        encounter = 1000013
        fileName = $binaryDocumentName
        mimetype = "application/pdf"
        contentBase64 = $binaryDocumentBase64
        notes = "Created by the smoke binary patient-document mutation check."
    } | ConvertTo-Json
    $createdBinaryDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/binary" -Method Post -ContentType "application/json" -Body $createBinaryDocumentBody -TimeoutSec 20
    $patientBinaryDocumentMutationId = $createdBinaryDocument.id
    $createdBinaryVisible = $createdBinaryDocument.detail.documents | Where-Object { $_.name -eq $binaryDocumentName -and $_.mimetype -eq "application/pdf" -and $_.contentPreview -and $_.contentPreview.Contains("Binary document") } | Select-Object -First 1

    $binaryContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId/content" -Method Get -TimeoutSec 20
    $binaryDownloadClient = [System.Net.Http.HttpClient]::new()
    try {
        $binaryDownload = $binaryDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId/download").GetAwaiter().GetResult()
        $binaryDownloadBytes = $binaryDownload.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $binaryDownloadContentType = $binaryDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $binaryDownloadClient.Dispose()
    }

    $archivedBinaryDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId/soft-delete" -Method Put -TimeoutSec 20
    $archivedBinaryVisible = $archivedBinaryDocument.detail.documents | Where-Object { $_.name -eq $binaryDocumentName } | Select-Object -First 1
    $patientBinaryDocumentMutationPassed = $null -ne $createdBinaryVisible `
        -and $binaryContent.name -eq $binaryDocumentName `
        -and $binaryContent.fileName -eq $binaryDocumentName `
        -and $binaryContent.mimetype -eq "application/pdf" `
        -and $binaryContent.isBinary `
        -and $binaryContent.contentBase64 -eq $binaryDocumentBase64 `
        -and $binaryDownload.IsSuccessStatusCode `
        -and $binaryDownloadContentType -eq "application/pdf" `
        -and [Convert]::ToBase64String($binaryDownloadBytes) -eq $binaryDocumentBase64 `
        -and $null -eq $archivedBinaryVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientBinaryDocumentMutationId = $null

    Add-Check -Name "patient binary document mutation lifecycle" -Result $(if ($patientBinaryDocumentMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdBinaryDocument.id
        createdVisible = $createdBinaryVisible
        contentIsBinary = $binaryContent.isBinary
        downloadStatus = [int]$binaryDownload.StatusCode
        downloadContentType = $binaryDownloadContentType
        archivedVisible = $archivedBinaryVisible
    }
}
catch {
    Add-Check -Name "patient binary document mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientBinaryDocumentMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientExternalLinkDocumentMutationId = $null
try {
    $externalLinkDocumentName = "Smoke External Link Patient Document"
    $externalLinkUrl = "https://example.test/legacy-ehr/smoke-external-record"
    $createExternalLinkDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $externalLinkDocumentName
        docDate = "2026-06-18"
        encounter = 1000013
        url = $externalLinkUrl
        notes = "Created by the smoke external-link patient-document mutation check."
    } | ConvertTo-Json
    $createdExternalLinkDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/external-link" -Method Post -ContentType "application/json" -Body $createExternalLinkDocumentBody -TimeoutSec 20
    $patientExternalLinkDocumentMutationId = $createdExternalLinkDocument.id
    $createdExternalLinkVisible = $createdExternalLinkDocument.detail.documents | Where-Object { $_.name -eq $externalLinkDocumentName -and $_.mimetype -eq "text/uri-list" -and $_.storageMethod -eq "web_url" -and $_.url -eq $externalLinkUrl } | Select-Object -First 1

    $externalLinkContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientExternalLinkDocumentMutationId/content" -Method Get -TimeoutSec 20
    $archivedExternalLinkDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientExternalLinkDocumentMutationId/soft-delete" -Method Put -TimeoutSec 20
    $archivedExternalLinkVisible = $archivedExternalLinkDocument.detail.documents | Where-Object { $_.name -eq $externalLinkDocumentName } | Select-Object -First 1
    $patientExternalLinkDocumentMutationPassed = $null -ne $createdExternalLinkVisible `
        -and $externalLinkContent.name -eq $externalLinkDocumentName `
        -and $externalLinkContent.storageMethod -eq "web_url" `
        -and $externalLinkContent.url -eq $externalLinkUrl `
        -and $externalLinkContent.content.Contains($externalLinkUrl) `
        -and $null -eq $archivedExternalLinkVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientExternalLinkDocumentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientExternalLinkDocumentMutationId = $null

    Add-Check -Name "patient external-link document mutation lifecycle" -Result $(if ($patientExternalLinkDocumentMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdExternalLinkDocument.id
        createdVisible = $createdExternalLinkVisible
        contentStorage = $externalLinkContent.storageMethod
        contentUrl = $externalLinkContent.url
        archivedVisible = $archivedExternalLinkVisible
    }
}
catch {
    Add-Check -Name "patient external-link document mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientExternalLinkDocumentMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientExternalLinkDocumentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientMessageMutationId = $null
try {
    $messageTitle = "Smoke Patient Message Mutation"
    $createMessageBody = @{
        patientId = "MOD-PAT-0004"
        title = $messageTitle
        body = "Created by the smoke patient-message mutation check."
        assignedTo = "admin"
    } | ConvertTo-Json
    $createdMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -ContentType "application/json" -Body $createMessageBody -TimeoutSec 20
    $patientMessageMutationId = $createdMessage.id
    $createdVisible = $createdMessage.detail.messages | Where-Object { $_.title -eq $messageTitle -and $_.status -eq "New" -and $_.assignedTo -eq "admin" } | Select-Object -First 1

    $editedMessageTitle = "Smoke Patient Message Edited"
    $editedMessageBody = "Edited by the smoke patient-message content check."
    $contentBody = @{
        title = $editedMessageTitle
        body = $editedMessageBody
    } | ConvertTo-Json
    $editedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/content" -Method Put -ContentType "application/json" -Body $contentBody -TimeoutSec 20
    $editedVisible = $editedMessage.detail.messages | Where-Object { $_.title -eq $editedMessageTitle -and $_.body -eq $editedMessageBody -and $_.status -eq "New" -and $_.assignedTo -eq "admin" } | Select-Object -First 1
    Add-Check -Name "patient message content update" -Result $(if ($null -ne $editedVisible) { "passed" } else { "failed" }) -Details @{
        messageId = $patientMessageMutationId
        editedVisible = $editedVisible
    }

    $assignmentBody = @{
        assignedTo = "billing"
    } | ConvertTo-Json
    $assignedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/assignment" -Method Put -ContentType "application/json" -Body $assignmentBody -TimeoutSec 20
    $assignedVisible = $assignedMessage.detail.messages | Where-Object { $_.title -eq $editedMessageTitle -and $_.status -eq "New" -and $_.assignedTo -eq "billing" } | Select-Object -First 1
    Add-Check -Name "patient message assignment update" -Result $(if ($null -ne $assignedVisible) { "passed" } else { "failed" }) -Details @{
        messageId = $patientMessageMutationId
        assignedVisible = $assignedVisible
    }

    $closeBody = @{
        status = "Done"
        body = "Closed by the smoke patient-message mutation check."
    } | ConvertTo-Json
    $closedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/status" -Method Put -ContentType "application/json" -Body $closeBody -TimeoutSec 20
    $closedVisible = $closedMessage.detail.messages | Where-Object { $_.title -eq $editedMessageTitle -and $_.status -eq "Done" -and $_.body -eq "Closed by the smoke patient-message mutation check." } | Select-Object -First 1

    $archivedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/soft-delete" -Method Put -TimeoutSec 20
    $archivedVisible = $archivedMessage.detail.messages | Where-Object { $_.title -eq $editedMessageTitle } | Select-Object -First 1
    $patientMessageMutationPassed = $null -ne $createdVisible -and $null -ne $editedVisible -and $null -ne $assignedVisible -and $null -ne $closedVisible -and $null -eq $archivedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientMessageMutationId = $null

    Add-Check -Name "patient message mutation lifecycle" -Result $(if ($patientMessageMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdMessage.id
        createdVisible = $createdVisible
        editedVisible = $editedVisible
        assignedVisible = $assignedVisible
        closedVisible = $closedVisible
        archivedVisible = $archivedVisible
    }
}
catch {
    Add-Check -Name "patient message mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientMessageMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $procedures = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0009" -Method Get -TimeoutSec 20
    $completedOrder = $procedures.orders | Where-Object { $_.name -eq "Complete blood count" -and $_.orderStatus -eq "complete" } | Select-Object -First 1
    $completedReport = $completedOrder.reports | Where-Object { $_.status -eq "complete" } | Select-Object -First 1
    $hemoglobin = $completedReport.results | Where-Object { $_.text -eq "Hemoglobin" -and $_.resultStatus -eq "final" } | Select-Object -First 1
    $proceduresPassed = $procedures.patientId -eq "MOD-PAT-0009" -and $null -ne $completedOrder -and $null -ne $completedReport -and $null -ne $hemoglobin
    Add-Check -Name "anchor procedure results" -Result $(if ($proceduresPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $procedures.patientId
        orderCount = $procedures.orders.Count
        completedOrder = $completedOrder
        completedReport = $completedReport
        hemoglobin = $hemoglobin
    }
}
catch {
    Add-Check -Name "anchor procedure results" -Result "failed" -Details $_.Exception.Message
}

try {
    $scheduledProcedures = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0701" -Method Get -TimeoutSec 20
    $scheduledOrder = $scheduledProcedures.orders | Where-Object {
        $_.name -eq "Complete blood count" `
            -and $_.code -eq "85025" `
            -and $_.orderStatus -eq "scheduled" `
            -and $_.orderDate -eq "2026-06-25" `
            -and $_.reports.Count -eq 0
    } | Select-Object -First 1
    $scheduledProceduresPassed = $scheduledProcedures.patientId -eq "MOD-PAT-0701" `
        -and $null -ne $scheduledOrder `
        -and $scheduledProcedures.counts.scheduledOrders -ge 1 `
        -and $scheduledProcedures.counts.futureScheduledOrders -ge 1 `
        -and $scheduledProcedures.counts.reportlessOrders -ge 1
    Add-Check -Name "anchor scheduled procedure orders" -Result $(if ($scheduledProceduresPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $scheduledProcedures.patientId
        counts = $scheduledProcedures.counts
        scheduledOrder = $scheduledOrder
    }
}
catch {
    Add-Check -Name "anchor scheduled procedure orders" -Result "failed" -Details $_.Exception.Message
}

$procedureOrderMutationId = $null
try {
    if ($null -eq $completedOrder -or $null -eq $completedOrder.encounter) {
        throw "Anchor procedure order did not provide an encounter for the procedure mutation check."
    }

    $procedureName = "Smoke Procedure Mutation"
    $procedureResultText = "Smoke Procedure Mutation Result"
    $createProcedureBody = @{
        patientId = "MOD-PAT-0009"
        providerId = $null
        encounterId = $completedOrder.encounter
        dateOrdered = "2026-06-18"
        priority = "routine"
        status = "pending"
        procedureCode = "80053"
        procedureName = $procedureName
        procedureType = "laboratory"
        diagnosis = "Z00.00"
        instructions = "Created by the smoke procedure mutation check."
    } | ConvertTo-Json
    $createdProcedureOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders" -Method Post -ContentType "application/json" -Body $createProcedureBody -TimeoutSec 20
    $procedureOrderMutationId = $createdProcedureOrder.id
    $createdProcedureVisible = $createdProcedureOrder.detail.orders | Where-Object { $_.id -eq $procedureOrderMutationId -and $_.name -eq $procedureName -and $_.orderStatus -eq "pending" } | Select-Object -First 1

    $completeProcedureBody = @{
        status = "complete"
    } | ConvertTo-Json
    $completedProcedureOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$procedureOrderMutationId/status" -Method Put -ContentType "application/json" -Body $completeProcedureBody -TimeoutSec 20
    $completedProcedureVisible = $completedProcedureOrder.detail.orders | Where-Object { $_.id -eq $procedureOrderMutationId -and $_.orderStatus -eq "complete" } | Select-Object -First 1

    $createProcedureReportBody = @{
        orderId = $procedureOrderMutationId
        dateCollected = "2026-06-18 12:30:00"
        dateReport = "2026-06-18 13:00:00"
        specimenNumber = "SMOKE-PROC"
        reportStatus = "final"
        reviewStatus = "reviewed"
        notes = "Smoke procedure report."
    } | ConvertTo-Json
    $createdProcedureReport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/reports" -Method Post -ContentType "application/json" -Body $createProcedureReportBody -TimeoutSec 20
    $procedureReportId = $createdProcedureReport.id

    $createProcedureResultBody = @{
        reportId = $procedureReportId
        resultCode = "2345-7"
        resultText = $procedureResultText
        dateTime = "2026-06-18 13:05:00"
        facility = "Modernization Family Medicine"
        units = "mg/dL"
        result = "104"
        range = "70-99"
        abnormal = "high"
        comments = "Created by the smoke procedure mutation check."
        status = "final"
    } | ConvertTo-Json
    $createdProcedureResult = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/results" -Method Post -ContentType "application/json" -Body $createProcedureResultBody -TimeoutSec 20
    $procedureResultId = $createdProcedureResult.id
    $resultOrder = $createdProcedureResult.detail.orders | Where-Object { $_.id -eq $procedureOrderMutationId } | Select-Object -First 1
    $resultReport = $resultOrder.reports | Where-Object { $_.id -eq $procedureReportId -and $_.reviewStatus -eq "reviewed" } | Select-Object -First 1
    $createdResultVisible = $resultReport.results | Where-Object { $_.id -eq $procedureResultId -and $_.text -eq $procedureResultText -and $_.result -eq "104" -and $_.resultStatus -eq "final" } | Select-Object -First 1
    $procedureMutationPassed = $null -ne $createdProcedureVisible -and $null -ne $completedProcedureVisible -and $null -ne $resultReport -and $null -ne $createdResultVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$procedureOrderMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $procedureOrderMutationId = $null

    Add-Check -Name "procedure mutation lifecycle" -Result $(if ($procedureMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdProcedureOrder.id
        createdVisible = $createdProcedureVisible
        completedVisible = $completedProcedureVisible
        reportId = $procedureReportId
        resultVisible = $createdResultVisible
    }
}
catch {
    Add-Check -Name "procedure mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $procedureOrderMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$procedureOrderMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $billing = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0001" -Method Get -TimeoutSec 20
    $latestEncounter = $billing.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $officeVisit = $latestEncounter.lines | Where-Object { $_.code -eq "99214" -and $_.codeText -eq "Established patient office visit" } | Select-Object -First 1
    $venipuncture = $latestEncounter.lines | Where-Object { $_.code -eq "36415" -and $_.codeText -eq "Routine venipuncture" } | Select-Object -First 1
    $billingPassed = $billing.patientId -eq "MOD-PAT-0001" -and $null -ne $latestEncounter -and $null -ne $officeVisit -and $null -ne $venipuncture
    Add-Check -Name "anchor fee sheet billing" -Result $(if ($billingPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $billing.patientId
        encounterCount = $billing.encounters.Count
        latestEncounter = $latestEncounter
        officeVisit = $officeVisit
        venipuncture = $venipuncture
    }
}
catch {
    Add-Check -Name "anchor fee sheet billing" -Result "failed" -Details $_.Exception.Message
}

try {
    $claimBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $claimRows = @($claimBilling.encounters | ForEach-Object { $_.claims } | Where-Object { $null -ne $_ })
    $queuedClaim = $claimRows | Where-Object { $_.statusLabel -eq "Queued for billing" -and $_.billProcess -eq 1 } | Select-Object -First 1
    $generatedClaim = $claimRows | Where-Object { $_.statusLabel -eq "Marked as cleared" -and $_.processFile -like "CLAIM-*-837P.txt" } | Select-Object -First 1
    $clearedClaim = $claimRows | Where-Object { $_.statusLabel -eq "Marked as cleared" -and [string]::IsNullOrWhiteSpace($_.processFile) } | Select-Object -First 1
    $claimStatusPassed = $claimBilling.patientId -eq "MOD-PAT-0005" `
        -and $claimRows.Count -ge 3 `
        -and $null -ne $queuedClaim `
        -and $null -ne $generatedClaim `
        -and $null -ne $clearedClaim
    Add-Check -Name "anchor claim status summary" -Result $(if ($claimStatusPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $claimBilling.patientId
        claimCount = $claimRows.Count
        queuedClaim = $queuedClaim
        generatedClaim = $generatedClaim
        clearedClaim = $clearedClaim
    }
}
catch {
    Add-Check -Name "anchor claim status summary" -Result "failed" -Details $_.Exception.Message
}

$claimStatusMutationId = $null
try {
    $beforeClaimMutationBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $beforeClaimRows = @($beforeClaimMutationBilling.encounters | ForEach-Object { $_.claims } | Where-Object { $null -ne $_ })
    $claimStatusSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $claimStatusProcessFile = "CLAIM-1000052-SMOKE-$claimStatusSuffix-837P.txt"
    $createClaimStatusBody = @{
        patientId = "MOD-PAT-0005"
        encounter = 1000052
        payerId = 9005
        payerName = "Northstar HMO"
        payerType = 1
        status = 1
        billProcess = 1
        billTime = "2026-06-18 12:15:00"
        processTime = $null
        processFile = ""
        target = "HCFA"
        x12PartnerId = 0
        submittedClaim = "Smoke claim status mutation $claimStatusSuffix"
    } | ConvertTo-Json
    $createdClaimStatus = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims" -Method Post -ContentType "application/json" -Body $createClaimStatusBody -TimeoutSec 20
    $claimStatusMutationId = $createdClaimStatus.id
    $createdClaimRows = @($createdClaimStatus.detail.encounters | ForEach-Object { $_.claims } | Where-Object { $null -ne $_ })
    $createdClaimVisible = $createdClaimRows | Where-Object { $_.id -eq $claimStatusMutationId -and $_.statusLabel -eq "Queued for billing" -and $_.billProcess -eq 1 -and $_.target -eq "HCFA" } | Select-Object -First 1

    $generateClaimStatusBody = @{
        status = 2
        billProcess = 0
        processTime = "2026-06-18 14:15:00"
        processFile = $claimStatusProcessFile
        target = "X12"
        x12PartnerId = 1
        submittedClaim = "Generated smoke claim status mutation $claimStatusSuffix"
    } | ConvertTo-Json
    $generatedClaimStatus = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims/$claimStatusMutationId/status" -Method Put -ContentType "application/json" -Body $generateClaimStatusBody -TimeoutSec 20
    $generatedClaimRows = @($generatedClaimStatus.detail.encounters | ForEach-Object { $_.claims } | Where-Object { $null -ne $_ })
    $generatedClaimVisible = $generatedClaimRows | Where-Object { $_.id -eq $claimStatusMutationId -and $_.statusLabel -eq "Marked as cleared" -and $_.processFile -eq $claimStatusProcessFile -and $_.target -eq "X12" } | Select-Object -First 1

    $clearClaimStatusBody = @{
        status = 3
        billProcess = 0
        processTime = $null
        processFile = ""
        target = "HCFA"
        x12PartnerId = 0
        submittedClaim = "Cleared smoke claim status mutation $claimStatusSuffix"
    } | ConvertTo-Json
    $clearedClaimStatus = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims/$claimStatusMutationId/status" -Method Put -ContentType "application/json" -Body $clearClaimStatusBody -TimeoutSec 20
    $clearedClaimRows = @($clearedClaimStatus.detail.encounters | ForEach-Object { $_.claims } | Where-Object { $null -ne $_ })
    $clearedClaimVisible = $clearedClaimRows | Where-Object { $_.id -eq $claimStatusMutationId -and $_.statusLabel -eq "Marked as cleared" -and [string]::IsNullOrWhiteSpace($_.processFile) -and $_.target -eq "HCFA" } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims/$claimStatusMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $claimStatusMutationId = $null
    $afterClaimMutationBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $afterClaimRows = @($afterClaimMutationBilling.encounters | ForEach-Object { $_.claims } | Where-Object { $null -ne $_ })

    $claimStatusMutationPassed = $null -ne $createdClaimVisible `
        -and $createdClaimRows.Count -eq ($beforeClaimRows.Count + 1) `
        -and $createdClaimVisible.version -gt 1 `
        -and $null -ne $generatedClaimVisible `
        -and $null -ne $clearedClaimVisible `
        -and $afterClaimRows.Count -eq $beforeClaimRows.Count
    Add-Check -Name "claim status mutation lifecycle" -Result $(if ($claimStatusMutationPassed) { "passed" } else { "failed" }) -Details @{
        claimId = $createdClaimStatus.id
        processFile = $claimStatusProcessFile
        createdClaim = $createdClaimVisible
        generatedClaim = $generatedClaimVisible
        clearedClaim = $clearedClaimVisible
    }
}
catch {
    Add-Check -Name "claim status mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $claimStatusMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims/$claimStatusMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $paymentBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $paymentRows = @($paymentBilling.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $anchorPayment = $paymentRows | Where-Object { $_.reference -eq "EOB-NSTAR-1000052" -and $_.payAmount -eq 126 } | Select-Object -First 1
    $anchorAdjustment = $paymentRows | Where-Object { $_.reference -eq "EOB-NSTAR-1000052" -and $_.adjustmentAmount -eq 42 -and $_.reasonCode -eq "CO-45" } | Select-Object -First 1
    $paymentPostingPassed = $paymentBilling.patientId -eq "MOD-PAT-0005" `
        -and $paymentRows.Count -ge 2 `
        -and $null -ne $anchorPayment `
        -and $null -ne $anchorAdjustment `
        -and $anchorPayment.payerName -eq "Northstar HMO"
    Add-Check -Name "anchor payment posting summary" -Result $(if ($paymentPostingPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $paymentBilling.patientId
        paymentCount = $paymentRows.Count
        anchorPayment = $anchorPayment
        anchorAdjustment = $anchorAdjustment
    }
}
catch {
    Add-Check -Name "anchor payment posting summary" -Result "failed" -Details $_.Exception.Message
}

$paymentPostingMutationId = $null
try {
    $beforePaymentMutationBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $beforePaymentMutationSummary = $beforePaymentMutationBilling.accountSummary
    $paymentPostingReference = "EOB-SMOKE-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
    $paymentPostingClaim = "NSTAR-CLM-SMOKE-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
    $createPaymentPostingBody = @{
        patientId = "MOD-PAT-0005"
        encounter = 1000052
        payerId = 9005
        payerName = "Northstar HMO"
        payerType = 1
        reference = $paymentPostingReference
        postDate = "2026-06-18"
        checkDate = "2026-06-18"
        depositDate = "2026-06-18"
        paymentType = "insurance_payment"
        paymentMethod = "check_payment"
        codeType = "CPT4"
        code = "99214"
        memo = "Smoke payment posting"
        payAmount = 21.00
        adjustmentAmount = 3.50
        accountCode = "CO45"
        reasonCode = "CO-45"
        payerClaimNumber = $paymentPostingClaim
    } | ConvertTo-Json
    $createdPaymentPosting = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments" -Method Post -ContentType "application/json" -Body $createPaymentPostingBody -TimeoutSec 20
    $paymentPostingMutationId = $createdPaymentPosting.id
    $createdPaymentRows = @($createdPaymentPosting.detail.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $createdPaymentVisible = $createdPaymentRows | Where-Object { $_.activityId -eq $paymentPostingMutationId -and $_.reference -eq $paymentPostingReference -and $_.payAmount -eq 21 -and $_.adjustmentAmount -eq 3.5 -and $_.reasonCode -eq "CO-45" } | Select-Object -First 1
    $createdPaymentSummary = $createdPaymentPosting.detail.accountSummary

    $voidedPaymentPosting = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$paymentPostingMutationId/void" -Method Put -TimeoutSec 20
    $voidedPaymentRows = @($voidedPaymentPosting.detail.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $voidedPaymentVisible = $voidedPaymentRows | Where-Object { $_.activityId -eq $paymentPostingMutationId } | Select-Object -First 1
    $voidedPaymentSummary = $voidedPaymentPosting.detail.accountSummary

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$paymentPostingMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $paymentPostingMutationId = $null

    $paymentPostingMutationPassed = $null -ne $createdPaymentVisible `
        -and [decimal]$createdPaymentSummary.paymentAmount -eq ([decimal]$beforePaymentMutationSummary.paymentAmount + 21) `
        -and [decimal]$createdPaymentSummary.adjustmentAmount -eq ([decimal]$beforePaymentMutationSummary.adjustmentAmount + 3.5) `
        -and [decimal]$createdPaymentSummary.balanceAmount -eq ([decimal]$beforePaymentMutationSummary.balanceAmount - 24.5) `
        -and $null -eq $voidedPaymentVisible `
        -and [decimal]$voidedPaymentSummary.paymentAmount -eq [decimal]$beforePaymentMutationSummary.paymentAmount `
        -and [decimal]$voidedPaymentSummary.adjustmentAmount -eq [decimal]$beforePaymentMutationSummary.adjustmentAmount `
        -and [decimal]$voidedPaymentSummary.balanceAmount -eq [decimal]$beforePaymentMutationSummary.balanceAmount
    Add-Check -Name "payment posting mutation lifecycle" -Result $(if ($paymentPostingMutationPassed) { "passed" } else { "failed" }) -Details @{
        paymentId = $createdPaymentPosting.id
        reference = $paymentPostingReference
        createdPayment = $createdPaymentVisible
        createdSummary = $createdPaymentSummary
        voidedSummary = $voidedPaymentSummary
    }
}
catch {
    Add-Check -Name "payment posting mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $paymentPostingMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$paymentPostingMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientPaymentMutationId = $null
try {
    $beforePatientPaymentBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $beforePatientPaymentSummary = $beforePatientPaymentBilling.accountSummary
    $patientPaymentReference = "RCPT-SMOKE-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
    $createPatientPaymentBody = @{
        patientId = "MOD-PAT-0005"
        encounter = 1000052
        payerId = 0
        payerName = ""
        payerType = 0
        reference = $patientPaymentReference
        postDate = "2026-06-18"
        checkDate = "2026-06-18"
        depositDate = "2026-06-18"
        paymentType = "patient_payment"
        paymentMethod = "credit_card"
        codeType = "CPT4"
        code = "99214"
        memo = "Smoke patient payment"
        payAmount = 35.00
        adjustmentAmount = 0.00
        accountCode = ""
        reasonCode = ""
        payerClaimNumber = ""
    } | ConvertTo-Json
    $createdPatientPayment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments" -Method Post -ContentType "application/json" -Body $createPatientPaymentBody -TimeoutSec 20
    $patientPaymentMutationId = $createdPatientPayment.id
    $createdPatientPaymentRows = @($createdPatientPayment.detail.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $createdPatientPaymentVisible = $createdPatientPaymentRows | Where-Object { $_.activityId -eq $patientPaymentMutationId -and $_.payerType -eq 0 -and $_.reference -eq $patientPaymentReference -and $_.payAmount -eq 35 -and $_.adjustmentAmount -eq 0 } | Select-Object -First 1
    $createdPatientPaymentSummary = $createdPatientPayment.detail.accountSummary

    $voidedPatientPayment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$patientPaymentMutationId/void" -Method Put -TimeoutSec 20
    $voidedPatientPaymentRows = @($voidedPatientPayment.detail.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $voidedPatientPaymentVisible = $voidedPatientPaymentRows | Where-Object { $_.activityId -eq $patientPaymentMutationId } | Select-Object -First 1
    $voidedPatientPaymentSummary = $voidedPatientPayment.detail.accountSummary

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$patientPaymentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientPaymentMutationId = $null

    $patientPaymentMutationPassed = $null -ne $createdPatientPaymentVisible `
        -and [decimal]$createdPatientPaymentSummary.paymentAmount -eq ([decimal]$beforePatientPaymentSummary.paymentAmount + 35) `
        -and [decimal]$createdPatientPaymentSummary.adjustmentAmount -eq [decimal]$beforePatientPaymentSummary.adjustmentAmount `
        -and [decimal]$createdPatientPaymentSummary.balanceAmount -eq ([decimal]$beforePatientPaymentSummary.balanceAmount - 35) `
        -and $null -eq $voidedPatientPaymentVisible `
        -and [decimal]$voidedPatientPaymentSummary.paymentAmount -eq [decimal]$beforePatientPaymentSummary.paymentAmount `
        -and [decimal]$voidedPatientPaymentSummary.adjustmentAmount -eq [decimal]$beforePatientPaymentSummary.adjustmentAmount `
        -and [decimal]$voidedPatientPaymentSummary.balanceAmount -eq [decimal]$beforePatientPaymentSummary.balanceAmount
    Add-Check -Name "patient payment capture lifecycle" -Result $(if ($patientPaymentMutationPassed) { "passed" } else { "failed" }) -Details @{
        paymentId = $createdPatientPayment.id
        reference = $patientPaymentReference
        createdPayment = $createdPatientPaymentVisible
        createdSummary = $createdPatientPaymentSummary
        voidedSummary = $voidedPatientPaymentSummary
    }
}
catch {
    Add-Check -Name "patient payment capture lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientPaymentMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$patientPaymentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $balanceBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $balanceEncounter = $balanceBilling.encounters | Where-Object { $_.encounter -eq 1000052 } | Select-Object -First 1
    $balanceSummary = $balanceBilling.accountSummary
    $accountBalancePassed = $balanceBilling.patientId -eq "MOD-PAT-0005" `
        -and $null -ne $balanceSummary `
        -and $null -ne $balanceEncounter `
        -and [decimal]$balanceEncounter.totalFee -eq 186 `
        -and [decimal]$balanceEncounter.paymentAmount -eq 126 `
        -and [decimal]$balanceEncounter.adjustmentAmount -eq 42 `
        -and [decimal]$balanceEncounter.balanceAmount -eq 18 `
        -and [decimal]$balanceSummary.chargeAmount -eq 635 `
        -and [decimal]$balanceSummary.paymentAmount -eq 206 `
        -and [decimal]$balanceSummary.adjustmentAmount -eq 64.25 `
        -and [decimal]$balanceSummary.balanceAmount -eq 364.75
    Add-Check -Name "anchor account balance summary" -Result $(if ($accountBalancePassed) { "passed" } else { "failed" }) -Details @{
        patientId = $balanceBilling.patientId
        accountSummary = $balanceSummary
        balanceEncounter = $balanceEncounter
    }
}
catch {
    Add-Check -Name "anchor account balance summary" -Result "failed" -Details $_.Exception.Message
}

try {
    $agingBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $agingSummary = $agingBilling.agingSummary
    $currentEncounter = $agingBilling.encounters | Where-Object { $_.encounter -eq 1000053 } | Select-Object -First 1
    $days31To60Encounter = $agingBilling.encounters | Where-Object { $_.encounter -eq 1000052 } | Select-Object -First 1
    $over90Encounter = $agingBilling.encounters | Where-Object { $_.encounter -eq 1000051 } | Select-Object -First 1
    $accountAgingPassed = $agingBilling.patientId -eq "MOD-PAT-0005" `
        -and $null -ne $agingSummary `
        -and $agingSummary.asOfDate -eq "2026-06-18" `
        -and [decimal]$agingSummary.currentAmount -eq 83.75 `
        -and [decimal]$agingSummary.days31To60Amount -eq 18 `
        -and [decimal]$agingSummary.days61To90Amount -eq 0 `
        -and [decimal]$agingSummary.over90Amount -eq 263 `
        -and [decimal]$agingSummary.totalBalanceAmount -eq 364.75 `
        -and $null -ne $currentEncounter `
        -and $currentEncounter.agingBucket -eq "Current" `
        -and $currentEncounter.ageDays -eq 6 `
        -and $null -ne $days31To60Encounter `
        -and $days31To60Encounter.agingBucket -eq "31-60" `
        -and $days31To60Encounter.ageDays -eq 56 `
        -and $null -ne $over90Encounter `
        -and $over90Encounter.agingBucket -eq "Over 90" `
        -and $over90Encounter.ageDays -eq 361
    Add-Check -Name "anchor account aging summary" -Result $(if ($accountAgingPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $agingBilling.patientId
        agingSummary = $agingSummary
        currentEncounter = $currentEncounter
        days31To60Encounter = $days31To60Encounter
        over90Encounter = $over90Encounter
    }
}
catch {
    Add-Check -Name "anchor account aging summary" -Result "failed" -Details $_.Exception.Message
}

try {
    $ledgerBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $ledgerSummary = $ledgerBilling.ledgerSummary
    $ledgerEntries = @($ledgerBilling.ledgerEntries)
    $firstLedgerEntry = $ledgerEntries | Select-Object -First 1
    $lastLedgerEntry = $ledgerEntries | Select-Object -Last 1
    $anchorLedgerPayment = $ledgerEntries | Where-Object { $_.entryType -eq "Payment" -and $_.reference -eq "EOB-NSTAR-1000052" -and [decimal]$_.amount -eq -126 } | Select-Object -First 1
    $anchorLedgerAdjustment = $ledgerEntries | Where-Object { $_.entryType -eq "Adjustment" -and $_.reference -eq "EOB-NSTAR-1000052" -and [decimal]$_.amount -eq -42 } | Select-Object -First 1
    $accountLedgerPassed = $ledgerBilling.patientId -eq "MOD-PAT-0005" `
        -and $null -ne $ledgerSummary `
        -and $ledgerEntries.Count -eq 10 `
        -and $ledgerSummary.entryCount -eq 10 `
        -and $ledgerSummary.firstEntryDate -eq "2025-06-22" `
        -and $ledgerSummary.lastEntryDate -eq "2026-06-25" `
        -and [decimal]$ledgerSummary.chargeAmount -eq 635 `
        -and [decimal]$ledgerSummary.paymentAmount -eq 206 `
        -and [decimal]$ledgerSummary.adjustmentAmount -eq 64.25 `
        -and [decimal]$ledgerSummary.endingBalanceAmount -eq 364.75 `
        -and $null -ne $firstLedgerEntry `
        -and $firstLedgerEntry.entryDate -eq "2025-06-22" `
        -and $firstLedgerEntry.entryType -eq "Charge" `
        -and $null -ne $lastLedgerEntry `
        -and $lastLedgerEntry.entryDate -eq "2026-06-25" `
        -and [decimal]$lastLedgerEntry.runningBalanceAmount -eq 364.75 `
        -and $null -ne $anchorLedgerPayment `
        -and $null -ne $anchorLedgerAdjustment
    Add-Check -Name "anchor account ledger summary" -Result $(if ($accountLedgerPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $ledgerBilling.patientId
        ledgerSummary = $ledgerSummary
        firstEntry = $firstLedgerEntry
        lastEntry = $lastLedgerEntry
        anchorPayment = $anchorLedgerPayment
        anchorAdjustment = $anchorLedgerAdjustment
    }
}
catch {
    Add-Check -Name "anchor account ledger summary" -Result "failed" -Details $_.Exception.Message
}

try {
    $statementBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $statementSummary = $statementBilling.statementSummary
    $accountStatementPassed = $statementBilling.patientId -eq "MOD-PAT-0005" `
        -and $null -ne $statementSummary `
        -and $statementSummary.statementStatus -eq "Past due review" `
        -and $statementSummary.statementPeriodStart -eq "2025-06-22" `
        -and $statementSummary.statementPeriodEnd -eq "2026-06-25" `
        -and $statementSummary.statementDate -eq "2026-06-25" `
        -and $statementSummary.dueDate -eq "2026-07-25" `
        -and $statementSummary.recipientName -eq "Elias Morgan" `
        -and $statementSummary.mailingAddressLine1 -eq "105 Test Patient Avenue" `
        -and $statementSummary.mailingAddressLine2 -eq "Carlsbad, CA 92008" `
        -and $statementSummary.openEncounterCount -eq 3 `
        -and $statementSummary.ledgerEntryCount -eq 10 `
        -and $statementSummary.oldestOpenAgeDays -eq 361 `
        -and $statementSummary.oldestOpenDate -eq "2025-06-22" `
        -and [decimal]$statementSummary.chargeAmount -eq 635 `
        -and [decimal]$statementSummary.paymentAmount -eq 206 `
        -and [decimal]$statementSummary.adjustmentAmount -eq 64.25 `
        -and [decimal]$statementSummary.currentDueAmount -eq 83.75 `
        -and [decimal]$statementSummary.pastDueAmount -eq 281 `
        -and [decimal]$statementSummary.balanceDueAmount -eq 364.75
    Add-Check -Name "anchor account statement readiness" -Result $(if ($accountStatementPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $statementBilling.patientId
        statementSummary = $statementSummary
    }
}
catch {
    Add-Check -Name "anchor account statement readiness" -Result "failed" -Details $_.Exception.Message
}

try {
    $statementDocumentBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -TimeoutSec 20
    $statementDocument = $statementDocumentBilling.statementDocument
    $lastStatementLine = if ($null -ne $statementDocument -and $statementDocument.lineItems.Count -gt 0) {
        $statementDocument.lineItems[$statementDocument.lineItems.Count - 1]
    } else {
        $null
    }
    $statementDocumentPassed = $statementDocumentBilling.patientId -eq "MOD-PAT-0005" `
        -and $null -ne $statementDocument `
        -and $statementDocument.statementNumber -eq "STMT-MOD-PAT-0005-20260625" `
        -and $statementDocument.title -eq "Patient Statement" `
        -and $statementDocument.paymentInstructions -eq "Please pay `$364.75 by 2026-07-25." `
        -and $statementDocument.generatedText -like "*Patient Statement STMT-MOD-PAT-0005-20260625*" `
        -and $statementDocument.generatedText -like "*Balance due `$364.75*" `
        -and [decimal]$statementDocument.balanceDueAmount -eq 364.75 `
        -and $statementDocument.lineItems.Count -eq 10 `
        -and $null -ne $lastStatementLine `
        -and $lastStatementLine.lineNumber -eq 10 `
        -and $lastStatementLine.entryType -eq "Adjustment" `
        -and [decimal]$lastStatementLine.adjustmentAmount -eq 22.25 `
        -and [decimal]$lastStatementLine.balanceAmount -eq 364.75
    Add-Check -Name "anchor patient statement generation" -Result $(if ($statementDocumentPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $statementDocumentBilling.patientId
        statementDocument = $statementDocument
    }
}
catch {
    Add-Check -Name "anchor patient statement generation" -Result "failed" -Details $_.Exception.Message
}

try {
    $statementPdfResponse = Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005/statement.pdf" -Method Get -TimeoutSec 20
    $statementPdfBytes = if ($statementPdfResponse.Content -is [byte[]]) {
        $statementPdfResponse.Content
    } else {
        [System.Text.Encoding]::ASCII.GetBytes([string]$statementPdfResponse.Content)
    }
    $statementPdfText = [System.Text.Encoding]::ASCII.GetString($statementPdfBytes)
    $statementPdfContentType = [string]$statementPdfResponse.Headers["Content-Type"]
    $statementPdfDisposition = [string]$statementPdfResponse.Headers["Content-Disposition"]
    $statementPdfPassed = $statementPdfResponse.StatusCode -eq 200 `
        -and $statementPdfContentType -like "application/pdf*" `
        -and $statementPdfDisposition -like "*STMT-MOD-PAT-0005-20260625.pdf*" `
        -and $statementPdfText.StartsWith("%PDF-1.4") `
        -and $statementPdfText -like "*Patient Statement STMT-MOD-PAT-0005-20260625*" `
        -and $statementPdfText -like "*Please pay `$364.75 by 2026-07-25.*" `
        -and $statementPdfText -like "*Northstar HMO insurance payment*" `
        -and $statementPdfText -like "*EOB-NSTAR-1000052*"
    Add-Check -Name "anchor patient statement PDF export" -Result $(if ($statementPdfPassed) { "passed" } else { "failed" }) -Details @{
        statusCode = $statementPdfResponse.StatusCode
        contentType = $statementPdfContentType
        contentDisposition = $statementPdfDisposition
        byteLength = $statementPdfBytes.Length
    }
}
catch {
    Add-Check -Name "anchor patient statement PDF export" -Result "failed" -Details $_.Exception.Message
}

try {
    $statementBatch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/statements/batch?limit=5" -Method Get -TimeoutSec 20
    $statementBatchCandidates = @($statementBatch.candidates)
    $firstStatementBatchCandidate = $statementBatchCandidates | Select-Object -First 1
    $statementBatchPassed = $statementBatch.asOfDate -eq "2026-06-18" `
        -and $statementBatch.candidateCount -gt 5 `
        -and $statementBatchCandidates.Count -eq 5 `
        -and [decimal]$statementBatch.totalBalanceAmount -gt 0 `
        -and [decimal]$statementBatch.totalPastDueAmount -gt 0 `
        -and [decimal]$statementBatch.totalCurrentDueAmount -gt 0 `
        -and $null -ne $firstStatementBatchCandidate `
        -and $firstStatementBatchCandidate.statementNumber -like "STMT-MOD-PAT-*" `
        -and @("Past due review", "Ready for statement") -contains $firstStatementBatchCandidate.statementStatus `
        -and [decimal]$firstStatementBatchCandidate.balanceDueAmount -gt 0 `
        -and $firstStatementBatchCandidate.openEncounterCount -gt 0 `
        -and @("Email-ready", "Print") -contains $firstStatementBatchCandidate.deliveryMethod
    Add-Check -Name "anchor statement batch candidates" -Result $(if ($statementBatchPassed) { "passed" } else { "failed" }) -Details @{
        asOfDate = $statementBatch.asOfDate
        candidateCount = $statementBatch.candidateCount
        totalBalanceAmount = $statementBatch.totalBalanceAmount
        totalPastDueAmount = $statementBatch.totalPastDueAmount
        totalCurrentDueAmount = $statementBatch.totalCurrentDueAmount
        firstCandidate = $firstStatementBatchCandidate
    }
}
catch {
    Add-Check -Name "anchor statement batch candidates" -Result "failed" -Details $_.Exception.Message
}

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
    Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue
    $statementPackagePath = Join-Path $env:TEMP "legacy-ehr-statement-batch-$([guid]::NewGuid().ToString('N')).zip"
    $statementPackageClient = [System.Net.Http.HttpClient]::new()
    $statementPackageClient.Timeout = [TimeSpan]::FromSeconds(20)
    $statementPackageResponse = $statementPackageClient.GetAsync("$ApiBaseUrl/api/billing/statements/batch/package.zip?limit=5").GetAwaiter().GetResult()
    $statementPackageResponse.EnsureSuccessStatusCode() | Out-Null
    $statementPackageStatusCode = [int]$statementPackageResponse.StatusCode
    $statementPackageContentType = if ($null -ne $statementPackageResponse.Content.Headers.ContentType) { $statementPackageResponse.Content.Headers.ContentType.ToString() } else { "" }
    $statementPackageDisposition = if ($null -ne $statementPackageResponse.Content.Headers.ContentDisposition) { $statementPackageResponse.Content.Headers.ContentDisposition.ToString() } else { "" }
    $statementPackageBytes = $statementPackageResponse.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    [System.IO.File]::WriteAllBytes($statementPackagePath, $statementPackageBytes)

    $statementPackageZip = [System.IO.Compression.ZipFile]::OpenRead($statementPackagePath)
    try {
        $manifestEntry = $statementPackageZip.GetEntry("manifest.json")
        $summaryEntry = $statementPackageZip.GetEntry("summary.csv")
        $manifestReader = [System.IO.StreamReader]::new($manifestEntry.Open())
        try {
            $statementPackageManifest = $manifestReader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $manifestReader.Dispose()
        }
        $statementPdfEntries = @($statementPackageZip.Entries | Where-Object { $_.FullName -like "statements/*.pdf" })
        $firstManifestEntry = @($statementPackageManifest.entries) | Select-Object -First 1
        $firstStatementPdfEntry = if ($null -ne $firstManifestEntry) { $statementPackageZip.GetEntry($firstManifestEntry.fileName) } else { $null }
        $firstStatementPdfText = ""
        if ($null -ne $firstStatementPdfEntry) {
            $firstPdfReader = [System.IO.StreamReader]::new($firstStatementPdfEntry.Open(), [System.Text.Encoding]::ASCII)
            try {
                $firstStatementPdfText = $firstPdfReader.ReadToEnd()
            }
            finally {
                $firstPdfReader.Dispose()
            }
        }

        $statementPackagePassed = $statementPackageStatusCode -eq 200 `
            -and $statementPackageContentType -like "application/zip*" `
            -and $statementPackageDisposition -like "*statement-batch-20260618-top5.zip*" `
            -and $statementPackageBytes.Length -gt 0 `
            -and $null -ne $manifestEntry `
            -and $null -ne $summaryEntry `
            -and $statementPackageManifest.packageId -eq "STMT-BATCH-20260618-TOP5" `
            -and $statementPackageManifest.asOfDate -eq "2026-06-18" `
            -and $statementPackageManifest.includedStatementCount -eq 5 `
            -and $statementPdfEntries.Count -eq 5 `
            -and $null -ne $firstManifestEntry `
            -and $firstManifestEntry.statementNumber -like "STMT-MOD-PAT-*" `
            -and $firstStatementPdfText.StartsWith("%PDF-1.4") `
            -and $firstStatementPdfText -like "*$($firstManifestEntry.statementNumber)*"
        Add-Check -Name "anchor statement batch package export" -Result $(if ($statementPackagePassed) { "passed" } else { "failed" }) -Details @{
            statusCode = $statementPackageStatusCode
            contentType = $statementPackageContentType
            contentDisposition = $statementPackageDisposition
            byteLength = $statementPackageBytes.Length
            packageId = $statementPackageManifest.packageId
            includedStatementCount = $statementPackageManifest.includedStatementCount
            pdfEntryCount = $statementPdfEntries.Count
            firstStatement = $firstManifestEntry
        }
    }
    finally {
        $statementPackageZip.Dispose()
        $statementPackageResponse.Dispose()
        $statementPackageClient.Dispose()
        Remove-Item -LiteralPath $statementPackagePath -Force -ErrorAction SilentlyContinue
    }
}
catch {
    Add-Check -Name "anchor statement batch package export" -Result "failed" -Details $_.Exception.Message
}

try {
    $collectionsWorkQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/collections/work-queue?limit=5" -TimeoutSec 20
    $firstCollectionItem = @($collectionsWorkQueue.items) | Select-Object -First 1
    $collectionsWorkQueuePassed = $collectionsWorkQueue.asOfDate -eq "2026-06-18" `
        -and [int]$collectionsWorkQueue.accountCount -gt 0 `
        -and [int]$collectionsWorkQueue.highPriorityCount -gt 0 `
        -and [decimal]$collectionsWorkQueue.totalPastDueAmount -gt 0 `
        -and [decimal]$collectionsWorkQueue.totalOver90Amount -gt 0 `
        -and @($collectionsWorkQueue.items).Count -eq 5 `
        -and $null -ne $firstCollectionItem `
        -and $firstCollectionItem.statementNumber -like "STMT-MOD-PAT-*" `
        -and $firstCollectionItem.collectionTier -eq "High" `
        -and $firstCollectionItem.recommendedAction -eq "Final notice review" `
        -and [decimal]$firstCollectionItem.pastDueAmount -gt 0 `
        -and [decimal]$firstCollectionItem.over90Amount -gt 0
    Add-Check -Name "anchor collections work queue" -Result $(if ($collectionsWorkQueuePassed) { "passed" } else { "failed" }) -Details @{
        asOfDate = $collectionsWorkQueue.asOfDate
        accountCount = $collectionsWorkQueue.accountCount
        highPriorityCount = $collectionsWorkQueue.highPriorityCount
        totalPastDueAmount = $collectionsWorkQueue.totalPastDueAmount
        totalOver90Amount = $collectionsWorkQueue.totalOver90Amount
        firstItem = $firstCollectionItem
    }
}
catch {
    Add-Check -Name "anchor collections work queue" -Result "failed" -Details $_.Exception.Message
}

$collectionsFollowUpId = $null
try {
    $collectionsWorkQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/collections/work-queue?limit=5" -TimeoutSec 20
    $firstCollectionItem = @($collectionsWorkQueue.items) | Select-Object -First 1
    $createFollowUpBody = @{
        patientId = $firstCollectionItem.pubpid
        assignedTo = "billing"
        action = $firstCollectionItem.recommendedAction
        note = "Created by the smoke collections follow-up check."
    } | ConvertTo-Json
    $createdFollowUp = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/collections/follow-ups" -Method Post -ContentType "application/json" -Body $createFollowUpBody -TimeoutSec 20
    $collectionsFollowUpId = $createdFollowUp.id

    $closeFollowUpBody = @{
        status = "Done"
        body = "Closed by the smoke collections follow-up check."
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$collectionsFollowUpId/status" -Method Put -ContentType "application/json" -Body $closeFollowUpBody -TimeoutSec 20 | Out-Null
    $patientMessages = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$($firstCollectionItem.pubpid)" -Method Get -TimeoutSec 20
    $closedFollowUp = $patientMessages.messages | Where-Object { $_.id -eq $collectionsFollowUpId -and $_.status -eq "Done" } | Select-Object -First 1
    $collectionsFollowUpPassed = $createdFollowUp.task.title -eq "Collections follow-up: $($firstCollectionItem.statementNumber)" `
        -and $createdFollowUp.task.assignedTo -eq "billing" `
        -and $createdFollowUp.task.action -eq $firstCollectionItem.recommendedAction `
        -and $createdFollowUp.task.collectionTier -eq $firstCollectionItem.collectionTier `
        -and $createdFollowUp.task.body -like "*$($firstCollectionItem.statementNumber)*" `
        -and $createdFollowUp.task.body -like "*Created by the smoke collections follow-up check.*" `
        -and $null -ne $closedFollowUp `
        -and $closedFollowUp.body -eq "Closed by the smoke collections follow-up check."

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$collectionsFollowUpId" -Method Delete -TimeoutSec 20 | Out-Null
    $collectionsFollowUpId = $null

    Add-Check -Name "collections follow-up task lifecycle" -Result $(if ($collectionsFollowUpPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $firstCollectionItem.pubpid
        taskId = $createdFollowUp.id
        title = $createdFollowUp.task.title
        assignedTo = $createdFollowUp.task.assignedTo
        action = $createdFollowUp.task.action
        closedVisible = $null -ne $closedFollowUp
    }
}
catch {
    Add-Check -Name "collections follow-up task lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $collectionsFollowUpId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$collectionsFollowUpId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$billingLineMutationId = $null
try {
    $billingCodeText = "Smoke Billing Mutation"
    $createBillingBody = @{
        patientId = "MOD-PAT-0001"
        providerId = $null
        encounter = 1000013
        billingDate = "2026-06-18"
        codeType = "CPT4"
        code = "99213"
        codeText = $billingCodeText
        fee = 125.00
        units = 1
        justify = "Z00.00"
    } | ConvertTo-Json
    $createdBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines" -Method Post -ContentType "application/json" -Body $createBillingBody -TimeoutSec 20
    $billingLineMutationId = $createdBillingLine.id
    $createdBillingEncounter = $createdBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $createdBillingVisible = $createdBillingEncounter.lines | Where-Object { $_.id -eq $billingLineMutationId -and $_.code -eq "99213" -and $_.codeText -eq $billingCodeText -and $_.billed -eq 0 -and $_.activity -eq 1 } | Select-Object -First 1
    $createdEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $createdEncounterBillingVisible = $createdEncounterDetail.billingLines | Where-Object { $_.id -eq $billingLineMutationId -and $_.code -eq "99213" -and $_.codeText -eq $billingCodeText -and $_.activity -eq 1 } | Select-Object -First 1

    $statusBillingBody = @{
        billed = 1
        activity = 0
    } | ConvertTo-Json
    $inactiveBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingLineMutationId/status" -Method Put -ContentType "application/json" -Body $statusBillingBody -TimeoutSec 20
    $inactiveBillingEncounter = $inactiveBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveBillingVisible = $inactiveBillingEncounter.lines | Where-Object { $_.id -eq $billingLineMutationId } | Select-Object -First 1
    $inactiveEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $inactiveEncounterBillingVisible = $inactiveEncounterDetail.billingLines | Where-Object { $_.id -eq $billingLineMutationId } | Select-Object -First 1
    $billingLineMutationPassed = $null -ne $createdBillingVisible -and $null -eq $inactiveBillingVisible
    $encounterBillingLinkMutationPassed = $null -ne $createdEncounterBillingVisible -and $null -eq $inactiveEncounterBillingVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingLineMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $billingLineMutationId = $null

    Add-Check -Name "billing line mutation lifecycle" -Result $(if ($billingLineMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdBillingLine.id
        createdVisible = $createdBillingVisible
        inactiveVisible = $inactiveBillingVisible
    }
    Add-Check -Name "encounter billing linkage mutation visibility" -Result $(if ($encounterBillingLinkMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdBillingLine.id
        encounterBillingVisible = $createdEncounterBillingVisible
        inactiveEncounterBillingVisible = $inactiveEncounterBillingVisible
    }
}
catch {
    Add-Check -Name "billing line mutation lifecycle" -Result "failed" -Details $_.Exception.Message
    Add-Check -Name "encounter billing linkage mutation visibility" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $billingLineMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingLineMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$billingCorrectionMutationId = $null
try {
    $correctedBillingText = "Smoke Corrected Billing Mutation"
    $createCorrectionBody = @{
        patientId = "MOD-PAT-0001"
        providerId = $null
        encounter = 1000013
        billingDate = "2026-06-18"
        codeType = "CPT4"
        code = "99213"
        codeText = "Smoke Billing Correction Seed"
        fee = 125.00
        units = 1
        justify = "Z00.00"
    } | ConvertTo-Json
    $createdCorrectionLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines" -Method Post -ContentType "application/json" -Body $createCorrectionBody -TimeoutSec 20
    $billingCorrectionMutationId = $createdCorrectionLine.id

    $correctionBody = @{
        codeText = $correctedBillingText
        fee = 142.25
        units = 3
        justify = "E78.5"
    } | ConvertTo-Json
    $correctedBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingCorrectionMutationId" -Method Put -ContentType "application/json" -Body $correctionBody -TimeoutSec 20
    $correctedBillingEncounter = $correctedBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $correctedBillingVisible = $correctedBillingEncounter.lines | Where-Object { $_.id -eq $billingCorrectionMutationId -and $_.code -eq "99213" -and $_.codeText -eq $correctedBillingText -and $_.fee -eq 142.25 -and $_.units -eq 3 -and $_.justify -eq "E78.5" -and $_.billed -eq 0 -and $_.activity -eq 1 } | Select-Object -First 1

    $statusCorrectionBody = @{
        billed = 1
        activity = 0
    } | ConvertTo-Json
    $inactiveCorrectionLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingCorrectionMutationId/status" -Method Put -ContentType "application/json" -Body $statusCorrectionBody -TimeoutSec 20
    $inactiveCorrectionEncounter = $inactiveCorrectionLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveCorrectionVisible = $inactiveCorrectionEncounter.lines | Where-Object { $_.id -eq $billingCorrectionMutationId } | Select-Object -First 1
    $billingCorrectionPassed = $null -ne $correctedBillingVisible -and $null -eq $inactiveCorrectionVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingCorrectionMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $billingCorrectionMutationId = $null

    Add-Check -Name "billing correction mutation lifecycle" -Result $(if ($billingCorrectionPassed) { "passed" } else { "failed" }) -Details @{
        correctedId = $correctedBillingLine.id
        correctedVisible = $correctedBillingVisible
        inactiveVisible = $inactiveCorrectionVisible
    }
}
catch {
    Add-Check -Name "billing correction mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $billingCorrectionMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingCorrectionMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$billingModifierMutationId = $null
try {
    $modifierBillingText = "Smoke Modifier Billing Mutation"
    $createModifierBody = @{
        patientId = "MOD-PAT-0001"
        providerId = $null
        encounter = 1000013
        billingDate = "2026-06-18"
        codeType = "CPT4"
        code = "99213"
        modifier = ""
        codeText = "Smoke Modifier Billing Seed"
        fee = 125.00
        units = 1
        justify = "Z00.00"
    } | ConvertTo-Json
    $createdModifierLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines" -Method Post -ContentType "application/json" -Body $createModifierBody -TimeoutSec 20
    $billingModifierMutationId = $createdModifierLine.id

    $modifierBody = @{
        codeText = $modifierBillingText
        modifier = "25"
        fee = 142.25
        units = 2
        justify = "E78.5"
    } | ConvertTo-Json
    $modifiedBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingModifierMutationId" -Method Put -ContentType "application/json" -Body $modifierBody -TimeoutSec 20
    $modifiedBillingEncounter = $modifiedBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $modifiedBillingVisible = $modifiedBillingEncounter.lines | Where-Object { $_.id -eq $billingModifierMutationId -and $_.code -eq "99213" -and $_.modifier -eq "25" -and $_.codeText -eq $modifierBillingText -and $_.fee -eq 142.25 -and $_.units -eq 2 -and $_.justify -eq "E78.5" -and $_.billed -eq 0 -and $_.activity -eq 1 } | Select-Object -First 1

    $statusModifierBody = @{
        billed = 1
        activity = 0
    } | ConvertTo-Json
    $inactiveModifierLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingModifierMutationId/status" -Method Put -ContentType "application/json" -Body $statusModifierBody -TimeoutSec 20
    $inactiveModifierEncounter = $inactiveModifierLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveModifierVisible = $inactiveModifierEncounter.lines | Where-Object { $_.id -eq $billingModifierMutationId } | Select-Object -First 1
    $billingModifierPassed = $null -ne $modifiedBillingVisible -and $null -eq $inactiveModifierVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingModifierMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $billingModifierMutationId = $null

    Add-Check -Name "billing modifier mutation lifecycle" -Result $(if ($billingModifierPassed) { "passed" } else { "failed" }) -Details @{
        modifiedId = $modifiedBillingLine.id
        modifiedVisible = $modifiedBillingVisible
        inactiveVisible = $inactiveModifierVisible
    }
}
catch {
    Add-Check -Name "billing modifier mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $billingModifierMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingModifierMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$diagnosisLineMutationId = $null
try {
    $encounterDiagnosisMutationPassed = $false
    $diagnosisCodeText = "Smoke Diagnosis Mutation"
    $createDiagnosisBody = @{
        patientId = "MOD-PAT-0001"
        providerId = $null
        encounter = 1000013
        billingDate = "2026-06-18"
        codeType = "ICD10"
        code = "R73.03"
        codeText = $diagnosisCodeText
        fee = 0.00
        units = 1
        justify = "R73.03"
    } | ConvertTo-Json
    $createdDiagnosisLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines" -Method Post -ContentType "application/json" -Body $createDiagnosisBody -TimeoutSec 20
    $diagnosisLineMutationId = $createdDiagnosisLine.id
    $createdDiagnosisEncounter = $createdDiagnosisLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $createdDiagnosisVisible = $createdDiagnosisEncounter.lines | Where-Object { $_.id -eq $diagnosisLineMutationId -and $_.codeType -eq "ICD10" -and $_.code -eq "R73.03" -and $_.codeText -eq $diagnosisCodeText -and $_.fee -eq 0 -and $_.justify -eq "R73.03" -and $_.billed -eq 0 -and $_.activity -eq 1 } | Select-Object -First 1
    $createdDiagnosisEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $createdEncounterDiagnosisVisible = @($createdDiagnosisEncounterDetail.diagnosisCodes | Where-Object { $null -ne $_ }) | Where-Object {
        $_.code -eq "R73.03" `
            -and $_.description -eq $diagnosisCodeText `
            -and $_.billingLineCount -eq 2 `
            -and (@($_.sources) -contains "Fee sheet diagnosis line") `
            -and (@($_.sources) -contains "Fee sheet justification") `
            -and (@($_.supportingBillingCodes) -contains "ICD10 R73.03")
    } | Select-Object -First 1

    $statusDiagnosisBody = @{
        billed = 1
        activity = 0
    } | ConvertTo-Json
    $inactiveDiagnosisLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$diagnosisLineMutationId/status" -Method Put -ContentType "application/json" -Body $statusDiagnosisBody -TimeoutSec 20
    $inactiveDiagnosisEncounter = $inactiveDiagnosisLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveDiagnosisVisible = $inactiveDiagnosisEncounter.lines | Where-Object { $_.id -eq $diagnosisLineMutationId } | Select-Object -First 1
    $inactiveDiagnosisEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -TimeoutSec 20
    $inactiveEncounterDiagnosisVisible = @($inactiveDiagnosisEncounterDetail.diagnosisCodes | Where-Object { $null -ne $_ }) | Where-Object { $_.code -eq "R73.03" } | Select-Object -First 1
    $diagnosisLineMutationPassed = $null -ne $createdDiagnosisVisible -and $null -eq $inactiveDiagnosisVisible
    $encounterDiagnosisMutationPassed = $null -ne $createdEncounterDiagnosisVisible -and $null -eq $inactiveEncounterDiagnosisVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$diagnosisLineMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $diagnosisLineMutationId = $null

    Add-Check -Name "billing diagnosis mutation lifecycle" -Result $(if ($diagnosisLineMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdDiagnosisLine.id
        createdVisible = $createdDiagnosisVisible
        inactiveVisible = $inactiveDiagnosisVisible
    }
    Add-Check -Name "encounter diagnosis coding mutation visibility" -Result $(if ($encounterDiagnosisMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdDiagnosis = $createdEncounterDiagnosisVisible
        inactiveDiagnosis = $inactiveEncounterDiagnosisVisible
    }
}
catch {
    Add-Check -Name "billing diagnosis mutation lifecycle" -Result "failed" -Details $_.Exception.Message
    Add-Check -Name "encounter diagnosis coding mutation visibility" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $diagnosisLineMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$diagnosisLineMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $administration = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/directory" -Method Get -TimeoutSec 20
    $provider = $administration.users | Where-Object { $_.username -eq "gold-provider-02" -and $_.role -eq "provider" } | Select-Object -First 1
    $billingUser = $administration.users | Where-Object { $_.username -eq "gold-billing-01" -and $_.role -eq "billing" } | Select-Object -First 1
    $mainFacility = $administration.facilities | Where-Object { $_.code -eq "MAIN" -and $_.name -eq "Modernization Family Medicine" } | Select-Object -First 1
    $northFacility = $administration.facilities | Where-Object { $_.code -eq "NORTH" -and $_.name -eq "North County Clinic" } | Select-Object -First 1
    $administrationPassed = $administration.counts.users -eq 20 -and $administration.counts.providers -eq 12 -and $administration.counts.facilities -eq 3 -and $null -ne $provider -and $null -ne $billingUser -and $null -ne $mainFacility -and $null -ne $northFacility
    Add-Check -Name "anchor administration directory" -Result $(if ($administrationPassed) { "passed" } else { "failed" }) -Details @{
        counts = $administration.counts
        provider = $provider
        billingUser = $billingUser
        mainFacility = $mainFacility
        northFacility = $northFacility
    }

    $adminGroup = $administration.accessControl.groups | Where-Object { $_.value -eq "admin" -and $_.name -eq "Administrators" -and $_.permissionCount -eq 64 } | Select-Object -First 1
    $physicianGroup = $administration.accessControl.groups | Where-Object { $_.value -eq "doc" -and $_.name -eq "Physicians" -and $_.permissionCount -eq 31 } | Select-Object -First 1
    $clinicianGroup = $administration.accessControl.groups | Where-Object { $_.value -eq "clin" -and $_.name -eq "Clinicians" -and $_.permissionCount -eq 23 } | Select-Object -First 1
    $adminAclPermission = $administration.accessControl.groupPermissions | Where-Object { $_.groupValue -eq "admin" -and $_.sectionValue -eq "admin" -and $_.permissionValue -eq "acl" -and $_.returnValue -eq "write" } | Select-Object -First 1
    $frontDeskDemoPermission = $administration.accessControl.groupPermissions | Where-Object { $_.groupValue -eq "front" -and $_.sectionValue -eq "patients" -and $_.permissionValue -eq "demo" -and $_.returnValue -eq "write" } | Select-Object -First 1
    $adminMembership = $administration.accessControl.userMemberships | Where-Object { $_.userValue -eq "admin" -and $_.groupValue -eq "admin" -and $_.groupName -eq "Administrators" } | Select-Object -First 1
    $systemMembership = $administration.accessControl.userMemberships | Where-Object { $_.userValue -eq "oe-system" -and $_.groupValue -eq "admin" -and $_.groupName -eq "Administrators" } | Select-Object -First 1
    $accessControlPassed = $administration.counts.accessGroups -eq 7 -and $administration.counts.accessPermissions -eq 65 -and $administration.counts.accessGroupPermissions -eq 203 -and $administration.counts.accessUserMemberships -eq 2 -and $null -ne $adminGroup -and $null -ne $physicianGroup -and $null -ne $clinicianGroup -and $null -ne $adminAclPermission -and $null -ne $frontDeskDemoPermission -and $null -ne $adminMembership -and $null -ne $systemMembership
    Add-Check -Name "anchor administration access control" -Result $(if ($accessControlPassed) { "passed" } else { "failed" }) -Details @{
        counts = $administration.counts
        adminGroup = $adminGroup
        physicianGroup = $physicianGroup
        clinicianGroup = $clinicianGroup
        adminAclPermission = $adminAclPermission
        frontDeskDemoPermission = $frontDeskDemoPermission
        adminMembership = $adminMembership
        systemMembership = $systemMembership
    }
}
catch {
    Add-Check -Name "anchor administration directory" -Result "failed" -Details $_.Exception.Message
    Add-Check -Name "anchor administration access control" -Result "failed" -Details $_.Exception.Message
}

try {
    $accessGrantBody = @{
        groupValue = "front"
        sectionValue = "patients"
        permissionValue = "demo"
        returnValue = "write"
    } | ConvertTo-Json

    $revokedAccess = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions/front/patients/demo" -Method Delete -TimeoutSec 20
    $revokedFrontGroup = $revokedAccess.detail.accessControl.groups | Where-Object { $_.value -eq "front" -and $_.permissionCount -eq 5 } | Select-Object -First 1
    $revokedFrontDemo = $revokedAccess.detail.accessControl.groupPermissions | Where-Object { $_.groupValue -eq "front" -and $_.sectionValue -eq "patients" -and $_.permissionValue -eq "demo" } | Select-Object -First 1

    $restoredAccess = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" -Method Put -ContentType "application/json" -Body $accessGrantBody -TimeoutSec 20
    $restoredFrontGroup = $restoredAccess.detail.accessControl.groups | Where-Object { $_.value -eq "front" -and $_.permissionCount -eq 6 } | Select-Object -First 1
    $restoredFrontDemo = $restoredAccess.detail.accessControl.groupPermissions | Where-Object { $_.groupValue -eq "front" -and $_.sectionValue -eq "patients" -and $_.permissionValue -eq "demo" -and $_.returnValue -eq "write" } | Select-Object -First 1

    $accessPermissionMutationPassed = $revokedAccess.detail.counts.accessGroupPermissions -eq 202 `
        -and $restoredAccess.detail.counts.accessGroupPermissions -eq 203 `
        -and $null -ne $revokedFrontGroup `
        -and $null -eq $revokedFrontDemo `
        -and $null -ne $restoredFrontGroup `
        -and $null -ne $restoredFrontDemo

    Add-Check -Name "administration access permission mutation lifecycle" -Result $(if ($accessPermissionMutationPassed) { "passed" } else { "failed" }) -Details @{
        revokedCount = $revokedAccess.detail.counts.accessGroupPermissions
        restoredCount = $restoredAccess.detail.counts.accessGroupPermissions
        revokedFrontGroup = $revokedFrontGroup
        restoredFrontGroup = $restoredFrontGroup
        restoredFrontDemo = $restoredFrontDemo
    }
}
catch {
    Add-Check -Name "administration access permission mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    try {
        $accessGrantBody = @{
            groupValue = "front"
            sectionValue = "patients"
            permissionValue = "demo"
            returnValue = "write"
        } | ConvertTo-Json
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" -Method Put -ContentType "application/json" -Body $accessGrantBody -TimeoutSec 20 | Out-Null
    }
    catch {
    }
}

$administrationMembershipUserId = $null
try {
    $membershipUserName = "smoke-membership-user"
    $createMembershipUserBody = @{
        username = $membershipUserName
        firstName = "Smoke"
        lastName = "Membership"
        role = "frontdesk"
        calendar = $false
        facilityId = 10
        email = "$membershipUserName@example.test"
        npi = ""
        active = $true
    } | ConvertTo-Json

    $createdMembershipUser = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users" -Method Post -ContentType "application/json" -Body $createMembershipUserBody -TimeoutSec 20
    $administrationMembershipUserId = $createdMembershipUser.id

    $membershipGrantBody = @{
        userValue = $membershipUserName
        groupValue = "front"
    } | ConvertTo-Json

    $grantedMembership = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/user-memberships" -Method Put -ContentType "application/json" -Body $membershipGrantBody -TimeoutSec 20
    $frontMembership = $grantedMembership.detail.accessControl.userMemberships | Where-Object { $_.userValue -eq $membershipUserName -and $_.groupValue -eq "front" -and $_.groupName -eq "Front Office" } | Select-Object -First 1

    $revokedMembership = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/user-memberships/$membershipUserName/front" -Method Delete -TimeoutSec 20
    $revokedFrontMembership = $revokedMembership.detail.accessControl.userMemberships | Where-Object { $_.userValue -eq $membershipUserName -and $_.groupValue -eq "front" } | Select-Object -First 1

    $membershipMutationPassed = $grantedMembership.detail.counts.accessUserMemberships -eq 3 `
        -and $revokedMembership.detail.counts.accessUserMemberships -eq 2 `
        -and $null -ne $frontMembership `
        -and $null -eq $revokedFrontMembership

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationMembershipUserId" -Method Delete -TimeoutSec 20 | Out-Null
    $administrationMembershipUserId = $null

    Add-Check -Name "administration user group membership mutation lifecycle" -Result $(if ($membershipMutationPassed) { "passed" } else { "failed" }) -Details @{
        grantedCount = $grantedMembership.detail.counts.accessUserMemberships
        revokedCount = $revokedMembership.detail.counts.accessUserMemberships
        frontMembership = $frontMembership
        revokedFrontMembership = $revokedFrontMembership
    }
}
catch {
    Add-Check -Name "administration user group membership mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    try {
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/user-memberships/smoke-membership-user/front" -Method Delete -TimeoutSec 20 | Out-Null
    }
    catch {
    }
    if ($null -ne $administrationMembershipUserId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationMembershipUserId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$administrationUserMutationId = $null
try {
    $userName = "smoke-admin-user"
    $createUserBody = @{
        username = $userName
        firstName = "Smoke"
        lastName = "Admin"
        role = "frontdesk"
        calendar = $false
        facilityId = 10
        email = "$userName@example.test"
        npi = ""
        active = $true
    } | ConvertTo-Json

    $createdUser = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users" -Method Post -ContentType "application/json" -Body $createUserBody -TimeoutSec 20
    $administrationUserMutationId = $createdUser.id
    $createdUserVisible = $createdUser.detail.users | Where-Object { $_.id -eq $administrationUserMutationId -and $_.username -eq $userName -and $_.active } | Select-Object -First 1

    $updateUserBody = @{
        username = $userName
        firstName = "Smoke"
        lastName = "Admin Inactive"
        role = "frontdesk"
        calendar = $false
        facilityId = 10
        email = "$userName@example.test"
        npi = ""
        active = $false
    } | ConvertTo-Json

    $updatedUser = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationUserMutationId" -Method Put -ContentType "application/json" -Body $updateUserBody -TimeoutSec 20
    $updatedUserVisible = $updatedUser.detail.users | Where-Object { $_.id -eq $administrationUserMutationId -and $_.lastName -eq "Admin Inactive" -and -not $_.active } | Select-Object -First 1
    $userMutationPassed = $null -ne $createdUserVisible -and $null -ne $updatedUserVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationUserMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $administrationUserMutationId = $null

    Add-Check -Name "administration user mutation lifecycle" -Result $(if ($userMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdUser.id
        createdVisible = $createdUserVisible
        updatedVisible = $updatedUserVisible
    }
}
catch {
    Add-Check -Name "administration user mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $administrationUserMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationUserMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$administrationFacilityMutationId = $null
try {
    $facilityName = "Smoke Facility Mutation"
    $createFacilityBody = @{
        code = "SMOKE"
        name = $facilityName
        phone = "(619) 555-0198"
        street = "901 Smoke Way"
        city = "San Diego"
        state = "CA"
        postalCode = "92109"
        color = "#356f9f"
        active = $true
    } | ConvertTo-Json

    $createdFacility = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/facilities" -Method Post -ContentType "application/json" -Body $createFacilityBody -TimeoutSec 20
    $administrationFacilityMutationId = $createdFacility.id
    $createdVisible = $createdFacility.detail.facilities | Where-Object { $_.id -eq $administrationFacilityMutationId -and $_.name -eq $facilityName -and $_.active } | Select-Object -First 1

    $updateFacilityBody = @{
        code = "SMOKE"
        name = "$facilityName Inactive"
        phone = "(619) 555-0298"
        street = "901 Smoke Way"
        city = "San Diego"
        state = "CA"
        postalCode = "92109"
        color = "#356f9f"
        active = $false
    } | ConvertTo-Json

    $updatedFacility = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/facilities/$administrationFacilityMutationId" -Method Put -ContentType "application/json" -Body $updateFacilityBody -TimeoutSec 20
    $updatedVisible = $updatedFacility.detail.facilities | Where-Object { $_.id -eq $administrationFacilityMutationId -and $_.name -eq "$facilityName Inactive" -and -not $_.active } | Select-Object -First 1
    $facilityMutationPassed = $null -ne $createdVisible -and $null -ne $updatedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/facilities/$administrationFacilityMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $administrationFacilityMutationId = $null

    Add-Check -Name "administration facility mutation lifecycle" -Result $(if ($facilityMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdFacility.id
        createdVisible = $createdVisible
        updatedVisible = $updatedVisible
    }
}
catch {
    Add-Check -Name "administration facility mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $administrationFacilityMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/facilities/$administrationFacilityMutationId" -Method Delete -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $reports = Invoke-RestMethod -Uri "$ApiBaseUrl/api/reports/operational" -Method Get -TimeoutSec 20
    $topProvider = $reports.providerActivity | Where-Object { $_.username -eq "gold-provider-02" } | Select-Object -First 1
    $northFacility = $reports.facilityActivity | Where-Object { $_.code -eq "NORTH" } | Select-Object -First 1
    $asthmaCondition = $reports.clinicalConditions | Where-Object { $_.title -eq "Asthma, uncomplicated" -and $_.diagnosis -eq "ICD10:J45.909" } | Select-Object -First 1
    $reportsPassed = $reports.counts.patients -eq 1000 `
        -and $reports.counts.futureAppointments -eq 1261 `
        -and $reports.counts.currentYearEncounters -eq 1100 `
        -and $reports.counts.billingLines -eq 3000 `
        -and $reports.counts.billingTotal -eq 446000 `
        -and $reports.counts.patientDocuments -eq 1200 `
        -and $null -ne $topProvider `
        -and $topProvider.encounters -eq 176 `
        -and $null -ne $northFacility `
        -and $northFacility.appointments -eq 935 `
        -and $null -ne $asthmaCondition `
        -and $asthmaCondition.patients -eq 188
    Add-Check -Name "anchor operational reports" -Result $(if ($reportsPassed) { "passed" } else { "failed" }) -Details @{
        counts = $reports.counts
        topProvider = $topProvider
        northFacility = $northFacility
        asthmaCondition = $asthmaCondition
    }
}
catch {
    Add-Check -Name "anchor operational reports" -Result "failed" -Details $_.Exception.Message
}

try {
    $reportExport = Invoke-WebRequest -Uri "$ApiBaseUrl/api/reports/operational/export" -Method Get -UseBasicParsing -TimeoutSec 20
    $contentType = ($reportExport.Headers["Content-Type"] -join ",")
    $exportText = [string]$reportExport.Content
    $sampleLines = $exportText -split "\r?\n" | Select-Object -First 5
    $exportPassed = $reportExport.StatusCode -eq 200 `
        -and $contentType -like "text/csv*" `
        -and $exportText.Contains("Section,Name,Metric,Value") `
        -and $exportText.Contains("Counts,Patients,Total,1000") `
        -and $exportText.Contains("Counts,Patient Documents,Total,1200") `
        -and $exportText.Contains("Provider Activity,gold-provider-02,Encounters,176") `
        -and $exportText.Contains("Facility Activity,NORTH,Billing Total,148904.00") `
        -and $exportText.Contains("Clinical Conditions,ICD10:J45.909,Title,""Asthma, uncomplicated""")
    Add-Check -Name "operational reports csv export" -Result $(if ($exportPassed) { "passed" } else { "failed" }) -Details @{
        statusCode = $reportExport.StatusCode
        contentType = $contentType
        sample = $sampleLines
    }
}
catch {
    Add-Check -Name "operational reports csv export" -Result "failed" -Details $_.Exception.Message
}

$result = [ordered]@{
    status = $status
    apiBaseUrl = $ApiBaseUrl
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    checks = $checks
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
Write-Host "Modernized smoke test result: $ResultPath"

if ($status -ne "passed") {
    exit 1
}
