# Sprint 34: synthetic pre-request readiness acknowledgment

Status: Approved for bounded implementation by [TH-DEC-0037](../decisions/0037-approved-sprint-34-synthetic-pre-request-readiness-acknowledgment.md)  
Scope: Applicant-owned immutable acknowledgment of a minimized five-section readiness projection after the clinical-information summary; no edit, completion, eligibility, task, acceptance, request, queue, care, financial, integration, external, or production consequence

## 1. Outcome

Give the promoted applicant one compact checkpoint showing which bounded synthetic onboarding receipts exist and which broad steps remain unresolved before any later practice review. The server exposes only five stable section keys, coarse receipt states, and informational route codes. Acknowledgment records patient understanding without representing the applicant as ready, accepted, submitted, queued, or authorized for care.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP34-001` | Build a server snapshot over the exact registration, insurance, communication/access, device-preparation, clinical-inventory, and clinical-summary receipts. |
| `TH-SP34-002` | Add applicant-key protected private/no-store retrieval returning only five section keys, coarse receipt states, unresolved routes, policy/version, and limitations. |
| `TH-SP34-003` | Add one idempotent atomic acknowledgment with four mandatory no-completion/no-request/correction-boundary affirmations. |
| `TH-SP34-004` | Derive one of three informational pre-request routes without creating a staff task, clinician task, practice decision, request, or queue item. |
| `TH-SP34-005` | Add append-only receipt/event provenance with every identity, clinical, operational, financial, integration, and external consequence false. |
| `TH-SP34-006` | Add an accessible applicant panel with stable retry, no-edit section review, correction-stop direction, explicit boundary acknowledgments, focus/reflow, and no browser persistence. |
| `TH-SP34-007` | Prove source/access/version/provenance isolation, route priority, minimization, replay/contention, append-only behavior, zero-delta consequences, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticClinicalInformationSummaryConfirmed`; the successful promotion, portal-disabled unmerged patient shell, registration confirmation, insurance handoff, communication/access receipt, passing device preparation, clinical inventory, and summary confirmation must still exist and agree; no canonical insurance, medication, prescription, allergy, or problem row may exist for the promoted shell; and stored practice/facility/applicant/patient relationships must remain unchanged.

## 4. Exit boundary

Sprint 34 ends at `SyntheticPreRequestReadinessAcknowledged`. The receipt retains only exact source IDs/fingerprints, bounded communication-support signals, five server-derived section states/routes, four acknowledgments, and one overall route. It is not identity assurance, coverage, rendering-clinician network verification, fulfilled support, technology readiness, clinical reconciliation, completed intake, eligibility, legal consent, a review task, practice acceptance, request submission, queue entry, appointment, encounter, or care authority.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact four acknowledgments; deterministic source fingerprint; five stable keys; server-owned section and overall routes; every consequence false. |
| Database | Complete source mismatch, stale/expired/canonical-data/portal-enabled rejection, exact replay with in-transaction revalidation, changed replay, concurrent convergence, append-only evidence, and zero source/canonical/downstream delta. |
| HTTP | Applicant-only private/no-store read/write, required idempotency, typed bounded input/output, and exclusion of source values, identity/contact/payer/clinical/device details and free text. |
| UI | Loading/error/retry, five-section no-edit review, correction-stop direction, all acknowledgments, disabled submit, informational result, 320-pixel reflow, focus, and no browser persistence. |
| Standards boundary | Explicitly no FHIR, US Core, USCDI, X12, eligibility, task, service request, appointment, claim, or interoperability payload. |
| Regression | Backend/frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
