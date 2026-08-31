# Decision 0069: POC synthetic request history

Status: approved for the non-production POC only

## Decision

The authenticated owner of a synthetic telehealth request may read a minimized lifecycle history for that exact request.

## Boundary

- The read returns only aggregate version, resulting lifecycle status, neutral POC message, and occurrence timestamp from the existing append-only request-event ledger.
- It never returns actor identity, raw action names, idempotency keys, fingerprints, clinical notes, media, transcripts, prescriptions, appointment details, billing, claims, integrations, notifications, or external activity.
- It is owner-scoped, read-only, and has no mutation, queue, appointment, reservation, consultation, care, financial, delivery, integration, external, or production effect.
- It is POC transparency only and is not a medical-record, audit-disclosure, or patient-delivery policy.

## Verification

The slice requires API contract coverage, ownership/not-found behavior, full backend/UI suites, runtime-safety validation, and loopback staging verification. Production remains disabled.
