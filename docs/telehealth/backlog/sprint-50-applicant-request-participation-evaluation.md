# Sprint 50: applicant request participation evaluation

Status: Approved for bounded implementation by [TH-DEC-0053](../decisions/0053-approved-sprint-50-applicant-request-participation-evaluation.md)

Scope: Evaluate one server-owned, effective-dated GA/CA/FL synthetic exact billing-entity and rendering-provider participation tuple after the immutable Sprint 49 context; advance only the request from pending `Verification` version 10 to version 11; do not verify real authority, credentials, payer or directory data, assign a clinician, establish canonical coverage, or create financial, operational, queue, care, integration, or production consequence.

## Outcome

Produce a truthful, immutable non-production result that distinguishes an exact match within a fixed synthetic catalog from real provider participation verification or a coverage guarantee. Include the new-patient-acceptance dimension required by the practice-branded acquisition flow.

## Stories

| ID | Story |
|---|---|
| `TH-SP50-001` | Add V0325 with one-request and one-context uniqueness, exact tuple/effective-period constraints, synthetic-match-only flags, append-only enforcement, and zero-real/downstream constraints. |
| `TH-SP50-002` | Add a deterministic state-specific policy, compatibility target, business outcome, opaque snapshot, new-patient dimension, and four mandatory acknowledgments. |
| `TH-SP50-003` | Add an access-key-bound locked transaction with full context and live roster provenance revalidation, exact replay, stale/foreign/drift/mismatch denial, and first-writer safety. |
| `TH-SP50-004` | Add private/no-store applicant GET/POST endpoints with stable Problem Details, correlation, semantic idempotency, and no provider, reference, network, or outcome inputs. |
| `TH-SP50-005` | Add an accessible no-edit evaluation review with unchecked acknowledgments, stable retry, focus recovery, masked output, explicit synthetic-versus-real language, reflow, and no browser persistence. |
| `TH-SP50-006` | Prove GA/CA/FL exact synthetic results, access isolation, replay/contention, context/roster drift denial, immutability, no real/downstream implication, migration recovery, and full regression. |

## Entry gate

The applicant is unexpired and unchanged at version 26; the portal-disabled unmerged patient shell is unchanged; the request is exactly `Verification` version 10 with `TelehealthEligible`; exactly one immutable Sprint 49 context binds the exact current eligibility, practice-network, rendering-candidate, practice/facility, staff roster, authority, role, affiliation, billing, contract, network, service, location, modality, state, new-patient, date-of-service, and effective-period provenance; no participation evaluation or downstream consequence exists.

## Mutation

After four acknowledgments, the server evaluates the immutable tuple against the fixed non-production catalog, appends one synthetic exact-match result, advances only the request to `Verification` version 11, and appends one event. Upstream evidence, applicant, patient, and staff records remain unchanged.

## Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact GA/CA/FL mappings, source/compatibility identity, effective dates, new-patient acceptance, synthetic-only outcome, masked output, snapshot, and acknowledgment validation. |
| Data | Locks, database-clock freshness, one evaluation/event, replay/contention, completed-result provenance revalidation, append-only evidence, and zero upstream/downstream mutation. |
| HTTP | Applicant ownership, private/no-store, idempotency, safe errors, configured host/facility, and prohibited-input absence. |
| UI | No provider/reference/network/outcome selector, four unchecked acknowledgments, masked output, stable retry, focus/reflow/keyboard evidence, and no persistence. |
| Regression | Backend, frontend, browser, migrations/recovery, runtime, authorization, OpenAPI, queue, planning, Graphify, bootstrap, and cleanup. |

## Gate preserved

Sprint 51 must separately authorize the next request progression gate. Real state-authority verification, real credentialing, real payer/directory participation verification, assignment, operational availability, canonical coverage, and all care consequences remain later gates.
