# Sprint 38: claimant-bound practice-review packet

Status: Approved for bounded implementation by [TH-DEC-0041](../decisions/0041-approved-sprint-38-claimant-bound-practice-review-packet.md)  
Scope: Private current-claimant read of one minimized synthetic operational review packet; no lease mutation, disposition, clinical review, contact, request, care queue, appointment, encounter, financial, integration, external, or production consequence

## 1. Outcome

Allow the authorized staff member holding an active Sprint 37 claim to open the exact case's minimized operational evidence packet. The workspace makes masked, attributable preparation possible before any later decision while preserving source-detail, clinical-detail, chart, claimant, and downstream boundaries.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP38-001` | Add a claimant-bound packet policy and repository query that revalidates the complete case/claim/submission/readiness/promotion/patient-shell/source-receipt/purpose/safety/expiry/zero-consequence chain. |
| `TH-SP38-002` | Return only the approved masked registration, insurance/network, communication/access, coarse device, visit-purpose, section-route, claim-expiry, and all-false consequence projection. |
| `TH-SP38-003` | Add one private/no-store, PHI-audited GET endpoint restricted to administrator/front-desk healthcare-operations sessions with `patients.demo.view`. |
| `TH-SP38-004` | Add an accessible claimant-only review workspace with loading, retry, expiry recovery, adjacent synthetic limitations, focus/keyboard behavior, 320-pixel reflow, and no persistence. |
| `TH-SP38-005` | Prove owner/non-owner/expired/stale/drifted/cross-scope behavior, exact minimization, unchanged claim expiry and product fingerprint, access audit, and full regression. |

## 3. Entry gate

The case remains unexpired `PendingPracticeReview`; the applicant remains `SyntheticPracticeReviewSubmitted`; the exact submission, readiness, promotion, copied portal-disabled unmerged patient shell, controlled purpose, passing safety result, and source receipts agree inside the configured practice/facility; all forbidden downstream state remains absent; and one active unexpired Sprint 37 claim belongs to the current actor.

## 4. Exit boundary

Sprint 38 ends with a private read-only operational packet and attributable access audit. The claim expiry, applicant, case, patient, and every product aggregate remain unchanged. No priority, staff disposition, clinical review, acceptance/decline, contact, telehealth request, patient/clinician care queue, appointment, encounter, care, prescribing, financial, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact field allowlist, source/clinical-detail denylist, current-claimant requirement, all action/downstream capabilities false, and no lease extension. |
| Data | Complete provenance revalidation, database-clock claim ownership/expiry, fail-closed drift handling, no new domain table, and zero product mutation. |
| HTTP | Admin/front-desk role, healthcare-operations context, demographics-view permission, private/no-store, PHI audit, safe errors, and opaque case correlation. |
| UI | Claimant-only open/close/retry/expiry behavior, synthetic limitations, no decision or chart controls, reflow, focus/keyboard behavior, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, runtime/authorization/OpenAPI/live minimization, planning validator, Graphify portable review, bootstrap, migration recovery, and exact cleanup. |
