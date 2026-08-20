# Data access architecture

AvenChart uses a hybrid data access layer:

- EF Core is the default choice for bounded, entity-oriented CRUD. `OfficeNoteRepository` is the first adopted slice.
- Direct Npgsql remains appropriate for reporting, bulk operations, PostgreSQL-specific SQL, and workflows that coordinate many existing tables in one explicit transaction.
- API request/response records remain separate from persistence entities so database mappings do not become the public contract.

The versioned SQL catalog in `database/migrations` is the only schema authority. EF mappings therefore use `ExcludeFromMigrations`, the API never calls `Database.Migrate`, and repositories must not execute `CREATE TABLE`, `ALTER TABLE`, or index DDL during requests.

Legacy integer keys and aggregate-scoped version numbers that do not have database-generated defaults must be allocated with `avenchart_next_integer`. Do not introduce `MAX(...) + 1`; it is unsafe when requests run concurrently.

When converting another repository to EF Core, keep the change narrowly scoped, map the existing database-first schema explicitly, preserve transaction boundaries, and retain raw SQL where it expresses the operation more clearly or efficiently.
