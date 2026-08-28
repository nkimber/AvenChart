# Sprint 4: prospective-patient identity shell

Status: Implementation in progress; independent reviews and program-owner packet review pending  
Authorization: [Decision 0007](../decisions/0007-approved-sprint-04-prospective-patient-identity-shell.md)  
Evidence: [Sprint 4 evidence packet](sprint-04-evidence.md)

## Objective

Add a disabled, synthetic-only, practice-branded new-patient entry path that creates an isolated prospective applicant, verifies control of a demonstration contact, and reports a privacy-safe duplicate disposition. Stop at identity review without creating or linking a canonical patient, portal account, chart, coverage record, telehealth request, or queue entry.

## Committed items

| Item | Deliverable |
|---|---|
| `TH-SP4-001` | Minimum-data applicant contract with adult/GA-CA-FL constraints and explicit synthetic acknowledgment |
| `TH-SP4-002` | Client-generated access key with hash-only persistence, practice/facility ownership, expiry, and semantic create idempotency |
| `TH-SP4-003` | Bounded synthetic contact challenge with hash-only verifier, five-attempt lock, optimistic concurrency, and append-only attempt/event evidence |
| `TH-SP4-004` | Practice-scoped exact duplicate classification with only coarse applicant-facing disposition and fail-closed identity-review state |
| `TH-SP4-005` | Accessible public create/verify/status UI with session-only credential storage, recovery, restart, emergency direction, and no identity-proofing claim |
| `TH-SP4-006` | Additive V0284 migration, readiness health update, authorization/OpenAPI/runtime/PostgreSQL/browser evidence, and prior-sprint regression |

## Exit criteria

- No applicant access key or plaintext verifier is persisted, returned by the API, logged, or written to evidence.
- Applicant/key mismatch, wrong host, and cross-practice/facility access disclose no applicant existence.
- Exact create replay is stable; conflicting idempotency reuse fails; verification replay does not consume another attempt.
- Wrong challenges are bounded at five attempts and locked; an expired applicant/challenge fails closed.
- Successful contact verification transitions only to `IdentityReviewPending` and explicitly states that identity is not yet proven.
- Duplicate screening cannot return candidate identifiers, demographics, contacts, match scores, reason details, or counts.
- Database/runtime proof shows no new `patients`, portal accounts, telehealth requests, or queue rows after both duplicate-disposition paths.
- The public form and recovery states pass keyboard, semantic, automated WCAG, and 320 px browser checks.
- Full Sprint 1–3 and shared backend/frontend gates remain green; Production rejection and disabled defaults remain unchanged.
- Independent clinical-safety, security/privacy, data, accessibility, and program-owner packet review remain open and are not self-certified.

