# Sprint 36: read-only practice-review inbox

Status: Approved for bounded implementation by [TH-DEC-0039](../decisions/0039-approved-sprint-36-read-only-practice-review-inbox.md)  
Scope: Administrator/front-desk read-only visibility of pending synthetic practice-review work items; no decision, assignment, priority, contact, telehealth request, care queue, appointment, encounter, financial, integration, external, or production consequence

## 1. Outcome

Give authorized practice operations staff a private, minimized, accessible inbox of the exact pending work items created in Sprint 35. The inbox supports awareness and later governed review design only. It contains no action control and does not represent these cases as telehealth requests or care-queue entries.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP36-001` | Add a practice/facility-scoped read model over exact pending case/submission/readiness/promotion/purpose/safety provenance. |
| `TH-SP36-002` | Minimize each item to an opaque case ID, bounded identity/contact region, controlled purpose and safety outcome, coarse readiness sections, route, time, and false-capability flags. |
| `TH-SP36-003` | Add one GET-only administrator/front-desk endpoint with healthcare-operations context, demographics-view permission, private/no-store headers, and opaque PHI audit context. |
| `TH-SP36-004` | Add an independent accessible admin panel with loading, empty, failure/retry, manual and periodic refresh, stable item identity, explicit no-action language, reflow, and no browser persistence. |
| `TH-SP36-005` | Prove role/purpose/permission/facility isolation, contract minimization, ordering/limit, exact provenance, read-only zero mutation, PHI audit evidence, and full regression. |

## 3. Entry gate

The case must remain `PendingPracticeReview`; the applicant must remain `SyntheticPracticeReviewSubmitted`; and its exact case, submission, readiness, promotion, portal-disabled unmerged patient shell, controlled visit purpose, and passing safety screen must agree within the configured practice/facility. Any mismatch excludes the item and creates no disclosure.

## 4. Exit boundary

Sprint 36 ends at staff read-only awareness. No durable telehealth aggregate changes state. The case remains pending and unassigned, with no priority or promise. No staff decision, clinical review, acceptance/decline, contact, telehealth request, patient/clinician care queue, appointment, encounter, care, prescribing, financial, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact route/status/purpose/safety/section allowlists, maximum 100, and every action/downstream capability false. |
| Data | Exact provenance joins, deterministic order, cross-practice/facility exclusion, no protected detail, and repeated-read zero product-table mutation. |
| HTTP | Admin/front-desk role, healthcare-operations context, demographics-view permission, private/no-store, PHI audit, GET-only contract, and forbidden field absence. |
| UI | Independent load/error/retry/empty state, refresh without stale actions, no write controls, stable items, explicit limitations, 320-pixel reflow, focus/keyboard behavior, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, runtime/authorization/OpenAPI/live safety, planning validator, Graphify portable review, bootstrap, and exact cleanup. |
