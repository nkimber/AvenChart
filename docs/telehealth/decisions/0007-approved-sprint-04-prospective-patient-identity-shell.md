# Decision 0007: Sprint 4 prospective-patient identity-shell authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Add a practice-branded, synthetic-only new-patient entry path that creates a **prospective applicant**, not a patient:

```text
public branded entry
  -> minimum applicant details
  -> synthetic contact verification
  -> privacy-safe duplicate classification
  -> identity review pending
```

The applicant remains separate from `patients`, patient-portal accounts, charts, insurance records, telehealth requests, and queues. A successful synthetic contact challenge proves control of the demonstration contact only; it is not identity proofing. Duplicate screening returns only `NoCandidate` or `PossibleMatchManualReview` to the applicant and never returns candidate identifiers, scores, demographics, contact data, or record counts.

## 2. Authorized implementation surfaces

Changes may use the existing Decisions 0003, 0005, and 0006 telehealth paths plus:

```text
docs/telehealth/decisions/0007-approved-sprint-04-prospective-patient-identity-shell.md
docs/telehealth/backlog/sprint-04-prospective-patient-identity-shell.md
docs/telehealth/backlog/sprint-04-evidence.md
avenchart/database/migrations/V0284__telehealth_prospective_patient_identity.sql
avenchart/scripts/Test-TelehealthProspectiveIdentity.ps1
```

The smallest telehealth contract, endpoint, service, repository, frontend, test, runtime-proof, OpenAPI, authorization, planning-validation, migration-readiness, health-check, and CI composition edits needed to connect and verify the slice are authorized.

## 3. Required controls

1. The feature remains disabled by default, synthetic-only, restricted to the configured practice host and facility, and rejected in Production.
2. Only minimum applicant data is collected: legal first and last name, date of birth, email, phone, residence state, and postal code. SSN, government-document images, payment cards, and insurance identifiers are prohibited in this slice.
3. Applicants must be adults and reside in synthetic Georgia, California, or Florida. This is an entry constraint, not a clinical eligibility or current-location determination.
4. The browser supplies a high-entropy applicant access key. Only its SHA-256 hash is persisted; the server never returns the key and logs/evidence must not contain it.
5. Applicant reads and commands require the configured host, practice/facility scope, applicant identifier, and access key. Invalid applicant/key combinations produce the same not-found response.
6. The development challenge is visibly labeled synthetic, expires after a bounded period, permits at most five attempts, stores no plaintext verifier, and has semantic idempotency for every attempt.
7. Contact verification and identity proofing are distinct. Passing the challenge can transition only to `IdentityReviewPending`.
8. Duplicate screening is practice/facility scoped and exact-input based. Public responses expose only `NoCandidate` or `PossibleMatchManualReview`; ambiguity always fails closed.
9. The slice cannot create, link, update, or reveal a canonical patient; create a portal identity, enrollment, chart, coverage record, request, or queue entry; or authorize staff promotion.
10. Applicant status transitions use optimistic concurrency. Aggregate events and verification attempts are append-only; expired/locked states do not erase evidence.
11. The interface must preserve entered data after recoverable errors, explain storage/session limitations, provide a restart path, and remain keyboard/screen-reader usable at 320 px.
12. Unit, API, authorization, migration, real-PostgreSQL, OpenAPI, desktop/mobile accessibility, failure-recovery, planning, and full regression evidence must pass without weakening prior gates.

## 4. Explicit exclusions

This decision does not authorize:

- real email/SMS delivery, production identity providers, NIST assurance claims, document capture, biometrics, knowledge-based authentication, or account recovery;
- candidate disclosure, automatic record linkage, canonical-patient creation, portal enrollment, HIM resolution, or staff promotion;
- insurance collection or in-network confirmation for the new applicant, clinical intake/triage, request creation, queue entry, consultation, video, prescribing, pharmacy, claims, or payment;
- marketplace entry, cross-practice search, production enablement, deployment, real people, real PHI, or patient care; or
- self-certification of clinical, security/privacy, data, accessibility, legal, operational, or program-owner review.

## 5. Stop conditions and rollback

Stop if an applicant can access another applicant, a secret or plaintext verifier is persisted/logged, duplicate candidate information escapes, any canonical patient or request is created, matching crosses the configured practice/facility, a real destination is contacted, Production accepts the feature, or prior evidence regresses. Rollback disables/removes the public route and API; additive applicant evidence remains retained for governed cleanup rather than destructive rollback.

## 6. Approval record

The program owner directed Codex to implement the complete telehealth goal, approved all decisions, and authorized uninterrupted operation and required bootstrap changes while unavailable. This record interprets that authority only for the bounded, reversible, synthetic identity shell above. It does not broaden authority to production, real patient care, external integrations, or independent-review sign-off.

## References

- [Decision 0006](0006-approved-sprint-03-patient-queue-transparency.md)
- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Security, privacy, consent and audit specification](../16-security-privacy-consent-and-audit.md)
- [Sprint 4 plan](../backlog/sprint-04-prospective-patient-identity-shell.md)

