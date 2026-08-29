# Decision 0055: Sprint 52 applicant request queue authorization

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit a current configured-practice administrator to review one applicant-originated request that is exactly `OperationalReview` version 12 after the immutable Sprint 51 submission and to authorize that request into the synthetic clinician queue.

The transaction appends one immutable staff authorization, creates one unassigned synthetic appointment and one ready queue entry, advances only the request to `Queued` version 13, and appends one request event. The authorization records practice acceptance for this bounded non-production queue exercise. It is not real insurance verification, a payment or price guarantee, assignment of a rendering physician, consent, an encounter, a care relationship, prescribing authority, a claim, or an external action.

## 2. Authorization meaning

The policy `SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION` version 1 rebinds the exact current applicant, portal-disabled patient shell, request, Sprint 51 operational-review submission, and all underlying request evidence through the current staff record used by the participation evaluation. It accepts no patient, clinical, insurance, provider, network, price, scheduling, priority, or disposition value from the client.

`practiceAccepted=true`, `patientCareQueueEntered=true`, `clinicianQueueEntered=true`, `doctorSearchStarted=true`, and `appointmentCreated=true` mean only that the configured synthetic practice accepted the bounded request for its internal clinician work queue and created an unassigned scheduling shell. `renderingPhysicianAssigned`, `coverageVerified`, `financialRouteCreated`, `queuePositionAssigned`, `encounterCreated`, `consentCreated`, and `careAuthorized` remain false. No payer, provider directory, licensing board, credentialing source, clinician, pharmacy, clearinghouse, or other external destination is contacted.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, authenticated-staff protected, and practice/facility isolated.
2. Only an administrator role with current configured-facility access may view or execute the decision; a front-desk actor must also be bound to a current active staff record.
3. The server rebinds the applicant, portal-disabled unmerged patient shell, exact request, Sprint 51 submission, all request evidence from Sprints 40–50, and the current candidate staff record under transaction locks.
4. The request must be exactly applicant-originated, `OperationalReview` version 12, and linked to exactly one current Sprint 51 submission. No prior queue authorization, queue entry, reservation, video, consultation, encounter, consent, care, claim, or other downstream consequence may exist.
5. The projection is no-edit and minimized. It returns no full NPI, staff identifier, TIN, canonical patient identifier, member identifier, source payload, internal provider/contract/authority reference, price, or clinical narrative.
6. The command accepts only expected request version, opaque authorization snapshot fingerprint, and four explicit true values: synthetic-evidence-reviewed acknowledgment, no-coverage-guarantee acknowledgment, practice-acceptance acknowledgment, and queue-is-not-care acknowledgment.
7. Missing, false, malformed, stale, foreign, changed-provenance, inactive-staff, unsupported-state, or generic-route submissions fail closed before evidence is written.
8. Exactly one authorization, one unassigned appointment, one ready queue entry, one request status/version advance, and one request event commit atomically. Exact replay returns the original result; changed-key reuse, a second command, and concurrent duplicate writers fail closed.
9. Evidence is append-only, database-clock constrained, snapshot-bound, private/no-store, and actor-correlated. The existing generic established-patient authorization route explicitly rejects applicant-originated requests.
10. The administrator UI distinguishes applicant-originated requests, loads the minimized decision packet, starts all acknowledgments unchecked, preserves a stable retry identity only for unchanged content, restores focus after errors and success, reflows at 320 pixels, and persists no authorization evidence.
11. No real authority, credentialing, rendering-clinician assignment, exact real network, canonical coverage, coverage selection/verification, estimate, financial route, patient contact, exact queue position, encounter, consent, care, prescription, claim, integration, or external call is created.
12. Migration, policy, role/access isolation, replay/contention, stale/provenance denial, minimization, append-only evidence, tenant isolation, generic-route denial, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–51.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Endpoint | GET and POST `/api/telehealth/v1/admin/applicant-requests/{requestId}/queue-authorization`. |
| Entry | Current configured administrator; applicant-originated request exactly `OperationalReview` version 12; one exact Sprint 51 submission and complete immutable provenance. |
| Input | Expected request version, opaque snapshot, and four acknowledgments; no evaluated, scheduling, priority, clinical, or operational value input. |
| Mutation | One immutable staff authorization, one unassigned synthetic appointment, one ready queue entry, request `OperationalReview` version 12 to `Queued` version 13, and one event. |
| Output | Minimized practice/payer/product/state/purpose/date/service/modality and masked candidate/billing references, authorization state, explicit honest consequence flags, and direction. |
| Queue effect | The request becomes visible in the configured practice/facility clinician queue and is eligible for later atomic clinician reservation. No clinician is assigned by this decision. |
| Outstanding gates | Real authority and credentialing; real rendering-provider participation; canonical coverage and financial route; patient queue-status access; consent; encounter; and care. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; real provider, license, credential, payer, contract, billing, or network data; FHIR or X12 serialization; payer, directory, licensing-board, credentialing, pharmacy, clearinghouse, or other connectivity; real state-practice-authority verification; real rendering-provider participation; exact real network; coverage/payment/price guarantees; canonical coverage; estimate or self-pay; patient contact; exact queue position or wait promise; clinician assignment; encounter; media; consent; care; prescribing; billing or claims; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if a non-administrator, inactive/foreign staff actor, or another practice/facility can view or authorize; if protected member/provider/internal identifiers are disclosed; if the client can supply an evaluated, scheduling, priority, clinical, or disposition field; if stale evidence authorizes the command; if multiple authorizations, appointments, or queue entries can exist; if the generic established-patient path can authorize an applicant-originated request; if a clinician is assigned; or if any care/external consequence appears. Rollback removes the route and UI and forward-disables the authorization path without rewriting immutable evidence; already-created synthetic queue records are handled by the existing controlled queue lifecycle.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic queue-authorization boundary above.

## References

- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Practice configuration and queue operations](../07-practice-configuration-and-queue-operations.md)
- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [Decision 0054](0054-approved-sprint-51-applicant-request-operational-review-submission.md)
- [HL7 Da Vinci PDex Plan-Net implementation guidance 1.2](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/implementation.html)
- [Medical Board of California telehealth guidance](https://www.mbc.ca.gov/Resources/Medical-Resources/telehealth.aspx)
- [Georgia Composite Medical Board Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3)
- [Florida Statutes section 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499/0456/Sections/0456.47.html)
- [Sprint 52 plan](../backlog/sprint-52-applicant-request-queue-authorization.md)
