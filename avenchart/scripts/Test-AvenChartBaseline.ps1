param(
    [string]$ApiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"

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
}
catch {
    Add-Check -Name "anchor administration directory" -Result "failed" -Details $_.Exception.Message
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
