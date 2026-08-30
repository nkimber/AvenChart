# Sprint 65 plan: POC synthetic patient request cancellation

Status: Implemented under [TH-DEC-0068](../decisions/0068-approved-poc-synthetic-patient-request-cancellation.md)

## Goal

Allow the exact authenticated owner to withdraw an incomplete synthetic request before the practice authorizes it into the clinician queue.

## Delivery boundary

- A versioned, semantic-idempotent patient command moves only eligible pre-queue requests to terminal `Cancelled`.
- The existing append-only request-event ledger records the cancellation.
- The patient workspace requires explicit confirmation and states the unavailable downstream effects in plain language.
- No queue, appointment, reservation, connection, consultation, prescription, billing, claim, integration, notification, or external behavior is changed.

## Gate preserved

Post-authorization cancellation, appointment cancellation, clinician/recovery workflows, patient delivery, claims, integrations, real patient care, and production remain separately governed work.
