# Phase 2 calibration pilot

## Purpose

The pilot tests the assessment method, not the quality of the entire product. It determines whether different reviewers using the same contract identify materially similar evidence, severity, uncertainty, and next actions.

## Default pilot slices

### Pilot A — Authentication, authorization, and PHI boundary

Trace professional sign-in and one sensitive patient-data request through browser state, API boundary, identity resolution, authorization, practice scope, persistence, audit, and failure behavior.

- Primary coverage: COV-001, COV-002, COV-011, COV-012
- Domains: 01, 03, 05, 07, 08, 09, 10
- Reviewers: `phase2_security_privacy` and `phase2_architecture`
- Required validation: independent verifier; security/privacy specialist for claims beyond reproducible engineering evidence

### Pilot B — Encounter and clinical documentation lifecycle

Trace encounter creation, coding, SOAP-note versioning, form/document association, concurrency, correction, audit, UI recovery, and downstream visibility.

- Primary coverage: COV-003, COV-004, COV-008, COV-009, COV-011, COV-014
- Domains: 01, 02, 03, 04, 05, 07, 08, 09, 10, 12
- Reviewers: `phase2_clinical_safety` and `phase2_data`
- Required validation: independent verifier; clinician or clinical informaticist for safety conclusions

### Pilot C — Critical laboratory result acknowledgement and follow-up

Trace result ingestion or creation, abnormal/critical classification, queue presentation, acknowledgement, communication, follow-up state, audit, retry or failure handling, and recovery after interruption.

- Primary coverage: COV-006, COV-008, COV-009, COV-010, COV-011, COV-014
- Domains: 03, 04, 05, 06, 07, 08, 09, 10
- Reviewers: `phase2_clinical_safety` and `phase2_quality_operations`
- Required validation: independent verifier; practicing clinician for consequence and workflow adequacy

### Pilot D — Frontend accessibility and failure recovery

Evaluate one matched clinician workflow and one patient-portal workflow using keyboard-only operation, screen semantics, focus behavior, validation, errors, loading, empty states, recovery, responsive layout, automated checks, and manual inspection.

- Primary coverage: COV-011, COV-012, COV-013, COV-014
- Domains: 02, 03, 05, 07, 08, 09, 12
- Reviewers: two independent passes by `phase2_frontend_accessibility`; one may focus on the modern UI and the other on matched behavior in the portal/reference UI
- Required validation: accessibility specialist and users with disabilities before any conformance claim

## Pilot execution

1. Coordinator freezes the exact baseline, tools, synthetic dataset, and review packets.
2. Reviewers work independently and do not see the other reviewer’s conclusions before submission.
3. Each submission contains coverage, strengths, candidate findings, severity, confidence, unknowns, commands, and evidence.
4. The coordinator normalizes IDs without changing meaning and compares outputs.
5. The verifier challenges every proposed blocker, high, systemic, or disputed finding.
6. Human validators review domain conclusions that exceed engineering evidence.
7. The team records rubric defects, missing evidence fields, ambiguous severity anchors, over-broad coverage rows, and agent-role drift.
8. The quality standard, skill, templates, or coverage matrix are revised and the affected portion is rerun.

## Calibration measures

The pilot is acceptable when:

- both reviewers identify the same material execution path and system boundaries;
- no blocker or high-severity condition found by one reviewer is dismissed by the other without recorded evidence;
- material severity disagreements are resolved to within one level or explicitly retained for program-owner decision;
- factual claims have reproducible citations and fact/inference separation;
- duplicate symptoms are reconciled to common causes without losing affected scope;
- required specialist validation is consistently recognized;
- reviewers can apply coverage and scorecard rules without inventing new schemas;
- the coordinator can synthesize the output without rereading all raw logs;
- the effort and evidence produced are proportionate enough to scale to the full matrix.

No target percentage agreement is used initially; the pilot emphasizes material risk agreement and transparent disagreement over false numeric precision.

## Pilot record

| Pilot | Packet approved | Independent reviews complete | Verification complete | Specialist validation | Rubric changes resolved | Status |
| --- | --- | --- | --- | --- | --- | --- |
| A — Identity and PHI boundary | Yes | Yes | Yes | Routed; pending | Yes | Accepted |
| B — Encounter lifecycle | Yes | Yes | Yes | Routed; pending | Yes | Accepted |
| C — Critical lab result | Yes | Yes | Yes | Routed; pending | Yes | Accepted |
| D — Accessibility and recovery | Yes | Yes | Yes | Routed; pending | Yes | Accepted |

## Launch decision

After all four pilots, the coordinator presents:

- calibration results and unresolved disagreements;
- changes made to the assessment contract;
- confirmed or revised coverage inventory;
- expected full-assessment batching and specialist needs;
- known evidence limitations;
- a recommendation to launch, run another calibration pass, or narrow the scope.

The program owner records the decision in [decision-log.md](decision-log.md). Full assessment begins only after an explicit launch decision.

## Recorded outcome

The calibration was accepted on 2026-08-20. Independent agreement met the criteria above, verifier narrowings were preserved, the assessment contract and inventory were revised, and specialist-dependent conclusions remain explicitly open. See the [calibration report](pilots/calibration-report.md) and decision P2-D008 in [decision-log.md](decision-log.md).
