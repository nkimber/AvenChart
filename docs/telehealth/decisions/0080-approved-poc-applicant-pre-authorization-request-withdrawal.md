# Decision 0080: POC applicant pre-authorization request withdrawal

Status: approved for the non-production POC only

## Decision

Allow the exact prospective-applicant access-key owner to withdraw their synthetic request while it is in `OperationalReview`, before a configured practice administrator authorizes it into the synthetic clinician queue.

## Boundary

- The command uses the existing applicant access-key route and requires the branded-practice host, current request version, semantic idempotency key, and explicit synthetic-withdrawal confirmation. It is not a portal session and reveals no protected applicant evidence.
- The server locks the applicant and request together. It permits only `OperationalReview` version 12 with no queue authorization, queue entry, appointment, reservation, connection, or consultation. It transitions the request to `Cancelled` version 13 and appends one access-key-subject-bound lifecycle event.
- A concurrently completed administrator authorization wins the request lock and causes this pre-authorization command to fail closed; the owner must reload and use the separately governed ready-queue withdrawal only when eligible.
- Applicant status remains minimized after cancellation. The terminal view states only whether no queue/appointment had been created or, for the existing queued path, whether the synthetic queue entry and provisional appointment were removed.
- The command creates no practice acceptance, queue entry, appointment, reservation, connection, media, consultation, note, prescription, billing, claim, integration, notification, external action, or production behavior.

## Verification

The slice requires focused policy, API transport, and UI-confirmation tests, full backend/frontend regression and bundle evidence, planning/runtime/OpenAPI/staging checks, and Graphify review. Applicant-record deletion, post-reservation cancellation, real communications, clinical disposition, care completion, billing, claims, integrations, and all release gates remain separately governed work.
