# Sprint 46: applicant request eligibility verification

Status: Approved for bounded implementation by [TH-DEC-0049](../decisions/0049-approved-sprint-46-applicant-request-eligibility-verification.md)

Scope: One applicant-owned fresh request-time inquiry against a bounded in-process synthetic eligibility adapter, with protected-source decryption only in server memory and a request-only pending `Verification` version 6-to-7 advance; no raw payload copy or return, X12 serialization, external call, canonical coverage, coverage selection, exact network, financial, operational, queue, care, integration, or production consequence

## 1. Outcome

Evaluate the exact protected synthetic primary insurance source for this exact request using fresh evidence. Return separate minimized eligibility outcome dimensions, preserve an explicit 15-minute evidence window, and leave practice/rendering-physician network participation and every later gate visible and false.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP46-001` | Add an additive migration for one request-time eligibility result referencing the exact Sprint 45 insurance-source receipt and protected member source, with one-request uniqueness, outcome constraints, provenance guard, append-only enforcement, and zero-downstream constraints. |
| `TH-SP46-002` | Add a deterministic request-bound masked projection, two mandatory acknowledgments, exact version 6-to-7 semantics, protected-source validation rules, and prohibited-input absence. |
| `TH-SP46-003` | Add an access-key-bound transaction that locks and revalidates the full applicant/request/intake/insurance provenance, decrypts the protected payload only in server memory, invokes and validates the bounded adapter, and provides replay/contention safety. |
| `TH-SP46-004` | Add private/no-store applicant GET/POST endpoints with safe state-change handling, request correlation, stable Problem Details, minimized output, and semantic idempotency. |
| `TH-SP46-005` | Add an accessible no-edit verification form with unchecked acknowledgments, no-guarantee and correction guidance, stable retry, focus recovery, minimized outcome presentation, and no browser persistence. |
| `TH-SP46-006` | Prove adapter outcome contracts, exact masks/projection, success/replay/contention, changed-key/stale/expired/foreign/provenance denial, protection/non-copy, immutable evidence, unchanged upstream/canonical records, zero downstream action, migration recovery, and full regression. |

## 3. Entry gate

The applicant remains unexpired and `SyntheticRequestCreated` version 26; the request is pending `Verification` version 6 with `TelehealthEligible`; exactly one Sprint 45 insurance-source receipt exists; the protected member source and all earlier GA/CA/FL location, safety, intake, promotion, handoff, eligibility, and network provenance remain bound and immutable; the canonical patient remains portal-disabled and unmerged with no insurance record; and every request-time eligibility result, canonical coverage, coverage selection/verification, rendering-physician network result, financial, operational-review, contact, queue, appointment, encounter, consent, care, integration, and external consequence remains absent.

## 4. Mutation and result

The client reviews the masked request-bound source and makes two explicit acknowledgments. The server unprotects and validates the source only in command memory, invokes the fixed non-production adapter, validates its compatibility metadata and outcome tuple, appends one current eligibility result, advances only the request to pending `Verification` version 7, and appends one event. The applicant, patient, protected source, canonical insurance, clinical records, and earlier evidence remain unchanged.

The result does not select or create coverage and does not establish exact network participation. Even `EligibleBenefitsReported` requires separately authorized practice/rendering-physician network, coverage-selection, financial, operational, consent, and care gates.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/adapter | Exact two acknowledgments, snapshot determinism, protected mask validation, fixed non-production metadata, all normalized outcome mappings, 15-minute expiry, prohibited-input absence, and minimized response. |
| Data | Full provenance under locks, database-clock freshness, server-memory unprotection/no-copy proof, exact one result/event, replay/contention, immutable evidence, and no upstream/canonical/downstream mutation. |
| HTTP | Access-key ownership, configured host/practice/facility, private/no-store, safe errors, request correlation, idempotency, and no prohibited fields. |
| UI | No editable source or outcome fields or defaults, no-guarantee/correction guidance, explicit outstanding gates, stable retry, result recovery, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL, migration/recovery, runtime/authorization/OpenAPI, queue regression, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |

## 6. Gate preserved

No real X12, payer connectivity, exact network determination, operational work, or care is bundled into this slice. Practice and rendering-physician network determination, canonical coverage and selection, benefits/patient-responsibility calculation, estimate/financial acknowledgment, consent, operational review, practice acceptance, appointment/queue creation, and care require separately bounded decisions.
