# Azure database resilience implementation and deployment

Implemented the [incident-analysis recommendations](azure-database-incident-analysis-2026-09-11.md)
and verified the synthetic deployment on 2026-09-11 UTC.

## Changes delivered

- The effective pool uses the smaller connection-string or application ceiling;
  both deployed settings are 15. The safe application default is also 15.
- Deployment validation includes old/new replica overlap. The default is one
  replica, requiring at most 30 application connections during rollout against
  35 ordinary database slots. The UI displays this rollout budget.
- Container Apps uses Single revision mode. Rollback copies a retained healthy
  revision into a fresh rollout with the current pool ceiling. Deployment health
  verification waits for the latest revision to become ready rather than relying
  only on a successful ARM operation.
- Idle report polling is two seconds. Maintenance runs on a separate 15-second
  cadence. Repeated worker failures back off from two to 30 seconds, resetting
  after success. Existing request cancellation and scoped resource disposal remain.
- The UI readiness probe checks nginx; the API readiness probe retains PostgreSQL,
  migration-ledger and telehealth checks without duplicating them through nginx.
- Four database alerts cover CPU, CPU credits, connection pressure, and failed
  connections. A fifth alert covers a three-location standard HTTPS readiness
  test, checking HTTP 200, healthy response content, and the certificate.
- Alerts route to the subscription Owner role because no email recipient was
  supplied. Explicit recipients can be set using `alertEmails`. Notification
  delivery was not tested by sending a message.
- Database diagnostic export is configured to the existing 30-day Log Analytics
  workspace. Statement and bind-parameter logging are disabled. Query runtime/wait
  export categories are configured, but Query Store capture remains disabled on
  the burstable server; do not interpret absent query-store rows as healthy queries.
- The Linux container build normalizes and syntax-checks its shell entrypoint.

The database SKU and connection limit were not increased. Early post-rollout
load is below the saturation observed during the incident. Longer-term credit
and workload evidence should drive any later move to General Purpose compute.

## Release identity

- Application source: `c27a7459f8c985150381b651ed22b350261be743` (resilience
  implementation `fb071abcf2f5d125795bc40d106c69279db3cf44` plus entrypoint fix).
- Registry builds: API `ch14`, UI `ch12`, both succeeded.
- API: `avce82741adacr.azurecr.io/avenchart-api@sha256:ed5f1a5d35e1c90d97795357d7b00c29a0828813390e6a1e6d6b8700d746c90e`
- UI: `avce82741adacr.azurecr.io/avenchart-ui@sha256:cc2f281051ed577030d56d717294ab6751386c2f7070cace0909b85c0741bd8b`
- Azure deployment: `avenchart-db-resilience-c27a745`.
- Serving revision: `avce82741ad-app--0000017`.
- Public site: https://avenchart.kimber.dev
- Custom-domain binding, managed identity, private PostgreSQL networking, and
  synthetic telehealth configuration were preserved.

## Packaging integrity and rollout recovery

The existing database ledger hashes migration file bytes, including line endings.
An archived-source build initially differed from the deployed migration bytes.
Before rollout, all 293 migration files were checked for identical normalized SQL
against the committed source, then packaged with the byte-identical files verified
against the running revision. No migration SQL or ledger records were changed.

The SHA-256 of the ordered `sha256sum` migration manifest (filenames without the
directory prefix) is unchanged before and after deployment:
`4c63a1b4ebf3ddaecba528ad282189849142e9c7a3d7d740d2f0f34cf9cd4685`.

Revision `0000016` exposed a separate CRLF shell-shebang problem and could not
start its API. Single revision rollout retained the previous healthy revision;
public readiness checks continued returning 200. The Dockerfile entrypoint fix
was committed and rebuilt, and revision `0000017` replaced it successfully. Only
`0000017` remains active. This exercised failure containment during deployment,
not a deliberate rollback of a healthy application.

For future releases, verify migration bytes against the deployed ledger before
building from a different operating system. Do not normalize ledgered migrations
or rewrite their checksums as a workaround. The shell script is normalized during
the build; migrations deliberately are not.

## Verification

- 821 backend unit tests passed, including effective pool precedence, invalid
  pool bounds, maintenance cadence, failure backoff/reset, and rollback selection.
- Four Azure Operations frontend API tests passed; frontend build, bundle budget,
  lint, and scoped C# formatting checks passed.
- Azure Operations integration verification passed against a fresh isolated
  database with all 293 migrations, including rollover connection-budget rejection.
  The test harness was repaired to wait for PostgreSQL, declare its local
  Development environment, and build the current migrator rather than reuse a
  stale image. Earlier setup failures were not counted as passing tests.
- Bicep compilation and Azure what-if completed. Monitoring and application
  deployments succeeded. The graph was refreshed and its portability checked.
- Both containers in `0000017` are started and ready, with zero restarts. It is the
  only active revision and receives 100% of traffic.
- Twenty public readiness requests at concurrency five all passed: average 278 ms,
  maximum 773 ms. The homepage and its entry assets returned HTTP 200.
- Deployed configuration inspection confirmed pool 15, polling 2000 ms,
  maintenance 15 seconds, and maximum failure backoff 30 seconds.
- Readiness confirms PostgreSQL, all 293 migration checksums, and all 71 required
  synthetic telehealth tables are healthy.
- At 04:04–04:06 UTC, minute CPU averages were 11.93%, 14.90%, and 13.24%; connection
  averages were 11, 11.5, and 12.5. These include rollout/warmup and verification
  activity and are a short observation, not a sustained-load acceptance test.
- The new public monitor recorded nine successful checks before the final rollout.
  New-revision logs through 04:06:40 UTC showed no worker failures or connection
  exhaustion. Log/metric ingestion can lag real time.

Authenticated clinical workflows, end-to-end report execution, two-person calling,
alert-message delivery, and a prolonged availability/load soak were not exercised
by this deployment verification.
