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

$AdministrationHeaders = $null
$FrontDeskHeaders = $null
$ClinicianHeaders = $null

function Get-AdministrationHeaders {
    if ($null -eq $script:AdministrationHeaders) {
        $loginBody = @{
            username = "admin"
            password = "pass"
        }
        $login = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/auth/login" `
            -Method Post `
            -ContentType "application/json" `
            -Body ($loginBody | ConvertTo-Json -Depth 5) `
            -TimeoutSec 20

        if ($login.authenticated -ne $true -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
            throw "Administration smoke login did not issue an active session."
        }

        $script:AdministrationHeaders = @{ "X-Legacy EHR-Session" = $login.sessionId }
    }

    return $script:AdministrationHeaders
}

function Get-FrontDeskHeaders {
    if ($null -eq $script:FrontDeskHeaders) {
        $loginBody = @{
            username = "gold-frontdesk-01"
            password = "pass"
        }
        $login = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/auth/login" `
            -Method Post `
            -ContentType "application/json" `
            -Body ($loginBody | ConvertTo-Json -Depth 5) `
            -TimeoutSec 20

        if ($login.authenticated -ne $true -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
            throw "Front-desk smoke login did not issue an active session."
        }

        $script:FrontDeskHeaders = @{ "X-Legacy EHR-Session" = $login.sessionId }
    }

    return $script:FrontDeskHeaders
}

function Get-ClinicianHeaders {
    if ($null -eq $script:ClinicianHeaders) {
        $loginBody = @{
            username = "gold-provider-01"
            password = "pass"
        }
        $login = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/auth/login" `
            -Method Post `
            -ContentType "application/json" `
            -Body ($loginBody | ConvertTo-Json -Depth 5) `
            -TimeoutSec 20

        if ($login.authenticated -ne $true -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
            throw "Clinician smoke login did not issue an active session."
        }

        $script:ClinicianHeaders = @{ "X-Legacy EHR-Session" = $login.sessionId }
    }

    return $script:ClinicianHeaders
}

function New-AuthenticatedHttpClient {
    $client = [System.Net.Http.HttpClient]::new()
    $headers = Get-AdministrationHeaders
    foreach ($headerName in $headers.Keys) {
        $client.DefaultRequestHeaders.Add($headerName, [string]$headers[$headerName])
    }

    return $client
}

function Read-HttpErrorBody {
    param(
        [Parameter(Mandatory = $true)]
        [object]$ErrorRecord
    )

    if ($ErrorRecord.ErrorDetails -and $ErrorRecord.ErrorDetails.Message) {
        return $ErrorRecord.ErrorDetails.Message
    }

    $response = $ErrorRecord.Exception.Response
    if ($null -eq $response) {
        return $null
    }

    try {
        if ($response.Content) {
            return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    catch {
    }

    try {
        if ($response.GetResponseStream) {
            $stream = $response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = [System.IO.StreamReader]::new($stream)
                try {
                    return $reader.ReadToEnd()
                }
                finally {
                    $reader.Dispose()
                }
            }
        }
    }
    catch {
    }

    return $null
}

try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -Method Get -TimeoutSec 15
    Add-Check -Name "api health" -Result $(if ($health.status -eq "healthy") { "passed" } else { "failed" }) -Details $health
}
catch {
    Add-Check -Name "api health" -Result "failed" -Details $_.Exception.Message
}

try {
    $loginBody = @{
        username = "admin"
        password = "pass"
    }
    $login = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body ($loginBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $frontDeskLogin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "gold-frontdesk-01"; password = "pass" } | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $clinicianLogin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "gold-provider-01"; password = "pass" } | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $rejectedLogin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "admin"; password = "wrong-pass" } | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $session = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/session" `
        -Method Get `
        -Headers @{ "X-Legacy EHR-Session" = $login.sessionId } `
        -TimeoutSec 20

    $unauthenticatedAuditStatus = 0
    try {
        $unauthenticatedAudit = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/auth/login-audit?limit=5" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedAuditStatus = [int]$unauthenticatedAudit.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedAuditStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $loginAudit = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login-audit?limit=5" `
        -Method Get `
        -Headers @{ "X-Legacy EHR-Session" = $login.sessionId } `
        -TimeoutSec 20

    $unauthenticatedAdministrationStatus = 0
    try {
        $unauthenticatedAdministration = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/administration/directory" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedAdministrationStatus = [int]$unauthenticatedAdministration.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedAdministrationStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $administrationDirectory = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/administration/directory" `
        -Method Get `
        -Headers @{ "X-Legacy EHR-Session" = $login.sessionId } `
        -TimeoutSec 20

    $frontDeskAdministrationStatus = 0
    try {
        $frontDeskAdministration = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/administration/directory" `
            -Method Get `
            -Headers @{ "X-Legacy EHR-Session" = $frontDeskLogin.sessionId } `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskAdministrationStatus = [int]$frontDeskAdministration.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskAdministrationStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $auditSuccess = $loginAudit.events | Where-Object { $_.username -eq "admin" -and $_.success -eq $true } | Select-Object -First 1
    $auditFailure = $loginAudit.events | Where-Object { $_.username -eq "admin" -and $_.success -eq $false } | Select-Object -First 1

    $logout = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/logout" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ sessionId = $login.sessionId } | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $sessionAfterLogout = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/session" `
        -Method Get `
        -Headers @{ "X-Legacy EHR-Session" = $login.sessionId } `
        -TimeoutSec 20

    $loginPassed = $login.authenticated -eq $true `
        -and $login.username -eq "admin" `
        -and $login.displayName -eq "Administrator" `
        -and $login.role -eq "administrator" `
        -and -not [string]::IsNullOrWhiteSpace($login.sessionId) `
        -and $session.authenticated -eq $true `
        -and $session.username -eq "admin" `
        -and $frontDeskLogin.authenticated -eq $true `
        -and $frontDeskLogin.username -eq "gold-frontdesk-01" `
        -and $clinicianLogin.authenticated -eq $true `
        -and $clinicianLogin.username -eq "gold-provider-01" `
        -and $clinicianLogin.role -eq "provider" `
        -and $frontDeskAdministrationStatus -eq 403 `
        -and $unauthenticatedAuditStatus -eq 401 `
        -and $unauthenticatedAdministrationStatus -eq 401 `
        -and $administrationDirectory.counts.users -ge 20 `
        -and $administrationDirectory.counts.facilities -ge 3 `
        -and $logout.authenticated -eq $false `
        -and $null -ne $logout.endedAt `
        -and $sessionAfterLogout.authenticated -eq $false `
        -and $rejectedLogin.authenticated -eq $false `
        -and $loginAudit.totalEvents -ge 2 `
        -and $loginAudit.successfulLogins -ge 1 `
        -and $loginAudit.failedLogins -ge 1 `
        -and $null -ne $auditSuccess `
        -and $null -ne $auditFailure

    Add-Check -Name "admin login readiness" -Result $(if ($loginPassed) { "passed" } else { "failed" }) -Details @{
        successUsername = $login.username
        successRole = $login.role
        sessionIssued = -not [string]::IsNullOrWhiteSpace($login.sessionId)
        sessionValidated = $session.authenticated
        frontDeskUsername = $frontDeskLogin.username
        clinicianUsername = $clinicianLogin.username
        clinicianRole = $clinicianLogin.role
        frontDeskAdministrationStatus = $frontDeskAdministrationStatus
        unauthenticatedAuditStatus = $unauthenticatedAuditStatus
        unauthenticatedAdministrationStatus = $unauthenticatedAdministrationStatus
        administrationUsers = $administrationDirectory.counts.users
        administrationFacilities = $administrationDirectory.counts.facilities
        sessionEnded = $null -ne $logout.endedAt
        sessionAfterLogout = $sessionAfterLogout.authenticated
        rejectedReason = $rejectedLogin.failureReason
        auditEvents = $loginAudit.totalEvents
        auditSuccesses = $loginAudit.successfulLogins
        auditFailures = $loginAudit.failedLogins
    }
}
catch {
    Add-Check -Name "admin login readiness" -Result "failed" -Details $_.Exception.Message
}

try {
    $unauthenticatedPatientSearchStatus = 0
    try {
        $unauthenticatedPatientSearch = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/patients?search=MOD-PAT-0001&limit=5" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedPatientSearchStatus = [int]$unauthenticatedPatientSearch.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedPatientSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $search = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients?search=MOD-PAT-0001&limit=5" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $anchor = $search.patients | Where-Object { $_.canonicalId -eq "MOD-PAT-0001" } | Select-Object -First 1
    $frontDeskPatientSearchStatus = 0
    $frontDeskPatientChartStatus = 0
    $frontDeskPatientSearch = $null
    $frontDeskPatientChart = $null
    try {
        $frontDeskPatientSearch = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/patients?search=MOD-PAT-0001&limit=5" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20
        $frontDeskPatientSearchStatus = 200
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskPatientSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }
    try {
        $frontDeskPatientChart = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0001" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20
        $frontDeskPatientChartStatus = 200
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskPatientChartStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    Add-Check -Name "anchor patient search" -Result $(if ($null -ne $anchor -and $unauthenticatedPatientSearchStatus -eq 401 -and $frontDeskPatientSearchStatus -eq 200 -and $frontDeskPatientChartStatus -eq 200) { "passed" } else { "failed" }) -Details @{
        unauthenticatedStatus = $unauthenticatedPatientSearchStatus
        frontDeskSearchStatus = $frontDeskPatientSearchStatus
        frontDeskChartStatus = $frontDeskPatientChartStatus
        totalMatches = $search.totalMatches
        firstPatient = $search.patients | Select-Object -First 1
        frontDeskFirstPatient = $frontDeskPatientSearch.patients | Select-Object -First 1
        frontDeskChart = if ($null -ne $frontDeskPatientChart) {
            @{
                canonicalId = $frontDeskPatientChart.canonicalId
                displayName = $frontDeskPatientChart.displayName
                purpose = $frontDeskPatientChart.purpose
            }
        } else {
            $null
        }
    }
}
catch {
    Add-Check -Name "anchor patient search" -Result "failed" -Details $_.Exception.Message
}

try {
    $chart = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $demographicsOriginal = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
        -Headers (Get-AdministrationHeaders) `
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
        -Headers (Get-AdministrationHeaders) `
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
                -Headers (Get-AdministrationHeaders) `
                -ContentType "application/json" `
                -Body ($originalDemographicsBody | ConvertTo-Json -Depth 5) `
                -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$deceasedOriginal = $null
try {
    $deceasedOriginal = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $originalDeceasedBody = @{
        deceasedDate = $deceasedOriginal.deceasedDate
        deceasedReason = $deceasedOriginal.deceasedReason
    }
    $deceasedBody = @{
        deceasedDate = "2026-06-20"
        deceasedReason = "Smoke deceased-status readiness"
    }

    $updatedDeceased = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010/deceased-status" `
        -Method Put `
        -Headers (Get-AdministrationHeaders) `
        -ContentType "application/json" `
        -Body ($deceasedBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $reloadedDeceased = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $restoredDeceased = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010/deceased-status" `
        -Method Put `
        -Headers (Get-AdministrationHeaders) `
        -ContentType "application/json" `
        -Body ($originalDeceasedBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20
    $deceasedOriginal = $null

    $mutationPassed = $updatedDeceased.deceasedDate -eq $deceasedBody.deceasedDate `
        -and $updatedDeceased.deceasedReason -eq $deceasedBody.deceasedReason `
        -and $reloadedDeceased.deceasedDate -eq $deceasedBody.deceasedDate `
        -and $reloadedDeceased.deceasedReason -eq $deceasedBody.deceasedReason

    $restorePassed = $restoredDeceased.deceasedDate -eq $originalDeceasedBody.deceasedDate `
        -and $restoredDeceased.deceasedReason -eq $originalDeceasedBody.deceasedReason

    Add-Check -Name "patient deceased status lifecycle" -Result $(if ($mutationPassed -and $restorePassed) { "passed" } else { "failed" }) -Details @{
        updatedDate = $updatedDeceased.deceasedDate
        updatedReason = $updatedDeceased.deceasedReason
        restoredDate = $restoredDeceased.deceasedDate
        restoredReason = $restoredDeceased.deceasedReason
    }
}
catch {
    Add-Check -Name "patient deceased status lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $deceasedOriginal) {
        try {
            $originalDeceasedBody = @{
                deceasedDate = $deceasedOriginal.deceasedDate
                deceasedReason = $deceasedOriginal.deceasedReason
            }
            Invoke-RestMethod `
                -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010/deceased-status" `
                -Method Put `
                -Headers (Get-AdministrationHeaders) `
                -ContentType "application/json" `
                -Body ($originalDeceasedBody | ConvertTo-Json -Depth 5) `
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
        -Headers (Get-AdministrationHeaders) `
        -ContentType "application/json" `
        -Body ($registrationBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20

    $loadedPatient = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$registrationPubpid" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $searchCreatedPatient = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients?search=$registrationPubpid&limit=5" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$registrationPubpid" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $registrationPubpid = $null

    $deletedLoadFailed = $false
    try {
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$($registrationBody.pubpid)" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$registrationPubpid" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    Add-Check -Name "patient registration lifecycle" -Result "failed" -Details $_.Exception.Message
}

$invalidRegistrationPubpid = "TMP-PAT-REG-VAL-SMK$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
try {
    $invalidRegistrationBody = @{
        pubpid = $invalidRegistrationPubpid
        firstName = "Validation"
        lastName = "Q"
        preferredName = "Slice192"
        sex = ""
        dateOfBirth = "1991-04-15"
        street = "192 Validation Way"
        city = "Hartford"
        state = "CT"
        postalCode = "06103"
        maritalStatus = "single"
        occupation = "Registration Validation Fixture"
        phoneHome = "(860) 555-1920"
        phoneCell = "(860) 555-1921"
        email = "not-an-email"
        hipaaAllowSms = "YES"
        hipaaAllowEmail = "YES"
    }

    $validationStatusCode = $null
    $validationProblem = $null
    try {
        Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/patients" `
            -Method Post `
            -Headers (Get-AdministrationHeaders) `
            -ContentType "application/json" `
            -Body ($invalidRegistrationBody | ConvertTo-Json -Depth 5) `
            -TimeoutSec 20 | Out-Null
    }
    catch {
        if ($_.Exception.Response) {
            $validationStatusCode = [int]$_.Exception.Response.StatusCode
        }
        $validationProblemBody = Read-HttpErrorBody -ErrorRecord $_
        if ($validationProblemBody) {
            $validationProblem = $validationProblemBody | ConvertFrom-Json
        }
    }

    $invalidLoadFailed = $false
    try {
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$invalidRegistrationPubpid" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    }
    catch {
        $invalidLoadFailed = $true
    }

    $registrationValidationPassed = $validationStatusCode -eq 400 `
        -and $validationProblem.title -eq "Patient registration validation failed" `
        -and (@($validationProblem.errors.lastName) -contains "Last name must be at least 2 characters.") `
        -and (@($validationProblem.errors.sex) -contains "Sex is required.") `
        -and (@($validationProblem.errors.email) -contains "Email must be a valid email address.") `
        -and $invalidLoadFailed

    Add-Check -Name "patient registration validation readiness" -Result $(if ($registrationValidationPassed) { "passed" } else { "failed" }) -Details @{
        pubpid = $invalidRegistrationPubpid
        statusCode = $validationStatusCode
        validationProblem = $validationProblem
        invalidLoadFailed = $invalidLoadFailed
    }
}
catch {
    Add-Check -Name "patient registration validation readiness" -Result "failed" -Details $_.Exception.Message
}

$duplicateRegistrationPubpid = $null
try {
    $suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $duplicateAnchor = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0010" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $duplicateRegistrationPubpid = "TMP-PAT-REG-DUP-SMK$suffix"
    $duplicateRegistrationBody = @{
        pubpid = $duplicateRegistrationPubpid
        firstName = $duplicateAnchor.firstName
        lastName = $duplicateAnchor.lastName
        preferredName = "Slice191"
        sex = $duplicateAnchor.sex
        dateOfBirth = $duplicateAnchor.dateOfBirth
        street = "191 Duplicate Way"
        city = "New Haven"
        state = "CT"
        postalCode = "06511"
        maritalStatus = "single"
        occupation = "Duplicate Detection Fixture"
        phoneHome = $duplicateAnchor.phoneHome
        phoneCell = $duplicateAnchor.phoneCell
        email = $duplicateAnchor.email
        hipaaAllowSms = "YES"
        hipaaAllowEmail = "YES"
    }

    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients" `
        -Method Post `
        -Headers (Get-AdministrationHeaders) `
        -ContentType "application/json" `
        -Body ($duplicateRegistrationBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20 | Out-Null

    $duplicateQuery = "firstName=$([System.Uri]::EscapeDataString([string]$duplicateRegistrationBody.firstName))" +
        "&lastName=$([System.Uri]::EscapeDataString([string]$duplicateRegistrationBody.lastName))" +
        "&dateOfBirth=$([System.Uri]::EscapeDataString([string]$duplicateRegistrationBody.dateOfBirth))" +
        "&phone=$([System.Uri]::EscapeDataString([string]$duplicateRegistrationBody.phoneHome))" +
        "&email=$([System.Uri]::EscapeDataString([string]$duplicateRegistrationBody.email))" +
        "&excludePatientId=$([System.Uri]::EscapeDataString([string]$duplicateRegistrationBody.pubpid))&limit=5"
    $duplicateSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/duplicates?$duplicateQuery" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $duplicateCandidate = @($duplicateSearch.candidates) | Where-Object { $_.pubpid -eq "MOD-PAT-0010" } | Select-Object -First 1

    $duplicateChart = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$duplicateRegistrationPubpid" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $chartDuplicateCandidate = @($duplicateChart.duplicateCandidates) | Where-Object { $_.pubpid -eq "MOD-PAT-0010" } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$duplicateRegistrationPubpid" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $duplicateRegistrationPubpid = $null

    $duplicateDetectionPassed = $duplicateSearch.totalCandidates -ge 1 `
        -and $null -ne $duplicateCandidate `
        -and $duplicateCandidate.matchScore -eq 100 `
        -and (@($duplicateCandidate.matchReasons) -contains "Same first name, last name, and date of birth") `
        -and (@($duplicateCandidate.matchReasons) -contains "Matching phone") `
        -and (@($duplicateCandidate.matchReasons) -contains "Matching email") `
        -and $duplicateChart.pubpid -eq $duplicateRegistrationBody.pubpid `
        -and $null -ne $chartDuplicateCandidate `
        -and $chartDuplicateCandidate.matchScore -eq 100

    Add-Check -Name "patient duplicate detection readiness" -Result $(if ($duplicateDetectionPassed) { "passed" } else { "failed" }) -Details @{
        pubpid = $duplicateRegistrationBody.pubpid
        totalCandidates = $duplicateSearch.totalCandidates
        duplicateCandidate = $duplicateCandidate
        chartDuplicateCandidate = $chartDuplicateCandidate
    }
}
catch {
    if ($duplicateRegistrationPubpid) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/$duplicateRegistrationPubpid" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    Add-Check -Name "patient duplicate detection readiness" -Result "failed" -Details $_.Exception.Message
}

try {
    $coverageChart = Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
        -Headers (Get-AdministrationHeaders) `
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
        -Headers (Get-AdministrationHeaders) `
        -ContentType "application/json" `
        -Body ($insuranceUpdateBody | ConvertTo-Json -Depth 5) `
        -TimeoutSec 20
    $updatedCoverage = @($updatedCoverageChart.insurance) | Where-Object { $_.id -eq $insuranceMutationId } | Select-Object -First 1

    $deletedCoverageChart = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patients/insurance/$insuranceMutationId" `
        -Method Delete `
        -Headers (Get-AdministrationHeaders) `
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/patients/insurance/$insuranceMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    Add-Check -Name "patient insurance mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}

try {
    $unauthenticatedAppointmentSearchStatus = 0
    $frontDeskAppointmentSearchStatus = 0
    try {
        $unauthenticatedAppointmentSearch = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-06-18&limit=5" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedAppointmentSearchStatus = [int]$unauthenticatedAppointmentSearch.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedAppointmentSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }
    try {
        $frontDeskAppointmentSearch = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-06-18&limit=5" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskAppointmentSearchStatus = 200
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskAppointmentSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $appointments = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-06-18&limit=5" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $anchorAppointment = $appointments.appointments | Select-Object -First 1
    $appointmentPassed = $unauthenticatedAppointmentSearchStatus -eq 401 -and $frontDeskAppointmentSearchStatus -eq 200 -and $null -ne $anchorAppointment -and ([datetime]$anchorAppointment.date) -gt ([datetime]"2026-06-18")
    Add-Check -Name "anchor appointment search" -Result $(if ($appointmentPassed) { "passed" } else { "failed" }) -Details @{
        unauthenticatedStatus = $unauthenticatedAppointmentSearchStatus
        frontDeskStatus = $frontDeskAppointmentSearchStatus
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

    $appointmentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$($anchorAppointment.id)" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

try {
    $reminderAppointments = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0191&from=2026-06-18&limit=5" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $reminderAppointment = $reminderAppointments.appointments | Where-Object { $_.id -eq "APPT-MOD-PAT-0191-3" } | Select-Object -First 1
    if ($null -eq $reminderAppointment) {
        throw "Expected reminder anchor appointment APPT-MOD-PAT-0191-3 was not returned."
    }

    $reminderDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$($reminderAppointment.id)" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $appointmentReminderPassed = $reminderDetail.patientId -eq "MOD-PAT-0191" `
        -and $reminderDetail.date -eq "2026-06-25" `
        -and $reminderDetail.reminderDue -eq $true `
        -and $reminderDetail.reminderStatus -eq "Due now" `
        -and $reminderDetail.reminderChannel -eq "SMS + Email" `
        -and $reminderDetail.reminderContact -eq "(619) 555-1191 / mod-pat-0191@example.test" `
        -and $reminderDetail.reminderLeadDays -eq 7
    Add-Check -Name "appointment reminder readiness" -Result $(if ($appointmentReminderPassed) { "passed" } else { "failed" }) -Details @{
        id = $reminderDetail.id
        patientId = $reminderDetail.patientId
        date = $reminderDetail.date
        reminderDue = $reminderDetail.reminderDue
        reminderStatus = $reminderDetail.reminderStatus
        reminderChannel = $reminderDetail.reminderChannel
        reminderContact = $reminderDetail.reminderContact
        reminderLeadDays = $reminderDetail.reminderLeadDays
    }
}
catch {
    Add-Check -Name "appointment reminder readiness" -Result "failed" -Details $_.Exception.Message
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
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentMutationId = $createdAppointment.id

    $cancelBody = @{
        status = "x"
        title = "Smoke Appointment Mutation Cancelled"
    } | ConvertTo-Json
    $cancelledAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $cancelBody -TimeoutSec 20
    $appointmentMutationPassed = $createdAppointment.status -eq "-" -and $cancelledAppointment.status -eq "x" -and $cancelledAppointment.title -eq "Smoke Appointment Mutation Cancelled"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentViewDowngradeActive = $false
try {
    $appointmentViewDowngradeBody = @{
        groupValue = "clin"
        sectionValue = "patients"
        permissionValue = "appt"
        returnValue = "view"
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $appointmentViewDowngradeBody -TimeoutSec 20 | Out-Null
    $appointmentViewDowngradeActive = $true
    $script:ClinicianHeaders = $null

    $clinicianAppointments = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-06-18&limit=5" -Method Get -Headers (Get-ClinicianHeaders) -TimeoutSec 20
    $clinicianAppointmentMutationStatus = 0
    $clinicianAppointmentMutationBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = "Blocked Appointment Mutation Authorization"
        date = "2026-11-17"
        startTime = "09:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Blocked"
        comments = "Blocked by the smoke appointment mutation authorization check."
    } | ConvertTo-Json
    try {
        $clinicianAppointmentMutation = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/appointments" `
            -Method Post `
            -Headers (Get-ClinicianHeaders) `
            -ContentType "application/json" `
            -Body $clinicianAppointmentMutationBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $clinicianAppointmentMutationStatus = [int]$clinicianAppointmentMutation.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $clinicianAppointmentMutationStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $appointmentMutationAuthorizationPassed = $clinicianAppointments.totalMatches -gt 0 -and $clinicianAppointmentMutationStatus -eq 403
    Add-Check -Name "appointment mutation authorization" -Result $(if ($appointmentMutationAuthorizationPassed) { "passed" } else { "failed" }) -Details @{
        clinicianAppointmentMatches = $clinicianAppointments.totalMatches
        clinicianMutationStatus = $clinicianAppointmentMutationStatus
    }
}
catch {
    Add-Check -Name "appointment mutation authorization" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($appointmentViewDowngradeActive) {
        try {
            $appointmentWriteRestoreBody = @{
                groupValue = "clin"
                sectionValue = "patients"
                permissionValue = "appt"
                returnValue = "write"
            } | ConvertTo-Json
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $appointmentWriteRestoreBody -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    $script:ClinicianHeaders = $null
}

$appointmentOverlapPrimaryId = $null
$appointmentOverlapSecondaryId = $null
try {
    $primaryOverlapBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 102
        title = "Smoke Appointment Provider Overlap A"
        date = "2026-12-04"
        startTime = "09:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Overlap"
        comments = "Smoke overlap primary appointment"
    } | ConvertTo-Json
    $secondaryOverlapBody = @{
        patientId = "MOD-PAT-0004"
        providerId = 102
        title = "Smoke Appointment Provider Overlap B"
        date = "2026-12-04"
        startTime = "09:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Overlap"
        comments = "Smoke overlap secondary appointment"
    } | ConvertTo-Json

    $primaryOverlapAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $primaryOverlapBody -TimeoutSec 20
    $appointmentOverlapPrimaryId = $primaryOverlapAppointment.id
    $secondaryOverlapAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $secondaryOverlapBody -TimeoutSec 20
    $appointmentOverlapSecondaryId = $secondaryOverlapAppointment.id

    $primaryOverlapDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOverlapPrimaryId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $secondaryOverlapDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOverlapSecondaryId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $appointmentProviderOverlapPassed = $primaryOverlapDetail.providerOverlapCount -eq 1 `
        -and $secondaryOverlapDetail.providerOverlapCount -eq 1 `
        -and ($primaryOverlapDetail.providerOverlapAppointmentIds -contains $appointmentOverlapSecondaryId) `
        -and ($secondaryOverlapDetail.providerOverlapAppointmentIds -contains $appointmentOverlapPrimaryId)

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOverlapSecondaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentOverlapSecondaryId = $null
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOverlapPrimaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentOverlapPrimaryId = $null

    Add-Check -Name "appointment provider overlap tolerance" -Result $(if ($appointmentProviderOverlapPassed) { "passed" } else { "failed" }) -Details @{
        primaryId = $primaryOverlapAppointment.id
        secondaryId = $secondaryOverlapAppointment.id
        primaryOverlapCount = $primaryOverlapDetail.providerOverlapCount
        secondaryOverlapCount = $secondaryOverlapDetail.providerOverlapCount
        primaryOverlapAppointmentIds = $primaryOverlapDetail.providerOverlapAppointmentIds
        secondaryOverlapAppointmentIds = $secondaryOverlapDetail.providerOverlapAppointmentIds
    }
}
catch {
    Add-Check -Name "appointment provider overlap tolerance" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentOverlapSecondaryId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOverlapSecondaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    if ($null -ne $appointmentOverlapPrimaryId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOverlapPrimaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentRescheduleId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = $null
        title = "Smoke Appointment Reschedule"
        date = "2026-10-15"
        startTime = "10:30"
        durationMinutes = 30
        facilityId = $null
        categoryId = 9
        room = "Smoke"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentRescheduleId = $createdAppointment.id

    $updateBody = @{
        providerId = $null
        title = "Smoke Appointment Rescheduled"
        date = "2026-10-22"
        startTime = "14:15"
        durationMinutes = 45
        facilityId = $null
        categoryId = 9
        room = "Resched"
        status = "@"
    } | ConvertTo-Json
    $updatedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRescheduleId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $appointmentReschedulePassed = $updatedAppointment.title -eq "Smoke Appointment Rescheduled" `
        -and $updatedAppointment.date -eq "2026-10-22" `
        -and $updatedAppointment.startTime -eq "14:15" `
        -and $updatedAppointment.durationMinutes -eq 45 `
        -and $updatedAppointment.room -eq "Resched" `
        -and $updatedAppointment.status -eq "@"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRescheduleId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentRescheduleId = $null

    Add-Check -Name "appointment reschedule lifecycle" -Result $(if ($appointmentReschedulePassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        title = $updatedAppointment.title
        date = $updatedAppointment.date
        startTime = $updatedAppointment.startTime
        durationMinutes = $updatedAppointment.durationMinutes
        status = $updatedAppointment.status
    }
}
catch {
    Add-Check -Name "appointment reschedule lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentRescheduleId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRescheduleId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentPatientOverlapPrimaryId = $null
$appointmentPatientOverlapSecondaryId = $null
try {
    $primaryPatientOverlapBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = "Smoke Appointment Patient Overlap A"
        date = "2026-12-05"
        startTime = "09:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Overlap"
        comments = "Smoke patient overlap primary appointment"
    } | ConvertTo-Json
    $secondaryPatientOverlapBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 102
        title = "Smoke Appointment Patient Overlap B"
        date = "2026-12-05"
        startTime = "09:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Overlap"
        comments = "Smoke patient overlap secondary appointment"
    } | ConvertTo-Json

    $primaryPatientOverlapAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $primaryPatientOverlapBody -TimeoutSec 20
    $appointmentPatientOverlapPrimaryId = $primaryPatientOverlapAppointment.id
    $secondaryPatientOverlapAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $secondaryPatientOverlapBody -TimeoutSec 20
    $appointmentPatientOverlapSecondaryId = $secondaryPatientOverlapAppointment.id

    $primaryPatientOverlapDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPatientOverlapPrimaryId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $secondaryPatientOverlapDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPatientOverlapSecondaryId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $appointmentPatientOverlapPassed = $primaryPatientOverlapDetail.patientOverlapCount -eq 1 `
        -and $secondaryPatientOverlapDetail.patientOverlapCount -eq 1 `
        -and ($primaryPatientOverlapDetail.patientOverlapAppointmentIds -contains $appointmentPatientOverlapSecondaryId) `
        -and ($secondaryPatientOverlapDetail.patientOverlapAppointmentIds -contains $appointmentPatientOverlapPrimaryId)

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPatientOverlapSecondaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentPatientOverlapSecondaryId = $null
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPatientOverlapPrimaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentPatientOverlapPrimaryId = $null

    Add-Check -Name "appointment patient overlap tolerance" -Result $(if ($appointmentPatientOverlapPassed) { "passed" } else { "failed" }) -Details @{
        primaryId = $primaryPatientOverlapAppointment.id
        secondaryId = $secondaryPatientOverlapAppointment.id
        primaryPatientOverlapCount = $primaryPatientOverlapDetail.patientOverlapCount
        secondaryPatientOverlapCount = $secondaryPatientOverlapDetail.patientOverlapCount
        primaryPatientOverlapAppointmentIds = $primaryPatientOverlapDetail.patientOverlapAppointmentIds
        secondaryPatientOverlapAppointmentIds = $secondaryPatientOverlapDetail.patientOverlapAppointmentIds
    }
}
catch {
    Add-Check -Name "appointment patient overlap tolerance" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentPatientOverlapSecondaryId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPatientOverlapSecondaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    if ($null -ne $appointmentPatientOverlapPrimaryId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPatientOverlapPrimaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentRoomOverlapPrimaryId = $null
$appointmentRoomOverlapSecondaryId = $null
try {
    $primaryRoomOverlapBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = "Smoke Appointment Room Overlap A"
        date = "2026-12-06"
        startTime = "09:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Smoke Room Overlap"
        comments = "Smoke room overlap primary appointment"
    } | ConvertTo-Json
    $secondaryRoomOverlapBody = @{
        patientId = "MOD-PAT-0004"
        providerId = 102
        title = "Smoke Appointment Room Overlap B"
        date = "2026-12-06"
        startTime = "09:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Smoke Room Overlap"
        comments = "Smoke room overlap secondary appointment"
    } | ConvertTo-Json

    $primaryRoomOverlapAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $primaryRoomOverlapBody -TimeoutSec 20
    $appointmentRoomOverlapPrimaryId = $primaryRoomOverlapAppointment.id
    $secondaryRoomOverlapAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $secondaryRoomOverlapBody -TimeoutSec 20
    $appointmentRoomOverlapSecondaryId = $secondaryRoomOverlapAppointment.id

    $primaryRoomOverlapDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRoomOverlapPrimaryId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $secondaryRoomOverlapDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRoomOverlapSecondaryId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $appointmentRoomOverlapPassed = $primaryRoomOverlapDetail.roomOverlapCount -eq 1 `
        -and $secondaryRoomOverlapDetail.roomOverlapCount -eq 1 `
        -and ($primaryRoomOverlapDetail.roomOverlapAppointmentIds -contains $appointmentRoomOverlapSecondaryId) `
        -and ($secondaryRoomOverlapDetail.roomOverlapAppointmentIds -contains $appointmentRoomOverlapPrimaryId)

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRoomOverlapSecondaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentRoomOverlapSecondaryId = $null
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRoomOverlapPrimaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentRoomOverlapPrimaryId = $null

    Add-Check -Name "appointment room overlap tolerance" -Result $(if ($appointmentRoomOverlapPassed) { "passed" } else { "failed" }) -Details @{
        primaryId = $primaryRoomOverlapAppointment.id
        secondaryId = $secondaryRoomOverlapAppointment.id
        primaryRoomOverlapCount = $primaryRoomOverlapDetail.roomOverlapCount
        secondaryRoomOverlapCount = $secondaryRoomOverlapDetail.roomOverlapCount
        primaryRoomOverlapAppointmentIds = $primaryRoomOverlapDetail.roomOverlapAppointmentIds
        secondaryRoomOverlapAppointmentIds = $secondaryRoomOverlapDetail.roomOverlapAppointmentIds
    }
}
catch {
    Add-Check -Name "appointment room overlap tolerance" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentRoomOverlapSecondaryId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRoomOverlapSecondaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    if ($null -ne $appointmentRoomOverlapPrimaryId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRoomOverlapPrimaryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentArrivalId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = $null
        title = "Smoke Appointment Arrival"
        date = "2026-10-29"
        startTime = "09:00"
        durationMinutes = 30
        facilityId = $null
        categoryId = 9
        room = "Arrival"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentArrivalId = $createdAppointment.id

    $arrivalBody = @{
        status = "@"
        title = "Smoke Appointment Arrival Arrived"
    } | ConvertTo-Json
    $arrivedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentArrivalId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $arrivalBody -TimeoutSec 20
    $appointmentArrivalPassed = $createdAppointment.status -eq "-" `
        -and $arrivedAppointment.status -eq "@" `
        -and $arrivedAppointment.title -eq "Smoke Appointment Arrival Arrived" `
        -and $arrivedAppointment.date -eq "2026-10-29" `
        -and $arrivedAppointment.startTime -eq "09:00" `
        -and $arrivedAppointment.durationMinutes -eq 30 `
        -and $arrivedAppointment.room -eq "Arrival"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentArrivalId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentArrivalId = $null

    Add-Check -Name "appointment arrival lifecycle" -Result $(if ($appointmentArrivalPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        title = $arrivedAppointment.title
        date = $arrivedAppointment.date
        startTime = $arrivedAppointment.startTime
        durationMinutes = $arrivedAppointment.durationMinutes
        status = $arrivedAppointment.status
    }
}
catch {
    Add-Check -Name "appointment arrival lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentArrivalId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentArrivalId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentCheckoutId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = $null
        title = "Smoke Appointment Checkout"
        date = "2026-11-05"
        startTime = "11:00"
        durationMinutes = 30
        facilityId = $null
        categoryId = 9
        room = "Checkout"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentCheckoutId = $createdAppointment.id

    $arrivalBody = @{
        status = "@"
        title = "Smoke Appointment Checkout Arrived"
    } | ConvertTo-Json
    $arrivedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCheckoutId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $arrivalBody -TimeoutSec 20

    $checkoutBody = @{
        status = ">"
        title = "Smoke Appointment Checkout Checked Out"
    } | ConvertTo-Json
    $checkedOutAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCheckoutId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $checkoutBody -TimeoutSec 20
    $appointmentCheckoutPassed = $createdAppointment.status -eq "-" `
        -and $arrivedAppointment.status -eq "@" `
        -and $checkedOutAppointment.status -eq ">" `
        -and $checkedOutAppointment.title -eq "Smoke Appointment Checkout Checked Out" `
        -and $checkedOutAppointment.date -eq "2026-11-05" `
        -and $checkedOutAppointment.startTime -eq "11:00" `
        -and $checkedOutAppointment.durationMinutes -eq 30 `
        -and $checkedOutAppointment.room -eq "Checkout"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCheckoutId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentCheckoutId = $null

    Add-Check -Name "appointment check-out lifecycle" -Result $(if ($appointmentCheckoutPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        arrivedTitle = $arrivedAppointment.title
        checkedOutTitle = $checkedOutAppointment.title
        date = $checkedOutAppointment.date
        startTime = $checkedOutAppointment.startTime
        durationMinutes = $checkedOutAppointment.durationMinutes
        status = $checkedOutAppointment.status
    }
}
catch {
    Add-Check -Name "appointment check-out lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentCheckoutId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCheckoutId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentNoShowId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = $null
        title = "Smoke Appointment Missed"
        date = "2026-11-12"
        startTime = "13:00"
        durationMinutes = 30
        facilityId = $null
        categoryId = 9
        room = "NoShow"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentNoShowId = $createdAppointment.id

    $noShowBody = @{
        status = "?"
        title = "Smoke Appointment Missed No Show"
    } | ConvertTo-Json
    $noShowAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentNoShowId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $noShowBody -TimeoutSec 20
    $appointmentNoShowPassed = $createdAppointment.status -eq "-" `
        -and $noShowAppointment.status -eq "?" `
        -and $noShowAppointment.title -eq "Smoke Appointment Missed No Show" `
        -and $noShowAppointment.date -eq "2026-11-12" `
        -and $noShowAppointment.startTime -eq "13:00" `
        -and $noShowAppointment.durationMinutes -eq 30 `
        -and $noShowAppointment.room -eq "NoShow"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentNoShowId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentNoShowId = $null

    Add-Check -Name "appointment no-show lifecycle" -Result $(if ($appointmentNoShowPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        title = $noShowAppointment.title
        date = $noShowAppointment.date
        startTime = $noShowAppointment.startTime
        durationMinutes = $noShowAppointment.durationMinutes
        status = $noShowAppointment.status
    }
}
catch {
    Add-Check -Name "appointment no-show lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentNoShowId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentNoShowId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentCategoryId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = $null
        title = "Smoke Appointment Category"
        date = "2026-11-19"
        startTime = "09:15"
        durationMinutes = 30
        facilityId = $null
        categoryId = 13
        room = "Category"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentCategoryId = $createdAppointment.id

    $updateBody = @{
        providerId = $null
        title = "Smoke Appointment Category"
        date = "2026-11-19"
        startTime = "09:15"
        durationMinutes = 30
        facilityId = $null
        categoryId = 10
        room = "Category"
        status = "-"
    } | ConvertTo-Json
    $updatedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCategoryId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $appointmentCategoryPassed = $createdAppointment.categoryId -eq 13 `
        -and $createdAppointment.categoryName -eq "Preventive Care Services" `
        -and $updatedAppointment.categoryId -eq 10 `
        -and $updatedAppointment.categoryName -eq "New Patient" `
        -and $updatedAppointment.date -eq "2026-11-19" `
        -and $updatedAppointment.startTime -eq "09:15" `
        -and $updatedAppointment.room -eq "Category"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCategoryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentCategoryId = $null

    Add-Check -Name "appointment category lifecycle" -Result $(if ($appointmentCategoryPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        createdCategoryId = $createdAppointment.categoryId
        createdCategoryName = $createdAppointment.categoryName
        updatedCategoryId = $updatedAppointment.categoryId
        updatedCategoryName = $updatedAppointment.categoryName
    }
}
catch {
    Add-Check -Name "appointment category lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentCategoryId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCategoryId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentPendingId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = $null
        title = "Smoke Appointment Pending"
        date = "2026-11-26"
        startTime = "10:45"
        durationMinutes = 30
        facilityId = $null
        categoryId = 9
        room = "Pending"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentPendingId = $createdAppointment.id

    $updateBody = @{
        providerId = $null
        title = "Smoke Appointment Pending Status"
        date = "2026-11-26"
        startTime = "10:45"
        durationMinutes = 30
        facilityId = $null
        categoryId = 9
        room = "Pending"
        status = "~"
    } | ConvertTo-Json
    $pendingAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPendingId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $appointmentPendingPassed = $createdAppointment.status -eq "-" `
        -and $pendingAppointment.status -eq "~" `
        -and $pendingAppointment.title -eq "Smoke Appointment Pending Status" `
        -and $pendingAppointment.date -eq "2026-11-26" `
        -and $pendingAppointment.startTime -eq "10:45" `
        -and $pendingAppointment.durationMinutes -eq 30 `
        -and $pendingAppointment.room -eq "Pending"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPendingId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentPendingId = $null

    Add-Check -Name "appointment pending-status lifecycle" -Result $(if ($appointmentPendingPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        title = $pendingAppointment.title
        date = $pendingAppointment.date
        startTime = $pendingAppointment.startTime
        durationMinutes = $pendingAppointment.durationMinutes
        status = $pendingAppointment.status
    }
}
catch {
    Add-Check -Name "appointment pending-status lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentPendingId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentPendingId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentProviderId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = "Smoke Appointment Provider"
        date = "2026-12-03"
        startTime = "11:45"
        durationMinutes = 30
        facilityId = 10
        categoryId = 9
        room = "Provider"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentProviderId = $createdAppointment.id

    $updateBody = @{
        providerId = 102
        title = "Smoke Appointment Provider Reassigned"
        date = "2026-12-03"
        startTime = "11:45"
        durationMinutes = 30
        facilityId = 10
        categoryId = 9
        room = "Provider"
        status = "-"
    } | ConvertTo-Json
    $reassignedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentProviderId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $appointmentProviderPassed = $createdAppointment.providerId -eq 101 `
        -and $createdAppointment.facilityId -eq 10 `
        -and $reassignedAppointment.providerId -eq 102 `
        -and $reassignedAppointment.facilityId -eq 10 `
        -and $reassignedAppointment.title -eq "Smoke Appointment Provider Reassigned" `
        -and $reassignedAppointment.date -eq "2026-12-03" `
        -and $reassignedAppointment.startTime -eq "11:45" `
        -and $reassignedAppointment.room -eq "Provider"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentProviderId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentProviderId = $null

    Add-Check -Name "appointment provider reassignment lifecycle" -Result $(if ($appointmentProviderPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        createdProviderId = $createdAppointment.providerId
        reassignedProviderId = $reassignedAppointment.providerId
        facilityId = $reassignedAppointment.facilityId
        title = $reassignedAppointment.title
    }
}
catch {
    Add-Check -Name "appointment provider reassignment lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentProviderId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentProviderId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentFacilityId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = "Smoke Appointment Facility"
        date = "2026-12-10"
        startTime = "10:00"
        durationMinutes = 30
        facilityId = 10
        categoryId = 9
        room = "Facility"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentFacilityId = $createdAppointment.id

    $updateBody = @{
        providerId = 101
        title = "Smoke Appointment Facility Reassigned"
        date = "2026-12-10"
        startTime = "10:00"
        durationMinutes = 30
        facilityId = 11
        categoryId = 9
        room = "Facility"
        status = "-"
    } | ConvertTo-Json
    $reassignedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentFacilityId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $appointmentFacilityPassed = $createdAppointment.facilityId -eq 10 `
        -and $reassignedAppointment.facilityId -eq 11 `
        -and $reassignedAppointment.providerId -eq 101 `
        -and $reassignedAppointment.title -eq "Smoke Appointment Facility Reassigned" `
        -and $reassignedAppointment.date -eq "2026-12-10" `
        -and $reassignedAppointment.startTime -eq "10:00" `
        -and $reassignedAppointment.room -eq "Facility"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentFacilityId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentFacilityId = $null

    Add-Check -Name "appointment facility reassignment lifecycle" -Result $(if ($appointmentFacilityPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        createdFacilityId = $createdAppointment.facilityId
        reassignedFacilityId = $reassignedAppointment.facilityId
        providerId = $reassignedAppointment.providerId
        title = $reassignedAppointment.title
    }
}
catch {
    Add-Check -Name "appointment facility reassignment lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentFacilityId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentFacilityId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentBillingLocationId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = "Smoke Appointment Billing Location"
        date = "2026-12-17"
        startTime = "09:15"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "BillingLoc"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentBillingLocationId = $createdAppointment.id

    $updateBody = @{
        providerId = 101
        title = "Smoke Appointment Billing Location Reassigned"
        date = "2026-12-17"
        startTime = "09:15"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 11
        categoryId = 9
        room = "BillingLoc"
        status = "-"
    } | ConvertTo-Json
    $reassignedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentBillingLocationId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $appointmentBillingLocationPassed = $createdAppointment.facilityId -eq 10 `
        -and $createdAppointment.billingLocationId -eq 10 `
        -and $reassignedAppointment.facilityId -eq 10 `
        -and $reassignedAppointment.billingLocationId -eq 11 `
        -and $reassignedAppointment.providerId -eq 101 `
        -and $reassignedAppointment.title -eq "Smoke Appointment Billing Location Reassigned" `
        -and $reassignedAppointment.date -eq "2026-12-17" `
        -and $reassignedAppointment.startTime -eq "09:15" `
        -and $reassignedAppointment.room -eq "BillingLoc"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentBillingLocationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentBillingLocationId = $null

    Add-Check -Name "appointment billing-location reassignment lifecycle" -Result $(if ($appointmentBillingLocationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        facilityId = $reassignedAppointment.facilityId
        createdBillingLocationId = $createdAppointment.billingLocationId
        reassignedBillingLocationId = $reassignedAppointment.billingLocationId
        providerId = $reassignedAppointment.providerId
        title = $reassignedAppointment.title
    }
}
catch {
    Add-Check -Name "appointment billing-location reassignment lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentBillingLocationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentBillingLocationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentCommentsId = $null
try {
    $initialComments = "Smoke scheduling comments for the appointment note lifecycle."
    $updatedComments = "Updated smoke scheduling comments with referral packet reminder."
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = "Smoke Appointment Comments"
        date = "2026-12-24"
        startTime = "08:30"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Comments"
        comments = $initialComments
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentCommentsId = $createdAppointment.id

    $updateBody = @{
        providerId = 101
        title = "Smoke Appointment Comments Updated"
        date = "2026-12-24"
        startTime = "08:30"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Comments"
        status = "-"
        comments = $updatedComments
    } | ConvertTo-Json
    $updatedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCommentsId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $appointmentCommentsPassed = $createdAppointment.comments -eq $initialComments `
        -and $updatedAppointment.comments -eq $updatedComments `
        -and $updatedAppointment.title -eq "Smoke Appointment Comments Updated" `
        -and $updatedAppointment.date -eq "2026-12-24" `
        -and $updatedAppointment.startTime -eq "08:30" `
        -and $updatedAppointment.room -eq "Comments"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCommentsId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentCommentsId = $null

    Add-Check -Name "appointment comments lifecycle" -Result $(if ($appointmentCommentsPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        initialComments = $createdAppointment.comments
        updatedComments = $updatedAppointment.comments
        title = $updatedAppointment.title
    }
}
catch {
    Add-Check -Name "appointment comments lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentCommentsId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentCommentsId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentRecurrenceId = $null
try {
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = "Smoke Appointment Recurrence"
        date = "2026-12-29"
        startTime = "10:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Repeat"
        comments = "Smoke recurrence metadata create."
        recurrenceType = 1
        repeatFrequency = 1
        repeatUnit = 1
        recurrenceEndDate = "2026-12-31"
    } | ConvertTo-Json
    $createdAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentRecurrenceId = $createdAppointment.id

    $updateBody = @{
        providerId = 101
        title = "Smoke Appointment Recurrence Updated"
        date = "2026-12-29"
        startTime = "10:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Repeat"
        status = "-"
        comments = "Smoke recurrence metadata update."
        recurrenceType = 1
        repeatFrequency = 2
        repeatUnit = 1
        recurrenceEndDate = "2027-01-28"
    } | ConvertTo-Json
    $updatedAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRecurrenceId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20
    $appointmentRecurrencePassed = $createdAppointment.recurrenceType -eq 1 `
        -and $createdAppointment.repeatFrequency -eq 1 `
        -and $createdAppointment.repeatUnit -eq 1 `
        -and $createdAppointment.recurrenceEndDate -eq "2026-12-31" `
        -and $createdAppointment.recurrenceLabel -eq "Every week until 2026-12-31" `
        -and $updatedAppointment.repeatFrequency -eq 2 `
        -and $updatedAppointment.repeatUnit -eq 1 `
        -and $updatedAppointment.recurrenceEndDate -eq "2027-01-28" `
        -and $updatedAppointment.recurrenceLabel -eq "Every 2 weeks until 2027-01-28" `
        -and $updatedAppointment.title -eq "Smoke Appointment Recurrence Updated"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRecurrenceId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentRecurrenceId = $null

    Add-Check -Name "appointment recurrence metadata lifecycle" -Result $(if ($appointmentRecurrencePassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdAppointment.id
        createdLabel = $createdAppointment.recurrenceLabel
        updatedLabel = $updatedAppointment.recurrenceLabel
        updatedFrequency = $updatedAppointment.repeatFrequency
    }
}
catch {
    Add-Check -Name "appointment recurrence metadata lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentRecurrenceId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentRecurrenceId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentMonthlyRecurrenceId = $null
try {
    $monthlyTitle = "Smoke Monthly Recurrence $([Guid]::NewGuid().ToString('N').Substring(0, 12))"
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = $monthlyTitle
        date = "2026-12-15"
        startTime = "11:00"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "Monthly"
        comments = "Smoke monthly recurrence create."
        recurrenceType = 1
        repeatFrequency = 1
        repeatUnit = 2
        recurrenceEndDate = "2027-04-15"
        recurrenceExdates = @()
    } | ConvertTo-Json -Depth 5
    $createdMonthly = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentMonthlyRecurrenceId = $createdMonthly.id

    $monthlySearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-12-15&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $monthlyBefore = @($monthlySearch.appointments | Where-Object { $_.title -eq $monthlyTitle })
    $monthlyBeforeDates = @($monthlyBefore | ForEach-Object { $_.date })
    $monthlyBeforeNumbers = @($monthlyBefore | ForEach-Object { $_.occurrenceNumber })

    $encodedMonthlyId = [System.Uri]::EscapeDataString($appointmentMonthlyRecurrenceId)
    $updateBody = @{
        providerId = $createdMonthly.providerId
        title = "$monthlyTitle Updated"
        date = $createdMonthly.date
        startTime = $createdMonthly.startTime
        durationMinutes = $createdMonthly.durationMinutes
        facilityId = $createdMonthly.facilityId
        billingLocationId = $createdMonthly.billingLocationId
        categoryId = $createdMonthly.categoryId
        room = $createdMonthly.room
        status = $createdMonthly.status
        comments = "Smoke monthly recurrence update."
        recurrenceType = 1
        repeatFrequency = 2
        repeatUnit = 2
        recurrenceEndDate = "2027-08-15"
        recurrenceExdates = @()
    } | ConvertTo-Json -Depth 5
    $updatedMonthly = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedMonthlyId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $updateBody -TimeoutSec 20

    $monthlyAfterSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-12-15&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $monthlyAfter = @($monthlyAfterSearch.appointments | Where-Object { $_.title -eq "$monthlyTitle Updated" })
    $monthlyAfterDates = @($monthlyAfter | ForEach-Object { $_.date })
    $monthlyAfterNumbers = @($monthlyAfter | ForEach-Object { $_.occurrenceNumber })
    $monthlyGenerated = $monthlyAfter | Where-Object { $_.date -eq "2027-02-15" } | Select-Object -First 1
    $appointmentMonthlyRecurrencePassed = $createdMonthly.repeatUnit -eq 2 `
        -and $createdMonthly.recurrenceLabel -eq "Every month until 2027-04-15" `
        -and (($monthlyBeforeDates -join ",") -eq "2026-12-15,2027-01-15,2027-02-15,2027-03-15,2027-04-15") `
        -and (($monthlyBeforeNumbers -join ",") -eq "1,2,3,4,5") `
        -and $updatedMonthly.repeatFrequency -eq 2 `
        -and $updatedMonthly.repeatUnit -eq 2 `
        -and $updatedMonthly.recurrenceLabel -eq "Every 2 months until 2027-08-15" `
        -and (($monthlyAfterDates -join ",") -eq "2026-12-15,2027-02-15,2027-04-15,2027-06-15,2027-08-15") `
        -and (($monthlyAfterNumbers -join ",") -eq "1,2,3,4,5") `
        -and $null -ne $monthlyGenerated `
        -and $monthlyGenerated.isVirtualOccurrence

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedMonthlyId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentMonthlyRecurrenceId = $null

    Add-Check -Name "appointment monthly recurrence lifecycle" -Result $(if ($appointmentMonthlyRecurrencePassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdMonthly.id
        createdLabel = $createdMonthly.recurrenceLabel
        createdDates = $monthlyBeforeDates
        updatedLabel = $updatedMonthly.recurrenceLabel
        updatedDates = $monthlyAfterDates
        generatedOccurrenceId = $monthlyGenerated.id
    }
}
catch {
    Add-Check -Name "appointment monthly recurrence lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentMonthlyRecurrenceId) {
        try {
            $encodedMonthlyId = [System.Uri]::EscapeDataString($appointmentMonthlyRecurrenceId)
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedMonthlyId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentMonthlyRepeatOnId = $null
try {
    $repeatOnTitle = "Smoke Monthly Repeat-On $([Guid]::NewGuid().ToString('N').Substring(0, 12))"
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = $repeatOnTitle
        date = "2026-12-08"
        startTime = "09:10"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "RepeatNth"
        comments = "Smoke monthly repeat-on recurrence create."
        recurrenceType = 2
        repeatOnNum = 2
        repeatOnDay = 2
        repeatOnFrequency = 1
        recurrenceEndDate = "2027-04-30"
        recurrenceExdates = @()
    } | ConvertTo-Json -Depth 5
    $createdRepeatOn = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentMonthlyRepeatOnId = $createdRepeatOn.id

    $repeatOnSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-12-08&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $repeatOnOccurrences = @($repeatOnSearch.appointments | Where-Object { $_.title -eq $repeatOnTitle })
    $repeatOnDates = @($repeatOnOccurrences | ForEach-Object { $_.date })
    $repeatOnNumbers = @($repeatOnOccurrences | ForEach-Object { $_.occurrenceNumber })
    $repeatOnGenerated = $repeatOnOccurrences | Where-Object { $_.date -eq "2027-01-12" } | Select-Object -First 1

    $appointmentMonthlyRepeatOnPassed = $createdRepeatOn.recurrenceType -eq 2 `
        -and $null -eq $createdRepeatOn.repeatFrequency `
        -and $null -eq $createdRepeatOn.repeatUnit `
        -and $createdRepeatOn.repeatOnNum -eq 2 `
        -and $createdRepeatOn.repeatOnDay -eq 2 `
        -and $createdRepeatOn.repeatOnFrequency -eq 1 `
        -and $createdRepeatOn.recurrenceLabel -eq "Every month on the 2nd Tue until 2027-04-30" `
        -and (($repeatOnDates -join ",") -eq "2026-12-08,2027-01-12,2027-02-09,2027-03-09,2027-04-13") `
        -and (($repeatOnNumbers -join ",") -eq "1,2,3,4,5") `
        -and $null -ne $repeatOnGenerated `
        -and $repeatOnGenerated.isVirtualOccurrence

    $encodedRepeatOnId = [System.Uri]::EscapeDataString($appointmentMonthlyRepeatOnId)
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedRepeatOnId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentMonthlyRepeatOnId = $null

    Add-Check -Name "appointment monthly repeat-on recurrence lifecycle" -Result $(if ($appointmentMonthlyRepeatOnPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdRepeatOn.id
        createdLabel = $createdRepeatOn.recurrenceLabel
        dates = $repeatOnDates
        generatedOccurrenceId = $repeatOnGenerated.id
    }
}
catch {
    Add-Check -Name "appointment monthly repeat-on recurrence lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentMonthlyRepeatOnId) {
        try {
            $encodedRepeatOnId = [System.Uri]::EscapeDataString($appointmentMonthlyRepeatOnId)
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedRepeatOnId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentRecurrenceUnitMatrixIds = @()
try {
    $unitSuffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
    $unitScenarios = @(
        @{
            key = "daily"
            title = "Smoke Daily Recurrence $unitSuffix"
            date = "2026-12-07"
            startTime = "08:00"
            room = "Daily"
            repeatFrequency = 2
            repeatUnit = 0
            endDate = "2026-12-13"
            expectedLabel = "Every 2 days until 2026-12-13"
            expectedDates = @("2026-12-07", "2026-12-09", "2026-12-11", "2026-12-13")
        },
        @{
            key = "workday"
            title = "Smoke Workday Recurrence $unitSuffix"
            date = "2026-12-11"
            startTime = "09:00"
            room = "Workday"
            repeatFrequency = 1
            repeatUnit = 4
            endDate = "2026-12-16"
            expectedLabel = "Every workday until 2026-12-16"
            expectedDates = @("2026-12-11", "2026-12-14", "2026-12-15", "2026-12-16")
        },
        @{
            key = "yearly"
            title = "Smoke Yearly Recurrence $unitSuffix"
            date = "2026-06-30"
            startTime = "10:00"
            room = "Yearly"
            repeatFrequency = 1
            repeatUnit = 3
            endDate = "2028-06-30"
            expectedLabel = "Every year until 2028-06-30"
            expectedDates = @("2026-06-30", "2027-06-30", "2028-06-30")
        }
    )
    $unitResults = @()

    foreach ($scenario in $unitScenarios) {
        $createBody = @{
            patientId = "MOD-PAT-0003"
            providerId = 101
            title = $scenario.title
            date = $scenario.date
            startTime = $scenario.startTime
            durationMinutes = 30
            facilityId = 10
            billingLocationId = 10
            categoryId = 9
            room = $scenario.room
            comments = "Smoke recurrence unit matrix create."
            recurrenceType = 1
            repeatFrequency = $scenario.repeatFrequency
            repeatUnit = $scenario.repeatUnit
            recurrenceEndDate = $scenario.endDate
            recurrenceExdates = @()
        } | ConvertTo-Json -Depth 5
        $createdUnit = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
        $appointmentRecurrenceUnitMatrixIds += $createdUnit.id

        $unitSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=$($scenario.date)&limit=100" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
        $unitMatches = @($unitSearch.appointments | Where-Object { $_.title -eq $scenario.title })
        $unitDates = @($unitMatches | ForEach-Object { $_.date })
        $unitNumbers = @($unitMatches | ForEach-Object { $_.occurrenceNumber })
        $unitGenerated = $unitMatches | Where-Object { $_.isVirtualOccurrence } | Select-Object -First 1
        $unitResults += [pscustomobject]@{
            key = $scenario.key
            createdId = $createdUnit.id
            createdLabel = $createdUnit.recurrenceLabel
            dates = $unitDates
            numbers = $unitNumbers
            generatedOccurrenceId = $unitGenerated.id
            expectedLabel = $scenario.expectedLabel
            expectedDates = $scenario.expectedDates
        }
    }

    $appointmentRecurrenceUnitMatrixPassed = $true
    foreach ($result in $unitResults) {
        $expectedNumbers = 1..(@($result.expectedDates).Count)
        $appointmentRecurrenceUnitMatrixPassed = $appointmentRecurrenceUnitMatrixPassed `
            -and $result.createdLabel -eq $result.expectedLabel `
            -and (($result.dates -join ",") -eq ($result.expectedDates -join ",")) `
            -and (($result.numbers -join ",") -eq ($expectedNumbers -join ",")) `
            -and $null -ne $result.generatedOccurrenceId
    }

    foreach ($appointmentId in $appointmentRecurrenceUnitMatrixIds) {
        $encodedUnitId = [System.Uri]::EscapeDataString($appointmentId)
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedUnitId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    }
    $appointmentRecurrenceUnitMatrixIds = @()

    Add-Check -Name "appointment recurrence unit matrix lifecycle" -Result $(if ($appointmentRecurrenceUnitMatrixPassed) { "passed" } else { "failed" }) -Details @{
        units = $unitResults
    }
}
catch {
    Add-Check -Name "appointment recurrence unit matrix lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    foreach ($appointmentId in $appointmentRecurrenceUnitMatrixIds) {
        try {
            $encodedUnitId = [System.Uri]::EscapeDataString($appointmentId)
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedUnitId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentDaysOfWeekRecurrenceId = $null
try {
    $daysTitle = "Smoke Days-of-Week Recurrence $([Guid]::NewGuid().ToString('N').Substring(0, 12))"
    $createBody = @{
        patientId = "MOD-PAT-0003"
        providerId = 101
        title = $daysTitle
        date = "2026-12-07"
        startTime = "08:45"
        durationMinutes = 30
        facilityId = 10
        billingLocationId = 10
        categoryId = 9
        room = "DaysWeek"
        comments = "Smoke days-of-week recurrence create."
        recurrenceType = 3
        repeatFrequency = $null
        repeatUnit = 6
        recurrenceDays = @(2, 4, 6)
        recurrenceEndDate = "2026-12-18"
        recurrenceExdates = @()
    } | ConvertTo-Json -Depth 5
    $createdDays = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBody -TimeoutSec 20
    $appointmentDaysOfWeekRecurrenceId = $createdDays.id

    $daysSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-12-07&limit=100" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $daysMatches = @($daysSearch.appointments | Where-Object { $_.title -eq $daysTitle })
    $daysDates = @($daysMatches | ForEach-Object { $_.date })
    $daysNumbers = @($daysMatches | ForEach-Object { $_.occurrenceNumber })
    $daysGenerated = $daysMatches | Where-Object { $_.date -eq "2026-12-09" } | Select-Object -First 1
    $appointmentDaysOfWeekRecurrencePassed = $createdDays.recurrenceType -eq 3 `
        -and $createdDays.repeatFrequency -eq $null `
        -and $createdDays.repeatUnit -eq 6 `
        -and (($createdDays.recurrenceDays -join ",") -eq "2,4,6") `
        -and $createdDays.recurrenceLabel -eq "Every week on Mon, Wed, Fri until 2026-12-18" `
        -and (($daysDates -join ",") -eq "2026-12-07,2026-12-09,2026-12-11,2026-12-14,2026-12-16,2026-12-18") `
        -and (($daysNumbers -join ",") -eq "1,2,3,4,5,6") `
        -and $null -ne $daysGenerated `
        -and $daysGenerated.isVirtualOccurrence `
        -and (($daysGenerated.recurrenceDays -join ",") -eq "2,4,6")

    $encodedDaysId = [System.Uri]::EscapeDataString($appointmentDaysOfWeekRecurrenceId)
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedDaysId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $appointmentDaysOfWeekRecurrenceId = $null

    Add-Check -Name "appointment days-of-week recurrence lifecycle" -Result $(if ($appointmentDaysOfWeekRecurrencePassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdDays.id
        createdLabel = $createdDays.recurrenceLabel
        recurrenceDays = $createdDays.recurrenceDays
        dates = $daysDates
        occurrenceNumbers = $daysNumbers
        generatedOccurrenceId = $daysGenerated.id
    }
}
catch {
    Add-Check -Name "appointment days-of-week recurrence lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentDaysOfWeekRecurrenceId) {
        try {
            $encodedDaysId = [System.Uri]::EscapeDataString($appointmentDaysOfWeekRecurrenceId)
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedDaysId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $seriesSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0003&from=2026-08-14&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesOccurrences = @($seriesSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $seriesDates = @($seriesOccurrences | ForEach-Object { $_.date })
    $expectedSeriesDates = @("2026-08-14", "2026-08-28", "2026-09-11", "2026-09-25", "2026-10-09")
    $appointmentSeriesPassed = $seriesOccurrences.Count -eq 5 `
        -and (($seriesDates -join ",") -eq ($expectedSeriesDates -join ",")) `
        -and $seriesOccurrences[0].isVirtualOccurrence `
        -and $seriesOccurrences[0].occurrenceNumber -eq 3 `
        -and $seriesOccurrences[0].recurrenceLabel -eq "Every 2 weeks until 2026-10-09"

    Add-Check -Name "appointment recurring series expansion" -Result $(if ($appointmentSeriesPassed) { "passed" } else { "failed" }) -Details @{
        firstOccurrenceId = $seriesOccurrences[0].id
        dates = $seriesDates
        totalMatches = $seriesSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment recurring series expansion" -Result "failed" -Details $_.Exception.Message
}

try {
    $exceptionSeriesSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-12-02&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $exceptionSeriesOccurrences = @($exceptionSeriesSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $exceptionSeriesDates = @($exceptionSeriesOccurrences | ForEach-Object { $_.date })
    $exceptionOccurrenceNumbers = @($exceptionSeriesOccurrences | ForEach-Object { $_.occurrenceNumber })
    $expectedExceptionSeriesDates = @("2026-12-02", "2026-12-30", "2027-01-13", "2027-01-27")
    $appointmentExceptionSeriesPassed = $exceptionSeriesOccurrences.Count -eq 4 `
        -and (($exceptionSeriesDates -join ",") -eq ($expectedExceptionSeriesDates -join ",")) `
        -and (($exceptionOccurrenceNumbers -join ",") -eq "3,5,6,7") `
        -and ($exceptionSeriesDates -notcontains "2026-12-16") `
        -and $exceptionSeriesOccurrences[0].recurrenceExceptionCount -eq 1 `
        -and ($exceptionSeriesOccurrences[0].recurrenceExdates -contains "2026-12-16")

    Add-Check -Name "appointment recurrence exception expansion" -Result $(if ($appointmentExceptionSeriesPassed) { "passed" } else { "failed" }) -Details @{
        skippedDate = "2026-12-16"
        dates = $exceptionSeriesDates
        occurrenceNumbers = $exceptionOccurrenceNumbers
        exceptionDates = $exceptionSeriesOccurrences[0].recurrenceExdates
        totalMatches = $exceptionSeriesSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment recurrence exception expansion" -Result "failed" -Details $_.Exception.Message
}

$appointmentOccurrenceCancelRootId = $null
try {
    $occurrenceCancelSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-12-02&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $occurrenceCancelBefore = @($occurrenceCancelSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $occurrenceToCancel = $occurrenceCancelBefore | Where-Object { $_.date -eq "2026-12-30" } | Select-Object -First 1
    if ($null -eq $occurrenceToCancel) {
        throw "Expected generated occurrence on 2026-12-30 before cancellation."
    }

    $appointmentOccurrenceCancelRootId = $occurrenceToCancel.seriesRootId
    $encodedOccurrenceId = [System.Uri]::EscapeDataString($occurrenceToCancel.id)
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedOccurrenceId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null

    $occurrenceCancelAfterSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-12-02&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $occurrenceCancelAfter = @($occurrenceCancelAfterSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $occurrenceCancelAfterDates = @($occurrenceCancelAfter | ForEach-Object { $_.date })
    $occurrenceCancelAfterNumbers = @($occurrenceCancelAfter | ForEach-Object { $_.occurrenceNumber })
    $rootAfterOccurrenceCancel = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceCancelRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $appointmentOccurrenceCancelPassed = $occurrenceCancelAfter.Count -eq 3 `
        -and (($occurrenceCancelAfterDates -join ",") -eq "2026-12-02,2027-01-13,2027-01-27") `
        -and (($occurrenceCancelAfterNumbers -join ",") -eq "3,6,7") `
        -and ($occurrenceCancelAfterDates -notcontains "2026-12-30") `
        -and $rootAfterOccurrenceCancel.recurrenceExceptionCount -eq 2 `
        -and ($rootAfterOccurrenceCancel.recurrenceExdates -contains "2026-12-16") `
        -and ($rootAfterOccurrenceCancel.recurrenceExdates -contains "2026-12-30")

    Add-Check -Name "appointment occurrence cancellation exception" -Result $(if ($appointmentOccurrenceCancelPassed) { "passed" } else { "failed" }) -Details @{
        cancelledDate = "2026-12-30"
        dates = $occurrenceCancelAfterDates
        occurrenceNumbers = $occurrenceCancelAfterNumbers
        exceptionDates = $rootAfterOccurrenceCancel.recurrenceExdates
        totalMatches = $occurrenceCancelAfterSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment occurrence cancellation exception" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentOccurrenceCancelRootId) {
        try {
            $rootToRestore = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceCancelRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
            $restoreBody = @{
                providerId = $rootToRestore.providerId
                title = $rootToRestore.title
                date = $rootToRestore.date
                startTime = $rootToRestore.startTime
                durationMinutes = $rootToRestore.durationMinutes
                facilityId = $rootToRestore.facilityId
                billingLocationId = $rootToRestore.billingLocationId
                categoryId = $rootToRestore.categoryId
                room = $rootToRestore.room
                status = $rootToRestore.status
                comments = $rootToRestore.comments
                recurrenceType = $rootToRestore.recurrenceType
                repeatFrequency = $rootToRestore.repeatFrequency
                repeatUnit = $rootToRestore.repeatUnit
                recurrenceEndDate = $rootToRestore.recurrenceEndDate
                recurrenceExdates = @("2026-12-16")
            } | ConvertTo-Json -Depth 5
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceCancelRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $restoreBody -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentOccurrenceRestoreRootId = $null
try {
    $occurrenceRestoreSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-12-02&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $occurrenceRestoreBefore = @($occurrenceRestoreSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $occurrenceToRestore = $occurrenceRestoreBefore | Where-Object { $_.date -eq "2026-12-30" } | Select-Object -First 1
    if ($null -eq $occurrenceToRestore) {
        throw "Expected generated occurrence on 2026-12-30 before restore smoke check."
    }

    $appointmentOccurrenceRestoreRootId = $occurrenceToRestore.seriesRootId
    $encodedOccurrenceToSkipId = [System.Uri]::EscapeDataString($occurrenceToRestore.id)
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedOccurrenceToSkipId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null

    $rootAfterSkipForRestore = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceRestoreRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $encodedRestoreRootId = [System.Uri]::EscapeDataString($appointmentOccurrenceRestoreRootId)
    $rootAfterRestore = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedRestoreRootId/recurrence-exceptions/2026-12-30/restore" -Method Post -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $occurrenceRestoreAfterSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-12-02&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $occurrenceRestoreAfter = @($occurrenceRestoreAfterSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $occurrenceRestoreAfterDates = @($occurrenceRestoreAfter | ForEach-Object { $_.date })
    $occurrenceRestoreAfterNumbers = @($occurrenceRestoreAfter | ForEach-Object { $_.occurrenceNumber })
    $appointmentOccurrenceRestorePassed = $rootAfterSkipForRestore.recurrenceExceptionCount -eq 2 `
        -and ($rootAfterSkipForRestore.recurrenceExdates -contains "2026-12-30") `
        -and $rootAfterRestore.recurrenceExceptionCount -eq 1 `
        -and ($rootAfterRestore.recurrenceExdates -contains "2026-12-16") `
        -and ($rootAfterRestore.recurrenceExdates -notcontains "2026-12-30") `
        -and $occurrenceRestoreAfter.Count -eq 4 `
        -and (($occurrenceRestoreAfterDates -join ",") -eq "2026-12-02,2026-12-30,2027-01-13,2027-01-27") `
        -and (($occurrenceRestoreAfterNumbers -join ",") -eq "3,5,6,7")

    Add-Check -Name "appointment occurrence restore exception" -Result $(if ($appointmentOccurrenceRestorePassed) { "passed" } else { "failed" }) -Details @{
        restoredDate = "2026-12-30"
        dates = $occurrenceRestoreAfterDates
        occurrenceNumbers = $occurrenceRestoreAfterNumbers
        skippedExceptionDates = $rootAfterSkipForRestore.recurrenceExdates
        restoredExceptionDates = $rootAfterRestore.recurrenceExdates
        totalMatches = $occurrenceRestoreAfterSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment occurrence restore exception" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentOccurrenceRestoreRootId) {
        try {
            $rootToRestore = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceRestoreRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
            $restoreBody = @{
                providerId = $rootToRestore.providerId
                title = $rootToRestore.title
                date = $rootToRestore.date
                startTime = $rootToRestore.startTime
                durationMinutes = $rootToRestore.durationMinutes
                facilityId = $rootToRestore.facilityId
                billingLocationId = $rootToRestore.billingLocationId
                categoryId = $rootToRestore.categoryId
                room = $rootToRestore.room
                status = $rootToRestore.status
                comments = $rootToRestore.comments
                recurrenceType = $rootToRestore.recurrenceType
                repeatFrequency = $rootToRestore.repeatFrequency
                repeatUnit = $rootToRestore.repeatUnit
                recurrenceEndDate = $rootToRestore.recurrenceEndDate
                recurrenceExdates = @("2026-12-16")
            } | ConvertTo-Json -Depth 5
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceRestoreRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $restoreBody -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentOccurrenceRescheduleRootId = $null
$appointmentOccurrenceRescheduleStandaloneId = $null
try {
    $occurrenceRescheduleSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-12-02&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $occurrenceRescheduleBefore = @($occurrenceRescheduleSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $occurrenceToReschedule = $occurrenceRescheduleBefore | Where-Object { $_.date -eq "2026-12-30" } | Select-Object -First 1
    if ($null -eq $occurrenceToReschedule) {
        throw "Expected generated occurrence on 2026-12-30 before reschedule smoke check."
    }

    $appointmentOccurrenceRescheduleRootId = $occurrenceToReschedule.seriesRootId
    $rescheduleRoot = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceRescheduleRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $encodedRescheduleRootId = [System.Uri]::EscapeDataString($appointmentOccurrenceRescheduleRootId)
    $rescheduleBody = @{
        providerId = $rescheduleRoot.providerId
        title = $rescheduleRoot.title
        date = "2027-01-06"
        startTime = "14:00"
        durationMinutes = 45
        facilityId = $rescheduleRoot.facilityId
        billingLocationId = $rescheduleRoot.billingLocationId
        categoryId = $rescheduleRoot.categoryId
        room = $rescheduleRoot.room
        status = $rescheduleRoot.status
        comments = $rescheduleRoot.comments
    } | ConvertTo-Json -Depth 5

    $rescheduledAppointment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedRescheduleRootId/occurrences/2026-12-30/reschedule" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $rescheduleBody -TimeoutSec 20
    $appointmentOccurrenceRescheduleStandaloneId = $rescheduledAppointment.id

    $occurrenceRescheduleAfterSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-12-02&limit=20" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $occurrenceRescheduleAfter = @($occurrenceRescheduleAfterSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $occurrenceRescheduleAfterDates = @($occurrenceRescheduleAfter | ForEach-Object { $_.date })
    $occurrenceRescheduleAfterNumbers = @($occurrenceRescheduleAfter | ForEach-Object { $_.occurrenceNumber })
    $standaloneRescheduledAppointment = $occurrenceRescheduleAfterSearch.appointments | Where-Object { $_.id -eq $appointmentOccurrenceRescheduleStandaloneId } | Select-Object -First 1
    $rootAfterOccurrenceReschedule = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceRescheduleRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $appointmentOccurrenceReschedulePassed = $rootAfterOccurrenceReschedule.recurrenceExceptionCount -eq 2 `
        -and ($rootAfterOccurrenceReschedule.recurrenceExdates -contains "2026-12-16") `
        -and ($rootAfterOccurrenceReschedule.recurrenceExdates -contains "2026-12-30") `
        -and $occurrenceRescheduleAfter.Count -eq 3 `
        -and (($occurrenceRescheduleAfterDates -join ",") -eq "2026-12-02,2027-01-13,2027-01-27") `
        -and (($occurrenceRescheduleAfterNumbers -join ",") -eq "3,6,7") `
        -and $null -ne $standaloneRescheduledAppointment `
        -and $standaloneRescheduledAppointment.date -eq "2027-01-06" `
        -and $standaloneRescheduledAppointment.startTime -eq "14:00" `
        -and -not $standaloneRescheduledAppointment.isRecurringSeries

    Add-Check -Name "appointment occurrence reschedule exception" -Result $(if ($appointmentOccurrenceReschedulePassed) { "passed" } else { "failed" }) -Details @{
        originalDate = "2026-12-30"
        rescheduledDate = "2027-01-06"
        rescheduledStartTime = "14:00"
        standaloneId = $appointmentOccurrenceRescheduleStandaloneId
        dates = $occurrenceRescheduleAfterDates
        occurrenceNumbers = $occurrenceRescheduleAfterNumbers
        exceptionDates = $rootAfterOccurrenceReschedule.recurrenceExdates
        totalMatches = $occurrenceRescheduleAfterSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment occurrence reschedule exception" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentOccurrenceRescheduleStandaloneId) {
        try {
            $encodedStandaloneAppointmentId = [System.Uri]::EscapeDataString($appointmentOccurrenceRescheduleStandaloneId)
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedStandaloneAppointmentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }

    if ($null -ne $appointmentOccurrenceRescheduleRootId) {
        try {
            $rootToRestore = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceRescheduleRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
            $restoreBody = @{
                providerId = $rootToRestore.providerId
                title = $rootToRestore.title
                date = $rootToRestore.date
                startTime = $rootToRestore.startTime
                durationMinutes = $rootToRestore.durationMinutes
                facilityId = $rootToRestore.facilityId
                billingLocationId = $rootToRestore.billingLocationId
                categoryId = $rootToRestore.categoryId
                room = $rootToRestore.room
                status = $rootToRestore.status
                comments = $rootToRestore.comments
                recurrenceType = $rootToRestore.recurrenceType
                repeatFrequency = $rootToRestore.repeatFrequency
                repeatUnit = $rootToRestore.repeatUnit
                recurrenceEndDate = $rootToRestore.recurrenceEndDate
                recurrenceExdates = @("2026-12-16")
            } | ConvertTo-Json -Depth 5
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$appointmentOccurrenceRescheduleRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $restoreBody -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentRecurrenceExceptionEditRootId = $null
try {
    $recurrenceExceptionEditSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-11-04&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $recurrenceExceptionEditBefore = @($recurrenceExceptionEditSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $recurrenceExceptionRoot = $recurrenceExceptionEditBefore | Where-Object { $_.date -eq "2026-11-04" } | Select-Object -First 1
    if ($null -eq $recurrenceExceptionRoot) {
        throw "Expected recurring appointment root on 2026-11-04 before recurrence exception edit smoke check."
    }

    $appointmentRecurrenceExceptionEditRootId = $recurrenceExceptionRoot.seriesRootId
    $encodedExceptionEditRootId = [System.Uri]::EscapeDataString($appointmentRecurrenceExceptionEditRootId)
    $rootToEdit = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedExceptionEditRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $exceptionEditBody = @{
        providerId = $rootToEdit.providerId
        title = $rootToEdit.title
        date = $rootToEdit.date
        startTime = $rootToEdit.startTime
        durationMinutes = $rootToEdit.durationMinutes
        facilityId = $rootToEdit.facilityId
        billingLocationId = $rootToEdit.billingLocationId
        categoryId = $rootToEdit.categoryId
        room = $rootToEdit.room
        status = $rootToEdit.status
        comments = $rootToEdit.comments
        recurrenceType = $rootToEdit.recurrenceType
        repeatFrequency = $rootToEdit.repeatFrequency
        repeatUnit = $rootToEdit.repeatUnit
        recurrenceEndDate = $rootToEdit.recurrenceEndDate
        recurrenceExdates = @("2026-12-16", "2026-12-30")
    } | ConvertTo-Json -Depth 5

    $rootAfterExceptionEdit = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedExceptionEditRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $exceptionEditBody -TimeoutSec 20
    $recurrenceExceptionEditAfterSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-11-04&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $recurrenceExceptionEditAfter = @($recurrenceExceptionEditAfterSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $recurrenceExceptionEditAfterDates = @($recurrenceExceptionEditAfter | ForEach-Object { $_.date })
    $recurrenceExceptionEditAfterNumbers = @($recurrenceExceptionEditAfter | ForEach-Object { $_.occurrenceNumber })
    $appointmentRecurrenceExceptionEditPassed = $rootAfterExceptionEdit.recurrenceExceptionCount -eq 2 `
        -and ($rootAfterExceptionEdit.recurrenceExdates -contains "2026-12-16") `
        -and ($rootAfterExceptionEdit.recurrenceExdates -contains "2026-12-30") `
        -and $recurrenceExceptionEditAfter.Count -eq 5 `
        -and (($recurrenceExceptionEditAfterDates -join ",") -eq "2026-11-04,2026-11-18,2026-12-02,2027-01-13,2027-01-27") `
        -and (($recurrenceExceptionEditAfterNumbers -join ",") -eq "1,2,3,6,7")

    Add-Check -Name "appointment recurrence exception-list edit" -Result $(if ($appointmentRecurrenceExceptionEditPassed) { "passed" } else { "failed" }) -Details @{
        addedExceptionDate = "2026-12-30"
        dates = $recurrenceExceptionEditAfterDates
        occurrenceNumbers = $recurrenceExceptionEditAfterNumbers
        exceptionDates = $rootAfterExceptionEdit.recurrenceExdates
        totalMatches = $recurrenceExceptionEditAfterSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment recurrence exception-list edit" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentRecurrenceExceptionEditRootId) {
        try {
            $encodedExceptionEditRootId = [System.Uri]::EscapeDataString($appointmentRecurrenceExceptionEditRootId)
            $rootToRestore = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedExceptionEditRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
            $restoreBody = @{
                providerId = $rootToRestore.providerId
                title = $rootToRestore.title
                date = $rootToRestore.date
                startTime = $rootToRestore.startTime
                durationMinutes = $rootToRestore.durationMinutes
                facilityId = $rootToRestore.facilityId
                billingLocationId = $rootToRestore.billingLocationId
                categoryId = $rootToRestore.categoryId
                room = $rootToRestore.room
                status = $rootToRestore.status
                comments = $rootToRestore.comments
                recurrenceType = $rootToRestore.recurrenceType
                repeatFrequency = $rootToRestore.repeatFrequency
                repeatUnit = $rootToRestore.repeatUnit
                recurrenceEndDate = $rootToRestore.recurrenceEndDate
                recurrenceExdates = @("2026-12-16")
            } | ConvertTo-Json -Depth 5
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedExceptionEditRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $restoreBody -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentSeriesRootUpdateRootId = $null
$appointmentSeriesRootUpdateOriginalRoot = $null
try {
    $seriesRootUpdateSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-11-04&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesRootUpdateBefore = @($seriesRootUpdateSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $seriesRootUpdateRoot = $seriesRootUpdateBefore | Where-Object { $_.date -eq "2026-11-04" } | Select-Object -First 1
    if ($null -eq $seriesRootUpdateRoot) {
        throw "Expected recurring appointment root on 2026-11-04 before series root update smoke check."
    }

    $appointmentSeriesRootUpdateRootId = $seriesRootUpdateRoot.seriesRootId
    $encodedSeriesRootUpdateRootId = [System.Uri]::EscapeDataString($appointmentSeriesRootUpdateRootId)
    $appointmentSeriesRootUpdateOriginalRoot = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRootUpdateRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesRootUpdateBody = @{
        providerId = $appointmentSeriesRootUpdateOriginalRoot.providerId
        title = "Preventive Care Root Update"
        date = $appointmentSeriesRootUpdateOriginalRoot.date
        startTime = "16:15"
        durationMinutes = $appointmentSeriesRootUpdateOriginalRoot.durationMinutes
        facilityId = $appointmentSeriesRootUpdateOriginalRoot.facilityId
        billingLocationId = $appointmentSeriesRootUpdateOriginalRoot.billingLocationId
        categoryId = $appointmentSeriesRootUpdateOriginalRoot.categoryId
        room = $appointmentSeriesRootUpdateOriginalRoot.room
        status = $appointmentSeriesRootUpdateOriginalRoot.status
        comments = $appointmentSeriesRootUpdateOriginalRoot.comments
        recurrenceType = $appointmentSeriesRootUpdateOriginalRoot.recurrenceType
        repeatFrequency = $appointmentSeriesRootUpdateOriginalRoot.repeatFrequency
        repeatUnit = $appointmentSeriesRootUpdateOriginalRoot.repeatUnit
        recurrenceEndDate = $appointmentSeriesRootUpdateOriginalRoot.recurrenceEndDate
        recurrenceExdates = @($appointmentSeriesRootUpdateOriginalRoot.recurrenceExdates)
    } | ConvertTo-Json -Depth 5

    $rootAfterSeriesRootUpdate = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRootUpdateRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $seriesRootUpdateBody -TimeoutSec 20
    $seriesRootUpdateAfterSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-11-04&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesRootUpdateAfter = @($seriesRootUpdateAfterSearch.appointments | Where-Object { $_.title -eq "Preventive Care Root Update" -and $_.isRecurringSeries })
    $seriesRootUpdateAfterDates = @($seriesRootUpdateAfter | ForEach-Object { $_.date })
    $seriesRootUpdateAfterNumbers = @($seriesRootUpdateAfter | ForEach-Object { $_.occurrenceNumber })
    $seriesRootUpdateAfterStartTimes = @($seriesRootUpdateAfter | ForEach-Object { $_.startTime })
    $seriesRootUpdateGeneratedOccurrence = $seriesRootUpdateAfter | Where-Object { $_.date -eq "2026-11-18" } | Select-Object -First 1
    $appointmentSeriesRootUpdatePassed = $rootAfterSeriesRootUpdate.title -eq "Preventive Care Root Update" `
        -and $rootAfterSeriesRootUpdate.startTime -eq "16:15" `
        -and $rootAfterSeriesRootUpdate.recurrenceExceptionCount -eq 1 `
        -and ($rootAfterSeriesRootUpdate.recurrenceExdates -contains "2026-12-16") `
        -and $seriesRootUpdateAfter.Count -eq 6 `
        -and (($seriesRootUpdateAfterDates -join ",") -eq "2026-11-04,2026-11-18,2026-12-02,2026-12-30,2027-01-13,2027-01-27") `
        -and (($seriesRootUpdateAfterNumbers -join ",") -eq "1,2,3,5,6,7") `
        -and (($seriesRootUpdateAfterStartTimes -join ",") -eq "16:15,16:15,16:15,16:15,16:15,16:15") `
        -and $null -ne $seriesRootUpdateGeneratedOccurrence `
        -and $seriesRootUpdateGeneratedOccurrence.isVirtualOccurrence

    Add-Check -Name "appointment series root update propagation" -Result $(if ($appointmentSeriesRootUpdatePassed) { "passed" } else { "failed" }) -Details @{
        updatedTitle = $rootAfterSeriesRootUpdate.title
        updatedStartTime = $rootAfterSeriesRootUpdate.startTime
        dates = $seriesRootUpdateAfterDates
        occurrenceNumbers = $seriesRootUpdateAfterNumbers
        startTimes = $seriesRootUpdateAfterStartTimes
        exceptionDates = $rootAfterSeriesRootUpdate.recurrenceExdates
        totalMatches = $seriesRootUpdateAfterSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment series root update propagation" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentSeriesRootUpdateRootId) {
        try {
            $encodedSeriesRootUpdateRootId = [System.Uri]::EscapeDataString($appointmentSeriesRootUpdateRootId)
            if ($null -eq $appointmentSeriesRootUpdateOriginalRoot) {
                $appointmentSeriesRootUpdateOriginalRoot = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRootUpdateRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
            }

            $restoreBody = @{
                providerId = $appointmentSeriesRootUpdateOriginalRoot.providerId
                title = $appointmentSeriesRootUpdateOriginalRoot.title
                date = $appointmentSeriesRootUpdateOriginalRoot.date
                startTime = $appointmentSeriesRootUpdateOriginalRoot.startTime
                durationMinutes = $appointmentSeriesRootUpdateOriginalRoot.durationMinutes
                facilityId = $appointmentSeriesRootUpdateOriginalRoot.facilityId
                billingLocationId = $appointmentSeriesRootUpdateOriginalRoot.billingLocationId
                categoryId = $appointmentSeriesRootUpdateOriginalRoot.categoryId
                room = $appointmentSeriesRootUpdateOriginalRoot.room
                status = $appointmentSeriesRootUpdateOriginalRoot.status
                comments = $appointmentSeriesRootUpdateOriginalRoot.comments
                recurrenceType = $appointmentSeriesRootUpdateOriginalRoot.recurrenceType
                repeatFrequency = $appointmentSeriesRootUpdateOriginalRoot.repeatFrequency
                repeatUnit = $appointmentSeriesRootUpdateOriginalRoot.repeatUnit
                recurrenceEndDate = $appointmentSeriesRootUpdateOriginalRoot.recurrenceEndDate
                recurrenceExdates = @($appointmentSeriesRootUpdateOriginalRoot.recurrenceExdates)
            } | ConvertTo-Json -Depth 5
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRootUpdateRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $restoreBody -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentSeriesRecurrenceUpdateRootId = $null
$appointmentSeriesRecurrenceUpdateOriginalRoot = $null
try {
    $seriesRecurrenceUpdateSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-11-04&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesRecurrenceUpdateBefore = @($seriesRecurrenceUpdateSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $seriesRecurrenceUpdateRoot = $seriesRecurrenceUpdateBefore | Where-Object { $_.date -eq "2026-11-04" } | Select-Object -First 1
    if ($null -eq $seriesRecurrenceUpdateRoot) {
        throw "Expected recurring appointment root on 2026-11-04 before series recurrence update smoke check."
    }

    $appointmentSeriesRecurrenceUpdateRootId = $seriesRecurrenceUpdateRoot.seriesRootId
    $encodedSeriesRecurrenceUpdateRootId = [System.Uri]::EscapeDataString($appointmentSeriesRecurrenceUpdateRootId)
    $appointmentSeriesRecurrenceUpdateOriginalRoot = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRecurrenceUpdateRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesRecurrenceUpdateBody = @{
        providerId = $appointmentSeriesRecurrenceUpdateOriginalRoot.providerId
        title = $appointmentSeriesRecurrenceUpdateOriginalRoot.title
        date = $appointmentSeriesRecurrenceUpdateOriginalRoot.date
        startTime = $appointmentSeriesRecurrenceUpdateOriginalRoot.startTime
        durationMinutes = $appointmentSeriesRecurrenceUpdateOriginalRoot.durationMinutes
        facilityId = $appointmentSeriesRecurrenceUpdateOriginalRoot.facilityId
        billingLocationId = $appointmentSeriesRecurrenceUpdateOriginalRoot.billingLocationId
        categoryId = $appointmentSeriesRecurrenceUpdateOriginalRoot.categoryId
        room = $appointmentSeriesRecurrenceUpdateOriginalRoot.room
        status = $appointmentSeriesRecurrenceUpdateOriginalRoot.status
        comments = $appointmentSeriesRecurrenceUpdateOriginalRoot.comments
        recurrenceType = $appointmentSeriesRecurrenceUpdateOriginalRoot.recurrenceType
        repeatFrequency = 3
        repeatUnit = $appointmentSeriesRecurrenceUpdateOriginalRoot.repeatUnit
        recurrenceEndDate = "2027-02-10"
        recurrenceExdates = @($appointmentSeriesRecurrenceUpdateOriginalRoot.recurrenceExdates)
    } | ConvertTo-Json -Depth 5

    $rootAfterSeriesRecurrenceUpdate = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRecurrenceUpdateRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $seriesRecurrenceUpdateBody -TimeoutSec 20
    $seriesRecurrenceUpdateAfterSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-11-04&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesRecurrenceUpdateAfter = @($seriesRecurrenceUpdateAfterSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $seriesRecurrenceUpdateAfterDates = @($seriesRecurrenceUpdateAfter | ForEach-Object { $_.date })
    $seriesRecurrenceUpdateAfterNumbers = @($seriesRecurrenceUpdateAfter | ForEach-Object { $_.occurrenceNumber })
    $seriesRecurrenceUpdateAfterFrequencies = @($seriesRecurrenceUpdateAfter | ForEach-Object { $_.repeatFrequency })
    $seriesRecurrenceUpdateGeneratedOccurrence = $seriesRecurrenceUpdateAfter | Where-Object { $_.date -eq "2026-11-25" } | Select-Object -First 1
    $appointmentSeriesRecurrenceUpdatePassed = $rootAfterSeriesRecurrenceUpdate.repeatFrequency -eq 3 `
        -and $rootAfterSeriesRecurrenceUpdate.repeatUnit -eq 1 `
        -and $rootAfterSeriesRecurrenceUpdate.recurrenceEndDate -eq "2027-02-10" `
        -and $rootAfterSeriesRecurrenceUpdate.recurrenceLabel -eq "Every 3 weeks until 2027-02-10" `
        -and $rootAfterSeriesRecurrenceUpdate.recurrenceExceptionCount -eq 1 `
        -and ($rootAfterSeriesRecurrenceUpdate.recurrenceExdates -contains "2026-12-16") `
        -and $seriesRecurrenceUpdateAfter.Count -eq 4 `
        -and (($seriesRecurrenceUpdateAfterDates -join ",") -eq "2026-11-04,2026-11-25,2027-01-06,2027-01-27") `
        -and (($seriesRecurrenceUpdateAfterNumbers -join ",") -eq "1,2,4,5") `
        -and (($seriesRecurrenceUpdateAfterFrequencies -join ",") -eq "3,3,3,3") `
        -and $null -ne $seriesRecurrenceUpdateGeneratedOccurrence `
        -and $seriesRecurrenceUpdateGeneratedOccurrence.isVirtualOccurrence

    Add-Check -Name "appointment series recurrence update propagation" -Result $(if ($appointmentSeriesRecurrenceUpdatePassed) { "passed" } else { "failed" }) -Details @{
        updatedLabel = $rootAfterSeriesRecurrenceUpdate.recurrenceLabel
        dates = $seriesRecurrenceUpdateAfterDates
        occurrenceNumbers = $seriesRecurrenceUpdateAfterNumbers
        frequencies = $seriesRecurrenceUpdateAfterFrequencies
        exceptionDates = $rootAfterSeriesRecurrenceUpdate.recurrenceExdates
        totalMatches = $seriesRecurrenceUpdateAfterSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment series recurrence update propagation" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentSeriesRecurrenceUpdateRootId) {
        try {
            $encodedSeriesRecurrenceUpdateRootId = [System.Uri]::EscapeDataString($appointmentSeriesRecurrenceUpdateRootId)
            if ($null -eq $appointmentSeriesRecurrenceUpdateOriginalRoot) {
                $appointmentSeriesRecurrenceUpdateOriginalRoot = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRecurrenceUpdateRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
            }

            $seriesRecurrenceUpdateRestoreBody = @{
                providerId = $appointmentSeriesRecurrenceUpdateOriginalRoot.providerId
                title = $appointmentSeriesRecurrenceUpdateOriginalRoot.title
                date = $appointmentSeriesRecurrenceUpdateOriginalRoot.date
                startTime = $appointmentSeriesRecurrenceUpdateOriginalRoot.startTime
                durationMinutes = $appointmentSeriesRecurrenceUpdateOriginalRoot.durationMinutes
                facilityId = $appointmentSeriesRecurrenceUpdateOriginalRoot.facilityId
                billingLocationId = $appointmentSeriesRecurrenceUpdateOriginalRoot.billingLocationId
                categoryId = $appointmentSeriesRecurrenceUpdateOriginalRoot.categoryId
                room = $appointmentSeriesRecurrenceUpdateOriginalRoot.room
                status = $appointmentSeriesRecurrenceUpdateOriginalRoot.status
                comments = $appointmentSeriesRecurrenceUpdateOriginalRoot.comments
                recurrenceType = $appointmentSeriesRecurrenceUpdateOriginalRoot.recurrenceType
                repeatFrequency = $appointmentSeriesRecurrenceUpdateOriginalRoot.repeatFrequency
                repeatUnit = $appointmentSeriesRecurrenceUpdateOriginalRoot.repeatUnit
                recurrenceEndDate = $appointmentSeriesRecurrenceUpdateOriginalRoot.recurrenceEndDate
                recurrenceExdates = @($appointmentSeriesRecurrenceUpdateOriginalRoot.recurrenceExdates)
            } | ConvertTo-Json -Depth 5
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRecurrenceUpdateRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $seriesRecurrenceUpdateRestoreBody -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$appointmentSeriesRootMetadataRootId = $null
$appointmentSeriesRootMetadataOriginalRoot = $null
try {
    $seriesRootMetadataSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-11-04&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesRootMetadataBefore = @($seriesRootMetadataSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $seriesRootMetadataRoot = $seriesRootMetadataBefore | Where-Object { $_.date -eq "2026-11-04" } | Select-Object -First 1
    if ($null -eq $seriesRootMetadataRoot) {
        throw "Expected recurring appointment root for MOD-PAT-0013 on 2026-11-04."
    }

    $appointmentSeriesRootMetadataRootId = $seriesRootMetadataRoot.seriesRootId
    $encodedSeriesRootMetadataRootId = [System.Uri]::EscapeDataString($appointmentSeriesRootMetadataRootId)
    $appointmentSeriesRootMetadataOriginalRoot = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRootMetadataRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $seriesRootMetadataBody = @{
        providerId = 101
        title = $appointmentSeriesRootMetadataOriginalRoot.title
        date = $appointmentSeriesRootMetadataOriginalRoot.date
        startTime = $appointmentSeriesRootMetadataOriginalRoot.startTime
        durationMinutes = $appointmentSeriesRootMetadataOriginalRoot.durationMinutes
        facilityId = 10
        billingLocationId = 10
        categoryId = 10
        room = "Series Meta"
        status = "~"
        comments = "Slice 111 recurring root metadata propagation check."
        recurrenceType = $appointmentSeriesRootMetadataOriginalRoot.recurrenceType
        repeatFrequency = $appointmentSeriesRootMetadataOriginalRoot.repeatFrequency
        repeatUnit = $appointmentSeriesRootMetadataOriginalRoot.repeatUnit
        recurrenceEndDate = $appointmentSeriesRootMetadataOriginalRoot.recurrenceEndDate
        recurrenceExdates = @($appointmentSeriesRootMetadataOriginalRoot.recurrenceExdates)
    } | ConvertTo-Json -Depth 5

    $rootAfterSeriesRootMetadata = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRootMetadataRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $seriesRootMetadataBody -TimeoutSec 20
    $seriesRootMetadataAfterSearch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments?patientId=MOD-PAT-0013&from=2026-11-04&limit=10" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $seriesRootMetadataAfter = @($seriesRootMetadataAfterSearch.appointments | Where-Object { $_.title -eq "Preventive Care" -and $_.isRecurringSeries })
    $seriesRootMetadataAfterDates = @($seriesRootMetadataAfter | ForEach-Object { $_.date })
    $seriesRootMetadataAfterNumbers = @($seriesRootMetadataAfter | ForEach-Object { $_.occurrenceNumber })
    $seriesRootMetadataAfterProviderIds = @($seriesRootMetadataAfter | ForEach-Object { $_.providerId })
    $seriesRootMetadataAfterFacilityIds = @($seriesRootMetadataAfter | ForEach-Object { $_.facilityId })
    $seriesRootMetadataAfterBillingLocationIds = @($seriesRootMetadataAfter | ForEach-Object { $_.billingLocationId })
    $seriesRootMetadataAfterCategoryIds = @($seriesRootMetadataAfter | ForEach-Object { $_.categoryId })
    $seriesRootMetadataAfterStatuses = @($seriesRootMetadataAfter | ForEach-Object { $_.status })
    $seriesRootMetadataAfterRooms = @($seriesRootMetadataAfter | ForEach-Object { $_.room })
    $seriesRootMetadataAfterComments = @($seriesRootMetadataAfter | ForEach-Object { $_.comments })
    $seriesRootMetadataGeneratedOccurrence = $seriesRootMetadataAfter | Where-Object { $_.date -eq "2026-11-18" } | Select-Object -First 1
    $seriesRootMetadataMismatchedRooms = @($seriesRootMetadataAfter | Where-Object { $_.room -ne "Series Meta" })
    $seriesRootMetadataMismatchedComments = @($seriesRootMetadataAfter | Where-Object { $_.comments -ne "Slice 111 recurring root metadata propagation check." })
    $appointmentSeriesRootMetadataPassed = $rootAfterSeriesRootMetadata.providerId -eq 101 `
        -and $rootAfterSeriesRootMetadata.facilityId -eq 10 `
        -and $rootAfterSeriesRootMetadata.billingLocationId -eq 10 `
        -and $rootAfterSeriesRootMetadata.categoryId -eq 10 `
        -and $rootAfterSeriesRootMetadata.status -eq "~" `
        -and $rootAfterSeriesRootMetadata.room -eq "Series Meta" `
        -and $rootAfterSeriesRootMetadata.comments -eq "Slice 111 recurring root metadata propagation check." `
        -and $rootAfterSeriesRootMetadata.recurrenceExceptionCount -eq 1 `
        -and ($rootAfterSeriesRootMetadata.recurrenceExdates -contains "2026-12-16") `
        -and $seriesRootMetadataAfter.Count -eq 6 `
        -and (($seriesRootMetadataAfterDates -join ",") -eq "2026-11-04,2026-11-18,2026-12-02,2026-12-30,2027-01-13,2027-01-27") `
        -and (($seriesRootMetadataAfterNumbers -join ",") -eq "1,2,3,5,6,7") `
        -and (($seriesRootMetadataAfterProviderIds -join ",") -eq "101,101,101,101,101,101") `
        -and (($seriesRootMetadataAfterFacilityIds -join ",") -eq "10,10,10,10,10,10") `
        -and (($seriesRootMetadataAfterBillingLocationIds -join ",") -eq "10,10,10,10,10,10") `
        -and (($seriesRootMetadataAfterCategoryIds -join ",") -eq "10,10,10,10,10,10") `
        -and (($seriesRootMetadataAfterStatuses -join ",") -eq "~,~,~,~,~,~") `
        -and $seriesRootMetadataMismatchedRooms.Count -eq 0 `
        -and $seriesRootMetadataMismatchedComments.Count -eq 0 `
        -and $null -ne $seriesRootMetadataGeneratedOccurrence `
        -and $seriesRootMetadataGeneratedOccurrence.isVirtualOccurrence

    Add-Check -Name "appointment series root metadata propagation" -Result $(if ($appointmentSeriesRootMetadataPassed) { "passed" } else { "failed" }) -Details @{
        providerIds = $seriesRootMetadataAfterProviderIds
        facilityIds = $seriesRootMetadataAfterFacilityIds
        billingLocationIds = $seriesRootMetadataAfterBillingLocationIds
        categoryIds = $seriesRootMetadataAfterCategoryIds
        statuses = $seriesRootMetadataAfterStatuses
        rooms = $seriesRootMetadataAfterRooms
        comments = $seriesRootMetadataAfterComments
        dates = $seriesRootMetadataAfterDates
        occurrenceNumbers = $seriesRootMetadataAfterNumbers
        exceptionDates = $rootAfterSeriesRootMetadata.recurrenceExdates
        totalMatches = $seriesRootMetadataAfterSearch.totalMatches
    }
}
catch {
    Add-Check -Name "appointment series root metadata propagation" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $appointmentSeriesRootMetadataRootId) {
        try {
            $encodedSeriesRootMetadataRootId = [System.Uri]::EscapeDataString($appointmentSeriesRootMetadataRootId)
            if ($null -eq $appointmentSeriesRootMetadataOriginalRoot) {
                $appointmentSeriesRootMetadataOriginalRoot = Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRootMetadataRootId" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
            }

            $seriesRootMetadataRestoreBody = @{
                providerId = $appointmentSeriesRootMetadataOriginalRoot.providerId
                title = $appointmentSeriesRootMetadataOriginalRoot.title
                date = $appointmentSeriesRootMetadataOriginalRoot.date
                startTime = $appointmentSeriesRootMetadataOriginalRoot.startTime
                durationMinutes = $appointmentSeriesRootMetadataOriginalRoot.durationMinutes
                facilityId = $appointmentSeriesRootMetadataOriginalRoot.facilityId
                billingLocationId = $appointmentSeriesRootMetadataOriginalRoot.billingLocationId
                categoryId = $appointmentSeriesRootMetadataOriginalRoot.categoryId
                room = $appointmentSeriesRootMetadataOriginalRoot.room
                status = $appointmentSeriesRootMetadataOriginalRoot.status
                comments = $appointmentSeriesRootMetadataOriginalRoot.comments
                recurrenceType = $appointmentSeriesRootMetadataOriginalRoot.recurrenceType
                repeatFrequency = $appointmentSeriesRootMetadataOriginalRoot.repeatFrequency
                repeatUnit = $appointmentSeriesRootMetadataOriginalRoot.repeatUnit
                recurrenceEndDate = $appointmentSeriesRootMetadataOriginalRoot.recurrenceEndDate
                recurrenceExdates = @($appointmentSeriesRootMetadataOriginalRoot.recurrenceExdates)
            } | ConvertTo-Json -Depth 5
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/appointments/$encodedSeriesRootMetadataRootId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $seriesRootMetadataRestoreBody -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $unauthenticatedEncounterSearchStatus = 0
    try {
        $unauthenticatedEncounterSearch = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/encounters?patientId=MOD-PAT-0001&from=2026-01-01&limit=5" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedEncounterSearchStatus = [int]$unauthenticatedEncounterSearch.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedEncounterSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $frontDeskEncounterSearchStatus = 0
    try {
        $frontDeskEncounterSearch = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/encounters?patientId=MOD-PAT-0001&from=2026-01-01&limit=5" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskEncounterSearchStatus = [int]$frontDeskEncounterSearch.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskEncounterSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $frontDeskEncounterMutationStatus = 0
    $frontDeskEncounterMutationBody = @{
        patientId = "MOD-PAT-0001"
        dateTime = "2026-06-18 10:00:00"
        reason = "Blocked Encounter Authorization"
        facilityId = 10
        billingFacilityId = 10
    } | ConvertTo-Json -Depth 5
    try {
        $frontDeskEncounterMutation = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/encounters" `
            -Method Post `
            -Headers (Get-FrontDeskHeaders) `
            -ContentType "application/json" `
            -Body $frontDeskEncounterMutationBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskEncounterMutationStatus = [int]$frontDeskEncounterMutation.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskEncounterMutationStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $encounters = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters?patientId=MOD-PAT-0001&from=2026-01-01&limit=5" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $anchorEncounter = $encounters.encounters | Select-Object -First 1
    $encounterPassed = $unauthenticatedEncounterSearchStatus -eq 401 -and $frontDeskEncounterSearchStatus -eq 403 -and $frontDeskEncounterMutationStatus -eq 403 -and $null -ne $anchorEncounter -and $anchorEncounter.patientId -eq "MOD-PAT-0001" -and $anchorEncounter.hasVitals -and $anchorEncounter.hasSoapNote
    Add-Check -Name "anchor encounter search" -Result $(if ($encounterPassed) { "passed" } else { "failed" }) -Details @{
        unauthenticatedStatus = $unauthenticatedEncounterSearchStatus
        frontDeskStatus = $frontDeskEncounterSearchStatus
        frontDeskMutationStatus = $frontDeskEncounterMutationStatus
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

    $encounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$($anchorEncounter.encounter)" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

    $createdSignature = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/sign" -Method Put -ContentType "application/json" -Body $signatureBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterSignatureId = $createdSignature.id
    $createdSignatureVisible = @($createdSignature.detail.signatures | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterSignatureId `
            -and $_.signerUsername -eq "admin" `
            -and $_.signedAt -eq "2026-06-18 10:20" `
            -and $_.isLock -eq $false `
            -and $_.amendment -eq $signatureNote
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/signatures/$smokeEncounterSignatureId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterSignatureId = $null
    $afterDeleteSignatureDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/signatures/$smokeEncounterSignatureId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterCoSignatureIds = @()
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $coSignatureSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $primarySignatureNote = "Smoke primary encounter attestation $coSignatureSuffix"
    $coSignatureNote = "Smoke co-signature review $coSignatureSuffix"
    $primarySignatureBody = @{
        signerUsername = "admin"
        signedAt = "2026-06-18 10:30:00"
        isLock = $false
        amendment = $primarySignatureNote
    } | ConvertTo-Json -Depth 5
    $coSignatureBody = @{
        signerUsername = "gold-provider-02"
        signedAt = "2026-06-18 10:35:00"
        isLock = $true
        amendment = $coSignatureNote
    } | ConvertTo-Json -Depth 5

    $createdPrimarySignature = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/sign" -Method Put -ContentType "application/json" -Body $primarySignatureBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterCoSignatureIds += $createdPrimarySignature.id
    $createdCoSignature = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/sign" -Method Put -ContentType "application/json" -Body $coSignatureBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterCoSignatureIds += $createdCoSignature.id

    $primarySignatureVisible = @($createdCoSignature.detail.signatures | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $createdPrimarySignature.id `
            -and $_.signerUsername -eq "admin" `
            -and $_.signedAt -eq "2026-06-18 10:30" `
            -and $_.isLock -eq $false `
            -and $_.amendment -eq $primarySignatureNote
    } | Select-Object -First 1
    $coSignatureVisible = @($createdCoSignature.detail.signatures | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $createdCoSignature.id `
            -and $_.signerUsername -eq "gold-provider-02" `
            -and $_.signedAt -eq "2026-06-18 10:35" `
            -and $_.isLock -eq $true `
            -and $_.amendment -eq $coSignatureNote
    } | Select-Object -First 1
    $amendmentHistory = @($createdCoSignature.detail.amendmentHistory | Where-Object { $null -ne $_ })
    $primaryAmendmentVisible = $amendmentHistory | Where-Object {
        $_.signatureId -eq $createdPrimarySignature.id `
            -and $_.signerUsername -eq "admin" `
            -and $_.amendment -eq $primarySignatureNote
    } | Select-Object -First 1
    $coSignatureAmendmentVisible = $amendmentHistory | Where-Object {
        $_.signatureId -eq $createdCoSignature.id `
            -and $_.signerUsername -eq "gold-provider-02" `
            -and $_.isLock -eq $true `
            -and $_.amendment -eq $coSignatureNote
    } | Select-Object -First 1

    foreach ($signatureId in @($smokeEncounterCoSignatureIds)) {
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/signatures/$signatureId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    }
    $smokeEncounterCoSignatureIds = @()
    $afterCoSignatureDeleteDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedCoSignaturesVisible = @($afterCoSignatureDeleteDetail.signatures | Where-Object { $null -ne $_ }) | Where-Object {
        $_.amendment -eq $primarySignatureNote -or $_.amendment -eq $coSignatureNote
    }
    $deletedAmendmentsVisible = @($afterCoSignatureDeleteDetail.amendmentHistory | Where-Object { $null -ne $_ }) | Where-Object {
        $_.amendment -eq $primarySignatureNote -or $_.amendment -eq $coSignatureNote
    }

    $encounterCoSignaturePassed = $null -ne $primarySignatureVisible -and $null -ne $coSignatureVisible -and $null -ne $primaryAmendmentVisible -and $null -ne $coSignatureAmendmentVisible -and @($amendmentHistory).Count -eq 2 -and @($deletedCoSignaturesVisible).Count -eq 0 -and @($deletedAmendmentsVisible).Count -eq 0
    Add-Check -Name "encounter co-signature lifecycle" -Result $(if ($encounterCoSignaturePassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        primarySignature = $primarySignatureVisible
        coSignature = $coSignatureVisible
        amendmentHistoryCount = @($amendmentHistory).Count
        primaryAmendment = $primaryAmendmentVisible
        coSignatureAmendment = $coSignatureAmendmentVisible
        deletedVisibleCount = @($deletedCoSignaturesVisible).Count
        deletedAmendmentCount = @($deletedAmendmentsVisible).Count
    }
}
catch {
    Add-Check -Name "encounter co-signature lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    foreach ($signatureId in @($smokeEncounterCoSignatureIds)) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/signatures/$signatureId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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

    $createdEncounterDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $documentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentId = $null
    $afterDeleteDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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

    $createdEncounterBinaryDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/binary" -Method Post -ContentType "application/json" -Body $binaryDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

    $encounterBinaryContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryDocumentId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $encounterBinaryDownloadClient = New-AuthenticatedHttpClient
    try {
        $encounterBinaryDownload = $encounterBinaryDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$smokeEncounterBinaryDocumentId/download").GetAwaiter().GetResult()
        $encounterBinaryDownloadBytes = $encounterBinaryDownload.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $encounterBinaryDownloadContentType = $encounterBinaryDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $encounterBinaryDownloadClient.Dispose()
    }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterBinaryDocumentId = $null
    $afterDeleteBinaryDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterBinaryReplacementDocumentId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $binaryReplacementSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $binaryReplacementName = "Smoke Encounter Binary Replacement $binaryReplacementSuffix"
    $binaryReplacementOriginalFileName = "$binaryReplacementName original.pdf"
    $binaryReplacementUpdatedFileName = "$binaryReplacementName updated.pdf"
    $binaryReplacementOriginalPayload = "%PDF-1.4`n% Smoke original encounter binary replacement $binaryReplacementSuffix`n%%EOF`n"
    $binaryReplacementUpdatedPayload = "%PDF-1.4`n% Smoke updated encounter binary replacement $binaryReplacementSuffix`n%%EOF`n"
    $binaryReplacementOriginalBase64 = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($binaryReplacementOriginalPayload))
    $binaryReplacementUpdatedBase64 = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($binaryReplacementUpdatedPayload))
    $binaryReplacementOriginalBody = @{
        categoryId = 3
        name = $binaryReplacementName
        docDate = "2026-06-18"
        fileName = $binaryReplacementOriginalFileName
        mimetype = "application/pdf"
        contentBase64 = $binaryReplacementOriginalBase64
        notes = "Created by the smoke encounter binary document content replacement check."
    } | ConvertTo-Json -Depth 5

    $createdBinaryReplacementDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/binary" -Method Post -ContentType "application/json" -Body $binaryReplacementOriginalBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterBinaryReplacementDocumentId = $createdBinaryReplacementDocument.id
    $binaryReplacementBody = @{
        fileName = $binaryReplacementUpdatedFileName
        mimetype = "application/pdf"
        contentBase64 = $binaryReplacementUpdatedBase64
    } | ConvertTo-Json -Depth 5

    $replacedBinaryDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterBinaryReplacementDocumentId/content/binary" -Method Put -ContentType "application/json" -Body $binaryReplacementBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $replacedBinaryDocumentVisible = @($replacedBinaryDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterBinaryReplacementDocumentId `
            -and $_.name -eq $binaryReplacementName `
            -and $_.mimetype -eq "application/pdf" `
            -and $_.fileName -eq $binaryReplacementUpdatedFileName `
            -and $_.storageMethod -eq "database" `
            -and $_.previewKind -eq "pdf" `
            -and $_.thumbnailLabel -eq "PDF" `
            -and $_.canDownload -eq $true `
            -and $_.hash -ne $null
    } | Select-Object -First 1

    $binaryReplacementContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryReplacementDocumentId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $binaryReplacementDownloadClient = New-AuthenticatedHttpClient
    try {
        $binaryReplacementDownload = $binaryReplacementDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$smokeEncounterBinaryReplacementDocumentId/download").GetAwaiter().GetResult()
        $binaryReplacementDownloadBytes = $binaryReplacementDownload.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $binaryReplacementDownloadContentType = $binaryReplacementDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $binaryReplacementDownloadClient.Dispose()
    }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryReplacementDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterBinaryReplacementDocumentId = $null

    $encounterBinaryReplacementPassed = $null -ne $replacedBinaryDocumentVisible `
        -and $binaryReplacementContent.contentBase64 -eq $binaryReplacementUpdatedBase64 `
        -and $binaryReplacementContent.contentBase64 -ne $binaryReplacementOriginalBase64 `
        -and $binaryReplacementContent.fileName -eq $binaryReplacementUpdatedFileName `
        -and $binaryReplacementContent.revisionHash -eq $binaryReplacementContent.hash `
        -and $binaryReplacementDownload.IsSuccessStatusCode `
        -and $binaryReplacementDownloadContentType -eq "application/pdf" `
        -and [Convert]::ToBase64String($binaryReplacementDownloadBytes) -eq $binaryReplacementUpdatedBase64
    Add-Check -Name "encounter binary document content replacement lifecycle" -Result $(if ($encounterBinaryReplacementPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdBinaryReplacementDocument.id
        fileName = $binaryReplacementContent.fileName
        previewKind = $binaryReplacementContent.previewKind
        downloadContentType = $binaryReplacementDownloadContentType
    }
}
catch {
    Add-Check -Name "encounter binary document content replacement lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterBinaryReplacementDocumentId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterBinaryReplacementDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterScannedDocumentId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $scanDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $scanDocumentName = "Smoke Encounter Scanned Attachment $scanDocumentSuffix.pdf"
    $scanNotes = "Scan source: front-desk scanner; OCR pending; Created by the smoke encounter scanned attachment readiness check."
    $scanPdfText = "%PDF-1.4`n% Smoke encounter scanned attachment readiness PDF`n1 0 obj << /Type /Catalog >> endobj`n%%EOF"
    $scanPdfBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($scanPdfText))
    $scanDocumentBody = @{
        categoryId = 3
        name = $scanDocumentName
        docDate = "2026-06-20"
        fileName = $scanDocumentName
        mimetype = "application/pdf"
        contentBase64 = $scanPdfBase64
        notes = $scanNotes
    } | ConvertTo-Json -Depth 5

    $createdEncounterScannedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/binary" -Method Post -ContentType "application/json" -Body $scanDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterScannedDocumentId = $createdEncounterScannedDocument.id
    $createdEncounterScannedDocumentVisible = @($createdEncounterScannedDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterScannedDocumentId `
            -and $_.name -eq $scanDocumentName `
            -and $_.isScannedAttachment -eq $true `
            -and $_.scanStatus -eq "Scanned attachment" `
            -and $_.captureSource -eq "front-desk scanner" `
            -and $_.scanPageCount -eq 1 `
            -and $_.ocrStatus -eq "OCR pending"
    } | Select-Object -First 1

    $afterCreateScannedDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $reloadedEncounterScannedDocumentVisible = @($afterCreateScannedDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterScannedDocumentId `
            -and $_.isScannedAttachment -eq $true `
            -and $_.scanStatus -eq "Scanned attachment" `
            -and $_.captureSource -eq "front-desk scanner" `
            -and $_.scanPageCount -eq 1 `
            -and $_.ocrStatus -eq "OCR pending"
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterScannedDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterScannedDocumentId = $null
    $afterDeleteScannedDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedEncounterScannedDocumentVisible = @($afterDeleteScannedDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $scanDocumentName
    } | Select-Object -First 1

    $encounterScannedAttachmentPassed = $null -ne $createdEncounterScannedDocumentVisible `
        -and $null -ne $reloadedEncounterScannedDocumentVisible `
        -and $null -eq $deletedEncounterScannedDocumentVisible
    Add-Check -Name "encounter scanned attachment readiness" -Result $(if ($encounterScannedAttachmentPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterScannedDocument.id
        scanStatus = $reloadedEncounterScannedDocumentVisible.scanStatus
        captureSource = $reloadedEncounterScannedDocumentVisible.captureSource
        scanPageCount = $reloadedEncounterScannedDocumentVisible.scanPageCount
        ocrStatus = $reloadedEncounterScannedDocumentVisible.ocrStatus
    }
}
catch {
    Add-Check -Name "encounter scanned attachment readiness" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterScannedDocumentId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterScannedDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterExternalLinkDocumentId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $externalLinkDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $externalLinkDocumentName = "Smoke Encounter External Link $externalLinkDocumentSuffix"
    $externalLinkUrl = "https://example.test/legacy-ehr/encounter-record/$externalLinkDocumentSuffix"
    $externalLinkDocumentBody = @{
        categoryId = 3
        name = $externalLinkDocumentName
        docDate = "2026-06-18"
        url = $externalLinkUrl
        notes = "Created by the smoke encounter external-link document check."
    } | ConvertTo-Json -Depth 5

    $createdEncounterExternalLinkDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/external-link" -Method Post -ContentType "application/json" -Body $externalLinkDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterExternalLinkDocumentId = $createdEncounterExternalLinkDocument.id
    $createdEncounterExternalLinkDocumentVisible = @($createdEncounterExternalLinkDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterExternalLinkDocumentId `
            -and $_.name -eq $externalLinkDocumentName `
            -and $_.categoryName -eq "Medical Record" `
            -and $_.docDate -eq "2026-06-18" `
            -and $_.mimetype -eq "text/uri-list" `
            -and $_.storageMethod -eq "web_url" `
            -and $_.url -eq $externalLinkUrl `
            -and $_.previewKind -eq "external-link" `
            -and $_.thumbnailLabel -eq "LINK" `
            -and $_.canDownload -eq $true
    } | Select-Object -First 1

    $externalLinkContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterExternalLinkDocumentId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterExternalLinkDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterExternalLinkDocumentId = $null
    $afterDeleteExternalLinkDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedEncounterExternalLinkDocumentVisible = @($afterDeleteExternalLinkDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $externalLinkDocumentName
    } | Select-Object -First 1

    $encounterExternalLinkDocumentPassed = $null -ne $createdEncounterExternalLinkDocumentVisible `
        -and $externalLinkContent.content -like "*$externalLinkUrl*" `
        -and $null -eq $deletedEncounterExternalLinkDocumentVisible
    Add-Check -Name "encounter external-link document lifecycle" -Result $(if ($encounterExternalLinkDocumentPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterExternalLinkDocument.id
        document = $createdEncounterExternalLinkDocumentVisible
    }
}
catch {
    Add-Check -Name "encounter external-link document lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterExternalLinkDocumentId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterExternalLinkDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterDocumentMetadataId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $metadataDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $metadataDocumentName = "Smoke Encounter Metadata Document $metadataDocumentSuffix"
    $updatedMetadataDocumentName = "Smoke Encounter Refiled Directive $metadataDocumentSuffix"
    $metadataDocumentBody = @{
        categoryId = 3
        name = $metadataDocumentName
        docDate = "2026-06-18"
        content = "Smoke encounter document metadata content $metadataDocumentSuffix."
        notes = "Created by the smoke encounter document metadata check."
    } | ConvertTo-Json

    $createdEncounterMetadataDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $metadataDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterDocumentMetadataId = $createdEncounterMetadataDocument.id
    $createdMetadataDocumentVisible = @($createdEncounterMetadataDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentMetadataId `
            -and $_.name -eq $metadataDocumentName `
            -and $_.categoryName -eq "Medical Record" `
            -and $_.docDate -eq "2026-06-18"
    } | Select-Object -First 1

    $metadataUpdateBody = @{
        categoryId = 6
        name = $updatedMetadataDocumentName
        docDate = "2026-06-19"
        encounter = 1000013
        notes = "Updated by the smoke encounter document metadata check."
    } | ConvertTo-Json
    $updatedEncounterMetadataDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentMetadataId/metadata" -Method Put -ContentType "application/json" -Body $metadataUpdateBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $updatedMetadataDocumentVisible = @($updatedEncounterMetadataDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentMetadataId `
            -and $_.name -eq $updatedMetadataDocumentName `
            -and $_.categoryName -eq "Advance Directive" `
            -and $_.docDate -eq "2026-06-19" `
            -and $_.notes -eq "Updated by the smoke encounter document metadata check."
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentMetadataId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $afterDeleteMetadataDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedMetadataDocumentVisible = @($afterDeleteMetadataDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $metadataDocumentName -or $_.name -eq $updatedMetadataDocumentName
    } | Select-Object -First 1

    $encounterDocumentMetadataPassed = $null -ne $createdMetadataDocumentVisible `
        -and $null -ne $updatedMetadataDocumentVisible `
        -and $null -eq $deletedMetadataDocumentVisible
    Add-Check -Name "encounter document metadata lifecycle" -Result $(if ($encounterDocumentMetadataPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterMetadataDocument.id
        document = $updatedMetadataDocumentVisible
    }
}
catch {
    Add-Check -Name "encounter document metadata lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterDocumentMetadataId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentMetadataId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterDocumentContentId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $contentDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $contentDocumentName = "Smoke Encounter Content Document $contentDocumentSuffix"
    $originalContent = "Smoke encounter document original content $contentDocumentSuffix."
    $replacementContent = "Smoke encounter document replacement content $contentDocumentSuffix."
    $contentDocumentBody = @{
        categoryId = 3
        name = $contentDocumentName
        docDate = "2026-06-18"
        content = $originalContent
        notes = "Created by the smoke encounter document content check."
    } | ConvertTo-Json

    $createdEncounterContentDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $contentDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterDocumentContentId = $createdEncounterContentDocument.id
    $createdContentDocumentVisible = @($createdEncounterContentDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentContentId `
            -and $_.name -eq $contentDocumentName `
            -and $_.contentPreview -like "*$originalContent*" `
            -and $_.versionLabel -eq "Version 1"
    } | Select-Object -First 1

    $replacementFileName = "$($contentDocumentName).txt"
    $contentReplacementBody = @{
        fileName = $replacementFileName
        content = $replacementContent
    } | ConvertTo-Json
    $replacedEncounterContentDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentContentId/content" -Method Put -ContentType "application/json" -Body $contentReplacementBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $replacedContentDocumentVisible = @($replacedEncounterContentDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentContentId `
            -and $_.name -eq $contentDocumentName `
            -and $_.mimetype -eq "text/plain" `
            -and $_.storageMethod -eq "database" `
            -and $_.fileName -eq $replacementFileName `
            -and $_.contentPreview -like "*$replacementContent*" `
            -and $_.revisionHash -eq $_.hash `
            -and $_.previewKind -eq "text"
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentContentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentContentId = $null
    $afterDeleteContentDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedContentDocumentVisible = @($afterDeleteContentDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $contentDocumentName
    } | Select-Object -First 1

    $encounterDocumentContentPassed = $null -ne $createdContentDocumentVisible `
        -and $null -ne $replacedContentDocumentVisible `
        -and $null -eq $deletedContentDocumentVisible
    Add-Check -Name "encounter document content replacement lifecycle" -Result $(if ($encounterDocumentContentPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterContentDocument.id
        document = $replacedContentDocumentVisible
    }
}
catch {
    Add-Check -Name "encounter document content replacement lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterDocumentContentId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentContentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterDocumentArchiveRestoreId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $archiveDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $archiveDocumentName = "Smoke Encounter Archive Document $archiveDocumentSuffix"
    $archiveDocumentContent = "Smoke encounter document archive restore content $archiveDocumentSuffix."
    $archiveDocumentBody = @{
        categoryId = 3
        name = $archiveDocumentName
        docDate = "2026-06-18"
        content = $archiveDocumentContent
        notes = "Created by the smoke encounter document archive restore check."
    } | ConvertTo-Json

    $createdEncounterArchiveDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $archiveDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterDocumentArchiveRestoreId = $createdEncounterArchiveDocument.id
    $createdArchiveDocumentVisible = @($createdEncounterArchiveDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentArchiveRestoreId `
            -and $_.name -eq $archiveDocumentName `
            -and $_.deleted -eq 0 `
            -and $_.contentPreview -like "*$archiveDocumentContent*"
    } | Select-Object -First 1

    $archivedEncounterDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentArchiveRestoreId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedDocumentVisible = @($archivedEncounterDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentArchiveRestoreId `
            -and $_.name -eq $archiveDocumentName `
            -and $_.deleted -eq 1
    } | Select-Object -First 1
    $activeOnlyAfterArchive = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedDocumentHidden = @($activeOnlyAfterArchive.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentArchiveRestoreId
    } | Select-Object -First 1
    $archivedDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013?includeArchivedDocuments=true" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedDocumentIncluded = @($archivedDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentArchiveRestoreId `
            -and $_.deleted -eq 1
    } | Select-Object -First 1

    $restoredEncounterDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentArchiveRestoreId/restore" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $restoredArchiveDocumentVisible = @($restoredEncounterDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentArchiveRestoreId `
            -and $_.name -eq $archiveDocumentName `
            -and $_.deleted -eq 0 `
            -and $_.contentPreview -like "*$archiveDocumentContent*"
    } | Select-Object -First 1
    $activeOnlyAfterRestore = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $restoredDocumentActive = @($activeOnlyAfterRestore.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentArchiveRestoreId `
            -and $_.deleted -eq 0
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentArchiveRestoreId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentArchiveRestoreId = $null
    $afterDeleteArchiveDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013?includeArchivedDocuments=true" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedArchiveDocumentVisible = @($afterDeleteArchiveDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $archiveDocumentName
    } | Select-Object -First 1

    $encounterDocumentArchiveRestorePassed = $null -ne $createdArchiveDocumentVisible `
        -and $null -ne $archivedDocumentVisible `
        -and $null -eq $archivedDocumentHidden `
        -and $null -ne $archivedDocumentIncluded `
        -and $null -ne $restoredArchiveDocumentVisible `
        -and $null -ne $restoredDocumentActive `
        -and $null -eq $deletedArchiveDocumentVisible
    Add-Check -Name "encounter document archive restore lifecycle" -Result $(if ($encounterDocumentArchiveRestorePassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterArchiveDocument.id
        archived = $archivedDocumentVisible
        restored = $restoredArchiveDocumentVisible
    }
}
catch {
    Add-Check -Name "encounter document archive restore lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterDocumentArchiveRestoreId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentArchiveRestoreId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterDocumentLifecycleId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $lifecycleDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $lifecycleDocumentName = "Smoke Encounter Lifecycle Document $lifecycleDocumentSuffix"
    $lifecycleDocumentContent = "Smoke encounter document lifecycle content $lifecycleDocumentSuffix."
    $lifecycleDocumentBody = @{
        categoryId = 3
        name = $lifecycleDocumentName
        docDate = "2026-06-18"
        content = $lifecycleDocumentContent
        notes = "Created by the smoke encounter document lifecycle timeline check."
    } | ConvertTo-Json

    $createdEncounterLifecycleDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $lifecycleDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterDocumentLifecycleId = $createdEncounterLifecycleDocument.id
    $createdLifecycleDocumentVisible = @($createdEncounterLifecycleDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentLifecycleId -and $_.name -eq $lifecycleDocumentName
    } | Select-Object -First 1
    $createdLifecycleCodes = if ($null -eq $createdLifecycleDocumentVisible) { @() } else { @($createdLifecycleDocumentVisible.lifecycleEvents | ForEach-Object { $_.code }) }

    $signLifecycleBody = @{
        reviewStatus = "approved"
        reviewedBy = "admin"
    } | ConvertTo-Json
    $signedLifecycleDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentLifecycleId/sign" -Method Put -ContentType "application/json" -Body $signLifecycleBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $signedLifecycleDocumentVisible = @($signedLifecycleDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentLifecycleId -and $_.reviewStatus -eq "approved" -and $_.reviewedBy -eq "admin"
    } | Select-Object -First 1
    $signedLifecycleCodes = if ($null -eq $signedLifecycleDocumentVisible) { @() } else { @($signedLifecycleDocumentVisible.lifecycleEvents | ForEach-Object { $_.code }) }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentLifecycleId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $archivedLifecycleDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013?includeArchivedDocuments=true" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedLifecycleDocumentVisible = @($archivedLifecycleDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentLifecycleId -and $_.deleted -eq 1
    } | Select-Object -First 1
    $archivedLifecycleCodes = if ($null -eq $archivedLifecycleDocumentVisible) { @() } else { @($archivedLifecycleDocumentVisible.lifecycleEvents | ForEach-Object { $_.code }) }

    $restoredLifecycleDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentLifecycleId/restore" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $restoredLifecycleDocumentVisible = @($restoredLifecycleDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentLifecycleId -and $_.deleted -eq 0
    } | Select-Object -First 1
    $restoredLifecycleCodes = if ($null -eq $restoredLifecycleDocumentVisible) { @() } else { @($restoredLifecycleDocumentVisible.lifecycleEvents | ForEach-Object { $_.code }) }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentLifecycleId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentLifecycleId = $null

    $encounterDocumentLifecyclePassed = $null -ne $createdLifecycleDocumentVisible `
        -and ($createdLifecycleCodes -contains "filed") `
        -and ($createdLifecycleCodes -contains "current-version") `
        -and ($createdLifecycleCodes -contains "review-pending") `
        -and ($createdLifecycleCodes -contains "active") `
        -and $null -ne $signedLifecycleDocumentVisible `
        -and ($signedLifecycleCodes -contains "review-approved") `
        -and $null -ne $archivedLifecycleDocumentVisible `
        -and ($archivedLifecycleCodes -contains "archived") `
        -and $null -ne $restoredLifecycleDocumentVisible `
        -and ($restoredLifecycleCodes -contains "active")
    Add-Check -Name "encounter document lifecycle timeline" -Result $(if ($encounterDocumentLifecyclePassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterLifecycleDocument.id
        createdCodes = $createdLifecycleCodes
        signedCodes = $signedLifecycleCodes
        archivedCodes = $archivedLifecycleCodes
        restoredCodes = $restoredLifecycleCodes
    }
}
catch {
    Add-Check -Name "encounter document lifecycle timeline" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterDocumentLifecycleId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentLifecycleId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterDocumentMoveId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $moveDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $moveDocumentName = "Smoke Encounter Moved Document $moveDocumentSuffix"
    $moveDocumentBody = @{
        categoryId = 3
        name = $moveDocumentName
        docDate = "2026-06-18"
        content = "Smoke encounter document move content $moveDocumentSuffix."
        notes = "Created by the smoke encounter document move check."
    } | ConvertTo-Json

    $createdEncounterMoveDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $moveDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterDocumentMoveId = $createdEncounterMoveDocument.id
    $createdMoveDocumentVisible = @($createdEncounterMoveDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentMoveId `
            -and $_.name -eq $moveDocumentName `
            -and $_.categoryName -eq "Medical Record" `
            -and $_.docDate -eq "2026-06-18"
    } | Select-Object -First 1

    $moveBody = @{
        targetEncounter = 1000011
    } | ConvertTo-Json
    $movedEncounterDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentMoveId/move" -Method Put -ContentType "application/json" -Body $moveBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $movedSourceDocumentVisible = @($movedEncounterDocument.sourceDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentMoveId
    } | Select-Object -First 1
    $movedTargetDocumentVisible = @($movedEncounterDocument.targetDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentMoveId `
            -and $_.name -eq $moveDocumentName `
            -and $_.categoryName -eq "Medical Record" `
            -and $_.docDate -eq "2026-06-18" `
            -and $_.notes -eq "Created by the smoke encounter document move check."
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentMoveId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentMoveId = $null
    $afterDeleteMovedDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000011" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedMovedDocumentVisible = @($afterDeleteMovedDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $moveDocumentName
    } | Select-Object -First 1

    $encounterDocumentMovePassed = $null -ne $createdMoveDocumentVisible `
        -and $null -eq $movedSourceDocumentVisible `
        -and $null -ne $movedTargetDocumentVisible `
        -and $null -eq $deletedMovedDocumentVisible
    Add-Check -Name "encounter document move lifecycle" -Result $(if ($encounterDocumentMovePassed) { "passed" } else { "failed" }) -Details @{
        sourceEncounter = 1000013
        targetEncounter = 1000011
        documentId = $createdEncounterMoveDocument.id
        document = $movedTargetDocumentVisible
    }
}
catch {
    Add-Check -Name "encounter document move lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterDocumentMoveId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentMoveId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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

    $createdEncounterSignedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $signedDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $signedEncounterDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentSignOffId/sign" -Method Put -ContentType "application/json" -Body $signBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $signedEncounterDocumentVisible = @($signedEncounterDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentSignOffId `
            -and $_.name -eq $signedDocumentName `
            -and $_.reviewStatus -eq "approved" `
            -and $_.reviewedBy -eq "admin" `
            -and -not [string]::IsNullOrWhiteSpace($_.reviewedAt)
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentSignOffId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentSignOffId = $null
    $afterDeleteSignedDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentSignOffId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeEncounterDocumentDenialId = $null
try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $deniedDocumentSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $deniedDocumentName = "Smoke Encounter Denied Document $deniedDocumentSuffix"
    $deniedDocumentBody = @{
        categoryId = 3
        name = $deniedDocumentName
        docDate = "2026-06-18"
        content = "Smoke encounter document denial content $deniedDocumentSuffix."
        notes = "Created by the smoke encounter document denial check."
    } | ConvertTo-Json

    $createdEncounterDeniedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents" -Method Post -ContentType "application/json" -Body $deniedDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $smokeEncounterDocumentDenialId = $createdEncounterDeniedDocument.id
    $createdDeniedDocumentVisible = @($createdEncounterDeniedDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentDenialId `
            -and $_.name -eq $deniedDocumentName `
            -and $_.reviewStatus -eq "pending" `
            -and [string]::IsNullOrWhiteSpace($_.reviewedBy)
    } | Select-Object -First 1

    $denyBody = @{
        reviewStatus = "denied"
        reviewedBy = "admin"
    } | ConvertTo-Json
    $deniedEncounterDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013/documents/$smokeEncounterDocumentDenialId/sign" -Method Put -ContentType "application/json" -Body $denyBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deniedEncounterDocumentVisible = @($deniedEncounterDocument.detail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterDocumentDenialId `
            -and $_.name -eq $deniedDocumentName `
            -and $_.reviewStatus -eq "denied" `
            -and $_.reviewedBy -eq "admin" `
            -and -not [string]::IsNullOrWhiteSpace($_.reviewedAt)
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentDenialId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterDocumentDenialId = $null
    $afterDeleteDeniedDocumentDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedDeniedDocumentVisible = @($afterDeleteDeniedDocumentDetail.documents | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $deniedDocumentName
    } | Select-Object -First 1

    $encounterDocumentDenialPassed = $null -ne $createdDeniedDocumentVisible `
        -and $null -ne $deniedEncounterDocumentVisible `
        -and $null -eq $deletedDeniedDocumentVisible
    Add-Check -Name "encounter document denial lifecycle" -Result $(if ($encounterDocumentDenialPassed) { "passed" } else { "failed" }) -Details @{
        encounter = 1000013
        documentId = $createdEncounterDeniedDocument.id
        document = $deniedEncounterDocumentVisible
    }
}
catch {
    Add-Check -Name "encounter document denial lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeEncounterDocumentDenialId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$smokeEncounterDocumentDenialId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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

    $procedureDiagnosisDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000011" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $procedureEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000011" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

    $createdEncounterProcedureOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $procedureOrderBody -TimeoutSec 20
    $smokeEncounterProcedureOrderId = $createdEncounterProcedureOrder.id
    $refreshedEncounterProcedureDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $createdEncounterProcedureOrderRow = @($refreshedEncounterProcedureDetail.procedureOrders | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterProcedureOrderId `
            -and $_.name -eq $procedureOrderName `
            -and $_.code -eq "80053" `
            -and $_.diagnosis -eq "E78.5" `
            -and $_.orderStatus -eq "pending" `
            -and $_.orderPriority -eq "routine" `
            -and @($_.reports).Count -eq 0
    } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeEncounterProcedureOrderId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterProcedureOrderId = $null
    $afterDeleteEncounterProcedureDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeEncounterProcedureOrderId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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

    $createdEncounterProcedureResultOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $procedureResultOrderBody -TimeoutSec 20
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
    $createdEncounterProcedureResultReport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/reports" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $procedureResultReportBody -TimeoutSec 20

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
    $createdEncounterProcedureResult = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/results" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $procedureResultBody -TimeoutSec 20

    $refreshedEncounterProcedureResultDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $createdEncounterProcedureResultOrderRow = @($refreshedEncounterProcedureResultDetail.procedureOrders | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeEncounterProcedureResultOrderId `
            -and $_.name -eq $procedureResultOrderName `
            -and $_.code -eq "80053" `
            -and $_.diagnosis -eq "E78.5" `
            -and $_.orderStatus -eq "pending"
    } | Select-Object -First 1
    $createdEncounterProcedureResultReportRow = if ($null -ne $createdEncounterProcedureResultOrderRow) {
        @($createdEncounterProcedureResultOrderRow.reports | Where-Object { $null -ne $_ }) | Where-Object {
            $_.id -eq $createdEncounterProcedureResultReport.id `
                -and $_.dateCollected -eq "2026-06-18 12:30" `
                -and $_.specimenNumber -eq "SMOKE-ENC-PROC" `
                -and $_.status -eq "final" `
                -and $_.reviewStatus -eq "reviewed"
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

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeEncounterProcedureResultOrderId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeEncounterProcedureResultOrderId = $null
    $afterDeleteEncounterProcedureResultDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeEncounterProcedureResultOrderId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$smokeProcedureResultCorrectionOrderId = $null
try {
    $procedureCorrectionSuffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $procedureCorrectionOrderName = "Smoke Procedure Result Correction $procedureCorrectionSuffix"
    $procedureCorrectionInitialText = "Smoke Initial Glucose $procedureCorrectionSuffix"
    $procedureCorrectionText = "Smoke Corrected Glucose $procedureCorrectionSuffix"
    $procedureCorrectionOrderBody = @{
        patientId = "MOD-PAT-0001"
        providerId = $null
        encounterId = 1000013
        dateOrdered = "2026-06-18"
        priority = "routine"
        status = "complete"
        procedureCode = "80053"
        procedureName = $procedureCorrectionOrderName
        procedureType = "laboratory"
        diagnosis = "E78.5"
        instructions = "Created by the smoke procedure result correction check."
    } | ConvertTo-Json -Depth 5

    $createdProcedureCorrectionOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $procedureCorrectionOrderBody -TimeoutSec 20
    $smokeProcedureResultCorrectionOrderId = $createdProcedureCorrectionOrder.id

    $procedureCorrectionReportBody = @{
        orderId = $smokeProcedureResultCorrectionOrderId
        dateCollected = "2026-06-18 12:30:00"
        dateReport = "2026-06-18 13:00:00"
        specimenNumber = "SMOKE-PROC-CORR"
        reportStatus = "final"
        reviewStatus = "reviewed"
        notes = "Created by the smoke procedure result correction check."
    } | ConvertTo-Json -Depth 5
    $createdProcedureCorrectionReport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/reports" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $procedureCorrectionReportBody -TimeoutSec 20

    $procedureCorrectionInitialBody = @{
        reportId = $createdProcedureCorrectionReport.id
        resultCode = "2345-7"
        resultText = $procedureCorrectionInitialText
        dateTime = "2026-06-18 13:05:00"
        facility = "Legacy EHR Modernization Clinic"
        units = "mg/dL"
        result = "104"
        range = "70-99"
        abnormal = "high"
        comments = "Initial smoke procedure result before correction."
        status = "final"
    } | ConvertTo-Json -Depth 5
    $createdProcedureCorrectionResult = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/results" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $procedureCorrectionInitialBody -TimeoutSec 20

    $procedureCorrectionBody = @{
        resultCode = "2345-7"
        resultText = $procedureCorrectionText
        dateTime = "2026-06-18 13:35:00"
        units = "mg/dL"
        result = "118"
        range = "70-110"
        abnormal = "borderline"
        status = "corrected"
    } | ConvertTo-Json -Depth 5
    $correctedProcedureResultResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/results/$($createdProcedureCorrectionResult.id)" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $procedureCorrectionBody -TimeoutSec 20

    $procedureCorrectionDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $procedureCorrectionOrderRow = @($procedureCorrectionDetail.orders | Where-Object { $null -ne $_ }) | Where-Object {
        $_.id -eq $smokeProcedureResultCorrectionOrderId -and $_.name -eq $procedureCorrectionOrderName
    } | Select-Object -First 1
    $procedureCorrectionReportRow = if ($null -ne $procedureCorrectionOrderRow) {
        @($procedureCorrectionOrderRow.reports | Where-Object { $null -ne $_ }) | Where-Object {
            $_.id -eq $createdProcedureCorrectionReport.id
        } | Select-Object -First 1
    } else {
        $null
    }
    $procedureCorrectionResultRow = if ($null -ne $procedureCorrectionReportRow) {
        @($procedureCorrectionReportRow.results | Where-Object { $null -ne $_ }) | Where-Object {
            $_.id -eq $createdProcedureCorrectionResult.id `
                -and $_.text -eq $procedureCorrectionText `
                -and $_.result -eq "118" `
                -and $_.units -eq "mg/dL" `
                -and $_.range -eq "70-110" `
                -and $_.abnormal -eq "borderline" `
                -and $_.resultStatus -eq "corrected"
        } | Select-Object -First 1
    } else {
        $null
    }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeProcedureResultCorrectionOrderId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $smokeProcedureResultCorrectionOrderId = $null
    $afterDeleteProcedureCorrectionDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deletedProcedureCorrectionOrderRow = @($afterDeleteProcedureCorrectionDetail.orders | Where-Object { $null -ne $_ }) | Where-Object {
        $_.name -eq $procedureCorrectionOrderName
    } | Select-Object -First 1

    $procedureResultCorrectionPassed = $correctedProcedureResultResponse.id -eq $createdProcedureCorrectionResult.id `
        -and $null -ne $procedureCorrectionOrderRow `
        -and $null -ne $procedureCorrectionReportRow `
        -and $null -ne $procedureCorrectionResultRow `
        -and $null -eq $deletedProcedureCorrectionOrderRow
    Add-Check -Name "procedure result correction lifecycle" -Result $(if ($procedureResultCorrectionPassed) { "passed" } else { "failed" }) -Details @{
        createdOrderId = $createdProcedureCorrectionOrder.id
        reportId = $createdProcedureCorrectionReport.id
        resultId = $createdProcedureCorrectionResult.id
        order = $procedureCorrectionOrderRow
        report = $procedureCorrectionReportRow
        result = $procedureCorrectionResultRow
    }
}
catch {
    Add-Check -Name "procedure result correction lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $smokeProcedureResultCorrectionOrderId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$smokeProcedureResultCorrectionOrderId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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

try {
    if ($null -eq $encounterDetail) {
        throw "Anchor encounter detail did not load."
    }

    $attachedDocuments = if ($null -eq $encounterDetail.documents) { @() } else { @($encounterDetail.documents) }
    $intakePacketAttachment = $attachedDocuments | Where-Object { $_.documentKey -eq "DOC-MOD-PAT-0001-1" } | Select-Object -First 1
    $advanceDirectiveAttachment = $attachedDocuments | Where-Object { $_.documentKey -eq "DOC-MOD-PAT-0001-2" } | Select-Object -First 1
    $encounterDocumentRevisionPassed = $encounterDetail.encounter -eq 1000013 `
        -and $attachedDocuments.Count -eq 2 `
        -and $null -ne $intakePacketAttachment `
        -and $intakePacketAttachment.versionLabel -eq "Version 1" `
        -and $intakePacketAttachment.versionStatus -eq "Current version" `
        -and $intakePacketAttachment.versionHistoryCount -eq 1 `
        -and $intakePacketAttachment.hasPriorVersions -eq $false `
        -and $intakePacketAttachment.revisionAt -eq "2026-06-10 14:30" `
        -and $intakePacketAttachment.revisionHash -eq $intakePacketAttachment.hash `
        -and $null -ne $advanceDirectiveAttachment `
        -and $advanceDirectiveAttachment.versionLabel -eq "Version 1" `
        -and $advanceDirectiveAttachment.versionStatus -eq "Current version" `
        -and $advanceDirectiveAttachment.versionHistoryCount -eq 1 `
        -and $advanceDirectiveAttachment.hasPriorVersions -eq $false `
        -and $advanceDirectiveAttachment.revisionAt -eq "2026-06-12 15:00" `
        -and $advanceDirectiveAttachment.revisionHash -eq $advanceDirectiveAttachment.hash
    Add-Check -Name "anchor encounter document revision readiness" -Result $(if ($encounterDocumentRevisionPassed) { "passed" } else { "failed" }) -Details @{
        encounter = $encounterDetail.encounter
        documents = $attachedDocuments | Select-Object name, documentKey, versionLabel, versionStatus, versionHistoryCount, hasPriorVersions, revisionAt, revisionHash, hash
    }
}
catch {
    Add-Check -Name "anchor encounter document revision readiness" -Result "failed" -Details $_.Exception.Message
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
    $createdEncounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters" -Method Post -ContentType "application/json" -Body $createEncounterBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $createdVitals = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId/vitals" -Method Post -ContentType "application/json" -Body $vitalsBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $soapBody = @{
        dateTime = "2026-06-18 10:10:00"
        subjective = "Patient reports smoke workflow symptoms are stable."
        objective = "Vitals reviewed during smoke workflow."
        assessment = "Stable smoke workflow condition."
        plan = "Continue smoke workflow validation."
    } | ConvertTo-Json
    $createdSoap = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId/soap-notes" -Method Post -ContentType "application/json" -Body $soapBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $updateBody = @{
        reason = "Smoke Encounter Mutation Updated"
        billingNote = "Updated by the smoke encounter mutation check."
    } | ConvertTo-Json
    $updatedEncounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId" -Method Put -ContentType "application/json" -Body $updateBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $encounterMutationPassed = $createdEncounter.reason -eq "Smoke Encounter Mutation" `
        -and $createdVitals.id -gt 0 `
        -and $createdSoap.id -gt 0 `
        -and $updatedEncounter.reason -eq "Smoke Encounter Mutation Updated" `
        -and $updatedEncounter.billingNote -eq "Updated by the smoke encounter mutation check." `
        -and $updatedEncounter.vitals.bloodPressure -eq "128/76" `
        -and $updatedEncounter.soapNote.assessment -eq "Stable smoke workflow condition."

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdMetadataEncounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters" -Method Post -ContentType "application/json" -Body $createMetadataBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $encounterMetadataMutationId = $createdMetadataEncounter.encounter

    $updateMetadataBody = @{
        reason = "Smoke Encounter Metadata $metadataSuffix Updated"
        sensitivity = "high"
        referralSource = "physician"
        externalId = "UPD-$metadataSuffix"
        posCode = 22
        billingNote = "Updated by the smoke encounter metadata check."
    } | ConvertTo-Json
    $updatedMetadataEncounter = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMetadataMutationId" -Method Put -ContentType "application/json" -Body $updateMetadataBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $encounterMetadataPassed = $createdMetadataEncounter.sensitivity -eq "normal" `
        -and $createdMetadataEncounter.referralSource -eq "self" `
        -and $createdMetadataEncounter.externalId -eq "EXT-$metadataSuffix" `
        -and $createdMetadataEncounter.posCode -eq 11 `
        -and $updatedMetadataEncounter.sensitivity -eq "high" `
        -and $updatedMetadataEncounter.referralSource -eq "physician" `
        -and $updatedMetadataEncounter.externalId -eq "UPD-$metadataSuffix" `
        -and $updatedMetadataEncounter.posCode -eq 22 `
        -and $updatedMetadataEncounter.billingNote -eq "Updated by the smoke encounter metadata check."

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMetadataMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/$encounterMetadataMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $frontDeskClinicalListsStatus = 0
    $frontDeskClinicalListMutationStatus = 0
    try {
        $frontDeskClinicalLists = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/clinical-lists/MOD-PAT-0001" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskClinicalListsStatus = [int]$frontDeskClinicalLists.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskClinicalListsStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $frontDeskClinicalListMutationBody = @{
        patientId = "MOD-PAT-0001"
        title = "Forbidden Smoke Allergy"
        dateTime = "2026-06-18 09:00:00"
        comments = "This request should be rejected before mutation."
        reaction = "Rash"
        severity = "mild"
        listOptionId = "parity-allergy"
    } | ConvertTo-Json
    try {
        $frontDeskClinicalListMutation = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/clinical-lists/allergies" `
            -Method Post `
            -Headers (Get-FrontDeskHeaders) `
            -ContentType "application/json" `
            -Body $frontDeskClinicalListMutationBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskClinicalListMutationStatus = [int]$frontDeskClinicalListMutation.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskClinicalListMutationStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $clinicalLists = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $problem = $clinicalLists.problems | Where-Object { $_.title -like "*diabetes*" } | Select-Object -First 1
    $allergy = $clinicalLists.allergies | Where-Object { $_.title -eq "Penicillin" } | Select-Object -First 1
    $medication = $clinicalLists.medications | Where-Object { $_.title -like "Metformin*" } | Select-Object -First 1
    $prescription = $clinicalLists.prescriptions | Where-Object { $_.drug -eq "Metformin" } | Select-Object -First 1
    $clinicalListsPassed = $frontDeskClinicalListsStatus -eq 403 -and $frontDeskClinicalListMutationStatus -eq 403 -and $clinicalLists.patientId -eq "MOD-PAT-0001" -and $null -ne $problem -and $null -ne $allergy -and $null -ne $medication -and $null -ne $prescription
    Add-Check -Name "anchor clinical lists" -Result $(if ($clinicalListsPassed) { "passed" } else { "failed" }) -Details @{
        frontDeskStatus = $frontDeskClinicalListsStatus
        frontDeskMutationStatus = $frontDeskClinicalListMutationStatus
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
    $immunizationLists = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/MOD-PAT-0007" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $createdAllergy = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/allergies" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createAllergyBody -TimeoutSec 20
    $clinicalAllergyMutationId = $createdAllergy.id
    $createdVisible = $createdAllergy.detail.allergies | Where-Object { $_.title -eq $allergyTitle -and $_.reaction -eq "Rash" -and $_.severity -eq "mild" } | Select-Object -First 1

    $deactivateBody = @{
        comments = "Deactivated by the smoke clinical-list mutation check."
    } | ConvertTo-Json
    $deactivatedAllergy = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/allergies/$clinicalAllergyMutationId/deactivate" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $deactivateBody -TimeoutSec 20
    $inactiveVisible = $deactivatedAllergy.detail.allergies | Where-Object { $_.title -eq $allergyTitle } | Select-Object -First 1
    $clinicalAllergyMutationPassed = $null -ne $createdVisible -and $null -eq $inactiveVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/allergies/$clinicalAllergyMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/allergies/$clinicalAllergyMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdProblem = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createProblemBody -TimeoutSec 20
    $clinicalProblemMutationId = $createdProblem.id
    $createdProblemVisible = $createdProblem.detail.problems | Where-Object { $_.title -eq $problemTitle -and $_.diagnosis -eq "ICD10:Z00.00" -and $_.activity -eq 1 } | Select-Object -First 1

    $deactivateProblemBody = @{
        comments = "Deactivated by the smoke problem-list mutation check."
    } | ConvertTo-Json
    $deactivatedProblem = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$clinicalProblemMutationId/deactivate" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $deactivateProblemBody -TimeoutSec 20
    $inactiveProblemVisible = $deactivatedProblem.detail.problems | Where-Object { $_.title -eq $problemTitle } | Select-Object -First 1
    $clinicalProblemMutationPassed = $null -ne $createdProblemVisible -and $null -eq $inactiveProblemVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$clinicalProblemMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/problems/$clinicalProblemMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdMedication = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createMedicationBody -TimeoutSec 20
    $clinicalMedicationMutationId = $createdMedication.id
    $createdMedicationVisible = $createdMedication.detail.medications | Where-Object { $_.title -eq $medicationTitle -and $_.diagnosis -eq "ICD10:Z00.00" -and $_.activity -eq 1 } | Select-Object -First 1

    $deactivateMedicationBody = @{
        comments = "Deactivated by the smoke medication-list mutation check."
    } | ConvertTo-Json
    $deactivatedMedication = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$clinicalMedicationMutationId/deactivate" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $deactivateMedicationBody -TimeoutSec 20
    $inactiveMedicationVisible = $deactivatedMedication.detail.medications | Where-Object { $_.title -eq $medicationTitle } | Select-Object -First 1
    $clinicalMedicationMutationPassed = $null -ne $createdMedicationVisible -and $null -eq $inactiveMedicationVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$clinicalMedicationMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/medications/$clinicalMedicationMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdPrescription = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createPrescriptionBody -TimeoutSec 20
    $clinicalPrescriptionMutationId = $createdPrescription.id
    $createdPrescriptionVisible = $createdPrescription.detail.prescriptions | Where-Object { $_.drug -eq $prescriptionDrug -and $_.dosage -eq "1 tablet daily" -and $_.active -eq 1 } | Select-Object -First 1

    $deactivatePrescriptionBody = @{
        endDate = "2026-08-15"
        note = "Deactivated by the smoke prescription mutation check."
    } | ConvertTo-Json
    $deactivatedPrescription = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$clinicalPrescriptionMutationId/deactivate" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $deactivatePrescriptionBody -TimeoutSec 20
    $inactivePrescriptionVisible = $deactivatedPrescription.detail.prescriptions | Where-Object { $_.drug -eq $prescriptionDrug } | Select-Object -First 1
    $clinicalPrescriptionMutationPassed = $null -ne $createdPrescriptionVisible -and $null -eq $inactivePrescriptionVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$clinicalPrescriptionMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/prescriptions/$clinicalPrescriptionMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdImmunization = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/immunizations" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createImmunizationBody -TimeoutSec 20
    $clinicalImmunizationMutationId = $createdImmunization.id
    $createdImmunizationVisible = $createdImmunization.detail.immunizations | Where-Object { $_.lotNumber -eq $immunizationLot -and $_.cvxCode -eq "141" } | Select-Object -First 1

    $enteredInErrorBody = @{
        note = "Marked entered in error by the smoke immunization mutation check."
    } | ConvertTo-Json
    $enteredInErrorImmunization = Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/immunizations/$clinicalImmunizationMutationId/entered-in-error" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $enteredInErrorBody -TimeoutSec 20
    $enteredInErrorVisible = $enteredInErrorImmunization.detail.immunizations | Where-Object { $_.lotNumber -eq $immunizationLot } | Select-Object -First 1
    $clinicalImmunizationMutationPassed = $null -ne $createdImmunizationVisible -and $null -eq $enteredInErrorVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/immunizations/$clinicalImmunizationMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/clinical-lists/immunizations/$clinicalImmunizationMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $unauthenticatedMessageSearchStatus = 0
    $frontDeskMessageSearchStatus = 0
    $frontDeskMessageCreateStatus = 0
    try {
        $unauthenticatedMessageSearch = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/messages/MOD-PAT-0004" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedMessageSearchStatus = [int]$unauthenticatedMessageSearch.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedMessageSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }
    try {
        $frontDeskMessageSearch = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/messages/MOD-PAT-0004" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskMessageSearchStatus = [int]$frontDeskMessageSearch.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskMessageSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }
    try {
        $frontDeskMessageCreateBody = @{
            patientId = "MOD-PAT-0004"
            title = "Blocked Message Authorization Smoke"
            body = "This request should be rejected before mutation."
            assignedTo = "admin"
        } | ConvertTo-Json
        $frontDeskMessageCreate = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/messages" `
            -Method Post `
            -Headers (Get-FrontDeskHeaders) `
            -ContentType "application/json" `
            -Body $frontDeskMessageCreateBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskMessageCreateStatus = [int]$frontDeskMessageCreate.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskMessageCreateStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $messages = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/MOD-PAT-0004" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $careTeamMessage = $messages.messages | Where-Object { $_.title -eq "Care team follow-up" -and $_.status -eq "New" } | Select-Object -First 1
    $portalMessage = $messages.messages | Where-Object { $_.title -eq "Portal message" -and $_.status -eq "Done" } | Select-Object -First 1
    $messagesPassed = $messages.patientId -eq "MOD-PAT-0004" `
        -and $unauthenticatedMessageSearchStatus -eq 401 `
        -and $frontDeskMessageSearchStatus -eq 403 `
        -and $frontDeskMessageCreateStatus -eq 403 `
        -and $messages.portalEnabled `
        -and $null -ne $careTeamMessage `
        -and $null -ne $portalMessage `
        -and $careTeamMessage.portalRelation -eq $null `
        -and -not $careTeamMessage.isEncrypted `
        -and $portalMessage.portalRelation -eq "portal:MOD-PAT-0004" `
        -and -not $portalMessage.isEncrypted
    Add-Check -Name "anchor patient messages" -Result $(if ($messagesPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $messages.patientId
        unauthenticatedMessageSearchStatus = $unauthenticatedMessageSearchStatus
        frontDeskMessageSearchStatus = $frontDeskMessageSearchStatus
        frontDeskMessageCreateStatus = $frontDeskMessageCreateStatus
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
    $unauthenticatedDocumentSearchStatus = 0
    try {
        $unauthenticatedDocumentSearch = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedDocumentSearchStatus = [int]$unauthenticatedDocumentSearch.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedDocumentSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $documents = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $intakePacket = $documents.documents | Where-Object { $_.name -eq "Primary care intake packet" -and $_.categoryName -eq "Medical Record" } | Select-Object -First 1
    $advanceDirective = $documents.documents | Where-Object { $_.name -eq "Advance directive acknowledgement" -and $_.categoryName -eq "Advance Directive" } | Select-Object -First 1
    $frontDeskDocumentSearchStatus = 0
    $frontDeskDocumentContentStatus = 0
    $frontDeskDocumentCreateStatus = 0
    $unauthenticatedDocumentContentStatus = 0
    if ($null -ne $intakePacket) {
        try {
            $unauthenticatedDocumentContent = Invoke-WebRequest `
                -Uri "$ApiBaseUrl/api/documents/$($intakePacket.id)/content" `
                -Method Get `
                -TimeoutSec 20 `
                -ErrorAction Stop
            $unauthenticatedDocumentContentStatus = [int]$unauthenticatedDocumentContent.StatusCode
        }
        catch {
            if ($_.Exception.Response) {
                $unauthenticatedDocumentContentStatus = [int]$_.Exception.Response.StatusCode
            }
            else {
                throw
            }
        }

        try {
            $frontDeskDocumentContent = Invoke-WebRequest `
                -Uri "$ApiBaseUrl/api/documents/$($intakePacket.id)/content" `
                -Method Get `
                -Headers (Get-FrontDeskHeaders) `
                -TimeoutSec 20 `
                -ErrorAction Stop
            $frontDeskDocumentContentStatus = [int]$frontDeskDocumentContent.StatusCode
        }
        catch {
            if ($_.Exception.Response) {
                $frontDeskDocumentContentStatus = [int]$_.Exception.Response.StatusCode
            }
            else {
                throw
            }
        }
    }

    try {
        $frontDeskDocumentSearch = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskDocumentSearchStatus = [int]$frontDeskDocumentSearch.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskDocumentSearchStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $frontDeskCreateDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = "Blocked Smoke Patient Document"
        docDate = "2026-06-18"
        encounter = 1000013
        content = "This front-desk document create should be rejected before mutation."
        notes = "Blocked by document authorization smoke check."
    } | ConvertTo-Json
    try {
        $frontDeskDocumentCreate = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/documents" `
            -Method Post `
            -Headers (Get-FrontDeskHeaders) `
            -ContentType "application/json" `
            -Body $frontDeskCreateDocumentBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskDocumentCreateStatus = [int]$frontDeskDocumentCreate.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskDocumentCreateStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $documentsPassed = $unauthenticatedDocumentSearchStatus -eq 401 `
        -and $unauthenticatedDocumentContentStatus -eq 401 `
        -and $frontDeskDocumentSearchStatus -eq 403 `
        -and $frontDeskDocumentContentStatus -eq 403 `
        -and $frontDeskDocumentCreateStatus -eq 403 `
        -and $documents.patientId -eq "MOD-PAT-0001" `
        -and $documents.count -eq 2 `
        -and $null -ne $intakePacket `
        -and $null -ne $advanceDirective `
        -and $intakePacket.contentPreview.Contains("Gold synthetic document DOC-MOD-PAT-0001-1")
    Add-Check -Name "anchor patient documents" -Result $(if ($documentsPassed) { "passed" } else { "failed" }) -Details @{
        unauthenticatedSearchStatus = $unauthenticatedDocumentSearchStatus
        unauthenticatedContentStatus = $unauthenticatedDocumentContentStatus
        frontDeskSearchStatus = $frontDeskDocumentSearchStatus
        frontDeskContentStatus = $frontDeskDocumentContentStatus
        frontDeskCreateStatus = $frontDeskDocumentCreateStatus
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
    $documents = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $documents = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $documents = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $intakePacket = $documents.documents | Where-Object { $_.name -eq "Primary care intake packet" -and $_.categoryName -eq "Medical Record" } | Select-Object -First 1
    if ($null -eq $intakePacket) {
        throw "Primary care intake packet document was not found."
    }

    $documentContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$($intakePacket.id)/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $downloadClient = New-AuthenticatedHttpClient
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
    $createdDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientDocumentMutationId = $createdDocument.id
    $createdVisible = $createdDocument.detail.documents | Where-Object { $_.name -eq $documentName -and $_.categoryName -eq "Medical Record" -and $_.contentPreview -and $_.contentPreview.Contains($documentBody) } | Select-Object -First 1

    $signDocumentBody = @{
        reviewStatus = "approved"
        reviewedBy = "admin"
    } | ConvertTo-Json
    $signedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId/sign" -Method Put -ContentType "application/json" -Body $signDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $signedVisible = $signedDocument.detail.documents | Where-Object { $_.name -eq $documentName -and $_.reviewStatus -eq "approved" -and $_.reviewedBy -eq "admin" } | Select-Object -First 1

    $archivedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedVisible = $archivedDocument.detail.documents | Where-Object { $_.name -eq $documentName } | Select-Object -First 1
    $patientDocumentMutationPassed = $null -ne $createdVisible -and $null -ne $signedVisible -and $null -eq $archivedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdDeniedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createDeniedDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientDocumentDenialMutationId = $createdDeniedDocument.id
    $createdDeniedVisible = $createdDeniedDocument.detail.documents | Where-Object { $_.name -eq $deniedDocumentName -and $_.reviewStatus -eq "pending" } | Select-Object -First 1

    $denyDocumentBody = @{
        reviewStatus = "denied"
        reviewedBy = "admin"
    } | ConvertTo-Json
    $deniedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentDenialMutationId/sign" -Method Put -ContentType "application/json" -Body $denyDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $deniedVisible = $deniedDocument.detail.documents | Where-Object { $_.name -eq $deniedDocumentName -and $_.reviewStatus -eq "denied" -and $_.reviewedBy -eq "admin" } | Select-Object -First 1

    $archivedDeniedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentDenialMutationId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedDeniedVisible = $archivedDeniedDocument.detail.documents | Where-Object { $_.name -eq $deniedDocumentName } | Select-Object -First 1
    $patientDocumentDenialPassed = $null -ne $createdDeniedVisible -and $null -ne $deniedVisible -and $null -eq $archivedDeniedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentDenialMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentDenialMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdMetadataDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createMetadataDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientDocumentMetadataMutationId = $createdMetadataDocument.id
    $createdMetadataVisible = $createdMetadataDocument.detail.documents | Where-Object { $_.name -eq $metadataDocumentName -and $_.categoryName -eq "Medical Record" } | Select-Object -First 1

    $updateMetadataDocumentBody = @{
        categoryId = 6
        name = $metadataDocumentUpdatedName
        docDate = "2026-06-19"
        encounter = 1000014
        notes = $metadataDocumentUpdatedNotes
    } | ConvertTo-Json
    $updatedMetadataDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId/metadata" -Method Put -ContentType "application/json" -Body $updateMetadataDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $updatedMetadataVisible = $updatedMetadataDocument.detail.documents | Where-Object { $_.name -eq $metadataDocumentUpdatedName -and $_.categoryName -eq "Advance Directive" -and $_.docDate -eq "2026-06-19" -and $_.encounter -eq 1000014 -and $_.notes -eq $metadataDocumentUpdatedNotes } | Select-Object -First 1
    $metadataContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $archivedMetadataDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentMetadataMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdReplaceDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createReplaceDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientDocumentContentReplaceId = $createdReplaceDocument.id
    $createdReplaceVisible = $createdReplaceDocument.detail.documents | Where-Object { $_.name -eq $replaceDocumentName -and $_.contentPreview -and $_.contentPreview.Contains($replaceDocumentOriginalBody) } | Select-Object -First 1
    $createdReplaceContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    Start-Sleep -Seconds 1

    $replaceContentBody = @{
        fileName = $replaceDocumentFileName
        content = $replaceDocumentUpdatedBody
    } | ConvertTo-Json
    $replacedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/content" -Method Put -ContentType "application/json" -Body $replaceContentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $replacedVisible = $replacedDocument.detail.documents | Where-Object { $_.name -eq $replaceDocumentName -and $_.fileName -eq $replaceDocumentFileName -and $_.contentPreview -and $_.contentPreview.Contains($replaceDocumentUpdatedBody) } | Select-Object -First 1
    $replacedContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

    $replaceDownloadClient = New-AuthenticatedHttpClient
    try {
        $replaceDownload = $replaceDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/download").GetAwaiter().GetResult()
        $replaceDownloadBody = $replaceDownload.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $replaceDownloadContentType = $replaceDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $replaceDownloadClient.Dispose()
    }

    $archivedReplacedDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentContentReplaceId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdArchiveDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createArchiveDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientDocumentArchiveRestoreId = $createdArchiveDocument.id
    $createdArchiveVisible = $createdArchiveDocument.detail.documents | Where-Object { $_.name -eq $archiveDocumentName -and $_.deleted -eq 0 } | Select-Object -First 1

    $archivedArchiveDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedDefaultVisible = $archivedArchiveDocument.detail.documents | Where-Object { $_.name -eq $archiveDocumentName } | Select-Object -First 1
    $archivedList = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001?includeArchived=true" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedIncludeVisible = $archivedList.documents | Where-Object { $_.name -eq $archiveDocumentName -and $_.deleted -eq 1 } | Select-Object -First 1

    $archiveContentClient = New-AuthenticatedHttpClient
    try {
        $archivedContent = $archiveContentClient.GetAsync("$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId/content").GetAwaiter().GetResult()
        $archivedContentStatus = [int]$archivedContent.StatusCode
    }
    finally {
        $archiveContentClient.Dispose()
    }

    $restoredArchiveDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId/restore" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $restoredArchiveVisible = $restoredArchiveDocument.detail.documents | Where-Object { $_.name -eq $archiveDocumentName -and $_.deleted -eq 0 } | Select-Object -First 1
    $restoredArchiveContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $patientDocumentArchiveRestorePassed = $null -ne $createdArchiveVisible `
        -and $null -eq $archivedDefaultVisible `
        -and $null -ne $archivedIncludeVisible `
        -and $archivedContentStatus -eq 404 `
        -and $null -ne $restoredArchiveVisible `
        -and $restoredArchiveContent.content.Contains($archiveDocumentBody)

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentArchiveRestoreId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientDocumentLifecycleId = $null
try {
    $lifecycleDocumentName = "Smoke Lifecycle Patient Document"
    $lifecycleDocumentBody = "Created by the smoke patient-document lifecycle timeline check."
    $createLifecycleDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $lifecycleDocumentName
        docDate = "2026-06-19"
        encounter = 1000013
        content = $lifecycleDocumentBody
        notes = "Created by the smoke patient-document lifecycle timeline check."
    } | ConvertTo-Json
    $createdLifecycleDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents" -Method Post -ContentType "application/json" -Body $createLifecycleDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientDocumentLifecycleId = $createdLifecycleDocument.id
    $createdLifecycleVisible = $createdLifecycleDocument.detail.documents | Where-Object { $_.id -eq $patientDocumentLifecycleId -and $_.name -eq $lifecycleDocumentName } | Select-Object -First 1
    $createdLifecycleCodes = if ($null -eq $createdLifecycleVisible) { @() } else { @($createdLifecycleVisible.lifecycleEvents | ForEach-Object { $_.code }) }

    $signLifecycleBody = @{
        reviewStatus = "approved"
        reviewedBy = "admin"
    } | ConvertTo-Json
    $signedLifecycleDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentLifecycleId/sign" -Method Put -ContentType "application/json" -Body $signLifecycleBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $signedLifecycleVisible = $signedLifecycleDocument.detail.documents | Where-Object { $_.id -eq $patientDocumentLifecycleId -and $_.reviewStatus -eq "approved" -and $_.reviewedBy -eq "admin" } | Select-Object -First 1
    $signedLifecycleCodes = if ($null -eq $signedLifecycleVisible) { @() } else { @($signedLifecycleVisible.lifecycleEvents | ForEach-Object { $_.code }) }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentLifecycleId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $archivedLifecycleList = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/MOD-PAT-0001?includeArchived=true" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedLifecycleVisible = $archivedLifecycleList.documents | Where-Object { $_.id -eq $patientDocumentLifecycleId -and $_.deleted -eq 1 } | Select-Object -First 1
    $archivedLifecycleCodes = if ($null -eq $archivedLifecycleVisible) { @() } else { @($archivedLifecycleVisible.lifecycleEvents | ForEach-Object { $_.code }) }

    $restoredLifecycleDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentLifecycleId/restore" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $restoredLifecycleVisible = $restoredLifecycleDocument.detail.documents | Where-Object { $_.id -eq $patientDocumentLifecycleId -and $_.deleted -eq 0 } | Select-Object -First 1
    $restoredLifecycleCodes = if ($null -eq $restoredLifecycleVisible) { @() } else { @($restoredLifecycleVisible.lifecycleEvents | ForEach-Object { $_.code }) }

    $viewerLifecycleContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentLifecycleId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $viewerLifecycleCodes = @($viewerLifecycleContent.lifecycleEvents | ForEach-Object { $_.code })

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentLifecycleId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $patientDocumentLifecycleId = $null

    $patientDocumentLifecyclePassed = $null -ne $createdLifecycleVisible `
        -and ($createdLifecycleCodes -contains "filed") `
        -and ($createdLifecycleCodes -contains "current-version") `
        -and ($createdLifecycleCodes -contains "review-pending") `
        -and ($createdLifecycleCodes -contains "active") `
        -and $null -ne $signedLifecycleVisible `
        -and ($signedLifecycleCodes -contains "review-approved") `
        -and $null -ne $archivedLifecycleVisible `
        -and ($archivedLifecycleCodes -contains "archived") `
        -and $null -ne $restoredLifecycleVisible `
        -and ($restoredLifecycleCodes -contains "active") `
        -and ($viewerLifecycleCodes -contains "review-approved")
    Add-Check -Name "patient document lifecycle timeline" -Result $(if ($patientDocumentLifecyclePassed) { "passed" } else { "failed" }) -Details @{
        documentId = $createdLifecycleDocument.id
        createdCodes = $createdLifecycleCodes
        signedCodes = $signedLifecycleCodes
        archivedCodes = $archivedLifecycleCodes
        restoredCodes = $restoredLifecycleCodes
        viewerCodes = $viewerLifecycleCodes
    }
}
catch {
    Add-Check -Name "patient document lifecycle timeline" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientDocumentLifecycleId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientDocumentLifecycleId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientScannedDocumentId = $null
try {
    $scanDocumentName = "Smoke Scanned Attachment Patient Document"
    $scanFileName = "smoke-scanned-attachment.pdf"
    $scanNotes = "Scan source: front-desk scanner; OCR pending; Created by the smoke scanned attachment readiness check."
    $scanPdfText = "%PDF-1.4`n% Smoke scanned attachment readiness PDF`n1 0 obj << /Type /Catalog >> endobj`n%%EOF"
    $scanPdfBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($scanPdfText))
    $createScanDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $scanDocumentName
        docDate = "2026-06-19"
        encounter = 1000013
        fileName = $scanFileName
        mimetype = "application/pdf"
        contentBase64 = $scanPdfBase64
        notes = $scanNotes
    } | ConvertTo-Json
    $createdScanDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/binary" -Method Post -ContentType "application/json" -Body $createScanDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientScannedDocumentId = $createdScanDocument.id
    $createdScanVisible = $createdScanDocument.detail.documents | Where-Object { $_.id -eq $patientScannedDocumentId -and $_.name -eq $scanDocumentName } | Select-Object -First 1
    $scanContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientScannedDocumentId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $patientScannedAttachmentPassed = $null -ne $createdScanVisible `
        -and $createdScanVisible.isScannedAttachment `
        -and $createdScanVisible.scanStatus -eq "Scanned attachment" `
        -and $createdScanVisible.captureSource -eq "front-desk scanner" `
        -and $createdScanVisible.scanPageCount -eq 1 `
        -and $createdScanVisible.ocrStatus -eq "OCR pending" `
        -and $scanContent.isScannedAttachment `
        -and $scanContent.scanStatus -eq "Scanned attachment" `
        -and $scanContent.captureSource -eq "front-desk scanner" `
        -and $scanContent.scanPageCount -eq 1 `
        -and $scanContent.ocrStatus -eq "OCR pending"

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientScannedDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $patientScannedDocumentId = $null

    Add-Check -Name "patient scanned attachment readiness" -Result $(if ($patientScannedAttachmentPassed) { "passed" } else { "failed" }) -Details @{
        documentId = $createdScanDocument.id
        scanListStatus = $createdScanVisible.scanStatus
        scanViewerStatus = $scanContent.scanStatus
        captureSource = $scanContent.captureSource
        scanPageCount = $scanContent.scanPageCount
        ocrStatus = $scanContent.ocrStatus
    }
}
catch {
    Add-Check -Name "patient scanned attachment readiness" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientScannedDocumentId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientScannedDocumentId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdBinaryDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/binary" -Method Post -ContentType "application/json" -Body $createBinaryDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientBinaryDocumentMutationId = $createdBinaryDocument.id
    $createdBinaryVisible = $createdBinaryDocument.detail.documents | Where-Object { $_.name -eq $binaryDocumentName -and $_.mimetype -eq "application/pdf" -and $_.contentPreview -and $_.contentPreview.Contains("Binary document") } | Select-Object -First 1

    $binaryContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $binaryDownloadClient = New-AuthenticatedHttpClient
    try {
        $binaryDownload = $binaryDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId/download").GetAwaiter().GetResult()
        $binaryDownloadBytes = $binaryDownload.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $binaryDownloadContentType = $binaryDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $binaryDownloadClient.Dispose()
    }

    $archivedBinaryDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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

    $patientPdfInlinePreviewPassed = $null -ne $createdBinaryVisible `
        -and $createdBinaryVisible.previewKind -eq "pdf" `
        -and $createdBinaryVisible.previewStatus -eq "Inline PDF preview" `
        -and $createdBinaryVisible.canPreviewInline `
        -and $binaryContent.previewKind -eq "pdf" `
        -and $binaryContent.previewStatus -eq "Inline PDF preview" `
        -and $binaryContent.canPreviewInline

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $patientBinaryDocumentMutationId = $null

    Add-Check -Name "patient binary document mutation lifecycle" -Result $(if ($patientBinaryDocumentMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdBinaryDocument.id
        createdVisible = $createdBinaryVisible
        contentIsBinary = $binaryContent.isBinary
        downloadStatus = [int]$binaryDownload.StatusCode
        downloadContentType = $binaryDownloadContentType
        archivedVisible = $archivedBinaryVisible
    }
    Add-Check -Name "patient PDF inline preview readiness" -Result $(if ($patientPdfInlinePreviewPassed) { "passed" } else { "failed" }) -Details @{
        createdPreviewKind = $createdBinaryVisible.previewKind
        createdPreviewStatus = $createdBinaryVisible.previewStatus
        createdCanPreviewInline = $createdBinaryVisible.canPreviewInline
        contentPreviewKind = $binaryContent.previewKind
        contentPreviewStatus = $binaryContent.previewStatus
        contentCanPreviewInline = $binaryContent.canPreviewInline
    }
}
catch {
    Add-Check -Name "patient binary document mutation lifecycle" -Result "failed" -Details $_.Exception.Message
    Add-Check -Name "patient PDF inline preview readiness" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientBinaryDocumentMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientBinaryDocumentReplaceId = $null
try {
    $binaryReplaceName = "Smoke Binary Replace Patient Document"
    $binaryReplaceOriginalFileName = "smoke-binary-replace-original.pdf"
    $binaryReplaceUpdatedFileName = "smoke-binary-replace-updated.pdf"
    $binaryReplaceOriginalBody = "%PDF-1.4`n1 0 obj`n<< /Type /Catalog >>`nendobj`n% Smoke patient binary replacement original.`n%%EOF`n"
    $binaryReplaceUpdatedBody = "%PDF-1.4`n1 0 obj`n<< /Type /Catalog >>`nendobj`n% Smoke patient binary replacement updated.`n%%EOF`n"
    $binaryReplaceOriginalBase64 = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($binaryReplaceOriginalBody))
    $binaryReplaceUpdatedBase64 = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($binaryReplaceUpdatedBody))
    $createBinaryReplaceBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $binaryReplaceName
        docDate = "2026-06-18"
        encounter = 1000013
        fileName = $binaryReplaceOriginalFileName
        mimetype = "application/pdf"
        contentBase64 = $binaryReplaceOriginalBase64
        notes = "Created by the smoke patient binary-document content replacement check."
    } | ConvertTo-Json
    $createdBinaryReplace = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/binary" -Method Post -ContentType "application/json" -Body $createBinaryReplaceBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientBinaryDocumentReplaceId = $createdBinaryReplace.id
    $createdBinaryReplaceVisible = $createdBinaryReplace.detail.documents | Where-Object { $_.id -eq $patientBinaryDocumentReplaceId -and $_.fileName -eq $binaryReplaceOriginalFileName } | Select-Object -First 1
    $createdBinaryReplaceContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentReplaceId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    Start-Sleep -Seconds 1

    $replaceBinaryBody = @{
        fileName = $binaryReplaceUpdatedFileName
        mimetype = "application/pdf"
        contentBase64 = $binaryReplaceUpdatedBase64
    } | ConvertTo-Json
    $replacedBinaryDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentReplaceId/content/binary" -Method Put -ContentType "application/json" -Body $replaceBinaryBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $replacedBinaryVisible = $replacedBinaryDocument.detail.documents | Where-Object { $_.id -eq $patientBinaryDocumentReplaceId -and $_.fileName -eq $binaryReplaceUpdatedFileName } | Select-Object -First 1
    $replacedBinaryContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentReplaceId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20

    $binaryReplaceDownloadClient = New-AuthenticatedHttpClient
    try {
        $binaryReplaceDownload = $binaryReplaceDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$patientBinaryDocumentReplaceId/download").GetAwaiter().GetResult()
        $binaryReplaceDownloadBytes = $binaryReplaceDownload.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $binaryReplaceDownloadContentType = $binaryReplaceDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $binaryReplaceDownloadClient.Dispose()
    }

    $patientBinaryDocumentReplacePassed = $null -ne $createdBinaryReplaceVisible `
        -and $null -ne $replacedBinaryVisible `
        -and $createdBinaryReplaceContent.contentBase64 -eq $binaryReplaceOriginalBase64 `
        -and $replacedBinaryContent.id -eq $patientBinaryDocumentReplaceId `
        -and $replacedBinaryContent.name -eq $binaryReplaceName `
        -and $replacedBinaryContent.fileName -eq $binaryReplaceUpdatedFileName `
        -and $replacedBinaryContent.mimetype -eq "application/pdf" `
        -and $replacedBinaryContent.isBinary `
        -and $replacedBinaryContent.contentBase64 -eq $binaryReplaceUpdatedBase64 `
        -and $replacedBinaryContent.contentBase64 -ne $binaryReplaceOriginalBase64 `
        -and $replacedBinaryContent.previewKind -eq "pdf" `
        -and $replacedBinaryContent.previewStatus -eq "Inline PDF preview" `
        -and $replacedBinaryContent.versionLabel -eq "Version 1" `
        -and $replacedBinaryContent.versionStatus -eq "Current version" `
        -and $replacedBinaryContent.versionHistoryCount -eq 1 `
        -and -not $replacedBinaryContent.hasPriorVersions `
        -and $replacedBinaryContent.revisionHash -eq $replacedBinaryContent.hash `
        -and $replacedBinaryContent.hash -ne $createdBinaryReplaceContent.hash `
        -and $replacedBinaryContent.revisionAt -ne $createdBinaryReplaceContent.revisionAt `
        -and $binaryReplaceDownload.IsSuccessStatusCode `
        -and $binaryReplaceDownloadContentType -eq "application/pdf" `
        -and [Convert]::ToBase64String($binaryReplaceDownloadBytes) -eq $binaryReplaceUpdatedBase64

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentReplaceId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $patientBinaryDocumentReplaceId = $null

    Add-Check -Name "patient binary document content replacement lifecycle" -Result $(if ($patientBinaryDocumentReplacePassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdBinaryReplace.id
        originalFileName = $createdBinaryReplaceContent.fileName
        replacementFileName = $replacedBinaryContent.fileName
        replacementPreviewKind = $replacedBinaryContent.previewKind
        replacementRevisionHash = $replacedBinaryContent.revisionHash
        downloadStatus = [int]$binaryReplaceDownload.StatusCode
        downloadContentType = $binaryReplaceDownloadContentType
    }
}
catch {
    Add-Check -Name "patient binary document content replacement lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientBinaryDocumentReplaceId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientBinaryDocumentReplaceId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientImageDocumentPreviewId = $null
try {
    $imageDocumentName = "Smoke Image Patient Document.svg"
    $imageDocumentBody = '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="40" viewBox="0 0 64 40"><rect width="64" height="40" fill="#ffffff"/><path d="M8 28l10-10 8 7 9-12 21 15H8z" fill="#32746d"/><circle cx="48" cy="11" r="5" fill="#f2b84b"/></svg>'
    $imageDocumentBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($imageDocumentBody))
    $imageDocumentThumbnailDataUri = "data:image/svg+xml;base64,$imageDocumentBase64"
    $createImageDocumentBody = @{
        patientId = "MOD-PAT-0001"
        categoryId = 3
        name = $imageDocumentName
        docDate = "2026-06-18"
        encounter = 1000013
        fileName = $imageDocumentName
        mimetype = "image/svg+xml"
        contentBase64 = $imageDocumentBase64
        notes = "Created by the smoke image patient-document preview check."
    } | ConvertTo-Json
    $createdImageDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/binary" -Method Post -ContentType "application/json" -Body $createImageDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientImageDocumentPreviewId = $createdImageDocument.id
    $createdImageVisible = $createdImageDocument.detail.documents | Where-Object {
        $_.name -eq $imageDocumentName `
            -and $_.mimetype -eq "image/svg+xml" `
            -and $_.previewKind -eq "image" `
            -and $_.previewStatus -eq "Inline image preview" `
            -and $_.thumbnailLabel -eq "IMG" `
            -and $_.canPreviewInline
    } | Select-Object -First 1

    $imageContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientImageDocumentPreviewId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $imageDownloadClient = New-AuthenticatedHttpClient
    try {
        $imageDownload = $imageDownloadClient.GetAsync("$ApiBaseUrl/api/documents/$patientImageDocumentPreviewId/download").GetAwaiter().GetResult()
        $imageDownloadBytes = $imageDownload.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $imageDownloadContentType = $imageDownload.Content.Headers.ContentType.ToString()
    }
    finally {
        $imageDownloadClient.Dispose()
    }

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientImageDocumentPreviewId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $patientImageDocumentPreviewId = $null

    $patientImageDocumentPreviewPassed = $null -ne $createdImageVisible `
        -and $imageContent.name -eq $imageDocumentName `
        -and $imageContent.previewKind -eq "image" `
        -and $imageContent.previewStatus -eq "Inline image preview" `
        -and $imageContent.canPreviewInline `
        -and $imageContent.contentBase64 -eq $imageDocumentBase64 `
        -and $imageDownload.IsSuccessStatusCode `
        -and $imageDownloadContentType -like "image/svg+xml*" `
        -and [Convert]::ToBase64String($imageDownloadBytes) -eq $imageDocumentBase64
    $patientImageDocumentThumbnailPassed = $null -ne $createdImageVisible `
        -and $createdImageVisible.thumbnailDataUri -eq $imageDocumentThumbnailDataUri

    Add-Check -Name "patient image document preview lifecycle" -Result $(if ($patientImageDocumentPreviewPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdImageDocument.id
        createdVisible = $createdImageVisible
        contentPreviewKind = $imageContent.previewKind
        contentCanPreviewInline = $imageContent.canPreviewInline
        downloadStatus = [int]$imageDownload.StatusCode
        downloadContentType = $imageDownloadContentType
    }
    Add-Check -Name "patient image document thumbnail readiness" -Result $(if ($patientImageDocumentThumbnailPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdImageDocument.id
        expectedThumbnail = $imageDocumentThumbnailDataUri
        actualThumbnail = $createdImageVisible.thumbnailDataUri
    }
}
catch {
    Add-Check -Name "patient image document preview lifecycle" -Result "failed" -Details $_.Exception.Message
    Add-Check -Name "patient image document thumbnail readiness" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientImageDocumentPreviewId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientImageDocumentPreviewId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdExternalLinkDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/external-link" -Method Post -ContentType "application/json" -Body $createExternalLinkDocumentBody -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $patientExternalLinkDocumentMutationId = $createdExternalLinkDocument.id
    $createdExternalLinkVisible = $createdExternalLinkDocument.detail.documents | Where-Object { $_.name -eq $externalLinkDocumentName -and $_.mimetype -eq "text/uri-list" -and $_.storageMethod -eq "web_url" -and $_.url -eq $externalLinkUrl } | Select-Object -First 1

    $externalLinkContent = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientExternalLinkDocumentMutationId/content" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedExternalLinkDocument = Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientExternalLinkDocumentMutationId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedExternalLinkVisible = $archivedExternalLinkDocument.detail.documents | Where-Object { $_.name -eq $externalLinkDocumentName } | Select-Object -First 1
    $patientExternalLinkDocumentMutationPassed = $null -ne $createdExternalLinkVisible `
        -and $externalLinkContent.name -eq $externalLinkDocumentName `
        -and $externalLinkContent.storageMethod -eq "web_url" `
        -and $externalLinkContent.url -eq $externalLinkUrl `
        -and $externalLinkContent.content.Contains($externalLinkUrl) `
        -and $null -eq $archivedExternalLinkVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientExternalLinkDocumentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/documents/$patientExternalLinkDocumentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createMessageBody -TimeoutSec 20
    $patientMessageMutationId = $createdMessage.id
    $createdVisible = $createdMessage.detail.messages | Where-Object { $_.title -eq $messageTitle -and $_.status -eq "New" -and $_.assignedTo -eq "admin" } | Select-Object -First 1

    $editedMessageTitle = "Smoke Patient Message Edited"
    $editedMessageBody = "Edited by the smoke patient-message content check."
    $contentBody = @{
        title = $editedMessageTitle
        body = $editedMessageBody
    } | ConvertTo-Json
    $editedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/content" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $contentBody -TimeoutSec 20
    $editedVisible = $editedMessage.detail.messages | Where-Object { $_.title -eq $editedMessageTitle -and $_.body -eq $editedMessageBody -and $_.status -eq "New" -and $_.assignedTo -eq "admin" } | Select-Object -First 1
    $createdUpdateMetadataBlank = $null -ne $createdVisible `
        -and $null -eq $createdVisible.updatedBy `
        -and [string]::IsNullOrWhiteSpace([string]$createdVisible.updatedAt)
    $editedUpdateMetadataStamped = $null -ne $editedVisible `
        -and $editedVisible.updatedBy -eq 1 `
        -and -not [string]::IsNullOrWhiteSpace([string]$editedVisible.updatedAt)
    Add-Check -Name "patient message content update" -Result $(if ($null -ne $editedVisible) { "passed" } else { "failed" }) -Details @{
        messageId = $patientMessageMutationId
        editedVisible = $editedVisible
    }
    Add-Check -Name "patient message update metadata" -Result $(if ($createdUpdateMetadataBlank -and $editedUpdateMetadataStamped) { "passed" } else { "failed" }) -Details @{
        messageId = $patientMessageMutationId
        createdUpdatedBy = $createdVisible.updatedBy
        createdUpdatedAt = $createdVisible.updatedAt
        editedUpdatedBy = $editedVisible.updatedBy
        editedUpdatedAt = $editedVisible.updatedAt
    }

    $replyMessageBody = "Replied by the smoke patient-message reply check."
    $replyBody = @{
        body = $replyMessageBody
        assignedTo = "admin"
    } | ConvertTo-Json
    $repliedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/reply" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $replyBody -TimeoutSec 20
    $replyVisible = $repliedMessage.detail.messages | Where-Object { $_.id -eq $patientMessageMutationId -and $_.body -like "*$replyMessageBody*" -and $_.body -like "*admin to admin*" -and $_.status -eq "New" } | Select-Object -First 1
    Add-Check -Name "patient message reply update" -Result $(if ($null -ne $replyVisible) { "passed" } else { "failed" }) -Details @{
        messageId = $patientMessageMutationId
        replyFound = $null -ne $replyVisible
    }

    $assignmentBody = @{
        assignedTo = "billing"
    } | ConvertTo-Json
    $assignedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/assignment" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $assignmentBody -TimeoutSec 20
    $assignedVisible = $assignedMessage.detail.messages | Where-Object { $_.title -eq $editedMessageTitle -and $_.status -eq "New" -and $_.assignedTo -eq "billing" } | Select-Object -First 1
    Add-Check -Name "patient message assignment update" -Result $(if ($null -ne $assignedVisible) { "passed" } else { "failed" }) -Details @{
        messageId = $patientMessageMutationId
        assignedVisible = $assignedVisible
    }

    $closeBody = @{
        status = "Done"
        body = "Closed by the smoke patient-message mutation check."
    } | ConvertTo-Json
    $closedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $closeBody -TimeoutSec 20
    $closedVisible = $closedMessage.detail.messages | Where-Object { $_.title -eq $editedMessageTitle -and $_.status -eq "Done" -and $_.body -eq "Closed by the smoke patient-message mutation check." } | Select-Object -First 1

    $archivedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId/soft-delete" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $archivedVisible = $archivedMessage.detail.messages | Where-Object { $_.title -eq $editedMessageTitle } | Select-Object -First 1
    $patientMessageMutationPassed = $null -ne $createdVisible -and $null -ne $editedVisible -and $null -ne $replyVisible -and $null -ne $assignedVisible -and $null -ne $closedVisible -and $null -eq $archivedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $patientMessageMutationId = $null

    Add-Check -Name "patient message mutation lifecycle" -Result $(if ($patientMessageMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdMessage.id
        createdVisible = $createdVisible
        editedVisible = $editedVisible
        replyVisible = $replyVisible
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientMessageMutationAuthorizationId = $null
try {
    $clinicianMessages = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/MOD-PAT-0004" -Method Get -Headers (Get-ClinicianHeaders) -TimeoutSec 20
    $authorizationMessageTitle = "Smoke Patient Message Authorization $([Guid]::NewGuid().ToString('N').Substring(0, 8))"
    $createAuthorizationMessageBody = @{
        patientId = "MOD-PAT-0004"
        title = $authorizationMessageTitle
        body = "Created by the smoke patient-message mutation authorization check."
        assignedTo = "admin"
    } | ConvertTo-Json
    $clinicianCreatedMessage = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages" -Method Post -Headers (Get-ClinicianHeaders) -ContentType "application/json" -Body $createAuthorizationMessageBody -TimeoutSec 20
    $patientMessageMutationAuthorizationId = $clinicianCreatedMessage.id
    $clinicianCreatedVisible = $clinicianCreatedMessage.detail.messages | Where-Object { $_.id -eq $patientMessageMutationAuthorizationId -and $_.title -eq $authorizationMessageTitle -and $_.status -eq "New" -and $_.assignedTo -eq "admin" } | Select-Object -First 1

    $clinicianStatusUpdateStatus = 0
    try {
        $blockedStatusBody = @{
            status = "Done"
            body = "This clinician status update should be blocked."
        } | ConvertTo-Json
        $clinicianStatusUpdate = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationAuthorizationId/status" `
            -Method Put `
            -Headers (Get-ClinicianHeaders) `
            -ContentType "application/json" `
            -Body $blockedStatusBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $clinicianStatusUpdateStatus = [int]$clinicianStatusUpdate.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $clinicianStatusUpdateStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $patientMessageMutationAuthorizationPassed = $clinicianMessages.patientId -eq "MOD-PAT-0004" `
        -and $null -ne $clinicianCreatedVisible `
        -and $clinicianStatusUpdateStatus -eq 403

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationAuthorizationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $patientMessageMutationAuthorizationId = $null

    Add-Check -Name "patient message mutation authorization" -Result $(if ($patientMessageMutationAuthorizationPassed) { "passed" } else { "failed" }) -Details @{
        clinicianPatientId = $clinicianMessages.patientId
        createdId = $clinicianCreatedMessage.id
        createdVisible = $clinicianCreatedVisible
        clinicianStatusUpdateStatus = $clinicianStatusUpdateStatus
    }
}
catch {
    Add-Check -Name "patient message mutation authorization" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $patientMessageMutationAuthorizationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$patientMessageMutationAuthorizationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $unauthenticatedProceduresStatus = 0
    try {
        $unauthenticatedProcedures = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0009" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedProceduresStatus = [int]$unauthenticatedProcedures.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedProceduresStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $procedures = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0009" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $completedOrder = $procedures.orders | Where-Object { $_.name -eq "Complete blood count" -and $_.orderStatus -eq "complete" } | Select-Object -First 1
    $completedReport = $completedOrder.reports | Where-Object { $_.status -eq "complete" } | Select-Object -First 1
    $hemoglobin = $completedReport.results | Where-Object { $_.text -eq "Hemoglobin" -and $_.resultStatus -eq "final" } | Select-Object -First 1

    $frontDeskProceduresStatus = 0
    try {
        $frontDeskProcedures = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0009" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskProceduresStatus = [int]$frontDeskProcedures.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskProceduresStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $frontDeskCatalogStatus = 0
    try {
        $frontDeskCatalog = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/procedures/order-catalog" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskCatalogStatus = [int]$frontDeskCatalog.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskCatalogStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $frontDeskCreateStatus = 0
    if ($null -eq $completedOrder -or $null -eq $completedOrder.encounter) {
        throw "Anchor procedure order did not provide an encounter for the procedure authorization check."
    }
    $frontDeskCreateBody = @{
        patientId = "MOD-PAT-0009"
        providerId = 101
        labId = 501
        encounterId = [int]$completedOrder.encounter
        dateOrdered = "2026-06-18"
        priority = "routine"
        status = "pending"
        procedureCode = "85025"
        procedureName = "Blocked Procedure Authorization Order"
        procedureType = "laboratory"
        diagnosis = "Z00.00"
        instructions = "This request should be rejected before mutation."
    } | ConvertTo-Json -Depth 8
    try {
        $frontDeskCreate = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/procedures/orders" `
            -Method Post `
            -Headers (Get-FrontDeskHeaders) `
            -ContentType "application/json" `
            -Body $frontDeskCreateBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskCreateStatus = [int]$frontDeskCreate.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskCreateStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $clinicianProceduresStatus = 0
    try {
        $clinicianProcedures = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0009" `
            -Method Get `
            -Headers (Get-ClinicianHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $clinicianProceduresStatus = 200
    }
    catch {
        if ($_.Exception.Response) {
            $clinicianProceduresStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $clinicianUpdateStatus = 0
    $clinicianUpdateBody = @{
        status = "complete"
    } | ConvertTo-Json -Depth 8
    try {
        $clinicianUpdate = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/procedures/orders/$($completedOrder.id)/status" `
            -Method Put `
            -Headers (Get-ClinicianHeaders) `
            -ContentType "application/json" `
            -Body $clinicianUpdateBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $clinicianUpdateStatus = [int]$clinicianUpdate.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $clinicianUpdateStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $clinicianSignStatus = 0
    $clinicianSignBody = @{
        reviewedBy = "gold-provider-01"
        reviewedAt = "2026-06-19 14:15:00"
    } | ConvertTo-Json -Depth 8
    try {
        $clinicianSign = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/procedures/reports/$($completedReport.id)/sign" `
            -Method Put `
            -Headers (Get-ClinicianHeaders) `
            -ContentType "application/json" `
            -Body $clinicianSignBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $clinicianSignStatus = [int]$clinicianSign.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $clinicianSignStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $proceduresPassed = $unauthenticatedProceduresStatus -eq 401 `
        -and $frontDeskProceduresStatus -eq 403 `
        -and $frontDeskCatalogStatus -eq 403 `
        -and $frontDeskCreateStatus -eq 403 `
        -and $clinicianProceduresStatus -eq 200 `
        -and $clinicianUpdateStatus -eq 403 `
        -and $clinicianSignStatus -eq 403 `
        -and $procedures.patientId -eq "MOD-PAT-0009" `
        -and $null -ne $completedOrder `
        -and $null -ne $completedReport `
        -and $null -ne $hemoglobin
    Add-Check -Name "anchor procedure results" -Result $(if ($proceduresPassed) { "passed" } else { "failed" }) -Details @{
        unauthenticatedStatus = $unauthenticatedProceduresStatus
        frontDeskProcedureStatus = $frontDeskProceduresStatus
        frontDeskCatalogStatus = $frontDeskCatalogStatus
        frontDeskCreateStatus = $frontDeskCreateStatus
        clinicianProcedureStatus = $clinicianProceduresStatus
        clinicianUpdateStatus = $clinicianUpdateStatus
        clinicianSignStatus = $clinicianSignStatus
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
    $scheduledProcedures = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/MOD-PAT-0701" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
        providerId = 101
        labId = 501
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
    $createdProcedureOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createProcedureBody -TimeoutSec 20
    $procedureOrderMutationId = $createdProcedureOrder.id
    $createdProcedureVisible = $createdProcedureOrder.detail.orders | Where-Object { $_.id -eq $procedureOrderMutationId -and $_.name -eq $procedureName -and $_.orderStatus -eq "pending" } | Select-Object -First 1

    $correctedProcedureName = "Smoke Corrected Procedure Order"
    $correctProcedureOrderBody = @{
        dateOrdered = "2026-06-19"
        priority = "urgent"
        status = "pending"
        procedureCode = "85025"
        procedureName = $correctedProcedureName
        procedureType = "hematology"
        diagnosis = "R53.83"
        instructions = "Corrected by the smoke procedure mutation check."
    } | ConvertTo-Json
    $correctedProcedureOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$procedureOrderMutationId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $correctProcedureOrderBody -TimeoutSec 20
    $correctedProcedureVisible = $correctedProcedureOrder.detail.orders | Where-Object {
        $_.id -eq $procedureOrderMutationId `
            -and $_.name -eq $correctedProcedureName `
            -and $_.code -eq "85025" `
            -and $_.orderDate -eq "2026-06-19" `
            -and $_.orderPriority -eq "urgent" `
            -and $_.procedureType -eq "hematology" `
            -and $_.diagnosis -eq "R53.83" `
            -and $_.instructions -eq "Corrected by the smoke procedure mutation check."
    } | Select-Object -First 1

    $completeProcedureBody = @{
        status = "complete"
    } | ConvertTo-Json
    $completedProcedureOrder = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$procedureOrderMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $completeProcedureBody -TimeoutSec 20
    $completedProcedureVisible = $completedProcedureOrder.detail.orders | Where-Object { $_.id -eq $procedureOrderMutationId -and $_.orderStatus -eq "complete" } | Select-Object -First 1

    $createProcedureReportBody = @{
        orderId = $procedureOrderMutationId
        dateCollected = "2026-06-18 12:30:00"
        dateReport = "2026-06-18 13:00:00"
        specimenNumber = "SMOKE-PROC"
        reportStatus = "final"
        reviewStatus = "received"
        notes = "Smoke procedure report."
    } | ConvertTo-Json
    $createdProcedureReport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/reports" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createProcedureReportBody -TimeoutSec 20
    $procedureReportId = $createdProcedureReport.id

    $correctProcedureReportBody = @{
        dateCollected = "2026-06-19 10:20:00"
        dateReport = "2026-06-19 11:00:00"
        specimenNumber = "SMOKE-PROC-CORR"
        reportStatus = "corrected"
        reviewStatus = "received"
        notes = "Corrected smoke procedure report."
    } | ConvertTo-Json
    $correctedProcedureReport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/reports/$procedureReportId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $correctProcedureReportBody -TimeoutSec 20
    $correctedProcedureReportVisible = $correctedProcedureReport.detail.orders | Where-Object { $_.id -eq $procedureOrderMutationId } | Select-Object -First 1
    $correctedProcedureReportRow = $correctedProcedureReportVisible.reports | Where-Object {
        $_.id -eq $procedureReportId `
            -and $_.dateCollected -eq "2026-06-19 10:20" `
            -and $_.reportDate -eq "2026-06-19 11:00" `
            -and $_.specimenNumber -eq "SMOKE-PROC-CORR" `
            -and $_.status -eq "corrected" `
            -and $_.reviewStatus -eq "received" `
            -and $_.notes -eq "Corrected smoke procedure report."
    } | Select-Object -First 1

    $createProcedureSpecimenBody = @{
        orderId = $procedureOrderMutationId
        specimenIdentifier = "SMOKE-SID"
        accessionIdentifier = "SMOKE-ACC"
        specimenTypeCode = "BLD"
        specimenType = "Blood"
        collectionMethodCode = "VP"
        collectionMethod = "Venipuncture"
        specimenLocationCode = "LAC"
        specimenLocation = "Left antecubital"
        collectedDate = "2026-06-18 12:20:00"
        volumeValue = 4.5
        volumeUnit = "mL"
        conditionCode = "OK"
        specimenCondition = "Acceptable"
        comments = "Created by the smoke procedure mutation check."
    } | ConvertTo-Json
    $createdProcedureSpecimen = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/specimens" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createProcedureSpecimenBody -TimeoutSec 20
    $procedureSpecimenId = $createdProcedureSpecimen.id

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
    $createdProcedureResult = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/results" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createProcedureResultBody -TimeoutSec 20
    $procedureResultId = $createdProcedureResult.id
    $resultOrder = $createdProcedureResult.detail.orders | Where-Object { $_.id -eq $procedureOrderMutationId } | Select-Object -First 1
    $resultSpecimen = $resultOrder.specimens | Where-Object {
        $_.id -eq $procedureSpecimenId `
            -and $_.specimenIdentifier -eq "SMOKE-SID" `
            -and $_.accessionIdentifier -eq "SMOKE-ACC" `
            -and $_.specimenType -eq "Blood" `
            -and $_.collectionMethod -eq "Venipuncture" `
            -and $_.specimenLocation -eq "Left antecubital" `
            -and $_.collectedDate -eq "2026-06-18 12:20" `
            -and $_.volumeValue -eq 4.5 `
            -and $_.volumeUnit -eq "mL" `
            -and $_.specimenCondition -eq "Acceptable"
    } | Select-Object -First 1
    $resultReport = $resultOrder.reports | Where-Object {
        $_.id -eq $procedureReportId `
            -and $_.dateCollected -eq "2026-06-19 10:20" `
            -and $_.reportDate -eq "2026-06-19 11:00" `
            -and $_.specimenNumber -eq "SMOKE-PROC-CORR" `
            -and $_.status -eq "corrected" `
            -and $_.reviewStatus -eq "received" `
            -and $_.notes -eq "Corrected smoke procedure report."
    } | Select-Object -First 1

    $unreviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=unreviewed&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $queuedProcedureReportBeforeSign = $unreviewedProcedureReportQueue.reports | Where-Object {
        $_.reportId -eq $procedureReportId `
            -and $_.orderId -eq $procedureOrderMutationId `
            -and $_.patientId -eq "MOD-PAT-0009" `
            -and $_.procedureName -eq $correctedProcedureName `
            -and $_.reviewStatus -eq "received"
    } | Select-Object -First 1
    $filteredUnreviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=unreviewed&patientId=MOD-PAT-0009&fromDate=2026-06-19&toDate=2026-06-19&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $filteredQueuedProcedureReportBeforeSign = $filteredUnreviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId } | Select-Object -First 1
    $providerFilteredUnreviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=unreviewed&patientId=MOD-PAT-0009&providerId=101&fromDate=2026-06-19&toDate=2026-06-19&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $providerFilteredQueuedProcedureReportBeforeSign = $providerFilteredUnreviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId -and $_.providerId -eq 101 } | Select-Object -First 1
    $outsideProviderUnreviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=unreviewed&patientId=MOD-PAT-0009&providerId=102&fromDate=2026-06-19&toDate=2026-06-19&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $outsideProviderQueuedProcedureReport = $outsideProviderUnreviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId } | Select-Object -First 1
    $labFilteredUnreviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=unreviewed&patientId=MOD-PAT-0009&labId=501&fromDate=2026-06-19&toDate=2026-06-19&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $labFilteredQueuedProcedureReportBeforeSign = $labFilteredUnreviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId -and $_.labId -eq 501 } | Select-Object -First 1
    $outsideLabUnreviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=unreviewed&patientId=MOD-PAT-0009&labId=502&fromDate=2026-06-19&toDate=2026-06-19&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $outsideLabQueuedProcedureReport = $outsideLabUnreviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId } | Select-Object -First 1
    $outsideDateUnreviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=unreviewed&patientId=MOD-PAT-0009&fromDate=2026-06-18&toDate=2026-06-18&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $outsideDateQueuedProcedureReport = $outsideDateUnreviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId } | Select-Object -First 1

    $signProcedureReportBody = @{
        reviewedBy = "admin"
        reviewedAt = "2026-06-19 14:15:00"
    } | ConvertTo-Json
    $signedProcedureReport = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/reports/$procedureReportId/sign" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $signProcedureReportBody -TimeoutSec 20
    $signedOrder = $signedProcedureReport.detail.orders | Where-Object { $_.id -eq $procedureOrderMutationId } | Select-Object -First 1
    $signedReport = $signedOrder.reports | Where-Object {
        $_.id -eq $procedureReportId `
            -and $_.reviewStatus -eq "reviewed" `
            -and $_.reviewedBy -eq "admin" `
            -and $_.reviewedAt -eq "2026-06-19 14:15"
    } | Select-Object -First 1
    $createdResultVisible = $signedReport.results | Where-Object { $_.id -eq $procedureResultId -and $_.text -eq $procedureResultText -and $_.result -eq "104" -and $_.resultStatus -eq "final" } | Select-Object -First 1

    $reviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=reviewed&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $queuedProcedureReportAfterSign = $reviewedProcedureReportQueue.reports | Where-Object {
        $_.reportId -eq $procedureReportId `
            -and $_.reviewStatus -eq "reviewed" `
            -and $_.reviewedBy -eq "admin" `
            -and $_.reviewedAt -eq "2026-06-19 14:15"
    } | Select-Object -First 1
    $filteredReviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=reviewed&patientId=MOD-PAT-0009&fromDate=2026-06-19&toDate=2026-06-19&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $filteredQueuedProcedureReportAfterSign = $filteredReviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId } | Select-Object -First 1
    $providerFilteredReviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=reviewed&patientId=MOD-PAT-0009&providerId=101&fromDate=2026-06-19&toDate=2026-06-19&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $providerFilteredQueuedProcedureReportAfterSign = $providerFilteredReviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId -and $_.providerId -eq 101 } | Select-Object -First 1
    $labFilteredReviewedProcedureReportQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=reviewed&patientId=MOD-PAT-0009&labId=501&fromDate=2026-06-19&toDate=2026-06-19&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $labFilteredQueuedProcedureReportAfterSign = $labFilteredReviewedProcedureReportQueue.reports | Where-Object { $_.reportId -eq $procedureReportId -and $_.labId -eq 501 } | Select-Object -First 1

    $unreviewedProcedureReportQueueAfterSign = Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/report-review-queue?status=unreviewed&limit=100" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $queuedProcedureReportStillUnreviewed = $unreviewedProcedureReportQueueAfterSign.reports | Where-Object { $_.reportId -eq $procedureReportId } | Select-Object -First 1

    $procedureMutationPassed = $null -ne $createdProcedureVisible `
        -and $null -ne $correctedProcedureVisible `
        -and $null -ne $completedProcedureVisible `
        -and $null -ne $correctedProcedureReportRow `
        -and $null -ne $resultSpecimen `
        -and $null -ne $resultReport `
        -and $null -ne $queuedProcedureReportBeforeSign `
        -and $null -ne $filteredQueuedProcedureReportBeforeSign `
        -and $null -ne $providerFilteredQueuedProcedureReportBeforeSign `
        -and $null -eq $outsideProviderQueuedProcedureReport `
        -and $null -ne $labFilteredQueuedProcedureReportBeforeSign `
        -and $null -eq $outsideLabQueuedProcedureReport `
        -and $null -eq $outsideDateQueuedProcedureReport `
        -and $null -ne $signedReport `
        -and $null -ne $createdResultVisible `
        -and $null -ne $queuedProcedureReportAfterSign `
        -and $null -ne $filteredQueuedProcedureReportAfterSign `
        -and $null -ne $providerFilteredQueuedProcedureReportAfterSign `
        -and $null -ne $labFilteredQueuedProcedureReportAfterSign `
        -and $null -eq $queuedProcedureReportStillUnreviewed

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$procedureOrderMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $procedureOrderMutationId = $null

    Add-Check -Name "procedure mutation lifecycle" -Result $(if ($procedureMutationPassed) { "passed" } else { "failed" }) -Details @{
        createdId = $createdProcedureOrder.id
        createdVisible = $createdProcedureVisible
        correctedVisible = $correctedProcedureVisible
        completedVisible = $completedProcedureVisible
        specimenId = $procedureSpecimenId
        specimenVisible = $resultSpecimen
        reportId = $procedureReportId
        correctedReportVisible = $correctedProcedureReportRow
        queuedReportBeforeSign = $queuedProcedureReportBeforeSign
        filteredQueuedReportBeforeSign = $filteredQueuedProcedureReportBeforeSign
        providerFilteredQueuedReportBeforeSign = $providerFilteredQueuedProcedureReportBeforeSign
        outsideProviderQueuedReport = $outsideProviderQueuedProcedureReport
        labFilteredQueuedReportBeforeSign = $labFilteredQueuedProcedureReportBeforeSign
        outsideLabQueuedReport = $outsideLabQueuedProcedureReport
        outsideDateQueuedReport = $outsideDateQueuedProcedureReport
        signedReportVisible = $signedReport
        queuedReportAfterSign = $queuedProcedureReportAfterSign
        filteredQueuedReportAfterSign = $filteredQueuedProcedureReportAfterSign
        providerFilteredQueuedReportAfterSign = $providerFilteredQueuedProcedureReportAfterSign
        labFilteredQueuedReportAfterSign = $labFilteredQueuedProcedureReportAfterSign
        resultVisible = $createdResultVisible
    }
}
catch {
    Add-Check -Name "procedure mutation lifecycle" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($null -ne $procedureOrderMutationId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/procedures/orders/$procedureOrderMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $unauthenticatedBillingStatus = 0
    try {
        $unauthenticatedBilling = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0001" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedBillingStatus = [int]$unauthenticatedBilling.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedBillingStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $frontDeskBillingStatus = 0
    try {
        $frontDeskBilling = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0001" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskBillingStatus = [int]$frontDeskBilling.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskBillingStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $frontDeskBillingMutationStatus = 0
    $frontDeskBillingMutationBody = @{
        patientId = "MOD-PAT-0001"
        encounter = 1000013
        billingDate = "2026-06-18"
        codeType = "CPT4"
        code = "99213"
        codeText = "Blocked Billing Authorization Line"
        fee = 125
        units = 1
        justify = "Z00.00"
    } | ConvertTo-Json
    try {
        $frontDeskBillingMutation = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/billing/lines" `
            -Method Post `
            -Headers (Get-FrontDeskHeaders) `
            -ContentType "application/json" `
            -Body $frontDeskBillingMutationBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskBillingMutationStatus = [int]$frontDeskBillingMutation.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskBillingMutationStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $billing = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0001" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $latestEncounter = $billing.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $officeVisit = $latestEncounter.lines | Where-Object { $_.code -eq "99214" -and $_.codeText -eq "Established patient office visit" } | Select-Object -First 1
    $venipuncture = $latestEncounter.lines | Where-Object { $_.code -eq "36415" -and $_.codeText -eq "Routine venipuncture" } | Select-Object -First 1
    $billingPassed = $unauthenticatedBillingStatus -eq 401 -and $frontDeskBillingStatus -eq 403 -and $frontDeskBillingMutationStatus -eq 403 -and $billing.patientId -eq "MOD-PAT-0001" -and $null -ne $latestEncounter -and $null -ne $officeVisit -and $null -ne $venipuncture
    Add-Check -Name "anchor fee sheet billing" -Result $(if ($billingPassed) { "passed" } else { "failed" }) -Details @{
        unauthenticatedStatus = $unauthenticatedBillingStatus
        frontDeskStatus = $frontDeskBillingStatus
        frontDeskMutationStatus = $frontDeskBillingMutationStatus
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

$billingViewGrantActive = $false
try {
    $administrationHeaders = Get-AdministrationHeaders
    $billingViewGrantBody = @{
        groupValue = "clin"
        sectionValue = "acct"
        permissionValue = "bill"
        returnValue = "view"
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" -Method Put -Headers $administrationHeaders -ContentType "application/json" -Body $billingViewGrantBody -TimeoutSec 20 | Out-Null
    $billingViewGrantActive = $true
    $script:ClinicianHeaders = $null

    $clinicianBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-ClinicianHeaders) -TimeoutSec 20
    $clinicianBillingMutationStatus = 0
    $clinicianBillingMutationBody = @{
        patientId = "MOD-PAT-0005"
        encounter = 1000052
        billingDate = "2026-06-18"
        codeType = "CPT4"
        code = "99213"
        codeText = "Blocked Billing Mutation Authorization Line"
        fee = 125
        units = 1
        justify = "Z00.00"
    } | ConvertTo-Json
    try {
        $clinicianBillingMutation = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/billing/lines" `
            -Method Post `
            -Headers (Get-ClinicianHeaders) `
            -ContentType "application/json" `
            -Body $clinicianBillingMutationBody `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $clinicianBillingMutationStatus = [int]$clinicianBillingMutation.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $clinicianBillingMutationStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $billingMutationAuthorizationPassed = $clinicianBilling.patientId -eq "MOD-PAT-0005" -and $clinicianBillingMutationStatus -eq 403
    Add-Check -Name "billing mutation authorization" -Result $(if ($billingMutationAuthorizationPassed) { "passed" } else { "failed" }) -Details @{
        patientId = $clinicianBilling.patientId
        clinicianMutationStatus = $clinicianBillingMutationStatus
    }
}
catch {
    Add-Check -Name "billing mutation authorization" -Result "failed" -Details $_.Exception.Message
}
finally {
    if ($billingViewGrantActive) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions/clin/acct/bill" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
    $script:ClinicianHeaders = $null
}

try {
    $claimBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $beforeClaimMutationBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $createdClaimStatus = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createClaimStatusBody -TimeoutSec 20
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
    $generatedClaimStatus = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims/$claimStatusMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $generateClaimStatusBody -TimeoutSec 20
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
    $clearedClaimStatus = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims/$claimStatusMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $clearClaimStatusBody -TimeoutSec 20
    $clearedClaimRows = @($clearedClaimStatus.detail.encounters | ForEach-Object { $_.claims } | Where-Object { $null -ne $_ })
    $clearedClaimVisible = $clearedClaimRows | Where-Object { $_.id -eq $claimStatusMutationId -and $_.statusLabel -eq "Marked as cleared" -and [string]::IsNullOrWhiteSpace($_.processFile) -and $_.target -eq "HCFA" } | Select-Object -First 1

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims/$claimStatusMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    $claimStatusMutationId = $null
    $afterClaimMutationBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/claims/$claimStatusMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $paymentBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $beforePaymentMutationBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $createdPaymentPosting = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createPaymentPostingBody -TimeoutSec 20
    $paymentPostingMutationId = $createdPaymentPosting.id
    $createdPaymentRows = @($createdPaymentPosting.detail.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $createdPaymentVisible = $createdPaymentRows | Where-Object { $_.activityId -eq $paymentPostingMutationId -and $_.reference -eq $paymentPostingReference -and $_.payAmount -eq 21 -and $_.adjustmentAmount -eq 3.5 -and $_.reasonCode -eq "CO-45" } | Select-Object -First 1
    $createdPaymentSummary = $createdPaymentPosting.detail.accountSummary

    $voidedPaymentPosting = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$paymentPostingMutationId/void" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $voidedPaymentRows = @($voidedPaymentPosting.detail.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $voidedPaymentVisible = $voidedPaymentRows | Where-Object { $_.activityId -eq $paymentPostingMutationId } | Select-Object -First 1
    $voidedPaymentSummary = $voidedPaymentPosting.detail.accountSummary

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$paymentPostingMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$paymentPostingMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$patientPaymentMutationId = $null
try {
    $beforePatientPaymentBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $createdPatientPayment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createPatientPaymentBody -TimeoutSec 20
    $patientPaymentMutationId = $createdPatientPayment.id
    $createdPatientPaymentRows = @($createdPatientPayment.detail.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $createdPatientPaymentVisible = $createdPatientPaymentRows | Where-Object { $_.activityId -eq $patientPaymentMutationId -and $_.payerType -eq 0 -and $_.reference -eq $patientPaymentReference -and $_.payAmount -eq 35 -and $_.adjustmentAmount -eq 0 } | Select-Object -First 1
    $createdPatientPaymentSummary = $createdPatientPayment.detail.accountSummary

    $voidedPatientPayment = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$patientPaymentMutationId/void" -Method Put -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $voidedPatientPaymentRows = @($voidedPatientPayment.detail.encounters | ForEach-Object { $_.payments } | Where-Object { $null -ne $_ })
    $voidedPatientPaymentVisible = $voidedPatientPaymentRows | Where-Object { $_.activityId -eq $patientPaymentMutationId } | Select-Object -First 1
    $voidedPatientPaymentSummary = $voidedPatientPayment.detail.accountSummary

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$patientPaymentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/payments/$patientPaymentMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $balanceBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $agingBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $ledgerBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $statementBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $statementDocumentBilling = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $statementPdfResponse = Invoke-WebRequest -UseBasicParsing -Uri "$ApiBaseUrl/api/billing/MOD-PAT-0005/statement.pdf" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $statementBatch = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/statements/batch?limit=5" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $statementPackageClient = New-AuthenticatedHttpClient
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
    $collectionsWorkQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/collections/work-queue?limit=5" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $collectionsWorkQueue = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/collections/work-queue?limit=5" -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $firstCollectionItem = @($collectionsWorkQueue.items) | Select-Object -First 1
    $createFollowUpBody = @{
        patientId = $firstCollectionItem.pubpid
        assignedTo = "billing"
        action = $firstCollectionItem.recommendedAction
        note = "Created by the smoke collections follow-up check."
    } | ConvertTo-Json
    $createdFollowUp = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/collections/follow-ups" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createFollowUpBody -TimeoutSec 20
    $collectionsFollowUpId = $createdFollowUp.id

    $closeFollowUpBody = @{
        status = "Done"
        body = "Closed by the smoke collections follow-up check."
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$collectionsFollowUpId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $closeFollowUpBody -TimeoutSec 20 | Out-Null
    $patientMessages = Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$($firstCollectionItem.pubpid)" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $closedFollowUp = $patientMessages.messages | Where-Object { $_.id -eq $collectionsFollowUpId -and $_.status -eq "Done" } | Select-Object -First 1
    $collectionsFollowUpPassed = $createdFollowUp.task.title -eq "Collections follow-up: $($firstCollectionItem.statementNumber)" `
        -and $createdFollowUp.task.assignedTo -eq "billing" `
        -and $createdFollowUp.task.action -eq $firstCollectionItem.recommendedAction `
        -and $createdFollowUp.task.collectionTier -eq $firstCollectionItem.collectionTier `
        -and $createdFollowUp.task.body -like "*$($firstCollectionItem.statementNumber)*" `
        -and $createdFollowUp.task.body -like "*Created by the smoke collections follow-up check.*" `
        -and $null -ne $closedFollowUp `
        -and $closedFollowUp.body -eq "Closed by the smoke collections follow-up check."

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$collectionsFollowUpId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/messages/$collectionsFollowUpId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createBillingBody -TimeoutSec 20
    $billingLineMutationId = $createdBillingLine.id
    $createdBillingEncounter = $createdBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $createdBillingVisible = $createdBillingEncounter.lines | Where-Object { $_.id -eq $billingLineMutationId -and $_.code -eq "99213" -and $_.codeText -eq $billingCodeText -and $_.billed -eq 0 -and $_.activity -eq 1 } | Select-Object -First 1
    $createdEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $createdEncounterBillingVisible = $createdEncounterDetail.billingLines | Where-Object { $_.id -eq $billingLineMutationId -and $_.code -eq "99213" -and $_.codeText -eq $billingCodeText -and $_.activity -eq 1 } | Select-Object -First 1

    $statusBillingBody = @{
        billed = 1
        activity = 0
    } | ConvertTo-Json
    $inactiveBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingLineMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $statusBillingBody -TimeoutSec 20
    $inactiveBillingEncounter = $inactiveBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveBillingVisible = $inactiveBillingEncounter.lines | Where-Object { $_.id -eq $billingLineMutationId } | Select-Object -First 1
    $inactiveEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $inactiveEncounterBillingVisible = $inactiveEncounterDetail.billingLines | Where-Object { $_.id -eq $billingLineMutationId } | Select-Object -First 1
    $billingLineMutationPassed = $null -ne $createdBillingVisible -and $null -eq $inactiveBillingVisible
    $encounterBillingLinkMutationPassed = $null -ne $createdEncounterBillingVisible -and $null -eq $inactiveEncounterBillingVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingLineMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingLineMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdCorrectionLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createCorrectionBody -TimeoutSec 20
    $billingCorrectionMutationId = $createdCorrectionLine.id

    $correctionBody = @{
        codeText = $correctedBillingText
        fee = 142.25
        units = 3
        justify = "E78.5"
    } | ConvertTo-Json
    $correctedBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingCorrectionMutationId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $correctionBody -TimeoutSec 20
    $correctedBillingEncounter = $correctedBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $correctedBillingVisible = $correctedBillingEncounter.lines | Where-Object { $_.id -eq $billingCorrectionMutationId -and $_.code -eq "99213" -and $_.codeText -eq $correctedBillingText -and $_.fee -eq 142.25 -and $_.units -eq 3 -and $_.justify -eq "E78.5" -and $_.billed -eq 0 -and $_.activity -eq 1 } | Select-Object -First 1

    $statusCorrectionBody = @{
        billed = 1
        activity = 0
    } | ConvertTo-Json
    $inactiveCorrectionLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingCorrectionMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $statusCorrectionBody -TimeoutSec 20
    $inactiveCorrectionEncounter = $inactiveCorrectionLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveCorrectionVisible = $inactiveCorrectionEncounter.lines | Where-Object { $_.id -eq $billingCorrectionMutationId } | Select-Object -First 1
    $billingCorrectionPassed = $null -ne $correctedBillingVisible -and $null -eq $inactiveCorrectionVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingCorrectionMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingCorrectionMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdModifierLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createModifierBody -TimeoutSec 20
    $billingModifierMutationId = $createdModifierLine.id

    $modifierBody = @{
        codeText = $modifierBillingText
        modifier = "25"
        fee = 142.25
        units = 2
        justify = "E78.5"
    } | ConvertTo-Json
    $modifiedBillingLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingModifierMutationId" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $modifierBody -TimeoutSec 20
    $modifiedBillingEncounter = $modifiedBillingLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $modifiedBillingVisible = $modifiedBillingEncounter.lines | Where-Object { $_.id -eq $billingModifierMutationId -and $_.code -eq "99213" -and $_.modifier -eq "25" -and $_.codeText -eq $modifierBillingText -and $_.fee -eq 142.25 -and $_.units -eq 2 -and $_.justify -eq "E78.5" -and $_.billed -eq 0 -and $_.activity -eq 1 } | Select-Object -First 1

    $statusModifierBody = @{
        billed = 1
        activity = 0
    } | ConvertTo-Json
    $inactiveModifierLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingModifierMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $statusModifierBody -TimeoutSec 20
    $inactiveModifierEncounter = $inactiveModifierLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveModifierVisible = $inactiveModifierEncounter.lines | Where-Object { $_.id -eq $billingModifierMutationId } | Select-Object -First 1
    $billingModifierPassed = $null -ne $modifiedBillingVisible -and $null -eq $inactiveModifierVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingModifierMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$billingModifierMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
    $createdDiagnosisLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines" -Method Post -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $createDiagnosisBody -TimeoutSec 20
    $diagnosisLineMutationId = $createdDiagnosisLine.id
    $createdDiagnosisEncounter = $createdDiagnosisLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $createdDiagnosisVisible = $createdDiagnosisEncounter.lines | Where-Object { $_.id -eq $diagnosisLineMutationId -and $_.codeType -eq "ICD10" -and $_.code -eq "R73.03" -and $_.codeText -eq $diagnosisCodeText -and $_.fee -eq 0 -and $_.justify -eq "R73.03" -and $_.billed -eq 0 -and $_.activity -eq 1 } | Select-Object -First 1
    $createdDiagnosisEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
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
    $inactiveDiagnosisLine = Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$diagnosisLineMutationId/status" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $statusDiagnosisBody -TimeoutSec 20
    $inactiveDiagnosisEncounter = $inactiveDiagnosisLine.detail.encounters | Where-Object { $_.encounter -eq 1000013 } | Select-Object -First 1
    $inactiveDiagnosisVisible = $inactiveDiagnosisEncounter.lines | Where-Object { $_.id -eq $diagnosisLineMutationId } | Select-Object -First 1
    $inactiveDiagnosisEncounterDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/api/encounters/1000013" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $inactiveEncounterDiagnosisVisible = @($inactiveDiagnosisEncounterDetail.diagnosisCodes | Where-Object { $null -ne $_ }) | Where-Object { $_.code -eq "R73.03" } | Select-Object -First 1
    $diagnosisLineMutationPassed = $null -ne $createdDiagnosisVisible -and $null -eq $inactiveDiagnosisVisible
    $encounterDiagnosisMutationPassed = $null -ne $createdEncounterDiagnosisVisible -and $null -eq $inactiveEncounterDiagnosisVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$diagnosisLineMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/billing/lines/$diagnosisLineMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $administrationHeaders = Get-AdministrationHeaders
    $administration = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/directory" -Method Get -Headers $administrationHeaders -TimeoutSec 20
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
    $frontDeskAppointmentPermission = $administration.accessControl.groupPermissions | Where-Object { $_.groupValue -eq "front" -and $_.sectionValue -eq "patients" -and $_.permissionValue -eq "appt" -and $_.returnValue -eq "write" } | Select-Object -First 1
    $adminMembership = $administration.accessControl.userMemberships | Where-Object { $_.userValue -eq "admin" -and $_.groupValue -eq "admin" -and $_.groupName -eq "Administrators" } | Select-Object -First 1
    $systemMembership = $administration.accessControl.userMemberships | Where-Object { $_.userValue -eq "oe-system" -and $_.groupValue -eq "admin" -and $_.groupName -eq "Administrators" } | Select-Object -First 1
    $frontDeskMembership = $administration.accessControl.userMemberships | Where-Object { $_.userValue -eq "gold-frontdesk-01" -and $_.groupValue -eq "front" -and $_.groupName -eq "Front Office" } | Select-Object -First 1
    $clinicianMembership = $administration.accessControl.userMemberships | Where-Object { $_.userValue -eq "gold-provider-01" -and $_.groupValue -eq "clin" -and $_.groupName -eq "Clinicians" } | Select-Object -First 1
    $accessControlPassed = $administration.counts.accessGroups -eq 7 -and $administration.counts.accessPermissions -eq 65 -and $administration.counts.accessGroupPermissions -eq 203 -and $administration.counts.accessUserMemberships -eq 4 -and $null -ne $adminGroup -and $null -ne $physicianGroup -and $null -ne $clinicianGroup -and $null -ne $adminAclPermission -and $null -ne $frontDeskDemoPermission -and $null -ne $frontDeskAppointmentPermission -and $null -ne $adminMembership -and $null -ne $systemMembership -and $null -ne $frontDeskMembership -and $null -ne $clinicianMembership
    Add-Check -Name "anchor administration access control" -Result $(if ($accessControlPassed) { "passed" } else { "failed" }) -Details @{
        counts = $administration.counts
        adminGroup = $adminGroup
        physicianGroup = $physicianGroup
        clinicianGroup = $clinicianGroup
        adminAclPermission = $adminAclPermission
        frontDeskDemoPermission = $frontDeskDemoPermission
        frontDeskAppointmentPermission = $frontDeskAppointmentPermission
        adminMembership = $adminMembership
        systemMembership = $systemMembership
        frontDeskMembership = $frontDeskMembership
        clinicianMembership = $clinicianMembership
    }
}
catch {
    Add-Check -Name "anchor administration directory" -Result "failed" -Details $_.Exception.Message
    Add-Check -Name "anchor administration access control" -Result "failed" -Details $_.Exception.Message
}

try {
    $administrationHeaders = Get-AdministrationHeaders
    $accessGrantBody = @{
        groupValue = "front"
        sectionValue = "patients"
        permissionValue = "demo"
        returnValue = "write"
    } | ConvertTo-Json

    $revokedAccess = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions/front/patients/demo" -Method Delete -Headers $administrationHeaders -TimeoutSec 20
    $revokedFrontGroup = $revokedAccess.detail.accessControl.groups | Where-Object { $_.value -eq "front" -and $_.permissionCount -eq 5 } | Select-Object -First 1
    $revokedFrontDemo = $revokedAccess.detail.accessControl.groupPermissions | Where-Object { $_.groupValue -eq "front" -and $_.sectionValue -eq "patients" -and $_.permissionValue -eq "demo" } | Select-Object -First 1

    $restoredAccess = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" -Method Put -Headers $administrationHeaders -ContentType "application/json" -Body $accessGrantBody -TimeoutSec 20
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
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/group-permissions" -Method Put -Headers (Get-AdministrationHeaders) -ContentType "application/json" -Body $accessGrantBody -TimeoutSec 20 | Out-Null
    }
    catch {
    }
}

$administrationMembershipUserId = $null
try {
    $administrationHeaders = Get-AdministrationHeaders
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

    $createdMembershipUser = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users" -Method Post -Headers $administrationHeaders -ContentType "application/json" -Body $createMembershipUserBody -TimeoutSec 20
    $administrationMembershipUserId = $createdMembershipUser.id

    $membershipGrantBody = @{
        userValue = $membershipUserName
        groupValue = "front"
    } | ConvertTo-Json

    $grantedMembership = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/user-memberships" -Method Put -Headers $administrationHeaders -ContentType "application/json" -Body $membershipGrantBody -TimeoutSec 20
    $frontMembership = $grantedMembership.detail.accessControl.userMemberships | Where-Object { $_.userValue -eq $membershipUserName -and $_.groupValue -eq "front" -and $_.groupName -eq "Front Office" } | Select-Object -First 1

    $revokedMembership = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/user-memberships/$membershipUserName/front" -Method Delete -Headers $administrationHeaders -TimeoutSec 20
    $revokedFrontMembership = $revokedMembership.detail.accessControl.userMemberships | Where-Object { $_.userValue -eq $membershipUserName -and $_.groupValue -eq "front" } | Select-Object -First 1

    $membershipMutationPassed = $grantedMembership.detail.counts.accessUserMemberships -eq 5 `
        -and $revokedMembership.detail.counts.accessUserMemberships -eq 4 `
        -and $null -ne $frontMembership `
        -and $null -eq $revokedFrontMembership

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationMembershipUserId" -Method Delete -Headers $administrationHeaders -TimeoutSec 20 | Out-Null
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
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/access-control/user-memberships/smoke-membership-user/front" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
    }
    catch {
    }
    if ($null -ne $administrationMembershipUserId) {
        try {
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationMembershipUserId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$administrationUserMutationId = $null
try {
    $administrationHeaders = Get-AdministrationHeaders
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

    $createdUser = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users" -Method Post -Headers $administrationHeaders -ContentType "application/json" -Body $createUserBody -TimeoutSec 20
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

    $updatedUser = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationUserMutationId" -Method Put -Headers $administrationHeaders -ContentType "application/json" -Body $updateUserBody -TimeoutSec 20
    $updatedUserVisible = $updatedUser.detail.users | Where-Object { $_.id -eq $administrationUserMutationId -and $_.lastName -eq "Admin Inactive" -and -not $_.active } | Select-Object -First 1
    $userMutationPassed = $null -ne $createdUserVisible -and $null -ne $updatedUserVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationUserMutationId" -Method Delete -Headers $administrationHeaders -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/users/$administrationUserMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

$administrationFacilityMutationId = $null
try {
    $administrationHeaders = Get-AdministrationHeaders
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

    $createdFacility = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/facilities" -Method Post -Headers $administrationHeaders -ContentType "application/json" -Body $createFacilityBody -TimeoutSec 20
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

    $updatedFacility = Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/facilities/$administrationFacilityMutationId" -Method Put -Headers $administrationHeaders -ContentType "application/json" -Body $updateFacilityBody -TimeoutSec 20
    $updatedVisible = $updatedFacility.detail.facilities | Where-Object { $_.id -eq $administrationFacilityMutationId -and $_.name -eq "$facilityName Inactive" -and -not $_.active } | Select-Object -First 1
    $facilityMutationPassed = $null -ne $createdVisible -and $null -ne $updatedVisible

    Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/facilities/$administrationFacilityMutationId" -Method Delete -Headers $administrationHeaders -TimeoutSec 20 | Out-Null
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
            Invoke-RestMethod -Uri "$ApiBaseUrl/api/administration/facilities/$administrationFacilityMutationId" -Method Delete -Headers (Get-AdministrationHeaders) -TimeoutSec 20 | Out-Null
        }
        catch {
        }
    }
}

try {
    $unauthenticatedReportsStatus = 0
    $frontDeskReportsStatus = 0
    try {
        $unauthenticatedReports = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/reports/operational" `
            -Method Get `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedReportsStatus = [int]$unauthenticatedReports.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedReportsStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }
    try {
        $frontDeskReports = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/reports/operational" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskReportsStatus = [int]$frontDeskReports.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskReportsStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $reports = Invoke-RestMethod -Uri "$ApiBaseUrl/api/reports/operational" -Method Get -Headers (Get-AdministrationHeaders) -TimeoutSec 20
    $topProvider = $reports.providerActivity | Where-Object { $_.username -eq "gold-provider-02" } | Select-Object -First 1
    $northFacility = $reports.facilityActivity | Where-Object { $_.code -eq "NORTH" } | Select-Object -First 1
    $asthmaCondition = $reports.clinicalConditions | Where-Object { $_.title -eq "Asthma, uncomplicated" -and $_.diagnosis -eq "ICD10:J45.909" } | Select-Object -First 1
    $reportsPassed = $unauthenticatedReportsStatus -eq 401 `
        -and $frontDeskReportsStatus -eq 403 `
        -and $reports.counts.patients -eq 1000 `
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
        unauthenticatedStatus = $unauthenticatedReportsStatus
        frontDeskStatus = $frontDeskReportsStatus
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
    $unauthenticatedReportExportStatus = 0
    $frontDeskReportExportStatus = 0
    try {
        $unauthenticatedReportExport = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/reports/operational/export" `
            -Method Get `
            -UseBasicParsing `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $unauthenticatedReportExportStatus = [int]$unauthenticatedReportExport.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $unauthenticatedReportExportStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }
    try {
        $frontDeskReportExport = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/api/reports/operational/export" `
            -Method Get `
            -Headers (Get-FrontDeskHeaders) `
            -UseBasicParsing `
            -TimeoutSec 20 `
            -ErrorAction Stop
        $frontDeskReportExportStatus = [int]$frontDeskReportExport.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $frontDeskReportExportStatus = [int]$_.Exception.Response.StatusCode
        }
        else {
            throw
        }
    }

    $reportExport = Invoke-WebRequest -Uri "$ApiBaseUrl/api/reports/operational/export" -Method Get -Headers (Get-AdministrationHeaders) -UseBasicParsing -TimeoutSec 20
    $contentType = ($reportExport.Headers["Content-Type"] -join ",")
    $exportText = [string]$reportExport.Content
    $sampleLines = $exportText -split "\r?\n" | Select-Object -First 5
    $exportPassed = $unauthenticatedReportExportStatus -eq 401 `
        -and $frontDeskReportExportStatus -eq 403 `
        -and $reportExport.StatusCode -eq 200 `
        -and $contentType -like "text/csv*" `
        -and $exportText.Contains("Section,Name,Metric,Value") `
        -and $exportText.Contains("Counts,Patients,Total,1000") `
        -and $exportText.Contains("Counts,Patient Documents,Total,1200") `
        -and $exportText.Contains("Provider Activity,gold-provider-02,Encounters,176") `
        -and $exportText.Contains("Facility Activity,NORTH,Billing Total,148904.00") `
        -and $exportText.Contains("Clinical Conditions,ICD10:J45.909,Title,""Asthma, uncomplicated""")
    Add-Check -Name "operational reports csv export" -Result $(if ($exportPassed) { "passed" } else { "failed" }) -Details @{
        unauthenticatedStatus = $unauthenticatedReportExportStatus
        frontDeskStatus = $frontDeskReportExportStatus
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
