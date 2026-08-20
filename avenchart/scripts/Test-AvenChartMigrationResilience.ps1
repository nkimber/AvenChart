# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [int[]]$FaultCheckpoints = @(1, 64, 127),
    [int]$ApiPort = 5012,
    [int]$ApiWaitSeconds = 90
)

$ErrorActionPreference = "Stop"

if ($ApiPort -lt 1024 -or $ApiPort -gt 65535) {
    throw "ApiPort must be between 1024 and 65535."
}

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ExpectedMigrationCount = @(Get-ChildItem (Join-Path $SolutionRoot "database\migrations") -Filter '*.sql' -File).Count
if ($ExpectedMigrationCount -lt 2) {
    throw "The packaged migration catalog is unexpectedly empty."
}
$repositoryFiles = Get-ChildItem (Join-Path $SolutionRoot "backend\src\AvenChart.Api\Data") -Filter '*Repository.cs' -File
$repositorySource = ($repositoryFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
if ([regex]::IsMatch($repositorySource, '(?i)\b(create\s+table|alter\s+table|create\s+(unique\s+)?index)\b')) {
    throw "Repository-time schema DDL was detected. Add schema changes to a versioned migration instead."
}
if ([regex]::IsMatch($repositorySource, '(?is)max\s*\([^)]*\)[^;\r\n]{0,120}\+\s*1\b')) {
    throw "A concurrency-unsafe MAX(...) + 1 allocator was detected in a repository."
}
foreach ($checkpoint in $FaultCheckpoints) {
    if ($checkpoint -lt 1 -or $checkpoint -ge $ExpectedMigrationCount) {
        throw "Fault checkpoint $checkpoint must be between 1 and $($ExpectedMigrationCount - 1)."
    }
}

$DatabaseName = "avenchart_test_$([Guid]::NewGuid().ToString('N'))"
$ApiContainerName = "avenchart-migration-test-$([Guid]::NewGuid().ToString('N'))"
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts\migration-resilience"
$ResultPath = Join-Path $ArtifactsRoot "latest-avenchart-migration-resilience.json"
$LocationPushed = $false
$DatabaseCreated = $false
$ApiStarted = $false
$CompletedScenarios = [System.Collections.Generic.List[string]]::new()

function Assert-TestDatabaseName {
    param([string]$Name)
    if ($Name -notmatch '^avenchart_test_[a-f0-9]{32}$') {
        throw "Refusing database operation for unexpected test database name '$Name'."
    }
}

function Invoke-DatabaseScalar {
    param([string]$Sql)

    Assert-TestDatabaseName -Name $DatabaseName
    $value = docker compose exec -T postgres psql -X -U avenchart -d $DatabaseName -t -A -v ON_ERROR_STOP=1 -c $Sql
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL scalar query failed for isolated database '$DatabaseName'."
    }
    return ($value | Select-Object -Last 1).Trim()
}

function Assert-LedgerCount {
    param([int]$Expected)

    $actual = [int](Invoke-DatabaseScalar -Sql "select count(*) from schema_migrations;")
    if ($actual -ne $Expected) {
        throw "Expected $Expected migration ledger rows after interruption, but found $actual."
    }
}

function Assert-AnchorChartData {
    $json = Invoke-DatabaseScalar -Sql @"
select json_build_object(
  'maritalStatus', p.marital_status,
  'occupation', p.occupation,
  'race', p.race,
  'ethnicity', p.ethnicity,
  'street', p.street,
  'providerName', concat_ws(' ', s.first_name, s.last_name),
  'insuranceCount', (select count(*) from insurance_records i where i.patient_id = p.canonical_id),
  'historyCount', (select count(*) from patient_histories h where h.patient_id = p.canonical_id)
)::text
from patients p
left join staff s on s.id = p.provider_id
where p.canonical_id = 'MOD-PAT-0001';
"@
    $facts = $json | ConvertFrom-Json
    if ($facts.maritalStatus -ne "married" -or
        $facts.occupation -ne "Retired" -or
        $facts.race -ne "White" -or
        $facts.ethnicity -ne "Not Hispanic or Latino" -or
        $facts.street -ne "101 Test Patient Avenue" -or
        [int]$facts.insuranceCount -lt 1 -or
        [int]$facts.historyCount -lt 1 -or
        [string]::IsNullOrWhiteSpace($facts.providerName)) {
        throw "The anchor patient chart data invariant failed after migration recovery."
    }
}

function Invoke-MigratorExpectingFailure {
    param([int]$FaultAfter = 0)

    $failed = $false
    try {
        & .\scripts\Invoke-AvenChartMigrations.ps1 `
            -SkipPostgresStartup `
            -SkipArtifact `
            -SkipImageBuild `
            -DatabaseName $DatabaseName `
            -TestFaultAfterAppliedMigrationCount $FaultAfter
    }
    catch {
        $failed = $true
        Write-Host "Observed expected migrator failure: $($_.Exception.Message)"
    }
    if (-not $failed) {
        throw "Expected the packaged migrator to fail, but it completed successfully."
    }
}

function Invoke-Http {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Body = "",
        [hashtable]$Headers = @{}
    )

    $parameters = @{
        Uri = "http://127.0.0.1:$ApiPort$Path"
        Method = $Method
        TimeoutSec = 15
        Headers = $Headers
    }
    if ((Get-Command Invoke-WebRequest).Parameters.ContainsKey("UseBasicParsing")) {
        $parameters.UseBasicParsing = $true
    }
    if ((Get-Command Invoke-WebRequest).Parameters.ContainsKey("SkipHttpErrorCheck")) {
        $parameters.SkipHttpErrorCheck = $true
    }
    if ($Body.Length -gt 0) {
        $parameters.Body = $Body
        $parameters.ContentType = "application/json"
    }
    try {
        return Invoke-WebRequest @parameters
    }
    catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) {
            throw
        }

        $stream = $errorResponse.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($stream)
        try {
            return [pscustomobject]@{
                StatusCode = [int]$errorResponse.StatusCode
                Content = $reader.ReadToEnd()
            }
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    }
}

function Assert-SchemaNotReadyResponse {
    param($Response)

    if ([int]$Response.StatusCode -ne 503) {
        throw "Expected HTTP 503, but received $($Response.StatusCode)."
    }
    $responseContent = Get-HttpResponseContent -Response $Response
    $problem = $responseContent | ConvertFrom-Json
    if ($problem.code -ne "schema_not_ready") {
        throw "Expected schema_not_ready problem code, but received '$($problem.code)'. Response body: $responseContent"
    }
}

function Get-HttpResponseContent {
    param($Response)

    if ($Response.Content -is [byte[]]) {
        return [System.Text.Encoding]::UTF8.GetString($Response.Content)
    }

    return [string]$Response.Content
}

function Wait-ForApiReady {
    $deadline = (Get-Date).AddSeconds($ApiWaitSeconds)
    do {
        try {
            $response = Invoke-Http -Method "GET" -Path "/health/ready"
            if ([int]$response.StatusCode -eq 200) {
                return
            }
        }
        catch {
            # The isolated API may still be starting.
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    throw "The isolated API did not become ready within $ApiWaitSeconds seconds."
}

try {
    Push-Location $SolutionRoot
    $LocationPushed = $true

    docker compose up -d postgres
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start PostgreSQL for migration resilience testing."
    }

    $postgresDeadline = (Get-Date).AddSeconds(90)
    do {
        docker compose exec -T postgres pg_isready -U avenchart -d avenchart *> $null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $postgresDeadline)
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL did not become ready for migration resilience testing."
    }

    # The migrator and API share avenchart-api:local; only the API service owns the build definition.
    docker compose build --quiet api
    if ($LASTEXITCODE -ne 0) {
        throw "Could not build the packaged migrator image for resilience testing."
    }

    Assert-TestDatabaseName -Name $DatabaseName
    docker compose exec -T postgres psql -X -U avenchart -d postgres -v ON_ERROR_STOP=1 -c "create database $DatabaseName owner avenchart;"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create isolated migration test database '$DatabaseName'."
    }
    $DatabaseCreated = $true

    foreach ($checkpoint in $FaultCheckpoints) {
        $faultObserved = $false
        try {
            & .\scripts\Seed-AvenChartGoldDataset.ps1 `
                -DatabaseName $DatabaseName `
                -TestFaultAfterAppliedMigrationCount $checkpoint `
                -SkipMigrationImageBuild `
                -SkipArtifact
        }
        catch {
            $faultObserved = $true
            Write-Host "Observed expected reset interruption at checkpoint $checkpoint."
        }
        if (-not $faultObserved) {
            throw "Synthetic reset fault checkpoint $checkpoint did not interrupt the migrator."
        }

        Assert-LedgerCount -Expected $checkpoint
        & .\scripts\Invoke-AvenChartMigrations.ps1 -SkipPostgresStartup -SkipArtifact -SkipImageBuild -DatabaseName $DatabaseName
        Assert-LedgerCount -Expected $ExpectedMigrationCount
        Assert-AnchorChartData
        $CompletedScenarios.Add("reset-interruption-$checkpoint")
    }

    & .\scripts\Invoke-AvenChartMigrations.ps1 -SkipPostgresStartup -SkipArtifact -SkipImageBuild -DatabaseName $DatabaseName
    Assert-LedgerCount -Expected $ExpectedMigrationCount
    $CompletedScenarios.Add("idempotent-no-op")

    $connectionString = "Host=postgres;Port=5432;Database=$DatabaseName;Username=avenchart;Password=avenchart_demo"
    docker compose run --detach --rm --no-deps --name $ApiContainerName -p "127.0.0.1:$($ApiPort):8080" -e "ConnectionStrings__AvenChart=$connectionString" api
    if ($LASTEXITCODE -ne 0) {
        throw "Could not start the isolated API container."
    }
    $ApiStarted = $true
    Wait-ForApiReady

    $lastMigrationId = (Get-ChildItem .\database\migrations -Filter '*.sql' -File | Sort-Object Name | Select-Object -Last 1).BaseName
    Invoke-DatabaseScalar -Sql "delete from schema_migrations where migration_id = '$lastMigrationId';" | Out-Null
    Start-Sleep -Seconds 2
    Assert-SchemaNotReadyResponse -Response (Invoke-Http -Method "POST" -Path "/api/auth/login" -Body '{"username":"admin","password":"pass"}')
    $readiness = Invoke-Http -Method "GET" -Path "/health/ready"
    if ([int]$readiness.StatusCode -ne 503) {
        throw "Readiness did not reject a missing packaged migration."
    }
    & .\scripts\Invoke-AvenChartMigrations.ps1 -SkipPostgresStartup -SkipArtifact -SkipImageBuild -DatabaseName $DatabaseName
    Wait-ForApiReady
    $CompletedScenarios.Add("missing-migration-request-gate")

    $lastMigrationFile = Get-ChildItem .\database\migrations -Filter "$lastMigrationId.sql" -File
    $lastChecksum = (Get-FileHash -LiteralPath $lastMigrationFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    Invoke-DatabaseScalar -Sql "update schema_migrations set checksum_sha256 = repeat('0', 64) where migration_id = '$lastMigrationId';" | Out-Null
    Start-Sleep -Seconds 2
    if ([int](Invoke-Http -Method "GET" -Path "/health/ready").StatusCode -ne 503) {
        throw "Readiness did not reject a migration checksum mismatch."
    }
    Invoke-MigratorExpectingFailure
    Invoke-DatabaseScalar -Sql "update schema_migrations set checksum_sha256 = '$lastChecksum' where migration_id = '$lastMigrationId';" | Out-Null
    Wait-ForApiReady
    $CompletedScenarios.Add("checksum-drift-rejection")

    Invoke-DatabaseScalar -Sql "insert into schema_migrations (migration_id, checksum_sha256, description, applied_at, applied_by) values ('V9999__unexpected_test', repeat('f', 64), 'isolated test row', now(), 'resilience-test');" | Out-Null
    Start-Sleep -Seconds 2
    if ([int](Invoke-Http -Method "GET" -Path "/health/ready").StatusCode -ne 503) {
        throw "Readiness did not reject an unexpected migration ledger row."
    }
    Invoke-MigratorExpectingFailure
    Invoke-DatabaseScalar -Sql "delete from schema_migrations where migration_id = 'V9999__unexpected_test';" | Out-Null
    Wait-ForApiReady
    $CompletedScenarios.Add("unexpected-migration-rejection")

    $login = (Invoke-Http -Method "POST" -Path "/api/auth/login" -Body '{"username":"admin","password":"pass"}').Content | ConvertFrom-Json
    if (-not $login.authenticated -or [string]::IsNullOrWhiteSpace($login.sessionId)) {
        throw "Could not establish the isolated API session required for schema-shape error testing."
    }
    $authenticatedHeaders = @{ "X-AvenChart-Session" = $login.sessionId }

    Invoke-DatabaseScalar -Sql "delete from avenchart_integer_counters where counter_key = 'test.atomic-integer-allocation';" | Out-Null
    $postgresContainerId = (docker compose ps -q postgres).Trim()
    if ([string]::IsNullOrWhiteSpace($postgresContainerId)) {
        throw "Could not resolve the PostgreSQL container for allocator concurrency testing."
    }
    $allocatorScript = Join-Path $SolutionRoot "scripts\sql\test-atomic-integer-allocation.sql"
    docker cp $allocatorScript "${postgresContainerId}:/tmp/test-atomic-integer-allocation.sql" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not stage the allocator concurrency script in PostgreSQL."
    }
    $allocatorClients = 16
    $allocatorTransactionsPerClient = 20
    docker compose exec -T postgres pgbench `
        -U avenchart `
        -d $DatabaseName `
        -n `
        -c $allocatorClients `
        -j 4 `
        -t $allocatorTransactionsPerClient `
        -f /tmp/test-atomic-integer-allocation.sql *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Parallel allocator execution failed."
    }
    $expectedAllocatedValue = $allocatorClients * $allocatorTransactionsPerClient
    $allocatedValue = [int](Invoke-DatabaseScalar -Sql "select current_value from avenchart_integer_counters where counter_key = 'test.atomic-integer-allocation';")
    if ($allocatedValue -ne $expectedAllocatedValue) {
        throw "Expected atomic allocator value $expectedAllocatedValue, but found $allocatedValue."
    }
    Invoke-DatabaseScalar -Sql "delete from avenchart_integer_counters where counter_key = 'test.atomic-integer-allocation';" | Out-Null
    $CompletedScenarios.Add("atomic-integer-allocation")

    $officeCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/office-notes/" `
        -Headers $authenticatedHeaders `
        -Body '{"body":"EF Core migration-resilience note"}'
    if ([int]$officeCreateResponse.StatusCode -ne 201) {
        throw "EF-backed office note creation returned HTTP $($officeCreateResponse.StatusCode)."
    }
    $officeCreate = (Get-HttpResponseContent -Response $officeCreateResponse) | ConvertFrom-Json
    $officeUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/office-notes/$($officeCreate.id)" `
        -Headers $authenticatedHeaders `
        -Body '{"body":"EF Core migration-resilience note updated"}'
    $officeInactiveResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/office-notes/$($officeCreate.id)/activity" `
        -Headers $authenticatedHeaders `
        -Body '{"active":false}'
    $officeInactiveListResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/office-notes/?activity=inactive" `
        -Headers $authenticatedHeaders
    if ([int]$officeUpdateResponse.StatusCode -ne 200 -or
        [int]$officeInactiveResponse.StatusCode -ne 200 -or
        [int]$officeInactiveListResponse.StatusCode -ne 200) {
        throw "EF-backed office note update, activity, or list operation failed."
    }
    $officeUpdate = (Get-HttpResponseContent -Response $officeUpdateResponse) | ConvertFrom-Json
    $officeInactive = (Get-HttpResponseContent -Response $officeInactiveResponse) | ConvertFrom-Json
    $officeInactiveList = (Get-HttpResponseContent -Response $officeInactiveListResponse) | ConvertFrom-Json
    if ($officeUpdate.body -ne "EF Core migration-resilience note updated" -or
        $officeInactive.active -ne $false -or
        $officeInactiveList.notes.id -notcontains $officeCreate.id) {
        throw "EF-backed office note CRUD returned unexpected data."
    }
    $officeDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/office-notes/$($officeCreate.id)" `
        -Headers $authenticatedHeaders
    if ([int]$officeDeleteResponse.StatusCode -ne 204) {
        throw "EF-backed office note deletion returned HTTP $($officeDeleteResponse.StatusCode)."
    }
    $officeAllResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/office-notes/?activity=all" `
        -Headers $authenticatedHeaders
    $officeAll = (Get-HttpResponseContent -Response $officeAllResponse) | ConvertFrom-Json
    if ($officeAll.notes.id -contains $officeCreate.id) {
        throw "Deleted EF-backed office note remained visible."
    }
    $CompletedScenarios.Add("ef-core-office-note-crud")

    $addressCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/administration/address-book/" `
        -Headers $authenticatedHeaders `
        -Body '{"organization":"EF Slice Directory","firstName":"Taylor","lastName":"Contact","specialty":"Care Coordination","npi":"1234567890","type":"external_provider","active":true}'
    if ([int]$addressCreateResponse.StatusCode -ne 201) {
        throw "EF-backed address-book creation returned HTTP $($addressCreateResponse.StatusCode)."
    }
    $addressCreate = (Get-HttpResponseContent -Response $addressCreateResponse) | ConvertFrom-Json
    $addressSearchResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/administration/address-book/?organization=EF%20Slice%20Directory" `
        -Headers $authenticatedHeaders
    $addressUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/administration/address-book/$($addressCreate.id)" `
        -Headers $authenticatedHeaders `
        -Body '{"organization":"EF Slice Directory","firstName":"Taylor","lastName":"Updated Contact","specialty":"Care Coordination","npi":"1234567890","type":"external_provider","active":true}'
    if ([int]$addressSearchResponse.StatusCode -ne 200 -or [int]$addressUpdateResponse.StatusCode -ne 200) {
        throw "EF-backed address-book search or update failed."
    }
    $addressSearch = (Get-HttpResponseContent -Response $addressSearchResponse) | ConvertFrom-Json
    $addressUpdate = (Get-HttpResponseContent -Response $addressUpdateResponse) | ConvertFrom-Json
    if ($addressSearch.entries.id -notcontains $addressCreate.id -or $addressUpdate.lastName -ne "Updated Contact") {
        throw "EF-backed address-book operations returned unexpected data."
    }
    $addressDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/administration/address-book/$($addressCreate.id)" `
        -Headers $authenticatedHeaders
    if ([int]$addressDeleteResponse.StatusCode -ne 204) {
        throw "EF-backed address-book deletion returned HTTP $($addressDeleteResponse.StatusCode)."
    }
    $CompletedScenarios.Add("ef-core-address-book-crud")

    $educationResourcesResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/patient-education/resources" `
        -Headers $authenticatedHeaders
    $educationSearchResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/patient-education/search" `
        -Headers $authenticatedHeaders `
        -Body '{"resourceKey":"medlineplus","searchText":"heart health"}'
    if ([int]$educationResourcesResponse.StatusCode -ne 200 -or [int]$educationSearchResponse.StatusCode -ne 200) {
        throw "EF-backed patient-education resource or search operation failed."
    }
    $educationResources = (Get-HttpResponseContent -Response $educationResourcesResponse) | ConvertFrom-Json
    $educationSearch = (Get-HttpResponseContent -Response $educationSearchResponse) | ConvertFrom-Json
    if ($educationResources.resources.key -notcontains "medlineplus" -or
        -not ([string]$educationSearch.url).StartsWith("https://", [StringComparison]::Ordinal)) {
        throw "EF-backed patient-education operations returned unexpected data."
    }
    $CompletedScenarios.Add("ef-core-patient-education-queries")

    $recallCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/recalls/" `
        -Headers $authenticatedHeaders `
        -Body '{"patientId":"MOD-PAT-0001","recallDate":"2026-12-01","reason":"EF slice outreach"}'
    if ([int]$recallCreateResponse.StatusCode -ne 201) {
        throw "EF-backed recall creation returned HTTP $($recallCreateResponse.StatusCode)."
    }
    $recall = (Get-HttpResponseContent -Response $recallCreateResponse) | ConvertFrom-Json
    $recallActivityResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/recalls/$($recall.id)/activity" `
        -Headers $authenticatedHeaders `
        -Body '{"activityType":"phone","note":"EF slice phone evidence"}'
    $recallHistoryResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/recalls/$($recall.id)/activity" `
        -Headers $authenticatedHeaders
    if ([int]$recallActivityResponse.StatusCode -ne 201 -or [int]$recallHistoryResponse.StatusCode -ne 200) {
        throw "EF-backed recall activity creation or history failed."
    }
    $recallActivity = (Get-HttpResponseContent -Response $recallActivityResponse) | ConvertFrom-Json
    $recallHistory = (Get-HttpResponseContent -Response $recallHistoryResponse) | ConvertFrom-Json
    if ($recallActivity.activityType -ne "phone" -or $recallHistory.id -notcontains $recallActivity.id) {
        throw "EF-backed recall operations returned unexpected data."
    }
    $recallDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/recalls/$($recall.id)" `
        -Headers $authenticatedHeaders
    if ([int]$recallDeleteResponse.StatusCode -ne 204) {
        throw "EF-backed recall deletion returned HTTP $($recallDeleteResponse.StatusCode)."
    }
    $CompletedScenarios.Add("ef-core-recall-aggregate")

    $trackerLookupResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/chart-tracker/lookup/MOD-PAT-0001" `
        -Headers $authenticatedHeaders
    $trackerOptionsResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/chart-tracker/options" `
        -Headers $authenticatedHeaders
    $trackerEventResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/chart-tracker/patients/MOD-PAT-0001/events" `
        -Headers $authenticatedHeaders `
        -Body '{"location":"Front Desk"}'
    if ([int]$trackerLookupResponse.StatusCode -ne 200 -or
        [int]$trackerOptionsResponse.StatusCode -ne 200 -or
        [int]$trackerEventResponse.StatusCode -ne 201) {
        throw "EF-backed chart-tracker lookup, options, or event creation failed."
    }
    $trackerLookup = (Get-HttpResponseContent -Response $trackerLookupResponse) | ConvertFrom-Json
    $trackerOptions = (Get-HttpResponseContent -Response $trackerOptionsResponse) | ConvertFrom-Json
    $trackerEvent = (Get-HttpResponseContent -Response $trackerEventResponse) | ConvertFrom-Json
    $trackerHistoryResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/chart-tracker/patients/MOD-PAT-0001/history" `
        -Headers $authenticatedHeaders
    $trackerHistory = (Get-HttpResponseContent -Response $trackerHistoryResponse) | ConvertFrom-Json
    if ($trackerLookup.patientId -ne "MOD-PAT-0001" -or
        $trackerOptions.locations -notcontains "Front Desk" -or
        $trackerEvent.location -ne "Front Desk" -or
        $trackerHistory.id -notcontains $trackerEvent.id) {
        throw "EF-backed chart-tracker operations returned unexpected data."
    }
    $CompletedScenarios.Add("ef-core-chart-tracker-aggregate")

    $recordRequestCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/patients/MOD-PAT-0010/record-requests" `
        -Headers $authenticatedHeaders `
        -Body '{}'
    if ([int]$recordRequestCreateResponse.StatusCode -ne 201) {
        throw "EF-backed patient record-request creation returned HTTP $($recordRequestCreateResponse.StatusCode)."
    }
    $recordRequest = (Get-HttpResponseContent -Response $recordRequestCreateResponse) | ConvertFrom-Json
    $duplicateRecordRequestResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/patients/MOD-PAT-0010/record-requests" `
        -Headers $authenticatedHeaders `
        -Body '{}'
    if ([int]$duplicateRecordRequestResponse.StatusCode -ne 400) {
        throw "The EF-backed open record-request uniqueness rule was not enforced."
    }
    $recordRequestCompleteResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/patients/MOD-PAT-0010/record-requests/$($recordRequest.requestId)/complete" `
        -Headers $authenticatedHeaders `
        -Body '{}'
    $recordRequestHistoryResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/patients/MOD-PAT-0010/record-requests" `
        -Headers $authenticatedHeaders
    if ([int]$recordRequestCompleteResponse.StatusCode -ne 200 -or [int]$recordRequestHistoryResponse.StatusCode -ne 200) {
        throw "EF-backed patient record-request completion or history failed."
    }
    $recordRequestComplete = (Get-HttpResponseContent -Response $recordRequestCompleteResponse) | ConvertFrom-Json
    $recordRequestHistory = (Get-HttpResponseContent -Response $recordRequestHistoryResponse) | ConvertFrom-Json
    $recordRequestVersion = [int](Invoke-DatabaseScalar -Sql "select row_version from patient_record_requests where request_id = '$($recordRequest.requestId)';")
    if ($recordRequestComplete.status -ne "Completed" -or
        $recordRequestHistory.requestId -notcontains $recordRequest.requestId -or
        $recordRequestVersion -ne 2) {
        throw "EF-backed patient record-request operations returned unexpected data or concurrency state."
    }
    $CompletedScenarios.Add("ef-core-patient-record-request-concurrency")

    $sdohCreateBody = @{
        assessmentDate = "2026-07-25"
        screeningTool = "Hunger Vital Signs"
        assessor = ""
        domains = @{
            transportation_insecurity = @{
                status = "sometimes"
                notes = "Needs transport support"
            }
        }
        hungerQuestionOne = "LA28397-0"
        hungerQuestionTwo = "LA6729-3"
        disabilityScale = @{}
        interventions = "Provide transportation resources."
    } | ConvertTo-Json -Depth 8
    $sdohCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/patients/MOD-PAT-0010/sdoh-assessments" `
        -Headers $authenticatedHeaders `
        -Body $sdohCreateBody
    if ([int]$sdohCreateResponse.StatusCode -ne 201) {
        throw "EF-backed SDOH creation returned HTTP $($sdohCreateResponse.StatusCode). Body: $(Get-HttpResponseContent -Response $sdohCreateResponse)"
    }
    $sdohCreate = (Get-HttpResponseContent -Response $sdohCreateResponse) | ConvertFrom-Json
    $sdohUpdateBody = @{
        assessmentDate = "2026-07-25"
        screeningTool = "Hunger Vital Signs"
        assessor = "EF assessor"
        domains = @{
            transportation_insecurity = @{
                status = "present"
                notes = "Transport plan active"
            }
        }
        hungerQuestionOne = "LA6729-3"
        hungerQuestionTwo = "LA6729-3"
        disabilityScale = @{}
        interventions = "Transportation resources retained."
    } | ConvertTo-Json -Depth 8
    $sdohUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/patients/MOD-PAT-0010/sdoh-assessments/$($sdohCreate.assessmentId)" `
        -Headers $authenticatedHeaders `
        -Body $sdohUpdateBody
    $sdohHistoryResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/patients/MOD-PAT-0010/sdoh-assessments" `
        -Headers $authenticatedHeaders
    if ([int]$sdohUpdateResponse.StatusCode -ne 200 -or [int]$sdohHistoryResponse.StatusCode -ne 200) {
        throw "EF-backed SDOH update or history failed."
    }
    $sdohUpdate = (Get-HttpResponseContent -Response $sdohUpdateResponse) | ConvertFrom-Json
    $sdohHistory = (Get-HttpResponseContent -Response $sdohHistoryResponse) | ConvertFrom-Json
    $sdohVersion = [int](Invoke-DatabaseScalar -Sql "select row_version from patient_sdoh_assessments where assessment_id = '$($sdohCreate.assessmentId)';")
    if ($sdohCreate.assessor -ne "admin" -or
        $sdohUpdate.assessor -ne "EF assessor" -or
        $sdohUpdate.domains.transportation_insecurity.status -ne "present" -or
        $sdohHistory.assessmentId -notcontains $sdohCreate.assessmentId -or
        $sdohVersion -ne 2) {
        throw "EF-backed SDOH operations returned unexpected data or concurrency state."
    }
    $CompletedScenarios.Add("ef-core-patient-sdoh-concurrency")

    $therapyGroupCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/therapy-groups/" `
        -Headers $authenticatedHeaders `
        -Body '{"name":"EF Core resilience group","facilitatorId":null,"description":"Therapy aggregate regression coverage","capacity":4}'
    if ([int]$therapyGroupCreateResponse.StatusCode -ne 201) {
        throw "EF-backed therapy-group creation returned HTTP $($therapyGroupCreateResponse.StatusCode). Body: $(Get-HttpResponseContent -Response $therapyGroupCreateResponse)"
    }
    $therapyGroup = (Get-HttpResponseContent -Response $therapyGroupCreateResponse) | ConvertFrom-Json
    $therapyMemberResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/therapy-groups/$($therapyGroup.id)/members" `
        -Headers $authenticatedHeaders `
        -Body '{"patientId":"MOD-PAT-0001"}'
    $therapySessionResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/therapy-groups/$($therapyGroup.id)/sessions" `
        -Headers $authenticatedHeaders `
        -Body '{"startsAt":"2026-12-15T15:00:00Z","durationMinutes":60,"topic":"EF Core aggregate verification"}'
    if ([int]$therapyMemberResponse.StatusCode -ne 201 -or [int]$therapySessionResponse.StatusCode -ne 201) {
        throw "EF-backed therapy-group membership or session creation failed."
    }
    $therapyMember = (Get-HttpResponseContent -Response $therapyMemberResponse) | ConvertFrom-Json
    $therapySession = (Get-HttpResponseContent -Response $therapySessionResponse) | ConvertFrom-Json
    $therapyAttendanceResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/therapy-groups/$($therapyGroup.id)/sessions/$($therapySession.id)/attendance/MOD-PAT-0001" `
        -Headers $authenticatedHeaders `
        -Body '{"status":"present","note":"EF aggregate test attendance"}'
    $therapyCompletionResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/therapy-groups/$($therapyGroup.id)/sessions/$($therapySession.id)/status" `
        -Headers $authenticatedHeaders `
        -Body '{"status":"completed"}'
    $therapyAttendanceListResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/therapy-groups/$($therapyGroup.id)/sessions/$($therapySession.id)/attendance" `
        -Headers $authenticatedHeaders
    if ([int]$therapyAttendanceResponse.StatusCode -ne 200 -or
        [int]$therapyCompletionResponse.StatusCode -ne 200 -or
        [int]$therapyAttendanceListResponse.StatusCode -ne 200) {
        throw "EF-backed therapy-group attendance or completion failed."
    }
    $therapyAttendance = (Get-HttpResponseContent -Response $therapyAttendanceResponse) | ConvertFrom-Json
    $therapyCompletion = (Get-HttpResponseContent -Response $therapyCompletionResponse) | ConvertFrom-Json
    $therapyAttendanceList = (Get-HttpResponseContent -Response $therapyAttendanceListResponse) | ConvertFrom-Json
    $therapyParticipantCount = [int](Invoke-DatabaseScalar -Sql "select count(*) from therapy_group_session_participants where session_id = '$($therapySession.id)' and patient_id = 'MOD-PAT-0001';")
    if ($therapyMember.patientId -ne "MOD-PAT-0001" -or
        $therapyAttendance.status -ne "present" -or
        $therapyCompletion.status -ne "completed" -or
        $therapyAttendanceList.attendance.patientId -notcontains "MOD-PAT-0001" -or
        $therapyParticipantCount -ne 1) {
        throw "EF-backed therapy-group aggregate returned unexpected state."
    }
    $CompletedScenarios.Add("ef-core-therapy-group-aggregate")

    $referralCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/patients/MOD-PAT-0001/referrals" `
        -Headers $authenticatedHeaders `
        -Body '{"encounterId":null,"destination":"EF Core referral destination","reason":"Referral aggregate regression coverage","externalReference":"EF-REF-001","notes":"Created by the migration-resilience suite","requestedAt":"2026-08-20T12:00:00Z","assignedTo":"admin","dueAt":"2026-12-20T12:00:00Z","workflowReason":"Verify EF-backed referral creation."}'
    if ([int]$referralCreateResponse.StatusCode -ne 201) {
        throw "EF-backed referral creation returned HTTP $($referralCreateResponse.StatusCode). Body: $(Get-HttpResponseContent -Response $referralCreateResponse)"
    }
    $referral = (Get-HttpResponseContent -Response $referralCreateResponse) | ConvertFrom-Json
    $referralAssignmentResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/patients/MOD-PAT-0001/referrals/$($referral.id)/assignment" `
        -Headers $authenticatedHeaders `
        -Body '{"assignedTo":"gold-provider-01","dueAt":"2026-12-22T12:00:00Z","expectedVersion":1,"reasonCode":"responsibility-transfer","reason":"Route to the responsible provider."}'
    if ([int]$referralAssignmentResponse.StatusCode -ne 200) {
        throw "EF-backed referral reassignment returned HTTP $($referralAssignmentResponse.StatusCode). Body: $(Get-HttpResponseContent -Response $referralAssignmentResponse)"
    }
    $referralAssignment = (Get-HttpResponseContent -Response $referralAssignmentResponse) | ConvertFrom-Json
    $referralStaleResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/patients/MOD-PAT-0001/referrals/$($referral.id)/status" `
        -Headers $authenticatedHeaders `
        -Body '{"status":"sent","expectedVersion":1,"reasonCode":"referral-sent","reason":"Deliberately stale transition."}'
    if ([int]$referralStaleResponse.StatusCode -ne 409) {
        throw "The EF-backed referral workflow accepted a stale version."
    }
    $referralStatusResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/patients/MOD-PAT-0001/referrals/$($referral.id)/status" `
        -Headers $authenticatedHeaders `
        -Body '{"status":"sent","expectedVersion":2,"reasonCode":"referral-sent","reason":"Send the verified referral."}'
    $referralHistoryResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/patients/MOD-PAT-0001/referrals/$($referral.id)/history" `
        -Headers $authenticatedHeaders
    $referralQueueResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/clinical-workflows/referral-work-queue?assignedTo=gold-provider-01&query=EF%20Core%20referral%20destination" `
        -Headers $authenticatedHeaders
    if ([int]$referralStatusResponse.StatusCode -ne 200 -or
        [int]$referralHistoryResponse.StatusCode -ne 200 -or
        [int]$referralQueueResponse.StatusCode -ne 200) {
        throw "EF-backed referral transition, history, or retained SQL work queue failed."
    }
    $referralStatus = (Get-HttpResponseContent -Response $referralStatusResponse) | ConvertFrom-Json
    $referralHistory = (Get-HttpResponseContent -Response $referralHistoryResponse) | ConvertFrom-Json
    $referralQueue = (Get-HttpResponseContent -Response $referralQueueResponse) | ConvertFrom-Json
    $referralDatabaseVersion = [int](Invoke-DatabaseScalar -Sql "select workflow_version from referrals where id = '$($referral.id)';")
    $referralEventCount = [int](Invoke-DatabaseScalar -Sql "select count(*) from clinical_workflow_events where workflow_type = 'patient-referral' and entity_id = '$($referral.id)';")
    if ($referralAssignment.assignedTo -ne "gold-provider-01" -or
        $referralAssignment.assignedDisplayName -ne "Alex Walker" -or
        $referralStatus.status -ne "sent" -or
        $referralStatus.workflowVersion -ne 3 -or
        $referralHistory.total -ne 3 -or
        $referralQueue.items.referral.id -notcontains $referral.id -or
        $referralDatabaseVersion -ne 3 -or
        $referralEventCount -ne 3) {
        throw "EF-backed referral aggregate returned unexpected state."
    }
    $CompletedScenarios.Add("ef-core-referral-workflow-concurrency")

    $templateCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/administration/document-templates/" `
        -Headers $authenticatedHeaders `
        -Body '{"name":"TMP-DOC-TEMPLATE-EF-RESILIENCE","content":"Hello ***NAME***, DOB ***DOB***, ID ***PATIENT_ID***","active":true}'
    if ([int]$templateCreateResponse.StatusCode -ne 201) {
        throw "EF-backed document-template creation returned HTTP $($templateCreateResponse.StatusCode). Body: $(Get-HttpResponseContent -Response $templateCreateResponse)"
    }
    $template = (Get-HttpResponseContent -Response $templateCreateResponse) | ConvertFrom-Json
    $templateListResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/administration/document-templates/?search=TMP-DOC-TEMPLATE-EF-RESILIENCE&includeInactive=true" `
        -Headers $authenticatedHeaders
    $templateRenderResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/administration/document-templates/$($template.id)/render" `
        -Headers $authenticatedHeaders `
        -Body '{"patientId":"MOD-PAT-0001"}'
    $templateBinaryResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/administration/document-templates/$($template.id)/binary-versions" `
        -Headers $authenticatedHeaders `
        -Body '{"fileName":"ef-resilience.txt","mimetype":"text/plain","contentBase64":"RUYgQ29yZSBiaW5hcnkgdGVtcGxhdGU="}'
    if ([int]$templateListResponse.StatusCode -ne 200 -or
        [int]$templateRenderResponse.StatusCode -ne 200 -or
        [int]$templateBinaryResponse.StatusCode -ne 201) {
        throw "EF-backed document-template list, render, or binary-version creation failed."
    }
    $templateList = (Get-HttpResponseContent -Response $templateListResponse) | ConvertFrom-Json
    $templateRender = (Get-HttpResponseContent -Response $templateRenderResponse) | ConvertFrom-Json
    $templateBinary = (Get-HttpResponseContent -Response $templateBinaryResponse) | ConvertFrom-Json
    $templateVersionsResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/administration/document-templates/$($template.id)/binary-versions" `
        -Headers $authenticatedHeaders
    $templateDownloadResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/administration/document-templates/$($template.id)/binary-versions/$($templateBinary.id)/download" `
        -Headers $authenticatedHeaders
    $templateRetireResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/administration/document-templates/$($template.id)" `
        -Headers $authenticatedHeaders `
        -Body '{"name":"TMP-DOC-TEMPLATE-EF-RESILIENCE","content":"Retired ***NAME***","active":false}'
    $templateHistoryResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/administration/document-templates/$($template.id)/history" `
        -Headers $authenticatedHeaders
    if ([int]$templateVersionsResponse.StatusCode -ne 200 -or
        [int]$templateDownloadResponse.StatusCode -ne 200 -or
        [int]$templateRetireResponse.StatusCode -ne 200 -or
        [int]$templateHistoryResponse.StatusCode -ne 200) {
        throw "EF-backed document-template version, download, update, or history failed."
    }
    $templateVersions = (Get-HttpResponseContent -Response $templateVersionsResponse) | ConvertFrom-Json
    $templateRetire = (Get-HttpResponseContent -Response $templateRetireResponse) | ConvertFrom-Json
    $templateHistory = (Get-HttpResponseContent -Response $templateHistoryResponse) | ConvertFrom-Json
    $templateRowVersion = [int](Invoke-DatabaseScalar -Sql "select row_version from document_templates where id = '$($template.id)';")
    if ($templateList.items.id -notcontains $template.id -or
        $templateRender.content -notlike "Hello Avery Stone*MOD-PAT-0001" -or
        $templateBinary.version -ne 1 -or
        $templateVersions.id -notcontains $templateBinary.id -or
        (Get-HttpResponseContent -Response $templateDownloadResponse) -ne "EF Core binary template" -or
        $templateRetire.active -ne $false -or
        $templateHistory.eventCount -ne 3 -or
        $templateHistory.events.action -notcontains "binary-version-uploaded" -or
        $templateRowVersion -ne 2) {
        throw "EF-backed document-template aggregate returned unexpected state."
    }
    $templateDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/administration/document-templates/$($template.id)/test-fixture" `
        -Headers $authenticatedHeaders
    if ([int]$templateDeleteResponse.StatusCode -ne 204) {
        throw "EF-backed document-template fixture cleanup returned HTTP $($templateDeleteResponse.StatusCode)."
    }
    $CompletedScenarios.Add("ef-core-document-template-aggregate")

    $messageResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/messages/MOD-PAT-0001" `
        -Headers $authenticatedHeaders
    if ([int]$messageResponse.StatusCode -ne 200) {
        throw "Patient messages without includeArchived should default to active-only, but returned HTTP $($messageResponse.StatusCode)."
    }
    $messagePayload = (Get-HttpResponseContent -Response $messageResponse) | ConvertFrom-Json
    if ($messagePayload.patientId -ne "MOD-PAT-0001") {
        throw "Patient messages default-query response did not preserve the requested patient identity."
    }
    $CompletedScenarios.Add("message-active-only-default-query")

    Wait-ForApiReady
    Invoke-DatabaseScalar -Sql "alter table patients rename column marital_status to marital_status_fault;" | Out-Null
    $chartResponse = Invoke-Http -Method "GET" -Path "/api/patients/MOD-PAT-0001" -Headers $authenticatedHeaders
    Assert-SchemaNotReadyResponse -Response $chartResponse
    $CompletedScenarios.Add("undefined-column-mapped-to-503")

    New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null
    [ordered]@{
        status = "passed"
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        expectedMigrationCount = $ExpectedMigrationCount
        faultCheckpoints = $FaultCheckpoints
        scenarios = $CompletedScenarios
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
    Write-Host "AvenChart migration resilience verification passed: $ResultPath"
}
finally {
    if ($ApiStarted) {
        docker rm -f $ApiContainerName *> $null
    }
    if ($DatabaseCreated) {
        Assert-TestDatabaseName -Name $DatabaseName
        docker compose exec -T postgres psql -X -U avenchart -d postgres -v ON_ERROR_STOP=1 -c "drop database if exists $DatabaseName with (force);" *> $null
    }
    if ($LocationPushed) {
        Pop-Location
    }
}
