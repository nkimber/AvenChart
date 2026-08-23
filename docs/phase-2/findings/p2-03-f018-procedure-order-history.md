# P2-03-F018 — Procedure orders remain rewriteable after transmission or reporting

- Status: validated
- Domain(s): 03, 04, 07, 09
- Coverage item(s): `COV-004`, `COV-006`, `COV-010`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated across procedure and laboratory orders
- Confidence: high
- Reviewers: `phase2_clinical_safety`, `phase2_data`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: ordering clinician, laboratory/procedure operations, and interoperability review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The general update path can rewrite order identity and clinical fields after transmission or reporting without an expected version, actor, correction reason, immutable revision, or transmitted snapshot.

## Evidence

- Update and status DTOs omit expected version, actor, and correction reason in `ProcedureDtos.cs:372-400`.
- `ProcedureRepository.UpdateOrderAsync` rewrites date, code, name, diagnosis, priority, type, instructions, and status at `ProcedureRepository.cs:1059-1124`.
- It has no guard for `date_transmitted`, reports, or results.
- Initial transmission blocks duplicate transmission and a pre-existing report at `ProcedureRepository.cs:996-1056`, but later general updates do not.

## Consequence

The locally displayed order can diverge from the order that was transmitted or the identity to which a result referred, without reconstructable amendment or acknowledgement evidence.

## Cause and reach

Order state is mutable in place and transmission is modeled as one field rather than an immutable communication snapshot and revision history.

## Risk calibration

Post-transmission identity drift is safety-sensitive and difficult to detect across system boundaries. This supports high severity and blocker status for the future-production target.

## Uncertainty and counterevidence

Creation verifies patient/encounter correspondence, transmission is idempotent, and report content has separate review/correction controls. External receiving and acknowledgement behavior was not runtime tested, and operating owners must validate intended scope.

## Validation record

All passes reproduced the unrestricted post-transmission update boundary. External integration replay remains outstanding.

## Disposition

Validated engineering condition and future-production blocker. No implementation recommendation is made.
