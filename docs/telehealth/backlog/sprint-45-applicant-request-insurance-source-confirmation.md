# Sprint 45: applicant request insurance-source confirmation

Status: Approved for bounded implementation by [TH-DEC-0048](../decisions/0048-approved-sprint-45-applicant-request-insurance-source-confirmation.md)

Scope: One applicant-owned, no-edit, masked request insurance-source confirmation from exact pending `Verification` version 5 to pending `Verification` version 6; no raw identifier copy, canonical coverage, current eligibility/network result, rendering-physician check, financial route, operational-review work, contact, queue, appointment, encounter, care, integration, external, or production consequence

## 1. Outcome

Bind the already protected and post-promotion-confirmed synthetic primary insurance source to this exact request, explicitly request a later fresh verification, preserve prior eligibility/network results only as historical provenance, and make every current verification and downstream gate visible and false.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP45-001` | Add an additive migration for one protected request insurance-source receipt referencing the exact Sprint 44 intake, insurance handoff, member-details payload, historical eligibility/network evidence, promotion/review chain, and request, with one-request uniqueness, append-only enforcement, and zero-downstream constraints. |
| `TH-SP45-002` | Add a deterministic policy for a request-bound insurance-source snapshot, masked projection, seven mandatory confirmations, historical-result labeling, exact version 5-to-6 semantics, and prohibited-input absence. |
| `TH-SP45-003` | Add an access-key-bound repository transaction that revalidates the complete request/intake/insurance source chain, protected-payload reference without decryption or duplication, current context, replay/contention, and exact same-status version advance. |
| `TH-SP45-004` | Add private/no-store applicant GET/POST endpoints with safe state-change handling, request correlation, stable Problem Details, minimized output, and idempotency. |
| `TH-SP45-005` | Add an accessible no-edit confirmation form with historical-only result labels, correction guidance, seven unchecked affirmations, stable retry, focus recovery, and no browser persistence. |
| `TH-SP45-006` | Prove exact masks/projection, success/replay/contention, changed-key/stale/expired/foreign/source/patient/intake/insurance drift denial, protected-source non-duplication, immutable evidence, unchanged upstream/canonical records, zero downstream action, migration recovery, and full regression. |

## 3. Entry gate

The applicant remains unexpired and `SyntheticRequestCreated` version 26; the request is pending `Verification` version 5 with `TelehealthEligible`; exactly one Sprint 44 intake and protected receipt exists; the GA/CA/FL location and safety context remain current; the canonical patient remains portal-disabled and unmerged with no insurance record; the exact protected member-details, synthetic eligibility, practice-network, promotion, registration, and insurance-handoff receipts remain bound and immutable; the historical outcomes are not accepted as current request evidence; and every coverage selection/verification, rendering-physician network result, financial, operational-review, contact, queue, appointment, encounter, consent, care, integration, and external consequence remains absent.

## 4. Mutation and result

The client reviews the masked payer/product/member/group/relationship/priority source, sees the previous synthetic result labels as historical-only, and makes seven explicit confirmations. The server appends one protected request insurance-source receipt, advances only the request to pending `Verification` version 6, and appends one event. The applicant, patient, protected payload, canonical insurance, clinical records, earlier evidence, and all current eligibility/network/downstream records remain unchanged.

`fresh_verification_requested=true` is intent for a later separately authorized step. It does not perform or schedule an external call, reuse the earlier result, create a work item, or establish coverage, exact network, payment, price, acceptance, queueing, or care.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Mask determinism, exact seven confirmations, historical-only semantics, snapshot determinism, prohibited-input absence, same-status version advance, and minimized response. |
| Data | Full provenance under locks, database-clock freshness, protected-payload reference/no-copy proof, exact one receipt/event, replay/contention, immutable evidence, and no upstream/canonical/downstream mutation. |
| HTTP | Access-key ownership, configured host/practice/facility, private/no-store, safe errors, request correlation, idempotency, and no prohibited fields. |
| UI | No editable insurance fields or defaults, historical/no-guarantee language, correction guidance, explicit outstanding gates, stable retry, result recovery, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL, migration/recovery, runtime/authorization/OpenAPI, queue regression, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |

## 6. Gate preserved

No verification execution or operational work is bundled into this slice. Protected-source decryption, current eligibility/benefits, exact rendering-physician network determination, canonical coverage creation, estimate/financial acknowledgment, consent, operational review, practice acceptance, appointment/queue creation, and care require separately bounded decisions.
