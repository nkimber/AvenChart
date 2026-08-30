# Sprint 60 plan: synthetic encounter finalization

Status: Implemented under [TH-DEC-0063](../decisions/0063-approved-sprint-60-synthetic-encounter-finalization.md)

## Goal

Create an immutable, governed encounter lock only after the owning physician has confirmed the exact current synthetic source evidence, without completing the visit or releasing the physician.

## Delivery boundary

- Exact owner, practice, facility, adult patient, released reservation, ended session, unfinished appointment, `MediaEnded` consultation, and `WrapUp` shift only.
- Recheck the complete SOAP, current safety-disposition version, and final clinical review matching the current SOAP, disposition, and optional signed-prescription order inside the transaction that invokes the generic governed encounter lock.
- Require explicit source-review and synthetic-only affirmations and exact expected source versions.
- Expose an accessible private physician control and truthful immutable-lock result.
- Keep visit completion, clinician release, appointment fulfillment, patient delivery, coding, billing, claims, integrations, and external effects disabled.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership | Exact current physician and locked consultation lineage only; opaque denial otherwise. |
| Atomicity | Source revalidation occurs in the encounter-lock transaction before the immutable snapshot. |
| Version binding | SOAP, disposition, final-review, and current optional prescription identifiers must match exactly. |
| Immutability | Existing governed encounter lock produces an immutable manifest and leaves later draft change to amendments. |
| Consequence | Legal effect, completion, patient delivery, billing, claims, integration, and external action remain false. |
| Availability | The physician remains in wrap-up and cannot accept new work. |
| Regression | Backend, focused UI, OpenAPI, authorization, runtime, migration/recovery, planning, and Graphify evidence. |

## Gate preserved

A separately approved lifecycle slice must define an atomic synthetic visit closure, request/appointment state, clinician availability, recovery and replay semantics. Production clinical, legal, billing, claims, pharmacy, AVS, and integration gates remain open.
