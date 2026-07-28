param(
    [int]$Port = 5002
)

$ErrorActionPreference = "Stop"

$SolutionRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ApiProject = Join-Path $SolutionRoot "backend\src\AvenChart.Api"
$ApiDll = Join-Path $ApiProject "bin\Release\net10.0\AvenChart.Api.dll"
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts"
$ResultPath = Join-Path $ArtifactsRoot "inventory-atomic-actions-result.json"
$DatabaseName = "legacy-ehr_inventory_atomic_$(Get-Date -Format 'yyyyMMddHHmmss')"
$ApiProcess = $null

if ($DatabaseName -notmatch "^legacy-ehr_inventory_atomic_[0-9]{14}$") {
    throw "The generated temporary database name is invalid."
}

New-Item -ItemType Directory -Path $ArtifactsRoot -Force | Out-Null
$LogRoot = Join-Path $ArtifactsRoot $DatabaseName
New-Item -ItemType Directory -Path $LogRoot -Force | Out-Null

function Test-RejectedRequest {
    param(
        [string]$Uri,
        [hashtable]$Headers,
        [hashtable]$Body
    )

    try {
        Invoke-RestMethod `
            -Uri $Uri `
            -Method Post `
            -Headers $Headers `
            -ContentType "application/json" `
            -Body ($Body | ConvertTo-Json -Depth 5) `
            -TimeoutSec 20 | Out-Null
        return $false
    }
    catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode -eq 400
        }
        throw
    }
}

try {
    Push-Location $SolutionRoot
    dotnet build `
        (Join-Path $ApiProject "AvenChart.Api.csproj") `
        --configuration Release `
        --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "The modernized API Release build failed."
    }

    docker compose exec -T postgres createdb -U legacy-ehr $DatabaseName
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create the isolated inventory verification database."
    }

    docker compose exec -T postgres pg_dump `
        -U legacy-ehr `
        -d legacy-ehr_modernized `
        --no-owner `
        --no-privileges |
        docker compose exec -T postgres psql `
            -X `
            -U legacy-ehr `
            -d $DatabaseName |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not clone the inventory verification database."
    }

    Get-Content `
        -LiteralPath (Join-Path $SolutionRoot "database\migrations\V0068__inventory_destruction_quantity_evidence.sql") `
        -Raw |
        docker compose exec -T postgres psql `
            -X `
            -U legacy-ehr `
            -d $DatabaseName `
            -v ON_ERROR_STOP=1 |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not apply the inventory destruction evidence migration to the isolated database."
    }

    $GeneralItemId = docker compose exec -T postgres psql `
        -X `
        -U legacy-ehr `
        -d $DatabaseName `
        -t `
        -A `
        -c "select i.item_id from inventory_items i where i.active and i.controlled_schedule is null order by i.item_id limit 1;"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($GeneralItemId)) {
        throw "The isolated dataset has no active general-inventory item."
    }

    $env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ConnectionStrings__AvenChart = "Host=localhost;Port=5433;Database=$DatabaseName;Username=legacy-ehr;Password=legacy-ehr_demo"
    $env:RuntimeSafety__RateLimitPermitLimit = "5000"
    $ApiProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @("`"$ApiDll`"") `
        -WorkingDirectory $ApiProject `
        -WindowStyle Hidden `
        -PassThru `
        -RedirectStandardOutput (Join-Path $LogRoot "api-stdout.log") `
        -RedirectStandardError (Join-Path $LogRoot "api-stderr.log")

    $ApiBaseUrl = "http://127.0.0.1:$Port"
    $Ready = $false
    for ($Attempt = 0; $Attempt -lt 60; $Attempt++) {
        try {
            $Health = Invoke-RestMethod -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 2
            if ($Health.status -eq "healthy") {
                $Ready = $true
                break
            }
        }
        catch {
            # Readiness is retried for a bounded 30 seconds.
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $Ready) {
        throw "The isolated API did not become ready."
    }

    $Login = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ username = "admin"; password = "pass" } | ConvertTo-Json) `
        -TimeoutSec 20
    if (-not $Login.authenticated -or [string]::IsNullOrWhiteSpace($Login.sessionId)) {
        throw "The isolated API did not issue an administration session."
    }
    $Headers = @{ "X-Legacy EHR-Session" = $Login.sessionId }

    $Inventory = Invoke-RestMethod -Uri "$ApiBaseUrl/api/inventory/" -Headers $Headers -TimeoutSec 20
    $Vendors = Invoke-RestMethod -Uri "$ApiBaseUrl/api/inventory/vendors" -Headers $Headers -TimeoutSec 20
    $Vendor = @($Vendors.vendors | Where-Object active)[0]
    $Facility = @($Inventory.facilities)[0]
    $Item = @($Inventory.items | Where-Object { $_.itemId -eq [int]$GeneralItemId.Trim() })[0]
    if (-not $Vendor -or -not $Facility -or -not $Item) {
        throw "The isolated dataset lacks the vendor, facility, or item receipt fixture."
    }

    $Suffix = Get-Date -Format "HHmmssfff"
    $DestructionReceipt = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/inventory/purchase-receipts" `
        -Method Post `
        -Headers $Headers `
        -ContentType "application/json" `
        -Body (@{
            vendorId = $Vendor.vendorId
            facilityId = $Facility.facilityId
            itemId = $Item.itemId
            lotNumber = "ATOMIC-D-$Suffix"
            expirationDate = "2027-12-31"
            quantity = 3
            unitCost = 2.5
            referenceNumber = "ATOMIC-D-REF-$Suffix"
            notes = "Isolated destruction verification"
        } | ConvertTo-Json) `
        -TimeoutSec 20

    $DestructionUri = "$ApiBaseUrl/api/inventory/lots/$($DestructionReceipt.lot.lotId)/destructions"
    $MissingWitnessRejected = Test-RejectedRequest `
        -Uri $DestructionUri `
        -Headers $Headers `
        -Body @{
            destructionDate = Get-Date -Format "yyyy-MM-dd"
            method = "Approved waste service"
            notes = "Missing witness must fail"
        }
    $FutureDateRejected = Test-RejectedRequest `
        -Uri $DestructionUri `
        -Headers $Headers `
        -Body @{
            destructionDate = (Get-Date).AddDays(2).ToString("yyyy-MM-dd")
            method = "Approved waste service"
            witness = "Verifier Two"
            notes = "Future date must fail"
        }
    $Destruction = Invoke-RestMethod `
        -Uri $DestructionUri `
        -Method Post `
        -Headers $Headers `
        -ContentType "application/json" `
        -Body (@{
            destructionDate = Get-Date -Format "yyyy-MM-dd"
            method = "Approved waste service"
            witness = "Verifier Two"
            notes = "Isolated full-lot destruction"
        } | ConvertTo-Json) `
        -TimeoutSec 20

    $ExpiredReceipt = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/inventory/purchase-receipts" `
        -Method Post `
        -Headers $Headers `
        -ContentType "application/json" `
        -Body (@{
            vendorId = $Vendor.vendorId
            facilityId = $Facility.facilityId
            itemId = $Item.itemId
            lotNumber = "ATOMIC-E-$Suffix"
            expirationDate = $Inventory.asOfDate
            quantity = 2
            unitCost = 2.5
            referenceNumber = "ATOMIC-E-REF-$Suffix"
            notes = "Isolated expiry verification"
        } | ConvertTo-Json) `
        -TimeoutSec 20
    $ExpiryDisposition = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/inventory/lots/$($ExpiredReceipt.lot.lotId)/expiry-dispositions" `
        -Method Post `
        -Headers $Headers `
        -ContentType "application/json" `
        -Body (@{
            disposition = "destroy"
            method = "Approved waste service"
            witness = "Verifier Two"
            notes = "Isolated expired-lot destruction"
        } | ConvertTo-Json) `
        -TimeoutSec 20

    $CountReceipt = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/inventory/purchase-receipts" `
        -Method Post `
        -Headers $Headers `
        -ContentType "application/json" `
        -Body (@{
            vendorId = $Vendor.vendorId
            facilityId = $Facility.facilityId
            itemId = $Item.itemId
            lotNumber = "ATOMIC-C-$Suffix"
            expirationDate = "2027-12-31"
            quantity = 5
            unitCost = 2.5
            referenceNumber = "ATOMIC-C-REF-$Suffix"
            notes = "Isolated count verification"
        } | ConvertTo-Json) `
        -TimeoutSec 20
    $Count = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/inventory/count-reconciliations" `
        -Method Post `
        -Headers $Headers `
        -ContentType "application/json" `
        -Body (@{
            lotId = $CountReceipt.lot.lotId
            countedQuantity = 4
            notes = "Isolated physical count"
        } | ConvertTo-Json) `
        -TimeoutSec 20

    $Activity = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/inventory/activity?facilityId=$($Facility.facilityId)" `
        -Headers $Headers `
        -TimeoutSec 20
    $DestructionLedger = @($Activity.entries | Where-Object transactionId -eq $Destruction.transaction.transactionId)[0]
    $ExpiryLedger = @($Activity.entries | Where-Object transactionId -eq $ExpiryDisposition.transaction.transactionId)[0]

    $Passed = $MissingWitnessRejected `
        -and $FutureDateRejected `
        -and $Destruction.quantityAffected -eq 3 `
        -and $Destruction.lot.quantityOnHand -eq 0 `
        -and $Destruction.lot.status -eq "inactive" `
        -and $Destruction.transaction.quantityDelta -eq -3 `
        -and $DestructionLedger.quantityDelta -eq -3 `
        -and $ExpiryDisposition.quantityAffected -eq 2 `
        -and $ExpiryDisposition.lot.quantityOnHand -eq 0 `
        -and $ExpiryDisposition.transaction.quantityDelta -eq -2 `
        -and $ExpiryLedger.quantityDelta -eq -2 `
        -and $Count.expectedQuantity -eq 5 `
        -and $Count.countedQuantity -eq 4 `
        -and $Count.quantityDelta -eq -1 `
        -and $Count.lot.quantityOnHand -eq 4

    $Result = [ordered]@{
        status = $(if ($Passed) { "passed" } else { "failed" })
        generatedAt = (Get-Date).ToString("o")
        databaseMode = "temporary-clone"
        missingWitnessRejected = $MissingWitnessRejected
        futureDateRejected = $FutureDateRejected
        destructionId = $Destruction.destructionId
        destructionTransactionId = $Destruction.transaction.transactionId
        destructionQuantityAffected = $Destruction.quantityAffected
        destructionRemaining = $Destruction.lot.quantityOnHand
        expiryDispositionId = $ExpiryDisposition.dispositionId
        expiryTransactionId = $ExpiryDisposition.transaction.transactionId
        expiryRemaining = $ExpiryDisposition.lot.quantityOnHand
        countReconciliationId = $Count.reconciliationId
        countVariance = $Count.quantityDelta
        countRemaining = $Count.lot.quantityOnHand
    }
    $Result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
    $Result | ConvertTo-Json -Depth 5

    if (-not $Passed) {
        throw "The isolated atomic inventory verification failed."
    }
}
finally {
    if ($ApiProcess -and -not $ApiProcess.HasExited) {
        Stop-Process -Id $ApiProcess.Id -Force
        $ApiProcess.WaitForExit()
    }
    if ($DatabaseName -match "^legacy-ehr_inventory_atomic_[0-9]{14}$") {
        docker compose exec -T postgres dropdb -U legacy-ehr --if-exists $DatabaseName | Out-Null
    }
    Pop-Location
}
