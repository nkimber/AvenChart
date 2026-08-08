<!--
SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
SPDX-License-Identifier: GPL-3.0-or-later
-->

# Local Docker scripts

These scripts build and run the complete AvenChart development environment on Docker Desktop. Run them from any directory; each script resolves the repository root from its own location.

## First-time setup

From the repository root:

```powershell
# This intentionally replaces the local database with synthetic demo data.
.\scripts\Reset-AvenChartDemoData.ps1 -Force

# Builds images, creates/starts every container, waits for readiness,
# and prints all local URLs and ports.
.\scripts\Start-AvenChartAll.ps1
```

For subsequent starts, use `-SkipBuild` to reuse existing images:

```powershell
.\scripts\Start-AvenChartAll.ps1 -SkipBuild
```

Windows command-file launchers are also available:

```text
scripts\start-all.cmd
scripts\build-all.cmd
scripts\status.cmd
scripts\stop-all.cmd
```

The command files prefer PowerShell 7 (`pwsh.exe`) and fall back to Windows PowerShell when necessary.

## Commands

| Script | Purpose |
| --- | --- |
| `Start-AvenChartAll.ps1` | Master deployment: build and start both Compose projects, wait for readiness, and print every URL and port. |
| `Build-AvenChartContainers.ps1` | Build all images, or use `-Component Core` / `-Component ModernUi`. Add `-Pull` to refresh base images. |
| `Start-AvenChartDatabase.ps1` | Start PostgreSQL only. |
| `Start-AvenChartApi.ps1` | Start PostgreSQL, the one-shot schema migrator, and the API. |
| `Start-AvenChartReferenceUi.ps1` | Start the reference UI and its database/API dependencies. |
| `Start-AvenChartModernUi.ps1` | Start only the modern UI after confirming that the API is ready. |
| `Get-AvenChartStatus.ps1` | Show both Compose projects and probe every HTTP endpoint. |
| `Show-AvenChartUrls.ps1` | Print the local URL and port table without changing container state. |
| `Stop-AvenChartAll.ps1` | Stop all containers while preserving containers and database volumes. |
| `Reset-AvenChartDemoData.ps1` | Destructively replace the local database with the deterministic synthetic dataset. |

`Start-AvenChartAll.ps1` builds by default. Use `-SkipBuild` only after images have been built at least once. The migrator is a one-shot container and is expected to show `Exited (0)` after it completes successfully.

## Local endpoints

| Application | Port | URL |
| --- | ---: | --- |
| Modern UI | 3100 | <http://localhost:3100/> |
| Professional sign-in | 3100 | <http://localhost:3100/login> |
| Patient portal | 3100 | <http://localhost:3100/portal/login> |
| Reference UI | 3000 | <http://localhost:3000/?entry=chooser> |
| API readiness | 5001 | <http://localhost:5001/health/ready> |
| PostgreSQL | 5433 | `postgresql://localhost:5433/avenchart` |

The Compose defaults use the synthetic-only local database credentials documented in `avenchart/docker-compose.yml`. Do not use these defaults for production or protected health information.
