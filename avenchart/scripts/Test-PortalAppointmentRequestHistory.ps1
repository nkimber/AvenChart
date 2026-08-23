# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = "http://127.0.0.1:5001",
    [switch]$IncludeBrowser
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "AvenChartStaffAccessContext.ps1")
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$checks = [System.Collections.Generic.List[object]]::new()
$createdAppointmentIds = [System.Collections.Generic.List[string]]::new()
$marker = "TMP-PORTAL-REQUEST-HISTORY-$(New-Guid)"
$adminHeaders = $null

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
}

function Assert-GeneratedAppointmentId([string]$AppointmentId) {
    if ($AppointmentId -notmatch '^APPT-PORTAL-[0-9a-f]{32}$') {
        throw "Unexpected generated appointment ID '$AppointmentId'."
    }
}

function Invoke-PostgresScalar([string]$Sql) {
    Push-Location $solutionRoot
    try {
        return (& docker compose exec -T postgres psql -X -U avenchart -d avenchart -Atc $Sql).Trim()
    }
    finally {
        Pop-Location
    }
}

function New-PortalRequest(
    [string]$PortalSessionId,
    [object]$Options,
    [string]$StartTime,
    [string]$Reason
) {
    $body = @{
        providerId = $Options.defaults.providerId
        facilityId = $Options.defaults.facilityId
        categoryId = $Options.defaults.categoryId
        date = $Options.defaults.date
        startTime = $StartTime
        durationMinutes = $Options.defaults.durationMinutes
        reason = $Reason
    } | ConvertTo-Json
    $created = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patient-portal/appointments/requests" `
        -Method Post `
        -Headers @{ "X-AvenChart-Patient-Portal-Session" = $PortalSessionId } `
        -ContentType "application/json" `
        -Body $body
    if (-not $created.created -or $null -eq $created.appointment) {
        throw "Portal appointment request was not created: $($created.failureReason)"
    }
    Assert-GeneratedAppointmentId $created.appointment.id
    $createdAppointmentIds.Add([string]$created.appointment.id)
    return $created
}

function Get-PortalHistory([string]$PortalSessionId) {
    return Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patient-portal/appointments" `
        -Headers @{ "X-AvenChart-Patient-Portal-Session" = $PortalSessionId }
}

try {
    $portal = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patient-portal/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body '{"username":"mod-pat-0004@example.test","password":"PortalPass207!"}'
    $admin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body '{"username":"admin","password":"pass"}'
    if (-not $portal.authenticated -or -not $admin.authenticated) {
        throw "The required synthetic portal and administrator sessions were not issued."
    }
    $adminHeaders = New-AvenChartStaffAccessContextHeaders -Login $admin
    $portalHeaders = @{ "X-AvenChart-Patient-Portal-Session" = $portal.sessionId }
    $options = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/patient-portal/appointments/request-options" `
        -Headers $portalHeaders

    $cancelledRequest = New-PortalRequest `
        -PortalSessionId $portal.sessionId `
        -Options $options `
        -StartTime "09:10" `
        -Reason "$marker accepted-then-cancelled"
    $pendingHistory = Get-PortalHistory $portal.sessionId
    $pending = @($pendingHistory.appointmentRequests | Where-Object {
        $_.appointmentId -eq $cancelledRequest.appointment.id
    }) | Select-Object -First 1
    Add-Check `
        "New portal request is durably pending with a runtime requested event" `
        ($null -ne $pending `
            -and $pending.state -eq "pending" `
            -and $pending.version -eq 1 `
            -and $pending.evidenceSource -eq "runtime" `
            -and @($pending.events).Count -eq 1 `
            -and $pending.events[0].action -eq "requested") `
        @{ state=$pending.state; version=$pending.version; actions=@($pending.events.action); source=$pending.evidenceSource }

    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/appointments/$($cancelledRequest.appointment.id)/status" `
        -Method Put `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body '{"status":"-","title":"Portal request accepted"}' | Out-Null
    $acceptedHistory = Get-PortalHistory $portal.sessionId
    $accepted = @($acceptedHistory.appointmentRequests | Where-Object {
        $_.appointmentId -eq $cancelledRequest.appointment.id
    }) | Select-Object -First 1
    Add-Check `
        "Staff scheduling acceptance advances the same request with immutable evidence" `
        ($accepted.state -eq "accepted" `
            -and $accepted.version -eq 2 `
            -and @($accepted.events.action) -contains "accepted" `
            -and @($accepted.events.action) -contains "requested") `
        @{ state=$accepted.state; version=$accepted.version; actions=@($accepted.events.action) }

    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/appointments/$($cancelledRequest.appointment.id)/status" `
        -Method Put `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body '{"status":"x","title":"Portal request cancelled"}' | Out-Null
    $cancelledHistory = Get-PortalHistory $portal.sessionId
    $cancelled = @($cancelledHistory.appointmentRequests | Where-Object {
        $_.appointmentId -eq $cancelledRequest.appointment.id
    }) | Select-Object -First 1
    Add-Check `
        "Cancellation after acceptance remains distinct from decline" `
        ($cancelled.state -eq "cancelled" `
            -and $cancelled.version -eq 3 `
            -and $cancelled.nextAction -match "new request" `
            -and $cancelled.events[0].action -eq "cancelled") `
        @{ state=$cancelled.state; version=$cancelled.version; nextAction=$cancelled.nextAction; actions=@($cancelled.events.action) }

    $declinedRequest = New-PortalRequest `
        -PortalSessionId $portal.sessionId `
        -Options $options `
        -StartTime "10:10" `
        -Reason "$marker declined"
    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/appointments/$($declinedRequest.appointment.id)/status" `
        -Method Put `
        -Headers $adminHeaders `
        -ContentType "application/json" `
        -Body '{"status":"x","title":"Portal request declined"}' | Out-Null
    $declinedHistory = Get-PortalHistory $portal.sessionId
    $declined = @($declinedHistory.appointmentRequests | Where-Object {
        $_.appointmentId -eq $declinedRequest.appointment.id
    }) | Select-Object -First 1
    Add-Check `
        "Cancellation directly from pending is patient-visible as declined" `
        ($declined.state -eq "declined" `
            -and $declined.version -eq 2 `
            -and $declined.events[0].action -eq "declined") `
        @{ state=$declined.state; version=$declined.version; actions=@($declined.events.action) }

    $expiredRequest = New-PortalRequest `
        -PortalSessionId $portal.sessionId `
        -Options $options `
        -StartTime "11:10" `
        -Reason "$marker expired"
    $expiredId = [string]$expiredRequest.appointment.id
    Invoke-PostgresScalar "update appointments set appointment_date=(select base_date - 1 from dataset_metadata order by generated_at desc limit 1) where id='$expiredId'; select 1;" | Out-Null
    $expiredHistory = Get-PortalHistory $portal.sessionId
    $expired = @($expiredHistory.appointmentRequests | Where-Object {
        $_.appointmentId -eq $expiredId
    }) | Select-Object -First 1
    Add-Check `
        "Past unaccepted request is transparently derived as expired" `
        ($expired.state -eq "expired" `
            -and $expired.stateSource -match "derived from request date" `
            -and $expired.nextAction -match "requested date passed" `
            -and @($expired.events.action) -contains "updated") `
        @{ state=$expired.state; stateSource=$expired.stateSource; nextAction=$expired.nextAction; actions=@($expired.events.action) }

    $ownedRequests = @($expiredHistory.appointmentRequests | Where-Object {
        $_.reason -like "$marker*"
    })
    Add-Check `
        "Portal response returns bounded counts, stable facts, timestamps, and next actions" `
        ($expiredHistory.appointmentRequestCount -ge 3 `
            -and $ownedRequests.Count -eq 3 `
            -and @($ownedRequests | Where-Object {
                -not $_.requestedAt `
                    -or -not $_.updatedAt `
                    -or -not $_.providerName `
                    -or -not $_.facilityName `
                    -or -not $_.nextAction
            }).Count -eq 0) `
        @{ total=$expiredHistory.appointmentRequestCount; returned=@($expiredHistory.appointmentRequests).Count; owned=$ownedRequests.Count }

    if ($IncludeBrowser) {
        $avenChartUiRoot = Resolve-Path (Join-Path $solutionRoot "..\avenchart-ui")
        $priorFixtureId = $env:MODERN_UI_PORTAL_REQUEST_HISTORY_ID
        $env:MODERN_UI_PORTAL_REQUEST_HISTORY_ID = $cancelledRequest.appointment.id
        Push-Location $avenChartUiRoot
        try {
            & npx playwright test e2e/portal-appointment-request-history.spec.ts --workers=1
            $browserExitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
            if ($null -eq $priorFixtureId) {
                Remove-Item Env:MODERN_UI_PORTAL_REQUEST_HISTORY_ID -ErrorAction SilentlyContinue
            }
            else {
                $env:MODERN_UI_PORTAL_REQUEST_HISTORY_ID = $priorFixtureId
            }
        }
        Add-Check `
            "Portal request history passes the configured browser and accessibility profiles" `
            ($browserExitCode -eq 0) `
            @{ exitCode=$browserExitCode; fixture=$cancelledRequest.appointment.id }
    }
}
catch {
    Add-Check "Unhandled portal appointment request history test error" $false $_.Exception.Message
}
finally {
    if ($null -ne $adminHeaders) {
        foreach ($appointmentId in $createdAppointmentIds) {
            try {
                Invoke-WebRequest `
                    -Uri "$ApiBaseUrl/api/appointments/$appointmentId" `
                    -Method Delete `
                    -Headers $adminHeaders `
                    -UseBasicParsing | Out-Null
            }
            catch {
                Add-Check "Cleanup appointment $appointmentId" $false $_.Exception.Message
            }
        }
    }

    if ($createdAppointmentIds.Count -gt 0) {
        $quotedIds = @($createdAppointmentIds | ForEach-Object {
            Assert-GeneratedAppointmentId $_
            "'$_'"
        }) -join ","
        try {
            Invoke-PostgresScalar "delete from messages where portal_relation in (select 'portal:appointment-request:' || id from unnest(array[$quotedIds]) as owned(id)); select 1;" | Out-Null
        }
        catch {
            Add-Check "Cleanup portal appointment reminder messages" $false $_.Exception.Message
        }

        $remaining = Invoke-PostgresScalar "select json_build_object('appointments',(select count(*) from appointments where id in ($quotedIds)),'requests',(select count(*) from patient_portal_appointment_requests where appointment_id in ($quotedIds)),'events',(select count(*) from patient_portal_appointment_request_events where appointment_id in ($quotedIds)),'messages',(select count(*) from messages where portal_relation in (select 'portal:appointment-request:' || id from unnest(array[$quotedIds]) as owned(id))));"
        $remainingFacts = $remaining | ConvertFrom-Json
        Add-Check `
            "Portal appointment request proof leaves zero owned residue" `
            ($remainingFacts.appointments -eq 0 `
                -and $remainingFacts.requests -eq 0 `
                -and $remainingFacts.events -eq 0 `
                -and $remainingFacts.messages -eq 0) `
            $remainingFacts
    }
}

$failed = @($checks | Where-Object { $_.status -ne "passed" })
$result = [ordered]@{
    status = if ($failed.Count -eq 0) { "passed" } else { "failed" }
    checks = $checks
}
$result | ConvertTo-Json -Depth 12
if ($failed.Count -gt 0) { exit 1 }
