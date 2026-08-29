# Sprint 49: applicant request participation context

Status: Approved for bounded implementation by [TH-DEC-0052](../decisions/0052-approved-sprint-49-applicant-request-participation-context.md)

Scope: Confirm one server-owned effective-dated GA/CA/FL synthetic prerequisite context after the immutable rendering-candidate selection; advance only the request from pending `Verification` version 9 to version 10; do not verify real authority or credentials, evaluate exact participation, assign a clinician, or establish coverage, financial, operational, queue, care, integration, or production consequence.

## Outcome

Give a future exact participation evaluator a fixed, provenance-bound set of synthetic practitioner, authority, role, affiliation, billing-organization, contract, network, service, location, modality, and date-of-service references while accurately preserving the distinction between prerequisite context and a verification result.

## Stories

| ID | Story |
|---|---|
| `TH-SP49-001` | Add V0324 with one-request and one-candidate-selection uniqueness, exact upstream references, effective-period/freshness constraints, context-only flags, append-only enforcement, and zero-downstream constraints. |
| `TH-SP49-002` | Add a deterministic state-specific policy and opaque snapshot with fixed GA/CA/FL synthetic prerequisite references, masked output, and four mandatory acknowledgments. |
| `TH-SP49-003` | Add an access-key-bound locked transaction with full candidate and live roster provenance revalidation, exact replay, stale/foreign/drift denial, and first-writer safety. |
| `TH-SP49-004` | Add private/no-store applicant GET/POST endpoints with stable Problem Details, correlation, semantic idempotency, and no provider, authority, contract, or network inputs. |
| `TH-SP49-005` | Add an accessible no-edit prerequisite review with unchecked acknowledgments, stable retry, focus recovery, masked output, reflow, and no browser persistence. |
| `TH-SP49-006` | Prove GA/CA/FL confirmation, access isolation, replay/contention, upstream/roster drift denial, immutability, no verification/downstream implication, migration recovery, and full regression. |

## Entry gate

The applicant is unexpired and unchanged at version 26; the portal-disabled unmerged patient shell is unchanged; the request is exactly `Verification` version 9 with `TelehealthEligible`; exactly one current positive eligibility result, one current positive practice-network result, and one immutable Sprint 48 candidate selection are bound; the configured facility and state-specific staff record are active and unchanged; no participation context or downstream consequence exists.

## Mutation

After four acknowledgments, the server binds the current candidate selection to the fixed state-specific prerequisite matrix and opaque snapshot, appends one context confirmation, advances only the request to `Verification` version 10, and appends one event. Upstream evidence, applicant, patient, and staff records remain unchanged.

## Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact GA/CA/FL mappings, effective dates, context-only purpose, masked provider/billing output, snapshot, and acknowledgment validation. |
| Data | Locks, database-clock freshness, one context/event, replay/contention, completed-result provenance revalidation, append-only evidence, and zero upstream/downstream mutation. |
| HTTP | Applicant ownership, private/no-store, idempotency, safe errors, configured host/facility, and prohibited-input absence. |
| UI | No provider/reference selector or editable context, four unchecked acknowledgments, masked output, stable retry, focus/reflow/keyboard evidence, and no persistence. |
| Regression | Backend, frontend, browser, migrations/recovery, runtime, authorization, OpenAPI, queue, planning, Graphify, bootstrap, and cleanup. |

## Gate preserved

Sprint 50 must separately authorize and implement the exact effective-dated synthetic billing-entity and rendering-provider participation evaluation. Real state-authority verification, real credentialing, assignment, operational availability, canonical coverage, and all care consequences remain later gates.
