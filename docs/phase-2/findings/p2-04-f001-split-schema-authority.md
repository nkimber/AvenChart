# P2-04-F001 — Schema readiness depends on a seed-owned foundation outside the migration ledger

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

## Condition

Persistence documentation names the SQL migration catalog as the sole schema authority, but the supported system uses a governed two-stage bootstrap. The deterministic seed generator creates the foundational schema and data before the 201-file migration catalog runs. Migration readiness validates ledger IDs and checksums rather than the complete foundational schema shape.

## Evidence

- `avenchart/backend/src/AvenChart.Api/Persistence/README.md:3-13` and `AvenChartDbContext.cs:9-12` declare SQL migrations to be the only schema authority.
- `avenchart/scripts/generate-postgres-seed.mjs` contains 92 `CREATE TABLE` statements and 74 indexes; representative foundational tables begin at `:449`, `:462`, `:606`, `:1057`, `:1083`, and `:1568`.
- `avenchart/scripts/Seed-AvenChartGoldDataset.ps1:86-114` recreates `public`, loads the generated schema and data, then applies migrations.
- `avenchart/database/migrations/V0005__inventory_core.sql:15-18` already references `facilities(id)`, while no preceding migration creates `facilities`.
- `avenchart/backend/src/AvenChart.Api/Infrastructure/SchemaMigrationState.cs:69-101` validates migration IDs and checksums, not tables, columns, foreign keys, indexes, triggers, or defaults.
- `avenchart/backend/src/AvenChart.Api/Infrastructure/AzureOperationsServices.cs:397-405` follows the same seed-before-migration order when demo seeding is enabled; no equivalent foundational bootstrap was found when it is disabled.
- Duplicate numeric prefixes at `V0110` and `V0112` are not a defect: the runner orders complete filenames and records complete basenames.
- Full commands, actual results, and limits are preserved in [EXT-S001 Packet 2](../external-feedback/ext-s001-packet-2-ef-core-sql-fitness.md).

## Consequence

Fresh provisioning and recovery depend on an additional schema artifact that migration readiness does not describe or validate. A ledger-complete database can have incompatible foundational shape and still appear migration-ready until runtime behavior exposes the drift. The evidence does not establish that the supported synthetic reset fails.

## Cause and reach

The deterministic demo seed owns both the original schema bootstrap and synthetic data, while later schema evolution moved into migrations. The ordering is explicit and version-controlled, so current synthetic resets have meaningful controls. The authority split still affects schema provenance, new environments, deployment with seeding disabled, readiness, replay, and recovery.

## Risk calibration

- Impact: failed provisioning, false-positive readiness, runtime schema errors, or ambiguous recovery ownership
- Likelihood or preconditions: a new non-demo database, foundational schema drift, or recovery outside the seeded workflow
- Detectability: high for an immediate empty-bootstrap failure; weaker for drift in a ledger-complete database
- Reversibility: potentially difficult after real data exists
- Severity rationale: systemic governance and recovery exposure, moderated to medium by the deliberate and reproducible supported synthetic workflow

## Uncertainty and counterevidence

The supported Phase 1 demo explicitly seeds first, and a fresh synthetic reset verified all 201 migrations after that bootstrap. Migration interruption/resume and backup/restore rehearsals passed. A disposable empty PostgreSQL database then failed during migration with `relation "facilities" does not exist`, reproducing the split-authority condition. The seed and migration artifacts are both version-controlled, but the production bootstrap contract has not been approved.

## Validation record

- Independent method: separate source-order trace, DDL inventory, migration catalog inspection, readiness-scope inspection, and Azure deployment-order trace
- Result: `corroborated`
- Reviewer agreement or dispute: agreement after narrowing the description to a governed two-stage synthetic bootstrap that contradicts the sole-authority claim
- Specialist conclusion or outstanding need: database/operations must define and exercise the intended production schema-authority and bootstrap contract

## Disposition

Validated from `EXT-S001-C03`. No implementation recommendation is accepted. Later recommendation work must compare a single independently bootstrappable migration history with an explicitly governed two-artifact bootstrap, including recovery, compatibility, and transition risk.
