# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [int]$PostgresWaitSeconds = 90,
    [switch]$SkipPostgresStartup
)

$ErrorActionPreference = "Stop"

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$MigrationsRoot = Join-Path $SolutionRoot "database\migrations"
$ArtifactsRoot = Join-Path $SolutionRoot "artifacts\migrations"
$ResultPath = Join-Path $ArtifactsRoot "latest-modernized-migration-result.json"
$MigrationNamePattern = '^V\d{4}__[A-Za-z0-9_-]+\.sql$'
$MigrationLock = [System.Threading.Mutex]::new($false, "Global\AvenChartSchemaMaintenance")
$MigrationLockHeld = $false
$LocationPushed = $false

function Invoke-PostgresCommand {
    param([string]$Command)

    $output = docker compose exec -T postgres psql -X -U legacy-ehr -d legacy-ehr_modernized -t -A -v ON_ERROR_STOP=1 -c $Command
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL command failed with exit code $LASTEXITCODE."
    }

    return $output
}

function Invoke-PostgresSqlFile {
    param([string]$Path)

    $sql = Get-Content -LiteralPath $Path -Raw
    $sql | docker compose exec -T postgres psql -X -U legacy-ehr -d legacy-ehr_modernized -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "Migration '$Path' failed with exit code $LASTEXITCODE."
    }
}

function Invoke-PostgresMigration {
    param(
        [string]$Path,
        [string]$MigrationId,
        [string]$Checksum,
        [string]$Description
    )

    $sql = Get-Content -LiteralPath $Path -Raw
    $transactionSql = @"
begin;
select pg_advisory_xact_lock(67531924012026001);
$sql
insert into schema_migrations (migration_id, checksum_sha256, description, applied_at, applied_by)
values ('$MigrationId', '$Checksum', '$Description', now(), 'local-cli');
commit;
"@
    $transactionSql | docker compose exec -T postgres psql -X -U legacy-ehr -d legacy-ehr_modernized -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "Migration '$Path' failed with exit code $LASTEXITCODE. Its schema changes and ledger entry were rolled back together."
    }
}

try {
    $MigrationLockHeld = $MigrationLock.WaitOne([TimeSpan]::FromMinutes(15))
    if (-not $MigrationLockHeld) {
        throw "Timed out waiting for the modernized schema-maintenance lock."
    }

    Push-Location $SolutionRoot
    $LocationPushed = $true
    $migrationFiles = @(Get-ChildItem -LiteralPath $MigrationsRoot -Filter '*.sql' -File | Sort-Object Name)
    if ($migrationFiles.Count -eq 0) {
        throw "No migration files were found in '$MigrationsRoot'."
    }

    foreach ($migrationFile in $migrationFiles) {
        if ($migrationFile.Name -notmatch $MigrationNamePattern) {
            throw "Migration '$($migrationFile.Name)' does not use the required V0001__description.sql naming convention."
        }
    }

    if (-not $SkipPostgresStartup) {
        docker compose up -d postgres
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to start the modernized PostgreSQL service."
        }

        $deadline = (Get-Date).AddSeconds($PostgresWaitSeconds)
        $ready = $false
        while ((Get-Date) -lt $deadline) {
            docker compose exec -T postgres pg_isready -U legacy-ehr -d legacy-ehr_modernized *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }

            Start-Sleep -Seconds 2
        }

        if (-not $ready) {
            throw "PostgreSQL was not ready within $PostgresWaitSeconds seconds."
        }
    }

    # Bootstrap the ledger before querying it. V0001 is idempotent by design.
    $bootstrapMigration = $migrationFiles[0]
    if ($bootstrapMigration.BaseName -ne 'V0001__migration_ledger') {
        throw "The first migration must be V0001__migration_ledger.sql."
    }
    Invoke-PostgresSqlFile -Path $bootstrapMigration.FullName

    $ledgerJson = Invoke-PostgresCommand -Command "select coalesce(json_agg(json_build_object('migrationId',migration_id,'checksumSha256',checksum_sha256) order by migration_id)::text,'[]') from schema_migrations;"
    $ledgerRows = $ledgerJson | ConvertFrom-Json
    $ledgerById = @{}
    foreach ($ledgerRow in $ledgerRows) {
        if ($ledgerById.ContainsKey($ledgerRow.migrationId)) {
            throw "Migration ledger contains duplicate id '$($ledgerRow.migrationId)'."
        }
        $ledgerById[$ledgerRow.migrationId] = $ledgerRow.checksumSha256
    }

    $expectedIds = @($migrationFiles | ForEach-Object { $_.BaseName })
    $unexpectedIds = @($ledgerById.Keys | Where-Object { $_ -notin $expectedIds } | Sort-Object)
    if ($unexpectedIds.Count -gt 0) {
        throw "Migration drift detected. The ledger contains migrations that are not checked in: $($unexpectedIds -join ', ')."
    }

    $applied = @()
    $skipped = @()
    foreach ($migrationFile in $migrationFiles) {
        $migrationId = $migrationFile.BaseName
        $checksum = (Get-FileHash -LiteralPath $migrationFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $storedChecksum = $ledgerById[$migrationId]

        if ($storedChecksum) {
            if ($storedChecksum -ne $checksum) {
                throw "Migration drift detected for '$migrationId'. The applied checksum does not match the checked-in SQL file."
            }

            $skipped += $migrationId
            continue
        }

        $description = $migrationFile.BaseName.Substring($migrationFile.BaseName.IndexOf('__') + 2).Replace('_', ' ')
        Invoke-PostgresMigration `
            -Path $migrationFile.FullName `
            -MigrationId $migrationId `
            -Checksum $checksum `
            -Description $description
        $applied += $migrationId
        $ledgerById[$migrationId] = $checksum
    }

    New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null
    [ordered]@{
        status = 'passed'
        generatedAt = (Get-Date).ToUniversalTime().ToString('o')
        database = 'legacy-ehr_modernized'
        migrationsRoot = $MigrationsRoot
        applied = $applied
        alreadyApplied = $skipped
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ResultPath -Encoding UTF8

    Write-Host "Modernized migrations complete: $ResultPath"
}
finally {
    if ($LocationPushed) {
        Pop-Location
    }
    if ($MigrationLockHeld) {
        $MigrationLock.ReleaseMutex()
    }
    $MigrationLock.Dispose()
}
