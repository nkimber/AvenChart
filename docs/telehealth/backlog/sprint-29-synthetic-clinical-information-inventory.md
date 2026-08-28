# Sprint 29: synthetic clinical-information inventory

Status: Approved for bounded implementation by [TH-DEC-0032](../decisions/0032-approved-sprint-29-synthetic-clinical-information-inventory.md)  
Scope: Applicant-owned immutable coarse inventory of whether medication, allergy/intolerance, and other health-history details need collection or assistance after device preparation; no clinical detail, canonical chart statement, reconciliation, eligibility, reviewer task, complete intake, consent, acceptance, request, queue, care, prescribing, external integration, or production use

## 1. Outcome

Add the next preparation checkpoint without converting a patient report into clinical evidence. The applicant selects one of three bounded states for each category, confirms the limitations, and receives only an informational next-step route. Detailed data collection and clinician reconciliation remain later separately governed workflows.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP29-001` | Add a server-owned inventory snapshot bound to the complete device-preparation and prior applicant/patient provenance chain. |
| `TH-SP29-002` | Add applicant-key protected private/no-store retrieval after device preparation, returning only the three allowed category states, policy/version, and explicit limitations. |
| `TH-SP29-003` | Add one idempotent atomic command for three category states and three mandatory patient-report/no-detail/reconciliation acknowledgments. |
| `TH-SP29-004` | Derive one informational route without creating a task, review queue, clinical priority, eligibility result, or operational authority. |
| `TH-SP29-005` | Add append-only receipt/event provenance bound to the promotion, patient shell, registration, insurance, communication/access, device preparation, safety location, callback, practice/facility, and aggregate version. |
| `TH-SP29-006` | Add an accessible patient panel with explicit provisional wording, distinct category controls, stable retry, keyboard/focus/reflow behavior, and no browser persistence. |
| `TH-SP29-007` | Prove source/access/version/provenance isolation, bounded vocabulary, partial failure, routing priority, exact replay, changed replay, contention, append-only evidence, zero source/patient/clinical/downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticDevicePreparationRecorded`; the successful promotion, portal-disabled unmerged patient shell, registration, insurance-handoff, communication/access receipt, passing device-preparation receipt, original passing safety evaluation, and verified callback source must still exist and agree; no canonical insurance may exist; and the stored practice/facility/applicant/patient relationships must remain unchanged.

## 4. Exit boundary

Sprint 29 ends at `SyntheticClinicalInformationInventoryRecorded`. The receipt records only coarse patient-reported inventory states and an informational route. Medication, allergy/intolerance, and history details remain uncollected and unreconciled. It is not clinical intake completion, a triage/eligibility outcome, clinician review, practice acceptance, a request/queue entry, or care/prescribing authority.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact three-value vocabulary per category; deterministic route priority; all acknowledgments; deterministic source fingerprint; hard-false reconciliation/intake/eligibility/review/chart/request/queue/care/prescribing output. |
| Database | Complete prior-chain mismatch, stale/expired/canonical-insurance/portal-enabled rejection, exact replay, changed replay, concurrent one-receipt convergence, append-only evidence, and zero source/patient/clinical/downstream delta. |
| HTTP | Applicant-only private/no-store reads/writes, required idempotency, typed bounded input/output, bounded failures, and exclusion of detailed clinical/contact/patient/insurance identifiers. |
| UI | Loading/error/retry, distinct radio groups, provisional wording, all acknowledgments, disabled submit, informational result, 320-pixel reflow, focus, and no browser persistence. |
| Standards boundary | Three separate inventory categories aligned conceptually to USCDI/FHIR while proving zero FHIR resource, vocabulary-coded statement, canonical list, or interoperability payload. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
