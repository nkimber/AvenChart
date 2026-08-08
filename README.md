# AvenChart

AvenChart is an experimental, independently branded electronic-health-record and practice-management application. This public repository contains the ASP.NET Core API, PostgreSQL migrations, two React frontends, deterministic synthetic demo data, deployment configuration, and a filtered record of the source-code history.

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

## Run locally

Prerequisites:

- Docker Desktop or a compatible Docker Engine with Compose
- PowerShell 7 and Node.js 24 for dataset generation and repository scripts

From the repository root, initialize the synthetic database once, then build and start every application:

```powershell
.\scripts\Reset-AvenChartDemoData.ps1 -Force
.\scripts\Start-AvenChartAll.ps1
```

The start command waits for readiness and prints every local URL and port. Open:

- Modern AvenChart UI: <http://localhost:3100/>
- Reference frontend: <http://localhost:3000/?entry=chooser>
- API readiness: <http://localhost:5001/health/ready>

Reuse the existing images on later starts:

```powershell
.\scripts\Start-AvenChartAll.ps1 -SkipBuild
```

Inspect or stop the complete environment with:

```powershell
.\scripts\Get-AvenChartStatus.ps1
.\scripts\Stop-AvenChartAll.ps1
```

See [`scripts/README.md`](scripts/README.md) for component-level scripts, Windows command-file launchers, parameters, and the full endpoint table. The stop script preserves the local PostgreSQL volume.

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
