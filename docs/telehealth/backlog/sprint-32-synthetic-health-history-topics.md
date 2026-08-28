# Sprint 32: synthetic patient-reported health-history topics

Status: Approved for bounded implementation by [TH-DEC-0035](../decisions/0035-approved-sprint-32-synthetic-health-history-topics.md)  
Scope: Applicant-owned immutable patient-reported health-history-topic receipt after allergy information, using only a server-owned local synthetic topic catalog; no diagnosis/finding/status/timing/detail/free text, external terminology claim, canonical problem or clinical record, confirmed negation, reconciliation, assessment, risk-modifier evaluation, triage change, clinician task, complete intake, eligibility, request, queue, care, prescribing, external integration, or production use

## 1. Outcome

Resolve the remaining `otherHealthHistoryStatus` inventory branch without creating clinical history. The applicant can select broad local synthetic topics for later review only when the prior inventory says items need review, identify whether additional/unlisted topics exist, confirm limitations, and receive an informational next-step route. Real history, diagnoses, procedures, health-status assertions, assessment instruments, family-history findings, clinician verification/reconciliation, risk evaluation, and standards-based resources remain later separately governed workflows.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP32-001` | Add a versioned server-owned local six-topic synthetic catalog whose opaque keys, displays, and broad local categories are never represented as clinical findings, externally coded, or complete coverage. |
| `TH-SP32-002` | Add a server-owned health-history-information snapshot bound to the complete inventory, medication and allergy receipts/items, device, applicant, and patient provenance chain. |
| `TH-SP32-003` | Add applicant-key protected private/no-store retrieval returning only the prior other-history inventory state, local catalog, source fingerprint/version, recorded bounded topics, and explicit limitations. |
| `TH-SP32-004` | Add one idempotent atomic command for unique bounded topic selections, an additional/unlisted signal, and four mandatory acknowledgments. |
| `TH-SP32-005` | Derive one informational route without creating a task, review queue, priority, assessment, risk score, triage change, problem, eligibility result, or operational authority. |
| `TH-SP32-006` | Add append-only parent/topic/event provenance with deferred branch/count checks and zero canonical-clinical or downstream consequence. |
| `TH-SP32-007` | Add an accessible patient panel with branch-specific instructions, bounded checkboxes, stable retry, keyboard/focus/reflow behavior, sensitive-topic explanation, and no free text or browser persistence. |
| `TH-SP32-008` | Prove catalog/source/access/version/provenance isolation, branch rules, topic uniqueness/bounds, route priority, partial failure, exact replay, changed replay, contention, append-only evidence, zero source/patient/canonical-problem/downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticAllergyInformationRecorded`; the successful promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access receipt, passing device-preparation receipt, clinical-information inventory, medication-information parent/exact children, allergy-information parent/exact children, original passing safety evaluation, and verified callback source must still exist and agree; no canonical insurance, medication, allergy, or problem row may exist for the promoted shell; and stored practice/facility/applicant/patient relationships must remain unchanged.

## 4. Exit boundary

Sprint 32 ends at `SyntheticHealthHistoryInformationRecorded`. The parent receipt and child topics retain only server-owned local topic displays/categories, the additional/unlisted signal, and limitations. Diagnosis, clinical/verification status, severity, onset/abatement, procedure details, pregnancy status, behavioral-health/substance-use assessment, family relationship/finding, date, note, coding, confirmed negation, risk evaluation, and reconciliation remain absent. It is not a Condition, Procedure, Observation, FamilyMemberHistory, QuestionnaireResponse, problem list, health assessment, completed intake, eligibility outcome, clinician review, practice acceptance, request/queue entry, care, or prescribing authority.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/catalog | Fixed versioned six-topic local synthetic catalog; exact local category values; unique server-ordered selections; prior-inventory branch rules; deterministic route priority; four acknowledgments; deterministic snapshot; every clinical/downstream consequence false. |
| Database | Complete prior-chain mismatch, stale/expired/canonical-insurance/medication/allergy/problem/portal-enabled rejection, deferred parent/topic parity, exact replay with in-transaction revalidation, changed replay, concurrent one-receipt convergence, append-only evidence, and zero source/patient/canonical-problem/downstream delta. |
| HTTP | Applicant-only private/no-store read/write, required idempotency, typed bounded input/output, bounded failures, server-owned displays/categories only, and exclusion of diagnosis/status/timing/detail/free text/contact/patient/insurance identifiers. |
| UI | Loading/error/retry, prior-state explanation, local topics only when applicable, additional/unlisted signal, all acknowledgments, disabled submit, informational result, 320-pixel reflow, focus, and no browser persistence. |
| Standards boundary | Patient-source/topic separation aligned only as a prevention boundary against Condition and assessment semantics while proving zero FHIR resource, US Core/USCDI conformance claim, SNOMED CT/ICD-10-CM/LOINC mapping, clinical/verification status, timing, assessment, reconciliation, or interoperability payload. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
