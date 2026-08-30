# Decision 0068: POC synthetic patient request cancellation

Status: approved for the non-production POC only

## Decision

The authenticated owner of a synthetic telehealth request may cancel that request before practice queue authorization. The command requires the current aggregate version, an idempotency key, and an explicit synthetic-cancellation confirmation.

## Boundary

- Allowed source states: `Draft`, `LocationConfirmed`, `SafetyScreening`, `Intake`, `Verification`, and `OperationalReview`.
- The command appends the request event `synthetic-request-cancelled` and transitions the request to terminal `Cancelled`.
- It is unavailable after queue authorization, including `Queued`, `Reserved`, `Connecting`, `InConsultation`, `WrapUp`, and `Closed`.
- It creates no appointment, queue entry, reservation, connection grant, consultation, clinical action, prescription, billing item, claim, integration, notification, or external action.
- It is a POC workflow control only; it is not a real patient-care, appointment, or medical-record cancellation policy.

## Verification

The slice requires state-machine coverage, API contract validation, PostgreSQL migration resilience, full backend/UI suites, runtime-safety validation, and loopback staging verification. Production remains disabled.
