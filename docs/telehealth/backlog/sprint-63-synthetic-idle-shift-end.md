# Sprint 63 plan: synthetic idle-shift end

Status: Approved under [TH-DEC-0066](../decisions/0066-approved-sprint-63-synthetic-idle-shift-end.md)

## Goal

Allow an exact physician to end an idle synthetic telehealth shift so availability is not left active after work is complete.

## Delivery boundary

- Require the exact active shift identifier, version, owner, practice, facility, and two explicit confirmations.
- Lock and prove no active reservation and no active or wrap-up consultation before changing only the shift to `Ended`.
- Persist end-command idempotency/provenance and return an immutable replay result.
- Clear the clinician UI workspace only after the server confirms the shift ended; a new shift remains a distinct start command.
- Create no patient, queue, appointment, encounter, documentation, prescription, delivery, billing, claim, media, integration, external, or production consequence.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership | Exact owner/practice/facility/shift/version only. |
| Idle guard | Active reservation, active consultation, wrap-up, and stale/ended source all fail closed. |
| Atomicity | Shift status/end time/provenance commit together or not at all. |
| Replay | Same semantic command returns one end result; changed content conflicts. |
| UX | Disabled until confirmations; accessible error recovery; no patient-care completion wording. |
| Consequence | Every patient, clinical, financial, media, integration, external, and production effect remains false. |
| Regression | Backend, focused UI, OpenAPI, authorization, runtime safety, migration/recovery, planning, and Graphify evidence. |

## Gate preserved

Reservation cancellation, consultation termination, appointment or encounter completion, clinical/legal signing, patient delivery, prescriptions, billing, claims, integrations, real media, real patient care, and production remain separate gated work.
