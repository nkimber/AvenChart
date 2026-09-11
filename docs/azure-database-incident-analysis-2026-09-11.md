# PostgreSQL connection exhaustion investigation — 2026-09-11

## Conclusion

The incident was connection exhaustion during sustained database overload, not
the database losing all connections. The strongest explanation is accumulated
active application revisions generating continuous work on a small burstable
server, compounded by a pool configuration bug. CPU-credit depletion preceded
the sustained saturation. The precise query/session that first triggered the
cascade cannot be reconstructed from the retained evidence.

This investigation made no Azure configuration or application-code changes.
The preceding recovery is recorded in [the recovery log](azure-recovery-2026-09-11.md).

## Confirmed configuration defects and contributors

1. **The intended pool limit is overwritten.** The deployed connection-string
   secret specifies `Maximum Pool Size=15`. `Program.cs:251-259` rebuilds that
   string and unconditionally assigns `MaxPoolSize` from `DatabaseConnection`
   options. The running image's `/app/appsettings.json` specifies 100; there is
   no corresponding Azure environment override. Thus the current API's configured
   ceiling is 100, not 15. The ceiling is not a count of connections preallocated.
   The singleton data source is shared with EF Core; these are not two separate
   pools in the inspected registration.
2. **Revision count was not bounded.** `application.bicep:60` selects Multiple
   revision mode and line 300 retains at least one replica for this deployment.
   Before recovery, revisions `0000010`–`0000015` and the current release were
   active. The six obsolete revisions had zero ingress traffic, but historical
   logs confirm report-worker failures and database-readiness failures from all
   seven. Zero traffic did not stop their database activity. The deployment
   coordinator has no successful-release deactivation step.
3. **Database capacity and application budgets do not agree.** Live PostgreSQL
   settings are `max_connections=50`, `superuser_reserved_connections=10`, and
   `reserved_connections=5`: only 35 slots are available to ordinary roles.
   Even a single 100-connection pool can exceed that budget. Seven replicas at
   the intended 15 limit would allow 105 connections. The deployment policy at
   `AzureDeploymentProfilePolicy.cs:106-114` multiplies the intended pool by
   maximum replicas, but does not include simultaneously active revisions or
   the application override.
4. **Continuous work is substantial for this demo.** The deployed report-worker
   interval is 250 ms. Each idle iteration calls `MaintainAsync` and
   `ClaimNextAsync`, opening two sequential transactions, executing five
   maintenance commands and a claim command. Seven workers can approach 28
   iterations/second before query time is included. This is an estimate from
   configuration, not a measured transaction rate. Each revision also has API
   and UI-proxied readiness probes every 10 seconds, exercising PostgreSQL,
   migration validation, and telehealth schema checks. Migration validation's
   cache lasts one second. Worker errors retry after the same idle delay rather
   than a progressively longer failure backoff.
5. **Burstable CPU headroom was depleted.** The server is Standard_B1ms. Its
   hourly CPU-credit metric declined from about 298 on September 1 to a floor
   of 1 by September 4 00:00 UTC, remaining there through the incident. This
   supports credit starvation; the observed value was 1, not literally zero.
   Microsoft documents severe performance and connection/management-operation
   failures when burstable CPU credits are exhausted.
6. **Detection and forensic coverage are incomplete.** No metric alerts exist
   in the deployment resource group, and the database has no Azure Monitor
   diagnostic settings. Container logs and platform metrics were available.
   `pg_stat_statements` is preloaded but its extension is not installed in the
   application database. No pre-restart `pg_stat_activity` snapshot exists.

## Historical evidence

Azure Monitor hourly data, summarized by UTC date:

| Date | Mean of hourly CPU averages | Mean hourly CPU credits | Mean available hourly connection averages |
|---|---:|---:|---:|
| September 1 | 27.24% | 259.78 | 29.29 |
| September 2 | 28.13% | 176.71 | 29.80 |
| September 3 | 31.54% | 65.19 | 33.27 |
| September 4 | 92.44% | 1.00 | 36.89 |
| September 7 | 98.76% | 1.00 | 40.15 |
| September 10 | 99.83% | 1.00 | 47.13 |

Connection samples become sparse during severe overload. Missing values are not
zeros, and this metric is not a count of executing SQL statements. Counts include
server connections beyond the application's ordinary-role budget.

Container logs on September 10 contain 39,043 report-worker failure entries,
112,337 PostgreSQL readiness-failure entries, and 3,535 lines matching connection
exhaustion. These are log-line counts, not unique failed requests. Across
September 4–11 02:00 UTC, each of the seven revisions logged roughly 19,000
worker failures, including every obsolete revision.

During recovery, a direct psql connection returned SQLSTATE 53300 / remaining
connection slots reserved for SUPERUSER. Private DNS resolved to the correct
private address and TCP port 5432 was reachable. Two Azure restart operations
failed with InternalServerError; a full stop/start succeeded. Those errors are
consistent with the documented behavior under burstable exhaustion, but Azure
did not provide a definitive internal cause for the failed management requests.

After recovery, at 02:39–02:43 UTC, CPU was 13.76–20.01%, connections 10–13.5,
and available credit samples were 30. A direct activity snapshot contained ten
client backends: nine idle and this investigation's active query. Both reducing
revisions and restarting the server occurred, so their individual effects cannot
be separated experimentally.

## Likely sequence and limits

Always-running old revisions multiplied polling and readiness work. Sustained
load consumed the small server's CPU credits. Slow database work held connections
longer while probes and workers continued trying. An ineffective pool budget
allowed pressure to reach the database's connection limit, causing more failures
and retries. This is strongly supported by the timing, source, logs, and recovery
comparison, but does not prove an individual connection leak or identify a
specific expensive query. The inspected worker and health-check paths use
asynchronous disposal; there is no demonstrated leak in those paths.

## Recommended remediation, in order

1. Make the effective API pool limit 15 and fix the conflicting configuration
   authorities so deployment validation assesses the limit actually used. Budget
   all simultaneous replicas/revisions, migration jobs, and operator connections
   against the 35 ordinary slots; leave explicit headroom. Test precedence.
2. For this single-instance demo, use Single revision mode or enforce old-revision
   deactivation after a successful rollout. Account for rollout overlap, and
   preserve a tested rollback procedure. Cleanup during recovery is not a durable
   deployment-policy fix.
3. Reduce idle report polling (for example 2–5 seconds if acceptable), separate
   maintenance cadence from claim polling, and add bounded failure backoff. Avoid
   duplicated expensive readiness work while retaining truthful dependency checks.
4. Alert on low CPU credits, sustained CPU, connection-budget pressure, and public
   readiness failures. Enable appropriate PostgreSQL diagnostic retention and
   query-performance evidence without logging sensitive parameters.
5. Measure steady state after these fixes. If the workload still consumes credits
   faster than they recover, move to a suitably sized General Purpose server.
   Increasing only `max_connections` on the same small server is not a sound fix.

## Primary documentation

- [Azure compute tiers and burstable behavior](https://learn.microsoft.com/en-gb/azure/postgresql/compute-storage/concepts-compute)
- [PostgreSQL connection limits and resource costs](https://learn.microsoft.com/en-us/azure/postgresql/configure-maintain/concepts-limits)
- [Container Apps revision lifecycle](https://learn.microsoft.com/en-ca/azure/container-apps/revisions)
- [Per-revision replica scaling](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)
- [Npgsql pooling parameters](https://www.npgsql.org/doc/connection-string-parameters)
