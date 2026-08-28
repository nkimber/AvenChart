# Sprint 27: synthetic communication/access readiness

Status: Approved for bounded implementation by [TH-DEC-0030](../decisions/0030-approved-sprint-27-synthetic-communication-access-readiness.md)  
Scope: Applicant-owned immutable receipt for reconfirming server-owned state/callback context and recording bounded spoken-language, interpreter, and accessibility-support preferences after insurance handoff; no chart mutation, support arrangement, technology readiness, complete intake, consent, acceptance, request, queue, care, communication, external integration, or production use

## 1. Outcome

Add the next progressive-intake checkpoint without collecting clinical history or pretending preferences have been operationally fulfilled. The patient sees only a state code and masked callback, confirms a sufficiently safe/private communication context and disconnection plan, selects a bounded language preference, and indicates whether interpreter or accessibility support will later be needed.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP27-001` | Add a server-owned communication-context snapshot bound to the original passing safety evaluation and verified callback source, with state/callback masking and deterministic fingerprinting. |
| `TH-SP27-002` | Add applicant-key protected private/no-store retrieval after insurance confirmation, returning an allowlisted language catalog and no raw location/contact, patient, insurance, proofing, clinical, candidate, or staff data. |
| `TH-SP27-003` | Add one idempotent atomic command for language preference, interpreter/accessibility indicators, and five mandatory context/safety/synthetic affirmations, without free text or source-field edits. |
| `TH-SP27-004` | Add append-only receipt/event provenance bound to the promotion, patient shell, registration, insurance handoff, safety evaluation, practice/facility, and aggregate version. |
| `TH-SP27-005` | Add an accessible patient panel with masked context, preference-vs-arrangement language, unsafe/private stop direction, disabled submit, stable ambiguous retry, focus, reflow, and no browser persistence. |
| `TH-SP27-006` | Prove source/access/version/provenance isolation, bounded vocabulary, all affirmations, unsafe-context rejection, exact replay, changed replay, contention, append-only evidence, zero applicant/patient/insurance/communication/downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticInsuranceDetailsConfirmed`; the successful promotion, registration receipt, insurance-handoff receipt, and original passing safety evaluation must still exist; the patient shell must remain facility-bound, unmerged, portal-disabled, and equal to applicant minimum fields; no canonical insurance record may exist; and the stored safety-location state plus verified callback source must remain bound to the same applicant.

## 4. Exit boundary

Sprint 27 ends at `SyntheticCommunicationAccessReadinessRecorded`. The receipt records only a bounded preference and context acknowledgment. It is not a new clinical safety screen, interpreter/accommodation assignment, translated content, technology readiness, complete intake, legal consent, practice acceptance, request creation, queueing, communication, or care authorization.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/snapshot | Exact allowlist, deterministic state/callback fingerprint, last-four callback mask, fixed language catalog, and no raw/source-field projection. |
| Database | Prior-chain mismatch, unsafe/private false, bounded vocabulary, stale/expired/canonical-coverage/portal-enabled rejection, exact replay, changed replay, concurrent one-receipt convergence, append-only evidence, and zero source/patient/insurance/communication/downstream delta. |
| HTTP | Applicant-only private/no-store reads/writes, required idempotency, typed bounded input/output, bounded failures, and hidden identifier/contact/location exclusion. |
| UI | Loading/error/retry, masked context, preference/arrangement distinction, unsafe/private direction, required affirmations, disabled submit, confirmed outcome, reflow, focus, and no preference/context browser persistence. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
