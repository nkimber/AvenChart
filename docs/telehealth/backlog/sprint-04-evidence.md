# Sprint 4 prospective-patient identity-shell evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0007](../decisions/0007-approved-sprint-04-prospective-patient-identity-shell.md)  
Scope: Disabled, synthetic-only, practice-branded prospective-applicant creation, demonstration contact control, and privacy-safe duplicate disposition

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP4-001` | `TelehealthContracts.cs`, `TelehealthProspectiveApplicantPolicy.cs` enforce minimum legal-name/DOB/contact/residence data, adult age, GA/CA/FL, and explicit synthetic acknowledgment | eight focused policy tests; API validation and browser form evidence |
| `TH-SP4-002` | `TelehealthProspectiveApplicantService.cs`, `TelehealthProspectiveApplicantRepository.cs`, and V0284 bind an applicant to practice/facility, a browser-generated 256-bit key stored only as SHA-256, 30-minute expiry, version, and create idempotency | real PostgreSQL/API exact replay, conflict, wrong-key, wrong-host, hash-shape, and zero-canonical-delta proof |
| `TH-SP4-003` | V0284 challenge, attempt, and event tables plus transactional verification implement hash-only verifier evidence, five attempts, lock, optimistic concurrency, and append-only triggers | rejected-attempt replay consumes one row; fifth attempt locks; sixth replay is stable; update/delete mutations are rejected |
| `TH-SP4-004` | Facility-scoped exact matching produces only `NoCandidate` or `PossibleMatchManualReview`; successful contact control stops at `IdentityReviewPending` | both dispositions exercised against synthetic PostgreSQL; response leak scan; no candidate IDs, demographics, scores, reasons, or counts; no patient/link/request mutation |
| `TH-SP4-005` | `ProspectivePatientTelehealthEntry.tsx`, `applicantSession.ts`, `api.ts`, route, landing choice, and telehealth CSS implement session-only credential storage, masked resume, same-command network retry, restart, emergency direction, and explicit identity limitations | focused Vitest transport/session tests; desktop/mobile keyboard, axe, 320 px, failure recovery, resume, and stop-state browser evidence |
| `TH-SP4-006` | V0284, generated bootstrap reconciliation, readiness health, OpenAPI applicant scheme, runtime/authorization/migration/prospective scripts, planning manifest, and CI runtime invocation | complete migration interruption/recovery; schema/health/OpenAPI/auth/runtime/prospective/queue proofs; full backend/frontend regression |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 112 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 59 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 47 files, 218 tests passed |
| Focused telehealth frontend tests | 4 files, 12 tests passed |
| Production frontend build and budget | Passed; 246,350 / 256,000 initial bytes; 136 JavaScript chunks checked |
| Desktop/mobile Chromium telehealth browser evidence | 20 passed: 18 accessibility/flow checks plus 2 stale-action failure-recovery checks |
| Full 240-migration empty/populated/interruption/recovery rehearsal | Passed, including checkpoints 1, 64, and 127, idempotent replay, missing migration, checksum drift, and unexpected-ledger rejection |
| Telehealth migration/schema proof | 15 passed, including V0284 ledger, four tables, append-only/no-delete triggers, and database constraints |
| Telehealth authorization proof | 14 passed, including separate applicant-key requirements and no patient-session substitution |
| Telehealth OpenAPI proof | 8 passed, including three applicant paths, separate key scheme, typed bodies, idempotency, conflict, and expiry |
| Telehealth runtime-safety proof | 6 passed, including disabled defaults, Production rejection, no delivery integration/canonical mutation, and 17/17 readiness tables |
| Prospective identity real-PostgreSQL proof | 11 passed, including exact replay, bounded attempts, both duplicate dispositions, hash-only secrets, immutable evidence, and zero canonical deltas |
| Existing real-PostgreSQL queue/concurrency regression | 28 passed with one reservation winner among 20 callers and preserved queue/privacy behavior |
| Generated empty bootstrap verification | Passed; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 49 passed across 42 Markdown files and 143 relative links; three controlled negative mutations returned nonzero |

The durable machine-readable runtime results are written under `avenchart/artifacts/telehealth/` and the shared migration result under `avenchart/artifacts/migration-resilience/`. Those generated runtime outputs are local evidence artifacts and are not treated as source authority.

## 3. Boundary results

The real database/API proof demonstrated all of the following without persisting or emitting an applicant access key:

- exact create replay returned the same applicant while changed command content returned conflict;
- a missing key returned unauthorized and a wrong key returned the same not-found scope response as an unknown applicant;
- retrying the same rejected-code command returned the same error and left exactly one attempt;
- ten concurrent rejected commands produced four ordinary rejections and six locked responses, recorded exactly five attempts, and never returned a server error; a later replay did not create a sixth;
- contact control returned `IdentityReviewPending` with `ContactControlOnly`, never an identity-proofing claim;
- a no-match fixture returned only `NoCandidate`, while an exact synthetic chart fixture returned only `PossibleMatchManualReview`;
- neither response contained a candidate identifier, canonical identifier, raw candidate name, match score, reason, or candidate count; and
- across both disposition paths, the deltas for `patients`, portal accounts, insurance records, telehealth requests, and queue entries were all zero.

The applicant response also fixes `CanonicalPatientCreated` to false, masks contact data on resume, and removes the demonstration code after successful verification. The frontend retains no form demographics in the applicant session record; only applicant ID and the browser credential are in `sessionStorage`.

## 4. Migration, recovery, and review evidence

V0284 is additive. The packaged migrator built an empty database, replayed all 240 migrations, recovered after three committed-migration fault points, rejected missing/checksum/unexpected ledger states, and removed its isolated test database/container. Database triggers rejected changes to challenge, attempt, and event evidence and rejected aggregate deletion.

The required Graphify query was used before implementation to locate existing portal identity, patient registration, and duplicate-review boundaries. The post-change delta reported 62 changed nodes, 80 impacted nodes, and broad `App.tsx` history-linked impact. Graphify did not surface the new tests as related nodes, so its test-gap list was not treated as a correctness conclusion; direct policy/transport/session tests, real PostgreSQL/API proofs, OpenAPI/authorization scripts, and desktop/mobile browser evidence cover the changed feature surfaces.

## 5. Negative assertions and exclusions

Source/runtime inspection found no email/SMS client, outbound delivery, patient insertion, portal-account insertion, request insertion, or queue insertion in the prospective identity path. No SSN, government ID image, insurance identifier, payment data, symptom, or complaint field is accepted by its contract. The browser explicitly says no message was sent and that the form does not request care.

This evidence does **not** authorize or claim:

- identity proofing, production authentication, account recovery, real contact delivery, automatic matching, patient creation/linkage, portal enrollment, or staff promotion;
- insurance collection/network confirmation for a new applicant, clinical intake/triage, request/queue entry, consultation, video, prescribing, pharmacy, claims, or payment;
- production enablement, deployment, real people, real PHI, or patient care; or
- completion of independent clinical-safety, security/privacy, data, accessibility, legal, operational, or program-owner review.

## 6. Open review gates

The following remain required before this slice can advance beyond synthetic implementation verification:

1. independent security/privacy review of credential handling, enumeration resistance, abuse/rate limits, applicant retention, duplicate privacy, and threat evidence;
2. independent data review of V0284 constraints, concurrency, expiry, retention/purge, recovery, indexing, and future atomic promotion boundaries;
3. independent accessibility/manual usability review, including cognitive load, error comprehension, mobile assistive technology, and real-user research;
4. clinical/legal/compliance confirmation of adult/jurisdiction entry wording and separation from clinical eligibility, consent, and identity assurance; and
5. program-owner review of this packet and an explicit later decision before identity proofing, HIM resolution, coverage, or canonical promotion work.

Until those reviews are recorded, Sprint 4 remains a disabled synthetic development slice and every production/patient-care gate remains closed.
