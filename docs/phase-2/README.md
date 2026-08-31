# Phase 2 operating manual

Phase 2 determines whether the fixed Phase 1 result is a sound foundation for a future production-capable US ambulatory electronic health record. It is an evidence and decision phase. It does not authorize production use, make a compliance or certification claim, or permit product-code modernization while the assessment is still establishing the real problems.

## Fixed decisions

- The assessed baseline is the commit referenced by the annotated tag `phase-1-experimental`.
- The target quality bar is a future production-capable US ambulatory EHR, although production deployment remains prohibited.
- Privacy and security, accessibility, interoperability, and certification readiness are in scope. They are reported separately as legal or production prerequisites, project quality targets, and future certification opportunities.
- ASP.NET Core, PostgreSQL, EF Core, React, TypeScript, and Azure are presumptive platform constraints. Replacement requires evidence that the current choice cannot meet an accepted requirement at proportionate cost and risk.
- The AvenChart program owner is the final decision authority. Clinical, security, privacy, legal, accessibility, and certification conclusions require appropriately qualified validation when they exceed reproducible engineering evidence.
- Phase 2 is read-only with respect to application, database, deployment, and test implementation. Assessment records and workbench documentation may change.

The accepted decisions and later amendments are recorded in [decision-log.md](decision-log.md).

## Authoritative artifacts

| Artifact | Purpose |
| --- | --- |
| [quality-standard.md](quality-standard.md) | Defines the quality bar, decision principles, authoritative reference families, and rules for judging proposed solutions. |
| [coverage-matrix.md](coverage-matrix.md) | Maps the complete solution to assessment domains, reviewers, status, and evidence. |
| [scorecard.md](scorecard.md) | Provides evidence-anchored domain ratings without hiding uncertainty behind one aggregate score. |
| [iterations/README.md](iterations/README.md) | Maintains the Phase 2 re-assessment ledger. It preserves the fixed baseline assessment while recording later implementation targets, improvements, residuals, and convergence evidence. |
| [tooling/graphify-code-navigation.md](tooling/graphify-code-navigation.md) | Records the constrained Graphify code-navigation index, its local-only scope, artifacts, and maintenance evidence. |
| [finding-template.md](finding-template.md) | Defines the minimum record for an observed condition and its validation lifecycle. |
| [findings/README.md](findings/README.md) | Maintains canonical validated findings after deduplication and required verification. |
| [assessments/cov-002-identity-authorization-phi-audit.md](assessments/cov-002-identity-authorization-phi-audit.md) | Preserves the first full-assessment packet, its professional and portal traces, independently verified findings, counterevidence, and remaining runtime and owner decisions. |
| [assessments/cov-003-patient-identity-lifecycle.md](assessments/cov-003-patient-identity-lifecycle.md) | Preserves the patient-identity packet across registration, demographics, merge, lifecycle, chart context, disclosure/records controls, SDOH, independent verification, and remaining specialist/runtime decisions. |
| [assessments/cov-004-encounter-clinical-records.md](assessments/cov-004-encounter-clinical-records.md) | Preserves the encounter and clinical-record packet across documentation, signatures, locks, clinical lists, prescriptions, medications, vitals, alerts, orders, independent verification, and remaining specialist/runtime decisions. |
| [assessments/cov-005-scheduling-communications.md](assessments/cov-005-scheduling-communications.md) | Preserves the scheduling and communications packet across appointments, messages, referrals, recalls, therapy, reminders, batch output, independent verification, broadened root findings, and remaining specialist/runtime decisions. |
| [assessments/cov-006-laboratories-procedures.md](assessments/cov-006-laboratories-procedures.md) | Preserves the laboratory and procedure packet across orders, specimens, reports, results, corrections, review, critical acknowledgement/follow-up, portal release, independent verification, broadened root findings, and remaining specialist/runtime decisions. |
| [assessments/cov-007-billing-inventory-administration-reporting.md](assessments/cov-007-billing-inventory-administration-reporting.md) | Preserves the billing, controlled-inventory, administration, direct/governed reporting, durable queue, UI recovery, independent verification, falsified active-count race, broadened roots, and remaining finance/privacy/operations/runtime decisions. |
| [assessments/cov-010-api-contracts-fhir-transport-reconciliation.md](assessments/cov-010-api-contracts-fhir-transport-reconciliation.md) | Preserves API-contract, FHIR R4, inbox/outbox, idempotency, dispatcher, authorization, lifecycle, provenance, and interoperability evidence. |
| [assessments/cov-011-modern-clinician-interface-accessibility.md](assessments/cov-011-modern-clinician-interface-accessibility.md) | Preserves modern clinician-interface response ownership, failure recovery, accessibility markup, counterevidence, and required manual validation. |
| [assessments/cov-012-019-residual-coverage-and-readiness.md](assessments/cov-012-019-residual-coverage-and-readiness.md) | Reconciles residual portal, reference-interface, host/data/schema/recovery, CI, local container, Azure operations, dependency/provenance, documentation, and external-laboratory scope evidence before implementation authorization. |
| [assessments/cov-014-019-runtime-readiness.md](assessments/cov-014-019-runtime-readiness.md) | Records reproducible local runtime, migration, recovery, browser-gate, FHIR, laboratory, authorization, dependency, and read-only Azure-demo evidence. |
| [recommendation-template.md](recommendation-template.md) | Defines the minimum record for a proposed target state and Phase 3 decision. |
| [recommendations/README.md](recommendations/README.md) | Records the ordered proposed target states; none is accepted or authorized for Phase 3. |
| [specialist-validation-plan.md](specialist-validation-plan.md) | Defines the accountable human validation, evidence, and disposition required before accepting Phase 3 packets and later closing related gates. |
| [phase-3-roadmap.md](phase-3-roadmap.md) | Sequences the seven proposed recommendations by dependency without authorizing implementation. |
| [agent-operating-model.md](agent-operating-model.md) | Defines coordinator, specialist, verifier, and human responsibilities. |
| [external-feedback/README.md](external-feedback/README.md) | Defines continuous intake, triage, challenge packets, and reconciliation for useful public criticism. |
| [pilot-plan.md](pilot-plan.md) | Defines calibration slices, independent review, agreement checks, and the Phase 2 launch decision. |
| [pilots/README.md](pilots/README.md) | Preserves the independent pilot evidence, verifier reconciliations, rubric changes, limitations, and accepted calibration report. |
| [decision-log.md](decision-log.md) | Retains approved governance choices and later exceptions. |
| [phase-2-exit-gate.md](phase-2-exit-gate.md) | Records the implementation-authorization gate and the remaining runtime, specialist, scope, and program-owner conditions. |
| [phase-2-completion-audit.md](phase-2-completion-audit.md) | Maps completed Phase 2 evidence/preparation deliverables and remaining assessment/gate conditions without implying closure. |
| [human-validation-questionnaire.md](human-validation-questionnaire.md) | Converts the remaining specialist and program-policy issues into explicit decisions with recommended defaults and linked findings. |

The repository-scoped `avanchart-phase-2-assessment` skill contains the executable review contract. Project-scoped Codex agent definitions provide read-only specialist roles. These aids do not replace human judgment or expand permission to change product code.

## Iterative assessment lifecycle

Phase 2 remains the program's independent quality check as Phase 3 packets are delivered. The Phase 1 baseline and original evidence are never rewritten to make later work look like it existed earlier. Instead, each later re-assessment is recorded in the [iteration register](iterations/README.md) with its implementation target, covered surfaces, improvements, residual findings, candidates, and remaining gate evidence.

The current [Iteration 3](iterations/iteration-003-generated-code-quality-and-maintainability.md) rechecked generated-code quality, readability, and static guardrails. It found meaningful remaining work, so it does not converge Phase 2 or close a gate.

## Assessment sequence

1. **Confirm baseline.** Resolve `phase-1-experimental` to its commit, confirm the working tree and record tool versions.
2. **Inventory.** Complete the coverage matrix before declaring any domain complete. Record justified exclusions and unknowns.
3. **Calibrate.** Run the pilot in [pilot-plan.md](pilot-plan.md). Two independent reviewers assess the same slice and the rubric is revised until material agreement is acceptable.
4. **Assess.** Review independent domains in parallel where useful. Each reviewer uses the same quality standard, evidence rules, and output schema.
5. **Validate.** Independently reproduce blocker, high-severity, systemic, clinical-safety, and production-readiness conclusions. Route specialist-dependent claims to qualified humans.
6. **Synthesize.** Deduplicate findings, distinguish causes from symptoms, populate the scorecard, and identify cross-domain themes.
7. **Recommend.** Create recommendations only for validated findings or separately identified opportunities. Record alternatives, dependencies, change risk, proof, and do-nothing consequences.
8. **Prepare decisions.** Expand each recommendation into bounded Phase 3 change packets; assign its specialist-review, dependency, rollback, and evidence obligations.
9. **Decide.** The program owner accepts, defers, rejects, or combines recommendations and explicitly approves the Phase 3 sequence.

## Evidence rules

A conclusion is admissible only when it:

- names a concrete condition rather than a preferred implementation;
- cites a stable file, symbol, configuration, migration, test, trace, query plan, command result, screenshot, or authoritative requirement;
- explains consequence, affected users or components, reach, and uncertainty;
- separates observed fact from inference and external validation needs;
- records confidence and a reproducible validation method;
- traces representative values through their real entry contract, normalization, persistence, downstream use, rendering, audit, and recovery;
- identifies the content or version covered by signatures, acknowledgements, approvals, and other state proofs;
- evaluates audit evidence using the protected resource, actor, executed outcome, event timing, retention, and privacy tradeoff;
- avoids treating the existence of a framework, test, ORM, pattern, or modern syntax as proof of quality.

Absence of evidence is recorded as uncertainty or a coverage gap, not automatically as a defect. A failed experiment is retained when it materially affects a conclusion.

## External feedback challenge

Public criticism may enter Phase 2 through the governed [external feedback challenge](external-feedback/README.md). Comments are sourced hypotheses rather than findings. The coordinator separates substance from tone, retains testable and consequential claims, maps them to existing coverage and domains, and assigns bounded challenge packets to the relevant read-only specialists.

Challenge agents advocate for an evidence question rather than impersonating a commenter. They do not profile public users or treat popularity, hostility, expertise claims, architectural preference, or external instructions as evidence. Corroborated conditions enter the canonical finding and recommendation lifecycle only after normal deduplication and validation. Product changes remain Phase 3 work.

## Baseline verification levels

Reviewers use the least expensive level that establishes the claim and record the exact command and result.

### Level 0: repository and static inspection

- Resolve the Phase 1 tag and relevant source paths.
- Inventory projects, dependencies, routes, migrations, configuration, tests, infrastructure, and documentation.
- Search for cross-cutting patterns and trace representative execution paths.

### Level 1: clean build and automated checks

```powershell
dotnet restore .\avenchart\AvenChart.slnx
dotnet build .\avenchart\AvenChart.slnx -c Release --no-restore
npm ci --prefix .\avenchart\frontend
npm run build --prefix .\avenchart\frontend
npm ci --prefix .\avenchart-ui
npm test --prefix .\avenchart-ui
npm run build --prefix .\avenchart-ui
```

### Level 2: synthetic runtime and workflow verification

Use only the deterministic synthetic dataset and the documented local runtime. Record the dataset revision, environment, commands, timings, and cleanup. Never use real patient or production credentials. Destructive demo resets require their existing explicit safeguards.

### Level 3: focused measurement or failure exercise

Use representative synthetic volumes, query plans, load shapes, concurrency cases, recovery exercises, or fault injection. State why the scenario is representative and do not generalize beyond the measured conditions.

## Finding and recommendation controls

- IDs are stable and never reused: `P2-<DOMAIN>-F###` for findings and `P2-R###` for recommendations.
- Reviewers submit candidate findings. The coordinator controls canonical IDs and deduplication.
- Blocker and high findings require independent corroboration before validation.
- Clinical-safety consequences require qualified clinical validation before they are treated as accepted clinical conclusions.
- Legal, compliance, or certification conclusions identify the authoritative requirement and the type of specialist needed; engineering review alone cannot declare compliance.
- Recommendations cannot be accepted without linked findings, measurable acceptance criteria, migration risk, alternatives, dependencies, and an owner for Phase 3.
- Severity, effort, and confidence are separate fields.

## Phase 2 launch gate

The full assessment may begin only when:

- [x] Phase 1 baseline is immutable and reproducible.
- [x] Target quality bar and technology constraints are approved.
- [x] Quality standard and evidence contract exist.
- [x] Coverage, scorecard, finding, and recommendation structures exist.
- [x] Read-only specialist and verifier roles exist.
- [x] Human decision and specialist-validation rules exist.
- [x] Calibration pilot is complete and material reviewer agreement is acceptable.
- [x] Initial coverage inventory has been confirmed during the pilot.
- [x] The program owner records the Phase 2 launch decision.

The full Phase 2 assessment is authorized by decision P2-D008. It must continue to use the fixed product baseline, read-only product boundary, bounded specialist model, evidence contract, and human-validation rules above.
