# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [switch]$ResetStagingData,
    [int]$ReadinessWaitSeconds = 90
)

$ErrorActionPreference = 'Stop'

if (-not $ResetStagingData) {
    throw 'Pass -ResetStagingData to explicitly discard and rebuild the synthetic staging database.'
}

if ($ReadinessWaitSeconds -lt 10 -or $ReadinessWaitSeconds -gt 300) {
    throw 'ReadinessWaitSeconds must be between 10 and 300.'
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$avenchartRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$stagingEnvironment = Join-Path $repositoryRoot '.env.staging'
$composeFile = Join-Path $repositoryRoot 'docker-compose.staging.yml'
$seedGenerator = Join-Path $PSScriptRoot 'generate-postgres-seed.mjs'
$seedSql = Join-Path $avenchartRoot 'artifacts\postgres\seed-gold.sql'

if (-not (Test-Path -LiteralPath $stagingEnvironment)) {
    throw 'Missing .env.staging. Copy staging.env.example and supply a local password before resetting staging.'
}

$composeArguments = @('compose', '--env-file', $stagingEnvironment, '-f', $composeFile)

Push-Location $repositoryRoot
try {
    & docker @composeArguments ps --services --filter status=running | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'The synthetic staging Docker stack is not available. Start it with docker compose --env-file .env.staging -f docker-compose.staging.yml up --build --wait.'
    }

    & docker @composeArguments stop api ui
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not stop the synthetic staging API and UI before resetting the database.'
    }

    & node $seedGenerator
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $seedSql)) {
        throw 'Could not generate the synthetic gold dataset SQL.'
    }

    $schemaResetSql = @'
drop schema if exists public cascade;
create schema public authorization avenchart_staging;
grant all on schema public to avenchart_staging;
grant all on schema public to public;
'@
    $schemaResetSql | & docker @composeArguments exec -T postgres psql -X -U avenchart_staging -d avenchart_staging -v ON_ERROR_STOP=1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'The synthetic staging schema reset failed.'
    }

    Get-Content -LiteralPath $seedSql -Raw | & docker @composeArguments exec -T postgres psql -X -U avenchart_staging -d avenchart_staging -v ON_ERROR_STOP=1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'The synthetic gold dataset import failed.'
    }

    & docker @composeArguments run --rm migrator
    if ($LASTEXITCODE -ne 0) {
        throw 'The synthetic staging migrations did not complete after the seed import.'
    }

    & docker @composeArguments up -d api ui
    if ($LASTEXITCODE -ne 0) {
        throw 'The synthetic staging API and UI could not be restarted after the seed import.'
    }

    $deadline = (Get-Date).AddSeconds($ReadinessWaitSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:8088/health/ready' -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                Write-Host 'Synthetic staging data reset and gold dataset seed completed.' -ForegroundColor Green
                exit 0
            }
        }
        catch {
        }

        Start-Sleep -Seconds 2
    }

    throw "The API did not become ready within $ReadinessWaitSeconds seconds after the staging seed."
}
finally {
    Pop-Location
}
