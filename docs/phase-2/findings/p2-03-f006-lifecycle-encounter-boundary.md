# P2-03-F006 — Encounter creation does not enforce or persistently display patient lifecycle state

- Status: validated
- Domain(s): 03, 07, 08, 09
- Coverage item(s): `COV-003`, `COV-004`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: cross-cutting
- Confidence: high for the source condition and approved target policy
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_quality_operations` pass
- Specialist validation: clinician, clinical informatics, HIM
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Encounter creation resolves and inserts for a patient without checking retired, deceased, or merged state. The UI always offers New encounter. Retired/deceased warnings are confined to Summary rather than the persistent chart identity header, and there is no explicit historical or post-mortem exception contract.

## Evidence

- `EncounterRepository.cs:256-355` selects the patient without lifecycle columns or predicates.
- `Program.cs:2406-2417` adds no lifecycle guard.
- `PatientEncounters.tsx:3295-3303` always offers New encounter.
- `PatientShell.tsx:68-151` shows strong identity details but no lifecycle warning.
- `PatientSummary.tsx:1120-1225,1849` contains the tab-local warnings/fact.
- `AppointmentRepository.cs:2463-2496` correctly rejects retired and deceased patients, proving an intentionally guarded counterexample.

## Consequence

Current clinical documentation can be associated with a non-current patient record without an explicit exceptional workflow, while users outside Summary may not see the relevant status.

## Cause and reach

Lifecycle validity is enforced by selected repositories and displayed by one tab rather than shared across patient mutation and identity context.

## Risk calibration

The engineering inconsistency is deterministic. `P2-D016` approves the target rule: merged source charts reject or redirect new action; retired/deceased charts block ordinary scheduling and encounter creation; exceptional/postmortem documentation requires a reasoned workflow. The present condition therefore blocks the adopted production target.

## Validation record

All three engineering passes reproduced the condition. Existing lifecycle tests cover scheduling only. The program owner approved the target policy in `P2-D016`; runtime current-date, historical-date, merge-redirect, and exceptional-workflow acceptance scenarios remain required.

## Disposition

Validated High production blocker against the approved target. This records the required behavior without claiming clinical safety or authorizing implementation.
