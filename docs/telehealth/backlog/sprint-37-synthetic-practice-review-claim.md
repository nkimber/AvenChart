# Sprint 37: synthetic practice-review claim

Status: Approved for bounded implementation by [TH-DEC-0040](../decisions/0040-approved-sprint-37-synthetic-practice-review-claim.md)  
Scope: Short first-writer-wins administrator/front-desk review claim over one pending synthetic practice-review item; no priority, disposition, contact, request, care queue, appointment, encounter, financial, integration, external, or production consequence

## 1. Outcome

Allow one authorized practice operations staff member to claim an exact pending review item for 120 seconds, preventing hidden concurrent work. The claim is an immutable operational receipt and does not change the case or applicant state. It is not acceptance, decline, clinical review, or care authorization.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP37-001` | Add immutable practice-review claim receipts with database-clock leases, semantic idempotency, append-only history, and one-active-claim transactional enforcement. |
| `TH-SP37-002` | Revalidate the full Sprint 36 case/submission/readiness/promotion/patient-shell/purpose/safety/expiry/zero-consequence chain while holding the exact case lock. |
| `TH-SP37-003` | Add one private/no-store, PHI-audited POST endpoint restricted to administrator/front-desk healthcare-operations sessions with `patients.demo.write`. |
| `TH-SP37-004` | Extend the minimized inbox with active/mine/expiry state while never returning another staff identity, priority, SLA, or action beyond claim. |
| `TH-SP37-005` | Add an accessible explicit claim action with pending, conflict, retry, ambiguous-result recovery, lease-expiry refresh, reflow, keyboard operation, and no persistence. |
| `TH-SP37-006` | Prove stale/expired/drifted/cross-scope exclusion, exact replay, changed-key conflict, first-writer contention, expiry/reclaim, append-only evidence, zero downstream mutation, and full regression. |

## 3. Entry gate

The case remains unexpired `PendingPracticeReview`; the applicant remains `SyntheticPracticeReviewSubmitted`; the exact submission, readiness, promotion, copied portal-disabled unmerged patient shell, controlled purpose, passing safety result, and all false downstream flags agree inside the configured practice/facility. No active unexpired claim exists.

## 4. Exit boundary

Sprint 37 ends with one immutable short-lived claim receipt and active/mine/expiry projection. The applicant and case remain unchanged and pending. No priority, staff disposition, clinical review, acceptance/decline, contact, telehealth request, patient/clinician care queue, appointment, encounter, care, prescribing, financial, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Exact command allowlist, 120-second server lease, all decision/downstream capabilities false, and private claimant visibility. |
| Data | Additive migration, exact provenance revalidation under case lock, first-writer convergence, immutable history, exact replay, expiry/reclaim, and zero unrelated mutation. |
| HTTP | Admin/front-desk role, healthcare-operations context, demographics-write permission, idempotency, private/no-store, PHI audit, safe errors, and minimized response. |
| UI | Explicit claim, stable pending/retry/ambiguous-result recovery, active/mine/another state, no other claimant identity, no decision controls, reflow, focus/keyboard behavior, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, runtime/authorization/OpenAPI/live contention, planning validator, Graphify portable review, bootstrap, migration recovery, and exact cleanup. |
