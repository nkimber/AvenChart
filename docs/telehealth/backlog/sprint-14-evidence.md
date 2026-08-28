# Sprint 14 synthetic prospective-applicant identity-review evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0017](../decisions/0017-approved-sprint-14-synthetic-applicant-identity-review.md)  
Scope: Disabled, synthetic-only, staff-governed contact-control and duplicate-disposition review; no identity proofing, patient promotion/linkage, portal enrollment, intake completion, coverage, request, queue, clinical, financial, prescribing, integration, external action, production use, or real PHI

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP14-001` | V0291 adds one append-only identity-review decision table, constrained applicant terminal states/events, actor/provenance fields, and hard-false promotion/downstream flags | Empty and populated migration/schema proof, live database constraints, append-only rejection, contention, and zero-delta checks |
| `TH-SP14-002` | `TelehealthApplicantIdentityReviewRepository` provides a practice/facility-scoped candidate-safe queue and transactionally rebinds current applicant/contact/duplicate/actor facts before deciding | Authorization, deterministic outcome, stale writer, exact replay, changed-content, and 12-writer contention evidence |
| `TH-SP14-003` | Policy and service permit only `NoCandidate -> ApprovedForProspectiveIntake` and `PossibleMatchManualReview -> ManualReviewRequired`, while explicitly denying identity proofing and patient promotion | Nine focused policy tests, typed contracts, live mismatch rejection, and public coarse-response assertions |
| `TH-SP14-004` | Private/no-store GET and idempotent PUT routes require staff session, treatment purpose, facility scope, and demographic view/write permission and emit PHI audit entries | Authorization matrix, OpenAPI contract, cache headers, and correlated audit proof |
| `TH-SP14-005` | The administrative panel shows only bounded applicant facts, server-derived actions, explicit limitations, accessible validation/recovery, and stable semantic retry without browser persistence | Component/API tests and browser accessibility/failure-recovery evidence |
| `TH-SP14-006` | Safeguard `TH-SG-019`, Decision 0017, planning validation, migration recovery, Graphify review, runbook, and regressions close the bounded evidence loop | Automated results and open gates below |

## 2. Automated results

| Gate | Result |
|---|---|
| Focused telehealth backend tests | 122 passed, 0 failed, including nine applicant identity-review policy cases |
| Focused telehealth frontend tests | 9 files, 43 tests passed, including deterministic review and ambiguous stable-retry behavior |
| Live applicant identity-review proof | 14 checks passed, including outcome constraints, actor/facility/purpose isolation, exact replay, changed-content/stale rejection, 12-way one-winner contention, append-only rejection, 11 correlated PHI audit entries, public minimization, and zero canonical/downstream deltas |
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 175 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 52 files, 249 tests passed in the authoritative one-worker run; focused telehealth run passed 9 files and 43 tests |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility and recovery | 48 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Full 247-migration empty/populated/interruption/recovery rehearsal | Passed 29 scenarios, including checkpoints 1, 64, and 127, bootstrap catch-up, idempotent replay, checksum-drift rejection, and unexpected-ledger rejection |
| Telehealth migration/schema regression | 44 passed; V0282–V0291, all 31 telehealth tables, 24 append-only triggers, and all earlier controls passed |
| Telehealth authorization proof | 41 passed, including absent identity, physician, permission, purpose, and cross-facility identity-review denial |
| Telehealth OpenAPI proof | 22 passed, including typed private GET/PUT contracts and mandatory `X-Idempotency-Key` for the semantic command |
| Telehealth runtime-safety proof | 15 top-level checks passed; synthetic-only mode and 31-table readiness remained healthy |
| Prospective identity regression | 11 passed, including contention and zero canonical deltas |
| Real-PostgreSQL end-to-end concurrency proof | 134 passed, including all prior workflow, ownership, exact-replay, append-only, contention, lifecycle, privacy, and zero-downstream controls |
| Generated empty bootstrap verification | Passed unchanged; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 59 passed across 72 Markdown files and 233 relative links; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-identity-review.json`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and coarse facts only.

After evidence capture, the exact labeled Sprint 14 API/migrator/PostgreSQL containers, network, and isolated volume were removed. That disposable synthetic dataset is intentionally not recoverable. The pre-existing default PostgreSQL service was restarted and verified unchanged at 237 migrations, 1,000 synthetic patients, and latest migration `V0281__index_flow_board_appointments_by_date`.

## 3. Safety, ownership, privacy, and UX results

The evidence demonstrates that:

- a dedicated administrator remains traceable by authenticated actor and audit evidence without requiring a clinical-staff record, while a front-desk principal must retain a current facility staff binding;
- only the applicant's own bounded facts are shown, masked contact details remain masked, and no possible matching patient or canonical candidate identifier is exposed;
- staff cannot select or override an outcome: the server derives the sole permitted result from the durable duplicate disposition;
- the review is explicitly contact-control and duplicate-disposition review, not NIST identity proofing or an identity-assurance claim;
- exact retry converges, stale/changed/conflicting commands fail, 12 concurrent first writers produce one decision and one event, and the evidence rows reject update/delete;
- recording affects only the applicant aggregate and bounded decision/event/audit evidence, with no canonical patient, portal, insurance, intake, coverage, request, queue, appointment, encounter, claim, prescription, integration, or external-call delta; the authoritative run recorded 11 correlated PHI audit entries; and
- the browser retains an ambiguous command's idempotency identity for explicit retry and stores no applicant or command data in local or session storage.

## 4. Boundary refinements found by the evidence gate

The live proof exposed that PostgreSQL returns the decision timestamp through the data provider as a `DateTime` shape in this scalar path rather than the assumed `DateTimeOffset`; the repository now reads the typed provider value without a direct invalid cast. The authoritative rerun passed all 14 live checks.

The first OpenAPI proof showed that the identity-review PUT path was not classified as a semantic idempotent command. The operation is now registered with the existing classifier, and the authoritative 22-check proof verifies the required `X-Idempotency-Key` header.

The seeded administrative model also demonstrated a legitimate distinction: the dedicated administrator principal is traceable but not a clinical staff row, whereas front-desk access is staff-bound. The schema and service preserve both invariants and reject a front-desk actor without the facility staff binding.

The default parallel Vitest pool produced timing failures in unrelated pre-existing completion-review, portal-message, governed-report, and merged-chart tests while the backend build or other test files competed for the local runner. Every affected file passed when isolated, and the authoritative one-worker full suite passed all 249 tests without a code change. The deterministic lower-concurrency run is therefore the recorded frontend regression evidence.

## 5. Graph and change-impact review

The deterministic code-only Graphify index was rebuilt to 6,742 nodes, 15,988 edges, and 348 communities, and its portability check passed. `review-delta` reported zero changed or impacted nodes for the nine principal Sprint 14 backend, migration, contract, and frontend files because the telehealth feature tree remains new and untracked relative to the current commit. Its generic missing-test hints were treated only as navigation prompts. Direct source review plus the unit, component, API, browser, real-database, authorization, OpenAPI, runtime-safety, migration, recovery, and no-delta evidence above covers those exact boundaries.

## 6. Negative assertions and exclusions

Sprint 14 does not authorize or claim NIST identity proofing, an identity-assurance level, document/biometric/knowledge-based verification, patient matching resolution, possible-match disclosure, merge/link, a canonical patient/chart, portal account, insurance eligibility or network participation, consent, clinical triage, practice acceptance, request/queue creation, appointment, encounter, care, prescription, claim, billing, communication, notification, integration, external call, production enablement, real people, real PHI, or patient care.

## 7. Open review gates

Independent identity, clinical, security/privacy, data, accessibility, interoperability, legal/compliance, operational, and program-owner review remain open. Until those reviews are recorded, Sprint 14 remains a disabled synthetic development slice and every production, identity-proofing, patient-promotion, request/queue, downstream, and patient-care gate remains closed.
