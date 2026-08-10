# AvenChart

AvenChart is an experimental, independently branded electronic-health-record and practice-management application built autonomously by coding agents. The latest OpenEMR source code serves as its functional specification: agents use it to understand behaviors and workflows, then implement them cleanly on a new technology stack rather than translating the existing code line by line. This public repository contains the ASP.NET Core API, PostgreSQL migrations, two React frontends, deterministic synthetic demo data, deployment configuration, and a filtered record of the source-code history.

> [!WARNING]
> AvenChart is not ready or authorized for production clinical use. It has not been certified for regulatory programs and must be evaluated independently for security, privacy, safety, accessibility, interoperability, and legal compliance. Use synthetic data only.

[Explore the public development history](https://nkimber.github.io/AvenChart/) · [License](LICENSE) · [Attribution](NOTICE.md) · [Security policy](SECURITY.md)

## Repository layout

| Path | Contents |
| --- | --- |
| [`avenchart/`](avenchart/) | ASP.NET Core 10 API, PostgreSQL migrations, reference React frontend, Docker Compose runtime, and application verification scripts |
| [`avenchart-ui/`](avenchart-ui/) | Independent React and TypeScript clinician and patient-portal interface for the same API |
| [`demo-data/`](demo-data/) | Deterministic synthetic dataset and generated database adapters |
| [`infra/`](infra/) | AvenChart deployment container definitions and web-server configuration |
| [`public-history/`](public-history/) | Static, read-only source history and statistics site |
| [`scripts/`](scripts/) | Docker Desktop build, component startup, full deployment, status, and shutdown commands |

## Docker Desktop quickstart

The [`scripts/`](scripts/) folder provides commands for building, starting, checking, resetting, and stopping the complete local AvenChart environment. The scripts can be run from any directory because they resolve the repository root from their own location; you do not need to run `docker compose` manually in each application folder.

Prerequisites:

- Docker Desktop, or a compatible Docker Engine with Compose, installed and running
- PowerShell 7 and Node.js 24 for dataset generation and repository scripts
- Local ports 3000, 3100, 5001, and 5433 available

### First-time setup

After cloning the repository, run these commands once from the repository root:

```powershell
# Starts PostgreSQL, replaces its contents with deterministic synthetic demo
# data, and applies all database migrations.
.\scripts\Reset-AvenChartDemoData.ps1 -Force

# Builds all application images, creates and starts every container, waits for
# readiness, and prints the local URLs and ports.
.\scripts\Start-AvenChartAll.ps1
```

On Windows, `scripts\start-all.cmd` is a convenient launcher for `Start-AvenChartAll.ps1` and can replace the second command. It builds by default on this first run.

The reset command is intentionally destructive to the local AvenChart database. Use only synthetic data; do not use it against a database containing information you need to preserve.

The first build downloads the required container base images and application dependencies, so it can take several minutes. When startup completes, open:

- Modern AvenChart UI: <http://localhost:3100/>
- Professional sign-in: <http://localhost:3100/login>
- Patient portal: <http://localhost:3100/portal/login>
- Reference frontend: <http://localhost:3000/?entry=chooser>
- API readiness: <http://localhost:5001/health/ready>
- PostgreSQL: `postgresql://localhost:5433/avenchart`

### Start the websites after initial setup

For normal later starts, reuse the existing container images and preserved database volume:

```powershell
.\scripts\Start-AvenChartAll.ps1 -SkipBuild
```

On Windows, the equivalent command-file launcher is:

```text
scripts\start-all.cmd -SkipBuild
```

To start only the reference frontend, including its PostgreSQL and API dependencies, run:

```powershell
.\scripts\Start-AvenChartReferenceUi.ps1 -SkipBuild
```

To start only the modern UI and its required backend, start the API first:

```powershell
.\scripts\Start-AvenChartApi.ps1 -SkipBuild
.\scripts\Start-AvenChartModernUi.ps1 -SkipBuild
```

### Check or stop the environment

```powershell
.\scripts\Get-AvenChartStatus.ps1
.\scripts\Stop-AvenChartAll.ps1
```

On Windows, `scripts\status.cmd` and `scripts\stop-all.cmd` provide the same operations. Stopping AvenChart preserves the containers and PostgreSQL volume, so the database does not need to be seeded again before the next start.

See [`scripts/README.md`](scripts/README.md) for every component-level script, optional parameter, Windows command-file launcher, and local endpoint.

## Azure deployment operations

Administrators can prepare, validate, plan, deploy, monitor, verify, and roll back
synthetic demo, development, and test environments from **Administration > Azure
operations** in the modern UI. Production deployment is deliberately blocked.

The page has a server-enforced second security gate in addition to the normal
administrator sign-in. New installations use the bootstrap Operations code
`AvenChartAdmin` and require it to be replaced before any Azure configuration can
be viewed. The replacement is stored as a salted password hash, and Operations
access grants remain only in browser memory and expire after 15 minutes.

Deployment execution is disabled by default. Review the prerequisites, least-
privilege requirements, safe enablement switch, conservative 20-user sizing, and
recovery workflow in [`infra/azure/operations/README.md`](infra/azure/operations/README.md).

## Build without Docker

```powershell
dotnet restore .\avenchart\AvenChart.slnx
dotnet build .\avenchart\AvenChart.slnx -c Release --no-restore

npm ci --prefix .\avenchart\frontend
npm run build --prefix .\avenchart\frontend

npm ci --prefix .\avenchart-ui
npm test --prefix .\avenchart-ui
npm run build --prefix .\avenchart-ui
```

## Public history

The repository preserves 749 application-source check-ins from the private project archive. Former internal product paths and commit subjects were normalized to AvenChart, commit bodies and non-source planning material were omitted, and source-only dates and authorship were retained. See [HISTORY.md](HISTORY.md) for the exact public-history boundary.

The static history site is generated from the retained Git graph:

```powershell
node .\tools\generate-history-data.mjs
```

## License and upstream attribution

Project-authored AvenChart software is licensed under [GNU GPL version 3 or later](LICENSE). See [NOTICE.md](NOTICE.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

AvenChart was developed with reference to the [original OpenEMR project](https://www.open-emr.org/) and its [public source code](https://github.com/openemr/openemr). We gratefully thank its maintainers, contributors, clinicians, implementers, documentarians, translators, testers, sponsors, and support community.

The OpenEMR name identifies that upstream source only. AvenChart is independent and is not affiliated with, sponsored by, certified by, or endorsed by the OpenEMR Foundation or community.
