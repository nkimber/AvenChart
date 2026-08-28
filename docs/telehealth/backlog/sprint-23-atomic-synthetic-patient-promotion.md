# Sprint 23: atomic synthetic patient promotion

Status: Approved for bounded implementation by [TH-DEC-0026](../decisions/0026-approved-sprint-23-atomic-synthetic-patient-promotion.md)  
Scope: Administrator-executed, duplicate-rechecked atomic creation of one disabled synthetic canonical patient shell after explicit promotion authorization; no existing-patient linkage, portal, complete intake, consent, practice acceptance, coverage, request, queue, care, downstream action, external integration, or production use

## 1. Outcome

Exercise the first canonical boundary safely. Recheck current duplicates under the canonical registration lock; either create and immutably link one minimal synthetic patient shell, or record a privacy-safe duplicate block with no patient mutation. Keep every portal, clinical, financial, operational, and external gate closed.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP23-001` | Add append-only promotion results and constrained `SyntheticPromotionAuthorized -> SyntheticPatientPromoted/SyntheticPromotionBlockedPossibleMatch` events with complete authorization/upstream provenance. |
| `TH-SP23-002` | Add administrator-only, practice/facility-scoped, private/no-store queue and idempotent execution endpoints with explicit canonical-creation and no-portal/no-care acknowledgments. |
| `TH-SP23-003` | Reuse the canonical patient-registration advisory lock and repeat current duplicate matching atomically; block without identifying candidates when any match appears. |
| `TH-SP23-004` | On no match, atomically create one deterministic minimal canonical patient shell with portal disabled and link it to immutable promotion evidence. |
| `TH-SP23-005` | Add an accessible independent staff execution section and privacy-safe applicant resume statuses with stable ambiguous retry and recovery. |
| `TH-SP23-006` | Prove one-patient atomicity, duplicate-race blocking, replay/contention, append-only provenance, response minimization, zero portal/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. Entry gate

Execution requires one current `SyntheticPromotionAuthorized` applicant and the exact immutable `AuthorizedForSyntheticPromotion` decision. The complete same-applicant chain through synthetic proofing must still be present and unexpired. The administrator must explicitly acknowledge canonical patient-shell creation and that no portal, identity assurance, request, queue, or care capability results.

## 4. Exit boundary

Sprint 23 ends with either one linked canonical synthetic patient shell or a duplicate-blocked prospective applicant. Existing-patient linkage/merge, portal enrollment and authentication, remaining demographics, complete intake, telehealth consent, practice acceptance, canonical insurance/coverage, estimate/payment, request creation, queueing, and every clinical/downstream action remain unavailable and separately gated.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/service | Administrator-only role, scope, command, reason, acknowledgments, deterministic identifier, outcome/status, and fingerprint tests. |
| Database | Fresh complete-chain success; newly appeared duplicate block; denial/stale/expired rejection; exact replay; changed replay; concurrent one-winner; injected partial-failure rollback; append-only; patient-link provenance; zero portal/downstream delta. |
| HTTP | Permission metadata, no-store/audit context, OpenAPI request/response, stable error vocabulary, no legacy/candidate identifiers, and minimized payload. |
| UI | Empty/load/error, explicit consequence language, acknowledgments, disabled submit, promoted/blocked outcomes, polling cleanup, and unchanged ambiguous retry. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
