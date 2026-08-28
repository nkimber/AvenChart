# Sprint 33: synthetic clinical-information summary confirmation

Status: Approved for bounded implementation by [TH-DEC-0036](../decisions/0036-approved-sprint-33-synthetic-clinical-information-summary-confirmation.md)  
Scope: Applicant-owned immutable no-edit confirmation of a server-derived summary over the prior coarse medication, allergy/intolerance, and health-history receipts; no new clinical details, canonical record, reconciliation, confirmed negative, intake completion, eligibility, task, acceptance, request, queue, care, prescribing, external integration, or production use

## 1. Outcome

Give the applicant one compact review checkpoint after all three bounded clinical-information branches. The server summarizes only prior category states, selected-item counts, additional/unlisted signals, and informational routes. Confirmation preserves patient-source provenance while explicitly leaving collection, correction, clinician verification/reconciliation, intake completion, and all operational gates unresolved.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP33-001` | Add a server-owned three-category summary snapshot bound to the exact inventory, medication, allergy, health-history, patient-shell, and upstream provenance chain. |
| `TH-SP33-002` | Add applicant-key protected private/no-store retrieval returning only stable category keys, prior coarse states, counts, additional signals, routes, policy/version, and limitations; friendly labels remain client-owned presentation text. |
| `TH-SP33-003` | Add one idempotent atomic no-edit confirmation with four mandatory patient-source/non-reconciliation/non-completion/correction acknowledgments. |
| `TH-SP33-004` | Derive one informational summary route without creating a task, priority, assessment, clinical decision, review queue, or operational authority. |
| `TH-SP33-005` | Add append-only receipt/event provenance and zero source, canonical-clinical, patient, request, queue, or downstream consequence. |
| `TH-SP33-006` | Add an accessible applicant summary panel with stable retry, keyboard/focus/reflow behavior, clear counts and routes, correction-stop direction, and no browser persistence. |
| `TH-SP33-007` | Prove source/access/version/provenance isolation, route priority, server-owned counts, minimization, exact replay, changed replay, contention, append-only evidence, zero-delta behavior, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticHealthHistoryInformationRecorded`; the successful promotion, portal-disabled unmerged patient shell, clinical-information inventory, medication-information parent/exact children, allergy-information parent/exact children, health-history-information parent/exact children, and retained upstream identifiers must still exist and agree; no canonical insurance, medication, prescription, allergy, or problem row may exist for the promoted shell; and stored practice/facility/applicant/patient relationships must remain unchanged.

## 4. Exit boundary

Sprint 33 ends at `SyntheticClinicalInformationSummaryConfirmed`. The receipt retains only exact source IDs/fingerprints, server-derived coarse summary values, acknowledgments, and one informational route. It is not a QuestionnaireResponse, clinical form, canonical chart summary, confirmed medication/allergy/history absence, reconciliation, completed intake, eligibility outcome, clinician review, practice acceptance, request/queue entry, or care/prescribing authority.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact four acknowledgments; deterministic source fingerprint; server-owned category keys/counts/signals/routes; route priority; every clinical/downstream consequence false. |
| Database | Complete prior-chain mismatch, stale/expired/canonical-data/portal-enabled rejection, source-count parity, exact replay with in-transaction revalidation, changed replay, concurrent one-receipt convergence, append-only evidence, and zero source/patient/canonical/downstream delta. |
| HTTP | Applicant-only private/no-store read/write, required idempotency, typed bounded input/output, minimization, and exclusion of clinical details, identifiers, contacts, payer fields, names, and free text. |
| UI | Loading/error/retry, three-category review, correction-stop direction, all acknowledgments, disabled submit, informational result, 320-pixel reflow, focus, and no browser persistence. |
| Standards boundary | Patient source and immutable provenance aligned only as prevention boundaries while proving zero FHIR QuestionnaireResponse/Provenance resource, US Core/USCDI conformance claim, canonical list, reconciliation, or interoperability payload. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
