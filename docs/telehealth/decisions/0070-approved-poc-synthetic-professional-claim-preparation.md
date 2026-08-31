# Decision 0070: POC synthetic professional-claim preparation receipt

Status: approved for the non-production POC only

## Decision

After the owning physician has recorded the existing final clinical-review evidence and governed synthetic encounter lock, the physician may create one source-bound, durable, non-transmitting professional-claim preparation receipt during synthetic `WrapUp`.

## Boundary

- The receipt binds only the consultation, locked encounter, current source-evidence versions, a one-way source fingerprint, and the existing `ASC_X12N_837P_005010X222A1` adapter label.
- It requires explicit source-review, synthetic-only, and no-submission confirmations and is semantic-idempotent for that exact physician and consultation.
- The existing non-production gateway may return a deterministic `PreparedOnly` receipt. It serializes no X12, retains no claim payload, and contacts no clearinghouse, payer, pharmacy, or other destination.
- The receipt is explicitly not a diagnosis, procedure, modifier, fee, payer/product, billing-provider, coverage, invoice, claim, submission, acknowledgment, adjudication, payment, or patient-billing record.
- It does not complete the appointment or encounter, deliver anything to a patient, change a prescription, create a financial record, or enable production behavior.

## Verification

The slice requires migration resilience, semantic-idempotency and ownership coverage, API contract validation, full backend/UI suites, runtime-safety validation, loopback staging verification, and Graphify review. Production remains disabled.
