# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [ValidateSet('All', 'Core', 'ModernUi')]
    [string]$Component = 'All',

    [switch]$Pull
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AvenChart.Local.psm1') -Force

Assert-AvenChartDocker
$buildArguments = @('build')
if ($Pull) {
    $buildArguments += '--pull'
}

if ($Component -in @('All', 'Core')) {
    Write-Output 'Building the API and reference UI images...'
    Invoke-AvenChartCompose -Project Core -ComposeArguments ($buildArguments + @('api', 'frontend'))
}

if ($Component -in @('All', 'ModernUi')) {
    Write-Output 'Building the modern UI image...'
    Invoke-AvenChartCompose -Project ModernUi -ComposeArguments $buildArguments
}

Write-Output "AvenChart container build completed for: $Component."
