# Decision 0072: POC synthetic after-visit plan preview

Status: approved for the non-production POC only

## Decision

When the existing physician-owned synthetic closure transaction succeeds, the same transaction may create one immutable, patient-owned after-visit plan preview from the already recorded synthetic safety-disposition draft and final-review evidence. It is available only to the established patient or the exact applicant access-key owner of the closed request.

## Boundary

- The preview binds only the closed request and consultation, governed encounter lock, current safety-disposition and final-review versions, and a one-way source fingerprint.
- It exposes only the physician-authored synthetic disposition label, follow-up owner and timeframe, next-step and warning text, communication state, explicit `NON_PRODUCTION` source mode, and the disclosure that it is not actual medical advice, an AVS, or an external delivery record.
- It identifies no clinician, diagnosis, medication, prescription, pharmacy, billing, claim, insurance, patient identifier, appointment identifier, or encounter identifier.
- It is rendered only through authenticated, no-store reads. It creates no notification, message, portal alert, email, text, download, print artifact, outbox work item, external traffic, appointment or encounter completion, legal record, or care delivery assertion.
- The source row is append-only. A changed safety disposition or final review cannot rewrite a closed preview; it instead requires a separately governed future workflow.

## Verification

The slice requires migration resilience, source-fingerprint and closed-lineage coverage, exact patient/applicant ownership coverage, API contract validation, full backend/UI suites, runtime-safety validation, loopback staging verification, and Graphify review. Production remains disabled.
