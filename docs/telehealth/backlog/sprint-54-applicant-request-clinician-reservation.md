# Sprint 54 plan: applicant request clinician reservation

Status: Implemented and automated verification passed under [TH-DEC-0057](../decisions/0057-approved-sprint-54-applicant-request-clinician-reservation.md); independent approvals remain open

## Goal

Let only the exact synthetic rendering candidate whose participation evidence was carried through the applicant request chain see and atomically reserve that queued request. Reuse the proven shift, fair queue, database lease, idempotency, and concurrency machinery; expose only a physician-preparing state to the applicant and create no consultation or care consequence.

## Delivery boundary

- Bind applicant-originated clinician queue visibility to the authenticated physician's exact candidate identifier and current Sprint 52 authorization evidence.
- Bind `reserve-next` selection to that same candidate, database-time freshness, one ready queue entry, and one unassigned same-patient/facility appointment.
- Preserve established-patient queue behavior, fair `ready_at` ordering, `FOR UPDATE SKIP LOCKED`, one active reservation per request/clinician, lease expiry, and command replay.
- Atomically create one generic reservation, reserve the queue/request, assign the appointment to the reservation owner, and append the generic lifecycle event.
- Extend the minimized applicant-owned status to `Reserved`/`PhysicianPreparing`, disclose no physician identity, exact position, wait promise, protected source, or care authority.
- Label applicant-originated work and exact synthetic candidate matching in the clinician UI without claiming real credentialing or network confirmation.
- Add policy/unit, transport, browser/accessibility, authorization, OpenAPI, runtime, live GA/CA/FL candidate-isolation, concurrency, regression, planning, and Graphify evidence.

## State and evidence contract

```text
SyntheticRequestCreated v26 (unchanged)
  + request Queued v13+
  + exact current Sprint 52 authorization(candidate_staff_id = physician)
  + one Ready queue entry
  + one unassigned scheduled appointment
  + active same-facility physician shift
  -> one active leased reservation
  -> queue Reserved
  -> request Reserved v+1
  -> appointment provider = reservation-owning physician
  -> applicant sees PhysicianPreparing with no physician identity
```

No connection grant, media session, encounter, consent, care, prescription, claim, integration, message, or external action is created.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Candidate isolation | Only the exact current synthetic candidate sees and reserves the applicant request; a different physician cannot see or reserve it. |
| Freshness/provenance | Exact applicant/request/patient/submission/authorization/candidate/practice/facility chain and database-time participation freshness. |
| Atomicity | Reservation, queue/request transitions, appointment assignment, and request event are all-or-nothing; replay is stable. |
| Concurrency/recovery | One winner under 20 callers; uniqueness holds; lease expiry preserves evidence and requeue remains candidate-bound. |
| Applicant status | Reserved is plain-language physician-preparing; assignment true, identity false, no position/wait/coverage/care claim. |
| UI/accessibility | Applicant-origin label, synthetic candidate-match explanation, keyboard recovery, stable focus, 320-pixel reflow, serious-or-critical WCAG check, no sensitive persistence. |
| Regression | Backend, frontend, browser, route/accessibility, runtime, authorization, OpenAPI, migrations/recovery, queue lifecycle, planning, Graphify, and cleanup. |

## Gate preserved

Sprint 55 must separately authorize the applicant path into a connection-room/grant boundary. Consultation, chart access, clinician-obtained consent, encounter, care, real coverage and financial routing, prescribing, claims, integrations, completion, cancellation, independent review, and production remain open.
