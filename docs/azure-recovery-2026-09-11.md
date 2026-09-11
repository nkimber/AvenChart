# Azure deployment verification and recovery — 2026-09-11 UTC

The synthetic demo at https://avenchart.kimber.dev already had the latest
application commit, `c5aa807e02c13bbc6e6a50ddb137dbeb673c5e88`, deployed.
GitHub `main`, local `main`, the release revision name, and the registry image
tags agreed. No application rebuild or schema migration was required.

## Deployment identity

- Resource group: `rg-avenchart-demo-e82741ad`
- Container App: `avce82741ad-app`
- Revision: `avce82741ad-app--release-20260902-154446-c5aa807-bytes`
- UI digest: `sha256:7e1eae2e7f88e208078047fb5bfe7716eaff3ec6cd2f8cf7f6e5d6df051c11c2`
- API digest: `sha256:58c2b9a287944cdfa11dbfeb29c58c261bafecab6cc3b4be4638968d56f929c0`
- Both registry image tags: `release-20260902-154446-c5aa807-bytes`

## Incident and recovery

Public requests initially timed out. Both containers failed readiness despite
the revision summary reporting Healthy. PostgreSQL reported Ready in the Azure
control plane but had 100% CPU and failed direct application connections.
Subsequent PostgreSQL errors identified exhausted connection slots (SQLSTATE
53300). The server's configured connection maximum was 50.

Six obsolete revisions (`0000010` through `0000015`) were active with no traffic.
They were deactivated to remove unnecessary application instances and their
potential database load. Their precise contribution to connection exhaustion
was not measured.

Restarting the application did not recover the database. Two PostgreSQL restart
requests failed with Azure InternalServerError. The remaining application
revision was deactivated and reactivated, leaving one fresh replica. A full
PostgreSQL stop/start then succeeded and restored application readiness. The
database SKU, connection limit, image digests, and custom domain were unchanged.

## Verification

At 2026-09-11 02:35:54 UTC:

- Public `/`, `/health`, `/health/api/live`, and `/health/api/ready`: HTTP 200.
- UI and API containers: ready.
- Latest revision: Healthy, RunningAtMaxScale, 100% traffic; only active revision.
- PostgreSQL and synthetic telehealth readiness: healthy; 71 required tables present.
- Migration ledger: all 293 packaged migrations applied, no missing/unexpected
  migrations or checksum mismatches; latest `V0337__telehealth_video_sessions_per_reservation`.

This verifies deployment identity and service recovery, not authenticated
clinical workflows, two-person video calls, or sustained availability. The
underlying source of connection exhaustion remains unproven; deployment revision
cleanup and database connection/CPU monitoring warrant follow-up.
