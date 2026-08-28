# Decision 0044: Sprint 41 applicant request location and callback confirmation

Status: Approved — active for the exact disabled synthetic slice below
Approved date: 2026-08-28
Decision owner: AvenChart program owner
Implementation owner: Codex delivery agent under AvenChart program-owner direction
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired prospective applicant whose Sprint 40 request remains at version 1 in `Draft` to reconfirm the exact supported current-location state and masked callback route already preserved in the applicant's immutable communication-readiness evidence. The request advances once to version 2 and `LocationConfirmed`.

This is a request-owned location/callback attestation only. It does not evaluate symptoms, create a triage result or clinical review, accept or contact the patient, start a doctor search, enter either care queue, assign a queue position, create an appointment, encounter, consent, media session, care authority, prescription, financial action, integration, or external communication.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and configured practice/facility isolated.
2. The server derives the applicant, canonical patient shell, request, request-creation receipt, communication-readiness receipt, callback last four digits, practice, facility, and all source identifiers. The command accepts no raw callback number, patient identifier, clinical answer, complaint, priority, note, or free text.
3. The transaction revalidates the unexpired `SyntheticRequestCreated` applicant, exact immutable Sprint 40 receipt, source-linked `Draft` request at version 1, portal-disabled unmerged patient shell, current-location and callback source receipt, supported Georgia/California/Florida state, and zero-downstream state.
4. The applicant must explicitly select the current state and it must exactly match the previously confirmed source state. A changed location fails closed with restart/review guidance because this slice does not invalidate and rebuild state-specific notice, network, eligibility, or readiness evidence.
5. Four independent confirmations are mandatory: the selected state is the current physical location; the masked callback route remains correct; a changed location requires restart/review rather than continuation; and urgent or worsening symptoms require immediate appropriate action rather than waiting.
6. The command is request-version checked, snapshot-bound, semantically idempotent, first-writer safe, database-clock constrained, private/no-store, and applicant-correlated in server request context without claiming a staff-session PHI-audit event.
7. The transaction inserts one append-only `telehealth_patient_locations` row, records one immutable applicant-location confirmation receipt, advances only the request from `Draft` version 1 to `LocationConfirmed` version 2, and appends one request event. The applicant, patient shell, creation receipt, and every earlier source receipt remain unchanged.
8. Exact replay returns the original result. Changed-content reuse, another idempotency key after success, stale request version, expired or foreign access, changed state, source drift, prior location evidence, and concurrent duplicate writers fail closed.
9. The applicant UI reloads the server-authoritative masked context, requires every confirmation, clearly explains that changed location cannot continue, provides stable retry and a durable result projection, supports keyboard/focus and 320-pixel reflow, and stores nothing new in the browser.
10. Migration, policy, replay/contention, access isolation, expiry/stale/state-change/source-drift denial, immutable evidence, request correlation, zero-downstream state, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–40.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION`, version 1. |
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/location`. |
| Entry state | Exact unexpired `SyntheticRequestCreated` applicant, Sprint 40 receipt, and source-linked request at `Draft` version 1. |
| Input | Expected request version, context snapshot fingerprint, selected supported state, and four true boundary confirmations. |
| Mutation | One immutable location row, one immutable applicant-location receipt, one request transition, and one request event. |
| Consequence | Request is `LocationConfirmed` version 2; every triage, clinical-review, contact, queue, appointment, encounter, care, financial, integration, and external capability remains absent. |

## 4. Explicit exclusions

This decision does not authorize real people or PHI; portal enablement or credentials; chart access; a location change or evidence invalidation workflow; raw callback editing; new symptom or clinical answers; triage evaluation; clinical eligibility; exact rendering-clinician network verification; priority; patient contact; practice acceptance; queue insertion or position; doctor assignment; appointment; encounter; consent; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 5. Stop conditions and rollback

Stop if foreign or expired access can confirm location; if the client can select an unapproved or source-mismatched state; if raw callback or clinical content is accepted; if more than one location or receipt can be created; if the applicant, patient shell, creation receipt, or prior evidence changes; if triage, contact, queue, appointment, encounter, consent, care, financial, integration, or external consequence appears; or if an earlier safeguard regresses. Rollback removes the route/UI and forward-disables the confirmation path without rewriting immutable evidence or deleting the request/location history.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, applicant-bound request location/callback confirmation above.

## References

- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Clinical triage and safety](../05-clinical-triage-and-safety.md)
- [State regulatory and clinical governance](../06-state-regulatory-and-clinical-governance.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0043](0043-approved-sprint-40-applicant-bound-request-creation.md)
- [Sprint 41 plan](../backlog/sprint-41-applicant-request-location-confirmation.md)
