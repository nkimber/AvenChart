# Decision 0076: POC clinician connection recovery

Status: approved for the non-production POC only

## Decision

Permit the owning physician to explicitly abandon a failed prepared synthetic connection attempt before any consultation starts, returning that request to its existing queue without waiting for the lease to expire.

## Boundary

- The command requires an active physician shift, exact reservation ownership, facility scope, idempotency, the current request version, and two affirmative confirmations.
- It applies only while the reservation is active, the request is `Connecting`, a current prepared or waiting-room synthetic session exists, and no consultation context exists.
- It atomically changes the reservation to `Released`, the existing queue entry to `Ready`, the request to `Queued`, and clears only the provisional appointment assignment.
- It revokes issued participant grants, ends the prepared synthetic session, and appends one `connection-abandoned` lifecycle event. The local WebRTC relay remains transient and can no longer be accessed with the revoked grants.
- It does not create a queue entry, reorder the queue, choose a physician, notify or contact a patient, record a clinical decision, create an encounter, or perform clinical, billing, claim, integration, or external work.

## Verification

The slice requires service boundary tests, interface confirmation tests, full backend and frontend regression, OpenAPI/runtime/planning validation, staging health, and Graphify review. Production connection-recovery policy, patient communication, operational monitoring, and clinical escalation remain separately governed work.
