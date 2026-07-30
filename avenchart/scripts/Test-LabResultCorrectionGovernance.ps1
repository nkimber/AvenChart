param(
    [string]$ApiBaseUrl = "http://localhost:5001",
    [string]$PatientId = "MOD-PAT-0004",
    [int]$EncounterId = 1000043
)

$ErrorActionPreference = "Stop"
$marker = "LAB-CORR-$([Guid]::NewGuid().ToString('N').Substring(0, 10).ToUpperInvariant())"
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

function Get-ResultFromDetail {
    param([object]$Detail, [int]$ExpectedResultId)

    return @(
        $Detail.orders |
            ForEach-Object { $_.reports } |
            ForEach-Object { $_.results } |
            Where-Object { $_.id -eq $ExpectedResultId }
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
    $orderedAt = $now.ToString("yyyy-MM-ddTHH:mm:ss")
    $order = Invoke-Api -Path "/api/procedures/orders" -Method "POST" -ExpectedStatus @(201) -Body @{
        patientId = $PatientId
        providerId = $null
        labId = $null
        encounterId = $EncounterId
        dateOrdered = $orderedAt
        priority = "routine"
        status = "pending"
        procedureCode = $marker
        procedureName = "Correction governance proof"
        procedureType = "laboratory"
        diagnosis = "Z00.00"
        instructions = "Temporary automated proof; safe to delete."
    }
    $orderId = [int]$order.Body.id
    Assert-Check ($orderId -gt 0) "Order creation" "Created temporary order $orderId."

    $specimen = Invoke-Api -Path "/api/procedures/specimens" -Method "POST" -ExpectedStatus @(201) -Body @{
        orderId = $orderId
        specimenIdentifier = "$marker-SPEC"
        accessionIdentifier = "$marker-ACC"
        specimenTypeCode = "SER"
        specimenType = "Serum"
        collectionMethodCode = "VEN"
        collectionMethod = "Venipuncture"
        specimenLocationCode = "LAB"
        specimenLocation = "Main laboratory"
        collectedDate = $orderedAt
        volumeValue = 2.0
        volumeUnit = "mL"
        conditionCode = "SAT"
        specimenCondition = "Satisfactory"
        comments = "Temporary correction governance proof."
    }
    Assert-Check ([int]$specimen.Body.id -gt 0) "Specimen creation" "Created a specimen linked to the temporary order."

    $report = Invoke-Api -Path "/api/procedures/reports" -Method "POST" -ExpectedStatus @(201) -Body @{
        orderId = $orderId
        dateCollected = $orderedAt
        dateReport = $orderedAt
        specimenNumber = "$marker-RPT"
        reportStatus = "final"
        reviewStatus = "received"
        notes = "Temporary correction governance proof."
    }
    $reportId = [int]$report.Body.id
    Assert-Check ($reportId -gt 0) "Report creation" "Created report $reportId in received review state."

    $result = Invoke-Api -Path "/api/procedures/results" -Method "POST" -ExpectedStatus @(201) -Body @{
        reportId = $reportId
        resultCode = "$marker-RES"
        resultText = "Governed critical result"
        dateTime = $orderedAt
        facility = "Main laboratory"
        units = "mg/dL"
        result = "8.1"
        range = "0.0-1.0"
        abnormal = "C"
        comments = "Temporary correction governance proof."
        status = "final"
    }
    $resultId = [int]$result.Body.id
    $createdResult = Get-ResultFromDetail -Detail $result.Body.detail -ExpectedResultId $resultId
    Assert-Check ($createdResult.currentVersion -eq 1) "Initial result version" "The new result started at version 1."

    $correctionBase = @{
        resultCode = "$marker-RES"
        resultText = "Governed critical result"
        dateTime = $orderedAt
        units = "mg/dL"
        result = "8.1"
        range = "0.0-1.0"
        abnormal = "C"
        status = "corrected"
        expectedVersion = 1
        reason = "Verified analyzer transcription against the source worksheet."
    }

    $missingReason = @{} + $correctionBase
    $missingReason.reason = ""
    $missingReasonResponse = Invoke-Api -Path "/api/procedures/results/$resultId" -Method "PUT" -Body $missingReason -ExpectedStatus @(400)
    Assert-Check ($missingReasonResponse.Status -eq 400) "Correction reason required" "A correction without a reason was rejected."

    $noOp = @{} + $correctionBase
    $noOp.status = "final"
    $noOpResponse = Invoke-Api -Path "/api/procedures/results/$resultId" -Method "PUT" -Body $noOp -ExpectedStatus @(400)
    Assert-Check ($noOpResponse.Status -eq 400) "No-op correction rejected" "An unchanged correction was rejected."

    $firstCorrection = @{} + $correctionBase
    $firstCorrection.result = "7.9"
    $corrected = Invoke-Api -Path "/api/procedures/results/$resultId" -Method "PUT" -Body $firstCorrection
    $correctedResult = Get-ResultFromDetail -Detail $corrected.Body.detail -ExpectedResultId $resultId
    $versionOne = @($correctedResult.versionHistory | Where-Object { $_.version -eq 1 })[0]
    Assert-Check ($correctedResult.currentVersion -eq 2 -and $correctedResult.result -eq "7.9") "Governed correction" "The changed result advanced to version 2."
    Assert-Check (
        $versionOne.correctionActor -eq "admin" -and
        $versionOne.correctionReason -eq $firstCorrection.reason -and
        $versionOne.resultingVersion -eq 2
    ) "Correction provenance" "Version 1 retained the authenticated actor, reason, and resulting version."

    $stale = @{} + $firstCorrection
    $stale.result = "7.8"
    $stale.reason = "Stale correction should not apply."
    $staleResponse = Invoke-Api -Path "/api/procedures/results/$resultId" -Method "PUT" -Body $stale -ExpectedStatus @(409)
    Assert-Check ($staleResponse.Body.currentVersion -eq 2) "Stale correction conflict" "A version-1 update was rejected after version 2 existed."

    $signed = Invoke-Api -Path "/api/procedures/reports/$reportId/sign" -Method "PUT" -Body @{
        expectedReviewVersion = 1
        reason = "Reviewed for correction-governance proof."
    }
    $signedReport = @($signed.Body.detail.orders.reports | Where-Object { $_.id -eq $reportId })[0]
    Assert-Check ($signedReport.reviewStatus -eq "reviewed" -and $signedReport.reviewVersion -eq 2) "Report signing" "The report entered terminal reviewed state at review version 2."

    $terminalCorrection = @{} + $firstCorrection
    $terminalCorrection.expectedVersion = 2
    $terminalCorrection.result = "7.7"
    $terminalCorrection.reason = "Terminal-state correction should not apply."
    $terminalResponse = Invoke-Api -Path "/api/procedures/results/$resultId" -Method "PUT" -Body $terminalCorrection -ExpectedStatus @(409)
    Assert-Check ($terminalResponse.Body.reviewStatus -eq "reviewed") "Terminal review protection" "A correction was blocked while review was terminal."

    $reopened = Invoke-Api -Path "/api/procedures/reports/$reportId/reopen-review" -Method "PUT" -Body @{
        expectedReviewVersion = 2
        reason = "Correction is required after clinical review."
    }
    $reopenedReport = @($reopened.Body.detail.orders.reports | Where-Object { $_.id -eq $reportId })[0]
    Assert-Check ($reopenedReport.reviewStatus -eq "received" -and $reopenedReport.reviewVersion -eq 3) "Review reopening" "The report returned to received state at review version 3."

    $secondCorrection = @{} + $terminalCorrection
    $secondCorrection.result = "7.6"
    $secondCorrection.reason = "Corrected after governed review reopening."
    $secondCorrected = Invoke-Api -Path "/api/procedures/results/$resultId" -Method "PUT" -Body $secondCorrection
    $versionThreeResult = Get-ResultFromDetail -Detail $secondCorrected.Body.detail -ExpectedResultId $resultId
    $versionTwo = @($versionThreeResult.versionHistory | Where-Object { $_.version -eq 2 })[0]
    Assert-Check ($versionThreeResult.currentVersion -eq 3 -and $versionTwo.resultingVersion -eq 3) "Post-reopen correction" "The reopened correction advanced the result to version 3."

    $resigned = Invoke-Api -Path "/api/procedures/reports/$reportId/sign" -Method "PUT" -Body @{
        expectedReviewVersion = 3
        reason = "Reviewed corrected value and re-signed."
    }
    $resignedReport = @($resigned.Body.detail.orders.reports | Where-Object { $_.id -eq $reportId })[0]
    Assert-Check ($resignedReport.reviewStatus -eq "reviewed" -and $resignedReport.reviewVersion -eq 4) "Report re-signing" "The corrected report returned to reviewed state at review version 4."

    $reviewHistory = Invoke-Api -Path "/api/procedures/reports/$reportId/review-history"
    $actions = @($reviewHistory.Body.events | ForEach-Object { $_.action })
    Assert-Check (
        $actions.Count -eq 3 -and
        $actions[0] -eq "signed" -and
        $actions[1] -eq "reopened" -and
        $actions[2] -eq "signed"
    ) "Review event history" "Sign, reopen, and re-sign events were retained in order."

    $criticalQueue = Invoke-Api -Path "/api/procedures/critical-result-queue"
    $criticalItem = @($criticalQueue.Body.results | Where-Object { $_.resultId -eq $resultId })[0]
    Assert-Check ($null -ne $criticalItem -and $criticalItem.abnormal -eq "C") "Critical flag normalization" "The UI's C flag appeared in the critical-result queue."

    $acknowledged = Invoke-Api -Path "/api/procedures/results/$resultId/critical-acknowledgement" -Method "PUT" -Body @{
        expectedVersion = $criticalItem.acknowledgementVersion
        reason = "Critical value acknowledged during lifecycle proof."
    }
    Assert-Check ($acknowledged.Body.acknowledged) "Critical acknowledgement" "The critical result was acknowledged."

    $finalDetail = Invoke-Api -Path "/api/procedures/$PatientId"
    $finalResult = Get-ResultFromDetail -Detail $finalDetail.Body -ExpectedResultId $resultId
    Assert-Check (
        $finalResult.currentVersion -eq 3 -and
        $finalResult.versionHistoryCount -eq 3 -and
        $finalResult.hasPriorVersions
    ) "Final patient projection" "The patient projection retained version 3 and both prior versions."

    [pscustomobject]@{
        marker = $marker
        patientId = $PatientId
        orderId = $orderId
        reportId = $reportId
        resultId = $resultId
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
