# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param([int]$PostgresWaitSeconds = 90)

$ErrorActionPreference = 'Stop'
$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ArtifactsRoot = Join-Path $SolutionRoot 'artifacts\backups\rehearsals'
$ResultPath = Join-Path $ArtifactsRoot 'latest-avenchart-backup-restore-rehearsal.json'

function Invoke-PostgresScalar {
    param([string]$Sql)
    $value = docker compose exec -T postgres psql -X -U avenchart -d avenchart -t -A -v ON_ERROR_STOP=1 -c $Sql
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL rehearsal query failed.' }
    return ($value | Select-Object -Last 1).Trim()
}

function Wait-Postgres {
    docker compose up -d postgres
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the PostgreSQL service for the recovery rehearsal.' }
    $deadline = (Get-Date).AddSeconds($PostgresWaitSeconds)
    while ((Get-Date) -lt $deadline) {
        docker compose exec -T postgres pg_isready -U avenchart -d avenchart *> $null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep -Seconds 2
    }
    throw "PostgreSQL was not ready within $PostgresWaitSeconds seconds for the recovery rehearsal."
}

Push-Location $SolutionRoot
try {
    Wait-Postgres
    New-Item -ItemType Directory -Force $ArtifactsRoot | Out-Null
    $runDirectory = Join-Path $ArtifactsRoot ((Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))
    New-Item -ItemType Directory -Force $runDirectory | Out-Null
    docker compose exec -T postgres psql -X -U avenchart -d avenchart -v ON_ERROR_STOP=1 -c 'drop table if exists backup_restore_rehearsal_marker;'
    if ($LASTEXITCODE -ne 0) { throw 'Could not clear a prior backup/restore rehearsal marker.' }
    $before = Invoke-PostgresScalar -Sql "select json_build_object('datasetId',(select dataset_id from dataset_metadata limit 1),'version',(select version from dataset_metadata limit 1),'patients',(select count(*) from patients),'migrations',(select count(*) from schema_migrations));"

    & .\scripts\Backup-AvenChartDatabase.ps1 -OutputDirectory $runDirectory -PostgresWaitSeconds $PostgresWaitSeconds
    if ($LASTEXITCODE -ne 0) { throw 'Backup script failed.' }
    $backupManifestPath = Get-ChildItem -LiteralPath $runDirectory -Filter '*.dump.json' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $backupManifestPath) { throw 'Backup manifest was not created.' }
    $backupManifest = Get-Content -LiteralPath $backupManifestPath -Raw | ConvertFrom-Json

    docker compose exec -T postgres psql -X -U avenchart -d avenchart -v ON_ERROR_STOP=1 -c "create table backup_restore_rehearsal_marker (id integer primary key, created_at timestamptz not null default now()); insert into backup_restore_rehearsal_marker (id) values (1);"
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the post-backup rehearsal marker.' }
    $markerBeforeRestore = Invoke-PostgresScalar -Sql 'select count(*) from backup_restore_rehearsal_marker;'

    & .\scripts\Restore-AvenChartDatabase.ps1 -SnapshotPath $backupManifest.backup -Force -PostgresWaitSeconds $PostgresWaitSeconds
    if ($LASTEXITCODE -ne 0) { throw 'Restore script failed.' }
    $after = Invoke-PostgresScalar -Sql "select json_build_object('datasetId',(select dataset_id from dataset_metadata limit 1),'version',(select version from dataset_metadata limit 1),'patients',(select count(*) from patients),'migrations',(select count(*) from schema_migrations));"
    $markerAfterRestore = Invoke-PostgresScalar -Sql "select to_regclass('public.backup_restore_rehearsal_marker') is not null;"
    $passed = $before -eq $after -and $markerBeforeRestore -eq '1' -and $markerAfterRestore -eq 'f' -and (Test-Path -LiteralPath $backupManifest.backup)
    if (-not $passed) { throw 'Backup/restore rehearsal did not return the database to its captured state.' }

    [ordered]@{ status='passed'; completedAt=(Get-Date).ToUniversalTime().ToString('o'); backup=$backupManifest.backup; sha256=$backupManifest.sha256; before=($before | ConvertFrom-Json); after=($after | ConvertFrom-Json); markerBeforeRestore=$markerBeforeRestore; markerAfterRestore=$markerAfterRestore } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultPath -Encoding utf8
    Write-Host "Backup/restore rehearsal passed: $ResultPath"
}
finally {
    Pop-Location
}
