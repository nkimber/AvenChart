# Sprint 41: applicant request location and callback confirmation

Status: Approved for bounded implementation by [TH-DEC-0044](../decisions/0044-approved-sprint-41-applicant-request-location-confirmation.md)
Scope: One applicant-owned, source-bound `Draft` to `LocationConfirmed` transition; no triage result, clinical review, contact, queue, doctor search, appointment, encounter, consent, care, financial, integration, external, or production consequence

## 1. Outcome

Allow the synthetic applicant who created the Sprint 40 request to explicitly reconfirm the request-time supported current-location state and masked callback route. The transaction binds immutable prior communication evidence to the request and stops before triage evaluation.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP41-001` | Add an additive migration for one immutable applicant request-location receipt, exact source/request guards, append-only enforcement, and no-downstream flags. |
| `TH-SP41-002` | Add an applicant-access-key-bound repository transaction that revalidates the complete request/source chain, requires the selected state to match the prior current-location evidence, inserts one location row, and advances only the request to `LocationConfirmed` version 2. |
| `TH-SP41-003` | Add private/no-store applicant GET/POST endpoints with a masked callback, opaque context fingerprint, safe state-change handling, applicant request correlation, and idempotency. |
| `TH-SP41-004` | Add an accessible location/callback confirmation form with explicit changed-location stop guidance, four confirmations, stable retry, success projection, keyboard behavior, and no new browser persistence. |
| `TH-SP41-005` | Prove exact success/replay/contention, changed-key/stale/expired/foreign/state-mismatch/drift denial, immutable evidence, unchanged applicant/patient/source fingerprints, zero downstream action, migration recovery, and full regression. |

## 3. Entry gate

The applicant remains unexpired and `SyntheticRequestCreated`; the request-creation receipt and request provenance agree; the live request remains `Draft` version 1; the canonical patient remains portal-disabled and unmerged; the prior communication-readiness receipt still records a confirmed supported state and callback last four; and every triage, contact, queue, appointment, encounter, consent, care, financial, integration, and external consequence remains absent.

## 4. Exit boundary

Sprint 41 ends with the applicant unchanged, the request at `LocationConfirmed` version 2, one append-only patient-location row, one immutable applicant request-location receipt, and one request event. No triage assessment or outcome, clinical review, patient contact, patient or clinician care queue, queue position, doctor search, appointment, encounter, consent, media, care, prescribing, financial, integration, or external action exists.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy | Snapshot fingerprint, supported exact-match state, and four confirmations; no raw callback or clinical input. |
| Data | Full provenance under locks, database-clock expiry, one request transition/location/receipt/event, replay/contention, immutable evidence, and no downstream mutation. |
| HTTP | Access-key ownership, configured host/practice/facility, private/no-store, safe errors, masked callback, request correlation, and idempotency. |
| UI | Server-authoritative state/callback display, changed-location stop guidance, disabled-until-confirmed action, stable retry, result recovery, reflow, keyboard/focus, and no persistence. |
| Regression | Backend/frontend/lint/build/E2E, live GA/CA/FL, migration/recovery, runtime/authorization/OpenAPI, planning validator, Graphify portability/review, bootstrap, and exact cleanup. |
