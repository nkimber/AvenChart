# Decision 0078: POC patient queued-request withdrawal

Status: approved for the non-production POC only

## Decision

Allow the exact authenticated established-patient owner to withdraw an already authorized synthetic request only while it is still `Queued`, ready for selection, and its provisional appointment has not started.

## Boundary

- The existing cancellation command remains owner-scoped, current-version-bound, semantically idempotent, and requires explicit synthetic confirmation.
- The server locks the request, queue entry, and provisional appointment together. It atomically changes the request to `Cancelled`, changes its only queue entry from `Ready` to `Removed`, and changes its provisional scheduling appointment to the existing cancelled (`x`) status.
- It appends a patient-owned lifecycle event. The original appointment facts remain retained by the scheduling system; no appointment record is deleted.
- It is unavailable once a clinician has reserved the request, a connection has begun, or a consultation/work-up state exists. Those states require separately governed disposition work.
- It creates no reservation, clinician shift change, connection, media, consultation, note, prescription, billing item, claim, integration, notification, external action, or production behavior.

## Verification

The slice requires state-machine and UI eligibility tests, full regression and bundle evidence, planning/runtime/OpenAPI checks, staging health, and Graphify review. Applicant-owned withdrawal, clinician-notified withdrawal, reserved/connecting cancellation, clinical disposition, communication delivery, and all release gates remain separately governed work.
