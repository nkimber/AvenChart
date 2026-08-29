# Decision 0045: Sprint 42 applicant request universal safety assessment

Status: Approved — active for the exact disabled synthetic slice below
Approved date: 2026-08-28
Decision owner: AvenChart program owner
Implementation owner: Codex delivery agent under AvenChart program-owner direction
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired prospective applicant whose source-bound request is exactly `LocationConfirmed` version 2 to answer a fresh, four-answer universal safety screen. The server evaluates those answers with the existing immutable `synthetic-universal-safety` version 1 deterministic fixture and appends one reproducible request-owned assessment.

`TelehealthEligible` means only that this universal screen found no stop condition: the request advances to `SafetyScreening` version 3 and still requires separately authorized complaint-specific triage. `Emergency` advances to `EmergencyRedirected`; `UrgentInPerson` or `InPersonRequired` advances to `InPersonRecommended`; and `ClinicalReview` advances to `ClinicalReview`. None of these outcomes creates a clinical-review work item, contact, doctor search, care queue, appointment, encounter, consent, care authority, prescription, financial action, integration, or external communication.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and configured practice/facility isolated.
2. The server rebinds the unchanged `SyntheticRequestCreated` version 26 applicant, portal-disabled unmerged patient shell, request-creation receipt, request-location receipt and location row, prior passing prospective universal safety fixture, controlled visit purpose, request, state, masked callback route, and zero-downstream state.
3. The command accepts only expected request version, an opaque server snapshot, exact supported state, four explicit nullable safety answers, and confirmations that the current location, callback route, and synthetic-data boundary remain correct. It accepts no patient identifier, callback number, complaint text, diagnosis, priority, narrative, note, override, outcome, or care instruction.
4. Missing answers fail closed. Evaluation priority is exact and deterministic: emergency, severe/worsening, hands-on examination, uncertainty, then universal-screen pass. Client logic never chooses or confers an outcome.
5. The `synthetic-universal-safety` fixture is NON_PRODUCTION test content, not a medically approved or published clinical protocol. Its fixed identifier, version, content hash, answer fingerprint, individual answers, ordered outcome, and source snapshot are retained so the result is reproducible.
6. The request-owned location context must remain supported, source-matched, callback-matched, and fresh within the bounded applicant session. Changed or stale evidence fails closed and cannot silently refresh clinical authority.
7. The transaction is request-version checked, snapshot-bound, semantically idempotent, first-writer safe, database-clock constrained, private/no-store, and applicant-correlated without claiming a staff-session PHI-audit event.
8. The transaction inserts or validates the exact synthetic protocol fixture, appends one generic assessment and one protected applicant assessment receipt, advances only the request from version 2 to version 3, and appends one request event. The applicant, patient shell, source receipts, location evidence, and prior prospective assessment remain unchanged.
9. Exact replay returns the original result. Changed-content reuse, another command after success, stale version, expired or foreign access, changed state or callback, source drift, fixture drift, and concurrent duplicate writers fail closed.
10. The UI provides emergency action before submission, explicit yes/no controls with no clinical default, stable retry, focus recovery, outcome-specific direction, keyboard/screen-reader behavior, 320-pixel reflow, and no answer or result persistence in browser storage.
11. A universal-screen pass is not clinical eligibility, complaint-specific triage, practice acceptance, or care authorization. `ClinicalReview` is a required route only; no reviewer assignment or review action exists in this slice.
12. Migration, state-machine, policy, replay/contention, access isolation, expiry/stale/state-change/source-drift/fixture-drift denial, immutable evidence, outcome priority, request correlation, zero-downstream state, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–41.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT`, version 1. |
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/safety`. |
| Entry state | Exact unexpired `SyntheticRequestCreated` applicant, immutable Sprint 41 receipt, and source-linked request at `LocationConfirmed` version 2. |
| Input | Expected request version, context snapshot, exact supported state, current-location/callback/synthetic confirmations, and four explicit safety answers. |
| Mutation | One immutable protocol fixture if absent, one immutable generic assessment, one immutable applicant assessment receipt, one request transition, and one request event. |
| Pass consequence | Request is `SafetyScreening` version 3; complaint-specific triage remains required and every downstream capability remains absent. |
| Protective consequence | Request is `EmergencyRedirected`, `InPersonRecommended`, or `ClinicalReview` version 3 with outcome-specific direction and no downstream work item or external action. |

## 4. Explicit exclusions

This decision does not authorize real people or PHI; medically published protocol content; diagnosis; complaint-specific questions or eligibility; generative or opaque clinical logic; clinical override; reviewer assignment or action; administrator clearance; patient contact; practice acceptance; queue insertion or position; doctor assignment; appointment; encounter; consent; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; emergency dispatch; external calls; or production enablement.

## 5. Stop conditions and rollback

Stop if foreign or expired access can evaluate a request; if the client can omit an answer, choose an outcome, or submit clinical narrative; if a universal pass is represented as clinical eligibility; if changed/stale location, callback, protocol, or source evidence can pass; if more than one assessment or receipt can be created; if the applicant, patient shell, source receipt, or prior evidence changes; if a clinical-review work item, contact, queue, appointment, encounter, consent, care, financial, integration, or external consequence appears; or if an earlier safeguard regresses. Rollback removes the route/UI and forward-disables the assessment path without rewriting immutable evidence.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, applicant-owned request universal safety assessment above.

## References

- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Clinical triage and safety](../05-clinical-triage-and-safety.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0044](0044-approved-sprint-41-applicant-request-location-confirmation.md)
- [Sprint 42 plan](../backlog/sprint-42-applicant-request-universal-safety-assessment.md)
