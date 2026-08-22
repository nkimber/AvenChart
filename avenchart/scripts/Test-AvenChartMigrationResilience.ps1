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
$persistenceRoot = Join-Path $SolutionRoot "backend\src\AvenChart.Api\Persistence"
$entityFiles = Get-ChildItem (Join-Path $persistenceRoot "Entities") -Filter '*.cs' -File
$configurationFiles = Get-ChildItem (Join-Path $persistenceRoot "Configurations") -Filter '*Configuration.cs' -File
$dbContextSource = Get-Content (Join-Path $persistenceRoot "AvenChartDbContext.cs") -Raw
if ([regex]::IsMatch($repositorySource, '(?i)\b(create\s+table|alter\s+table|create\s+(unique\s+)?index)\b')) {
    throw "Repository-time schema DDL was detected. Add schema changes to a versioned migration instead."
}
if ([regex]::IsMatch($repositorySource, '(?is)max\s*\([^)]*\)[^;\r\n]{0,120}\+\s*1\b')) {
    throw "A concurrency-unsafe MAX(...) + 1 allocator was detected in a repository."
}
if ([regex]::IsMatch($repositorySource, "(?i)avenchart_next_integer\s*\(\s*'")) {
    throw "A global literal-key integer allocator was detected. Use a database-owned sequence; reserve avenchart_next_integer for aggregate-scoped counters."
}
$configurationWithoutExclusion = @($configurationFiles | Where-Object {
    (Get-Content -LiteralPath $_.FullName -Raw) -notmatch 'ExcludeFromMigrations\s*\('
})
if ($configurationWithoutExclusion.Count -gt 0) {
    throw "EF mapping(s) missing ExcludeFromMigrations: $($configurationWithoutExclusion.Name -join ', ')."
}
$entityNames = @($entityFiles | ForEach-Object {
    $match = [regex]::Match((Get-Content -LiteralPath $_.FullName -Raw), 'public\s+sealed\s+class\s+(?<name>\w+)')
    if ($match.Success) { $match.Groups['name'].Value }
} | Sort-Object -Unique)
$configuredEntityNames = @($configurationFiles | ForEach-Object {
    $match = [regex]::Match((Get-Content -LiteralPath $_.FullName -Raw), 'IEntityTypeConfiguration<(?<name>\w+)>')
    if ($match.Success) { $match.Groups['name'].Value }
} | Sort-Object -Unique)
$dbSetEntityNames = @([regex]::Matches($dbContextSource, 'DbSet<(?<name>\w+)>') | ForEach-Object {
    $_.Groups['name'].Value
} | Sort-Object -Unique)
$unconfiguredEntities = @($entityNames | Where-Object { $_ -notin $configuredEntityNames })
$entitiesWithoutDbSets = @($entityNames | Where-Object { $_ -notin $dbSetEntityNames })
if ($unconfiguredEntities.Count -gt 0 -or $entitiesWithoutDbSets.Count -gt 0) {
    throw "EF persistence coverage is incomplete. Missing configuration: $($unconfiguredEntities -join ', '); missing DbSet: $($entitiesWithoutDbSets -join ', ')."
}
$apiSource = (Get-ChildItem (Join-Path $SolutionRoot "backend\src\AvenChart.Api") -Filter '*.cs' -File -Recurse | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw
}) -join "`n"
if ([regex]::IsMatch($apiSource, '(?i)Database\.(Migrate|EnsureCreated)\s*\(')) {
    throw "Runtime EF schema creation was detected. Use the versioned SQL migration catalog."
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
$VerificationPassed = $false
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

    $globalIdentityDefaultCount = [int](Invoke-DatabaseScalar -Sql @"
select count(*)
from (values
  ('patient_documents', 'id', 'patient_documents_id_seq'),
  ('payment_sessions', 'id', 'payment_sessions_id_seq'),
  ('encounters', 'id', 'encounters_id_seq'),
  ('encounter_signatures', 'id', 'encounter_signatures_id_seq'),
  ('portal_mailbox_messages', 'id', 'portal_mailbox_messages_id_seq'),
  ('patients', 'legacy_pid', 'patients_legacy_pid_seq'),
  ('lab_orders', 'id', 'lab_orders_id_seq'),
  ('lab_reports', 'id', 'lab_reports_id_seq'),
  ('lab_results', 'id', 'lab_results_id_seq'),
  ('lab_specimens', 'id', 'lab_specimens_id_seq')
) expected(table_name, column_name, sequence_name)
join information_schema.columns actual
  on actual.table_schema = 'public'
 and actual.table_name = expected.table_name
 and actual.column_name = expected.column_name
where actual.column_default = 'nextval(''' || expected.sequence_name || '''::regclass)';
"@)
    if ($globalIdentityDefaultCount -ne 10) {
        throw "Expected 10 global integer identities to use database-owned sequence defaults, but found $globalIdentityDefaultCount."
    }
    $CompletedScenarios.Add("database-owned-global-identities")

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

    $adminFacilityCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/administration/facilities" `
        -Headers $authenticatedHeaders `
        -Body '{"code":"EFADM","name":"EF Administration Facility","phone":"555-0199","street":"10 EF Lane","city":"Boston","state":"MA","postalCode":"02108","color":"#246b73","active":true}'
    if ([int]$adminFacilityCreateResponse.StatusCode -ne 201) {
        throw "EF-backed administration facility creation returned HTTP $($adminFacilityCreateResponse.StatusCode). Body: $(Get-HttpResponseContent -Response $adminFacilityCreateResponse)"
    }
    $adminFacility = (Get-HttpResponseContent -Response $adminFacilityCreateResponse) | ConvertFrom-Json
    $adminUserBody = @{
        username = "ef-admin-resilience"
        firstName = "Entity"
        lastName = "Framework"
        role = "frontdesk"
        calendar = $false
        facilityId = $adminFacility.id
        email = "ef-admin-resilience@example.test"
        npi = ""
        active = $true
    } | ConvertTo-Json
    $adminUserCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/administration/users" `
        -Headers $authenticatedHeaders `
        -Body $adminUserBody
    if ([int]$adminUserCreateResponse.StatusCode -ne 201) {
        throw "EF-backed administration user creation returned HTTP $($adminUserCreateResponse.StatusCode). Body: $(Get-HttpResponseContent -Response $adminUserCreateResponse)"
    }
    $adminUser = (Get-HttpResponseContent -Response $adminUserCreateResponse) | ConvertFrom-Json
    $adminMembershipResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/administration/access-control/user-memberships" `
        -Headers $authenticatedHeaders `
        -Body '{"userValue":"ef-admin-resilience","groupValue":"front"}'
    $adminPermissionResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/administration/access-control/group-permissions" `
        -Headers $authenticatedHeaders `
        -Body '{"groupValue":"front","sectionValue":"patients","permissionValue":"demo","returnValue":"write"}'
    if ([int]$adminMembershipResponse.StatusCode -ne 200 -or
        [int]$adminPermissionResponse.StatusCode -ne 200) {
        throw "EF-backed administration access-control mutation failed."
    }
    $adminMembership = (Get-HttpResponseContent -Response $adminMembershipResponse) | ConvertFrom-Json
    $adminPermission = (Get-HttpResponseContent -Response $adminPermissionResponse) | ConvertFrom-Json
    $adminUserUpdateBody = @{
        username = "ef-admin-resilience"
        firstName = "Entity"
        lastName = "Framework Updated"
        role = "frontdesk"
        calendar = $false
        facilityId = $adminFacility.id
        email = "ef-admin-resilience@example.test"
        npi = ""
        active = $false
    } | ConvertTo-Json
    $adminUserUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/administration/users/$($adminUser.id)" `
        -Headers $authenticatedHeaders `
        -Body $adminUserUpdateBody
    $adminFacilityUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/administration/facilities/$($adminFacility.id)" `
        -Headers $authenticatedHeaders `
        -Body '{"code":"EFADM","name":"EF Administration Facility Updated","phone":"555-0199","street":"10 EF Lane","city":"Boston","state":"MA","postalCode":"02108","color":"#246b73","active":false}'
    if ([int]$adminUserUpdateResponse.StatusCode -ne 200 -or
        [int]$adminFacilityUpdateResponse.StatusCode -ne 200) {
        throw "EF-backed administration user or facility update failed."
    }
    $adminUserUpdate = (Get-HttpResponseContent -Response $adminUserUpdateResponse) | ConvertFrom-Json
    $adminFacilityUpdate = (Get-HttpResponseContent -Response $adminFacilityUpdateResponse) | ConvertFrom-Json
    $staffDefault = Invoke-DatabaseScalar -Sql "select column_default from information_schema.columns where table_name = 'staff' and column_name = 'id';"
    $facilityDefault = Invoke-DatabaseScalar -Sql "select column_default from information_schema.columns where table_name = 'facilities' and column_name = 'id';"
    if ($adminMembership.detail.accessControl.userMemberships.userValue -notcontains "ef-admin-resilience" -or
        $adminPermission.detail.accessControl.groupPermissions.returnValue -notcontains "write" -or
        $adminUserUpdate.detail.users.lastName -notcontains "Framework Updated" -or
        $adminFacilityUpdate.detail.facilities.name -notcontains "EF Administration Facility Updated" -or
        $staffDefault -notlike "nextval*staff_id_seq*" -or
        $facilityDefault -notlike "nextval*facilities_id_seq*") {
        throw "EF-backed administration aggregate or sequence defaults returned unexpected state."
    }
    $adminMembershipDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/administration/access-control/user-memberships/ef-admin-resilience/front" `
        -Headers $authenticatedHeaders
    $adminUserDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/administration/users/$($adminUser.id)" `
        -Headers $authenticatedHeaders
    $adminFacilityDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/administration/facilities/$($adminFacility.id)" `
        -Headers $authenticatedHeaders
    if ([int]$adminMembershipDeleteResponse.StatusCode -ne 200 -or
        [int]$adminUserDeleteResponse.StatusCode -ne 204 -or
        [int]$adminFacilityDeleteResponse.StatusCode -ne 204) {
        throw "EF-backed administration cleanup failed."
    }
    $CompletedScenarios.Add("ef-core-administration-directory-mutations")

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
    $recallCloseResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/recalls/$($recall.id)/close" `
        -Headers $authenticatedHeaders `
        -Body '{"status":"completed","reason":"EF slice retained closure evidence"}'
    $closedRecallsResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/recalls/?includeClosed=true" `
        -Headers $authenticatedHeaders
    if ([int]$recallCloseResponse.StatusCode -ne 200 -or [int]$closedRecallsResponse.StatusCode -ne 200) {
        throw "EF-backed recall closure or retained-history listing failed."
    }
    $closedRecall = (Get-HttpResponseContent -Response $recallCloseResponse) | ConvertFrom-Json
    $closedRecalls = @((Get-HttpResponseContent -Response $closedRecallsResponse) | ConvertFrom-Json)
    if ($closedRecall.status -ne "completed" -or [string]::IsNullOrWhiteSpace($closedRecall.closedBy) -or $closedRecalls.id -notcontains $recall.id) {
        throw "EF-backed recall closure did not retain the recall evidence."
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

    $encounterCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/encounters/" `
        -Headers $authenticatedHeaders `
        -Body '{"patientId":"MOD-PAT-0001","providerId":101,"dateTime":"2026-08-20T10:00:00","reason":"EF encounter-state regression","facilityId":10,"billingFacilityId":10,"sensitivity":"normal","referralSource":"migration-resilience","externalId":"EF-ENC-STATE","posCode":11,"billingNote":"Initial EF state","sourceAppointmentId":null}'
    if ([int]$encounterCreateResponse.StatusCode -ne 201) {
        throw "Encounter creation for the EF state slice returned HTTP $($encounterCreateResponse.StatusCode). Body: $(Get-HttpResponseContent -Response $encounterCreateResponse)"
    }
    $encounterState = (Get-HttpResponseContent -Response $encounterCreateResponse) | ConvertFrom-Json
    $encounterUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/encounters/$($encounterState.encounter)" `
        -Headers $authenticatedHeaders `
        -Body '{"reason":"EF encounter state updated","sensitivity":"restricted","referralSource":"EF regression","externalId":"EF-ENC-STATE-UPDATED","posCode":12,"billingNote":"Updated atomically with audit"}'
    $encounterVitalsResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/encounters/$($encounterState.encounter)/vitals" `
        -Headers $authenticatedHeaders `
        -Body '{"dateTime":"2026-08-20T10:15:00","systolic":122,"diastolic":78,"weight":170,"height":70,"temperature":98.6,"pulse":72,"respiration":16,"oxygenSaturation":99,"note":"EF vital"}'
    if ([int]$encounterUpdateResponse.StatusCode -ne 200 -or
        [int]$encounterVitalsResponse.StatusCode -ne 201) {
        throw "EF-backed encounter summary or vitals mutation failed. Summary HTTP $($encounterUpdateResponse.StatusCode): $(Get-HttpResponseContent -Response $encounterUpdateResponse) Vitals HTTP $($encounterVitalsResponse.StatusCode): $(Get-HttpResponseContent -Response $encounterVitalsResponse)"
    }
    $encounterUpdate = (Get-HttpResponseContent -Response $encounterUpdateResponse) | ConvertFrom-Json
    $encounterVitals = (Get-HttpResponseContent -Response $encounterVitalsResponse) | ConvertFrom-Json
    $encounterArchiveResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/encounters/$($encounterState.encounter)/archive" `
        -Headers $authenticatedHeaders `
        -Body '{"reason":"Verify EF archive transition","expectedArchiveVersion":1}'
    $encounterRestoreResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/encounters/$($encounterState.encounter)/restore" `
        -Headers $authenticatedHeaders `
        -Body '{"reason":"Verify EF restore transition","expectedArchiveVersion":2}'
    $encounterAuditResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/encounters/$($encounterState.encounter)/audit" `
        -Headers $authenticatedHeaders
    if ([int]$encounterArchiveResponse.StatusCode -ne 204 -or
        [int]$encounterRestoreResponse.StatusCode -ne 204 -or
        [int]$encounterAuditResponse.StatusCode -ne 200) {
        throw "EF-backed encounter archive, restore, or audit history failed."
    }
    $encounterAudit = (Get-HttpResponseContent -Response $encounterAuditResponse) | ConvertFrom-Json
    $encounterVersions = (Invoke-DatabaseScalar -Sql "select row_version || ':' || archive_version from encounters where encounter = $($encounterState.encounter);").Split(':')
    $vitalsDefault = Invoke-DatabaseScalar -Sql "select column_default from information_schema.columns where table_name = 'vitals' and column_name = 'id';"
    if ($encounterUpdate.reason -ne "EF encounter state updated" -or
        $encounterVitals.id -le 0 -or
        $encounterVitals.detail.vitals.systolic -ne 122 -or
        $encounterAudit.events.action -notcontains "summary-updated" -or
        $encounterAudit.events.action -notcontains "archived" -or
        $encounterAudit.events.action -notcontains "restored" -or
        [int]$encounterVersions[0] -ne 4 -or
        [int]$encounterVersions[1] -ne 3 -or
        $vitalsDefault -notlike "nextval*vitals_id_seq*") {
        throw "EF-backed encounter state returned unexpected data, versions, or sequence defaults."
    }
    $CompletedScenarios.Add("ef-core-encounter-state-and-vitals")

    $allergyCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/clinical-lists/allergies" `
        -Headers $authenticatedHeaders `
        -Body '{"patientId":"MOD-PAT-0001","title":"EF test allergen","dateTime":"2026-08-20","comments":"EF allergy state","reaction":"rash","severity":"mild","listOptionId":null}'
    $problemCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/clinical-lists/problems" `
        -Headers $authenticatedHeaders `
        -Body '{"patientId":"MOD-PAT-0001","title":"EF test problem","dateTime":"2026-08-20","diagnosis":"Z00.00","comments":"EF problem state"}'
    $medicationCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/clinical-lists/medications" `
        -Headers $authenticatedHeaders `
        -Body '{"patientId":"MOD-PAT-0001","title":"EF test medication","dateTime":"2026-08-20","diagnosis":"Z79.899","comments":"EF medication state"}'
    $immunizationCreateResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/clinical-lists/immunizations" `
        -Headers $authenticatedHeaders `
        -Body '{"patientId":"MOD-PAT-0001","encounter":null,"immunizationId":null,"cvxCode":"207","vaccine":"EF test vaccine","administeredAt":"2026-08-20T12:00:00","manufacturer":"Example","lotNumber":"EF-LOT","administeredById":null,"administeredBy":"Entity Framework","educationDate":"2026-08-20","visDate":"2026-08-20","amountAdministered":0.5,"amountAdministeredUnit":"mL","expirationDate":"2027-08-20","route":"IM","administrationSite":"left deltoid","completionStatus":"complete","informationSource":"administered","note":"EF immunization state"}'
    if ([int]$allergyCreateResponse.StatusCode -ne 201 -or
        [int]$problemCreateResponse.StatusCode -ne 201 -or
        [int]$medicationCreateResponse.StatusCode -ne 201 -or
        [int]$immunizationCreateResponse.StatusCode -ne 201) {
        throw "EF-backed clinical-list creation failed. Allergy HTTP $($allergyCreateResponse.StatusCode): $(Get-HttpResponseContent -Response $allergyCreateResponse) Problem HTTP $($problemCreateResponse.StatusCode): $(Get-HttpResponseContent -Response $problemCreateResponse) Medication HTTP $($medicationCreateResponse.StatusCode): $(Get-HttpResponseContent -Response $medicationCreateResponse) Immunization HTTP $($immunizationCreateResponse.StatusCode): $(Get-HttpResponseContent -Response $immunizationCreateResponse)"
    }
    $allergy = (Get-HttpResponseContent -Response $allergyCreateResponse) | ConvertFrom-Json
    $problem = (Get-HttpResponseContent -Response $problemCreateResponse) | ConvertFrom-Json
    $medication = (Get-HttpResponseContent -Response $medicationCreateResponse) | ConvertFrom-Json
    $immunization = (Get-HttpResponseContent -Response $immunizationCreateResponse) | ConvertFrom-Json
    $medicationUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/clinical-lists/medications/$($medication.id)" `
        -Headers $authenticatedHeaders `
        -Body '{"title":"EF test medication updated","diagnosis":"Z79.899","date":"2026-08-21","comments":"Updated EF medication state","reason":"Verify EF content mutation","expectedVersion":1}'
    $staleMedicationResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/clinical-lists/medications/$($medication.id)/deactivate" `
        -Headers $authenticatedHeaders `
        -Body '{"comments":"Stale EF transition","expectedVersion":1}'
    $medicationDeactivateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/clinical-lists/medications/$($medication.id)/deactivate" `
        -Headers $authenticatedHeaders `
        -Body '{"comments":"Valid EF transition","expectedVersion":2}'
    $medicationRestoreResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/clinical-lists/medications/$($medication.id)/restore" `
        -Headers $authenticatedHeaders `
        -Body '{"reason":"Verify EF restore","expectedVersion":3}'
    $medicationHistoryResponse = Invoke-Http `
        -Method "GET" `
        -Path "/api/clinical-lists/medications/$($medication.id)/lifecycle-history" `
        -Headers $authenticatedHeaders
    $allergyDeactivateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/clinical-lists/allergies/$($allergy.id)/deactivate" `
        -Headers $authenticatedHeaders `
        -Body '{"comments":"EF allergy transition"}'
    $problemDeactivateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/clinical-lists/problems/$($problem.id)/deactivate" `
        -Headers $authenticatedHeaders `
        -Body '{"comments":"EF problem transition"}'
    $immunizationErrorResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/clinical-lists/immunizations/$($immunization.id)/entered-in-error" `
        -Headers $authenticatedHeaders `
        -Body '{"note":"EF entered-in-error transition"}'
    if ([int]$medicationUpdateResponse.StatusCode -ne 200 -or
        [int]$staleMedicationResponse.StatusCode -ne 409 -or
        [int]$medicationDeactivateResponse.StatusCode -ne 200 -or
        [int]$medicationRestoreResponse.StatusCode -ne 200 -or
        [int]$medicationHistoryResponse.StatusCode -ne 200 -or
        [int]$allergyDeactivateResponse.StatusCode -ne 200 -or
        [int]$problemDeactivateResponse.StatusCode -ne 200 -or
        [int]$immunizationErrorResponse.StatusCode -ne 200) {
        throw "EF-backed clinical-list lifecycle mutation or optimistic concurrency behavior failed."
    }
    $medicationHistory = (Get-HttpResponseContent -Response $medicationHistoryResponse) | ConvertFrom-Json
    $immunizationDefault = Invoke-DatabaseScalar -Sql "select column_default from information_schema.columns where table_name = 'immunizations' and column_name = 'id';"
    if ($medicationHistory.currentVersion -ne 4 -or
        $medicationHistory.eventCount -ne 4 -or
        $medicationHistory.events.action -notcontains "edited" -or
        $medicationHistory.events.action -notcontains "deactivated" -or
        $medicationHistory.events.action -notcontains "restored" -or
        $immunization.id -le 8500000 -or
        $immunizationDefault -notlike "nextval*immunizations_id_seq*") {
        throw "EF-backed clinical-list state, lifecycle history, or sequence default returned unexpected data."
    }
    $allergyDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/clinical-lists/allergies/$($allergy.id)" `
        -Headers $authenticatedHeaders
    $problemDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/clinical-lists/problems/$($problem.id)" `
        -Headers $authenticatedHeaders
    $immunizationDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/clinical-lists/immunizations/$($immunization.id)" `
        -Headers $authenticatedHeaders
    if ([int]$allergyDeleteResponse.StatusCode -ne 204 -or
        [int]$problemDeleteResponse.StatusCode -ne 204 -or
        [int]$immunizationDeleteResponse.StatusCode -ne 204) {
        throw "EF-backed clinical-list cleanup failed."
    }
    $CompletedScenarios.Add("ef-core-clinical-list-state")

    $procedureOrganizationResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/procedures/lab-provider-address-book" `
        -Headers $authenticatedHeaders `
        -Body '{"organization":"EF Procedure Laboratory","type":"ord_lab","active":true}'
    if ([int]$procedureOrganizationResponse.StatusCode -ne 201) {
        throw "EF-backed procedure address-book creation failed. HTTP $($procedureOrganizationResponse.StatusCode): $(Get-HttpResponseContent -Response $procedureOrganizationResponse)"
    }
    $procedureOrganization = (Get-HttpResponseContent -Response $procedureOrganizationResponse) | ConvertFrom-Json
    $procedureProviderBody = @{
        name = "EF Procedure Provider"
        labDirectorId = $procedureOrganization.id
        npi = "1234567890"
        protocol = "dl"
        usage = "t"
        direction = "r"
        sendApplicationId = "EF-SEND"
        sendFacilityId = "EF-FAC"
        receiveApplicationId = "EF-RECV"
        receiveFacilityId = "EF-RECV-FAC"
        remoteHost = "ef.example.test"
        login = "ef-user"
        password = "ef-password"
        ordersPath = "/orders"
        resultsPath = "/results"
        notes = "EF provider state"
        active = $true
    } | ConvertTo-Json
    $procedureProviderResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/procedures/lab-providers" `
        -Headers $authenticatedHeaders `
        -Body $procedureProviderBody
    if ([int]$procedureProviderResponse.StatusCode -ne 201) {
        throw "EF-backed procedure provider creation failed. HTTP $($procedureProviderResponse.StatusCode): $(Get-HttpResponseContent -Response $procedureProviderResponse)"
    }
    $procedureProvider = (Get-HttpResponseContent -Response $procedureProviderResponse) | ConvertFrom-Json
    $procedureGroupResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/procedures/order-catalog" `
        -Headers $authenticatedHeaders `
        -Body '{"parentId":null,"labId":null,"name":"EF Procedure Group","code":null,"itemType":"grp","procedureTypeName":null,"description":"EF group state","specimen":null,"standardCode":null,"sequence":900,"active":true}'
    if ([int]$procedureGroupResponse.StatusCode -ne 201) {
        throw "EF-backed procedure catalog group creation failed. HTTP $($procedureGroupResponse.StatusCode): $(Get-HttpResponseContent -Response $procedureGroupResponse)"
    }
    $procedureGroup = (Get-HttpResponseContent -Response $procedureGroupResponse) | ConvertFrom-Json
    $procedureOrderBody = @{
        parentId = $procedureGroup.id
        labId = $procedureProvider.id
        name = "EF Procedure Order"
        code = "EF-ORDER"
        itemType = "ord"
        procedureTypeName = "laboratory"
        description = "EF order state"
        specimen = "blood"
        standardCode = "EF-LOINC"
        sequence = 901
        active = $true
    } | ConvertTo-Json
    $procedureOrderResponse = Invoke-Http `
        -Method "POST" `
        -Path "/api/procedures/order-catalog" `
        -Headers $authenticatedHeaders `
        -Body $procedureOrderBody
    if ([int]$procedureOrderResponse.StatusCode -ne 201) {
        throw "EF-backed procedure catalog order creation failed. HTTP $($procedureOrderResponse.StatusCode): $(Get-HttpResponseContent -Response $procedureOrderResponse)"
    }
    $procedureOrder = (Get-HttpResponseContent -Response $procedureOrderResponse) | ConvertFrom-Json
    $procedureOrderUpdateBody = @{
        parentId = $procedureGroup.id
        labId = $procedureProvider.id
        name = "EF Procedure Order Updated"
        code = "EF-ORDER"
        itemType = "ord"
        procedureTypeName = "laboratory"
        description = "Updated EF order state"
        specimen = "serum"
        standardCode = "EF-LOINC"
        sequence = 902
        active = $false
    } | ConvertTo-Json
    $procedureOrderUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/procedures/order-catalog/$($procedureOrder.id)" `
        -Headers $authenticatedHeaders `
        -Body $procedureOrderUpdateBody
    $procedureProviderUpdateBody = $procedureProviderBody | ConvertFrom-Json
    $procedureProviderUpdateBody.notes = "Updated EF provider state"
    $procedureProviderUpdateBody.active = $false
    $procedureProviderUpdateResponse = Invoke-Http `
        -Method "PUT" `
        -Path "/api/procedures/lab-providers/$($procedureProvider.id)" `
        -Headers $authenticatedHeaders `
        -Body ($procedureProviderUpdateBody | ConvertTo-Json)
    if ([int]$procedureOrderUpdateResponse.StatusCode -ne 200 -or
        [int]$procedureProviderUpdateResponse.StatusCode -ne 200) {
        throw "EF-backed procedure catalog or provider update failed."
    }
    $procedureOrderUpdate = (Get-HttpResponseContent -Response $procedureOrderUpdateResponse) | ConvertFrom-Json
    $procedureProviderUpdate = (Get-HttpResponseContent -Response $procedureProviderUpdateResponse) | ConvertFrom-Json
    $catalogDefault = Invoke-DatabaseScalar -Sql "select column_default from information_schema.columns where table_name = 'lab_order_catalog' and column_name = 'id';"
    $providerDefault = Invoke-DatabaseScalar -Sql "select column_default from information_schema.columns where table_name = 'lab_providers' and column_name = 'id';"
    $organizationDefault = Invoke-DatabaseScalar -Sql "select column_default from information_schema.columns where table_name = 'lab_provider_address_book' and column_name = 'id';"
    if ($procedureOrderUpdate.catalog.items.name -notcontains "EF Procedure Order Updated" -or
        $procedureProviderUpdate.directory.providers.notes -notcontains "Updated EF provider state" -or
        $catalogDefault -notlike "nextval*lab_order_catalog_id_seq*" -or
        $providerDefault -notlike "nextval*lab_providers_id_seq*" -or
        $organizationDefault -notlike "nextval*lab_provider_address_book_id_seq*") {
        throw "EF-backed procedure directory state or sequence defaults returned unexpected data."
    }
    $procedureOrderDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/procedures/order-catalog/$($procedureOrder.id)" `
        -Headers $authenticatedHeaders
    $procedureGroupDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/procedures/order-catalog/$($procedureGroup.id)" `
        -Headers $authenticatedHeaders
    $procedureProviderDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/procedures/lab-providers/$($procedureProvider.id)" `
        -Headers $authenticatedHeaders
    $procedureOrganizationDeleteResponse = Invoke-Http `
        -Method "DELETE" `
        -Path "/api/procedures/lab-provider-address-book/$($procedureOrganization.id)" `
        -Headers $authenticatedHeaders
    if ([int]$procedureOrderDeleteResponse.StatusCode -ne 204 -or
        [int]$procedureGroupDeleteResponse.StatusCode -ne 204 -or
        [int]$procedureProviderDeleteResponse.StatusCode -ne 204 -or
        [int]$procedureOrganizationDeleteResponse.StatusCode -ne 204) {
        throw "EF-backed procedure directory cleanup failed."
    }
    $CompletedScenarios.Add("ef-core-procedure-directory-mutations")

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
    $VerificationPassed = $true
    Write-Host "AvenChart migration resilience verification passed: $ResultPath"
}
finally {
    if ($ApiStarted) {
        if (-not $VerificationPassed) {
            docker logs $ApiContainerName 2>&1 | Select-Object -Last 120 | Write-Host
        }
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
