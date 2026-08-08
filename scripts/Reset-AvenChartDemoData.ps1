# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [switch]$Force,

    [ValidateRange(10, 900)]
    [int]$PostgresWaitSeconds = 90
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AvenChart.Local.psm1') -Force

Assert-AvenChartDocker
$repositoryRoot = Get-AvenChartRepositoryRoot
$seedScript = Join-Path $repositoryRoot 'avenchart\scripts\Seed-AvenChartGoldDataset.ps1'

if ($Force) {
    $ConfirmPreference = 'None'
}

if (-not $PSCmdlet.ShouldProcess('the local AvenChart PostgreSQL database', 'replace its schema and data with the synthetic gold dataset')) {
    return
}

Write-Output 'Resetting the local database to the deterministic synthetic demo dataset...'
& $seedScript -PostgresWaitSeconds $PostgresWaitSeconds
if ($LASTEXITCODE -ne 0) {
    throw "The AvenChart demo-data reset failed with exit code $LASTEXITCODE."
}
