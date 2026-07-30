param(
    [string]$ApiBaseUrl = "http://localhost:5001",
    [string]$PatientId = "MOD-PAT-0004",
    [switch]$IncludeBrowser
)

$ErrorActionPreference = "Stop"
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$modernUiRoot = Resolve-Path (Join-Path $solutionRoot "..\avenchart-ui")
$checks = [System.Collections.Generic.List[object]]::new()
$marker = "TMP-ENC-CODING-$(New-Guid)"
$headers = $null
$encounter = $null
$billingLineIds = [System.Collections.Generic.List[string]]::new()
$procedureOrderIds = [System.Collections.Generic.List[int]]::new()

function Add-Check([string]$Name, [bool]$Passed, [object]$Details) {
    $checks.Add([ordered]@{
        name = $Name
        status = if ($Passed) { "passed" } else { "failed" }
        details = $Details
    })
}

function Invoke-JsonRequest(
    [string]$Uri,
    [string]$Method = "Get",
    [object]$Body = $null
) {
    $parameters = @{
        Uri = $Uri
        Method = $Method
        Headers = $headers
        TimeoutSec = 30
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 10
    }
    return Invoke-RestMethod @parameters
}

function Get-HttpStatus([System.Management.Automation.ErrorRecord]$ErrorRecord) {
    if ($null -eq $ErrorRecord.Exception.Response) {
        return 0
    }
    return [int]$ErrorRecord.Exception.Response.StatusCode
}

function Invoke-FixtureSql([string]$Sql) {
    Push-Location $solutionRoot
    try {
        & docker compose exec -T postgres psql `
            -X `
            -U legacy-ehr `
            -d legacy-ehr_modernized `
            -v ON_ERROR_STOP=1 `
            -c $Sql | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Fixture SQL failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-BrowserProof {
    Push-Location $modernUiRoot
    try {
        & npx playwright test e2e/encounter-create-coding.spec.ts --workers=4 | Out-Host
        return $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}

try {
    if ($marker -notmatch '^TMP-ENC-CODING-[0-9a-f-]+$') {
        throw "Unsafe encounter-coding test marker."
    }

    $admin = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body '{"username":"admin","password":"pass"}'
    if (-not $admin.authenticated) {
        throw "The synthetic administrator session was not issued."
    }
    $headers = @{ "X-Legacy EHR-Session" = $admin.sessionId }

    $schedulingOptions = Invoke-JsonRequest "$ApiBaseUrl/api/appointments/scheduling-options"
    $provider = @($schedulingOptions.providers) | Select-Object -First 1
    $facility = @($schedulingOptions.facilities) | Select-Object -First 1
    if ($null -eq $provider -or $null -eq $facility) {
        throw "Provider and facility scheduling options are required."
    }

    $today = (Get-Date).ToString("yyyy-MM-dd")
    $created = Invoke-JsonRequest `
        "$ApiBaseUrl/api/encounters" `
        "Post" `
        @{
            patientId = $PatientId
            providerId = $provider.id
            dateTime = "$today 10:30:00"
            reason = "$marker complete visit"
            facilityId = $facility.id
            billingFacilityId = $facility.id
            sensitivity = "standard"
            referralSource = "$marker referral"
            externalId = "$marker external"
            posCode = 11
            billingNote = "$marker billing note"
            sourceAppointmentId = $null
        }
    $encounter = [int]$created.encounter
    Add-Check `
        "Full encounter metadata is persisted with resolved provider and facility" `
        ($encounter -gt 0 `
            -and $created.patientId -eq $PatientId `
            -and $created.reason -eq "$marker complete visit" `
            -and $created.providerName `
            -and $created.facilityName `
            -and $created.sensitivity -eq "standard" `
            -and $created.referralSource -eq "$marker referral" `
            -and $created.externalId -eq "$marker external" `
            -and $created.posCode -eq 11 `
            -and $created.billingNote -eq "$marker billing note") `
        @{ encounter=$encounter; provider=$created.providerName; facility=$created.facilityName; sensitivity=$created.sensitivity; posCode=$created.posCode }

    $invalidPatientStatus = 0
    try {
        Invoke-JsonRequest `
            "$ApiBaseUrl/api/encounters" `
            "Post" `
            @{
                patientId = "NOT-A-PATIENT"
                providerId = $provider.id
                dateTime = "$today 10:30:00"
                reason = "$marker invalid patient"
                facilityId = $facility.id
            } | Out-Null
    }
    catch {
        $invalidPatientStatus = Get-HttpStatus $_
    }
    Add-Check `
        "Unknown patient encounter creation is rejected without a row" `
        ($invalidPatientStatus -eq 400) `
        @{ status=$invalidPatientStatus }

    $diagnosis = Invoke-JsonRequest `
        "$ApiBaseUrl/api/billing/lines" `
        "Post" `
        @{
            patientId = $PatientId
            providerId = $provider.id
            encounter = $encounter
            billingDate = $today
            codeType = "ICD10"
            code = "Z71.89"
            modifier = $null
            codeText = "$marker diagnosis"
            fee = 0
            units = 1
            justify = ""
        }
    $billingLineIds.Add([string]$diagnosis.id)

    $charge = Invoke-JsonRequest `
        "$ApiBaseUrl/api/billing/lines" `
        "Post" `
        @{
            patientId = $PatientId
            providerId = $provider.id
            encounter = $encounter
            billingDate = $today
            codeType = "CPT"
            code = "99213"
            modifier = "25"
            codeText = "$marker charge"
            fee = 125.50
            units = 2
            justify = "Z71.89"
        }
    $billingLineIds.Add([string]$charge.id)

    $orderCatalog = Invoke-JsonRequest "$ApiBaseUrl/api/procedures/order-catalog"
    $catalogItem = @(
        $orderCatalog.items |
            Where-Object { $_.active -and $_.itemType -eq "ord" -and $_.code }
    ) | Select-Object -First 1
    if ($null -eq $catalogItem) {
        throw "An active procedure catalog item is required."
    }
    $order = Invoke-JsonRequest `
        "$ApiBaseUrl/api/procedures/orders" `
        "Post" `
        @{
            patientId = $PatientId
            providerId = $provider.id
            labId = $catalogItem.labId
            encounterId = $encounter
            dateOrdered = $today
            priority = "urgent"
            status = "pending"
            procedureCode = $catalogItem.code
            procedureName = $catalogItem.name
            procedureType = if ($catalogItem.procedureTypeName) { $catalogItem.procedureTypeName } else { "laboratory" }
            diagnosis = "Z71.89"
            instructions = "$marker procedure instructions"
        }
    $procedureOrderIds.Add([int]$order.id)

    $detail = Invoke-JsonRequest "$ApiBaseUrl/api/encounters/$encounter"
    $projectedDiagnosis = @($detail.diagnosisCodes | Where-Object { $_.code -eq "Z71.89" }) | Select-Object -First 1
    $projectedCharge = @($detail.billingLines | Where-Object { $_.id -eq [string]$charge.id }) | Select-Object -First 1
    $projectedOrder = @($detail.procedureOrders | Where-Object { $_.id -eq [int]$order.id }) | Select-Object -First 1
    Add-Check `
        "Encounter projection carries diagnosis provenance across billing and procedure links" `
        ($null -ne $projectedDiagnosis `
            -and $projectedDiagnosis.billingLineCount -eq 2 `
            -and $projectedDiagnosis.procedureOrderCount -eq 1 `
            -and @($projectedDiagnosis.sources) -contains "Fee sheet diagnosis line" `
            -and @($projectedDiagnosis.sources) -contains "Fee sheet justification" `
            -and @($projectedDiagnosis.sources) -contains "Procedure order diagnosis") `
        $projectedDiagnosis
    Add-Check `
        "Encounter projection exposes the linked charge and governed procedure order" `
        ($null -ne $projectedCharge `
            -and $projectedCharge.code -eq "99213" `
            -and $projectedCharge.modifier -eq "25" `
            -and $projectedCharge.fee -eq 125.50 `
            -and $projectedCharge.units -eq 2 `
            -and $null -ne $projectedOrder `
            -and $projectedOrder.code -eq $catalogItem.code `
            -and $projectedOrder.orderPriority -eq "urgent" `
            -and $projectedOrder.diagnosis -eq "Z71.89") `
        @{ billingLine=$projectedCharge; procedureOrder=$projectedOrder }

    $mismatchedPatientStatus = 0
    try {
        Invoke-JsonRequest `
            "$ApiBaseUrl/api/billing/lines" `
            "Post" `
            @{
                patientId = "MOD-PAT-0001"
                providerId = $provider.id
                encounter = $encounter
                billingDate = $today
                codeType = "CPT"
                code = "00000"
                modifier = $null
                codeText = "$marker invalid relationship"
                fee = 1
                units = 1
                justify = "Z71.89"
            } | Out-Null
    }
    catch {
        $mismatchedPatientStatus = Get-HttpStatus $_
    }
    Add-Check `
        "Patient and encounter mismatch is rejected by the billing contract" `
        ($mismatchedPatientStatus -eq 400) `
        @{ status=$mismatchedPatientStatus }

    if ($IncludeBrowser) {
        $browserExitCode = Invoke-BrowserProof
        Add-Check `
            "Full metadata creation and encounter coding pass configured browser profiles" `
            ($browserExitCode -eq 0) `
            @{ exitCode=$browserExitCode }
    }
}
finally {
    foreach ($billingLineId in $billingLineIds) {
        try {
            Invoke-JsonRequest "$ApiBaseUrl/api/billing/lines/$billingLineId" "Delete" | Out-Null
        }
        catch {
            Write-Warning "Could not delete billing-line fixture $billingLineId."
        }
    }
    foreach ($procedureOrderId in $procedureOrderIds) {
        try {
            Invoke-JsonRequest "$ApiBaseUrl/api/procedures/orders/$procedureOrderId" "Delete" | Out-Null
        }
        catch {
            Write-Warning "Could not delete procedure-order fixture $procedureOrderId."
        }
    }
    if ($null -ne $encounter -and [int]$encounter -gt 0) {
        $safeEncounter = [int]$encounter
        $safeMarker = $marker.Replace("'", "''")
        Invoke-FixtureSql "delete from encounters where encounter = $safeEncounter and reason = '$safeMarker complete visit';"
    }

    $residue = 0
    if ($null -ne $encounter -and [int]$encounter -gt 0) {
        $safeEncounter = [int]$encounter
        Push-Location $solutionRoot
        try {
            $residue = [int]((& docker compose exec -T postgres psql `
                -X `
                -U legacy-ehr `
                -d legacy-ehr_modernized `
                -Atc "select count(*) from encounters where encounter = $safeEncounter;").Trim())
        }
        finally {
            Pop-Location
        }
    }
    Add-Check `
        "Encounter-coding fixtures leave no database residue" `
        ($residue -eq 0) `
        @{ encounter=$encounter; residue=$residue }
}

$failed = @($checks | Where-Object { $_.status -eq "failed" })
[ordered]@{
    script = "Test-EncounterCreateCoding.ps1"
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
    checkCount = $checks.Count
    failedCount = $failed.Count
    checks = $checks
} | ConvertTo-Json -Depth 12

if ($failed.Count -gt 0) {
    exit 1
}
