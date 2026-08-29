# Sprint 43 applicant request complaint-triage evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open  
Decision: [TH-DEC-0046](../decisions/0046-approved-sprint-43-applicant-request-complaint-triage.md)  
Plan: [Sprint 43 applicant request complaint triage](sprint-43-applicant-request-complaint-triage.md)

## Implemented boundary

The access-key owner of one unexpired synthetic prospective applicant can, after an exact passing Sprint 42 universal assessment, complete one immutable complaint-specific assessment for the server-owned `migraine` or `sleep` category. The transaction revalidates the complete applicant, patient, request, creation, location, universal-safety, visit-purpose, protocol, and zero-downstream source chain; inserts one generic triage assessment and one protected complaint receipt; advances only the request from `SafetyScreening` version 3 to an exact version 4 disposition; and records one request event. The applicant remains `SyntheticRequestCreated` version 26.

`Emergency` maps to `EmergencyRedirected`; `UrgentInPerson` and `InPersonRequired` map to `InPersonRecommended`; `Unsupported` maps to `Unsupported`; `ClinicalReview` maps to an unassigned `ClinicalReview` state; and `TelehealthEligible` maps only to `Intake`. No intake snapshot, clinical-review work item, contact, doctor search, patient or clinician care queue, queue position, appointment, encounter, consent, media session, care authority, prescription, financial action, claim, integration, external communication, or production enablement is created.

## Evidence summary

| Evidence | Result |
|---|---:|
| Applicant complaint policy/evaluator tests | 38 passed; included in 621 passing backend tests |
| Full backend regression | 621 passed |
| Backend formatting verification | Passed with zero changes |
| Full frontend regression | 53 files / 300 tests passed |
| Focused frontend complaint-triage regression | 2 files / 63 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Focused four-engine applicant complaint flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full browser accessibility and recovery | 68 passed in one serial run across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL applicant complaint-triage proof | 7 checks covering both categories, all six outcomes, explicit `NotSure`, publication blocking, replay, drift denial, immutability, and zero downstream action |
| Runtime safety | 44 checks |
| OpenAPI contract | 62 checks |
| Authorization matrix | 119 checks |
| Telehealth migration/schema integrity | 121 checks |
| Isolated migration ledger/readiness | 274 migrations through V0318 / 62 required tables |
| Full migration and recovery rehearsal | 274 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 88 checks / 159 Markdown files / 540 relative links / 3 rejected mutations |
| Deterministic code graph | 9,608 nodes / 21,451 edges / 545 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Configured branded host, practice and facility isolation; constant-time applicant access-key ownership; database-clock expiry; exact request version; opaque source snapshot; exact server-owned category; callback continuity; three mandatory context confirmations; and the complete fresh category-specific coded answer set with no defaults.
- Full source-provenance revalidation under locks, including the applicant, portal-disabled and unmerged canonical patient shell, request creation, location receipt and row, passing universal assessment, supported current state, masked callback, exact immutable non-production protocol fixture, prior prospective evidence, and zero-downstream state.
- Deterministic, priority-ordered evaluation records every fired rule and reason in private immutable evidence. Missing or malformed answers fail validation; explicit `NotSure` routes to `ClinicalReview`; the highest-severity fired rule determines the exact outcome.
- One generic assessment, one protected complaint receipt, one exact request transition, and one event; the applicant, patient shell, request-creation receipt, location evidence, universal assessment, and all earlier evidence remain unchanged.
- Exact semantic replay returns the original result; changed-content reuse, another command after success, stale version, expired or foreign access, category substitution, current-state mismatch, source/patient/protocol drift, and duplicate writers fail closed.
- Private/no-store responses, safe Problem Details, applicant request correlation without a staff-session PHI-audit claim, stable retry with one idempotency key, immediate 911/988 direction, keyboard operation, automated WCAG checks, result focus recovery, 320-pixel reflow, and no answer, result, checksum, rule, or reason persistence in browser storage.
- The clinical content is explicitly `UNAPPROVED_SYNTHETIC`; medical-director approval is required but unrecorded, the clinical golden-case pack is unapproved, and production publication is false and database-guarded.

## Defects and evidence-environment findings

The first empty-schema rehearsal exposed that a disposable live-proof database created from the bootstrap alone had no synthetic facility rows. The isolated proof database was rebuilt from the gold synthetic seed before applying all migrations. The complaint harness then exposed a Windows PowerShell `OrderedDictionary` clone assumption and an initial setup that created only migraine requests; the harness was corrected to use a JSON deep copy and two controlled migraine/sleep setup batches. All seven live scenarios then passed.

The first focused browser assertion used a substring label that matched more than one complaint question; exact accessible labels removed the ambiguity and all four engines passed. The first full serial browser command configured only the public-entry API variable, so existing authenticated-workspace cases addressed the default API. The run was stopped and restarted with both supported API-base variables bound to the disposable Sprint 43 API; all 68 cases then passed from the beginning with one worker.

The full backend regression exposed that the exhaustive state-machine declaration test had not yet listed the five new `SafetyScreening` transitions. The test declaration was brought into exact agreement with the approved state graph and all 621 tests passed. Formatting verification then identified line wrapping in two new files; the repository formatter changed only those files and the zero-diff formatting check passed. No clinical rule or product behavior was changed by these harness, test-declaration, environment, or formatting corrections.

## Environment boundary

The live proof ran against the exact disposable `avenchart_test_sprint43_schema` database and `avenchart-api-sprint43-e2e` API container with synthetic Georgia, California, and Florida fixtures. No real person, PHI, credential, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 1,030 code files into 9,608 nodes, 21,451 edges, and 545 communities. Its two durable artifacts passed the repository portability check. The Sprint 43 review delta identified all nine selected migration, evaluator, policy, repository, service, endpoint, contract, frontend API, and applicant-entry files across 420 changed nodes and 80 impacted nodes; the endpoint, migration guard, repository, policy, and shared frontend API surfaces are the principal hubs. Direct backend, frontend, policy, four-engine browser, schema, live replay/isolation/drift, OpenAPI, runtime, and authorization coverage addresses the graph's conservative test-gap warnings.

The exact disposable Sprint 43 API container and database were removed after every API-dependent verification completed and the normal database was confirmed unchanged. Both disposable targets were then confirmed absent; this synthetic proof environment is intentionally not recoverable.

## Remaining product and production gates

This evidence does not approve real patients or PHI, the synthetic migraine or sleep rules as medical content, medical-director or golden-case approval, production publication, assigned clinical review, patient communication, final clinical eligibility, comprehensive clinical collection or reconciliation, exact rendering-clinician network confirmation, doctor search, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
