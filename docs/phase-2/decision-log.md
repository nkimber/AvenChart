# Phase 2 decision log

This log records governance and scope decisions. It does not contain findings or recommendations.

## P2-D001 — Assess against a future production-capable US ambulatory EHR bar

- Date: 2026-08-20
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: Phase 2 evaluates the fixed Phase 1 result as a candidate foundation for a future production-capable US ambulatory EHR. Production use remains prohibited and the assessment itself makes no readiness claim.
- Consequence: Safety, correctness, privacy, security, accessibility, recoverability, and operational evidence receive production-level scrutiny even when the current runtime is synthetic or demonstrative.

## P2-D002 — Include regulatory, accessibility, interoperability, and certification readiness

- Date: 2026-08-20
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: Phase 2 includes HIPAA privacy/security readiness, accessibility, interoperability, and future certification readiness.
- Consequence: Reports distinguish legal and production prerequisites, project quality targets, and future certification opportunities. Qualified specialists are required for claims beyond engineering evidence.

## P2-D003 — Retain the current technology stack as a rebuttable constraint

- Date: 2026-08-20
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: ASP.NET Core, PostgreSQL, EF Core, React, TypeScript, and Azure remain presumptive platform choices.
- Consequence: Focused restructuring or replacement may be recommended, but wholesale replacement requires unusually strong comparative evidence, including migration and operational consequences.

## P2-D004 — Preserve a hybrid EF Core and parameterized SQL boundary

- Date: 2026-08-20
- Status: accepted as assessment principle
- Decision owner: AvenChart program owner
- Decision: Phase 2 will not use “more EF” or “less SQL” as an outcome measure. It will evaluate whether each mechanism is correct, clear, observable, performant, testable, and proportionate to the requirement.
- Consequence: EF Core is the default for ordinary entity work; parameterized SQL remains valid when it is demonstrably the clearer or safer fit. Exceptions in either direction require evidence.

## P2-D005 — Use read-only specialist agents with independent verification

- Date: 2026-08-20
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: A coordinating agent may delegate bounded assessment packets to project-scoped read-only specialists. Blocker, high, systemic, and disputed findings receive independent verification.
- Consequence: Product code is not modified during Phase 2. Agents return structured evidence; the coordinator owns canonical records and humans retain decision authority.

## P2-D006 — Program owner is final approver; specialists validate domain claims

- Date: 2026-08-20
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: The program owner accepts scope, risks, findings, recommendations, and the Phase 3 roadmap. Appropriately qualified people validate clinical, security/privacy, legal/compliance, accessibility, certification, and high-risk operational conclusions.
- Consequence: Missing expertise is recorded as an open validation dependency rather than filled by agent inference.

## P2-D007 — Full assessment requires a calibrated pilot

- Date: 2026-08-20
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: Phase 2 remains in readiness and calibration until representative pilot slices have been independently reviewed and the assessment contract produces materially consistent results.
- Consequence: The pilot may revise the rubric, templates, agent packets, or inventory before the full review launches.

## P2-D008 — Accept calibration and authorize the full Phase 2 assessment

- Date: 2026-08-20
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: Accept the four-slice calibration and authorize the full Phase 2 assessment to run under the approved read-only operating model, using no more than three concurrent specialists initially.
- Context and alternatives: The program owner approved the default operating choices and requested implementation. All pilots produced independent reviews and separate verifier challenges. Material agreement was acceptable after counterevidence and severity narrowings were preserved. Repeating calibration would add limited value before the focused runtime and human-specialist evidence already identified.
- Consequence: Full coverage-matrix assessment may begin. Production use remains prohibited, product implementation remains outside Phase 2 authority, the scorecard remains unscored until full evidence is reconciled, and clinical, security/privacy, legal/compliance, accessibility, coding, certification, and high-risk operations conclusions remain subject to qualified validation.
- Evidence: [pilots/calibration-report.md](pilots/calibration-report.md)

## P2-D009 — Establish a continuous external feedback challenge

- Date: 2026-08-21
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: Phase 2 will maintain a reusable intake for substantive public criticism, beginning with the August 2026 `r/dotnet` discussion. External comments are captured as sourced challenge hypotheses, triaged independently of tone or popularity, mapped to the existing coverage matrix, and assessed through bounded read-only specialist packets and normal independent verification.
- Context and alternatives: The initial thread contains a mixture of specific engineering observations, architectural preferences, concerns about evidence independence and modernization claims, hostility, misunderstandings, and promotion. Ignoring the discussion would discard useful adversarial review; treating every comment or commenter as authoritative would distort the evidence model. Creating agents that impersonate named respondents was rejected in favor of criticism advocates that represent the sourced technical argument and actively seek counterevidence.
- Consequence: Future Reddit posts and other public technical discussions may be added using `external-feedback/source-template.md`. External challenge IDs never substitute for canonical findings or recommendations, public users are not profiled, product code remains unchanged during Phase 2, and the workbench publishes what was corroborated, narrowed, disputed, not reproduced, or left needing evidence.
- Evidence: [external-feedback/README.md](external-feedback/README.md), [external-feedback/reddit-dotnet-2026-08-19.md](external-feedback/reddit-dotnet-2026-08-19.md)

## P2-D010 — Record COV-007 without authorizing implementation

- Date: 2026-08-21
- Status: accepted as assessment disposition
- Decision owner: AvenChart program owner
- Decision: Record the COV-007 billing, controlled-inventory, administration, reporting, and background-execution evidence against the fixed Phase 1 baseline. Keep the product tree read-only, retain runtime and human-policy gaps as open evidence, and do not begin Phase 3 changes from this packet.
- Context and alternatives: The packet includes independent data, operations, frontend, security/privacy, and verifier passes. It corroborates durable financial/custody/report risks, broadens existing cross-cutting roots, falsifies the suspected duplicate-active-count race, and retains worker/artifact/configuration-policy questions where evidence is not sufficient. Implementing the obvious-looking fixes before the remaining verticals are assessed would change the object of review and make later comparisons harder.
- Consequence: `COV-007` advances to In review; the next planned cross-cutting packet is `COV-010` (API contracts, FHIR, transport, and reconciliation). Findings remain recommendations inputs only; no code, migration, infrastructure, or test implementation change is authorized by Phase 2.
- Evidence: [COV-007 assessment](assessments/cov-007-billing-inventory-administration-reporting.md), [coverage matrix](coverage-matrix.md), [validated findings register](findings/README.md)

## P2-D011 — Record COV-010 without authorizing implementation

- Date: 2026-08-21
- Status: accepted as assessment disposition
- Decision owner: AvenChart program owner
- Decision: Record the COV-010 API-contract, FHIR, transport, inbox/outbox, and reconciliation evidence against the fixed Phase 1 baseline. Keep the product tree read-only, classify external FHIR and partner-delivery consequences as conditional, and do not begin Phase 3 changes from this packet.
- Context and alternatives: Independent architecture/operations, data, security/privacy, and verifier passes corroborated a systemic OpenAPI metadata gap, a conditional FHIR R4 conformance gap, content-unbound integration identity, and at-least-once outbox delivery mechanics. Strong local controls, explicit local-only boundaries, and absent partner scope prevent unconditional production claims. Treating the local state machine as a production integration would overstate evidence; ignoring the contract gaps would understate the future boundary.
- Consequence: `COV-010` advances to In review; four new conditional findings enter the register, six existing findings receive FHIR/integration evidence, and validator, PostgreSQL, partner, deployment, and qualified interoperability decisions remain required. Findings remain recommendation inputs only; no code, migration, infrastructure, or test implementation change is authorized by Phase 2.
- Evidence: [COV-010 assessment](assessments/cov-010-api-contracts-fhir-transport-reconciliation.md), [coverage matrix](coverage-matrix.md), [validated findings register](findings/README.md)

## P2-D012 — Record COV-011 without authorizing implementation

- Date: 2026-08-21
- Status: accepted as assessment disposition
- Decision owner: AvenChart program owner
- Decision: Record the COV-011 modern clinician-interface and accessibility evidence against the fixed Phase 1 baseline. Add two bounded medium conditions, broaden the existing response-ownership and verification-governance findings, retain accessibility and scheduling-policy questions as open evidence, and do not begin Phase 3 changes from this packet.
- Context and alternatives: The packet confirms strong shell, dashboard, transport, cancellation, and retry patterns alongside weaker page-specific async ownership and failure semantics. Treating visual success-path coverage as sufficient would miss stale actionable state and assistive-technology recovery gaps; treating source inspection as formal WCAG proof would overstate the evidence. No product implementation was made.
- Consequence: `COV-011` remains In review; `P2-08-F005` and `P2-08-F006` enter the register, while `P2-03-F007` and `P2-09-F002` are broadened. Deferred-response browser traces, assistive-technology runs, and clinical/scheduling validation remain required. Findings remain recommendation inputs only; no code, migration, infrastructure, or test implementation change is authorized by Phase 2.
- Evidence: [COV-011 assessment](assessments/cov-011-modern-clinician-interface-accessibility.md), [coverage matrix](coverage-matrix.md), [validated findings register](findings/README.md)

## P2-D013 — Reconcile residual coverage before implementation authorization

- Date: 2026-08-21
- Status: accepted as assessment disposition
- Decision owner: AvenChart program owner
- Decision: Record the residual COV-001/COV-008/COV-009/COV-012–COV-019 evidence packet and keep Phase 2 implementation authorization closed. Static evidence is substantially collected, but no Phase 3 code, migration, infrastructure, test, or runtime change may begin until the documented runtime, deployment, specialist, scope, release, and program-owner gates are completed or explicitly accepted as residual risk.
- Context and alternatives: The residual review confirms meaningful host, hybrid persistence, local-operations, portal, CI, Azure-template, licensing, and documentation controls. It also confirms that Docker/PostgreSQL, deployed Azure, manual accessibility, external interoperability, release provenance, and several clinical/privacy policy decisions are not evidenced in the current environment. Calling Phase 2 complete would convert missing evidence into an unsupported readiness claim; beginning fixes now would change the assessed object before recommendations are accepted.
- Consequence: `COV-012` receives bounded evidence and remains In review; `COV-013` and `COV-019` remain Inventoried pending scope decisions; residual rows remain In review. `P2-03-F007` and `P2-08-F006` are broadened, no new high root is created, seven recommendations are drafted but not accepted, and the scorecard records implementation readiness as pending.
- Evidence: [residual coverage assessment](assessments/cov-012-019-residual-coverage-and-readiness.md), [scorecard](scorecard.md), [coverage matrix](coverage-matrix.md), [validated findings register](findings/README.md)

## P2-D014 — Fix the production target and supported product scope

- Date: 2026-08-21
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: Continue Phase 2 against a production-worthy US ambulatory EHR target while restricting supported user-interface scope to the modern `avenchart-ui` clinician and patient-portal application. Exclude the reference `avenchart/frontend` interface from future production scope. Require standards-conformant FHIR interoperability, an external laboratory-result API exercised with synthetic laboratory data, multi-facility and purpose-of-use authorization, vendor-neutral standards-based SSO compatible with major identity vendors, and a first-party test identity provider for deterministic verification.
- Context and alternatives: The reference interface is a Phase 1 compatibility aid and is not a supported production target. No production identity vendor has been selected, so the target is an OIDC/OAuth standards boundary rather than a vendor-specific dependency. External laboratory partner testing is unavailable, but the supported API contract and synthetic end-to-end behavior remain assessable. The program owner will act as the initial human validator and will identify conclusions that require another specialist.
- Consequence: `COV-013` is Excluded by approved scope; `COV-019` advances to In review. FHIR, external laboratory API, multi-facility/purpose authorization, SSO/provider portability, and test-identity evidence are required before the applicable gates can close. All implementation gates remain open until the program owner gives explicit closure instructions.
- Evidence: [coverage matrix](coverage-matrix.md), [Phase 2 exit gate](phase-2-exit-gate.md), [P2-R001](recommendations/p2-r001-identity-resource-safety.md), [P2-R006](recommendations/p2-r006-contracts-integration-report-governance.md)

## P2-D015 — Record runtime evidence and retain every implementation gate as open

- Date: 2026-08-21
- Status: accepted as assessment disposition
- Decision owner: AvenChart program owner and Phase 2 coordinator under `P2-D008`
- Decision: Record the synthetic Docker/PostgreSQL, migration/bootstrap, backup/restore, browser-gate, FHIR, integration, identity-scope, dependency, and read-only Azure-demo evidence without changing the fixed product baseline. Make the FHIR condition an unconditional production blocker, add the absent external laboratory ingestion contract as `P2-07-F005`, and leave every implementation gate open until explicit closure instructions are given.
- Context and alternatives: The environment can now answer several earlier unknowns. Seed-first migration recovery and backup/restore pass, but empty-database migrations fail on the seed-owned foundation. Clean builds, unit tests, lint, and dependency inventories pass, while broad smoke/accessibility/workflow/mutation gates fail or short-circuit. FHIR search/MIME/error behavior and generic laboratory inbox replay fail the newly adopted production requirements. The existing Azure demo is healthy but predates the assessed baseline and is not a production topology.
- Consequence: The canonical register contains 64 findings: 39 High, 23 Medium, and 2 Low; 37 High findings are unconditional target blockers. `COV-009`, `COV-010`, `COV-014`, `COV-015`, `COV-017`, and `COV-019` gain representative evidence-complete status, while `COV-016` retains bounded deployed-demo evidence and open production questions. Runtime availability is no longer the primary Phase 2 blocker; human validation, recommendation acceptance, and explicit implementation authorization remain outstanding.
- Evidence: [runtime-readiness assessment](assessments/cov-014-019-runtime-readiness.md), [coverage matrix](coverage-matrix.md), [validated findings register](findings/README.md), [Phase 2 exit gate](phase-2-exit-gate.md)

## P2-D016 — Approve all human-validation target defaults without closing gates

- Date: 2026-08-21
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: Approve every recommended default in the Phase 2 human-validation questionnaire. The production target therefore uses vendor-neutral OpenID Connect with SAML deferred, enforced facility/patient/purpose boundaries and governed exceptional access, explicit patient lifecycle and content-bound amendment rules, non-destructive correction/history, closed-loop critical-result follow-up, FHIR R4/US Core 9.0.0/SMART with a profiled FHIR laboratory bundle and later partner-driven HL7 v2 adapter, durable scheduling/communication workflows, a production-grade internal billing ledger, independently authenticated controlled-inventory attestation, governed exports/configuration, independent WCAG 2.2 AA validation, and the documented production operations/release floor.
- Context and alternatives: The program owner approved the defaults as one set. SAML, real clearinghouse/ERA certification, HL7 v2 laboratory transport, and partner certification are not first-release blockers unless a selected customer or partner makes them applicable. Independent accessibility evaluation and qualified legal/HIM, clinical, interoperability, security/privacy, and operations evidence remain required where the questionnaire says so.
- Consequence: The human target-policy questions are answered. `P2-03-F006` and `P2-03-F027` become unconditional High production blockers, so all 39 High findings are now blockers against the adopted target. The seven recommendations remain proposed rather than accepted implementation packets; ownership, rollback, acceptance evidence, current production proof, and explicit gate closure remain open. No product change or Phase 3 implementation is authorized.
- Evidence: [human validation questionnaire](human-validation-questionnaire.md), [validated findings register](findings/README.md), [scorecard](scorecard.md), [Phase 2 exit gate](phase-2-exit-gate.md)

## P2-D017 — Prepare Phase 3 decision packets without authorizing implementation

- Date: 2026-08-21
- Status: accepted as assessment disposition
- Decision owner: AvenChart program owner
- Decision: Convert the seven proposed target-state recommendations into decision-ready Phase 3 packets, publish a specialist-validation plan, and sequence the packets by dependency. Retain every implementation gate as open; this preparation does not accept any recommendation or authorize code, schema, infrastructure, test, or runtime changes.
- Context and alternatives: The approved target policies in `P2-D016` establish what the future system must achieve, but implementation still lacked packet-level scope, rollback, specialist-review, and sequencing records. Beginning coding from the broad recommendations would create avoidable ambiguity; treating the prepared packets as accepted would bypass required specialist and program-owner decisions.
- Consequence: `P2-R001` through `P2-R007` are decision-ready proposals, governed by the specialist-validation plan and Phase 3 roadmap. They remain Proposed. The exit gate continues to require explicit acceptance, named owners, validation evidence, rollback planning, current deployment proof, and program-owner closure.
- Evidence: [recommendation register](recommendations/README.md), [specialist validation plan](specialist-validation-plan.md), [Phase 3 roadmap](phase-3-roadmap.md), [completion audit](phase-2-completion-audit.md), [Phase 2 exit gate](phase-2-exit-gate.md)

## P2-D018 — Maintain an iterative Phase 2 reassessment register

- Date: 2026-08-23
- Status: accepted
- Decision owner: AvenChart program owner
- Decision: Record Phase 2 as a continuing evidence loop during Phase 3 delivery. Preserve the fixed Phase 1 assessment as Iteration 1 and record each later re-assessment against its exact implementation target, including improvements, residual findings, candidates, specialist needs, recommendation effects, and gate status. Do not close a Phase 2 or implementation gate merely because an iteration is recorded.
- Context and alternatives: A one-time assessment would make it difficult to distinguish resolved baseline conditions from later regressions, partial remediation, or newly exposed cross-cutting risks. Rewriting the original findings to match later code would erase the experimental baseline. The iteration register keeps both forms of evidence visible and comparable.
- Consequence: [iterations/README.md](iterations/README.md) is the authoritative ledger. [Iteration 2](iterations/iteration-002-current-implementation-reassessment.md) is recorded as not converged: it corroborates meaningful improvements and retains material identity/browser, interoperability, structure, and verification work. Canonical baseline finding IDs and the Phase 2 exit gate remain unchanged until their separate evidence and decision requirements are satisfied.
- Evidence: [iteration register](iterations/README.md), [Iteration 2 reassessment](iterations/iteration-002-current-implementation-reassessment.md), [Phase 2 exit gate](phase-2-exit-gate.md)

## P2-D019 — Adopt a constrained Graphify code-navigation index

- Date: 2026-08-23
- Status: accepted as assessment tooling
- Decision owner: AvenChart program owner
- Decision: Add a pinned, deterministic, code-only Graphify index and a read-only Codex MCP registration to assist future repository navigation. Preserve the graph as a supporting artifact and keep the initial scope limited to committed supported product code, tests, and automation. Exclude documentation, synthetic data, the reference frontend, generated output, agent state, and environment files. Disable semantic/provider extraction, corpus ingestion, agent-stat collection, and writable stores unless separately authorized.
- Context and alternatives: Repeated large-repository exploration by LLM harnesses can be expensive and lose structural context. Graphify's local AST pass provides a potentially useful map of symbols, imports, calls, clusters, and impacts without treating index output as an assessment conclusion. A broad semantic corpus scan was not selected because it introduces provider-data, provenance, and staleness concerns that are unnecessary for the initial code-navigation purpose.
- Consequence: The repository gains reproducible tool dependencies, an ignore policy, update script, durable graph artifacts, and maintenance guidance. Graph counts, clusters, query answers, and reports are navigation aids only; no finding, recommendation, scorecard rating, specialist judgment, implementation gate, or production-readiness claim changes.
- Evidence: [Graphify integration record](tooling/graphify-code-navigation.md), [`tools/graphify/package.json`](../../tools/graphify/package.json), [update script](../../scripts/Update-AvenChartGraph.ps1)

## Decision amendment template

```markdown
## P2-D### — <decision>

- Date: YYYY-MM-DD
- Status: proposed | accepted | superseded | withdrawn
- Decision owner:
- Decision:
- Context and alternatives:
- Consequence:
- Supersedes / superseded by:
```
