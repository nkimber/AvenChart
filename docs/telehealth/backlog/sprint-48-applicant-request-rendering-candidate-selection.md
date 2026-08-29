# Sprint 48: applicant request rendering-candidate selection

Status: Approved for bounded implementation by [TH-DEC-0051](../decisions/0051-approved-sprint-48-applicant-request-rendering-candidate-selection.md)

Scope: Bind one server-owned GA/CA/FL synthetic clinician as the candidate for a future exact network evaluation after current positive eligibility and practice-network evidence; advance only the request from pending `Verification` version 8 to version 9; do not assign a clinician or establish licensure, credentialing, availability, exact network, coverage, financial, operational, queue, care, integration, or production consequence.

## Outcome

Give the future exact participation gate a fixed rendering subject while accurately preserving the distinction between candidate selection and clinician assignment or network confirmation.

## Stories

| ID | Story |
|---|---|
| `TH-SP48-001` | Add V0323 with one-request uniqueness, exact upstream references, roster/freshness constraints, candidate-only flags, append-only enforcement, and zero-downstream constraints. |
| `TH-SP48-002` | Add a deterministic state-specific policy and snapshot with fixed GA/CA/FL synthetic candidates, masked provider output, and four mandatory acknowledgments. |
| `TH-SP48-003` | Add an access-key-bound locked transaction with full provenance revalidation, exact replay, stale/foreign/drift denial, and first-writer safety. |
| `TH-SP48-004` | Add private/no-store applicant GET/POST endpoints with stable Problem Details, correlation, and semantic idempotency. |
| `TH-SP48-005` | Add an accessible no-edit candidate review with unchecked acknowledgments, stable retry, focus recovery, masked output, reflow, and no browser persistence. |
| `TH-SP48-006` | Prove GA/CA/FL selection, access isolation, replay/contention, upstream drift denial, immutability, no assignment/network/downstream implication, migration recovery, and full regression. |

## Entry gate

The applicant is unexpired and unchanged at version 26; the portal-disabled unmerged patient shell is unchanged; the request is exactly `Verification` version 8 with `TelehealthEligible`; exactly one current active Sprint 46 eligibility result and one current positive Sprint 47 practice-network result are bound; the configured facility and state-specific candidate are active; no selection or downstream consequence exists.

## Mutation

After four acknowledgments, the server resolves the fixed candidate from the current location state, binds the roster and upstream evidence into an opaque snapshot, appends one selection, advances only the request to `Verification` version 9, and appends one event. Upstream evidence, applicant, patient, and staff records remain unchanged.

## Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact GA/CA/FL mapping, effective dates, candidate-only purpose, mask, snapshot, and acknowledgment validation. |
| Data | Locks, database-clock freshness, one selection/event, replay/contention, append-only evidence, and zero upstream/downstream mutation. |
| HTTP | Applicant ownership, private/no-store, idempotency, safe errors, configured host/facility, and prohibited-input absence. |
| UI | No provider selector or editable context, four unchecked acknowledgments, masked provider output, stable retry, focus/reflow/keyboard evidence, and no persistence. |
| Regression | Backend, frontend, browser, migrations/recovery, runtime, authorization, OpenAPI, queue, planning, Graphify, bootstrap, and cleanup. |

## Gate preserved

Sprint 49 must separately model the effective-dated billing entity, rendering clinician state authority, payer/product contract, service location, modality, and date-of-service matrix before any exact participation result can be authorized. Assignment and operational availability remain later gates.
