# EXT-S001 Packet 2 — EF Core and SQL fitness

## Packet

- Source challenge: `EXT-S001-C03`
- Status: evidence complete and independently verified
- Baseline tag: `phase-1-experimental`
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Review date: 2026-08-21
- Reviewer: `phase2_data`
- Independent verifier: `phase2_verifier`
- Evidence level: Level 0 repository and static inspection; targeted Level 1 checks when proportionate
- Product worktree: `avenchart/` had no status entry or diff from the baseline when the packet launched
- Assessment worktree: Phase 2 documentation and workbench changes were present and are not treated as baseline product evidence
- Tool environment: Git 2.53.0.windows.1; .NET SDK 10.0.400; Node.js 24.13.1; npm 11.8.0; PowerShell 7.6.4
- Runtime limit: `psql` was not on `PATH`; the Docker client was present but no server or local containers were available

## Scope and coverage

| Coverage | Packet concern |
| --- | --- |
| `COV-008` | EF Core entities/configurations, SQL repositories, mapping fidelity, transactions, concurrency, cancellation, observability, testing, and the documented hybrid boundary |
| `COV-009` | PostgreSQL migrations, ledger and ordering, schema ownership, seed adapters, backup, restore, replay, and recovery evidence |

Primary domain is 04 Data and persistence. Domains 01, 02, 06, 09, 10, 11, and 12 are supporting lenses only where needed to assess fitness or verification.

## Representative categories

1. Ordinary EF-backed entity retrieval and mutation, including a simple aggregate.
2. An EF-backed state repository with optimistic concurrency or explicit transaction behavior.
3. Ordinary parameterized-SQL CRUD that could plausibly be expressed through EF Core.
4. Complex joined or reporting SQL.
5. Bulk, import, or set-based work.
6. Cross-table workflow transactions and PostgreSQL-specific locking, leases, or idempotency.
7. SQL migration authority, EF mapping fidelity, ordering, duplicate numeric prefixes, replay, failure recovery, backup, and restore.
8. Cancellation, async I/O, command timeouts, retries, disposal, generated SQL observability, query plans, indexes, and representative-volume assumptions.

## Evidence questions

1. For each representative category, is EF Core or parameterized SQL the clearest, safest, most observable, testable, performant, and proportionate expression of the actual requirement?
2. Where ordinary SQL is not justified, what concrete mapping, lifecycle, concurrency, transaction, or maintenance burden exists?
3. Where SQL is appropriate, which reporting, bulk, database-specific locking, idempotency, or cross-table property supports it?
4. Do adopted EF boundaries correctly map the database-first schema and preserve concurrency, timestamps, defaults, keys, and transaction behavior?
5. Does the documented hybrid policy match the code, migrations, and verification evidence?
6. Which conclusions require generated query plans, synthetic concurrency, migration replay, or database/operations validation that is unavailable in this environment?

## Exclusions and limits

- EF adoption percentage, SQL-statement count, file size, and framework fashion are not quality scores.
- This packet does not prescribe blanket SQL removal, repository splitting, schema-authority changes, or an ORM rewrite.
- It does not repeat Packet 1's general structure assessment except to deduplicate persistence-specific conditions.
- No real or production data, credentials, deployment, database reset, migration execution, backup restore, or product modification is permitted.
- Query-plan, load, concurrency, and recovery conclusions remain limited unless supported by existing reproducible evidence.

## Results

### Specialist challenge outcome

`EXT-S001-C03` is `partially corroborated`. Greater EF Core adoption is not a general quality objective, and the sampled hybrid boundary is substantially implemented. The material conditions concern shared schema and invariant governance plus one SQL bulk path that is not actually set-oriented—not parameterized SQL itself.

### Representative boundary assessment

| Category | Sample | Specialist assessment |
| --- | --- | --- |
| Simple entity CRUD | `AddressBookRepository.SaveAsync` and `DeleteAsync` | Stay EF |
| EF state and concurrency | `PatientRecordRequestRepository` and `EncounterStateRepository` | Stay EF |
| Plausibly ordinary SQL | Patient contact and demographic mutation | Undecidable; EF is plausible, but no concrete defect attributable to SQL was established |
| Joined and reporting | `ReportRepository.GetGovernedFamilyCsvAsync` | Stay SQL |
| Bulk and import | `ProcedureRepository.ImportOrderCatalogCompendiumAsync` | SQL remains appropriate; current row-at-a-time implementation is not proportionate to an unbounded bulk path |
| Locking and lease | `ReportExecutionQueueRepository.ClaimNextAsync` | Stay SQL |
| Idempotency | `IntegrationRepository` inbox and outbox operations | Stay SQL |
| Schema and migration | Seed-owned base schema plus versioned SQL migrations | Material governance condition found |

### Methods and actual results

The initial `git rev-parse phase-1-experimental^{}` invocation was misparsed by PowerShell and failed with `fatal: ambiguous argument ''`. The reviewer preserved the failed attempt and then resolved the baseline with:

```powershell
git rev-list -n 1 phase-1-experimental
git show-ref --tags -d phase-1-experimental
git diff --name-only d77a8320e6751a2deb2daf14cf1ac5d6b00cb989 -- avenchart
```

The baseline resolved to `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`, the annotated tag and peeled commit were confirmed, and the product-diff command produced no output.

Level 1 checks were:

```powershell
dotnet restore .\avenchart\AvenChart.slnx
dotnet build .\avenchart\AvenChart.slnx -c Release --no-restore
```

Restore reported all projects current. The Release build passed with zero warnings and zero errors in 1.49 seconds.

Static inventory found 53 repository files, 41 EF entities, 41 configurations, and 41 `DbSet` entries. Fourteen repositories reference `AvenChartDbContext` and 42 reference `NpgsqlDataSource`; these overlap and are inventory, not quality metrics. The migration catalog contains 201 SQL files.

Static migration-resilience inspection found:

- no repository-time schema DDL;
- no repository `MAX(...) + 1` allocator;
- no literal-key use of `avenchart_next_integer`;
- no EF configuration missing `ExcludeFromMigrations`;
- no entity missing a configuration or `DbSet`;
- no runtime `Database.Migrate` or `EnsureCreated`;
- no invalid migration filename or duplicate full migration ID.

Two `V0110` and two `V0112` numeric prefixes exist. `SchemaMigrationCatalog` orders complete filenames ordinally and keys the ledger by complete basename. The retained migration artifact lists all four complete IDs. The prefixes therefore do not collide under this runner.

Repository searches found no synchronous Npgsql connection, command, transaction commit, or rollback calls. No EF execution retry, generated-SQL capture, `EXPLAIN`, `ToQueryString`, `pg_stat_statements`, or EF SQL logging was found. Azure connection strings explicitly set a 30-second command timeout; no equivalent repository-wide local policy was located. These observations are evidence gaps or environment differences, not automatic defects.

No PostgreSQL runtime was available: `psql` was absent and Docker daemon access failed. The packet therefore produced no new query plan, timing, concurrency experiment, migration replay, or backup/restore rehearsal. Retained Phase 1 artifacts are historical supporting evidence only: 201 ready migrations, migration checkpoint 1 with 23 scenarios, and synthetic volumes of 1,000 patients, 2,800 appointments, 2,100 encounters, and 2,400 lab results. Operational readiness records backup/restore rehearsal as `not-run`.

### Material strengths and counterexamples

- The documented hybrid policy is implemented rather than aspirational: `Persistence/README.md:5-6` assigns ordinary entity CRUD to EF and reporting, bulk, locking, and multi-table workflows to Npgsql.
- `AddressBookRepository` uses EF for ordinary external-contact state while retaining SQL for a unioned internal/external search with `ILIKE`, window count, ordering, and a bounded projection.
- `PatientRecordRequestRepository` uses EF relationships, a database partial-uniqueness rule, an explicit concurrency token, cancellation, and conflict translation.
- `EncounterStateRepository` uses EF tracking for summary, archive, and vital mutations; advances `row_version`; translates `DbUpdateConcurrencyException`; and writes state plus audit records through one `SaveChangesAsync` transaction.
- `ProcedureDirectoryRepository` appropriately uses EF for individual directory and catalog mutation, while queue, compendium, and clinical-result workflows remain SQL.
- `ReportRepository.GetGovernedFamilyCsvAsync` uses bounded whitelisted branches, parameters, and a 5,000-row limit.
- `ReportExecutionQueueRepository.ClaimNextAsync` uses one `FOR UPDATE SKIP LOCKED` claim/update with lease, expiry, attempt state, and event write inside an explicit transaction.
- `IntegrationRepository` uses uniqueness and `ON CONFLICT` for inbox/outbox idempotency and explicit lifecycle transactions.
- Sampled EF and Npgsql code consistently uses asynchronous I/O, cancellation tokens, and `await using` disposal.
- SQL migrations run one file per transaction, use SHA-256 ledger entries and a PostgreSQL advisory lock, and reject missing, unexpected, or changed ledger rows.

## Candidate finding 1 — Schema readiness depends on a seed-owned base schema outside the migration ledger

- Status: validated
- Domain(s): 04, 10, 11, 12
- Coverage item(s): `COV-009`
- Severity: medium
- Production blocker: unknown
- Reach: systemic
- Confidence: high for the static condition; medium for operational consequence
- Reviewer: `phase2_data`
- Independent verifier: `phase2_verifier`
- Specialist validation: database/operations
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

### Condition

Observed fact: persistence documentation says `database/migrations` is the only schema authority, but foundational tables are created by the deterministic seed generator outside the migration ledger. The supported reset installs this seed-owned schema before applying migrations. Migration readiness validates only migration IDs and checksums.

Inference: the packaged migration catalog cannot independently bootstrap a genuinely empty PostgreSQL database and cannot prove the shape of seed-owned tables.

### Evidence

- Claimed authority: `Persistence/README.md:9`.
- The seed generator contains 92 `CREATE TABLE` statements and 74 indexes. Representative foundational DDL is at `generate-postgres-seed.mjs:449` facilities; `:462` staff; `:606` patients; `:1057` appointments; `:1083` encounters; `:1568` lab order catalog.
- Reset order: `Seed-AvenChartGoldDataset.ps1:86-114` recreates `public`, loads generated seed SQL, then invokes migrations.
- Azure deployment is also two-stage when demo seed is enabled: `AzureOperationsServices.cs:397-405` runs the seed job before migration. No equivalent foundational bootstrap was found when that flag is disabled.
- `V0005__inventory_core.sql:15-18` adds a foreign key to `facilities`, although no earlier migration creates that table.
- `SchemaMigrationState.cs:72-101` compares migration ledger IDs and checksums only.
- `Test-AvenChartMigrationResilience.ps1:1300-1304` intentionally renames a patient column and expects the affected request to fail with 503; it does not establish readiness detection of changed base-table shape.
- Historical readiness proves a seeded demo reached 201 migrations, not that migrations construct or validate the complete schema.

Expected result: a declared sole schema authority should account for creation and validation of the complete durable schema, or accurately identify and govern every prerequisite schema authority.

### Consequence

Fresh-environment provisioning depends on an additional schema artifact not represented in migration readiness. Base-table changes can evade migration checksums, and a ledger-complete database can remain structurally incompatible until a runtime request fails. Production migration, recovery, and schema-drift reasoning therefore cannot rely on the ledger alone.

### Cause and reach

The demo seed acts as both schema bootstrap and synthetic-data adapter while later evolution moved into migrations. The resulting two-stage authority affects base tables, new environments, migration replay, readiness, and recovery. Deterministic version-controlled generation and an explicit supported demo reset are meaningful controls.

### Risk calibration

- Impact: failed provisioning, false-positive readiness, runtime schema errors, or ambiguous recovery ownership
- Likelihood or preconditions: a new non-demo database, base-schema drift, or recovery outside the supported seeded workflow
- Detectability: high during failed empty bootstrap; weaker when a ledger-complete database has base-table drift
- Reversibility: potentially difficult after real data exists; no production migration path is approved
- Severity rationale: medium because the condition is systemic but supported present use is an explicitly seeded synthetic demo

### Uncertainty and counterevidence

The supported first-time workflow explicitly requires deterministic demo reset. Generator source and generated SQL are version controlled, and the retained demo artifact reports all 201 migrations healthy. No empty-database migration run was possible. The real-data rehearsal contract remains owner-approval-required and does not establish target base-schema construction.

### Validation record

- Independent method: separate source-order trace, schema-DDL inventory, migration catalog inspection, readiness-scope inspection, and deployment-order trace; isolated empty-database execution unavailable
- Result: `corroborated`
- Reviewer agreement or dispute: agreement after narrowing the description to a governed two-stage synthetic bootstrap that contradicts the sole-authority claim and lacks migrations-only bootstrap
- Specialist conclusion or outstanding need: database/operations validation required

### Disposition

Assigned canonical ID `P2-04-F001` after independent verification and coordinator deduplication. No implementation recommendation is accepted. Database/operations validation remains required before deciding the intended production schema-authority model.

## Candidate finding 2 — Procedure-catalog parentage and logical identity are enforced only by race-prone application prechecks

- Status: validated
- Domain(s): 04
- Coverage item(s): `COV-008`, `COV-009`
- Severity: medium
- Production blocker: unknown
- Reach: isolated
- Confidence: high for static feasibility
- Reviewer: `phase2_data`
- Independent verifier: `phase2_verifier`
- Specialist validation: clinical and database/operations
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

### Condition

`lab_order_catalog` has no foreign key from `parent_id` to its parent row and no uniqueness constraint for the logical import identity `(parent_id, code, item_type)`. EF single-item mutation validates context before saving but does not check tuple duplication; SQL import selects an existing row before inserting or updating. The delete uses one generated `DELETE ... NOT EXISTS` predicate, which protects only against already committed visible children. The database cannot preserve the hierarchy or logical identity across concurrent operations.

### Evidence

- Seed DDL at `generate-postgres-seed.mjs:1568-1581` defines the primary key and provider foreign key but no parent foreign key or logical uniqueness.
- `LabOrderCatalogConfiguration.cs:14-31` maps the table and provider relationship only.
- `ProcedureDirectoryRepository` create, update, delete, and context validation: `:19-83` and `:201-216`.
- Import lookup and upsert: `ProcedureRepository.cs:2639-2732`.
- The import lookup orders duplicate matches by descending ID and selects one.
- Two ordinary sequential EF creates with the same parent, code, and item type can both succeed.
- Two imports can both observe no matching tuple and insert distinct rows. A child creator can validate a parent, race a parent delete, and then insert an orphan because no foreign key exists.
- `Test-AvenChartMigrationResilience.ps1:893-988` proves sequential CRUD and sequence defaults but not duplicates, orphans, or concurrency.
- No catalog uniqueness migration, constraint, or trigger was located.

Expected result: durable parentage and logical identity assumed by mutation code should survive concurrent create, import, and delete timing.

### Consequence

Concurrent writes can create multiple active rows for one imported identity or a child whose parent was concurrently deleted. Directory and import code can then select an arbitrary latest row. Any clinical consequence from catalog ambiguity requires clinical validation.

### Cause and reach

EF administration and SQL import share invariants held only in application prechecks. The condition is localized to the procedure catalog but crosses both mutation mechanisms.

### Risk calibration

- Impact: ambiguous or orphaned catalog entries and inconsistent selection
- Likelihood or preconditions: overlapping administration/import requests or parent deletion during create/import
- Detectability: duplicates may appear in lists; orphaning produces no database error
- Reversibility: data cleanup is possible before downstream use but can become ambiguous afterward
- Severity rationale: medium for data correctness; clinical severity remains unvalidated

### Uncertainty and counterevidence

The synthetic dataset contains only 21 catalog items. Sequential EF CRUD passed historically, and the delete predicate blocks ordinary sequential parent deletion. Actual administrative concurrency and semantic-uniqueness requirements are undocumented. A disposable concurrency exercise is required to establish exact outcomes and likelihood.

### Validation record

- Independent method: separate search across all migrations, seed constraints, EF configuration, triggers, and catalog writers, followed by exact interleaving analysis
- Result: `corroborated`
- Reviewer agreement or dispute: agreement; the verifier corrected the delete description and established that sequential duplicate EF creates are also possible
- Specialist conclusion or outstanding need: database/operations reproduction and clinical review

### Disposition

Assigned canonical ID `P2-04-F002` after independent verification and coordinator deduplication. Greater EF adoption would not by itself enforce the missing invariant. Database/operations and clinical-informatics validation remain outstanding for runtime likelihood and any patient-safety consequence.

## Candidate finding 3 — The compendium bulk import performs an uncapped series of per-row database commands

- Status: validated
- Domain(s): 04, 06, 09
- Coverage item(s): `COV-008`
- Severity: low
- Production blocker: no
- Reach: isolated
- Confidence: high for command growth; low/medium for workload impact
- Reviewer: `phase2_data`
- Independent verifier: `phase2_verifier`
- Specialist validation: database/operations
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

### Condition

`ImportOrderCatalogCompendiumAsync` parses all accepted CSV rows into memory and awaits individual lookup/update or lookup/sequence/insert commands for every row inside one transaction. No application row cap, batching, or focused representative-volume test was found.

### Evidence

- Import and transaction: `ProcedureRepository.cs:708-849`.
- Serial row loop and upserts: `:748-798`.
- Full CSV parsing: `:2439-2508`.
- Per-row lookup and write: `:2639-2732`.
- Corrected static lower bounds, including response metadata and catalog queries, are `3N + 4` explicit commands for existing YMPG/DPMG, `4N + 4` for new YMPG/DPMG, `4N + U + 4` for existing PathGroup, and `5N + 2U + 4` for new PathGroup orders/results, where `N` is accepted rows and `U` is distinct order IDs. Transaction begin and commit add two provider round trips. These are command counts, not latency measurements.
- No focused test invocation of `/api/procedures/order-catalog/import-compendium` was located.
- Available catalog volume is 21 synthetic rows; no vendor-compendium size or latency target is documented.

Expected result: a path classified as bulk import should have work and transaction duration proportionate to representative input size with focused verification of its selected SQL mechanism.

### Consequence

At larger vendor volumes, latency and transaction duration grow with network command count, increasing timeout, cancellation, and lock-retention exposure. No measured user-visible or capacity failure is claimed.

### Cause and reach

The import reuses single-row lookup and upsert helpers rather than a set-oriented boundary. SQL remains the more suitable mechanism; moving the loop to EF would not address the condition.

### Risk calibration

- Impact: slow import, extended transaction lifetime, and possible timeout or contention
- Likelihood or preconditions: compendia materially larger than 21 rows or nontrivial database round-trip latency
- Detectability: measurable through command counts, timing, and database activity
- Reversibility: high; the import is transactional and propagates cancellation
- Severity rationale: low because command growth is certain but representative volume and measured latency are unknown

### Uncertainty and counterevidence

The import is atomic and cancellable. Current synthetic volume is small. No query plan, timing, target size, or concurrent workload is available. The condition should become an opportunity rather than a defect if representative measurement proves negligible cost.

### Validation record

- Independent method: separate command-site trace across the repository, DTO, endpoint, and UI plus cap, batch, `COPY`, temporary-table, and focused-test searches
- Result: `corroborated`; the verifier corrected the original YMPG lower bound because every distinct imported order also deactivates result children
- Reviewer agreement or dispute: agreement on the condition and low severity; measured workload impact remains unknown
- Specialist conclusion or outstanding need: database/operations measurement

### Disposition

Assigned canonical ID `P2-04-F003` after independent verification and coordinator review. Do not prescribe EF conversion: bulk/set-oriented work belongs on the SQL side, while this implementation still needs bounded or measured evidence.

## Unknowns and counterevidence

- No query plans exist for sampled EF-generated queries, joined reports, appointment projections, or import SQL.
- No target concurrency, database size, latency objective, or vendor-compendium size is documented.
- Azure connection strings set a 30-second command timeout. The local path has no equivalent explicit policy in repository configuration, and deployment-supplied options remain possible.
- No EF/Npgsql transient execution policy was found. Retrying arbitrary mutations may be unsafe, so absence is not itself a defect.
- No generated EF SQL capture or database query attribution was located.
- The environment could not exercise isolation, locking, connection failure, migration replay, or restore.
- Retained migration resilience exercised checkpoint 1 rather than current checkpoints 1, 64, and 127.
- Backup and restore scripts contain checksums, preflight, destructive guards, and a rehearsal, but retained operations evidence records the rehearsal as not run.
- Complete EF entity/configuration/`DbSet`/`ExcludeFromMigrations` inventory does not prove live column, default, type, or index fidelity.
- Patient demographic SQL is a fair EF comparison candidate but owns before/after audit snapshots inside an explicit transaction and has real API tests; no SQL-attributable defect was established.
- Appointment workflows combine recurrence, active-patient checks, reference validation, locking, reminders, and overlap behavior; no simple ORM conclusion is supported.

## Specialist validation required

- `phase2_verifier` must independently check Candidate 1 because reach is systemic.
- Database/operations validation is required for empty bootstrap, schema ownership, catalog concurrency, import plans/timing, later migration replay checkpoints, and backup/restore.
- A clinician or clinical informaticist must assess any ordering or result-association consequence from duplicate or orphan catalog rows.
- No legal, compliance, security, accessibility, or certification conclusion is made.

## Independent verification and reconciliation

The verifier independently confirmed the fixed baseline, the unchanged product tree, and a successful targeted Release build. A second trace across the migration runner, seed/bootstrap order, catalog constraints and writers, import command sites, endpoint contract, and user interface produced these final outcomes:

| Challenge or condition | Outcome | Canonical record |
| --- | --- | --- |
| `EXT-S001-C03` | `partially corroborated` | The boundary-specific review found real persistence conditions, but did not reproduce broader EF adoption as the remedy |
| Split schema authority and migrations-only bootstrap gap | `corroborated` | `P2-04-F001` |
| Catalog hierarchy and logical identity not database-enforced | `corroborated` | `P2-04-F002` |
| Uncapped sequential compendium import | `corroborated` | `P2-04-F003` |

The verifier corrected three material details before publication: the catalog delete uses one conditional delete statement rather than a separate application precheck; import command growth is higher than the specialist's first estimate; and Azure explicitly configures a 30-second database command timeout. Duplicate numeric migration prefixes remain a verified non-defect under the repository's complete-basename runner.

The final public answer is deliberately narrow: greater EF Core adoption is not supported as a general improvement target. EF is appropriate for the sampled ordinary entity and state lifecycles. Parameterized SQL is appropriate for the sampled reporting, PostgreSQL locking and leasing, idempotency, cross-table workflows, bulk work, and versioned schema. The validated conditions are about schema provenance, database-enforced invariants, and the implementation quality of one SQL bulk path.

## Coverage and scorecard impact

- `COV-008` has representative static evidence for EF mapping/configuration, concurrency examples, SQL reporting/import/locking, cancellation, and testing; plans, catalog concurrency, and representative performance remain unresolved.
- `COV-009` has representative static evidence for catalog ordering, checksums, per-file transactions, schema ownership, retained recovery evidence, and backup/restore scripts; split authority, empty bootstrap, later checkpoints, and restore rehearsal remain unresolved.
- Domain 04 has substantial evidence consistent with a provisional `2 — Partial` ceiling: deliberate mechanism selection exists, but material schema-ownership and catalog-integrity gaps remain.
- Domain 06 remains unscored because no representative measurement was possible.
- Domain 09 gains migration-resilience and EF workflow strengths plus focused import and concurrency gaps.
- Domain 10 retains an explicit backup/restore evidence gap.
- Domain 12 gains a documentation inconsistency between sole-authority language and the seed-plus-migration construction process.

## Recommended next evidence

1. Apply the packaged migrator to an empty disposable PostgreSQL database and record the first failure.
2. Generate the base schema, apply all migrations, and compare schema-only output with EF mappings and the declared ownership contract.
3. Exercise concurrent catalog create/create, import/import, and parent-delete/child-create interleavings.
4. Measure import command count, duration, lock waits, and latency at synthetic sizes such as 21, 500, and 5,000 rows.
5. Capture generated SQL and plans for sampled EF and reporting queries at retained and approved larger volumes.
6. Replay migration recovery at checkpoints 64 and 127 as well as 1.
7. Run the guarded backup/restore rehearsal using disposable synthetic data.
8. Define operating assumptions before accepting timeout or performance conclusions.
