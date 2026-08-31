# Phase 2 iteration register

Phase 2 is an evidence-led feedback loop, not a one-time inspection. The fixed Phase 1 baseline remains the comparison point for every iteration, while each iteration may examine a later implementation target produced by Phase 3 work. This register makes that distinction explicit: it preserves the original assessment and records whether later delivery has removed, narrowed, or introduced meaningful work.

## Operating rule

1. A Phase 3 packet changes the implementation only after its own authorization and evidence requirements are satisfied.
2. Phase 2 then re-assesses the affected system surfaces and cross-cutting controls against the fixed Phase 1 baseline and the approved target policies.
3. The re-assessment records improvements, residual findings, new candidates, counterevidence, and the remaining Phase 3 work. It does not overwrite the original finding or claim that a gate has closed.
4. The program owner decides when the evidence has converged. No count of iterations, green test suite, or agent conclusion closes a gate automatically.

An iteration is meaningful when it changes the disposition, reach, confidence, priority, or recommended sequence of a finding; validates a previously unknown behavior; or finds a material new condition. Cosmetic restatements and untested preferences do not create new iterations or findings.

## Required iteration record

Each record must state:

- the immutable Phase 1 baseline and the exact implementation target reviewed;
- the Phase 3 changes or delivery evidence under review;
- covered `COV-*` rows, methods, commands, results, and limits;
- corroborated improvements, residual canonical findings, and separately labelled candidates;
- counterevidence, exclusions, specialist-validation needs, recommendation effects, and gate status; and
- the next evidence required before another iteration or a convergence decision.

High, blocker, systemic, clinical-safety, and disputed conclusions retain the independent-validation and specialist rules in the [operating manual](../README.md). An iteration is an assessment record, not implementation authorization or a production-readiness claim.

## Ledger

| Iteration | Status | Fixed comparison | Implementation target | Outcome |
| --- | --- | --- | --- | --- |
| [01 — baseline synthesis](../phase-2-completion-audit.md) | Recorded | `phase-1-experimental` at `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989` | The fixed Phase 1 result | 64 canonical findings and seven proposed recommendations; gates remained open. |
| [02 — current implementation reassessment](iteration-002-current-implementation-reassessment.md) | Recorded · not converged | Same immutable Phase 1 baseline | `af0f321f6eb215384dff7c1dd882d39ea973be1a` | Material implementation gains are corroborated; production identity/CORS, SMART/US Core, structural, and verification work remains. |
| [03 — generated-code quality and maintainability](iteration-003-generated-code-quality-and-maintainability.md) | Recorded · not converged | Same immutable Phase 1 baseline | `10fbf3940259c3176c7d86ff72de273e84093adf` | Explicit static-quality guardrails are verified; persistence/template and readability opportunities remain evidence-gated. |

## Convergence decision

Phase 2 may be proposed as converged only after the latest iteration shows that the accepted Phase 3 scope no longer identifies meaningful unaddressed improvements, all residual findings have an accepted disposition, and the required specialist and current-environment evidence is available. The program owner must record that decision explicitly in the [decision log](../decision-log.md); the Phase 2 exit gate remains open until separately closed.
