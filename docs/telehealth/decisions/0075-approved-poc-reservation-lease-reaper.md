# Decision 0075: POC reservation lease reaper

Status: approved for the non-production POC only

## Decision

Permit a server-owned synthetic lease reaper to return expired pre-consultation reservations to the existing clinician queue without waiting for another physician to request work.

## Boundary

- It runs only while the already-disabled-by-default synthetic telehealth feature is enabled in a non-production environment.
- Every short periodic run uses the existing atomic expiration transaction for the configured synthetic practice and facility.
- An expired active reservation changes to `Expired`; its existing queue entry returns to `Ready`; the request returns to `Queued`; provisional appointment assignment is cleared; pending grants and sessions are expired; and one append-only system event is added.
- The reaper does not select a physician, create a queue entry, reorder the queue, create a connection, deliver media, notify a patient, make a clinical decision, or perform an external action.
- Failures are operationally logged without patient payload and retry on the next bounded interval. When telehealth is disabled the worker exits without opening a scope or database connection.

## Verification

The slice requires registration/runtime-boundary tests, full backend regression, planning validation, staging health, and Graphify review. Production scheduling, distributed leader election, patient notifications, operational alerting, and production recovery policy remain separately governed work.
