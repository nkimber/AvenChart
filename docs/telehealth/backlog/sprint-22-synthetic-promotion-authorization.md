# Sprint 22: synthetic promotion authorization

Status: Approved for bounded implementation by [TH-DEC-0025](../decisions/0025-approved-sprint-22-synthetic-promotion-authorization.md)  
Scope: Staff-governed authorization or denial after complete unexpired synthetic process evidence; no canonical patient, chart, account, intake completion, consent, acceptance, request, queue, care, downstream action, external integration, or production use

## 1. Outcome

Add the human governance checkpoint that must precede any future synthetic patient-promotion exercise. Show authorized staff a minimized, normalized view of the complete applicant evidence chain; require an explicit no-assurance acknowledgment; append an immutable decision; and stop with the applicant still prospective and every canonical/downstream consequence false.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP22-001` | Add append-only promotion-authorization decisions and constrained `SyntheticIdentityProofingRecorded -> SyntheticPromotionAuthorized/SyntheticPromotionDenied` events with complete upstream provenance and hard-false consequences. |
| `TH-SP22-002` | Add staff-only, permission-filtered, private/no-store list and idempotent decision endpoints with configured practice/facility scoping, active staff binding, normalized validation, and no client-authored evidence. |
| `TH-SP22-003` | Return a minimized review projection containing synthetic applicant identity/masked contact plus normalized plan, eligibility, network, process, assurance, freshness, and server-derived decision options; exclude raw member/proofing/vendor evidence and canonical identifiers. |
| `TH-SP22-004` | Extend telehealth administration with an accessible independent promotion-review section, explicit synthetic/no-assurance acknowledgments, authorize/deny choice, reason, stable ambiguous retry, polling, and recovery. |
| `TH-SP22-005` | Keep identity proofing, patient/chart/account, intake, consent, acceptance, financial, request/queue, clinical, prescribing, billing/claim, communication, integration, and external consequences false. |
| `TH-SP22-006` | Prove authorization, provenance, freshness, replay-before-mutation, changed-key rejection, contention, append-only history, response minimization, zero canonical/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. Entry gate

The staff queue requires the exact same configured practice/facility applicant and complete immutable chain through a current `SyntheticIdentityProofingRecorded` result. The applicant and process result must be unexpired. Upstream eligibility must remain `Active` with `Reported` benefits; practice network must remain `PracticeInNetworkAcceptingNewPatients`; the proofing fixture must remain `SyntheticProofingPassed` with assurance `None`, `identityProofed=false`, and every real-evidence/source/biometric/authenticator flag false.

## 4. Exit boundary

Sprint 22 ends at a human-authored synthetic authorization or denial record and a prospective applicant status. Actual synthetic promotion, deterministic matching policy, canonical patient transaction, portal identity, authenticator, consent, practice acceptance, financial construction, request creation, and all clinical/downstream operations remain unavailable and separately gated.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/service | Role, facility, command, reason, acknowledgment, decision/status, and fingerprint tests. |
| Database | Fresh complete-chain success; denial; exact replay; changed replay; stale version; expiration; cross-chain rejection; concurrent one-winner; append-only; zero downstream delta. |
| HTTP | Permission metadata, no-store/audit context, OpenAPI request/response, stable error vocabulary, and minimized payload. |
| UI | Empty/load/error, independent refresh, both decisions, disabled submission, screen-reader labels, polling cleanup, and unchanged ambiguous retry. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
