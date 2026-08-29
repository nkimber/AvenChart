# Sprint 42 applicant request universal safety assessment evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open
Decision: [TH-DEC-0045](../decisions/0045-approved-sprint-42-applicant-request-universal-safety-assessment.md)
Plan: [Sprint 42 applicant request universal safety assessment](sprint-42-applicant-request-universal-safety-assessment.md)

## Implemented boundary

The access-key owner of one unexpired synthetic prospective applicant can, after the exact Sprint 41 request-location confirmation, complete one immutable four-answer universal safety assessment. The transaction revalidates the complete applicant, patient, request, creation, location, callback, protocol, and zero-downstream source chain; inserts one generic triage assessment and one protected applicant receipt; advances only the request from `LocationConfirmed` version 2 to an exact version 3 safety state; and records one request event. The applicant remains `SyntheticRequestCreated` version 26.

`Emergency` maps to `EmergencyRedirected`; `UrgentInPerson` and `InPersonRequired` map to `InPersonRecommended`; `ClinicalReview` maps to an unassigned `ClinicalReview` state; and `TelehealthEligible` maps only to `SafetyScreening`. A universal-screen pass is not complaint-specific clinical eligibility. No clinical-review work item, contact, doctor search, patient or clinician care queue, queue position, appointment, encounter, consent, media session, care authority, prescription, financial action, claim, integration, external communication, or production enablement is created.

## Evidence summary

| Evidence | Result |
|---|---:|
| Applicant request-safety policy tests | 14 passed; included in 578 passing backend tests |
| Full backend regression | 578 passed |
| Backend formatting verification | Passed with zero changes |
| Full frontend regression | 53 files / 298 tests passed |
| Focused frontend safety regression | 2 files / 61 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Focused four-engine applicant safety flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full browser accessibility and recovery | 64 passed in one serial run across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL applicant request-safety proof | 9 checks, including all five ordered outcomes and eight concurrent callers |
| Runtime safety | 43 checks |
| OpenAPI contract | 60 checks |
| Authorization matrix | 116 checks |
| Telehealth migration/schema integrity | 117 checks |
| Isolated migration ledger/readiness | 273 migrations through V0317 / 61 required tables |
| Full migration and recovery rehearsal | 273 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 87 checks / 156 Markdown files / 527 relative links / 3 rejected mutations |
| Deterministic code graph | 9,480 nodes / 21,192 edges / 536 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Configured branded host, practice and facility isolation; constant-time applicant access-key ownership; database-clock expiry; exact request version; opaque source snapshot; exact supported-state match; callback continuity; three mandatory context confirmations; and four fresh nullable answers with no defaults.
- Full source-provenance revalidation under locks, including the applicant, portal-disabled and unmerged canonical patient shell, request creation, request-location receipt and row, practice-review authorization chain, supported current state, masked callback, exact immutable non-production protocol fixture, prior prospective safety pass, and zero-downstream state.
- Deterministic priority is emergency, severe or worsening, hands-on examination, uncertainty, then universal-screen pass. The server derives every outcome and request state; the command accepts no patient identifier, complaint, diagnosis, note, priority, callback, or free text.
- One generic assessment, one protected applicant receipt, one exact request transition, and one event; the applicant, patient shell, request-creation receipt, request-location evidence, and all earlier evidence remain unchanged.
- Exact semantic replay returns the original result; changed-content reuse, another command after success, stale version, expired or foreign access, current-state mismatch, callback/source/protocol drift, and duplicate concurrent writers fail closed.
- Private/no-store responses, safe Problem Details, applicant request correlation without a staff-session PHI-audit claim, stable retry with one idempotency key, immediate 911 direction, keyboard operation, automated WCAG checks, result focus recovery, 320-pixel reflow, and no answer or source-fingerprint persistence.
- Protective outcomes are terminal or stop progression. A pass exposes only `universalSafetyPassed=true` and `complaintSpecificTriageRequired=true`; complaint-specific assessment, clinical-review work, contact, queueing, care, financial, integration, and external capabilities remain false.

## Defects and evidence-environment findings

The first live product proof passed every product assertion but its final no-downstream query used the wrong appointment primary-key column. The proof was corrected from `appointment_id` to `id`, then all nine scenarios passed. The focused browser proof exposed an ambiguous alert selector when both the immediate emergency warning and retry error were present; the assertion was narrowed to the retry alert and all four engines passed.

The first full serial browser command had no live API base configured for the existing authenticated-workspace cases. After binding the test runner to the disposable Sprint 42 API, all 64 cases passed from the beginning in one worker. The first queue regression was accidentally launched under Windows PowerShell 5.1, which lacks `ForEach-Object -Parallel` and misread the UTF-8 mask literal; that aborted harness left disposable queue rows. The exact disposable schema was deterministically rebuilt from the gold synthetic seed and all 273 migrations, the suite was rerun under PowerShell 7, and all 134 checks—including both 20-caller one-winner proofs—passed. No product code was changed for these harness or environment corrections.

## Environment boundary

The live proof ran against the exact disposable `avenchart_test_sprint42_schema` database and `avenchart-api-sprint42-e2e` API container with synthetic Georgia, California, and Florida fixtures. No real person, PHI, credential, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 1,022 code files into 9,480 nodes, 21,192 edges, and 536 communities. Its two durable artifacts passed the repository portability check. The Sprint 42 review delta identified all eight selected migration, policy, repository, service, endpoint, contract, frontend API, and applicant-entry files; the endpoint, migration guard, safety repository, and shared frontend API surfaces are the principal hubs. Direct backend, frontend, policy, four-engine browser, schema, live replay/contention/isolation/drift, OpenAPI, runtime, and authorization coverage addresses the graph's conservative test-gap warnings.

The exact disposable Sprint 42 API container and database were removed after every API-dependent verification completed and the normal database was confirmed unchanged. Both disposable targets were then confirmed absent; this synthetic proof environment is intentionally not recoverable.

## Remaining product and production gates

This evidence does not approve real patients or PHI, portal access for the synthetic applicant, practice acceptance or decline, patient communication, complaint-specific symptom collection or triage, final clinical eligibility, a clinical-review work item, exact rendering-clinician network confirmation, doctor search, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
