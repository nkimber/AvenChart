# Sprint 52 plan: applicant request queue authorization

Status: Implemented under [TH-DEC-0055](../decisions/0055-approved-sprint-52-applicant-request-queue-authorization.md); automated verification passed, independent approvals remain open

## Goal

Allow a current configured-practice administrator to make one explicit, evidence-bound decision that accepts a Sprint 51 applicant-originated request into the synthetic clinician queue. Atomically create one unassigned appointment and one ready queue entry and advance only the request from `OperationalReview` version 12 to `Queued` version 13. Do not imply real coverage, financial clearance, rendering-clinician assignment, consent, an encounter, or care.

## Delivery boundary

- Add staff-private GET/POST `/api/telehealth/v1/admin/applicant-requests/{requestId}/queue-authorization`.
- Add migration `V0327__telehealth_applicant_request_queue_authorization.sql` with one append-only request/source/actor-bound authorization record, exact provenance trigger, database-clock constraints, and explicit consequence flags.
- Rebind the exact Sprints 40–51 evidence chain, applicant, portal-disabled patient shell, request, and current candidate staff record. Require applicant-originated `OperationalReview` version 12 and exactly one current operational-review submission.
- Present a minimized server-owned decision packet: practice, payer/product, patient state, purpose, date of service, masked candidate and billing references, service/modality, and synthetic evidence statuses. Do not return source payload, patient/member identifiers, full NPI/TIN, internal evidence/contract/authority/staff identifiers, price, or clinical narrative.
- Require four explicit acknowledgments. Append one authorization, create one unassigned appointment and one ready queue entry, append one request event, and advance the request atomically to `Queued` version 13.
- Add an applicant-origin discriminator to the administrator operational-review projection. Applicant items use only the dedicated route; established-patient items retain the existing authorization. Explicitly reject applicant-originated requests in the generic repository path.
- Extend administrator UI/API contracts, deterministic browser journey, runtime workflow, authorization/OpenAPI/migration/readiness/planning evidence, and a dedicated live proof.

## State and evidence contract

```text
SyntheticRequestCreated v26 (unchanged)
  + request OperationalReview v12
  + exact immutable request evidence chain through Sprint 51
  + current configured administrator
  + four administrator acknowledgments
  -> one append-only queue authorization
  -> one unassigned synthetic appointment
  -> one Ready queue entry
  -> request Queued v13
  -> one request event
  -> visible to configured practice/facility clinician queue
```

`Queued` means accepted only for the disabled synthetic practice's internal clinician work queue. It does not mean a rendering physician was assigned, a queue position or wait time was promised, insurance/payment was guaranteed, consent was obtained, an encounter began, or care was authorized.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Policy | Exact normalization, four required acknowledgments, request v12 entry and v13 result, stable snapshots/fingerprints, no client-supplied decision outcome. |
| Access | Missing, portal, ordinary staff, inactive, foreign facility, and wrong-role access fail closed without revealing existence. |
| Provenance | Exact applicant/request/patient-shell/evidence/staff/submission chain; no duplicate or downstream consequence; generic path rejects applicant requests. |
| Transaction | One authorization, appointment, queue entry, request transition, and event commit atomically; contention yields one winner; replay is exact; changed-key reuse fails. |
| Database | Additive V0327, append-only trigger, request/source uniqueness, state/hash/acknowledgment/consequence checks, guarded insert. |
| Projection | Decision packet is masked/minimized/no-store; administrator and clinician lists remain practice/facility isolated. |
| UI/accessibility | Applicant-origin label and dedicated review, all acknowledgments unchecked, no-edit evidence, stable retry, focus recovery, keyboard operation, 320-pixel reflow, automated serious-or-critical WCAG checks, no persistence. |
| Regression | Backend, frontend, browser, migrations/recovery, runtime, authorization, OpenAPI, queue, planning, Graphify, bootstrap, and cleanup. |

## Gate preserved

Sprint 53 must separately authorize applicant queue-status access and polling after this staff decision. Real state authority, credentialing, payer/directory participation, canonical coverage, financial route, consent, clinician assignment, encounter, and care remain later gates.
