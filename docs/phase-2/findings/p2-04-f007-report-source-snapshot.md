# P2-04-F007 — Governed report metadata is pinned without pinning source row state

- Status: validated condition
- Domain(s): 04, 06, 07, 09, 10
- Coverage item(s): `COV-007`, `COV-008`, `COV-009`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: cross-cutting across governed report families
- Confidence: high
- Reviewer: `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: report/data-governance owner and PostgreSQL/recovery specialist outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Queued report runs persist dataset ID/version, watermark, as-of date, definition revision, and scope snapshot. The worker later verifies only that matching dataset metadata still exists, then queries mutable live operational tables without a source revision, temporal predicate, request-time snapshot, or mutation watermark.

## Evidence

- Run context requires the available synthetic watermark and as-of date at `ReportExecutionRepository.cs:925-959` and persists the pinned context at `:1129-1248`.
- The worker's `source_snapshot_available` check only joins `dataset_metadata` by dataset/version/base date at `ReportExecutionQueueRepository.cs:447-453`.
- Execution then calls `GetGovernedFamilyCsvAsync` against current patients, appointments, encounters, referrals, chart tracker, inventory, and form rows at `ReportExecutionQueueRepository.cs:553-563` and `ReportRepository.cs:550-710`.
- No query predicate or transaction binds those source rows to the recorded dataset version/as-of date.
- The execution policy itself states that historical snapshots and source-version time travel are unavailable at `ReportExecutionRepository.cs:145-146`.

## Consequence

A row changed after enqueue or after the nominal dataset base date can appear under the earlier dataset/version/as-of labels. Preview and execution, or two retries, can produce different artifacts while each retains a valid checksum and identical pinned metadata.

## Cause and reach

Report metadata and definition/scope evidence are snapshotted, but the source operational rows are not. Metadata presence is being used as a proxy for a queryable dataset snapshot.

## Risk calibration

The workflow presents itself as revision- and as-of-governed. Non-reproducible source content under those labels supports high severity and future-production blocker status even though the artifact itself is durably checksummed.

## Uncertainty and counterevidence

Definition revision, scope, purpose, recipient, request idempotency, leases, lifecycle events, checksums, and artifact retention are strong controls. The explicit local-only policy is transparent and reduces the chance of hidden operator assumptions; it does not supply source-time correctness. A paused-worker mutation/retry experiment remains outstanding.

## Validation record

The data pass traced queue metadata to live-table queries; the independent verifier confirmed the missing source binding and high/blocking calibration. No PostgreSQL mutation schedule was run.

## Disposition

Validated source-level condition and future-production blocker. No implementation recommendation is made.
