# SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param(
    [string]$ApiBaseUrl = 'http://localhost:5001',
    [int]$ApiWaitSeconds = 90
)

$ErrorActionPreference = 'Stop'

$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ArtifactsRoot = Join-Path $SolutionRoot 'artifacts'
$ResultPath = Join-Path $ArtifactsRoot 'latest-modernized-operational-readiness.json'
$checks = New-Object System.Collections.Generic.List[object]
$status = 'passed'

function Add-Check {
    param([string]$Name, [string]$Result, [object]$Details = $null)

    $script:checks.Add([ordered]@{ name = $Name; status = $Result; details = $Details })
    if ($Result -eq 'failed') { $script:status = 'failed' }
}

function Invoke-PostgresScalar {
    param([string]$Sql)

    $value = docker compose exec -T postgres psql -X -U legacy-ehr -d legacy-ehr_modernized -t -A -v ON_ERROR_STOP=1 -c $Sql
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL readiness query failed.' }
    return ($value | Select-Object -Last 1).Trim()
}

Push-Location $SolutionRoot
try {
    New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null
    docker compose up -d postgres api frontend
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the modernized runtime services.' }

    $runningServices = @(docker compose ps --status running --services)
    if ($LASTEXITCODE -ne 0) { throw 'Could not read Docker Compose service status.' }
    $requiredServices = @('postgres', 'api', 'frontend')
    $missingServices = @($requiredServices | Where-Object { $_ -notin $runningServices })
    Add-Check -Name 'Compose runtime services' -Result $(if ($missingServices.Count -eq 0) { 'passed' } else { 'failed' }) -Details @{ running = $runningServices; missing = $missingServices }

    $deadline = (Get-Date).AddSeconds($ApiWaitSeconds)
    $health = $null
    $liveness = $null
    $readiness = $null
    while ((Get-Date) -lt $deadline) {
        try {
            $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -TimeoutSec 5
            $liveness = Invoke-RestMethod -Uri "$ApiBaseUrl/health/live" -TimeoutSec 5
            $readiness = Invoke-RestMethod -Uri "$ApiBaseUrl/health/ready" -TimeoutSec 5
            if ($health.status -eq 'healthy' -and $liveness.status -eq 'healthy' -and $readiness.status -eq 'healthy' -and $readiness.dependencies.postgres -eq 'healthy' -and $readiness.dependencies.schemaMigrations -eq 'healthy') { break }
        }
        catch {
        }
        Start-Sleep -Seconds 2
    }
    $apiReady = $null -ne $health -and $health.status -eq 'healthy' -and $null -ne $liveness -and $liveness.status -eq 'healthy' -and $null -ne $readiness -and $readiness.status -eq 'healthy' -and $readiness.dependencies.postgres -eq 'healthy' -and $readiness.dependencies.schemaMigrations -eq 'healthy'
    Add-Check -Name 'API liveness, PostgreSQL, and schema readiness' -Result $(if ($apiReady) { 'passed' } else { 'failed' }) -Details @{ health = $health; liveness = $liveness; readiness = $readiness }

    $dataset = Invoke-PostgresScalar -Sql "select json_build_object('datasetId',(select dataset_id from dataset_metadata limit 1),'version',(select version from dataset_metadata limit 1),'patients',(select count(*) from patients),'migrations',(select count(*) from schema_migrations));" | ConvertFrom-Json
    $datasetPassed = $dataset.datasetId -eq 'legacy-ehr-shared-synthetic-v1' -and $dataset.version -eq 'v1' -and $dataset.patients -eq 1000
    Add-Check -Name 'Synthetic dataset and migration ledger' -Result $(if ($datasetPassed) { 'passed' } else { 'failed' }) -Details $dataset

    $chartData = Invoke-PostgresScalar -Sql "select json_build_object('maritalStatus',p.marital_status,'occupation',p.occupation,'race',p.race,'ethnicity',p.ethnicity,'street',p.street,'city',p.city,'state',p.state,'postalCode',p.postal_code,'providerName',concat_ws(' ',s.first_name,s.last_name),'insuranceCount',(select count(*) from insurance_records i where i.patient_id=p.canonical_id),'historyCount',(select count(*) from patient_histories h where h.patient_id=p.canonical_id)) from patients p left join staff s on s.id=p.provider_id where p.canonical_id='MOD-PAT-0001';" | ConvertFrom-Json
    $chartDataPassed = $chartData.maritalStatus -eq 'married' -and $chartData.occupation -eq 'Retired' -and $chartData.race -eq 'White' -and $chartData.ethnicity -eq 'Not Hispanic or Latino' -and $chartData.street -eq '101 Test Patient Avenue' -and $chartData.city -eq 'La Mesa' -and $chartData.state -eq 'CA' -and $chartData.postalCode -eq '91942' -and -not [string]::IsNullOrWhiteSpace($chartData.providerName) -and [int]$chartData.insuranceCount -ge 1 -and [int]$chartData.historyCount -ge 1
    Add-Check -Name 'Patient chart data invariants' -Result $(if ($chartDataPassed) { 'passed' } else { 'failed' }) -Details $chartData

    $migrationFiles = @(Get-ChildItem -LiteralPath (Join-Path $SolutionRoot 'database\migrations') -Filter '*.sql' -File | Sort-Object Name)
    $expectedMigrations = @($migrationFiles | ForEach-Object {
        [ordered]@{
            migrationId = $_.BaseName
            checksumSha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    $appliedMigrationRows = Invoke-PostgresScalar -Sql "select coalesce(json_agg(json_build_object('migrationId',migration_id,'checksumSha256',checksum_sha256) order by migration_id)::text,'[]') from schema_migrations;" | ConvertFrom-Json
    $appliedById = @{}
    foreach ($migration in $appliedMigrationRows) {
        $appliedById[$migration.migrationId] = $migration.checksumSha256
    }
    $expectedIds = @($expectedMigrations | ForEach-Object { $_.migrationId })
    $missingMigrations = @($expectedMigrations | Where-Object { -not $appliedById.ContainsKey($_.migrationId) } | ForEach-Object { $_.migrationId })
    $unexpectedMigrations = @($appliedById.Keys | Where-Object { $_ -notin $expectedIds } | Sort-Object)
    $checksumMismatches = @($expectedMigrations | Where-Object {
        $appliedById.ContainsKey($_.migrationId) -and $appliedById[$_.migrationId] -ne $_.checksumSha256
    } | ForEach-Object { $_.migrationId })
    $migrationLedgerPassed = $migrationFiles.Count -gt 0 -and $missingMigrations.Count -eq 0 -and $unexpectedMigrations.Count -eq 0 -and $checksumMismatches.Count -eq 0
    Add-Check -Name 'Schema migration completeness' -Result $(if ($migrationLedgerPassed) { 'passed' } else { 'failed' }) -Details @{
        expected = $expectedMigrations.Count
        applied = $appliedById.Count
        missing = $missingMigrations
        unexpected = $unexpectedMigrations
        checksumMismatches = $checksumMismatches
    }

    $rehearsalPath = Join-Path $SolutionRoot 'artifacts\backups\rehearsals\latest-modernized-backup-restore-rehearsal.json'
    if (Test-Path -LiteralPath $rehearsalPath -PathType Leaf) {
        $rehearsal = Get-Content -LiteralPath $rehearsalPath -Raw | ConvertFrom-Json
        Add-Check -Name 'Latest recovery rehearsal evidence' -Result $(if ($rehearsal.status -eq 'passed') { 'passed' } else { 'failed' }) -Details $rehearsal
    }
    else {
        Add-Check -Name 'Latest recovery rehearsal evidence' -Result 'not-run' -Details 'Run Test-ModernizedBackupRestore.ps1 to create local recovery evidence.'
    }
}
catch {
    Add-Check -Name 'Operational readiness execution' -Result 'failed' -Details $_.Exception.Message
}
finally {
    $result = [ordered]@{
        status = $status
        completedAt = (Get-Date).ToUniversalTime().ToString('o')
        apiBaseUrl = $ApiBaseUrl
        checks = $checks
    }
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ResultPath -Encoding utf8
    Pop-Location
}

Write-Host "Operational readiness result: $ResultPath"
if ($status -ne 'passed') { exit 1 }
