# P2-08-F002 — Only the three newest open critical results are actionable in the modern UI

- Status: validated
- Domain(s): 03, 08, 09
- Coverage item(s): `COV-006`, `COV-011`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated whenever more than three critical results are open
- Confidence: high
- Reviewers: `phase2_frontend_accessibility`, `phase2_clinical_safety`, `phase2_data`
- Independent verifier: Pilot C verifier pass
- Specialist validation: clinical operations and accessibility review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The API returns every recognized open critical result, but the Lab Queue renders acknowledgement controls only for the three newest and provides no complete worklist, expansion, or pagination path.

## Evidence

- `ProcedureRepository.cs:217-251` returns the full ordered open-result list and total count without a limit.
- `LabQueue.tsx:492-513` renders `criticalResults.results.slice(0, 3)`.
- The ordinary report-review queue has no critical-result acknowledgement action.
- No component or browser test covers four or more open critical results.

## Consequence

The fourth and older open items are reflected only in the total count. They cannot be identified or acted on through the supplied modern UI until newer items are cleared.

## Cause and reach

A safety-relevant worklist was implemented as a fixed-size banner preview without navigation.

## Risk calibration

The condition is deterministic once the backlog exceeds three and blocks access to older safety-relevant work. This supports high severity and future-production blocker status.

## Uncertainty and counterevidence

The full API list remains intact, the visible total prevents a false all-clear, and sequentially acknowledging newer items eventually reveals older ones. Operational volume and response-time expectations require qualified validation.

## Validation record

Three specialist traces and Pilot C independently reproduced the truncation. A four-plus-item browser and accessibility exercise remains outstanding.

## Disposition

Validated engineering condition and future-production blocker. No implementation recommendation is made.
