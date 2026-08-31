# Decision 0079: POC applicant queued-request withdrawal

Status: approved for the non-production POC only

## Decision

Allow the exact prospective applicant access-key owner to withdraw their synthetic request only while it is `Queued`, its sole queue entry is `Ready`, and its provisional appointment has not started.

## Boundary

- The applicant command is separate from the established-patient portal command. It requires the exact access key, branded-practice host, current request version, semantic idempotency key, and explicit synthetic-withdrawal confirmation.
- The server locks the applicant, request, queue entry, and provisional appointment together. It changes the request to `Cancelled`, removes the ready queue entry, marks the existing provisional appointment cancelled, and appends one access-key-subject-bound lifecycle event.
- The applicant queue-status projection continues to expose only a minimized cancelled state. It does not disclose staff, physician, protected registration, payer, candidate, reservation, media, or clinical information.
- It is unavailable after reservation, connection, consultation, wrap-up, or closure. It creates no reservation, shift change, connection, media, consultation, note, prescription, billing, claim, integration, notification, external action, or production behavior.

## Verification

The slice requires focused applicant-policy, API transport, and UI-confirmation tests, full backend/frontend regression and bundle evidence, planning/runtime/OpenAPI/staging checks, and Graphify review. Applicant-record deletion, withdrawal before authorization, post-reservation cancellation, messaging, clinical disposition, care completion, and all release gates remain separately governed work.
