# P2-R004 — Establish one bootstrappable schema authority and measured persistence behavior

- **Status:** Proposed — target policy approved by `P2-D016`; implementation is not authorized
- **Linked findings:** `P2-04-F001`, `P2-04-F002`, `P2-04-F003`, `P2-04-F006`, `P2-04-F007`
- **Priority band:** Foundation
- **Size:** L
- **Difficulty:** High
- **Confidence:** High static; runtime measurement pending
- **Proposed owner:** Data platform and database operations lead
- **Decision owner:** AvenChart program owner
- **Specialist approval needed:** PostgreSQL/database operations, data migration, performance

## Problem and evidence

Choose and document a single independently bootstrappable schema authority, enforce catalog invariants in the database where appropriate, and measure import, report, queue, timeout, lock, and connection behavior under representative synthetic volumes. Preserve the deliberate EF/SQL hybrid boundary unless measurement proves a focused change is superior.

The linked `P2-04-*` findings establish that migrations are not independently bootstrappable from an empty database, schema readiness validates a ledger but not the complete shape, catalog integrity relies on application checks, and selected imports/queries lack measured bounds. The evidence supports focused persistence work, not a conclusion that parameterized SQL or EF Core should replace one another wholesale.

## Target state

One explicit bootstrap authority creates and validates a recoverable PostgreSQL schema from an empty environment. Constraints protect material invariants, and the EF/SQL boundary is documented and measured by operation type, volume, lock behavior, and recovery consequence.

## Expected value

Reliable fresh provisioning, visible schema drift, fewer duplicate/orphan catalog rows, bounded import time, and evidence-based persistence choices.

## Options considered

| Option | Benefits | Costs and risks | Disposition |
| --- | --- | --- | --- |
| Migration-only | One explicit schema ledger and standard fresh provisioning | Requires expressing the current foundation in ordered migrations | Preferred target, subject to empty-database proof |
| Governed seed-plus-migration | Can preserve generated deterministic fixtures | Two authorities unless seed shape, version, and validation are governed as a single bootstrap contract | Contingency only if a migration-only proof exposes an unavoidable constraint |
| Transitional bootstrap | Limits immediate conversion scope | Carries temporary ambiguity and must have an expiry/reconciliation plan | Allowed only with a signed retirement plan |

## Acceptance criteria

Empty-database replay, drift detection, migration rollback/recovery, FK/unique constraint tests, query plans, round-trip/timing/lock measurements, and documented EF-vs-SQL rationale are retained as release evidence. No blanket EF conversion is an acceptance criterion.

## Dependencies and sequence

Start with `R007-A` to define reproducible evidence and with the authority decision in `R004-A`. Complete bootstrap and migration recovery before adding broad constraints; remediate data and add constraints before depending on them in `R001`, `R002`, `R003`, or `R006`. Measure existing behavior before batching or rewriting selected data paths, then feed the resulting recovery proof into `R007-D/E`.

## Scope and affected contracts

- Migration catalog, seed generator, reset/deploy scripts, schema-ledger validation, PostgreSQL roles, backups, restore, and provisioning documentation.
- Database constraints and EF mappings for catalog, patient, encounter, laboratory, workflow, queue, and reporting invariants.
- Import/report/queue command paths, command timeout and connection policy, representative performance fixtures, query-plan capture, and operational dashboards.
- Synthetic demo data stays disposable and must no longer be a hidden prerequisite for a recoverable production schema.

## Delivery risk and rollback

Schema changes can strand environments, break fixture reset, or create long locks. Use additive migrations, a reproducible empty-database harness, logical schema fingerprinting, explicit data-backfill checkpoints, point-in-time restore rehearsal, and a versioned compatibility window. A rollback decision must restore a verified database state and preserve the migration/audit ledger; it must not delete applied clinical or workflow evidence.

## Size and difficulty rationale

This is Large because it governs every deployment and many aggregates, while only a subset requires structural change. Difficulty is High because schema authority, recovery, performance, and the EF/SQL boundary must be proven together against PostgreSQL rather than inferred from source review.

## Phase 3 change packets

1. **R004-A — Schema authority and bootstrap:** select the authority, make empty-database provisioning reproducible, validate full shape, and retire or govern the legacy seed dependency.
2. **R004-B — Invariant constraint catalog:** introduce prioritized foreign keys, uniqueness, checks, and supporting mappings with duplicate/orphan remediation and migration evidence.
3. **R004-C — Persistence measurement and batching:** set timeout/connection policy, capture plans and lock/timing baselines, and correct proven row-at-a-time hot paths without a blanket ORM conversion.
4. **R004-D — Recovery and topology proof:** rehearse backup/restore, migration failure recovery, schema drift response, and production topology readiness.

## Decision record

- **Decision:** Pending acceptance as a Phase 3 recommendation.
- **Decided by:** AvenChart program owner.
- **Date:** Not set.
- **Rationale and conditions:** `P2-D016` approves the target persistence policy. Acceptance requires a named database owner, selected bootstrap option, data remediation/rollback owner, live PostgreSQL evidence, and the acceptance evidence above.
