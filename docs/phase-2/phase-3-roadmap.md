# Phase 3 recommendation roadmap

**Status:** Preparation only. This is a dependency-ordered delivery proposal, not authorization to change product code or close a gate.
**Baseline:** `phase-1-experimental^{}` = `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
**Policy context:** [P2-D016](decision-log.md) supplies approved targets; the [Phase 2 exit gate](phase-2-exit-gate.md) remains open.

## Delivery posture

Phase 3 should improve the supported modern Claude clinician and portal UI, not the reference UI. It should use the existing ASP.NET Core/PostgreSQL application as the migration surface; it does not imply a blanket Entity Framework conversion or a platform rewrite. FHIR R4/SMART and an external-laboratory API exercised with synthetic laboratory messages are required targets. OIDC/SSO must remain vendor-neutral and be proven with a deterministic test IdP before provider configurations are accepted. Multi-facility and purpose-of-use authorization are required.

The seven recommendations below are still **Proposed**. A wave may begin only when its packets have named owners, the applicable [specialist validation plan](specialist-validation-plan.md) reviews are complete, scope and rollback are accepted, and its prerequisite evidence is available. No wave closing statement can close a Phase 2 gate without explicit instruction.

## Dependency model

| Recommendation | Depends on | Enables |
| --- | --- | --- |
| `P2-R004` — schema/persistence | `P2-R007-A` test manifest for evidence discipline | Durable constraints and migration support for `R001`, `R002`, `R003`, and `R006` |
| `P2-R007` — verification/release | Starts immediately and evolves with every wave | Credible implementation, migration, browser, recovery, and release evidence for all work |
| `P2-R001` — identity/resource safety | Initial schema/test foundation from `R004`/`R007` | Facility/purpose/session/audit boundary required by clinical, reporting, FHIR, laboratory, and operations work |
| `P2-R005` — modern UI recovery/accessibility | `R007-A`; API error/concurrency decisions from `R001`/`R002`/`R003` as they land | Safe UI adoption of new authorization, concurrency, and workflow contracts |
| `P2-R002` — clinical record integrity | `R004` for constraints/migrations; `R001` for protected resource policy | Correct laboratory aggregate and lifecycle rules required by `R003` and `R006` |
| `P2-R003` — workflows/history/recovery | `R001`, `R002`, `R004`, `R007` foundations | Reliable operational events and delivery/recovery behavior for integrations and release evidence |
| `P2-R006` — contracts/integration/report governance | `R001` resource policy; `R002-D` laboratory aggregate; `R004` migrations; `R007` contract/recovery suites | Standards-conformant FHIR/lab/report boundary and future partner integration |

## Proposed waves

| Wave | Objective and packets | Primary evidence before progressing |
| --- | --- | --- |
| **0 — Evidence and foundation decisions** | `R007-A` verification manifest; `R004-A` schema-authority selection/bootstrap proof; `R006-A` API/OpenAPI contract design; detailed specialist rule matrices | Named owners, baseline suite inventory, empty-DB design/experiment, published contract/profile decisions, rollback plans |
| **1 — Trust and resource boundary** | `R001-A` identity/claim/test-IdP; `R001-B` session/browser lifecycle; `R001-C` facility/purpose/resource authorization; `R001-D` audit/attestation; `R005-A` UI race/failure test foundation | Test-IdP/vendor-neutral claim evidence, fail-closed cross-scope cases, audit continuity, browser fault fixtures, security/privacy/HIM review |
| **2 — Clinical integrity and safe interaction** | `R004-B/C` constraints/performance evidence; `R002-A` patient identity/lifecycle; `R002-B` encounter integrity; `R002-C` clinical-list/prescription evidence; `R005-B/C` high-risk UI and mutation recovery | Concurrent/stale-write and lifecycle tests, clinical corrections/recovery evidence, database plans/lock limits, safe modern UI failure behavior |
| **3 — Closed-loop workflow and laboratory aggregate** | `R002-D` order/specimen/report/result aggregate; `R003-A` scheduling; `R003-B` communication/follow-up; `R003-C` therapy; `R003-D/E` controlled/financial and recovery | Specialist-approved state matrices, two-actor/fault tests, outcome/reconciliation evidence, synthetic laboratory aggregate evidence, operational recovery drill |
| **4 — Governed interoperability and reports** | `R006-B` FHIR R4/SMART; `R006-C` synthetic external laboratory intake; `R006-D` integration transport/evidence; `R006-E` reports/configuration governance | Validator and content-negotiation/error evidence, synthetic source create/correct/replay/reconcile, least-privilege report/artifact tests, configuration policy proof |
| **5 — Release and operational proof** | `R005-D` accessibility closure; `R007-B/C/D/E` full risk-shaped suites, supply chain, deployment/operations, production-worthiness decision; `R004-D` recovery/topology proof | Independent accessibility result, current environment evidence, migration/backup/restore/rollback rehearsal, SBOM/provenance, residual-risk acceptance |

Waves intentionally overlap only where the dependency table permits it: `R007` remains active throughout, and `R005-A` can begin with Wave 1. The roadmap favors small, independently verifiable packets over large simultaneous rewrites.

## Entry criteria for an individual Phase 3 packet

1. The parent recommendation is explicitly accepted by its decision owner; today none is accepted.
2. The packet names implementation, decision, specialist, operations, and rollback owners.
3. Its semantic rule/exception matrix and the relevant specialist reviews are recorded.
4. Its migration, compatibility, threat/privacy, accessibility, observability, recovery, and test evidence requirements are proportionate to its risk.
5. Its dependencies are either complete or represented by an approved compatibility contract.
6. A baseline, success measure, stop condition, and rollback/forward-recovery plan are stated before implementation begins.

## Boundaries and later decisions

- The reference UI is out of Phase 3 scope.
- Real partner laboratory onboarding, FHIR certification, SAML federation details, HL7 v2 adapter work, clearinghouse connectivity, and production deployment occur only under later explicitly accepted packets. They are not implied by the synthetic laboratory and standards-contract requirement.
- The selected external identity provider is intentionally undecided. Provider-neutral OIDC/OAuth, SSO, and a first-party test IdP are the accepted target; provider-specific rollout is a later configuration decision.
- External delivery, legal retention, compliance applicability, and clinical policy are not inferred from implementation. They remain accountable specialist/owner decisions.

## Roadmap success condition

The roadmap is complete when every accepted packet has independently verified evidence, all linked High findings are either remediated or explicitly accepted with mitigations, the production-worthiness release evidence is current, and the program owner explicitly directs the applicable gate decision. Until then, the Phase 2 gate stays open.
