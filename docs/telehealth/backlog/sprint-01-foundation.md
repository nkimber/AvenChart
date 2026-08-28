# Sprint 1: disabled telehealth foundation

Status: Bounded automated implementation evidence passing; independent reviews and program-owner packet review pending  
Sprint objective: Establish a safe, disabled-by-default vertical foundation from practice host resolution through deterministic request state, without real patient care or live integrations.

Authorization: [Decision 0003](../decisions/0003-proposed-sprint-01-synthetic-foundation.md) is approved for this exact bounded slice through 2026-10-31.

Implementation and automated evidence are indexed in the [Sprint 1 evidence packet](sprint-01-evidence.md). The [runbook](sprint-01-runbook.md) and [release manifest](sprint-01-release-manifest.json) are non-production controls for the synthetic slice. They do not close the independent-review or program-owner evidence-review criteria below.

Under approved [Decision 0004](../decisions/0004-proposed-bootstrap-schema-reconciliation.md), the generated bootstrap was deterministically normalized from CRLF to LF with no logical-line change and verified at SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2`. The full repository empty/populated/interruption/recovery rehearsal and the telehealth-specific schema assertions now pass. This closes the automated bootstrap condition only; it does not satisfy the independent-review or production gates.

## 1. Sprint outcome

At completion, a developer can run a synthetic local/API test showing:

```text
Verified practice host
  -> public non-PHI practice context
  -> authenticated established synthetic patient
  -> new TelehealthRequest in Draft
  -> current-location attestation
  -> emergency/safety command evaluated by a deterministic fixture protocol
  -> unsafe result terminates OR eligible result reaches OperationalReview
  -> authorized synthetic administrator queues the request
  -> one eligible synthetic physician reserves it atomically
```

The feature remains disabled in production configuration. No new-patient promotion, insurance vendor, video, prescription, claim, notification delivery, or patient-care deployment is included.

## 2. Committed stories

| Sprint item | Primary backlog stories | Deliverable | Estimate placeholder |
|---|---|---|---|
| `TH-SP1-001` | `TH-E13-S01` | Telehealth feature folders, DI/route registration extensions, dependency tests, feature option validated on start | Team estimate |
| `TH-SP1-002` | `TH-E16-S01` | Resource/action authorization contract for public context, patient request, admin authorize, clinician reserve | Team estimate |
| `TH-SP1-003` | `TH-E14-S01` | Additive migration for request/event/location/protocol/assessment/queue/shift/reservation foundation with constraints | Team estimate |
| `TH-SP1-004` | `TH-E03-S01` | Pure request state machine and permitted-transition tests | Team estimate |
| `TH-SP1-005` | `TH-E15-S01` | `/api/telehealth/v1` route group, typed Problem Details, version/idempotency conventions, OpenAPI snapshot | Team estimate |
| `TH-SP1-006` | `TH-E07-S01` | Host-to-practice resolver, approved public projection, unknown/ambiguous/disabled fail-closed behavior | Team estimate |
| `TH-SP1-007` | `TH-E05-S01` | Protocol schema/evaluator interface and synthetic universal safety fixture; no production clinical content | Team estimate |
| `TH-SP1-008` | `TH-E01-S01` | Patient-facing feature shell and public landing/safety entry behind disabled flag | Team estimate |
| `TH-SP1-009` | `TH-E07-S02` | OperationalReview projection and atomic authorize-to-queue command using synthetic gates | Team estimate |
| `TH-SP1-010` | `TH-E07-S03` | Clinician shift and atomic `reserve-next` lease with real PostgreSQL concurrency test | Team estimate |
| `TH-SP1-011` | `TH-E17-S01` | Accessible patient/admin/physician route shells based on approved wireframes | Team estimate |
| `TH-SP1-012` | `TH-E19-S01` | Requirement/OpenAPI/migration/auth/concurrency/runtime-safety CI evidence wired to the implementation paths | Team estimate |
| `TH-SP1-013` | `TH-E18-S01` | Safe structured telemetry, health capability and runbook skeleton; no PHI labels | Team estimate |
| `TH-SP1-014` | `TH-E20-S01` | Release manifest records exact feature/config/schema/protocol/test versions and keeps G2–G4 disabled | Team estimate |

## 3. Explicit non-scope

- No production clinical protocol or medical claim.
- No anonymous/new-patient chart creation or identity proofing.
- No eligibility/network, price/GFE or payment behavior.
- No video provider, SignalR patient status, chat, e-prescribing or claim adapter.
- No live notification, vendor credential, production domain or PHI.
- No closure of G2, G3 or G4.

## 4. Engineering decisions to record during implementation

1. Telehealth feature namespaces/projects and dependency enforcement mechanism.
2. PostgreSQL status representation, aggregate-version constraint and append-only event guard.
3. Atomic `reserve-next` SQL/transaction strategy and lease clock source.
4. Existing appointment projection timing/status mapping.
5. Resource authorization interface and practice/patient/request lookup order.
6. Problem Details code catalog and idempotency fingerprint normalization.
7. React route/module boundaries so telehealth does not expand a monolithic component.

These decisions may refine implementation details but cannot weaken the master invariants.

## 5. Required tests

- Pure state transition table including every invalid transition.
- Real PostgreSQL schema/bootstrap/migration and rollback-compatible rehearsal.
- Twenty-or-more concurrent `reserve-next` callers proving one active reservation per request and physician.
- Cross-practice, cross-patient, missing-purpose, wrong-role and stale-version denials.
- Idempotent replay and conflicting key reuse.
- Unknown/disabled/ambiguous host and forged practice parameter denial.
- Emergency outcome proves no operational/financial/queue work occurs.
- Production-mode startup rejects enabled feature with synthetic protocol/identity/adapter configuration.
- OpenAPI includes required security, idempotency, concurrency and Problem Details responses.
- Keyboard/screen-reader/zoom tests for the route shells and safety action.

## 6. Exit criteria

- All committed stories meet specification 19's definition of done.
- All tests above pass in CI and the runtime evidence workflow.
- Empty and populated database migration rehearsals pass without editing existing migrations.
- No sequential public identifiers, PHI logs, live destinations or production-enabling defaults are introduced.
- Feature is demonstrable only with deterministic synthetic data and remains off in production.
- Independent review finds no unresolved clinical-safety, tenant-isolation, data-integrity, authorization or critical accessibility defect.
- Sprint evidence is linked to every mapped requirement; no G2/G3/G4 claim is made.
