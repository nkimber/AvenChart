# Sprint 2: established-patient readiness

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Authorization: [Decision 0005](../decisions/0005-approved-sprint-02-established-patient-readiness.md)
Evidence: [Sprint 2 evidence packet](sprint-02-evidence.md)

## Objective

Add a disabled, synthetic-only `Intake -> Verification -> OperationalReview` path for an authenticated established patient. Preserve the existing patient and insurance records as systems of record and prove that unknown or mismatched coverage cannot reach staff review.

## Committed items

| Item | Deliverable |
|---|---|
| `TH-SP2-001` | `Intake` and `Verification` request states, transitions, permitted actions, and tests |
| `TH-SP2-002` | Patient-scoped readiness projection with masked coverage choices and exact source fingerprints |
| `TH-SP2-003` | Versioned confirmation, complaint intake, coverage selection, and synthetic acknowledgment command |
| `TH-SP2-004` | Deterministic `NON_PRODUCTION` coverage port with separate eligibility/network semantics |
| `TH-SP2-005` | Append-only V0283 evidence schema with ownership, state, idempotency, and freshness constraints |
| `TH-SP2-006` | Patient UI for reviewing/affirming synthetic data and running verification with accessible recovery |
| `TH-SP2-007` | Runtime/API/authorization/migration/OpenAPI/browser evidence and durable evidence packet |

## Exit criteria

- Eligible triage enters `Intake`, not staff review.
- Readiness accepts only the authenticated request owner's current server projection and insurance record.
- All affirmations are explicit and exact fingerprints are checked again inside the transaction.
- Verification stores separate eligibility and network evidence from the deterministic non-production adapter.
- Only current `Active` plus `ConfirmedInNetwork` evidence reaches `OperationalReview`.
- Unknown/mismatched results remain in `Verification`; no administrator override is introduced.
- Existing Sprint 1 automated gates remain green and the feature remains impossible to enable in Production.
- Independent clinical-safety, security/privacy, data, accessibility, and program-owner packet review remain open and are not self-certified.

All automated exit criteria above are demonstrated in the [Sprint 2 evidence packet](sprint-02-evidence.md). The open independent reviews prevent production or patient-care use.
