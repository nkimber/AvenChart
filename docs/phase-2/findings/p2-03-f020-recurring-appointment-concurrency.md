# P2-03-F020 — Recurring appointment mutations can lose exceptions and bypass conflict validation

- Status: validated condition
- Domain(s): 03, 04, 07, 08, 09
- Coverage item(s): `COV-005`, `COV-008`, `COV-009`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across recurring-series edit, exception, restore, and reschedule paths
- Confidence: high static confidence
- Reviewers: `phase2_clinical_safety`, `phase2_data`, `phase2_frontend_accessibility`
- Independent verifier: separate `phase2_verifier` pass; live interleaving outstanding
- Specialist validation: scheduling operations and database-concurrency review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Recurring appointment operations read and later replace the complete recurrence-exception set without a caller-supplied version or consistently held row lock. Occurrence rescheduling also does not apply the creation-time availability policy.

## Evidence

- Restore and add-exception operations read and overwrite recurrence exceptions without a shared lock at `AppointmentRepository.cs:1229-1317,1545-1618`.
- Occurrence rescheduling starts a transaction but reads the source without `FOR UPDATE`, then overwrites exceptions and inserts a replacement at `AppointmentRepository.cs:1322-1542`.
- Full-series update locks only the request-time row; its contract contains no expected version at `AppointmentDtos.cs:287-307` and `AppointmentRepository.cs:1122-1208`.
- The edit dialog resubmits the loaded recurrence set and invokes update or reschedule without an expected version at `EditAppointmentDialog.tsx:100-150`.

## Consequence

Concurrent exception edits can silently discard one another. A reschedule can lose or resurrect an excluded occurrence, and an edited or rescheduled occurrence can enter an occupied slot without the explicit conflict decision used by creation.

## Cause and reach

Recurrence is stored as a replaceable aggregate string, but its mutation contracts do not carry the aggregate version that the user reviewed. Locking is inconsistent across the related operations.

## Risk calibration

Lost exceptions and duplicate or conflicting occurrences can create inaccurate schedules and patient commitments. The repeated mutation boundary supports high severity and future-production blocker status.

## Uncertainty and counterevidence

One reschedule transaction atomically writes its exception and replacement. Status transitions are separately locked and policy validated. Live concurrent exception, reschedule, and stale-editor scenarios were not available.

## Validation record

Specialist and independent passes reproduced the contracts, complete-set overwrites, missing caller version, and inconsistent locks. The lifecycle-status bypass subclaim was rejected and is not part of this finding.

## Disposition

Validated source-level condition and future-production blocker; runtime confirmation remains required. No implementation recommendation is made.
