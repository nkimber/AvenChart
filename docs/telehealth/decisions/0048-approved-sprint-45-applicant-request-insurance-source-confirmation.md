# Decision 0048: Sprint 45 applicant request insurance-source confirmation

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired prospective applicant whose source-bound request is exactly pending `Verification` version 5 after the Sprint 44 intake snapshot to review one minimized, masked insurance-source projection and confirm that the already protected synthetic member-insurance source remains the intended primary source for this request.

The transaction appends one request-bound insurance-source confirmation, advances only the request from `Verification` version 5 to `Verification` version 6, and appends one request event. It records an explicit request for a separately authorized future fresh verification step, but it does not decrypt or duplicate the protected payload, create canonical coverage, select generic coverage, contact a payer or directory, reuse an earlier result as current, establish eligibility or exact network participation, create a financial route or operational-review item, accept the request, contact the patient, or enter any queue.

## 2. Data-minimization and meaning boundary

The applicant is shown only payer and product display names, masked member and group identifiers, subscriber relationship, coverage priority, the prior synthetic eligibility and practice-level network result labels with their timestamps/expiration, and explicit limitations. The earlier result labels are historical provenance only: they are not inherited as current request evidence and cannot establish rendering-physician participation.

The new receipt references the existing protected member-details payload, insurance handoff, eligibility result, practice-network determination, patient promotion, request creation, and Sprint 44 intake receipt without copying raw member or group identifiers. This is consistent with purpose-limited collection and access concepts in the [HHS Minimum Necessary Requirement guidance](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/minimum-necessary-requirement/index.html), but that guidance does not constitute legal approval and is not used to weaken the synthetic-only boundary.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and configured practice/facility isolated.
2. The server rebinds the unchanged `SyntheticRequestCreated` version 26 applicant, portal-disabled unmerged patient shell, request creation, current location/callback, universal and complaint assessments, intake snapshot/receipt, promotion/review chain, member-insurance details, pre-request eligibility result, pre-request practice-network determination, post-promotion insurance handoff, and all required earlier receipts.
3. The request must be exactly pending `Verification` version 5 with `TelehealthEligible`, one Sprint 44 intake receipt, no insurance-source confirmation, no canonical insurance record for the promoted shell, no coverage selection or verification, and no operational, queue, appointment, encounter, consent, care, financial, integration, or external consequence.
4. The projection fingerprint binds the request and version, applicant and patient shell, insurance handoff and protected-source references, payer/product masks, subscriber relationship, coverage priority, historical result identifiers/outcomes/timestamps, Sprint 44 intake receipt and context expiry, and the earliest governing database-clock expiry.
5. The command accepts only expected request version, opaque server snapshot, and seven explicit confirmations: payer/product, masked member/group details, subscriber relationship, primary coverage source, request for future fresh verification, evidence limitations, and synthetic data.
6. The command accepts no patient identifier, raw member or group identifier, protected payload, payer or plan override, subscriber edit, self-pay choice, eligibility/network result, rendering physician, price, financial route, operational decision, consent claim, contact instruction, or queue instruction.
7. All confirmations are explicit `true` values with no defaults. Missing, false, malformed, stale, foreign, expired, conflicting, or changed-source submissions fail closed.
8. The prior synthetic eligibility and practice-level network evidence is displayed and recorded only as historical source provenance. `fresh_verification_requested` is true while `prior_result_reused`, `coverage_verified`, `exact_network_confirmed`, and every financial/operational/care consequence remain false.
9. The transaction is request-version checked, snapshot-bound, semantically idempotent, first-writer safe, database-clock constrained, private/no-store, and applicant-correlated.
10. Exactly one protected request insurance-source receipt, one same-status request version advance, and one request event are created. No raw protected source is copied and earlier evidence remains immutable.
11. Exact replay returns the original projection. Changed-content reuse, another command after success, stale version, expired or foreign access, source/patient/intake/insurance drift, and concurrent duplicate writers fail closed.
12. The UI has no editable insurance text, no default confirmations, a clear correction/restart route, explicit historical-result and no-guarantee wording, stable retry, focus recovery, 320-pixel reflow, keyboard operation, and no insurance-source or result persistence in browser storage.
13. No canonical insurance record, generic coverage selection, eligibility/network verification, rendering-physician check, estimate, financial acknowledgment, operational-review item, clinical-review item, contact, doctor search, patient or clinician queue, queue position, appointment, encounter, consent, media, care, prescription, claim, integration, or external communication is created.
14. Migration, policy, replay/contention, access isolation, expiry/stale/source/intake/insurance drift denial, protected-source non-duplication, immutable evidence, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–44.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION`, version 1. |
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/insurance-source`. |
| Entry state | Exact unexpired `SyntheticRequestCreated` applicant and source-linked request at pending `Verification` version 5 with one Sprint 44 intake receipt. |
| Input | Expected request version, opaque insurance-source snapshot, and seven explicit confirmations. |
| Mutation | One immutable request insurance-source confirmation, one `Verification` version 5-to-6 advance, and one request event. |
| Result | The masked primary source is bound and fresh verification is requested; prior result reuse and every current coverage/network claim remain false. |
| Outstanding gates | Protected-source evaluation, current eligibility/benefits, exact rendering-physician network, canonical coverage, price/financial route, legal/clinician consent, operational authorization, queueing, appointment, encounter, and care remain unavailable. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; insurance edits or raw identifier return; decryption or payload duplication; canonical insurance/coverage mutation; a current eligibility, benefits, exact network, rendering-physician, price, or financial result; X12 or FHIR serialization; a payer, clearinghouse, directory, or other external call; self-pay; legal or clinician consent; staff operational action; patient contact; practice acceptance; queue insertion or position; doctor assignment; appointment; encounter; media; care; prescribing; pharmacy transmission; billing/claim; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can view or confirm the source; if raw or unmasked identifiers, protected payload, another applicant, or staff-only evidence is returned; if the client can edit insurance or submit an eligibility/network/financial result; if historical evidence is presented or persisted as current; if omitted/false confirmations can pass; if stale or changed evidence can be bound; if more than one receipt can be created; if the applicant, patient, canonical insurance, or earlier evidence changes; or if any downstream consequence appears. Rollback removes the route/UI and forward-disables the insurance-source path without rewriting immutable evidence.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, applicant-owned request insurance-source confirmation slice above.

## References

- [Actors, permissions, and journeys](../02-actors-and-journeys.md)
- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0047](0047-approved-sprint-44-applicant-request-intake-snapshot-confirmation.md)
- [Sprint 45 plan](../backlog/sprint-45-applicant-request-insurance-source-confirmation.md)
