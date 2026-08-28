# Sprint 35 synthetic practice-review submission evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open  
Decision: [TH-DEC-0038](../decisions/0038-approved-sprint-35-synthetic-practice-review-submission.md)  
Plan: [Sprint 35 synthetic practice-review submission](sprint-35-synthetic-practice-review-submission.md)

## Implemented boundary

The branded prospective-patient flow now permits the access-key owner of an eligible synthetic applicant to create exactly one immutable `PendingPracticeReview` practice/facility work item. The server owns the review route; the applicant submits only an expected version, a server fingerprint, and four acknowledgments. The transition, case, receipt, and event commit atomically.

`staffReviewCreated=true` is the sole newly authorized operational consequence. This is not staff action, clinical review, practice acceptance, a telehealth request, a doctor search, a patient or clinician care queue, a queue position, an appointment, an encounter, prescribing, billing, a claim, integration, or care authorization. Those consequences remain explicitly false.

## Evidence summary

| Evidence | Result |
|---|---:|
| Practice-review policy tests | 14 passed |
| Full backend regression | 488 passed |
| Focused branded patient component | 18 passed |
| Full frontend regression, serial isolation | 53 files / 284 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Full browser accessibility and recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL practice-review proof | 10 checks |
| Runtime safety | 36 checks |
| OpenAPI contract | 46 checks |
| Authorization matrix | 91 checks |
| Queue/lifecycle PostgreSQL concurrency | 134 checks / 20 callers |
| Base migration and recovery rehearsal | 268 migrations / 29 scenarios |
| Telehealth migration/schema integrity | 100 checks |
| Planning validator v3.2 | 80 checks / 135 Markdown files / 443 relative links / 0 failures or broken links |
| Controlled planning mutations | 3 rejected / restored positive pass |
| Graphify deterministic refresh | 6,742 nodes / 15,988 edges / 348 communities |
| Graph portability | 2 artifacts passed |

The first parallel full-frontend run exposed an existing isolation-sensitive `GovernedReportExecution` failure. That test passed alone, and the complete suite passed with one worker (53 files / 284 tests); no unrelated product code was changed. The generated bootstrap verified unchanged with SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2`.

## Controls demonstrated

- Applicant-key ownership, branded practice/facility isolation, exact readiness and patient-shell provenance, and fail-closed canonical-data drift checks.
- Server-owned routing; no priority, assignee, response-time promise, doctor identity, or queue-position semantics.
- Exact idempotent replay after in-transaction provenance revalidation, changed-key and second-command rejection, contention convergence, one-case cardinality, and append-only case/receipt/event evidence.
- Private no-store minimized API output and an exact six-field command with no identity, contact, payer, device, clinical, narrative, or free-text input.
- Accessible loading, retry, stable ambiguous-submit recovery, result focus, emergency direction, 320-pixel reflow, and no browser persistence.
- Exactly three live practice-review cases and receipts for Georgia, California, and Florida, with zero forbidden table delta and prohibited operational columns absent.

## Environment and graph seal

The final isolated readiness snapshot reported the synthetic telehealth dependency healthy with all 56 required tables present and all 268 packaged migrations through V0312 applied. The exact Sprint 35 API containers and disposable database were removed after verification. The normal database was checked independently and remained at 237 migrations through V0281 with 1,000 synthetic patients, outside the Sprint 35 proof.

The deterministic Graphify refresh and two-artifact portability check passed. Review-delta received the 11 core Sprint 35 code and migration paths, but reported zero changed or impacted graph nodes because the telehealth paths remain untracked relative to repository HEAD; its test-gap heuristic is therefore not treated as coverage evidence. Direct policy, component, browser, live, authorization, schema, migration, and full-regression suites provide the coverage evidence above.

## Remaining product and production gates

This evidence does not approve real patients, staff review decisions, clinical review, acceptance or decline, patient communication, telehealth-request creation, patient or clinician queueing, queue estimates, scheduling, examination, consent, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
