# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipApiCheck,

    [ValidateRange(1, 900)]
    [int]$WaitSeconds = 120
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AvenChart.Local.psm1') -Force

Assert-AvenChartDocker
if (-not $SkipApiCheck -and -not (Test-AvenChartHttpEndpoint -Url 'http://localhost:5001/health/ready')) {
    throw 'The AvenChart API is not ready. Run Start-AvenChartApi.ps1 first, or use Start-AvenChartAll.ps1.'
}

$upArguments = @('up', '-d', $(if ($SkipBuild) { '--no-build' } else { '--build' }))
Write-Output 'Starting the modern AvenChart UI...'
Invoke-AvenChartCompose -Project ModernUi -ComposeArguments $upArguments
Wait-AvenChartHttpEndpoint -Name 'Modern UI' -Url 'http://localhost:3100/' -WaitSeconds $WaitSeconds

Get-AvenChartEndpoints |
    Where-Object { $_.Name -in @('Modern UI', 'Professional sign-in', 'Patient portal') } |
    Format-Table Name, Port, Url -AutoSize
