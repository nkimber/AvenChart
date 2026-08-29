# Sprint 47 evidence: applicant request practice-network verification

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0050](../decisions/0050-approved-sprint-47-applicant-request-practice-network-verification.md)

Plan: [Sprint 47 applicant request practice-network verification](sprint-47-applicant-request-practice-network-verification.md)

## Implemented boundary

- Applicant-private GET/POST route at `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/practice-network`.
- Exact current positive request eligibility, request, applicant, patient-shell, practice, facility, plan, state, service, and date provenance is rebound under transaction locks.
- The in-process `NON_PRODUCTION` gateway receives no member, group, subscriber, or patient data and targets Plan-Net-shaped provider-directory concepts without FHIR serialization or an external call.
- One immutable practice-network result and request event advance only pending `Verification` version 7 to version 8.
- Rendering-physician selection and exact participation, canonical coverage, financial, operational, contact, queue, appointment, encounter, consent, care, integration, and external action remain false.
- The applicant UI requires three unchecked acknowledgments, has stable unchanged-content retry, exposes the practice-only limitation, restores focus, reflows, and persists no evidence.

## Evidence summary

| Evidence | Result |
|---|---:|
| Applicant request practice-network policy and adapter-contract tests | Included in 674 passing backend tests |
| Full backend regression | 674 passed |
| Backend Release build and formatting verification | Passed with zero warnings, errors, or formatting changes |
| Full frontend regression | 53 files / 306 tests passed in a single-worker run |
| Production modern-frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Modern and reference frontend lint/build | Passed |
| Four-engine applicant request practice-network flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full telehealth browser accessibility and recovery | 76 passed in a serial run across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL applicant request practice-network proof | 6 checks covering minimization, three acknowledgments, fresh result, replay, contention, provenance, immutability, and zero downstream action |
| Runtime safety | 48 checks / 66 required tables |
| OpenAPI contract | 70 checks |
| Authorization matrix | 131 checks |
| Telehealth migration/schema integrity | 137 checks |
| Isolated migration ledger/readiness | 278 migrations through V0322 / 66 required tables |
| Full migration and recovery rehearsal | 278 migrations / 29 scenarios, including checkpoints 1, 64, and 127 |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 92 checks / 171 Markdown files / 583 relative links / 3 rejected mutations |
| Deterministic code graph | 9,887 nodes / 22,118 edges / 570 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Configured branded host, practice and facility isolation; constant-time applicant access-key ownership; database-clock expiry; exact applicant, request, intake, source-confirmation, eligibility-result, promotion, handoff, and practice-review provenance; and an unexpired bounded context.
- A server-owned no-edit projection containing only practice, facility, payer and plan labels, request state/service/date, controlled outcome vocabulary, limitations, and opaque provenance. No member, subscriber, group, policy, patient, clinical, free-text, requested-outcome, or financial data is returned or accepted.
- A fixed `NON_PRODUCTION` adapter receives only organization, location, plan, state, healthcare-service, and date criteria. It targets Plan-Net-shaped concepts without FHIR serialization, provider-directory communication, or any external action.
- The adapter output is rejected unless its organization, location, service, plan, network, time, and result tuples match the server-owned synthetic candidate contract. Missing or contradictory references fail closed.
- One immutable practice-level result, one same-status request-version advance from 7 to 8, and one request event. Applicant state, patient shell, intake, protected insurance source, eligibility evidence, promotion, and practice-review evidence remain unchanged.
- Exact semantic replay returns the original result; changed-content key reuse, another command after success, stale version, expired or foreign access, source or eligibility drift, missing or false acknowledgment, and duplicate writers fail closed.
- Private/no-store responses, safe Problem Details, stable retry with one idempotency key, keyboard operation, automated accessibility checks, result focus recovery, 320-pixel reflow, and no evidence persistence in browser storage.

## Evidence-environment findings

The first broad browser invocation used Playwright's general eight-worker default. The live-login cases share synthetic runtime state and therefore interfered with each other, while the isolated public applicant tests passed. The governed workflow intentionally runs this suite with one worker. A subsequent serial attempt correctly showed that authenticated cases also require the workflow's seeded API on port 5001. The final run used a freshly migrated, seeded, isolated API database at that port and all 76 tests passed without changing product behavior or weakening an assertion.

## Environment boundary

The final live, concurrency, migration, contract, and browser proofs used exact disposable `avenchart_test_sprint47_*` databases and local enabled API containers with synthetic Georgia, California, and Florida fixtures. No real person, PHI, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside these proofs and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 1,048 code files into 9,887 nodes, 22,118 edges, and 570 communities. Its two durable artifacts passed the repository portability check. The Sprint 47 review delta identified 21 changed code files and 462 changed nodes, with 80 capped impacted nodes across 14 surfaced files. The endpoint group, shared applicant transport/header boundary, policy/service/repository path, immutable database provenance guard, and applicant UI are the principal review surfaces. Direct policy/adapter tests plus frontend, browser, schema, live replay/isolation/minimization, OpenAPI, runtime, authorization, migration, and concurrency evidence address the graph's conservative test-gap warnings.

Every exact Sprint 47 disposable API container and database is removed after API-dependent verification. Their absence and the unchanged normal-database baseline are confirmed before commit. This synthetic proof environment is intentionally not recoverable.

## Remaining product and production gates

Rendering-physician selection and exact participation verification; canonical coverage and selection; pricing, patient responsibility and self-pay; consent; operational authorization; patient contact; queueing; appointment; encounter; care; live integrations; production enablement; and independent clinical, legal/compliance, privacy/security, accessibility, data, interoperability, and operations review remain open.
