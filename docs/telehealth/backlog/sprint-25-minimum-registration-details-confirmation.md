# Sprint 25: synthetic minimum registration-details confirmation

Status: Approved for bounded implementation by [TH-DEC-0028](../decisions/0028-approved-sprint-25-minimum-registration-details-confirmation.md)  
Scope: Applicant-owned review and immutable no-edit confirmation of the exact minimum registration details copied into a successfully promoted portal-disabled synthetic patient shell after state-notice acknowledgment; no identity assurance, corrections, complete demographics/history/intake, legal consent, practice acceptance, insurance confirmation, request, queue, care, downstream action, external integration, or production use

## 1. Outcome

Add a privacy-bounded registration review that proves the applicant affirmatively confirmed the minimum copied fields without allowing the browser to author patient data. Keep correction honest: any problem stops this path and directs the applicant to restart or contact the practice.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP25-001` | Add a server-owned minimum-details snapshot policy with exact applicant/patient equality, contact masking, bounded display fields, and deterministic fingerprinting. |
| `TH-SP25-002` | Add applicant-key protected private/no-store retrieval after a current state-notice acknowledgment, returning no patient identifier, full contact value, street address, or hidden evidence. |
| `TH-SP25-003` | Add one idempotent atomic no-edit confirmation command with name/birth-date, contact, residence-region, no-correction-needed, and synthetic affirmations. |
| `TH-SP25-004` | Add append-only receipt/event provenance bound to the notice acknowledgment, promotion, portal-disabled unmerged patient shell, applicant/patient snapshot, practice/facility, and aggregate version. |
| `TH-SP25-005` | Add an accessible patient review section with clear source labels, masked contacts, independent confirmations, correction stop guidance, disabled submit, stable ambiguous retry, and recovery. |
| `TH-SP25-006` | Prove snapshot equality and minimization, missing/drifted/portal-enabled/merged rejection, no-edit input, exact replay, contention, append-only behavior, zero patient/downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticTelehealthNoticeAcknowledged`; the state notice receipt and successful promotion must still exist; the linked patient shell must remain active, facility-bound, unmerged, and portal-disabled; and the patient's legal name, birth date, email, phone, residence state, and postal code must still exactly equal the source applicant.

## 4. Exit boundary

Sprint 25 ends at `SyntheticMinimumRegistrationDetailsConfirmed`. The receipt confirms only the bounded copied registration snapshot. It is not identity proofing, a portal login, a correction, a complete demographic/clinical-history intake, legal consent, practice acceptance, insurance confirmation, request creation, queueing, or care authorization.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/snapshot | Exact allowlist and masks, adult/date validity inherited from applicant, applicant/patient equality, deterministic fingerprint, and no street-address or hidden-field projection. |
| Database | Successful receipt; notice/promotion/patient/snapshot mismatch rejection; stale/expired/portal-enabled/merged rejection; exact replay; changed replay; concurrent one-winner; append-only; zero patient/downstream delta. |
| HTTP | Applicant-only private/no-store reads/writes, required idempotency, typed no-edit input/output, bounded errors, and no patient/member/proofing identifiers. |
| UI | Loading/error/retry, source labels, masked contact details, explicit confirmations, correction stop direction, disabled submit, confirmed outcome, reflow, focus, and no browser persistence. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
