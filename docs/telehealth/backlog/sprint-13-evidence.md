# Sprint 13 synthetic prescription-preparation draft evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0016](../decisions/0016-approved-sprint-13-synthetic-prescription-preparation-draft.md)  
Scope: Disabled, synthetic-only, owner-bound prescription preparation during unfinished wrap-up; no recommendation, interaction or contraindication adjudication, controlled substance, canonical prescription or medication, legal effect, signature, transmission, patient delivery, completion, downstream action, external integration, or patient care

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP13-001` | V0290 adds append-only preparation-draft versions and events with current consultation/encounter/pharmacy-choice/catalog references, immutable history, controlled-substance rejection, and hard-false consequential flags | Full migration recovery, 41-check schema proof, live insert rejection, append-only trigger checks, revision history, and signature-lock evidence |
| `TH-SP13-002` | `TelehealthPrescriptionRepository` performs explicit-query neutral search over the existing versioned medication vocabulary, returns at most 20 active non-controlled catalog facts, and supplies no default or recommendation | Service tests, OpenAPI assertions, live catalog search, controlled/unknown rejection, and four-browser no-default checks |
| `TH-SP13-003` | Repository and service rebind the opaque consultation to its current physician owner and require an open unsigned encounter, current confirmed pharmacy-choice version, manual structured directions, all review acknowledgments, expected version, synthetic confirmation, and semantic idempotency | Owner/non-owner/administrator authorization, exact replay, changed-key/stale conflicts, 20-way first-write contention, and no-delta evidence |
| `TH-SP13-004` | Private/no-store GET and idempotent PUT routes expose typed opaque contracts, stable limitations, false legal/safety/delivery flags, and consultation-correlated view/write audit without canonical keys | Authorization, audit/cache/privacy, OpenAPI, response allowlist, and signed-encounter denial evidence |
| `TH-SP13-005` | The WrapUp-only clinician panel starts empty, distinguishes catalog facts from manual directions, requires four explicit acknowledgments, retains ambiguous commands only for explicit retry, reflows at 320 px, and uses no browser persistence | Component/API tests and complete desktop/mobile Chromium, Firefox, and WebKit accessibility/recovery journeys |
| `TH-SP13-006` | Safeguard `TH-SG-018`, Decision 0016, planning validation, Graphify review, runbook, migration/bootstrap recovery, and full regressions close the bounded evidence loop | Complete automated results below |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 166 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 113 passed, 0 failed, 0 skipped, including six prescription-preparation service cases |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 52 files, 246 tests passed in the authoritative sequential run; focused prescription panel/API run passed 22 tests |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility and recovery | 48 passed across desktop/mobile Chromium, Firefox, and WebKit; the authoritative rerun used four workers |
| Full 246-migration empty/populated/interruption/recovery rehearsal | Passed 29 scenarios, including checkpoints 1, 64, and 127, bootstrap catch-up, idempotent replay, checksum-drift rejection, and unexpected-ledger rejection |
| Telehealth migration/schema regression | 41 passed; V0282–V0290, all 30 telehealth tables, 23 append-only triggers, and all earlier controls passed |
| Telehealth authorization proof | 38 passed, including absent-identity, administrator, non-owner, and signature-lock prescription-draft denial |
| Telehealth OpenAPI proof | 20 passed, including typed GET/PUT contracts and mandatory `X-Idempotency-Key` for the semantic command |
| Telehealth runtime-safety proof | 14 top-level checks passed; its 10 focused runtime-policy tests also passed, including no live prescribing or outbound transmission path |
| Prospective identity regression | 11 passed, including contention and zero canonical deltas |
| Real-PostgreSQL end-to-end concurrency proof | 134 passed; neutral search, controlled rejection, exact replay, 20-way contention, revision history, signature lock, audit/cache/privacy, zero canonical/downstream/lifecycle delta, and all prior workflow controls passed |
| Seeded API readiness | Passed after 246 migrations; all 30 telehealth tables ready |
| Generated empty bootstrap verification | Passed unchanged; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 58 passed across 69 Markdown files and 224 relative links; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and coarse facts only. Database evidence ran in the isolated `avtelehealthsp1320260827` Compose project while the existing default PostgreSQL volume remained stopped and untouched.

After evidence capture, the exact labeled API container, Compose database container, network, and isolated volume were removed. That isolated synthetic dataset is intentionally not recoverable. The pre-existing default PostgreSQL service was restarted and verified unchanged at 237 migrations, 1,000 synthetic patients, and latest migration `V0281__index_flow_board_appointments_by_date`.

## 3. Safety, ownership, privacy, and UX results

The evidence demonstrates that:

- only the physician who owns the current unsigned unfinished wrap-up can search or read/write the preparation draft; an administrator is forbidden, another physician receives opaque not-found, and a canonical locking signature removes eligibility;
- an empty query yields no catalog entry and search returns neutral reference facts only, limited to active items with no controlled-substance schedule;
- selecting a catalog item does not populate a dose, frequency, quantity, duration, refills, indication, or directions; the physician explicitly enters each field and affirms current medication review, allergy review, adequate evaluation, and the non-production boundary;
- a current patient-confirmed pharmacy choice is required but only its immutable version is referenced; pharmacy identity/address is not copied into the draft contract;
- exact retry converges on one version/event, stale or changed-key commands fail, 20 concurrent first writes create one version/event, and revision two leaves version one immutable;
- every legal-effect, interaction/safety-check, signature, transmission, patient-delivery, and completion capability remains false;
- writes affect only the two Sprint 13 append-only tables and required audit evidence, with no canonical prescription, medication, signature, AVS, financial, communication, integration, lifecycle, clinician-release, or external-call delta; and
- the browser focuses validation/failure summaries, permits explicit retry with the same command after ambiguity, reflows at 320 px, and keeps clinical content and command keys out of local and session storage.

## 4. Defects and boundary refinements found by the evidence gate

The first live read exposed a raw SQL assembly defect where the common projection and predicate joined as `draftwhere`; the repository now inserts the required newline. The rebuilt API and authoritative 134-check PostgreSQL run prove successful empty/current reads, writes, revisions, replay, and locking behavior.

The first OpenAPI proof showed that the new PUT path was not classified as a semantic idempotent command, so its generated contract omitted `X-Idempotency-Key`. The operation is now registered with the existing command classifier, and the authoritative 20-check proof passes.

The first eight-worker browser matrix passed 46 of 48 cases while Firefox login and a WebKit lazy module import saturated the local development servers. The new prescription-preparation journey itself passed in every browser/project. Repeating the complete unchanged matrix with four workers passed all 48 cases, establishing the lower-concurrency run as the authoritative browser evidence.

The program owner authorized generated-bootstrap changes. Regeneration produced no change: direct verification and the complete 246-migration recovery rehearsal proved the committed base bootstrap current and byte-identical, while V0290 remains migration-owned.

## 5. Graph and change-impact review

The deterministic code-only Graphify index was rebuilt to 6,742 nodes, 15,988 edges, and 348 communities, and its portability check passed. A direct query surfaced the tracked API client, composition root, canonical encounter repository, and broad neighbors but not the new Sprint 13 backend, migration, panel, or tests. `review-delta` likewise reported zero changed or impacted nodes for the eight principal files because the entire telehealth feature remains new and untracked relative to the current commit. The query result and direct-source conclusion were saved under `.graphify/memory/`. Generic missing-test hints were treated only as navigation prompts; the direct unit, API, browser, real-database, authorization, OpenAPI, runtime-safety, migration, and recovery evidence above covers those exact boundaries.

## 6. Negative assertions and exclusions

Sprint 13 does not authorize or claim medication advice, drug recommendation, interaction or contraindication adjudication, allergy inference, diagnosis, medication reconciliation, a canonical medication or prescription, EPCS, controlled substances, prescription signature, NCPDP NewRx mapping, transmission, vendor/network calls, pharmacy acknowledgments, dispense status, drug claims, AVS, patient delivery, encounter/request/appointment completion, clinician release, billing, professional claims, messages, tasks, notifications, outbox work, real media, production enablement, real people, real PHI, or patient care.

## 7. Open review gates

Independent clinical/pharmacy safety, security/privacy, data, accessibility, interoperability/e-prescribing, legal/compliance, operational, and program-owner review remain open. Until those reviews are recorded, Sprint 13 remains a disabled synthetic development slice and every production, legal-prescription, external-transmission, and patient-care gate remains closed.
