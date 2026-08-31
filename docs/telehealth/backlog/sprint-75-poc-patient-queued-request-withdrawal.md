# Sprint 75 plan: POC patient queued-request withdrawal

Status: Implemented and staging-verified under [TH-DEC-0078](../decisions/0078-approved-poc-patient-queued-request-withdrawal.md)

## Goal

Give an established patient a safe way to withdraw a synthetic request before any clinician takes it, even after the practice has placed it in the ready queue.

## Delivery boundary

- Extend patient cancellation only from `Queued` when its durable queue entry is `Ready` and the linked provisional appointment is still unstarted.
- Lock the request, queue entry, and appointment together; transition the request to `Cancelled`, the queue item to `Removed`, and the appointment to the existing cancellation status.
- Retain all scheduling facts and append one owner-attributed lifecycle event.
- Keep cancellation unavailable from `Reserved`, `Connecting`, `InConsultation`, `WrapUp`, and `Closed`.
- Do not create, alter, or infer a reservation, clinician shift, connection, media session, consultation, documentation, prescription, billing, claim, notification, integration, external action, or production behavior.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Ownership | Only the current authenticated established-patient owner can use the existing patient cancellation command. |
| Concurrency | The request, ready queue item, and provisional appointment are locked and changed atomically. |
| Lifecycle | `Queued -> Cancelled` is allowed; all clinician-work and consultation states remain blocked. |
| Scheduling | The provisional appointment is retained and changed only to the existing cancelled (`x`) status. |
| Transparency | The patient page shows the cancellation control while queued and explains the narrow outcome without clinical or downstream claims. |
| Consequence | No reservation, connection, consultation, care, financial, integration, external, or production effect is created. |
| Regression | State/UI tests, backend/frontend suites, bundle, planning/runtime/OpenAPI/staging/Graphify evidence passes. |

## Gate preserved

Applicant withdrawal, cancellation after a reservation or connection, patient/clinician messaging, clinical disposition, care completion, billing, claims, integrations, and production remain separately governed work.
