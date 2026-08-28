# Sprint 26: synthetic insurance handoff confirmation

Status: Approved for bounded implementation by [TH-DEC-0029](../decisions/0029-approved-sprint-26-synthetic-insurance-handoff-confirmation.md)  
Scope: Applicant-owned review and immutable no-edit confirmation of masked synthetic insurance inputs and recorded fixture limitations after minimum registration-details confirmation; no canonical coverage, exact network or rendering-physician conclusion, price/financial action, complete intake, consent, acceptance, request, queue, care, external integration, or production use

## 1. Outcome

Add a privacy-bounded post-promotion insurance handoff that lets the applicant recognize the information already supplied without revealing protected raw identifiers or pretending the prior deterministic fixtures are insurer responses. Keep incorrect information honest: any discrepancy stops this path instead of mutating coverage.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP26-001` | Add a server-owned insurance-handoff snapshot policy with exact source-chain provenance, last-four masks, normalized evidence status, freshness, and deterministic fingerprinting. |
| `TH-SP26-002` | Add applicant-key protected private/no-store retrieval after minimum registration confirmation, returning no patient/source identifier, raw member/group value, subscriber identity, protected payload, or trace token. |
| `TH-SP26-003` | Add one idempotent atomic no-edit confirmation command with payer/product, masked member details, subscriber relationship/priority, evidence-limitations, and synthetic affirmations. |
| `TH-SP26-004` | Add append-only receipt/event provenance bound to the registration confirmation, promotion, portal-disabled patient shell, member details, positive fresh eligibility, positive fresh practice-network fixture, practice/facility, and aggregate version. |
| `TH-SP26-005` | Add an accessible patient review section with masking, source labels, freshness, explicit rendering-physician/coverage limitations, correction stop guidance, disabled submit, stable ambiguous retry, and recovery. |
| `TH-SP26-006` | Prove minimization, canonical-coverage/portal/provenance rejection, no-edit input, exact replay, contention, append-only behavior, zero patient/insurance/downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticMinimumRegistrationDetailsConfirmed`; the successful promotion and registration receipt must still exist; the linked patient shell must remain facility-bound, unmerged, portal-disabled, and exactly equal to applicant minimum fields; no canonical `insurance_records` row may exist; and the same applicant's protected member-details, positive synthetic eligibility, and positive practice-level network evidence must be complete and unexpired. The network evidence must still state that no rendering physician was checked and no exact aggregate network or coverage result was created.

## 4. Exit boundary

Sprint 26 ends at `SyntheticInsuranceDetailsConfirmed`. The receipt confirms only that the applicant recognized masked copied insurance inputs and acknowledged fixture limitations. It is not insurer confirmation, canonical coverage, a payment/benefit guarantee, exact network confirmation, rendering-physician participation, a price estimate, completed intake, legal consent, practice acceptance, request creation, queueing, or care authorization.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Policy/snapshot | Exact allowlist, last-four-only masks, deterministic source-bound fingerprint, separate eligibility/practice-network outcomes and freshness, rendering-physician false, and no protected/raw field projection. |
| Database | Successful receipt; registration/promotion/patient/member/eligibility/network mismatch rejection; stale/expired/canonical-coverage/portal-enabled rejection; exact replay; changed replay; concurrent one-winner; append-only; zero patient/insurance/downstream delta. |
| HTTP | Applicant-only private/no-store reads/writes, required idempotency, typed no-edit input/output, bounded errors, and no patient/source/member/proofing/trace identifiers. |
| UI | Loading/error/retry, masks and source labels, evidence freshness, explicit coverage/payment/rendering-physician limitations, correction stop direction, disabled submit, confirmed outcome, reflow, focus, and no insurance-detail browser persistence. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
