# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AvenChart.Local.psm1') -Force

Assert-AvenChartDocker
Write-Output 'Starting the AvenChart PostgreSQL container...'
Invoke-AvenChartCompose -Project Core -ComposeArguments @('up', '-d', 'postgres')
Invoke-AvenChartCompose -Project Core -ComposeArguments @('ps', 'postgres')

$databaseEndpoint = Get-AvenChartEndpoints | Where-Object { $_.Name -eq 'PostgreSQL' }
$databaseEndpoint | Format-Table Name, Port, Url -AutoSize
