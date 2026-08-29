# Sprint 44 applicant request intake snapshot confirmation evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0047](../decisions/0047-approved-sprint-44-applicant-request-intake-snapshot-confirmation.md)

Plan: [Sprint 44 applicant request intake snapshot confirmation](sprint-44-applicant-request-intake-snapshot-confirmation.md)

## Implemented boundary

The access-key owner of one unexpired synthetic prospective applicant can, after an exact `TelehealthEligible` Sprint 43 complaint result, review a server-owned migraine or sleep summary, select one controlled symptom-duration value, and make eight explicit confirmations. The atomic command revalidates the full applicant, patient shell, request creation, location, universal-safety, complaint-triage, promotion, and practice-review provenance chain; inserts one generic intake snapshot and one applicant-protected receipt; advances only the request from `Intake` version 4 to `Verification` version 5; and records one request event. The applicant remains `SyntheticRequestCreated` version 26.

The response remains explicitly pending and publication-blocked. It does not verify coverage or exact network participation, create operational review, accept the patient, contact anyone, search for a doctor, enter a patient or clinician care queue, assign a queue position, schedule an appointment, create an encounter or consent, authorize care, enable prescribing or billing, create a claim, call an integration, or communicate externally.

## Evidence summary

| Evidence | Result |
|---|---:|
| Applicant intake policy tests | Included in 639 passing backend tests |
| Full backend regression | 639 passed |
| Backend formatting verification | Passed with zero changes |
| Full frontend regression | 53 files / 302 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Four-engine applicant intake flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full browser accessibility and recovery | 76 passed in one serial run across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL applicant intake proof | 6 checks covering minimization, eight confirmations, stable replay, contention, drift denial, immutability, publication blocking, and zero downstream action |
| Runtime safety | 45 checks |
| OpenAPI contract | 64 checks |
| Authorization matrix | 122 checks |
| Telehealth migration/schema integrity | 125 checks |
| Isolated migration ledger/readiness | 275 migrations through V0319 / 63 required tables |
| Full migration and recovery rehearsal | 275 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 89 checks / 162 Markdown files / 542 relative links / 3 rejected mutations |
| Deterministic code graph | 9,709 nodes / 21,707 edges / 532 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Configured branded host, practice and facility isolation; constant-time applicant access-key ownership; database-clock expiry; exact applicant and request state/version; opaque complaint snapshot binding; supported GA, CA, or FL location; and an exact prior `TelehealthEligible` result.
- A server-owned fixed migraine or sleep summary, one of four controlled duration values, and eight required confirmations. No chief-complaint, symptom-detail, history, medication, allergy, diagnosis, or other clinical free text is accepted by this endpoint.
- Full source-provenance revalidation under locks, including the applicant, portal-disabled and unmerged synthetic patient shell, original request creation, fresh location, passing universal-safety assessment, exact complaint result and protocol, promotion and practice-review approvals, and zero downstream state.
- One generic intake snapshot, one protected applicant receipt, one exact request transition, and one event. Applicant state, patient data, request-creation evidence, location, universal-safety, complaint-triage, promotion, and practice-review evidence remain unchanged.
- Exact semantic replay returns the original result; changed-content key reuse, another command after success, stale version, expired or foreign access, unsupported duration, missing or false confirmation, source drift, and duplicate writers fail closed.
- Private/no-store responses, safe Problem Details, applicant correlation without a staff-session PHI-audit claim, stable retry with one idempotency key, keyboard operation, automated WCAG checks, result focus recovery, 320-pixel reflow, and no answer, receipt, checksum, or result persistence in browser storage.
- Clinical content remains `UNAPPROVED_SYNTHETIC`; medical-director approval is required but unrecorded, the clinical golden-case pack is unapproved, and production publication is false and database-guarded.
- The applicant-specific receipt owns its one-request/one-intake invariant without imposing uniqueness on the shared established-patient intake table, preserving append-only refreshed readiness after source coverage changes.

## Defects and evidence-environment findings

The first live intake run was launched under legacy Windows PowerShell and failed inside an inherited Sprint 23 web request despite an HTTP 200 response. The disposable database was reset and the full chain was rerun with repository-supported PowerShell 7, matching CI and the runbook; all six Sprint 44 checks passed. No clinical or product behavior changed.

The migration-resilience login exposed a Windows PowerShell array-unwrapping assumption in the shared staff access-context helper: one facility was returned as a scalar and therefore had no reliable `Count`. Wrapping the complete branch result in an array preserved identical access semantics in PowerShell 7 and made the helper portable to Windows PowerShell; the full 275-migration rehearsal then passed.

The first queue regression found that V0319 had added a global unique request index to the shared established-patient intake table. That prevented the existing governed recovery path from appending a refreshed readiness snapshot after coverage source data changed. The index was removed before commit; the applicant-specific table retains unique request and intake references, and a migration-source guard now rejects reintroducing the overbroad shared-table constraint. The full 134-check lifecycle proof, including refreshed readiness and 20 concurrent callers, then passed.

The first runtime-safety run identified its expected readiness-table count as 62 after V0319 correctly raised the API invariant to 63; the proof expectation was advanced to 63 and all 45 checks passed. One OpenAPI invocation initially omitted the disposable API URL and addressed the inactive default port; the command was rerun with the explicit Sprint 44 URL and all 64 contract checks passed. These were evidence-harness and invocation corrections, not product relaxations.

## Environment boundary

The live proof ran against the exact disposable `avenchart_test_sprint44_schema` database and `avenchart-api-sprint44-e2e` API container with synthetic Georgia, California, and Florida fixtures. No real person, PHI, credential, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 1,036 code files into 9,709 nodes, 21,707 edges, and 532 communities. Its two durable artifacts passed the repository portability check. The Sprint 44 review delta identified all 11 selected migration, policy, repository, service, endpoint, contract, registration, readiness, frontend API, applicant-entry, and staff-access-helper surfaces across 442 changed nodes and 80 capped impacted nodes. The endpoint group, shared frontend transport and applicant headers, database provenance guard, applicant receipt, and repository are the principal hubs. Direct backend, frontend, policy, browser, schema, live replay/isolation/drift, OpenAPI, runtime, authorization, and queue-concurrency coverage addresses the graph's conservative test-gap warnings.

The exact disposable Sprint 44 API container and database were removed after every API-dependent verification completed and the normal database was confirmed unchanged. Both disposable targets were then confirmed absent; this synthetic proof environment is intentionally not recoverable.

## Remaining product and production gates

This evidence does not approve real patients or PHI, the synthetic migraine or sleep rules as medical content, medical-director or golden-case approval, production publication, comprehensive clinical collection or reconciliation, coverage or exact rendering-clinician network verification, operational review, practice acceptance, patient communication, final clinical eligibility, doctor search, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
