# Sprint 35: synthetic practice-review submission

Status: Approved for bounded implementation by [TH-DEC-0038](../decisions/0038-approved-sprint-35-synthetic-practice-review-submission.md)  
Scope: Applicant-owned immutable submission of one synthetic practice-intake review work item after the pre-request readiness acknowledgment; no practice decision, telehealth request, patient/clinician queue, appointment, encounter, care, financial, integration, external, or production consequence

## 1. Outcome

Let a promoted synthetic applicant send the previously acknowledged, server-derived pre-request receipt bundle to the branded practice for operational review. The system creates exactly one `PendingPracticeReview` work item and gives the applicant an honest status. It does not call this a telehealth request, show a queue position, claim that a doctor is being searched, or authorize clinical or financial action.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP35-001` | Build a server snapshot over the exact Decision 0037 readiness acknowledgment, its route, the promoted portal-disabled patient shell, and the still-valid underlying receipt chain. |
| `TH-SP35-002` | Add applicant-key protected private/no-store retrieval returning only the review route, policy/version, limitations, acknowledgments, and server snapshot fingerprint. |
| `TH-SP35-003` | Add one idempotent atomic submission with four mandatory patient-reported/review/no-request-or-care-queue/worsening-symptom acknowledgments. |
| `TH-SP35-004` | Create one immutable practice/facility-scoped `PendingPracticeReview` work item with no priority, assignee, acceptance, SLA promise, doctor identity, or queue position. |
| `TH-SP35-005` | Add append-only submission receipt/event provenance with `staffReviewCreated=true` and every clinical, patient, request, care-queue, financial, integration, and external consequence false. |
| `TH-SP35-006` | Add an accessible applicant panel with stable retry, explicit submission boundary, result focus, calm pending-review status, 320-pixel reflow, and no browser persistence. |
| `TH-SP35-007` | Prove access/version/provenance isolation, server-owned routing, minimization, replay/contention, append-only behavior, one-work-item cardinality, zero forbidden deltas, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticPreRequestReadinessAcknowledged`; the successful promotion, portal-disabled unmerged patient shell, readiness receipt and every source receipt must still exist and agree; the readiness receipt's stored route and fingerprint are authoritative; no canonical insurance, medication, prescription, allergy, or problem row may exist for the promoted shell; and practice/facility/applicant/patient relationships must remain unchanged.

## 4. Exit boundary

Sprint 35 ends at `SyntheticPracticeReviewSubmitted`. Exactly one practice-review case, one submission receipt and one applicant event exist. `staffReviewCreated=true`; no staff action, clinician review, practice acceptance, patient mutation, telehealth request, patient/clinician queue entry, queue position, appointment, encounter, care, prescribing, billing, claim, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact four acknowledgments; deterministic source fingerprint; server-owned review route; one pending-review state; every consequence except staff-review creation false. |
| Database | Complete source mismatch, stale/expired/canonical-data/portal-enabled rejection, exact replay with in-transaction revalidation, changed replay, concurrent convergence, append-only case/receipt/event, and zero forbidden delta. |
| HTTP | Applicant-only private/no-store read/write, required idempotency, typed bounded input/output, and exclusion of source values, identity/contact/payer/clinical/device details and free text. |
| UI | Loading/error/retry, explicit practice-review explanation, all acknowledgments, disabled submit, calm pending-review result, 320-pixel reflow, focus, and no browser persistence. |
| Standards boundary | Explicitly no FHIR, US Core, USCDI, X12, eligibility request, clinical task, service request, appointment, claim, or interoperability payload. |
| Regression | Backend/frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
