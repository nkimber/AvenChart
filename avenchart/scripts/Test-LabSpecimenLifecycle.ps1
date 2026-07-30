param(
    [string]$ApiBaseUrl = "http://localhost:5001",
    [string]$PatientId = "MOD-PAT-0004",
    [int]$EncounterId = 1000043
)

$ErrorActionPreference = "Stop"
$marker = "LAB-SPEC-$([Guid]::NewGuid().ToString('N').Substring(0, 10).ToUpperInvariant())"
$orderId = $null
$headers = @{}
$checks = [System.Collections.Generic.List[object]]::new()

function Assert-Check {
    param([bool]$Condition, [string]$Name, [string]$Detail)

    if (-not $Condition) {
        throw "$Name failed: $Detail"
    }
    $checks.Add([pscustomobject]@{ name = $Name; detail = $Detail })
}

function Invoke-Api {
    param(
        [string]$Path,
        [string]$Method = "GET",
        [object]$Body = $null,
        [int[]]$ExpectedStatus = @(200)
    )

    $parameters = @{
        Uri = "$ApiBaseUrl$Path"
        Method = $Method
        Headers = $headers
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 12 -Compress
    }

    try {
        $response = Invoke-WebRequest @parameters
        $status = [int]$response.StatusCode
        $content = $response.Content
    }
    catch {
        if ($null -eq $_.Exception.Response) {
            throw
        }
        $response = $_.Exception.Response
        $status = [int]$response.StatusCode
        $reader = [System.IO.StreamReader]::new($response.GetResponseStream())
        try {
            $content = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }

    if ($ExpectedStatus -notcontains $status) {
        throw "$Method $Path returned HTTP $status; expected $($ExpectedStatus -join ', '). Body: $content"
    }

    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($content)) {
        $json = $content | ConvertFrom-Json
    }
    return [pscustomobject]@{ Status = $status; Body = $json; Content = $content }
}

function Get-SpecimenFromDetail {
    param([object]$Detail, [int]$ExpectedSpecimenId)

    return @(
        $Detail.orders |
            ForEach-Object { $_.specimens } |
            Where-Object { $_.id -eq $ExpectedSpecimenId }
    )[0]
}

try {
    $health = Invoke-Api -Path "/health"
    Assert-Check ($health.Body.status -eq "healthy") "API health" "The isolated API reported healthy."

    $login = Invoke-Api -Path "/api/auth/login" -Method "POST" -Body @{
        username = "admin"
        password = "pass"
    }
    Assert-Check ($login.Body.authenticated -and -not [string]::IsNullOrWhiteSpace($login.Body.sessionId)) "Administrator login" "A governed staff session was issued."
    $headers = @{ "X-Legacy EHR-Session" = $login.Body.sessionId }

    $now = Get-Date
    $collectedAt = $now.ToString("yyyy-MM-ddTHH:mm:ss")
    $order = Invoke-Api -Path "/api/procedures/orders" -Method "POST" -ExpectedStatus @(201) -Body @{
        patientId = $PatientId
        providerId = $null
        labId = $null
        encounterId = $EncounterId
        dateOrdered = $collectedAt
        priority = "routine"
        status = "pending"
        procedureCode = $marker
        procedureName = "Specimen lifecycle proof"
        procedureType = "laboratory"
        diagnosis = "Z00.00"
        instructions = "Temporary automated proof; safe to delete."
    }
    $orderId = [int]$order.Body.id
    Assert-Check ($orderId -gt 0) "Order creation" "Created temporary order $orderId."

    $created = Invoke-Api -Path "/api/procedures/specimens" -Method "POST" -ExpectedStatus @(201) -Body @{
        orderId = $orderId
        specimenIdentifier = "$marker-SPEC-1"
        accessionIdentifier = "$marker-ACC-1"
        specimenTypeCode = "SER"
        specimenType = "Serum"
        collectionMethodCode = "VEN"
        collectionMethod = "Venipuncture"
        specimenLocationCode = "LAB"
        specimenLocation = "Main laboratory"
        collectedDate = $collectedAt
        volumeValue = 2.0
        volumeUnit = "mL"
        conditionCode = "SAT"
        specimenCondition = "Satisfactory"
        comments = "Temporary specimen lifecycle proof."
    }
    $specimenId = [int]$created.Body.id
    $createdSpecimen = Get-SpecimenFromDetail -Detail $created.Body.detail -ExpectedSpecimenId $specimenId
    Assert-Check (
        $createdSpecimen.specimenStatus -eq "collected" -and
        $createdSpecimen.specimenVersion -eq 1 -and
        $createdSpecimen.historyCount -eq 1
    ) "Governed collection" "The new specimen started collected at version 1 with one event."
    Assert-Check (
        $createdSpecimen.history[0].action -eq "collect" -and
        $createdSpecimen.history[0].actor -eq "admin" -and
        $createdSpecimen.history[0].reason -eq "Initial local specimen collection."
    ) "Collection provenance" "The initial event retained the session actor and transparent collection reason."

    $missingReason = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -ExpectedStatus @(400) -Body @{
        action = "label"
        expectedVersion = 1
        reason = ""
    }
    Assert-Check ($missingReason.Status -eq 400) "Transition reason required" "A transition without a reason was rejected."

    $illegalRecollect = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -ExpectedStatus @(400) -Body @{
        action = "recollect"
        expectedVersion = 1
        reason = "Recollection is not allowed from collected."
        specimenIdentifier = "$marker-INVALID"
        collectedDate = $collectedAt
    }
    Assert-Check ($illegalRecollect.Status -eq 400) "Illegal recollection rejected" "Recollection was blocked before rejection."

    $labeled = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -Body @{
        action = "label"
        expectedVersion = 1
        reason = "Barcode label verified against the local order."
    }
    $labeledSpecimen = Get-SpecimenFromDetail -Detail $labeled.Body.detail -ExpectedSpecimenId $specimenId
    Assert-Check (
        $labeledSpecimen.specimenStatus -eq "labeled" -and
        $labeledSpecimen.specimenVersion -eq 2
    ) "Label transition" "The specimen advanced from collected to labeled at version 2."

    $stale = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -ExpectedStatus @(409) -Body @{
        action = "receive"
        expectedVersion = 1
        reason = "A stale workstation should not receive this specimen."
    }
    Assert-Check (
        $stale.Body.currentVersion -eq 2 -and
        $stale.Body.currentStatus -eq "labeled"
    ) "Stale transition conflict" "The stale version-1 transition was rejected with authoritative state."

    $received = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -Body @{
        action = "receive"
        expectedVersion = 2
        reason = "Laboratory intake verified label and container."
    }
    $receivedSpecimen = Get-SpecimenFromDetail -Detail $received.Body.detail -ExpectedSpecimenId $specimenId
    Assert-Check (
        $receivedSpecimen.specimenStatus -eq "received" -and
        $receivedSpecimen.specimenVersion -eq 3
    ) "Receive transition" "The labeled specimen entered received state at version 3."

    $rejected = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -Body @{
        action = "reject"
        expectedVersion = 3
        reason = "Container integrity failed intake inspection."
    }
    $rejectedSpecimen = Get-SpecimenFromDetail -Detail $rejected.Body.detail -ExpectedSpecimenId $specimenId
    Assert-Check (
        $rejectedSpecimen.specimenStatus -eq "rejected" -and
        $rejectedSpecimen.specimenVersion -eq 4
    ) "Reject transition" "The received specimen entered rejected state at version 4."

    $illegalReceive = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -ExpectedStatus @(400) -Body @{
        action = "receive"
        expectedVersion = 4
        reason = "Rejected specimens cannot be received without recollection."
    }
    Assert-Check ($illegalReceive.Status -eq 400) "Rejected specimen protection" "Direct receipt from rejected state was blocked."

    $missingRecollectionIdentity = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -ExpectedStatus @(400) -Body @{
        action = "recollect"
        expectedVersion = 4
        reason = "A replacement specimen is required."
        specimenIdentifier = ""
        accessionIdentifier = ""
        collectedDate = $collectedAt
    }
    Assert-Check ($missingRecollectionIdentity.Status -eq 400) "Recollection identity required" "A recollection without a new identifier was rejected."

    $unchangedRecollectionIdentity = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -ExpectedStatus @(400) -Body @{
        action = "recollect"
        expectedVersion = 4
        reason = "An unchanged identity must not masquerade as a replacement."
        specimenIdentifier = "$marker-SPEC-1"
        accessionIdentifier = "$marker-ACC-1"
        collectedDate = $collectedAt
    }
    Assert-Check ($unchangedRecollectionIdentity.Status -eq 400) "Replacement identity must change" "A recollection using both original identifiers was rejected."

    $recollectedAt = $now.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:ss")
    $recollectedReason = "Replacement container collected after documented intake rejection."
    $recollected = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -Body @{
        action = "recollect"
        expectedVersion = 4
        reason = $recollectedReason
        specimenIdentifier = "$marker-SPEC-2"
        accessionIdentifier = "$marker-ACC-2"
        collectedDate = $recollectedAt
        conditionCode = "SAT"
        specimenCondition = "Satisfactory replacement"
        comments = "Replacement specimen after container failure."
    }
    $recollectedSpecimen = Get-SpecimenFromDetail -Detail $recollected.Body.detail -ExpectedSpecimenId $specimenId
    $recollectEvent = @($recollectedSpecimen.history | Where-Object { $_.action -eq "recollect" })[0]
    Assert-Check (
        $recollectedSpecimen.specimenStatus -eq "recollected" -and
        $recollectedSpecimen.specimenVersion -eq 5 -and
        $recollectedSpecimen.specimenIdentifier -eq "$marker-SPEC-2"
    ) "Governed recollection" "The rejected specimen was replaced at version 5 with a new identity."
    Assert-Check (
        $recollectEvent.actor -eq "admin" -and
        $recollectEvent.reason -eq $recollectedReason -and
        $recollectEvent.expectedVersion -eq 4 -and
        $recollectEvent.resultingVersion -eq 5
    ) "Recollection provenance" "The recollection event retained actor, reason, expected version, resulting version, and replacement snapshot."

    $relabeled = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -Body @{
        action = "label"
        expectedVersion = 5
        reason = "Replacement label verified."
    }
    $relabeledSpecimen = Get-SpecimenFromDetail -Detail $relabeled.Body.detail -ExpectedSpecimenId $specimenId
    Assert-Check ($relabeledSpecimen.specimenVersion -eq 6 -and $relabeledSpecimen.specimenStatus -eq "labeled") "Replacement labeling" "The replacement specimen entered labeled state at version 6."

    $rereceived = Invoke-Api -Path "/api/procedures/specimens/$specimenId/transition" -Method "PUT" -Body @{
        action = "receive"
        expectedVersion = 6
        reason = "Replacement specimen accepted at laboratory intake."
    }
    $finalSpecimen = Get-SpecimenFromDetail -Detail $rereceived.Body.detail -ExpectedSpecimenId $specimenId
    Assert-Check (
        $finalSpecimen.specimenVersion -eq 7 -and
        $finalSpecimen.specimenStatus -eq "received" -and
        $finalSpecimen.historyCount -eq 7 -and
        $finalSpecimen.history.Count -eq 7
    ) "Complete lifecycle projection" "The authoritative projection retained all seven lifecycle versions and events."

    $eventActions = @($finalSpecimen.history | Sort-Object resultingVersion | ForEach-Object { $_.action })
    Assert-Check (
        ($eventActions -join ",") -eq "collect,label,receive,reject,recollect,label,receive"
    ) "Immutable event sequence" "Collection, rejection, recollection, and replacement receipt remained in order."

    [pscustomobject]@{
        marker = $marker
        patientId = $PatientId
        orderId = $orderId
        specimenId = $specimenId
        finalStatus = $finalSpecimen.specimenStatus
        finalVersion = $finalSpecimen.specimenVersion
        checkCount = $checks.Count
        checks = $checks
    } | ConvertTo-Json -Depth 8
}
finally {
    if ($null -ne $orderId) {
        try {
            Invoke-Api -Path "/api/procedures/orders/$orderId" -Method "DELETE" -ExpectedStatus @(204, 404) | Out-Null
        }
        catch {
            Write-Warning "API cleanup failed for order $orderId; database cleanup will be attempted."
            $safeMarker = $marker.Replace("'", "''")
            & docker compose exec -T postgres psql -X -U legacy-ehr -d legacy-ehr_modernized -v ON_ERROR_STOP=1 -c "delete from lab_orders where id = $orderId and code = '$safeMarker';" | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Database cleanup also failed for order $orderId."
            }
        }
    }
}
