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
$upArguments = @('up', '-d', $(if ($SkipBuild) { '--no-build' } else { '--build' }), 'api')
Write-Output 'Starting PostgreSQL, the one-shot migrator, and the AvenChart API...'
Invoke-AvenChartCompose -Project Core -ComposeArguments $upArguments
Wait-AvenChartHttpEndpoint -Name 'API readiness' -Url 'http://localhost:5001/health/ready' -WaitSeconds $WaitSeconds

Get-AvenChartEndpoints |
    Where-Object { $_.Name -in @('API readiness', 'PostgreSQL') } |
    Format-Table Name, Port, Url -AutoSize
