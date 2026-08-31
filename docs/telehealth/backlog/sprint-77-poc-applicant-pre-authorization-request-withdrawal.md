# Sprint 77 plan: POC applicant pre-authorization request withdrawal

Status: Implemented and staging-verified under [TH-DEC-0080](../decisions/0080-approved-poc-applicant-pre-authorization-request-withdrawal.md)

## Goal

Let a prospective applicant stop their own synthetic request while it is awaiting practice operational review, without exposing staff review evidence or creating a queue, provisional appointment, or patient portal session.

## Delivery boundary

- Extend the exact applicant access-key withdrawal command to the sole pre-authorization state: `OperationalReview` version 12.
- Lock the applicant and request together and fail closed unless authorization, queue, appointment, reservation, connection, and consultation counts are all zero.
- Append one event and transition only `OperationalReview -> Cancelled`; the no-queue/no-appointment terminal response has every later consequence false.
- Continue supporting the existing ready-queue withdrawal as a distinct, atomic branch; when authorization wins a race, require a reload rather than inferring a broader cancellation authority.
- Do not create or expose practice acceptance, staff/physician identity, protected evidence, a queue entry, appointment, reservation, connection, consultation, clinical action, prescription, billing item, claim, integration, notification, external action, or production behavior.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Access | Only the exact applicant access key may act; portal, staff, and clinician identities are not accepted. |
| Lifecycle | Only `OperationalReview` version 12 may become `Cancelled` version 13 before queue authorization. |
| Concurrency | Applicant and request are locked; concurrent authorization resolves through the current request version and fails closed. |
| Consequence | Queue/appointment flags remain false for pre-authorization withdrawal; reservation, connection, consultation, care, financial, integration, notification, external, and production effects remain false. |
| Transparency | The status projection and UI state the minimized cancelled result without staff, physician, payer, clinical, or protected-evidence disclosure. |
| Regression | Focused policy/API/UI tests, full suites, bundle, planning/runtime/OpenAPI/staging/Graphify evidence pass. |

## Gate preserved

Applicant-record deletion, post-reservation or post-connection cancellation, real patient/clinician messaging, clinical disposition, care completion, billing, claims, integrations, and production remain separately governed work.
