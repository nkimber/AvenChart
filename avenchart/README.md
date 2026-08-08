# AvenChart application

This directory contains the primary AvenChart runtime:

- ASP.NET Core 10 API in `backend/src/AvenChart.Api/`;
- PostgreSQL schema and versioned migrations in `database/`;
- React reference frontend in `frontend/`;
- Docker Compose services for PostgreSQL, migrations, API, and frontend; and
- PowerShell verification and maintenance scripts in `scripts/`.

The root [README](../README.md) contains supported local startup and build commands. The application uses only the deterministic synthetic dataset under [`demo-data/`](../demo-data/) for public examples.

AvenChart is licensed under GPL-3.0-or-later. It is independent and was developed with reference to the [original OpenEMR project](https://www.open-emr.org/); see the repository [notice](../NOTICE.md) for complete attribution and the healthcare disclaimer.
