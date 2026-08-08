# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AvenChart.Local.psm1') -Force

Assert-AvenChartDocker
Write-Output 'Stopping the modern AvenChart UI...'
Invoke-AvenChartCompose -Project ModernUi -ComposeArguments @('stop')

Write-Output 'Stopping the AvenChart reference UI, API, and database...'
Invoke-AvenChartCompose -Project Core -ComposeArguments @('stop')
Write-Output 'All AvenChart containers are stopped. Containers and database volumes were preserved.'
