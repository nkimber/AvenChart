# Sprint 76 plan: POC applicant queued-request withdrawal

Status: Implemented and staging-verified under [TH-DEC-0079](../decisions/0079-approved-poc-applicant-queued-request-withdrawal.md)

## Goal

Give a prospective applicant the same pre-reservation ability to withdraw their own synthetic queued request without converting the access-key flow into a patient portal or disclosing protected evidence.

## Delivery boundary

- Add one applicant-key-only command for an exact `Queued` request with a `Ready` queue entry and unstarted provisional appointment.
- Lock the applicant, request, queue entry, and appointment atomically; retain scheduling facts while moving the request to `Cancelled`, queue entry to `Removed`, and appointment to `x`.
- Preserve a minimized applicant queue-status projection after withdrawal.
- Require an explicit confirmation and semantic idempotency; block reservation, connection, consultation, and all later lifecycle states.
- Do not create a portal session, access protected applicant evidence, identify a physician, create a reservation/connection/consultation, or perform care, financial, notification, integration, external, or production work.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Access | Only the exact applicant access key can call the new command; it accepts no portal or staff identity. |
| Concurrency | Applicant, request, queue entry, and appointment are locked and transition atomically. |
| Lifecycle | Only `Queued -> Cancelled` is permitted, with a `Ready -> Removed` queue and cancelled provisional appointment. |
| Transparency | Applicant polling exposes a neutral terminal cancellation status with no clinician or protected-information disclosure. |
| Consequence | Reservation, connection, consultation, care, prescription, financial, integration, notification, external, and production effects remain false. |
| Regression | Applicant-policy/API/UI tests, full suites, bundle, planning/runtime/OpenAPI/staging/Graphify evidence passes. |

## Gate preserved

Applicant-record deletion, withdrawal before queue authorization, cancellation after a reservation or connection, communication delivery, clinical disposition, care, billing, claims, integrations, and production remain separately governed work.
