# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [switch]$SkipBuild,

    [ValidateRange(1, 900)]
    [int]$WaitSeconds = 120
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AvenChart.Local.psm1') -Force

Assert-AvenChartDocker
$upArguments = @('up', '-d', $(if ($SkipBuild) { '--no-build' } else { '--build' }), 'frontend')
Write-Output 'Starting the reference UI and its database/API dependencies...'
Invoke-AvenChartCompose -Project Core -ComposeArguments $upArguments
Wait-AvenChartHttpEndpoint -Name 'API readiness' -Url 'http://localhost:5001/health/ready' -WaitSeconds $WaitSeconds
Wait-AvenChartHttpEndpoint -Name 'Reference UI' -Url 'http://localhost:3000/?entry=chooser' -WaitSeconds $WaitSeconds

Get-AvenChartEndpoints |
    Where-Object { $_.Name -in @('Reference UI', 'API readiness') } |
    Format-Table Name, Port, Url -AutoSize
