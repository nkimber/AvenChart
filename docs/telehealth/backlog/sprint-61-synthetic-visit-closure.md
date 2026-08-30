# Sprint 61 plan: synthetic visit closure

Status: Implemented under [TH-DEC-0064](../decisions/0064-approved-sprint-61-synthetic-visit-closure.md)

## Goal

After a governed synthetic encounter lock, safely close only the request and consultation lifecycle and return the owning physician to availability without completing the appointment or creating any downstream work.

## Delivery boundary

- Exact owner, practice, facility, active adult patient, ended session, released reservation, unfinished appointment, `MediaEnded` consultation, `WrapUp` request/shift, and governed encounter lock only.
- Require the current consultation version plus explicit lock-review and synthetic-only confirmations.
- Use one serializable, append-only, semantic-idempotent transaction to move the consultation/request to `Closed` and the existing shift to `Active`.
- Keep the appointment in progress and preserve zero encounter-completion, delivery, billing, claim, pharmacy, integration, and external effects.
- Present the private physician control only after the current workspace successfully records the encounter lock.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership | Exact current physician and locked consultation lineage only; opaque denial otherwise. |
| Atomicity | Consultation, request, shift, and append-only events commit together under serializable isolation. |
| Replay | Same semantic command returns its original closure; conflicting idempotency content fails closed. |
| Appointment | Existing appointment status is never mutated and remains in progress. |
| Consequence | Encounter completion, patient delivery, billing, claims, pharmacy transmission, integration, and external action remain false. |
| Recovery | Any invalid or stale source leaves the physician in wrap-up and source lifecycle unchanged. |
| Regression | Backend, focused UI, OpenAPI, authorization, runtime, migration/recovery, planning, and Graphify evidence. |

## Gate preserved

Clinical completion, appointment fulfillment, patient-facing completion/status delivery, AVS, legal signing, coding, billing, claims, pharmacy delivery, integrations, independent review, and production remain separate gated work.
