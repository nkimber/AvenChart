# P2-04-F002 — Procedure-catalog hierarchy and logical identity are not database-enforced

- Status: validated
- Domain(s): 04
- Coverage item(s): `COV-008`, `COV-009`
- Severity: medium
- Production blocker: unknown
- Reach: isolated
- Confidence: high for the condition and interleavings; medium for clinical consequence
- Reviewer: `phase2_data`
- Independent verifier: `phase2_verifier`
- Specialist validation: database/operations; clinical informatics for any patient-safety consequence
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

`lab_order_catalog.parent_id` has no self-referencing foreign key, and the logical import identity `(parent_id, code, item_type)` has no uniqueness constraint. EF administration and SQL import both assume these invariants, but the database cannot preserve them across ordinary duplicate creation or concurrent catalog operations.

## Evidence

- `avenchart/scripts/generate-postgres-seed.mjs:1568-1581` defines the catalog primary key and provider foreign key but no parent foreign key or logical uniqueness.
- `avenchart/backend/src/AvenChart.Api/Persistence/Configurations/LabOrderCatalogConfiguration.cs:14-31` maps only the provider relationship; it has no self relationship or alternate key.
- No migration, seed constraint, unique index, or trigger enforces catalog parentage or `(parent_id, code, item_type)` identity.
- `ProcedureDirectoryRepository.CreateOrderCatalogItemAsync:19-39` does not check tuple duplication, so sequential duplicate EF creates can succeed.
- `ProcedureDirectoryRepository.IsValidOrderCatalogContextAsync:201-217` validates parent and provider before save without a protecting database constraint.
- `ProcedureRepository.UpsertImportedOrderCatalogItemAsync:2639-2698` selects before it inserts or updates; `GetImportedOrderCatalogItemAsync:2701-2732` treats the unconstrained tuple as identity.
- `ProcedureDirectoryRepository.DeleteOrderCatalogItemAsync:73-83` uses one conditional delete. It blocks deletion only when a committed child is visible to that statement.
- Exact feasible interleavings include two imports both selecting no match and inserting distinct rows, and a child validating a parent before a concurrent delete and then inserting an orphan.
- Full commands, actual results, and limits are preserved in [EXT-S001 Packet 2](../external-feedback/ext-s001-packet-2-ef-core-sql-fitness.md).

## Consequence

The catalog can contain duplicate logical identities or children without a valid parent. Listing and import behavior can then select an arbitrary latest matching row. No wrong order, patient harm, or downstream clinical failure is claimed without clinical validation.

## Cause and reach

The shared catalog invariants live in EF and SQL application behavior rather than in the database used by both writers. The condition is isolated to the laboratory/procedure catalog but crosses administration, compendium import, hierarchy changes, and deletion.

## Risk calibration

- Impact: ambiguous or orphaned catalog data and inconsistent selection
- Likelihood or preconditions: duplicate administration, concurrent imports, or parent deletion overlapping child creation
- Detectability: duplicates may be visible; orphaning raises no database error
- Reversibility: cleanup is possible before downstream use but becomes ambiguous after references accumulate
- Severity rationale: medium for durable data correctness; clinical severity is unresolved

## Uncertainty and counterevidence

The seeded dataset is small, the identity sequence prevents primary-key collisions, and retained happy-path checks cover sequential CRUD. The conditional delete protects parents with already committed visible children. No live two-session concurrency exercise was possible, and the real administrative concurrency and clinical effect are unknown.

## Validation record

- Independent method: separate search of every migration, seed constraint, EF mapping, trigger, and catalog writer plus exact interleaving analysis
- Result: `corroborated`
- Reviewer agreement or dispute: agreement; the verifier corrected the delete description and established a sequential duplicate-create path
- Specialist conclusion or outstanding need: reproduce against disposable PostgreSQL and obtain clinical-informatics judgment before assigning a patient-safety consequence

## Disposition

Validated from `EXT-S001-C03`. No implementation recommendation is accepted. Greater EF adoption alone would not enforce the missing invariants; later recommendation work must evaluate database and application responsibilities together.
