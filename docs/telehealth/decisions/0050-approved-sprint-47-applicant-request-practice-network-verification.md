# Decision 0050: Sprint 47 applicant request practice-network verification

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired prospective applicant whose source-bound request is exactly pending `Verification` version 7 after a fresh positive Sprint 46 eligibility result to run one fresh practice/facility/service network inquiry against the bounded `NON_PRODUCTION` synthetic adapter.

The server binds the exact current eligibility evidence, configured practice, facility, synthetic plan, patient state, intended professional telehealth service, location state, and date of service. The transaction appends one immutable practice-network result, advances only the request from `Verification` version 7 to `Verification` version 8, and appends one request event. It does not select or check a rendering physician; establish exact network participation; create or select canonical coverage; calculate price or patient responsibility; create operational work; accept or contact the patient; start doctor search; enter a patient or clinician queue; or contact a payer, directory, clearinghouse, or other external destination.

## 2. Network meaning and standards boundary

This decision intentionally distinguishes practice-level network evidence from exact network participation. The adapter models only the configured practice, facility, service category, state, date, and synthetic plan. It targets provider-directory concepts from `HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0`, but creates no FHIR resource, makes no external call, and makes no conformance, certification, directory-authority, or production-readiness claim.

`PracticeInNetworkAcceptingNewPatients` means only that the fixed fixture reports the configured practice/service as in network and accepting new patients for that synthetic plan context. `PracticeOutOfNetwork` and `UnableToDetermine` remain distinct fail-closed outcomes. No outcome is exact network confirmation because no rendering physician, billing contract, or payer-authoritative member/product verification source is checked. Every outcome leaves coverage, financial, operational, consent, queue, appointment, encounter, and care gates closed.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and configured practice/facility isolated.
2. The server rebinds the unchanged `SyntheticRequestCreated` version 26 applicant, portal-disabled unmerged patient shell, exact request, full request/intake/insurance provenance, and exactly one fresh positive Sprint 46 request eligibility result.
3. The request must be exactly pending `Verification` version 7 with `TelehealthEligible`; eligibility must be current, `EligibleBenefitsReported`, matched, active, and benefits-reported; no prior request practice-network result, canonical insurance, coverage selection or verification, operational, queue, appointment, encounter, consent, care, financial, integration, or external consequence may exist.
4. The projection contains only practice/payer/product display data, location, purpose, current eligibility outcome/times, opaque request-bound fingerprint, expiry, and explicit limitations. It contains no member/group value or mask, patient identifier, provider-directory reference, billing identifier, physician identifier, raw payload, or trace token.
5. The command accepts only expected request version, opaque network snapshot fingerprint, and three explicit true values: synthetic-data confirmation, practice-only-scope acknowledgment, and no-guarantee acknowledgment.
6. The command accepts no member, group, patient, payer/product override, requested outcome, provider/directory reference, rendering physician, NPI, TIN, price, financial route, operational instruction, consent claim, contact instruction, or queue instruction.
7. Missing, false, malformed, stale, foreign, expired, non-positive-eligibility, changed-evidence, or changed-context submissions fail closed before evidence is written or the adapter runs.
8. The adapter receives only server-bound practice, facility, plan key, state, date, service category, and database check time. It receives no member or subscriber data.
9. Adapter compatibility metadata and the complete normalized outcome tuple are validated before persistence. Unknown, inconsistent, out-of-window, or unsupported results fail closed.
10. Composite evidence expires at the earlier of the eligibility evidence, network evidence, or applicant session. An expired eligibility result cannot authorize a network check.
11. The transaction is request-version checked, snapshot-bound, semantically idempotent, first-writer safe, database-clock constrained, private/no-store, and applicant-correlated.
12. Exactly one request practice-network result, one same-status request version advance, and one request event are created. Eligibility and all earlier evidence remain immutable.
13. Exact replay returns the original result. Changed-content reuse, another command after success, stale version, expired or foreign access, provenance drift, and concurrent duplicate writers fail closed.
14. The UI has no editable context or outcome fields, no default acknowledgments, explicit practice-only/no-guarantee guidance, stable retry, focus recovery, 320-pixel reflow, keyboard operation, and no evidence persistence in browser storage.
15. No rendering physician selection or check, exact network confirmation, canonical coverage, coverage selection/verification, estimate, financial acknowledgment, operational-review item, practice acceptance, patient contact, doctor search, patient or clinician queue, queue position, appointment, encounter, consent, media, care, prescription, claim, integration, or external communication is created.
16. Migration, policy/outcome-contract, replay/contention, access isolation, expiry/stale/provenance denial, minimization, immutable evidence, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–46.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION`, version 1. |
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/practice-network`. |
| Entry state | Exact unexpired `SyntheticRequestCreated` applicant and source-linked request at pending `Verification` version 7 with one current positive Sprint 46 eligibility result. |
| Input | Expected request version, opaque network snapshot, synthetic-data confirmation, practice-only acknowledgment, and no-guarantee acknowledgment. |
| Adapter | In-process `NON_PRODUCTION` fixture targeting `HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0`; no FHIR serialization or external call. |
| Mutation | One immutable request-time practice-network result, one `Verification` version 7-to-8 advance, and one request event. |
| Result | Separate normalized transport, plan match, practice affiliation, service availability, new-patient acceptance, and business outcomes with bounded composite freshness. |
| Outstanding gates | Rendering-physician participation and exact network, canonical coverage/selection, price/financial route, legal/clinician consent, operational authorization, queueing, appointment, encounter, and care remain unavailable. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; member/subscriber disclosure; FHIR resource serialization; payer, provider-directory, clearinghouse, pharmacy, or other external connectivity; rendering-physician selection or participation; exact network confirmation; a coverage, payment, price, patient-responsibility, capacity, or appointment guarantee; canonical insurance/coverage mutation; operational review; self-pay; legal or clinician consent; staff action; patient contact; practice acceptance; queue insertion or position; doctor search or assignment; appointment; encounter; media; care; prescribing; pharmacy transmission; billing/claim; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can view or run the check; if member, subscriber, patient, billing, physician, directory-reference, or trace data is returned; if the client can select an outcome or submit provider/network/financial instructions; if stale or non-positive eligibility can authorize the command; if more than one result can be created; if eligibility, applicant, patient, request source, or earlier evidence changes; if FHIR is serialized or an external call occurs; if practice-level evidence is represented as exact network confirmation; or if any downstream consequence appears. Rollback removes the route/UI and forward-disables the practice-network path without rewriting immutable evidence.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, applicant-owned request practice-network verification slice above.

## References

- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [API, events, and integration contracts](../15-api-events-and-integration-contracts.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0049](0049-approved-sprint-46-applicant-request-eligibility-verification.md)
- [Sprint 47 plan](../backlog/sprint-47-applicant-request-practice-network-verification.md)
