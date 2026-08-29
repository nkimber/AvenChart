# Sprint 42: applicant request universal safety assessment

Status: Approved for bounded implementation by [TH-DEC-0045](../decisions/0045-approved-sprint-42-applicant-request-universal-safety-assessment.md)
Scope: One applicant-owned, source-bound universal safety assessment from `LocationConfirmed` version 2 to an exact version 3 safety state; no complaint-specific eligibility, clinical-review work item, contact, queue, appointment, encounter, consent, care, financial, integration, external, or production consequence

## 1. Outcome

Re-screen the synthetic request with the existing deterministic four-answer universal safety fixture after request-time location confirmation. Preserve protective outcomes and stop conditions while distinguishing a universal-screen pass from complaint-specific clinical eligibility.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP42-001` | Add an additive migration for request safety states, one immutable reproducible applicant request-safety receipt, exact assessment/source guards, append-only enforcement, and no-downstream flags. |
| `TH-SP42-002` | Add an access-key-bound repository transaction that revalidates the complete request/source chain, current location/callback freshness, protocol fixture, outcome priority, replay/contention, and exact request version 2 to 3 transition. |
| `TH-SP42-003` | Add private/no-store applicant GET/POST endpoints with an opaque context snapshot, nullable explicit answers, safe state-change handling, applicant request correlation, and idempotency. |
| `TH-SP42-004` | Add an accessible emergency-first request safety form with no defaults, immediate 911 action, stable retry, outcome-specific result projection, focus recovery, and no browser persistence. |
| `TH-SP42-005` | Prove five ordered outcomes, exact success/replay/contention, changed-key/stale/expired/foreign/state-mismatch/source/fixture drift denial, immutable evidence, unchanged applicant/patient/source fingerprints, zero downstream action, migration recovery, and full regression. |

## 3. Entry gate

The applicant remains unexpired and `SyntheticRequestCreated` version 26; the request-creation and request-location receipts agree; the live request is `LocationConfirmed` version 2; one fresh supported-state location row exists; the callback route still matches; the prior prospective universal safety fixture passed; the canonical patient remains portal-disabled and unmerged; and every triage, clinical-review work item, contact, queue, appointment, encounter, consent, care, financial, integration, and external consequence remains absent.

## 4. Outcome boundary

| Deterministic outcome | Request state at version 3 | Meaning in this slice |
|---|---|---|
| `Emergency` | `EmergencyRedirected` | Terminal for this request; show direct 911/ED direction without claiming dispatch. |
| `UrgentInPerson` or `InPersonRequired` | `InPersonRecommended` | Terminal for this request; show prompt/in-person direction. |
| `ClinicalReview` | `ClinicalReview` | A qualified clinical review is required, but no review work item or queue exists yet. |
| `TelehealthEligible` | `SafetyScreening` | Universal screen passed only; complaint-specific triage is still required. |

The applicant remains unchanged. Exactly one generic assessment, one applicant assessment receipt, and one request event are appended. No contact, doctor search, patient or clinician care queue, queue position, appointment, encounter, consent, media, care, prescribing, financial, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Missing-answer denial, snapshot validation, five priority outcomes, exact request-state mapping, and explicit pass limitations. |
| Data | Full provenance under locks, database-clock expiry, exact protocol validation, one request transition/assessment/receipt/event, replay/contention, immutable evidence, and no downstream mutation. |
| HTTP | Access-key ownership, configured host/practice/facility, private/no-store, safe errors, minimized outcome, request correlation, and idempotency. |
| UI | No default answers, emergency action before submission, changed-context stop guidance, stable retry, result recovery, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL plus all five outcomes, migration/recovery, runtime/authorization/OpenAPI, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |
