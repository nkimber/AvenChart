# P2-08-F003 — Governed report status can remain stale after a recoverable poll or lifecycle conflict

- Status: validated condition
- Domain(s): 07, 08, 09, 10
- Coverage item(s): `COV-007`, `COV-014`
- Severity: medium
- Production blocker: no by itself
- Reach: repeated across governed background runs
- Confidence: high static confidence
- Reviewer: `phase2_frontend_accessibility`
- Independent verifier: not required for an isolated medium condition; browser reproduction outstanding
- Specialist validation: accessibility and report-operations review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

One failed queued/running status poll terminates the polling loop, leaving the selected run in its prior state without an in-place resume/refresh control. A lifecycle 409 returns current run evidence, but the client reduces it to generic error text and neither renders nor refetches that state.

## Evidence

- `GovernedReportExecution.tsx:241-309` returns permanently from the poll catch; reselecting the same scalar run/state does not reliably restart the effect.
- Lifecycle mutations submit the displayed version, but `GovernedReportExecution.tsx:389-427` only sets an error after conflict.
- `Program.cs:8645-8717` includes `existingRun` in 409 responses.
- `api/transport.ts:11-18,50-78` and the page do not model or consume that current-run evidence.
- Existing report API tests cover version payloads, not failed polling or conflict recovery.

## Consequence

An operator can continue seeing a run as queued or running after completion, failure, cancellation, or another lifecycle change, delaying download or recovery decisions.

## Cause and reach

Transient refresh failure is treated as terminal, and server-current conflict state is not reintegrated into the durable-run view.

## Risk calibration

Durable queue evidence is not lost and page reload/navigation can recover, so medium severity and non-blocker status are appropriate.

## Uncertainty and counterevidence

Errors are announced with `role="alert"`, expected versions reject stale mutations, and an operations projection exists. A browser interception test is still required to demonstrate recovery and focus/announcement behavior.

## Validation record

The frontend specialist reproduced the state/effect path and found no negative test. Coordinator review accepted it as an isolated medium condition.

## Disposition

Validated source-level condition. No implementation recommendation is made.
