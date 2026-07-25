param([Parameter(Mandatory=$true)][string]$SnapshotPath,[switch]$Force)
$ErrorActionPreference = 'Stop'
if (-not $Force) { throw 'Restore is destructive. Re-run with -Force after verifying the snapshot path.' }
$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$BackupsRoot = [IO.Path]::GetFullPath((Join-Path $SolutionRoot 'artifacts\backups'))
$SnapshotPath = [IO.Path]::GetFullPath($SnapshotPath)
if (-not $SnapshotPath.StartsWith($BackupsRoot, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $SnapshotPath -PathType Leaf)) { throw 'Snapshot must be an existing file under artifacts\backups.' }
Push-Location $SolutionRoot
try {
  docker compose up -d postgres; if ($LASTEXITCODE -ne 0) { throw 'Could not start the PostgreSQL service.' }
  docker compose exec -T postgres psql -U legacy-ehr -d postgres -v ON_ERROR_STOP=1 -c 'drop database if exists legacy-ehr_modernized with (force);' -c 'create database legacy-ehr_modernized owner legacy-ehr;'
  if ($LASTEXITCODE -ne 0) { throw 'Could not recreate the target database.' }
  Get-Content -LiteralPath $SnapshotPath -AsByteStream | docker compose exec -T postgres pg_restore -U legacy-ehr -d legacy-ehr_modernized --no-owner --exit-on-error
  if ($LASTEXITCODE -ne 0) { throw 'pg_restore failed.' }
  Write-Host "Restored: $SnapshotPath"
} finally { Pop-Location }
