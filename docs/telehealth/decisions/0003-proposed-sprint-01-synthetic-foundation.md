# Decision 0003: Sprint 1 synthetic-foundation authorization

Status: Approved — active for the exact disabled synthetic Sprint 1 scope  
Proposed date: 2026-08-26  
Approved date: 2026-08-26  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Risk owner: AvenChart program owner  
Clinical-content owner: Medical director or delegated qualified clinician before any non-synthetic protocol  
Review/expiry: 2026-10-31, or immediately when superseded by a Phase 2 gate decision

## 1. Decision requested

Authorize the complete [Sprint 1 synthetic foundation](../backlog/sprint-01-foundation.md) as a scoped Phase 3 implementation exception. The authorized outcome is the disabled-by-default, synthetic-only vertical slice already approved in Decision 0001:

```text
verified practice host
  -> public non-PHI practice context
  -> authenticated established synthetic patient
  -> Draft request
  -> current-location attestation
  -> deterministic synthetic emergency/triage result
  -> operational review
  -> authorized synthetic administrator acceptance
  -> clinician queue
  -> atomic reserve-next by one eligible synthetic physician
```

This decision would accept only the bounded portions of `P2-R001`, `P2-R004`, `P2-R005`, `P2-R006` and `P2-R007` required to build and verify that slice. It would not close those recommendations, their linked findings, or the Phase 2 exit gate.

## 2. Exact authorized implementation scope

If approved, changes are limited to the following implementation surfaces and the smallest composition edits required to connect them:

```text
avenchart/backend/src/AvenChart.Api/Features/Telehealth/**
avenchart/backend/src/AvenChart.Api/Program.cs
avenchart/backend/src/AvenChart.Api/appsettings*.json
avenchart/backend/tests/AvenChart.Api.Tests/Telehealth/**
avenchart/database/migrations/V0282__telehealth_foundation.sql
avenchart/scripts/Test-TelehealthMigrationResilience.ps1
avenchart/scripts/Test-TelehealthOpenApiContract.ps1
avenchart/scripts/Test-TelehealthQueueConcurrency.ps1
avenchart/scripts/Test-TelehealthAuthorization.ps1
avenchart/scripts/Test-TelehealthRuntimeSafety.ps1
avenchart-ui/src/features/telehealth/**
avenchart-ui/src/App.tsx
avenchart-ui/e2e/telehealth-accessibility.spec.ts
avenchart-ui/e2e/telehealth-failure-recovery.spec.ts
.github/workflows/verify.yml
.github/workflows/runtime-evidence.yml
docs/telehealth/**
scripts/Test-TelehealthPlanningArtifacts.ps1
```

An implementation discovery may show that one additional composition, project, test-registration or configuration file is strictly required. Work must stop before touching it; the decision record must name and justify the file first. Existing non-telehealth domain behavior may not be refactored under this authorization merely for convenience.

## 3. Explicit exclusions

This decision does not authorize:

- real patient, workforce, payer, pharmacy or clinician data;
- public availability, patient care, production enablement or a deployment;
- a production clinical protocol or clinical eligibility claim;
- anonymous/new-patient chart creation, identity proofing or chart promotion;
- insurance, network, price, payment, video, messaging, prescribing, claims or notification integrations;
- live identity, video, payer, pharmacy, clearinghouse or other vendor credentials;
- recording, controlled-substance functionality or external delivery;
- changes to existing patient, encounter, billing, laboratory, pharmacy or reporting workflows outside an explicit compatibility seam; or
- acceptance or closure of unrelated Phase 2 findings.

## 4. Phase 2 findings affected

| Finding | Scoped relationship and residual status |
|---|---|
| `P2-04-F001` | A new migration depends on the current schema/bootstrap authority. Empty and populated PostgreSQL rehearsals are mandatory; the finding remains open for the whole repository. |
| `P2-05-F001` | Sprint 1 uses only the deterministic test identity boundary. Production configuration must reject that boundary; no production identity provider is selected or claimed. |
| `P2-05-F002` | New telehealth resources must enforce practice, patient, facility, purpose and role scope. Existing ordinary chart-access findings remain open. |
| `P2-05-F003` | Every new protected-resource decision and transition must retain resource-correlated audit evidence. Existing audit gaps remain open. |
| `P2-03-F007` | New React state must bind results/actions to the current request and reject stale responses. Existing affected routes remain open. |
| `P2-08-F005` | Telehealth queue refresh must not preserve actionable state across a failed or changed selection. The existing Flow Board finding remains open. |
| `P2-08-F006` | Telehealth failures require perceivable, operable recovery. Existing clinician-route recovery gaps remain open. |
| `P2-07-F001` | `/api/telehealth/v1` must publish its actual auth, Problem Details, idempotency and concurrency contract. Existing OpenAPI scope remains governed separately. |
| `P2-09-F001` | The 329-requirement denominator and Sprint evidence remain versioned; this does not establish whole-product parity. |
| `P2-09-F002` | Real PostgreSQL, authorization, runtime-safety and browser checks are mandatory for the slice. The broader default-gate finding remains open. |

No other High finding is accepted. These findings are not closed; their residual risk is accepted only for isolated synthetic implementation and evidence generation within the expiry window.

## 5. Residual risk accepted for this scope

The program owner would accept that:

- new code is being developed while broader production identity, resource, audit, schema, UI-recovery and verification findings remain open;
- a synthetic protocol proves evaluator mechanics, not clinical appropriateness;
- a feature-disabled configuration and test data reduce exposure but do not prove production safety;
- the current application composition and migration foundation may require compatibility work discovered during implementation; and
- automated evidence created by the same delivery stream requires independent review before later gates.

This acceptance is solely for building a non-production test artifact. It does not accept risk for patient care, external users, production deployment or later telehealth increments.

## 6. Required compensating controls

1. The feature defaults off in every configuration and has no endpoint behavior when disabled except a non-PHI capability indication if explicitly specified.
2. Production-mode startup fails if telehealth is enabled with test identity, synthetic protocol/data, stub adapter, permissive host mapping or unsafe defaults.
3. Only deterministic synthetic identities, practices, patients, protocols and destinations are used.
4. Emergency evaluation precedes any operational transition; an emergency, unknown or invalid result cannot reach the queue.
5. Administrative commands cannot edit or override clinical outcomes.
6. Practice, patient, facility, purpose, role and resource authorization is enforced server-side and covered by negative tests.
7. Request transitions are explicit, versioned and auditable; commands require idempotency and optimistic-concurrency evidence.
8. Queue authorization and `reserve-next` are atomic in real PostgreSQL and enforce one active reservation per request and clinician.
9. Schema changes are additive; empty and populated migration rehearsals, failure recovery and rollback-compatible evidence pass.
10. The React feature remains modular, keyboard operable and safe under stale, failed and reordered responses.
11. No PHI or sensitive intake content appears in logs, metrics, traces, URLs, workflow artifacts or Graphify inputs.
12. `TH-SG-001` remains mandatory and safeguards `TH-SG-002` through `TH-SG-008` activate before their trigger paths merge.
13. Existing verification gates may be extended but not weakened, bypassed or made optional.

## 7. Stop conditions

Stop implementation and revoke the exception if:

- any real person or live destination enters the workflow;
- telehealth can be enabled in Production or a production-like environment with synthetic/test dependencies;
- an unsafe triage result reaches operational review or a non-clinician can change it;
- cross-practice, cross-patient, missing-purpose, wrong-role or stale-version access succeeds;
- concurrent callers can double-authorize, double-queue or double-reserve;
- an empty/populated migration or recovery rehearsal fails;
- a critical accessibility, clinical-safety, authorization, audit or data-integrity defect remains unresolved;
- the implementation needs an unlisted code path; or
- the decision expires without review.

## 8. Rollback and recovery

The feature flag remains the immediate rollback and must disable route registration/behavior without deleting evidence. Database rollback is forward recovery: leave additive tables dormant or apply a separately reviewed corrective migration; never edit or remove an applied migration and never delete durable request/audit evidence. UI route registration may be removed while retaining test and incident evidence. A stop-condition failure blocks further Sprint 1 work until reviewed.

## 9. Evidence required to close the scoped Sprint 1 packet

- every `TH-SP1-001` through `TH-SP1-014` deliverable is linked to implementation and tests;
- Release build, unit, formatting, lint, frontend test/build and planning validation pass;
- real PostgreSQL empty/populated migration and concurrency evidence passes;
- OpenAPI, cross-resource authorization and production-startup rejection suites pass;
- desktop/mobile keyboard, automated accessibility and failure-recovery browser evidence passes;
- no live destinations, secrets, PHI logs or production-enabling defaults are present;
- changed-file review proves the implementation remained inside the authorized scope;
- Graphify delta review is refreshed for meaningful committed-code changes;
- independent clinical-safety, security/privacy, data and accessibility review of the bounded slice records no unresolved blocker; and
- the program owner reviews the evidence before the expiry date.

Packet closure would authorize planning for the next increment only. It would not authorize real patient care, production release, a vendor integration, `G2`, `G3`, `G4`, or closure of the Phase 2 exit gate.

## 10. Approval instruction

This proposal becomes effective only if the AvenChart program owner explicitly instructs:

> Approve Decision 0003 exactly as written. Accept the listed residual risk only for the disabled, synthetic Sprint 1 foundation through 2026-10-31. Authorize only the listed paths and keep all other Phase 2 and telehealth release gates open.

Any broader instruction requires a revised decision record.

## 11. Approval record

On 2026-08-26, the AvenChart program owner explicitly stated, “I approve all Decisions.” Decision 0003 is therefore effective exactly as written through its review/expiry date. The approval accepts only the residual risk and paths listed above; it does not authorize production enablement, real patient care, live integrations, G2–G4, or closure of the repository Phase 2 exit gate.

## References

- [Decision 0001](0001-g0-development-baseline.md)
- [Decision 0002](0002-proposed-scoped-verification-authorization.md)
- [Sprint 1 plan](../backlog/sprint-01-foundation.md)
- [Engineering safeguards](../backlog/engineering-safeguards.md)
- [Phase 2 exit gate](../../phase-2/phase-2-exit-gate.md)
- [Phase 3 roadmap](../../phase-2/phase-3-roadmap.md)
- [P2-R001](../../phase-2/recommendations/p2-r001-identity-resource-safety.md)
- [P2-R004](../../phase-2/recommendations/p2-r004-data-schema-persistence-recovery.md)
- [P2-R005](../../phase-2/recommendations/p2-r005-ui-response-recovery-accessibility.md)
- [P2-R006](../../phase-2/recommendations/p2-r006-contracts-integration-report-governance.md)
- [P2-R007](../../phase-2/recommendations/p2-r007-verification-release-operations.md)
