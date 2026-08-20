# Data access architecture

AvenChart uses a hybrid data access layer:

- EF Core is the default choice for bounded, entity-oriented CRUD. Each adopted aggregate has an explicitly mapped persistence entity and a scoped repository.
- Direct Npgsql remains appropriate for reporting, bulk operations, PostgreSQL-specific SQL, and workflows that coordinate many existing tables in one explicit transaction.
- API request/response records remain separate from persistence entities so database mappings do not become the public contract.

The versioned SQL catalog in `database/migrations` is the only schema authority. EF mappings therefore use `ExcludeFromMigrations`, the API never calls `Database.Migrate`, and repositories must not execute `CREATE TABLE`, `ALTER TABLE`, or index DDL during requests.

Aggregate-scoped version numbers that cannot use a database default may be allocated with `avenchart_next_integer`. Global integer identities use database-owned sequences; do not introduce `MAX(...) + 1`, which is unsafe when requests run concurrently.

When converting another repository to EF Core, keep the change narrowly scoped, map the existing database-first schema explicitly, preserve transaction boundaries, and retain raw SQL where it expresses the operation more clearly or efficiently.
