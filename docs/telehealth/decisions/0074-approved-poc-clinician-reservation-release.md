# Decision 0074: POC clinician reservation release

Status: approved for the non-production POC only

## Decision

Permit the exact physician who owns an active synthetic reservation to deliberately release it back to its existing practice queue before any connection room or synthetic consultation has started.

## Boundary

- The action requires the current request version, a new idempotency key, and explicit confirmations that no connection room or consultation exists and that the effect is synthetic-only.
- The server verifies the exact owning physician, active shift, practice/facility, active reservation, `Reserved` request, and absence of a video session or consultation context. It fails closed after a waiting room, connection, consultation, expiry, ownership change, or stale version.
- One transaction changes only the reservation to `Released`, the existing queue entry to `Ready`, the request to `Queued`, and its unassigned synthetic appointment back to unassigned. The queue's original `ready_at` is preserved.
- An append-only request event records the request-state transition and command fingerprint. Exact replay returns the same release result; changed reuse fails closed.
- This is not a clinical declination, diagnosis, acuity determination, patient contact, patient notification, assignment decision, service refusal, care delivery, or production workflow.
- There is no connection-room issuance, media, consultation, documentation, prescription, billing, claim, external integration, vendor, or production activation.

## Verification

The slice requires physician authorization and confirmation tests, exact OpenAPI/UI contract coverage, queue-refresh behavior, full backend/UI regression, runtime-safety and planning validation, staging health, and Graphify review.
