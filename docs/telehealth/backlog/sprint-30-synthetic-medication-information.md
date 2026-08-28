# Sprint 30: synthetic patient-reported medication information

Status: Approved for bounded implementation by [TH-DEC-0033](../decisions/0033-approved-sprint-30-synthetic-medication-information.md)  
Scope: Applicant-owned immutable patient-reported medication-information receipt after the coarse inventory, using only a server-owned local synthetic ingredient catalog and bounded use states; no dose/directions/free text, RxNorm claim, canonical medication statement/list, reconciliation, interaction check, clinician task, complete intake, eligibility, request, queue, care, prescribing, external integration, or production use

## 1. Outcome

Add the first bounded clinical-detail preparation checkpoint without converting patient-reported selections into canonical medication evidence. The applicant can select local synthetic ingredient names only when the prior inventory says items need review, identify whether additional/unlisted items exist, confirm limitations, and receive an informational next-step route. Clinical reconciliation and complete terminology-enabled medication collection remain later separately governed workflows.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP30-001` | Add a versioned server-owned local synthetic ingredient catalog whose opaque keys and displays are never represented as RxNorm or complete coverage. |
| `TH-SP30-002` | Add a server-owned medication-information snapshot bound to the complete inventory/device/applicant/patient provenance chain. |
| `TH-SP30-003` | Add applicant-key protected private/no-store retrieval returning only the prior medication inventory state, local catalog, source fingerprint/version, recorded bounded selections, and explicit limitations. |
| `TH-SP30-004` | Add one idempotent atomic command for unique bounded catalog selections, reported-use states, additional/unlisted signal, and four mandatory acknowledgments. |
| `TH-SP30-005` | Derive one informational route without creating a task, review queue, interaction check, clinical priority, medication statement, eligibility result, or operational authority. |
| `TH-SP30-006` | Add append-only parent/item/event provenance with deferred branch/count checks and zero canonical-medication or downstream consequence. |
| `TH-SP30-007` | Add an accessible patient panel with branch-specific instructions, checkboxes and bounded use-status controls, stable retry, keyboard/focus/reflow behavior, and no free text or browser persistence. |
| `TH-SP30-008` | Prove catalog/source/access/version/provenance isolation, branch rules, item uniqueness/bounds, route priority, partial failure, exact replay, changed replay, contention, append-only evidence, zero source/patient/canonical-medication/downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticClinicalInformationInventoryRecorded`; the successful promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access receipt, passing device-preparation receipt, clinical-information inventory, original passing safety evaluation, and verified callback source must still exist and agree; no canonical insurance or canonical medication row may exist for the promoted shell; and the stored practice/facility/applicant/patient relationships must remain unchanged.

## 4. Exit boundary

Sprint 30 ends at `SyntheticMedicationInformationRecorded`. The parent receipt and child items retain only server-owned local ingredient displays, `Taking`/`NotTaking`/`Unsure`, the additional/unlisted signal, and limitations. Dose, route, frequency, timing, indication, prescriber, pharmacy, date, note, coding, and reconciliation remain absent. It is not a MedicationStatement, medication list, interaction check, clinical intake completion, eligibility outcome, clinician review, practice acceptance, request/queue entry, care, or prescribing authority.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/catalog | Fixed versioned local synthetic catalog; unique server-ordered selections; exact three-value reported-use vocabulary; prior-inventory branch rules; deterministic route priority; four acknowledgments; deterministic snapshot; all clinical/downstream consequences false. |
| Database | Complete prior-chain mismatch, stale/expired/canonical-insurance/canonical-medication/portal-enabled rejection, deferred parent/item parity, exact replay with in-transaction revalidation, changed replay, concurrent one-receipt convergence, append-only evidence, and zero source/patient/canonical-medication/downstream delta. |
| HTTP | Applicant-only private/no-store read/write, required idempotency, typed bounded input/output, bounded failures, server-owned displays only, and exclusion of dose/directions/free text/contact/patient/insurance identifiers. |
| UI | Loading/error/retry, prior-state explanation, local catalog selections and use-status controls only when applicable, additional/unlisted signal, all acknowledgments, disabled submit, informational result, 320-pixel reflow, focus, and no browser persistence. |
| Standards boundary | Patient-source/status/medication separation aligned conceptually to MedicationStatement while proving zero FHIR resource, RxNorm/NDC/SNOMED CT mapping, canonical list, order, dosage, or interoperability payload. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
