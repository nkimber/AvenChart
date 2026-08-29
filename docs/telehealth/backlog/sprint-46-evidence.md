# Sprint 46 applicant request eligibility verification evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0049](../decisions/0049-approved-sprint-46-applicant-request-eligibility-verification.md)

Plan: [Sprint 46 applicant request eligibility verification](sprint-46-applicant-request-eligibility-verification.md)

## Implemented boundary

The access-key owner of one unexpired synthetic prospective applicant can, after the exact Sprint 45 source confirmation, review a server-owned masked eligibility projection and make two explicit acknowledgments. The atomic command revalidates the complete applicant, patient shell, request, intake, protected member-detail, historical eligibility/network, promotion, handoff, and source-confirmation provenance chain; decrypts the protected payload only in server memory; validates it against the masked receipt; invokes the bounded in-process synthetic adapter; validates normalized adapter metadata and outcome facts; inserts one applicant-protected current result; advances only the request from `Verification` version 6 to the same `Verification` status at version 7; and records one request event. The applicant remains `SyntheticRequestCreated` version 26.

The response separates transport, subscriber match, eligibility, benefit-information, and business outcomes. It creates no X12 payload, external call, canonical coverage, coverage selection, exact network determination, estimate or patient responsibility, operational review, acceptance, contact, doctor search, patient or clinician care queue, queue position, appointment, encounter, consent, care, prescription, claim, integration, or external communication.

## Evidence summary

| Evidence | Result |
|---|---:|
| Applicant request eligibility policy and adapter-contract tests | Included in 663 passing backend tests |
| Full backend regression | 663 passed |
| Backend formatting verification | Passed with zero changes |
| Full frontend regression | 53 files / 305 tests passed in a single-worker run |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Four-engine applicant request eligibility flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full telehealth browser accessibility and recovery | 76 passed in a serial run across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL applicant request eligibility proof | 6 checks covering minimization, two acknowledgments, fresh result, replay, contention, source protection, immutability, and zero downstream action |
| Runtime safety | 47 checks |
| OpenAPI contract | 68 checks |
| Authorization matrix | 128 checks |
| Telehealth migration/schema integrity | 133 checks |
| Isolated migration ledger/readiness | 277 migrations through V0321 / 65 required tables |
| Full migration and recovery rehearsal | 277 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 91 checks / 168 Markdown files / 570 relative links / 3 rejected mutations |
| Deterministic code graph | 9,805 nodes / 21,936 edges / 561 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Configured branded host, practice and facility isolation; constant-time applicant access-key ownership; database-clock expiry; exact applicant and request state/version; exact Sprint 45 source-confirmation provenance; and an unexpired bounded context.
- A server-owned no-edit projection containing payer and product labels, masked member and optional group suffixes, subscriber relationship, primary coverage priority, request location, controlled purpose, and explicit limitations. No full identifier, insurance edit, requested outcome, free text, clinical content, or protected payload is returned or accepted.
- Protected payload decryption only inside server command memory, source-mask and relationship validation before adapter use, no copy into result evidence, and no payload or normalized inquiry logging.
- A fixed `NON_PRODUCTION` adapter targeting `ASC_X12N_270_271_005010X279A1` inquiry/response semantics without X12 serialization or external communication. Transport, match, eligibility, benefit-information, and business outcomes remain independent and contract-validated.
- One request-time result, one same-status request-version advance, and one event. Applicant state, patient data, intake, protected insurance source, promotion, and practice-review evidence remain unchanged.
- Exact semantic replay returns the original result; changed-content key reuse, another command after success, stale version, expired or foreign access, source drift, missing or false acknowledgment, and duplicate writers fail closed.
- Private/no-store responses, safe Problem Details, applicant correlation without a staff-session PHI-audit claim, stable retry with one idempotency key, keyboard operation, automated accessibility checks, result focus recovery, 320-pixel reflow, and no source, answer, receipt, checksum, or result persistence in browser storage.

## Evidence-environment findings

An initial live run completed the product workflow but the new proof expected three different adapter outcomes from the Georgia, California, and Florida applicants. Those state scenarios intentionally use the same known-active synthetic plan/member fixture, so all three correctly returned `EligibleBenefitsReported`; the existing adapter contract tests independently cover active, inactive, unmatched, and unavailable tuples. The live assertion was corrected to test the seeded contract without changing product behavior.

A diagnostic rerun reused the already-populated disposable database and reached an inherited Sprint 43 global exact-six-row assertion. That rerun was discarded; final live evidence is collected from a freshly migrated and seeded exact disposable database, matching the runbook boundary.

The first broad browser invocation began before the disposable API had been mapped to the UI harness's default port, so seven desktop-Chromium authenticated-login cases timed out while every public mocked flow and every later engine passed. After mapping the same disposable API to the expected local port, the complete 76-test serial suite passed in one clean run. No product or test assertion was weakened.

## Environment boundary

The final live proof used the exact disposable `avenchart_test_sprint46_schema` database and local enabled API containers with synthetic Georgia, California, and Florida fixtures. No real person, PHI, credential, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 1,042 code files into 9,805 nodes, 21,936 edges, and 561 communities. Its two durable artifacts passed the repository portability check. The Sprint 46 review delta identified 21 changed code files and 454 changed nodes, with 80 capped impacted nodes across 14 surfaced files. The endpoint group, shared frontend transport and applicant headers, eligibility repository, protected source boundary, and database provenance guard are the principal review surfaces. Direct policy and adapter unit tests plus frontend, browser, schema, live replay/isolation/protection, OpenAPI, runtime, authorization, migration, and queue-concurrency evidence address the graph's conservative test-gap warnings.

Both exact disposable API containers were removed and the exact disposable database was dropped after every API-dependent verification completed. Their absence and the unchanged normal-database baseline were then confirmed. This synthetic proof environment is intentionally not recoverable.

## Remaining product and production gates

This evidence does not approve real patients or PHI, the synthetic migraine or sleep rules as medical content, medical-director or golden-case approval, production publication, comprehensive clinical collection or reconciliation, exact practice or rendering-clinician network confirmation, canonical coverage or selection, benefits or patient-responsibility calculation, operational review, practice acceptance, patient communication, doctor search, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, real standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
