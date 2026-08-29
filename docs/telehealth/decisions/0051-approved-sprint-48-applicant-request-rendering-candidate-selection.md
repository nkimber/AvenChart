# Decision 0051: Sprint 48 applicant request rendering-candidate selection

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired `SyntheticRequestCreated` version 26 applicant whose source-bound request is exactly pending `Verification` version 8 after current positive request eligibility and practice-network evidence to select one server-owned, state-specific synthetic clinician as the candidate for a future exact network evaluation.

The transaction appends one immutable candidate selection, advances only the request from `Verification` version 8 to `Verification` version 9, and appends one request event. Candidate selection is not clinician assignment, availability, licensure, credentialing, exact network participation, appointment, consent, or care.

## 2. Candidate and network meaning

The policy `SYNTHETIC_APPLICANT_REQUEST_RENDERING_CANDIDATE_SELECTION` version 1 resolves exactly one fixed synthetic staff record for Georgia, California, or Florida. The roster is effective from 2026-08-29 through 2026-10-31 and binds facility 10, the candidate staff record and NPI, a synthetic practitioner reference, a synthetic state-authority reference, `ProfessionalTelehealthConsultation`, and `RealTimeAudioVideo`. Only the display name and masked provider reference are returned to the applicant; the staff identifier and full NPI remain server-side.

`candidateSelectedForNetworkEvaluation=true` means only that a later separately authorized participation check now has a fixed rendering subject. `renderingPhysicianAssigned`, `renderingPhysicianNetworkChecked`, and `exactNetworkConfirmed` remain false. A production-capable future check must use effective-dated payer/product, billing legal entity/TIN/NPI, rendering NPI and state authority, service location/state, service, modality, and date-of-service participation evidence.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and practice/facility isolated.
2. The server rebinds the unchanged applicant, portal-disabled unmerged patient shell, exact request, exact current positive eligibility result, and exact current positive practice-network result under transaction locks.
3. The request must be exactly pending `Verification` version 8; no prior candidate selection or downstream consequence may exist.
4. The projection is no-edit and minimized. It returns no full NPI, staff identifier, TIN, canonical patient identifier, member data, contract, price, queue, or assignment data.
5. The command accepts only expected request version, opaque candidate snapshot fingerprint, and four explicit true values: synthetic-data confirmation, candidate-only-scope acknowledgment, no-assignment acknowledgment, and network-check-still-required acknowledgment.
6. The server owns candidate selection. The client cannot choose a staff member, NPI, authority, practice, payer, product, outcome, network status, financial route, queue instruction, or care action.
7. Missing, false, malformed, stale, foreign, expired, changed-provenance, or unsupported-state submissions fail closed before evidence is written.
8. Exactly one selection, one request-only same-status version advance, and one request event are committed atomically. Exact replay returns the original result; changed-key reuse, a second command, and concurrent duplicate writers fail closed.
9. Evidence is append-only, database-clock constrained, snapshot-bound, private/no-store, and applicant-correlated.
10. The UI starts all four acknowledgments unchecked, preserves a stable retry identity only for unchanged content, restores focus after errors and success, reflows at 320 pixels, and persists no selection evidence.
11. No clinician assignment, network check, exact network, canonical coverage, coverage selection/verification, financial route, operational work, acceptance, contact, doctor search, queue, position, appointment, encounter, consent, care, prescription, claim, integration, or external call is created.
12. Migration, policy, access isolation, replay/contention, expiry/stale/provenance denial, minimization, append-only evidence, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–47.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/rendering-candidate`. |
| Entry | Exact current positive Sprint 46 eligibility and Sprint 47 practice-network evidence; request pending `Verification` version 8. |
| Input | Expected version, opaque snapshot, and four acknowledgments; no provider choice or network outcome input. |
| Candidate | Server-owned GA/CA/FL synthetic roster entry for network evaluation only. |
| Mutation | One immutable selection, request `Verification` version 8 to version 9, and one event. |
| Output | Candidate display name, masked provider reference, synthetic references, service/modality, prior evidence identifiers/times, and explicit false downstream flags. |
| Outstanding gates | Exact rendering and billing-entity participation, assignment/availability, coverage, financial, operational, consent, queue, appointment, encounter, and care. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; provider choice by the client; real licensure or credentialing claims; payer, directory, credentialing, pharmacy, clearinghouse, or other connectivity; rendering-physician participation; billing-entity participation; exact network confirmation; coverage/payment/price guarantees; canonical coverage; operational review; self-pay; consent; staff action; contact; practice acceptance; queueing; doctor assignment; appointment; encounter; media; care; prescribing; billing or claims; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can view or select the candidate; if a full NPI, staff identifier, member value, billing identifier, or contract value is disclosed; if the client can choose a provider or outcome; if stale or non-positive upstream evidence authorizes the command; if multiple selections can exist; if the selection is represented as assignment or exact network; or if any downstream consequence appears. Rollback removes the route and UI and forward-disables the candidate path without rewriting immutable evidence.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic candidate-selection boundary above.

## References

- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [Decision 0050](0050-approved-sprint-47-applicant-request-practice-network-verification.md)
- [Sprint 48 plan](../backlog/sprint-48-applicant-request-rendering-candidate-selection.md)
