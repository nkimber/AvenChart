# Sprint 31: synthetic patient-reported allergy/intolerance information

Status: Approved for bounded implementation by [TH-DEC-0034](../decisions/0034-approved-sprint-31-synthetic-allergy-information.md)  
Scope: Applicant-owned immutable patient-reported allergy/intolerance-information receipt after medication information, using only a server-owned local synthetic substance catalog; no reaction/severity/criticality/type/status/free text, external terminology claim, canonical allergy record, confirmed negation, reconciliation, contraindication check, alert, clinician task, complete intake, eligibility, request, queue, care, prescribing, external integration, or production use

## 1. Outcome

Add the next bounded clinical-detail preparation checkpoint without converting patient-reported selections into canonical allergy/intolerance evidence. The applicant can select local synthetic substance names only when the prior inventory says items need review, identify whether additional/unlisted substances exist, confirm limitations, and receive an informational next-step route. Reaction collection, clinical verification/reconciliation, negation semantics, terminology-enabled allergy capture, and safety checking remain later separately governed workflows.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP31-001` | Add a versioned server-owned local synthetic substance catalog whose opaque keys, displays, and local categories are never represented as externally coded or complete coverage. |
| `TH-SP31-002` | Add a server-owned allergy-information snapshot bound to the complete inventory, medication receipt/items, device, applicant, and patient provenance chain. |
| `TH-SP31-003` | Add applicant-key protected private/no-store retrieval returning only the prior allergy/intolerance inventory state, local catalog, source fingerprint/version, recorded bounded selections, and explicit limitations. |
| `TH-SP31-004` | Add one idempotent atomic command for unique bounded catalog selections, an additional/unlisted signal, and four mandatory acknowledgments. |
| `TH-SP31-005` | Derive one informational route without creating a task, review queue, alert, contraindication check, clinical priority, allergy record, eligibility result, or operational authority. |
| `TH-SP31-006` | Add append-only parent/item/event provenance with deferred branch/count checks and zero canonical-allergy or downstream consequence. |
| `TH-SP31-007` | Add an accessible patient panel with branch-specific instructions, bounded checkboxes, stable retry, keyboard/focus/reflow behavior, and no free text or browser persistence. |
| `TH-SP31-008` | Prove catalog/source/access/version/provenance isolation, branch rules, item uniqueness/bounds, route priority, partial failure, exact replay, changed replay, contention, append-only evidence, zero source/patient/canonical-allergy/downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticMedicationInformationRecorded`; the successful promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access receipt, passing device-preparation receipt, clinical-information inventory, medication-information parent and exact child set, original passing safety evaluation, and verified callback source must still exist and agree; no canonical insurance, canonical medication, or canonical allergy row may exist for the promoted shell; and the stored practice/facility/applicant/patient relationships must remain unchanged.

## 4. Exit boundary

Sprint 31 ends at `SyntheticAllergyInformationRecorded`. The parent receipt and child items retain only server-owned local substance displays/categories, the additional/unlisted signal, and limitations. Reaction, manifestation, type, clinical status, verification status, severity, criticality, onset, date, note, coding, confirmed negation, and reconciliation remain absent. It is not an AllergyIntolerance resource, allergy list, alert, contraindication check, clinical intake completion, eligibility outcome, clinician review, practice acceptance, request/queue entry, care, or prescribing authority.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/catalog | Fixed versioned six-item local synthetic catalog; exact category values; unique server-ordered selections; prior-inventory branch rules; deterministic route priority; four acknowledgments; deterministic snapshot; all clinical/downstream consequences false. |
| Database | Complete prior-chain mismatch, stale/expired/canonical-insurance/canonical-medication/canonical-allergy/portal-enabled rejection, deferred parent/item parity, exact replay with in-transaction revalidation, changed replay, concurrent one-receipt convergence, append-only evidence, and zero source/patient/canonical-allergy/downstream delta. |
| HTTP | Applicant-only private/no-store read/write, required idempotency, typed bounded input/output, bounded failures, server-owned displays/categories only, and exclusion of reaction/severity/criticality/type/status/free text/contact/patient/insurance identifiers. |
| UI | Loading/error/retry, prior-state explanation, local catalog selections only when applicable, additional/unlisted signal, all acknowledgments, disabled submit, informational result, 320-pixel reflow, focus, and no browser persistence. |
| Standards boundary | Patient-source/substance/category separation aligned conceptually to AllergyIntolerance while proving zero FHIR resource, confirmed negation, SNOMED CT/RxNorm/NDC/UNII mapping, clinical/verification status, type, reaction, severity, criticality, reconciliation, alert, or interoperability payload. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
