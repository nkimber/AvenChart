# Data access architecture

AvenChart uses a hybrid data access layer:

- EF Core is the default choice for bounded, entity-oriented CRUD. Each adopted aggregate has an explicitly mapped persistence entity and a scoped repository.
- Direct Npgsql remains appropriate for reporting, bulk operations, PostgreSQL-specific SQL, and workflows that coordinate many existing tables in one explicit transaction.
- API request/response records remain separate from persistence entities so database mappings do not become the public contract.

The versioned SQL catalog in `database/migrations` is the only schema authority. EF mappings therefore use `ExcludeFromMigrations`, the API never calls `Database.Migrate`, and repositories must not execute `CREATE TABLE`, `ALTER TABLE`, or index DDL during requests.

Aggregate-scoped version numbers that cannot use a database default may be allocated with `avenchart_next_integer`. Global integer identities use database-owned sequences; do not introduce `MAX(...) + 1`, which is unsafe when requests run concurrently.

When converting another repository to EF Core, keep the change narrowly scoped, map the existing database-first schema explicitly, preserve transaction boundaries, and retain raw SQL where it expresses the operation more clearly or efficiently.

## Adopted EF mutation boundaries

The current EF-backed boundaries are office notes, external address-book contacts, patient education, recalls, chart tracking, patient record requests, SDOH assessments, therapy-group state, referrals, document-template lifecycle state, administration users/facilities/access control, encounter summary/archive/vitals state, clinical-list entity state, and procedure directory/catalog state.

Large SQL repositories remain as read-model or workflow repositories when they perform joined projections, reporting, bulk import, PostgreSQL-specific locking, or a governed transaction across several legacy tables. In particular:

- `ClinicalListRepository` owns the combined patient list projection, reconciliation, vocabulary, and prescription workflows; `ClinicalListStateRepository` owns allergy, problem, medication lifecycle, and immunization mutations.
- `ProcedureRepository` owns queues, compendium import, and order/report/specimen/result workflows; `ProcedureDirectoryRepository` owns catalog and lab-provider directory mutations.
- `AdministrationRepository` owns administration dashboards and cross-domain workflows; `AdministrationDirectoryRepository` owns user, facility, and access-control mutations.
- `EncounterRepository` owns chart projections, signatures, SOAP/version workflows, and encounter creation; `EncounterStateRepository` owns summary, archive/restore, and vitals mutations.

## Mapping and concurrency rules

- Every persistence entity has an `IEntityTypeConfiguration<T>` mapping and a `DbSet<T>` entry.
- Every mapped table is excluded from EF migrations. SQL files under `database/migrations` remain authoritative.
- Legacy `timestamp without time zone` columns are mapped explicitly and receive `DateTimeKind.Unspecified` values.
- API DTOs are never reused as persistence entities.
- User-visible optimistic-concurrency contracts map their database version field as an EF concurrency token and translate `DbUpdateConcurrencyException` into the endpoint's conflict result.
- A repository may use `FromSql` or direct Npgsql for locking, bulk, and projection workloads, but schema DDL, literal/global allocator keys, and `MAX(...) + 1` are prohibited by the migration-resilience checks.
