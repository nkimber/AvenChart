# Sprint 40: applicant-bound synthetic request creation

Status: Approved for bounded implementation by [TH-DEC-0043](../decisions/0043-approved-sprint-40-applicant-bound-request-creation.md)
Scope: One access-key-owned, authorization-gated `Draft` request shell; no contact, queue, doctor search, appointment, encounter, consent, care, financial, integration, external, or production consequence

## 1. Outcome

Allow the synthetic prospective applicant to explicitly create one telehealth request after practice authorization. The transaction preserves exact source provenance and stops at `Draft`, before any care workflow.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP40-001` | Add an additive migration for immutable applicant/request provenance, one request-creation receipt, applicant/request events, database guards, append-only enforcement, and exact no-downstream flags. |
| `TH-SP40-002` | Add an access-key-bound repository transaction that revalidates the full authorized chain, derives the controlled complaint category, creates one Draft request, advances the applicant once, and supports semantic replay. |
| `TH-SP40-003` | Add private/no-store applicant GET/POST endpoints with safe errors, applicant request correlation, and an opaque request receipt. |
| `TH-SP40-004` | Add an accessible applicant-owned request form with explicit limitations, three confirmations, stable retry, success projection, keyboard behavior, and no new browser persistence. |
| `TH-SP40-005` | Prove exact success/replay/contention, changed-key/stale/expired/foreign/drift denial, immutable evidence, unchanged source/patient/downstream fingerprints, request correlation, migration recovery, and full regression. |

## 3. Entry gate

The applicant remains unexpired and `SyntheticPracticeReviewAuthorized`; the positive authorization, case, claim, submission, readiness, promotion, portal-disabled patient shell, source receipts, controlled purpose, passing safety outcome, and zero-downstream evidence agree; and the command carries the applicant access key, current version, authorization policy version 1, and all confirmations.

## 4. Exit boundary

Sprint 40 ends with applicant state `SyntheticRequestCreated`, one source-linked `Draft` telehealth request, one immutable creation receipt, and one event on each aggregate. The patient shell and all prior receipts remain unchanged. No patient or clinician care queue, queue position, doctor search, appointment, encounter, consent, media, care, prescribing, financial, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Controlled version and three confirmations; server-derived category; every prohibited downstream capability false. |
| Data | Full provenance under locks, database-clock expiry, one applicant transition/request/receipt/events, exact replay/contention, immutable provenance, and no downstream mutation. |
| HTTP | Access-key ownership, configured host/practice/facility, private/no-store, safe errors, opaque request correlation, and idempotency. |
| UI | Authorized-state placement, explicit Draft boundary, disabled-until-confirmed action, stable retry, success projection, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL, migration/recovery, runtime/authorization/OpenAPI, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |
