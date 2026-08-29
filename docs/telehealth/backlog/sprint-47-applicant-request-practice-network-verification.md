# Sprint 47: applicant request practice-network verification

Status: Approved for bounded implementation by [TH-DEC-0050](../decisions/0050-approved-sprint-47-applicant-request-practice-network-verification.md)

Scope: One applicant-owned fresh request-time practice/facility/service network inquiry after a current positive Sprint 46 eligibility result, with a request-only pending `Verification` version 7-to-8 advance; no member data sent to the adapter, rendering physician selection/check, exact network, canonical coverage, financial, operational, queue, care, integration, or production consequence

## 1. Outcome

Evaluate the exact configured synthetic practice/facility/service context against the applicant's exact current plan and eligibility evidence. Return separate minimized practice-network outcome dimensions, preserve the earlier-of composite freshness window, and leave rendering-physician participation and every later gate visible and false.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP47-001` | Add an additive migration for one request-time practice-network result referencing the exact Sprint 46 eligibility result, with one-request uniqueness, outcome constraints, provenance guard, append-only enforcement, and zero-downstream constraints. |
| `TH-SP47-002` | Add a deterministic request-bound minimized projection, three mandatory acknowledgments, exact version 7-to-8 semantics, current-positive-eligibility rules, and prohibited-input absence. |
| `TH-SP47-003` | Add an access-key-bound transaction that locks and revalidates the full applicant/request/eligibility provenance, invokes and validates the bounded adapter with no member data, and provides replay/contention safety. |
| `TH-SP47-004` | Add private/no-store applicant GET/POST endpoints with safe state-change handling, request correlation, stable Problem Details, minimized output, and semantic idempotency. |
| `TH-SP47-005` | Add an accessible no-edit verification form with unchecked acknowledgments, exact-network and no-guarantee guidance, stable retry, focus recovery, minimized outcome presentation, and no browser persistence. |
| `TH-SP47-006` | Prove three adapter outcomes, exact projection, success/replay/contention, non-positive/stale/expired/foreign/provenance denial, no-member adapter boundary, immutable eligibility/network evidence, zero downstream action, migration recovery, and full regression. |

## 3. Entry gate

The applicant remains unexpired and `SyntheticRequestCreated` version 26; the request is pending `Verification` version 7 with `TelehealthEligible`; exactly one fresh current Sprint 46 eligibility result is matched, active, benefits-reported, and `EligibleBenefitsReported`; all earlier GA/CA/FL location, safety, intake, promotion, handoff, and insurance provenance remains bound and immutable; the canonical patient remains portal-disabled and unmerged with no insurance record; and every request practice-network result, rendering-physician assignment/result, canonical coverage, coverage selection/verification, financial, operational-review, contact, queue, appointment, encounter, consent, care, integration, and external consequence remains absent.

## 4. Mutation and result

The client reviews the exact practice/plan/current-eligibility context and makes three explicit acknowledgments. The server invokes the fixed non-production adapter with only server-bound practice, facility, plan key, state, date, service category, and database time; validates the compatibility metadata and outcome tuple; appends one current practice-network result; advances only the request to pending `Verification` version 8; and appends one event. The applicant, patient, eligibility, insurance source, protected payload, canonical insurance, clinical records, and earlier evidence remain unchanged.

Even `PracticeInNetworkAcceptingNewPatients` is not exact network confirmation. A rendering physician must later be selected and checked against the exact member/product, billing entity, location, service, state, and date through a separately authorized gate.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/adapter | Exact three acknowledgments, snapshot determinism, fixed non-production metadata, all normalized outcome mappings, composite expiry, prohibited-input absence, no-member inquiry, and minimized response. |
| Data | Full provenance under locks, database-clock freshness, exact one result/event, replay/contention, immutable eligibility/network evidence, and no upstream/canonical/downstream mutation. |
| HTTP | Access-key ownership, configured host/practice/facility, private/no-store, safe errors, request correlation, idempotency, and no prohibited fields. |
| UI | No editable context/outcome fields or defaults, practice-only/no-guarantee guidance, explicit outstanding gates, stable retry, result recovery, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL, migration/recovery, runtime/authorization/OpenAPI, queue regression, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |

## 6. Gate preserved

No FHIR serialization, provider-directory or payer connectivity, rendering-physician selection/check, exact network determination, coverage/financial route, operational work, or care is bundled into this slice. Exact physician participation, canonical coverage and selection, benefits/patient-responsibility calculation, estimate/financial acknowledgment, consent, operational review, practice acceptance, appointment/queue creation, and care require separately bounded decisions.
