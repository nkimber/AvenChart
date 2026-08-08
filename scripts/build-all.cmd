@rem SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
@rem SPDX-License-Identifier: GPL-3.0-or-later
@echo off
where pwsh.exe >nul 2>&1
if errorlevel 1 goto windows_powershell
pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-AvenChartContainers.ps1" %*
exit /b %errorlevel%

:windows_powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-AvenChartContainers.ps1" %*
exit /b %errorlevel%
