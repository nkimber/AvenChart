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

    $archivedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId/soft-delete" -Method Put -TimeoutSec 20
    $archivedVisible = $archivedDocument.detail.documents | Where-Object { $_.name -eq $documentName } | Select-Object -First 1
    $patientDocumentMutationPassed = $null -ne $createdVisible -and $null -eq $archivedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientDocumentMutationId = $null

    Add-Check -Name "patient document mutation lifecycle" -Result $(if ($patientDocumentMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdDocument.id
        createdVisible = $createdVisible
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

    $closeBody = @{
        status = "Done"
        body = "Closed by the smoke patient-message mutation check."
    } | ConvertTo-Json
    $closedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/status" -Method Put -ContentType "application/json" -Body $closeBody -TimeoutSec 20
    $closedVisible = $closedMessage.detail.messages | Where-Object { $_.title -eq $messageTitle -and $_.status -eq "Done" -and $_.body -eq "Closed by the smoke patient-message mutation check." } | Select-Object -First 1

    $archivedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/soft-delete" -Method Put -TimeoutSec 20
    $archivedVisible = $archivedMessage.detail.messages | Where-Object { $_.title -eq $messageTitle } | Select-Object -First 1
    $patientMessageMutationPassed = $null -ne $createdVisible -and $null -ne $closedVisible -and $null -eq $archivedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $patientMessageMutationId = $null

    Add-Check -Name "patient message mutation lifecycle" -Result $(if ($patientMessageMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdMessage.id
        createdVisible = $createdVisible
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

    $statusBillingBody = @{
        billed = 1
        activity = 0
    } | ConvertTo-Json
    $inactiveBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingLineMutationId/status" -Method Put -ContentType "application/json" -Body $statusBillingBody -TimeoutSec 20
    $inactiveBillingEncounter = $inactiveBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveBillingVisible = $inactiveBillingEncounter.lines | Where-Object { $_.id -eq $billingLineMutationId } | Select-Object -First 1
    $billingLineMutationPassed = $null -ne $createdBillingVisible -and $null -eq $inactiveBillingVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingLineMutationId" -Method Delete -TimeoutSec 20 | Out-Null
    $billingLineMutationId = $null

    Add-Check -Name "billing line mutation lifecycle" -Result $(if ($billingLineMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdBillingLine.id
        createdVisible = $createdBillingVisible
        inactiveVisible = $inactiveBillingVisible
    }
}
catch {
    Add-Check -Name "billing line mutation lifecycle" -Result "failed" -Details $_.Exception.Message
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
