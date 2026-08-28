# Sprint 37 synthetic practice-review claim evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open  
Decision: [TH-DEC-0040](../decisions/0040-approved-sprint-37-synthetic-practice-review-claim.md)  
Plan: [Sprint 37 synthetic practice-review claim](sprint-37-synthetic-practice-review-claim.md)

## Implemented boundary

An authorized administrator or front-desk staff member can claim one exact pending synthetic practice-review item for 120 seconds. The database clock sets the lease, the exact case is locked, and one first writer wins. Claim receipts are immutable; exact replay is actor-bound; expiry permits a new receipt without overwriting history.

The inbox exposes only whether an active claim exists, whether it belongs to the current staff member, and when it expires. Another claimant's identity is never returned. Claiming prevents duplicate staff work only. It does not create priority, a disposition, clinical review, acceptance or decline, patient contact, a telehealth request, a patient or clinician care queue, an appointment, encounter, care, prescribing, financial activity, integration, or external action.

## Evidence summary

| Evidence | Result |
|---|---:|
| Practice-review claim policy/repository tests | Included in 507 passing backend tests |
| Full backend regression | 507 passed |
| Focused admin/API frontend tests | 45 passed |
| Full frontend regression | 53 files / 289 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Full browser accessibility and recovery | 52 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL claim proof | 10 checks |
| Runtime safety | 38 checks |
| OpenAPI contract | 50 checks |
| Authorization matrix | 99 checks |
| Telehealth migration/schema integrity | 103 checks |
| Isolated migration ledger/readiness | 269 migrations through V0313 / 57 required tables |
| Full migration and recovery rehearsal | 269 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 82 checks / 141 Markdown files / 465 relative links / 3 rejected mutations |
| Deterministic code graph | 6,742 nodes / 15,988 edges / 348 communities / portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Administrator/front-desk role, healthcare-operations purpose, `patients.demo.write` permission, and configured practice/facility isolation.
- In-transaction revalidation of the exact pending case, submission, readiness, promotion, portal-disabled unmerged patient shell and copied fields, controlled purpose, passing safety result, expiry, and zero downstream provenance.
- Three independent no-decision, no-contact, and no-request-or-care-queue acknowledgments with semantic idempotency.
- Exact actor-bound replay, changed-content conflict, stale-version denial, active-claim conflict, eight-way first-writer convergence, and immutable expiry/reclaim history.
- A database-clock lease of exactly 120 seconds, append-only evidence, direct database constraints and guards, and case-correlated PHI access auditing.
- Active/mine/expiry-only projection, with no other claimant identity, priority, SLA, queue position, source detail, or broader action exposed.
- Accessible explicit claim, disabled-until-acknowledged behavior, stable retry identity, keyboard operation, 320-pixel reflow, four-engine browser coverage, and no browser persistence.
- An unchanged product-state fingerprint outside immutable claim and audit evidence; every decision, request, queue, appointment, encounter, care, prescribing, financial, integration, and external capability remained false.

## Environment boundary

The proof ran against the exact disposable `avenchart_test_sprint37` database and `avenchart-api-sprint37-e2e` API container with synthetic Georgia, California, and Florida fixtures. The full migration rehearsal used its own isolated databases and passed all 29 bootstrap, interruption recovery, drift rejection, concurrency, persistence, and failure-mapping scenarios. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients.

The deterministic graph was rebuilt from 772 code files, its two durable artifacts passed the repository portability check, and the Sprint 37 delta surfaced no committed-node dependency chain because the new files are outside the baseline graph diff. Direct backend, frontend, four-engine browser, schema, live concurrency, authorization, OpenAPI, runtime, and mutation coverage supplies the corresponding change evidence. The exact disposable Sprint 37 API container and database were removed after verification and both were confirmed absent.

## Remaining product and production gates

This evidence does not approve real patients or PHI, staff disposition, priority, clinical review, practice acceptance or decline, patient communication, telehealth-request creation, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
