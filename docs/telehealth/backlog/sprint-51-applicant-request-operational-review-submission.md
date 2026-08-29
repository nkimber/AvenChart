# Sprint 51 plan: applicant request operational-review submission

Status: Authorized by [TH-DEC-0054](../decisions/0054-approved-sprint-51-applicant-request-operational-review-submission.md); implementation in progress

## Goal

Allow the owner of one exact, unexpired Sprint 50 synthetic request to submit its completed non-production automated evidence to the configured practice's operational-review queue. Advance only the request from pending `Verification` version 11 to `OperationalReview` version 12. Do not imply or create real coverage, financial clearance, practice acceptance, contact, queueing, appointment, encounter, consent, or care.

## Delivery boundary

- Add applicant-private GET/POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/operational-review-submission`.
- Add migration `V0326__telehealth_applicant_request_operational_review_submission.sql` with one append-only, request/applicant/source-unique submission record, exact provenance trigger, database-clock/freshness constraints, and explicit no-consequence flags.
- Rebind the exact Sprints 40–50 evidence chain, request, applicant, patient shell, and current candidate staff record. Require request `Verification` version 11 and one current exact synthetic participation result.
- Present a minimized server-owned review: practice, payer/product, patient state, purpose, date of service, masked candidate, service/modality, and synthetic evidence statuses. Do not return internal evidence, patient, member, NPI, TIN, authority, affiliation, contract, or staff identifiers.
- Require four explicit acknowledgments. Append one submission and one request event and advance the request atomically to `OperationalReview` version 12.
- Reuse the existing practice/facility-scoped administrator operational-review projection. Do not add a staff claim, assignment, decision, exception, queue entry, appointment, or contact action.
- Extend applicant UI/API contracts, deterministic browser journey, runtime workflow, authorization/OpenAPI/migration/readiness/planning evidence, and a dedicated live proof.

## State and evidence contract

```text
SyntheticRequestCreated v26 (unchanged)
  + request Verification v11
  + exact immutable request evidence chain through Sprint 50
  + four applicant acknowledgments
  -> one append-only operational-review submission
  -> request OperationalReview v12
  -> one request event
  -> visible to configured practice/facility administrator review list
```

`OperationalReview` means awaiting a later authorized staff decision. It is not `Ready`, `Queued`, `Reserved`, an appointment, an encounter, consent, or care. The patient-facing direction must state that the practice has not accepted the request and that insurance/payment are not guaranteed.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Policy | Exact normalization, four required acknowledgments, request v11 entry and v12 result, stable fingerprints, no client-supplied disposition. |
| Access | Missing, portal, staff, foreign, expired, and wrong-host access fail closed without revealing existence. |
| Provenance | Exact applicant/request/patient-shell/evidence/staff chain; current participation result; no duplicate or downstream consequence. |
| Transaction | One submission, request transition, and event commit atomically; contention yields one winner; replay is exact; changed-key reuse fails. |
| Database | Additive V0326, append-only trigger, source uniqueness, state/freshness/hash/acknowledgment/no-consequence checks, guarded insert. |
| Projection | Applicant projection is masked/minimized/no-store; administrator list is practice/facility isolated and exposes only the existing minimum item. |
| UI/accessibility | All acknowledgments unchecked, no-edit review, stable retry, focus recovery, keyboard operation, 320-pixel reflow, automated serious-or-critical WCAG checks, no persistence. |
| Regression | Backend, frontend, browser, migrations/recovery, runtime, authorization, OpenAPI, queue, planning, Graphify, bootstrap, and cleanup. |

## Gate preserved

Sprint 52 must separately authorize administrator review/acceptance and final atomic queue authorization for applicant-originated requests. Real state authority, credentialing, payer/directory participation, canonical coverage, financial route, consent, availability, appointment, encounter, and care remain later gates.
