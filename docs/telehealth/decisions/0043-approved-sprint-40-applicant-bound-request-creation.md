# Decision 0043: Sprint 40 applicant-bound request creation

Status: Approved — active for the exact disabled synthetic slice below
Approved date: 2026-08-28
Decision owner: AvenChart program owner
Implementation owner: Codex delivery agent under AvenChart program-owner direction
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired prospective applicant whose Sprint 39 practice review was positively authorized to confirm the current boundary and create exactly one synthetic telehealth request in `Draft`.

The request is an applicant-linked workflow shell. It is not a patient or clinician care-queue entry, doctor search, queue position, appointment, encounter, consent, clinical eligibility decision, coverage guarantee, care authorization, prescription, financial action, integration, or external communication.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, access-key protected, and configured practice/facility isolated.
2. The server derives practice, facility, applicant, promotion, canonical patient shell, practice-review case, authorization, purpose, and complaint category. The command accepts no patient identifier, source identifier, complaint text, priority, notes, or clinical values.
3. The transaction revalidates the exact unexpired applicant and full immutable provenance through controlled purpose, passing safety outcome, readiness, submitted case, and positive Sprint 39 authorization. The canonical patient must remain portal-disabled, unmerged, and unchanged, and all prohibited downstream tables must remain empty.
4. Three independent applicant confirmations are mandatory: create this synthetic request now; no queue, doctor search, appointment, encounter, consent, or care is created; and urgent or worsening symptoms require immediate appropriate action rather than waiting.
5. The command is applicant scoped, expected-version checked, semantically idempotent, first-writer safe, database-clock constrained, private/no-store, and applicant-correlated in server request context without claiming a staff-session PHI-audit event.
6. The transaction advances the applicant from `SyntheticPracticeReviewAuthorized` to `SyntheticRequestCreated`, increments its version once, inserts one `Draft` telehealth request linked to immutable source provenance, appends one request event and one applicant event, and records one immutable request-creation receipt.
7. Exact replay returns the original result. Changed-content replay, another idempotency key after success, stale version, expired or foreign access, source drift, missing authorization, prior request, and concurrent duplicate writers fail closed.
8. Every receipt records `telehealthRequestCreated=true` while `patientContacted=false`, `patientCareQueueEntered=false`, `clinicianQueueEntered=false`, `doctorSearchStarted=false`, `queuePositionAssigned=false`, `appointmentCreated=false`, `encounterCreated=false`, `consentCreated=false`, `careAuthorized=false`, `prescribingEnabled=false`, `billingEnabled=false`, `claimCreated=false`, `integrationEnabled=false`, and `externalCallPerformed=false`.
9. The applicant UI reloads the server-authoritative authorized state, keeps limitations adjacent, requires every confirmation, provides stable retry and a durable success projection, supports keyboard/focus and 320-pixel reflow, and stores nothing new in the browser.
10. Migration, policy, request creation, replay/contention, access isolation, expiry/drift denial, immutable evidence, request correlation, zero-downstream state, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–39.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION`, version 1. |
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request`. |
| Entry state | Exact unexpired `SyntheticPracticeReviewAuthorized` applicant and matching positive authorization with unchanged source provenance. |
| Input | Expected applicant version, authorization policy version 1, and three true boundary confirmations. |
| Mutation | One applicant transition, one `Draft` request, one immutable creation receipt, and one event on each aggregate. |
| Consequence | A request shell exists; every queue, appointment, encounter, care, prescribing, financial, integration, and external capability remains absent. |

## 4. Explicit exclusions

This decision does not authorize real people or PHI; portal enablement or credentials; chart access; edits to prior identity, contact, insurance, communication, device, or clinical receipts; new clinical answers or free text; clinical eligibility; exact rendering-clinician network verification; priority; patient contact; queue insertion or position; doctor assignment; appointment; encounter; consent; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 5. Stop conditions and rollback

Stop if foreign or expired access can create a request; if client-selected identity, provenance, complaint, priority, or clinical values are accepted; if more than one request or receipt can be created; if prior evidence or the patient shell changes; if a queue, contact, appointment, encounter, consent, care, financial, integration, or external consequence appears; or if an earlier safeguard regresses. Rollback removes the route/UI and forward-disables the creation path without rewriting immutable evidence or deleting the synthetic request shell.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, applicant-bound Draft request creation above.

## References

- [Actors and journeys specification](../02-actors-and-journeys.md)
- [Request workflows specification](../03-workflows-and-state-machines.md)
- [Onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Security, privacy, consent, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Decision 0042](0042-approved-sprint-39-synthetic-practice-review-authorization.md)
- [Sprint 40 plan](../backlog/sprint-40-applicant-bound-request-creation.md)
