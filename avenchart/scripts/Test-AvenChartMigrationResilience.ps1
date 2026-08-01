# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
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
foreach ($checkpoint in $FaultCheckpoints) {
    if ($checkpoint -lt 1 -or $checkpoint -ge $ExpectedMigrationCount) {
        throw "Fault checkpoint $checkpoint must be between 1 and $($ExpectedMigrationCount - 1)."
    }
}

$DatabaseName = "legacy-ehr_modernized_test_$([Guid]::NewGuid().ToString('N'))"
$ApiContainerName = "legacy-ehr-migration-test-$([Guid]::NewGuid().ToString('N'))"
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts\migration-resilience"
$ResultPath = Join-Path $ArtifactsRoot "latest-modernized-migration-resilience.json"
$LocationPushed = $false
$DatabaseCreated = $false
$ApiStarted = $false
$CompletedScenarios = [System.Collections.Generic.List[string]]::new()

function Assert-TestDatabaseName {
    param([string]$Name)
    if ($Name -notmatch '^legacy-ehr_modernized_test_[a-f0-9]{32}$') {
        throw "Refusing database operation for unexpected test database name '$Name'."
    }
}

function Invoke-DatabaseScalar {
    param([string]$Sql)

    Assert-TestDatabaseName -Name $DatabaseName
    $value = docker compose exec -T postgres psql -X -U legacy-ehr -d $DatabaseName -t -A -v ON_ERROR_STOP=1 -c $Sql
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
        & .\scripts\Invoke-ModernizedMigrations.ps1 `
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
    $responseContent = if ($Response.Content -is [byte[]]) {
        [System.Text.Encoding]::UTF8.GetString($Response.Content)
    }
    else {
        [string]$Response.Content
    }
    $problem = $responseContent | ConvertFrom-Json
    if ($problem.code -ne "schema_not_ready") {
        throw "Expected schema_not_ready problem code, but received '$($problem.code)'. Response body: $responseContent"
    }
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
        docker compose exec -T postgres pg_isready -U legacy-ehr -d legacy-ehr_modernized *> $null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $postgresDeadline)
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL did not become ready for migration resilience testing."
    }

    docker compose build --quiet migrator
    if ($LASTEXITCODE -ne 0) {
        throw "Could not build the packaged migrator image for resilience testing."
    }

    Assert-TestDatabaseName -Name $DatabaseName
    docker compose exec -T postgres psql -X -U legacy-ehr -d postgres -v ON_ERROR_STOP=1 -c "create database $DatabaseName owner legacy-ehr;"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create isolated migration test database '$DatabaseName'."
    }
    $DatabaseCreated = $true

    foreach ($checkpoint in $FaultCheckpoints) {
        $faultObserved = $false
        try {
            & .\scripts\Seed-ModernizedGoldDataset.ps1 `
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
        & .\scripts\Invoke-ModernizedMigrations.ps1 -SkipPostgresStartup -SkipArtifact -SkipImageBuild -DatabaseName $DatabaseName
        Assert-LedgerCount -Expected $ExpectedMigrationCount
        Assert-AnchorChartData
        $CompletedScenarios.Add("reset-interruption-$checkpoint")
    }

    & .\scripts\Invoke-ModernizedMigrations.ps1 -SkipPostgresStartup -SkipArtifact -SkipImageBuild -DatabaseName $DatabaseName
    Assert-LedgerCount -Expected $ExpectedMigrationCount
    $CompletedScenarios.Add("idempotent-no-op")

    $connectionString = "Host=postgres;Port=5432;Database=$DatabaseName;Username=legacy-ehr;Password=legacy-ehr_demo"
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
    & .\scripts\Invoke-ModernizedMigrations.ps1 -SkipPostgresStartup -SkipArtifact -SkipImageBuild -DatabaseName $DatabaseName
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
    Wait-ForApiReady
    Invoke-DatabaseScalar -Sql "alter table patients rename column marital_status to marital_status_fault;" | Out-Null
    $chartResponse = Invoke-Http -Method "GET" -Path "/api/patients/MOD-PAT-0001" -Headers @{ "X-Legacy EHR-Session" = $login.sessionId }
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
    Write-Host "Modernized migration resilience verification passed: $ResultPath"
}
finally {
    if ($ApiStarted) {
        docker rm -f $ApiContainerName *> $null
    }
    if ($DatabaseCreated) {
        Assert-TestDatabaseName -Name $DatabaseName
        docker compose exec -T postgres psql -X -U legacy-ehr -d postgres -v ON_ERROR_STOP=1 -c "drop database if exists $DatabaseName with (force);" *> $null
    }
    if ($LocationPushed) {
        Pop-Location
    }
}
