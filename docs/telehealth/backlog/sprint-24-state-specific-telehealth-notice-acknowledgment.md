# Sprint 24: state-specific synthetic telehealth-notice acknowledgment

Status: Approved for bounded implementation by [TH-DEC-0027](../decisions/0027-approved-sprint-24-state-specific-telehealth-notice-acknowledgment.md)  
Scope: Applicant-owned retrieval and immutable acknowledgment of one server-selected Georgia, California, or Florida telehealth-notice fixture after successful synthetic patient-shell promotion; no final/legal clinician consent, portal, complete intake, practice acceptance, insurance/coverage promotion, request, queue, care, downstream action, external integration, or production use

## 1. Outcome

Add the first post-promotion patient-owned checkpoint without confusing it with authentication or legally effective consent. Bind the notice to the server-held current-location state and successful patient-shell promotion, require explicit comprehension acknowledgments, and preserve every clinical and downstream gate as false.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP24-001` | Add a versioned server-owned GA/CA/FL notice catalog with official-source metadata, bounded state-specific content, and an explicit pending-legal-review/non-consent label. |
| `TH-SP24-002` | Add applicant-key protected private/no-store notice retrieval after one current successful promotion, returning no canonical patient identifier or hidden evidence. |
| `TH-SP24-003` | Add one idempotent atomic acknowledgment command with location, mode, privacy, emergency, in-person, clinician-reconfirmation, and synthetic affirmations. |
| `TH-SP24-004` | Add append-only receipt/event provenance bound to the promotion, portal-disabled patient shell, safety location, practice/facility, notice key/version, and aggregate version. |
| `TH-SP24-005` | Add an accessible post-promotion patient section with state-specific wording, independent acknowledgments, disabled submit, stable ambiguous retry, and recovery. |
| `TH-SP24-006` | Prove state selection, no-consent labeling, stale/location/missing-patient/portal-enabled rejection, exact replay, contention, append-only behavior, minimization, zero downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticPatientPromoted`; the successful promotion, authorized chain, and passing universal safety screen must still exist; the deterministic patient shell must still be linked to that promotion, active, facility-bound, and portal-disabled; and no prior notice receipt may exist.

## 4. Exit boundary

Sprint 24 ends at `SyntheticTelehealthNoticeAcknowledged`. This proves only that the synthetic applicant acknowledged the currently selected notice fixture. Clinician disclosure and consent documentation, legal consent, identity assurance, portal enrollment, full demographics/history, complete intake, practice acceptance, canonical insurance/coverage, financial acknowledgment, request creation, queueing, and every clinical/downstream action remain unavailable and separately gated.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/catalog | Exact state-to-notice mapping, current official-source URLs, explicit non-consent language, all acknowledgments, and deterministic fingerprints. |
| Database | Successful GA/CA/FL receipts; state/notice/provenance mismatch rejection; stale/expired/missing/blocked/portal-enabled rejection; exact replay; changed replay; concurrent one-winner; append-only; zero downstream delta. |
| HTTP | Applicant-only private/no-store reads/writes, required idempotency, typed minimized input/output, bounded errors, and no patient/member/proofing identifiers. |
| UI | Loading/error/retry, state-specific source/context, explicit non-consent warning, independent checkboxes, disabled submit, acknowledged outcome, reflow, focus, and polling cleanup. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
