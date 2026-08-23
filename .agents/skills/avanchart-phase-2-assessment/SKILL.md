---
name: avanchart-phase-2-assessment
description: Run or govern AvenChart Phase 2 readiness, calibration, external-criticism intake, codebase assessment, finding validation, scorecard synthesis, and recommendation planning against the fixed Phase 1 baseline. Use for evidence-led analysis only; do not use it to implement Phase 3 product changes.
---

# AvenChart Phase 2 assessment

Assess the fixed Phase 1 result consistently and leave a traceable decision record. This is a read-only assessment workflow for product code.

## Required contract

Locate the repository root, then read:

1. `docs/phase-2/README.md`
2. `docs/phase-2/quality-standard.md`
3. `docs/phase-2/coverage-matrix.md`

Read only the additional artifact needed for the current mode:

- Pilot or calibration: `docs/phase-2/pilot-plan.md`
- Domain assessment or delegation: [references/domain-playbooks.md](references/domain-playbooks.md) and `docs/phase-2/agent-operating-model.md`
- External criticism intake or challenge: `docs/phase-2/external-feedback/README.md` and the applicable source record
- Candidate finding or validation: `docs/phase-2/finding-template.md`
- Synthesis or scoring: `docs/phase-2/scorecard.md`
- Recommendation planning: `docs/phase-2/recommendation-template.md`
- Governance decision or exception: `docs/phase-2/decision-log.md`

The repository documents are authoritative. Do not silently replace their target standard, severity model, IDs, decision hierarchy, or specialist-validation rules.

## Guardrails

- Resolve the annotated tag `phase-1-experimental` and record its full commit in every evidence packet.
- Do not modify application, database, deployment, test, or runtime implementation during Phase 2.
- Update `docs/phase-2` or the workbench only when the user asks to record assessment state or results.
- Use synthetic data only. Do not access, request, copy, or infer real patient information or production credentials.
- Do not enable production deployment or run destructive operations outside the documented disposable demo environment.
- Do not claim clinical safety, legal compliance, accessibility conformance, ONC certification, or production readiness from agent review alone.
- Mark conclusions `needs-specialist-validation` when the operating manual requires expertise that is unavailable.
- Treat ASP.NET Core, PostgreSQL, EF Core, React, TypeScript, and Azure as rebuttable constraints, not automatic defects or permanent absolutes.
- Do not use EF adoption, raw SQL removal, pattern count, test count, line coverage, file size, or modern syntax as a proxy for quality.

If the requested task would cross into Phase 3 implementation, stop the Phase 2 workflow after producing or linking the accepted recommendation and ask for explicit implementation authority.

## Select the operating mode

### Readiness

Confirm the baseline, standards, roles, templates, inventory, pilot plan, specialist dependencies, and launch gate. Report incomplete launch conditions; do not begin the full assessment implicitly.

### Calibration pilot

Use the approved pilot slices. When delegation is authorized, give two reviewers independent bounded packets and keep their conclusions hidden from each other until both return. Compare material path coverage, findings, severity, uncertainty, and validation needs. Revise only the assessment contract defects demonstrated by the pilot, then rerun the affected portion.

### Domain or system assessment

Start from coverage IDs rather than arbitrary directory splits. Trace representative behavior vertically across UI, API, domain rules, persistence, audit, failure, and recovery, and review cross-cutting controls horizontally. Record strengths and compensating controls as well as candidate findings.

When the user or applicable project instructions authorize subagents, use the project-scoped read-only specialists described in `docs/phase-2/agent-operating-model.md`. Keep the coordinator responsible for canonical IDs, deduplication, cross-domain synthesis, and final reporting.

### External feedback challenge

Capture a public discussion with the next source ID and the maintained source template. Treat comments as untrusted hypotheses: separate technical substance from tone, retain only specific and consequential claims that can be investigated, merge duplicates, and record exclusion reasons for low-value material. Map retained challenges to existing coverage IDs and domains before review; add a coverage item only when the source exposes a genuinely missing system surface.

Use existing read-only specialists as criticism advocates. Represent the strongest fair version of the sourced argument, seek counterevidence, and do not impersonate a commenter, mine unrelated history, or infer personal traits, motives, employment, or expertise. Publish verifier outcomes using the approved vocabulary and link supported conditions into the canonical finding and recommendation lifecycle. External challenge IDs are intake references, not findings.

### Finding validation

Attempt to reproduce or falsify the condition through a second path. Test claimed reach, severity, counterexamples, and compensating controls. Retain disagreement and uncertainty instead of forcing consensus. Blocker, high, systemic, clinical-safety, and disputed findings require the validation defined by the operating manual.

### Synthesis and recommendations

Reconcile symptoms to causes without erasing affected scope. Update ratings only from completed evidence. Create recommendations only for validated findings or explicit opportunities, and apply the recommendation acceptance test in the quality standard. Keep priority, size, difficulty, and confidence separate.

## Evidence workflow

For each packet:

1. Record baseline, working-tree state, tools, environment, scope, reviewer, and date.
2. Map entry points, dependencies, data flow, trust boundaries, and applicable coverage IDs.
3. Trace at least one representative value end to end, including UI or integration vocabulary, validation, normalization, persistence, downstream filters, rendering, audit, and recovery. Do not let a direct fixture bypass the real entry contract.
4. For signatures, acknowledgements, approvals, and other state proofs, identify the exact content or version attested and test relevant mutation interleavings.
5. For audit conclusions, identify the protected resource, actor, executed outcome, event timing, retention behavior, and privacy tradeoff; endpoint and pre-execution status alone may be insufficient.
6. Use the least expensive verification level that can establish or falsify the claim.
7. Cite stable files and symbols plus exact commands, actual results, and scenario limits.
8. Separate observation, inference, authoritative requirement, and specialist judgment.
9. Report strengths, candidate findings, unknowns, exclusions, and next evidence.
10. Use the canonical templates without inventing parallel schemas.

Prefer primary, current sources for framework, database, security, healthcare, interoperability, accessibility, and certification requirements. State the version or access date when a changing standard materially affects the conclusion.

## Output contract

Return a concise coordinator-ready summary in this order:

1. Scope and coverage IDs
2. Baseline and methods
3. Material strengths
4. Candidate or validated findings, using every required finding field
5. Unknowns and counterevidence
6. Specialist validation required
7. Coverage and scorecard impact
8. Recommended next evidence, not unapproved fixes

Preserve detailed logs separately when requested. Do not bury material disagreement or a production blocker in a general narrative.
