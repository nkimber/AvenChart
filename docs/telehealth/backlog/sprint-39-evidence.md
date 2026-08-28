# Sprint 39 synthetic practice-review authorization evidence

Status: Bounded automated implementation evidence complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open  
Decision: [TH-DEC-0042](../decisions/0042-approved-sprint-39-synthetic-practice-review-authorization.md)  
Plan: [Sprint 39 synthetic practice-review authorization](sprint-39-synthetic-practice-review-authorization.md)

## Implemented boundary

The authenticated administrator or front-desk staff member holding the current unexpired short review claim can record one positive-only operational authorization after reviewing the minimized Sprint 38 packet. The transaction revalidates the full synthetic source chain, advances only the prospective applicant to `SyntheticPracticeReviewAuthorized`, records one immutable authorization and one aggregate event, and preserves the submitted case and short-claim history.

The authorization permits only a separately gated future synthetic request-creation step. It does not accept the applicant into the practice, contact the patient, create a clinical review or telehealth request, enter either care queue, establish a queue position, create an appointment, encounter, consent, or care authority, enable prescribing or billing, create a claim, call an integration, or produce any external consequence.

## Evidence summary

| Evidence | Result |
|---|---:|
| Practice-review authorization policy tests | Included in 532 passing backend tests |
| Full backend regression | 532 passed |
| Full frontend regression | 53 files / 292 tests passed |
| Production frontend build | 137 chunks / 246,399 initial bytes of 256,000-byte budget |
| Frontend lint | Passed |
| Focused four-engine admin authorization flow | 4 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Full browser accessibility and recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit |
| Live GA/CA/FL practice-review authorization proof | 12 checks |
| Runtime safety | 40 checks |
| OpenAPI contract | 54 checks |
| Authorization matrix | 107 checks |
| Telehealth migration/schema integrity | 106 checks |
| Isolated migration ledger/readiness | 270 migrations through V0314 / 58 required tables |
| Full migration and recovery rehearsal | 270 migrations / 29 scenarios |
| Queue and consultation lifecycle regression | 134 checks / 20 concurrent callers |
| Planning and governance validation | 84 checks / 147 Markdown files / 489 relative links / 3 rejected mutations |
| Deterministic code graph | 6,742 nodes / 15,988 edges / 348 communities / 2 portable artifacts passed |
| Generated bootstrap fingerprint | Unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |

## Controls demonstrated

- Administrator/front-desk role, healthcare-operations purpose, `patients.demo.write` permission, configured practice/facility isolation, current-actor claim ownership, and database-clock expiry.
- Full packet provenance revalidation under locks, including the submitted case, readiness, promotion, portal-disabled patient shell, source receipts, controlled purpose, passing safety result, packet policy version, and zero-downstream state.
- One controlled decision and rationale, three independent limitation acknowledgments, no denial or free text, applicant-only status/version advancement, and immutable authorization/event evidence.
- Exact same-actor replay returns the original result; changed-content reuse, stale or foreign state, expired claims, source drift, and duplicate concurrent writers fail closed.
- The pending inbox item disappears after success and the packet becomes unavailable because the applicant no longer has the pending entry state.
- Private/no-store response handling, safe Problem Details, case-correlated PHI audit, stable retry with the same idempotency command, accessible keyboard operation, and 320-pixel reflow.
- `requestCreationAuthorized=true` is the sole positive capability; practice acceptance, patient contact, clinical review, request, both queues, appointment, encounter, consent, care, prescribing, financial, integration, external, and production capabilities remain false.

## Defects found by live evidence

Live execution exposed three repository defects that unit tests did not reach: a PostgreSQL reserved-word table alias, an invalid generic timestamp scalar cast in the authorization transaction, and the same invalid empty-result clock cast in the existing inbox reader. The alias was made unambiguous and both timestamps are now read through typed data readers. Failed authorization transactions rolled back, the empty inbox now returns normally, and the complete live and authorization proofs subsequently passed with unchanged case, claim, source, and downstream fingerprints.

## Environment boundary

The live proof ran against the exact disposable `avenchart_test_sprint39` database and `avenchart-api-sprint39-e2e` API container with synthetic Georgia, California and Florida fixtures. No real person, PHI, credential, payer, pharmacy, provider directory, notification, media, clearinghouse, or other external destination was used. The normal database remained outside the proof and was verified unchanged at 237 recorded migrations, maximum numeric migration version 281, and 1,000 patients. The generated bootstrap verified unchanged with the recorded fingerprint.

The deterministic graph was rebuilt from 772 code files, its two durable artifacts passed the repository portability check, and the Sprint 39 review delta surfaced no committed-node dependency chain because the new files are outside the baseline graph diff. Direct backend, frontend, four-engine browser, schema, live replay/contention/claimant-isolation/drift/authorization, OpenAPI, runtime, and mutation coverage supplies the corresponding change evidence. The exact disposable Sprint 39 API container and database were removed after verification and both were confirmed absent; this synthetic proof environment was intentionally disposable and is not recoverable.

## Remaining product and production gates

This evidence does not approve real patients or PHI, practice acceptance or decline, patient communication, telehealth-request creation, patient or clinician care queueing, queue estimates, scheduling, examination, consent, media, care, prescribing, pharmacy transmission, claims, standards serialization, external integration, or production use. Those require later bounded decisions plus the independent approvals in the master specification.
