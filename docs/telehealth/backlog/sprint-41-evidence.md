# Sprint 41 applicant request location and callback confirmation evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open
Decision: [TH-DEC-0044](../decisions/0044-approved-sprint-41-applicant-request-location-confirmation.md)
Plan: [Sprint 41 applicant request location and callback confirmation](sprint-41-applicant-request-location-confirmation.md)

## Implemented boundary

The access-key owner of one unexpired synthetic prospective applicant can, after the exact Sprint 40 request creation, explicitly reconfirm the prior supported current-location state and masked callback route. The transaction revalidates the complete source chain, inserts one immutable patient-location row and applicant request-location receipt, advances only the request from `Draft` version 1 to `LocationConfirmed` version 2, and records one request event. The applicant remains `SyntheticRequestCreated` version 26.

The confirmation is not a triage result, clinical review, patient contact, doctor search, patient or clinician care-queue entry, queue position, appointment, encounter, consent, media session, care authority, prescription, financial action, claim, integration, external communication, or production enablement.

## Evidence summary

| Evidence | Result |
|---|---:|
| Applicant request-location policy tests | 7 passed; included in 556 passing backend tests |
| Full backend regression | 556 passed |
| Backend formatting verification | Passed with zero changes |
| Full frontend regression | 53 files / 296 tests passed |
| Focused frontend location-confirmation regression | 2 files / 59 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Focused four-engine applicant location-confirmation flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full browser accessibility and recovery | 60 passed in one serial run across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL applicant location-confirmation proof | 10 checks |
| Runtime safety | 42 checks |
| OpenAPI contract | 58 checks |
| Authorization matrix | 113 checks |
| Telehealth migration/schema integrity | 113 checks |
| Isolated migration ledger/readiness | 272 migrations through V0316 / 60 required tables |
| Full migration and recovery rehearsal | 272 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 86 checks / 153 Markdown files / 513 relative links / 3 rejected mutations |
| Deterministic code graph | 9,392 nodes / 20,996 edges / 524 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Configured branded host, practice and facility isolation; constant-time applicant access-key ownership; database-clock expiry; expected request version; exact supported-state match; opaque source snapshot; and four mandatory boundary confirmations.
- Full source-provenance revalidation under locks, including the applicant, canonical patient shell, request creation, communication-readiness evidence, practice-review authorization chain, supported prior state, masked callback last four, and zero-downstream state.
- Server-derived applicant, patient, request, source receipt, practice, facility, prior state, callback mask, and all source identifiers. The command accepts no raw callback, patient identifier, clinical answer, complaint, priority, note, or free text.
- One append-only location row, one immutable confirmation receipt, one exact request transition, and one event; the applicant, patient shell, request-creation receipt, and all earlier evidence remain unchanged.
- Exact semantic replay returns the original result; changed-content reuse, another command after success, stale version, expired or foreign access, source mismatch, changed state, drift, and duplicate concurrent writers fail closed.
- Private/no-store response handling, safe Problem Details, applicant request correlation without a staff-session PHI-audit claim, stable retry with the same idempotency key, accessible keyboard operation, automated WCAG checks, focus recovery, and 320-pixel reflow.
- `locationConfirmed=true` is the sole positive capability. Triage result, clinical review, contact, both care queues, doctor search, queue position, appointment, encounter, consent, care, prescribing, billing, claim, integration, and external-call capabilities remain false.

## Defects found by live evidence

The first live attempt used an empty migration-only database and could not traverse the prerequisite synthetic practice/facility fixture chain. That run was not product evidence. The proof environment was rebuilt in the required order—gold synthetic seed, migrations, then API—and every prerequisite slice passed.

The first Sprint 41 drift case changed only `patients.phone`, while the effective callback projection correctly preferred `phone_cell` and then `phone_home`. The proof was corrected to change and restore every effective callback field; the product then rejected the drift and the complete live proof passed. Static contract checks also exposed two test-harness patterns that were broader than their intended contracts: a C# quote matcher and a raw-callback prohibition that accidentally matched `maskedCallbackPhone`. Both matchers were narrowed to the exact prohibited shapes. The focused browser proof exposed an ambiguous accessible-name query; the selector was made exact, after which all four engines and the complete serial browser suite passed.

## Environment boundary

The live proof ran against the exact disposable `avenchart_test_sprint41` database and `avenchart-api-sprint41-e2e` API container with synthetic Georgia, California, and Florida fixtures. No real person, PHI, credential, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 1,016 code files into 9,392 nodes, 20,996 edges, and 524 communities. Its two durable artifacts passed the repository portability check. The Sprint 41 review delta identified all eight selected migration, policy, repository, service, endpoint, contract, frontend API, and applicant-entry files, with the endpoint, location repository, migration guard, and shared frontend API surfaces as hubs. Direct backend, frontend, policy, four-engine browser, schema, live replay/contention/isolation/drift, OpenAPI, runtime, and authorization coverage addresses the graph's conservative test-gap warnings.

The exact disposable Sprint 41 API container and database were removed after every API-dependent verification completed and the normal database was confirmed unchanged. Both disposable targets were then confirmed absent; this synthetic proof environment is intentionally not recoverable.

## Remaining product and production gates

This evidence does not approve real patients or PHI, portal access, practice acceptance or decline, patient communication, symptom collection, triage evaluation, clinical eligibility, exact rendering-clinician network confirmation, doctor search, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
