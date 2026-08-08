# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param([Parameter(Mandatory=$true)][string]$SnapshotPath,[switch]$Force,[int]$PostgresWaitSeconds = 90)
$ErrorActionPreference = 'Stop'
if (-not $Force) { throw 'Restore is destructive. Re-run with -Force after verifying the snapshot path.' }
$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$BackupsRoot = [IO.Path]::GetFullPath((Join-Path $SolutionRoot 'artifacts\backups'))
$BackupsRootPrefix = $BackupsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$SnapshotPath = [IO.Path]::GetFullPath($SnapshotPath)
$containerDump = '/tmp/avenchart_restore.dump'
if (-not $SnapshotPath.StartsWith($BackupsRootPrefix, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $SnapshotPath -PathType Leaf)) { throw 'Snapshot must be an existing file under artifacts\backups.' }
$manifestPath = "$SnapshotPath.json"
if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
  $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
  $actualHash = (Get-FileHash -LiteralPath $SnapshotPath -Algorithm SHA256).Hash
  if ([string]::IsNullOrWhiteSpace($manifest.sha256) -or -not [string]::Equals($actualHash, $manifest.sha256, [StringComparison]::OrdinalIgnoreCase)) { throw 'Snapshot checksum does not match its backup manifest.' }
}
Push-Location $SolutionRoot
try {
  docker compose up -d postgres; if ($LASTEXITCODE -ne 0) { throw 'Could not start the PostgreSQL service.' }
  $deadline = (Get-Date).AddSeconds($PostgresWaitSeconds); $ready = $false
  while ((Get-Date) -lt $deadline) {
    docker compose exec -T postgres pg_isready -U avenchart -d avenchart *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 2
  }
  if (-not $ready) { throw "PostgreSQL was not ready within $PostgresWaitSeconds seconds." }
  $containerId = (docker compose ps -q postgres).Trim(); if (-not $containerId) { throw 'Could not resolve PostgreSQL container.' }
  docker cp $SnapshotPath "${containerId}:$containerDump"
  if ($LASTEXITCODE -ne 0) { throw 'Could not copy the snapshot into the PostgreSQL container.' }
  docker compose exec -T postgres pg_restore -l $containerDump *> $null
  if ($LASTEXITCODE -ne 0) { throw 'Snapshot preflight failed; the dump is not restorable.' }
  docker compose exec -T postgres psql -U avenchart -d postgres -v ON_ERROR_STOP=1 -c 'drop database if exists avenchart with (force);' -c 'create database avenchart owner avenchart;'
  if ($LASTEXITCODE -ne 0) { throw 'Could not recreate the target database.' }
  docker compose exec -T postgres pg_restore -U avenchart -d avenchart --no-owner --exit-on-error $containerDump
  if ($LASTEXITCODE -ne 0) { throw 'pg_restore failed.' }
  Write-Host "Restored: $SnapshotPath"
} finally {
  docker compose exec -T postgres rm -f $containerDump *> $null
  Pop-Location
}
