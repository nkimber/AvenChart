# SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'AvenChart.Local.psm1') -Force

Assert-AvenChartDocker
Write-Output 'Core containers:'
Invoke-AvenChartCompose -Project Core -ComposeArguments @('ps', '-a')
Write-Output ''
Write-Output 'Modern UI container:'
Invoke-AvenChartCompose -Project ModernUi -ComposeArguments @('ps', '-a')
Write-Output ''
Write-Output 'HTTP endpoint status:'
$table = Get-AvenChartHttpStatus | Format-Table Name, Port, Status, Url -AutoSize | Out-String
Write-Output $table.TrimEnd()
