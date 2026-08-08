# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

param([string]$OutputDirectory,[int]$PostgresWaitSeconds = 90)
$ErrorActionPreference = 'Stop'
$SolutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $SolutionRoot 'artifacts\backups' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$backup = Join-Path $OutputDirectory "avenchart_$stamp.dump"
$containerDump = "/tmp/avenchart_$stamp.dump"
Push-Location $SolutionRoot
try {
  docker compose up -d postgres
  if ($LASTEXITCODE -ne 0) { throw 'Could not start the PostgreSQL service.' }
  $deadline = (Get-Date).AddSeconds($PostgresWaitSeconds); $ready = $false
  while ((Get-Date) -lt $deadline) {
    docker compose exec -T postgres pg_isready -U avenchart -d avenchart *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 2
  }
  if (-not $ready) { throw "PostgreSQL was not ready within $PostgresWaitSeconds seconds." }
  docker compose exec -T postgres pg_dump -U avenchart -d avenchart -Fc -f $containerDump
  if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed.' }
  $containerId = (docker compose ps -q postgres).Trim(); if (-not $containerId) { throw 'Could not resolve PostgreSQL container.' }
  docker cp "${containerId}:$containerDump" $backup
  docker compose exec -T postgres rm -f $containerDump
  if ($LASTEXITCODE -ne 0) { Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue; throw 'Could not copy database dump from the container.' }
  [ordered]@{ database='avenchart'; createdAt=(Get-Date).ToUniversalTime().ToString('o'); backup=$backup; sha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash } | ConvertTo-Json | Set-Content -LiteralPath "$backup.json" -Encoding utf8
  Write-Host "Backup created: $backup"
} finally { Pop-Location }
