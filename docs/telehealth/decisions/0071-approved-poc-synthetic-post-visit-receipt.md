# Decision 0071: POC synthetic post-visit receipt

Status: approved for the non-production POC only

## Decision

When the existing physician-owned synthetic closure transaction succeeds, the same transaction may create one immutable, patient-owned post-visit receipt. The receipt is available only to the established patient or the exact applicant access-key owner of the closed request.

## Boundary

- The receipt binds only the closed request and consultation, the governed encounter lock, source versions, and a one-way source fingerprint.
- Its patient-facing content is fixed, explicit `NON_PRODUCTION` lifecycle information: it states that the synthetic lifecycle closed while the appointment and encounter remain incomplete.
- It identifies no physician, clinical finding, diagnosis, medication, prescription, pharmacy, billing, claim, insurance, recommendation, follow-up instruction, or emergency/care outcome.
- It is rendered only by an authenticated, no-store read endpoint. It sends no notification, message, portal alert, email, text, document download, print artifact, outbox item, or external traffic.
- The source row is append-only; the request and consultation closure remain the sole lifecycle transitions.

## Verification

The slice requires migration resilience, exact patient/applicant ownership and closed-lineage coverage, API contract validation, full backend/UI suites, runtime-safety validation, loopback staging verification, and Graphify review. Production remains disabled.
