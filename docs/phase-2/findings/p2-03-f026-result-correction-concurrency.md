# P2-03-F026 — Concurrent result corrections can erase an intermediate correction from current state and history

- Status: validated condition
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-006`, `COV-008`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across mutable laboratory results
- Confidence: high for silent stale overwrite; medium-high for the exact overlap schedule
- Reviewers: `phase2_data`, `phase2_clinical_safety`
- Independent verifier: separate read-only correction-concurrency pass
- Specialist validation: laboratory medicine and database runtime validation outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Laboratory result correction has no caller-observed content version. It snapshots and then unconditionally replaces the current row without first locking that row or conditionally matching an expected version.

## Evidence

- `ProcedureResultUpdateRequest` has no expected version at `ProcedureDtos.cs:498-506`.
- `LabReportAndResultCapture.tsx:41-60` submits a full correction body without version or stale-write contract.
- `UpdateResultAsync` reads context before the transaction, snapshots, and then updates only by result ID at `ProcedureRepository.cs:1625-1677`.
- `SnapshotCurrentProcedureResultVersionAsync` does not use `FOR UPDATE` at `ProcedureRepository.cs:2033-2063`.
- The sequence allocator and unique `(result_id, version_no)` constraint protect version numbers but do not serialize the source result content at `V0238__centralize_runtime_schema_and_key_allocation.sql:320-336,550-570`.

Under PostgreSQL's ordinary command-snapshot behavior, two overlapping corrections can both snapshot the original content, receive different history version numbers, and then commit different unconditional replacements. The last replacement becomes current while the first correction is absent from both current state and history.

## Consequence

A clinically material intermediate correction can be silently overwritten and become unreconstructable from application history. A sequential stale form is also accepted without warning, even when its immediate predecessor is retained.

## Cause and reach

Version-number allocation is serialized, but the mutable result aggregate has neither a caller version nor a shared row lock across snapshot and update.

## Risk calibration

The endpoint represents a correction workflow, so silent loss of both current content and retained history supports high severity and future-production blocker status.

## Uncertainty and counterevidence

Each individual snapshot and update is one transaction, version numbers remain unique, and non-overlapping later corrections normally preserve the preceding value. A barrier-controlled PostgreSQL reproduction is still required to capture the inferred overlap schedule and locks.

## Validation record

The data and clinical passes reproduced the contract. A separate verifier confirmed the source-supported schedule and distinguished it from review/acknowledgement content binding. No live database interleaving was available.

## Disposition

Validated source-level condition and future-production blocker. It remains separate from `P2-03-F009` because it concerns loss of result content itself. No implementation recommendation is made.
