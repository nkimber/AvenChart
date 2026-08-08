# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipWait,

    [ValidateRange(1, 900)]
    [int]$WaitSeconds = 120
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AvenChart.Local.psm1') -Force

Assert-AvenChartDocker
$upArguments = @('up', '-d', $(if ($SkipBuild) { '--no-build' } else { '--build' }))

Write-Output 'Starting the AvenChart database, migrations, API, and reference UI...'
Invoke-AvenChartCompose -Project Core -ComposeArguments $upArguments

Write-Output 'Starting the modern AvenChart UI...'
Invoke-AvenChartCompose -Project ModernUi -ComposeArguments $upArguments

if (-not $SkipWait) {
    Wait-AvenChartHttpEndpoint -Name 'API readiness' -Url 'http://localhost:5001/health/ready' -WaitSeconds $WaitSeconds
    Wait-AvenChartHttpEndpoint -Name 'Reference UI' -Url 'http://localhost:3000/?entry=chooser' -WaitSeconds $WaitSeconds
    Wait-AvenChartHttpEndpoint -Name 'Modern UI' -Url 'http://localhost:3100/' -WaitSeconds $WaitSeconds
}

Write-Output ''
Write-Output 'AvenChart is running.'
Write-AvenChartEndpoints
