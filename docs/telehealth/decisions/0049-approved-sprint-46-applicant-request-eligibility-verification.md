# Decision 0049: Sprint 46 applicant request eligibility verification

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired prospective applicant whose source-bound request is exactly pending `Verification` version 6 after the Sprint 45 insurance-source confirmation to run one fresh request-time eligibility inquiry against the bounded `NON_PRODUCTION` synthetic adapter.

The server decrypts the existing protected synthetic member payload only in server memory, validates it against the masked source receipt, and passes the minimum inquiry facts to the in-process adapter. The transaction appends one immutable current eligibility result, advances only the request from `Verification` version 6 to `Verification` version 7, and appends one request event. It does not return, log, persist again, or copy the protected payload or its raw member data; serialize X12; call a payer, clearinghouse, provider directory, pharmacy, or any external destination; create canonical coverage; select coverage; establish exact practice or rendering-physician network participation; calculate price or responsibility; create operational work; accept or contact the patient; or enter any care queue.

## 2. Eligibility meaning and standards boundary

The adapter separates transport, subscriber match, eligibility, benefit-information, and business outcomes. It targets the inquiry/response semantics of `ASC_X12N_270_271_005010X279A1`, but this slice creates no X12 payload and makes no conformance, certification, connectivity, or production-readiness claim. The only committed adapter mode is `NON_PRODUCTION`, backed by the fixed effective synthetic dataset.

An `EligibleBenefitsReported` result means only that the bounded fixture reports an active subscriber and benefit information at the check time. It is not a guarantee of coverage, payment, patient responsibility, practice participation, or rendering-physician participation. `CoverageInactive`, `SubscriberNotFound`, and `UnableToDetermine` remain distinct fail-closed outcomes. Every outcome leaves later network, coverage-selection, financial, operational, consent, queue, and care gates closed.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and configured practice/facility isolated.
2. The server rebinds the unchanged `SyntheticRequestCreated` version 26 applicant, portal-disabled unmerged patient shell, exact request, request location, universal and complaint assessments, intake snapshot, promotion/review chain, protected member-insurance details, historical eligibility/network evidence, post-promotion handoff, and Sprint 45 insurance-source receipt.
3. The request must be exactly pending `Verification` version 6 with `TelehealthEligible`; the protected source and request context must be unexpired; no prior request-time eligibility result, canonical insurance record, coverage selection or verification, operational, queue, appointment, encounter, consent, care, financial, integration, or external consequence may exist.
4. The projection is no-edit and contains only payer/product display names, masked member and optional group suffixes, subscriber relationship, coverage priority, request location state, purpose category, the opaque request-bound fingerprint, expiry, and explicit limitations.
5. The command accepts only expected request version, the opaque eligibility snapshot fingerprint, and two explicit true values: synthetic-data confirmation and acknowledgment that eligibility/benefit information is not a coverage, payment, network, or responsibility guarantee.
6. The command accepts no patient identifier, member or group identifier, protected payload, payer or plan override, subscriber edit, requested outcome, network result, rendering physician, price, financial route, operational instruction, consent claim, contact instruction, or queue instruction.
7. Missing, false, malformed, stale, foreign, expired, changed-source, or changed-context submissions fail closed before evidence is written or the adapter runs.
8. Protected payload unprotection occurs only inside the server command path. The raw payload and normalized inquiry are not returned or logged and are not copied into current eligibility evidence.
9. The adapter contract and outcome tuple are validated before persistence. Unknown, inconsistent, out-of-window, or unsupported adapter results fail closed.
10. The transaction is request-version checked, snapshot-bound, semantically idempotent, first-writer safe, database-clock constrained, private/no-store, and applicant-correlated.
11. Exactly one request-time eligibility result, one same-status request version advance, and one request event are created. Earlier evidence and the protected source remain immutable.
12. Exact replay returns the original result. Changed-content reuse, another command after success, stale version, expired or foreign access, provenance drift, and concurrent duplicate writers fail closed.
13. The UI has no editable insurance or outcome fields, no default acknowledgments, explicit no-guarantee and correction guidance, stable retry, focus recovery, 320-pixel reflow, keyboard operation, and no eligibility source or result persistence in browser storage.
14. No raw transaction, canonical coverage, generic coverage selection, network verification, rendering-physician check, estimate, financial acknowledgment, operational-review item, practice acceptance, patient contact, doctor search, patient or clinician queue, queue position, appointment, encounter, consent, media, care, prescription, claim, integration, or external communication is created.
15. Migration, policy/outcome-contract, replay/contention, access isolation, expiry/stale/provenance denial, payload protection and non-copy, immutable evidence, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–45.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION`, version 1. |
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/eligibility`. |
| Entry state | Exact unexpired `SyntheticRequestCreated` applicant and source-linked request at pending `Verification` version 6 with one Sprint 45 insurance-source receipt. |
| Input | Expected request version, opaque eligibility snapshot, synthetic-data confirmation, and no-guarantee acknowledgment. |
| Adapter | In-process `NON_PRODUCTION` fixture targeting `ASC_X12N_270_271_005010X279A1`; no X12 serialization or external call. |
| Mutation | One immutable request-time eligibility result, one `Verification` version 6-to-7 advance, and one request event. |
| Result | Separate normalized transport, match, eligibility, benefit-information, and business outcomes with a 15-minute evidence expiry. |
| Outstanding gates | Exact practice/rendering-physician network, canonical coverage and selection, price/financial route, legal/clinician consent, operational authorization, queueing, appointment, encounter, and care remain unavailable. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; insurance edits; raw identifier or protected-payload return, logging, or copying; X12 serialization; payer, clearinghouse, directory, pharmacy, or other external connectivity; a coverage, payment, price, patient-responsibility, practice-network, or rendering-physician guarantee; canonical insurance/coverage mutation; operational review; self-pay; legal or clinician consent; staff action; patient contact; practice acceptance; queue insertion or position; doctor assignment; appointment; encounter; media; care; prescribing; pharmacy transmission; billing/claim; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can view or run the inquiry; if raw or unmasked identifiers, the protected payload, another applicant, or staff-only evidence is returned or logged; if the client can select an outcome or submit payer/network/financial instructions; if the payload is copied into result evidence; if omitted/false acknowledgments can pass; if stale or changed evidence can be evaluated; if more than one result can be created; if the applicant, patient, source, or earlier evidence changes; if X12 or an external call occurs; or if any downstream consequence appears. Rollback removes the route/UI and forward-disables the request eligibility path without rewriting immutable evidence.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, applicant-owned request eligibility verification slice above.

## References

- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [API, events, and integration contracts](../15-api-events-and-integration-contracts.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0048](0048-approved-sprint-45-applicant-request-insurance-source-confirmation.md)
- [Sprint 46 plan](../backlog/sprint-46-applicant-request-eligibility-verification.md)
