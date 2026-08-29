# Decision 0057: Sprint 54 applicant request clinician reservation

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the authenticated physician who exactly matches the current applicant request's Sprint 48 rendering-candidate selection and Sprint 50 synthetic participation evaluation to see and atomically reserve that request through the existing clinician queue and leased `reserve-next` transaction. The request must already have the exact Sprint 52 queue authorization, one unassigned scheduled appointment, and one `Ready` queue entry.

The transaction may create one existing-format active reservation, move the queue entry `Ready -> Reserved`, move the request `Queued -> Reserved` with one version increment, assign the scheduled appointment only to the reservation-owning physician, and append the existing request lifecycle event. This is a disabled synthetic operational assignment, not real credentialing, payer-directory verification, legal consent, an encounter, or care authorization.

## 2. Candidate and freshness rule

Applicant-originated requests are visible and reservable only when the authenticated physician's staff identifier equals the immutable `candidate_staff_id` carried through the rendering-candidate, participation, operational-review, and queue-authorization chain. The same practice, facility, patient shell, request, queue entry, appointment, and authorization must be rebound, and the authorization's composite participation result must still be current at database reservation time.

A different physician must not see that applicant request in their clinician queue and cannot reserve it. Established-patient queue behavior remains unchanged. An expired or drifted applicant authorization is skipped rather than assigned; it cannot fall back to an arbitrary physician.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, configured-practice/facility scoped, current-session authenticated, and physician-role restricted.
2. The physician must have current treatment-purpose access and one active same-facility telehealth shift before reservation.
3. Applicant queue visibility and selection rebind the exact Sprint 52 authorization policy/version/source/outcome, selected candidate, patient shell, request, practice, facility, and unexpired `result_valid_through`.
4. The candidate query must also lock one unassigned scheduled appointment for the same patient and facility before changing request or queue state.
5. Fair ordering remains database-owned by `ready_at` and queue identifier. `FOR UPDATE SKIP LOCKED`, active-reservation uniqueness, clinician uniqueness, leases, idempotency, and database time remain authoritative.
6. One transaction creates the reservation, changes queue/request state, assigns the appointment, and appends the request event. A partial assignment is forbidden.
7. Replay returns the original reservation. Changed key content, stale state, missing shift, active clinician reservation, foreign facility, unmatched candidate, expired participation, appointment drift, and concurrency lose or fail closed without a partial mutation.
8. The clinician response and UI may identify the request as applicant-originated and confirm that the exact synthetic candidate matched. They must not expose the applicant access key, protected intake/insurance payload, member identifiers, or a real-network guarantee.
9. The applicant-owned status route may add only `Reserved`/`PhysicianPreparing`, set synthetic physician assignment true, and keep physician identity undisclosed. It exposes no provider identifier, name, NPI, queue position, or wait promise.
10. No browser storage may retain queue, reservation, applicant, or provider evidence. Staff UI recovery remains keyboard and screen-reader operable and reflows at 320 pixels.
11. Reservation creates no connection grant, media session, encounter, consent, diagnosis, treatment, prescription, claim, message, integration, or external call. Every such capability remains separately gated.
12. Candidate isolation, freshness, appointment binding, replay, concurrency, lease recovery, applicant status minimization, authorization, OpenAPI, runtime, browser/accessibility, planning, full regression, and Graphify evidence are required without weakening Sprints 1–53.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Clinician queue | Existing GET `/api/telehealth/v1/clinician/queue`; an applicant item is returned only to its exact current synthetic candidate. |
| Reservation | Existing POST `/api/telehealth/v1/clinician/reservations/reserve-next`; one active shift and idempotency key required. |
| Applicant entry | Exact Sprint 52 authorization, one `Ready` queue entry, one unassigned scheduled appointment, and current candidate-bound participation result. |
| Atomic result | Reservation active; queue `Reserved`; request `Reserved` with one version increment; appointment provider equals reservation owner; one request event. |
| Applicant projection | Existing applicant status GET may show `Reserved` as `PhysicianPreparing`; assignment true, identity disclosure false, no position or wait estimate. |
| Recovery | Lease expiry may use the existing evidence-preserving return to `Queued`; later reservation must again satisfy the same candidate/freshness rule. |
| Outstanding gates | Connection/video, consultation, chart workspace for this applicant path, consent, encounter, care, prescribing, claims, integrations, completion, cancellation, and production. |

## 5. Explicit exclusions

This decision does not authorize a different or fallback physician; physician identity disclosure to the applicant; real state authority, credentialing, payer-directory verification, exact real network, canonical coverage, payment, or price; exact queue position or wait estimate; patient contact; connection grants, WebRTC, recording, transcription, or vendor media; encounter, consent, diagnosis, treatment, prescribing, billing, or claims; FHIR, X12, pharmacy, payer, directory, clearinghouse, or other external connectivity; completion or cancellation; real people or PHI; or production enablement.

## 6. Stop conditions and rollback

Stop if an unmatched physician can see or reserve an applicant request; if expired or drifted participation can assign an appointment; if reservation partially changes request, queue, appointment, event, or lease evidence; if concurrency creates multiple winners; if patient status exposes physician identity or implies real network/coverage/care; or if any connection, encounter, consent, care, financial, integration, external, or production consequence occurs. Rollback restores the prior applicant filter and `Reserved` projection boundary; durable generic reservation evidence is retained for governed recovery.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic, exact-candidate applicant reservation boundary above.

## References

- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Eligibility and network participation](../08-insurance-eligibility-network-and-pricing.md)
- [Practice configuration and queue operations](../07-practice-configuration-and-queue-operations.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0055](0055-approved-sprint-52-applicant-request-queue-authorization.md)
- [Decision 0056](0056-approved-sprint-53-applicant-request-queue-status.md)
- [Sprint 54 plan](../backlog/sprint-54-applicant-request-clinician-reservation.md)
