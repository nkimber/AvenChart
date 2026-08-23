# P2-04-F003 — Compendium import performs uncapped sequential row-by-row database work

- Status: validated
- Domain(s): 04, 06, 09
- Coverage item(s): `COV-008`
- Severity: low
- Production blocker: no
- Reach: isolated
- Confidence: high for command growth; low/medium for workload impact
- Reviewer: `phase2_data`
- Independent verifier: `phase2_verifier`
- Specialist validation: database/operations or performance measurement
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

`ImportOrderCatalogCompendiumAsync` accepts and parses the full compendium, opens one transaction, and sequentially awaits per-row lookup and write commands. No row or byte cap, batching, set-oriented staging path, or representative-volume test was found.

## Evidence

- `avenchart/backend/src/AvenChart.Api/Data/ProcedureRepository.cs:708-849` owns parsing, one transaction, a serial row loop, and per-row order/result mutations.
- `ProcedureRepository.cs:2439-2508` deduplicates accepted keys but imposes no row, byte, or accepted-item cap.
- `ProcedureRepository.cs:2639-2732` issues lookup/update or lookup/sequence/insert commands per logical item.
- `ProcedureDtos.cs:210-214`, `Program.cs:5119-5130`, and `avenchart/frontend/src/App.tsx:21935-21971` impose no compendium-specific limit.
- No compendium-specific batching, PostgreSQL `COPY`, temporary-table path, or focused automated route/performance test was found.
- Corrected lower bounds, including response metadata and catalog queries, are `3N + 4` explicit commands for existing YMPG/DPMG, `4N + 4` for new YMPG/DPMG, `4N + U + 4` for existing PathGroup, and `5N + 2U + 4` for new PathGroup rows, where `N` is accepted rows and `U` is distinct order IDs. Transaction begin and commit add two provider round trips.
- Cancellation is propagated and the transaction provides atomic import behavior.
- Full commands, actual results, and limits are preserved in [EXT-S001 Packet 2](../external-feedback/ext-s001-packet-2-ef-core-sql-fitness.md).

## Consequence

Database command count and transaction duration grow linearly with accepted input. At larger vendor volumes or nontrivial database latency, this can extend lock retention and increase timeout, cancellation, and contention exposure. No measured user-visible failure or capacity limit is claimed.

## Cause and reach

The bulk path reuses single-item lookup and upsert helpers rather than using a bounded or set-oriented ingestion boundary. The condition is isolated to compendium import. SQL remains the proportionate mechanism; moving the same loop to EF would not address it.

## Risk calibration

- Impact: potentially slow import, longer transaction lifetime, and increased timeout or contention exposure
- Likelihood or preconditions: representative compendia materially larger than the 21-item retained synthetic catalog or meaningful database round-trip latency
- Detectability: measurable through command attribution, timing, lock waits, and cancellation exercises
- Reversibility: high; the import is atomic and cancellable
- Severity rationale: low because linear command growth is certain but representative volume and experienced impact are unknown

## Uncertainty and counterevidence

Input is deduplicated, all changes are atomic, cancellation is propagated, and the reference interface uses a small textarea rather than a file upload. No target compendium size, latency objective, lock measurement, timeout observation, or representative performance run is available. The condition should be reconsidered as a measured opportunity if realistic inputs prove negligible.

## Validation record

- Independent method: separate command-site trace across repository, DTO, endpoint, and UI plus cap, batch, `COPY`, staging, and focused-test searches
- Result: `corroborated` at low severity
- Reviewer agreement or dispute: agreement after the verifier corrected the original static command estimate
- Specialist conclusion or outstanding need: measure representative synthetic input sizes and retain low severity unless operational impact is demonstrated

## Disposition

Validated from `EXT-S001-C03`. No implementation recommendation is accepted. Later recommendation work should compare bounded, batched, and set-oriented SQL options; it should not presume EF conversion.
