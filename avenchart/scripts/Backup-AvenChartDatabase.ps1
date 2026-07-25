param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $SolutionRoot 'artifacts\backups' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$backup = Join-Path $OutputDirectory "legacy-ehr_modernized_$stamp.dump"
$containerDump = "/tmp/legacy-ehr_modernized_$stamp.dump"
Push-Location $SolutionRoot
try {
  docker compose up -d postgres
  if ($LASTEXITCODE -ne 0) { throw 'Could not start the PostgreSQL service.' }
  docker compose exec -T postgres pg_dump -U legacy-ehr -d legacy-ehr_modernized -Fc -f $containerDump
  if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed.' }
  $containerId = (docker compose ps -q postgres).Trim(); if (-not $containerId) { throw 'Could not resolve PostgreSQL container.' }
  docker cp "${containerId}:$containerDump" $backup
  docker compose exec -T postgres rm -f $containerDump
  if ($LASTEXITCODE -ne 0) { Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue; throw 'Could not copy database dump from the container.' }
  [ordered]@{ database='legacy-ehr_modernized'; createdAt=(Get-Date).ToUniversalTime().ToString('o'); backup=$backup; sha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash } | ConvertTo-Json | Set-Content -LiteralPath "$backup.json" -Encoding utf8
  Write-Host "Backup created: $backup"
} finally { Pop-Location }
