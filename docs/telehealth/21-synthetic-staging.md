# Synthetic staging environment

This Docker Compose stack is for synthetic-data telehealth demonstration and verification only. It is not a clinical or production service.

## Safety boundary

- It starts the API in the `Staging` environment, which permits the telehealth feature only in its enforced `Synthetic` mode.
- The video, pharmacy-directory, and professional-claim adapters are all pinned to `NON_PRODUCTION`.
- PostgreSQL and the API have no host-published ports. The modern UI is the only published service, bound to `127.0.0.1` by default.
- The stack has an isolated, named PostgreSQL volume and does not share the development Compose database.
- Do not load patient information, credentials, payer data, prescription data, or any other protected information into this environment.

## Start

From the repository root, copy `staging.env.example` to `.env.staging`, replace the password placeholder with a random local value, then run:

```powershell
docker compose --env-file .env.staging -f docker-compose.staging.yml up --build --wait
```

Open <http://127.0.0.1:8088/>. Readiness is available at <http://127.0.0.1:8088/health/ready>.

The isolated volume starts empty. To load the repository's synthetic gold dataset for a local demonstration, explicitly reset that staging volume after startup:

```powershell
.\avenchart\scripts\Reset-AvenChartTelehealthPocStaging.ps1 -ResetStagingData
```

This operation drops and recreates only the `avenchart_staging` schema in the isolated staging volume, imports the repository's synthetic dataset, reapplies migrations, and restarts the API/UI. It must never be pointed at a development or production database.

## Validate and stop

```powershell
Invoke-RestMethod http://127.0.0.1:8088/health/ready
docker compose --env-file .env.staging -f docker-compose.staging.yml ps
docker compose --env-file .env.staging -f docker-compose.staging.yml down
```

To discard the synthetic staging database as well, use `down --volumes`. That deletion is irreversible for the staging volume.
