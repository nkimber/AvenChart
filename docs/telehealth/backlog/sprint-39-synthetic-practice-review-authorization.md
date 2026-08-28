# Sprint 39: synthetic practice-review authorization

Status: Approved for bounded implementation by [TH-DEC-0042](../decisions/0042-approved-sprint-39-synthetic-practice-review-authorization.md)  
Scope: One positive-only, current-claimant, immutable operational authorization for a separately gated future synthetic request-creation step; no contact, request, queue, appointment, encounter, consent, care, financial, integration, external, or production consequence

## 1. Outcome

Allow the authorized staff member who owns the active short claim and reviewed the minimized packet to record `AuthorizedForSyntheticRequestCreation`. The decision concludes the pending inbox item but deliberately stops before creating the request or any care workflow.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP39-001` | Add an additive migration for one immutable authorization receipt, the applicant status/event transition, database guards, append-only enforcement, and exact no-downstream flags. |
| `TH-SP39-002` | Add a current-claimant repository transaction that revalidates the full packet provenance, advances the applicant once, records the authorization/event atomically, and supports actor-bound semantic replay. |
| `TH-SP39-003` | Add a private/no-store, PHI-audited POST endpoint restricted to administrator/front-desk healthcare-operations sessions with `patients.demo.write`. |
| `TH-SP39-004` | Add an accessible positive-only authorization form inside the open packet with controlled rationale, three acknowledgments, stable retry, success refresh, keyboard behavior, and no persistence. |
| `TH-SP39-005` | Prove exact success/replay/contention, changed-key/stale/expired/foreign/drift denial, immutable evidence, unchanged case/claim/downstream fingerprints, audit, migration recovery, and full regression. |

## 3. Entry gate

The case remains the exact unexpired pending case; the applicant remains `SyntheticPracticeReviewSubmitted`; all submission, readiness, promotion, patient-shell, source-receipt, controlled-purpose, passing-safety and zero-downstream evidence agrees; the current actor owns the active unexpired claim; and the client acknowledges Sprint 38 packet policy version 1.

## 4. Exit boundary

Sprint 39 ends with applicant state `SyntheticPracticeReviewAuthorized`, one immutable authorization, and one aggregate event. The submitted case and claim receipts remain unchanged. No practice acceptance, patient contact, clinical review, telehealth request, patient/clinician care queue, queue position, appointment, encounter, consent, care, prescribing, financial, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Positive-only controlled decision/rationale, three acknowledgments, packet-version binding, and every downstream capability false. |
| Data | Full provenance under locks, active claimant and database-clock expiry, one version transition, immutable receipt/event, exact replay/contention, and no case/claim/downstream mutation. |
| HTTP | Admin/front-desk role, healthcare-operations context, demographics-write permission, private/no-store, PHI audit, safe errors, opaque case correlation, and idempotency. |
| UI | Current claimant/open-packet placement, explicit limitations, disabled-until-acknowledged action, stable retry, success refresh, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL, migration/recovery, runtime/authorization/OpenAPI, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |
