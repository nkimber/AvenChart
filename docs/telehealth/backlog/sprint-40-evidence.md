# Sprint 40 applicant-bound request-creation evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open
Decision: [TH-DEC-0043](../decisions/0043-approved-sprint-40-applicant-bound-request-creation.md)
Plan: [Sprint 40 applicant-bound request creation](sprint-40-applicant-bound-request-creation.md)

## Implemented boundary

The access-key owner of one unexpired synthetic prospective applicant can, after the exact Sprint 39 positive practice-review authorization, separately confirm three workflow and safety boundaries and create exactly one source-linked telehealth request in `Draft`. The transaction revalidates the complete authorized provenance, advances only the applicant to `SyntheticRequestCreated`, and records one immutable creation receipt plus one event on each aggregate.

The request is not practice acceptance, patient contact, clinical eligibility, a doctor search, a patient or clinician care-queue entry, a queue position, an appointment, encounter, consent, media session, care authority, prescription, financial action, claim, integration, external communication, or production enablement.

## Evidence summary

| Evidence | Result |
|---|---:|
| Applicant request-creation policy tests | 7 passed; included in 539 passing backend tests |
| Full backend regression | 539 passed |
| Backend formatting verification | Passed with zero changes |
| Full frontend regression | 53 files / 294 tests passed |
| Focused frontend request-creation regression | 2 files / 57 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Focused four-engine applicant request-creation flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full browser accessibility and recovery | 56 passed in one serial run across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL applicant request-creation proof | 10 checks |
| Runtime safety | 41 checks |
| OpenAPI contract | 56 checks |
| Authorization matrix | 110 checks |
| Telehealth migration/schema integrity | 109 checks |
| Isolated migration ledger/readiness | 271 migrations through V0315 / 59 required tables |
| Full migration and recovery rehearsal | 271 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 85 checks / 150 Markdown files / 501 relative links / 3 rejected mutations |
| Deterministic code graph | 9,319 nodes / 20,829 edges / 517 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Configured branded host, practice and facility isolation; constant-time applicant access-key ownership; database-clock expiry; expected applicant version; and three mandatory applicant confirmations.
- Full source-provenance revalidation under the applicant lock, including the positive authorization, submitted case, readiness acknowledgment, promotion, portal-disabled patient shell, controlled visit purpose, passing safety outcome, policy versions, and zero-downstream state.
- Server-derived patient, practice, facility, promotion, case, authorization, purpose and complaint category; the command accepts no patient identifier, source identifier, complaint text, priority, note, or clinical value.
- One `Draft` request, one applicant transition, one immutable creation receipt, and one event per aggregate; the prior applicant evidence and patient shell remain unchanged.
- Exact semantic replay returns the original result; changed-content reuse, another command after success, stale version, expired or foreign access, missing or changed provenance, and duplicate concurrent writers fail closed.
- Private/no-store response handling, safe Problem Details, applicant request correlation without a staff-session PHI-audit claim, stable retry with the same idempotency key, accessible keyboard operation, automated WCAG checks, and 320-pixel reflow.
- `telehealthRequestCreated=true` is the sole positive capability. Contact, both care queues, doctor search, queue position, appointment, encounter, consent, care, prescribing, billing, claim, integration, and external-call capabilities remain false.

## Defects found by live evidence

Live execution found a PostgreSQL reserved-word alias, an untyped nullable query parameter, and a database identifier that PostgreSQL truncated beyond its 63-byte limit. The query was made type-safe, the alias was replaced, and the identifier was shortened consistently across migration and verification code. The live environment also exposed stale readiness-table and append-only-trigger counts, and the browser proof exposed a read-after-create mock that returned the pre-creation state. Those expectations were corrected, the disposable database was rebuilt from all migrations, and the complete live, migration, runtime, contract, authorization, frontend, and browser proofs subsequently passed.

The first broad browser invocation used the frontend's default API base instead of the disposable Sprint 40 API and therefore was not release evidence. A correctly configured parallel run passed 55 of 56 cases; the single unrelated Firefox patient-readiness case passed immediately in isolation, indicating shared-state contention. The final release invocation ran the entire 56-case suite serially against `http://127.0.0.1:5020` and passed without retry or failure.

## Environment boundary

The live proof ran against the exact disposable `avenchart_test_sprint40` database and `avenchart-api-sprint40-e2e` API container with synthetic Georgia, California and Florida fixtures. No real person, PHI, credential, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 1,010 code files into 9,319 nodes, 20,829 edges and 517 communities. Its two durable artifacts passed the repository portability check. The Sprint 40 review delta identified all seven selected policy, repository, service, endpoint, contract, frontend API and applicant-entry files, with the endpoint, repository and shared frontend API surfaces as hubs. Direct backend, frontend, policy, four-engine browser, schema, live replay/contention/isolation/drift, OpenAPI, runtime and authorization coverage addresses the graph's conservative test-gap warnings.

The exact disposable Sprint 40 API container and database were removed after every API-dependent verification completed and the normal database was confirmed unchanged. Both disposable targets were then confirmed absent; this synthetic proof environment is intentionally not recoverable.

## Remaining product and production gates

This evidence does not approve real patients or PHI, portal access, practice acceptance or decline, patient communication, clinical eligibility, exact rendering-clinician network confirmation, doctor search, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
