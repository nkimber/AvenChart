# Sprint 8 consultation documentation draft evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0011](../decisions/0011-approved-sprint-08-consultation-documentation-draft.md)  
Scope: Disabled, synthetic-only, owner-scoped explicit save and reload of an unsigned SOAP draft on the existing consultation encounter; no signing, diagnosis, orders, prescribing, completion, or financial action

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP8-001` | The owner-bound consultation workspace now returns one current documentation projection containing version, server author/time, lock flags, and the four bounded SOAP sections | Initial version-zero empty state, current-only reload, field/key exclusion, no-store headers, and browser rendering/recovery assertions |
| `TH-SP8-002` | `TelehealthConsultationService.SaveDocumentationDraftAsync` and the opaque consultation draft route require physician role, selected facility, treatment purpose, staff identity, both chart-read permissions, encounter-write permission, and current consultation ownership | Service denial tests, 22-check authorization matrix, administrator denial, temporary non-owner physician 404, and zero-note-delta proofs |
| `TH-SP8-003` | `TelehealthConsultationRepository` rebinds and locks the active consultation/request/reservation/shift/session/appointment/encounter/adult-patient relationship, then calls the canonical `EncounterRepository` SOAP append operation in the same transaction | Real PostgreSQL version 1/version 2 linkage, authenticated author/server time, one encounter, stale expected-version conflict with zero delta, and locking-signature conflict with zero delta |
| `TH-SP8-004` | Read and write routes remain synthetic-only, private/no-store, opaque-resource audited, and free of draft content in URLs, evidence events, ordinary logs, telemetry, or browser storage | Runtime-safety source checks, live cache-header and three-permission audit assertions, response/URL key checks, and local/session storage negative assertions |
| `TH-SP8-005` | The physician workspace provides blank clinician-controlled SOAP fields, explicit Save and Reload actions, dirty/conflict/error state, deliberate keep/replace recovery, 10,000-character client limits, and conspicuous unsigned/non-final/not-patient-visible wording | 11 API tests; first simulated 409 retains typed content; successful version-one retry; dirty reload keep/replace; axe, keyboard, reflow, mobile, and multi-engine journeys; no autosave or storage |
| `TH-SP8-006` | Typed API/OpenAPI contracts, authorization/runtime scripts, safeguard `TH-SG-013`, Decision 0011 planning validation, runbook scope, and full regressions close the bounded evidence loop | Complete automated results below |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 132 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 79 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 48 files, 226 tests passed |
| Focused telehealth API tests | 11 passed, including exact opaque PUT payload/headers and no idempotency or forbidden identifiers |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility | 44 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Cross-browser stale-action recovery | 4 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Full 242-migration empty/populated/interruption/recovery rehearsal | Passed, including checkpoints 1, 64, and 127, idempotent replay, checksum-drift rejection, and unexpected-ledger rejection |
| Telehealth migration/schema regression | 26 passed; all 23 telehealth tables and V0282–V0286 invariants remained intact; Sprint 8 added no migration |
| Telehealth authorization proof | 22 passed, including absent identity and administrator documentation-write denial |
| Telehealth OpenAPI proof | 12 passed, including scoped typed draft PUT, optimistic conflict, and no external/canonical identifiers |
| Telehealth runtime-safety proof | 8 top-level checks passed; its 9 focused runtime-policy tests also passed, including no signing, payer, pharmacy, media, or other excluded mutation path |
| Prospective identity regression | 11 passed alone, including ten-way contention and zero canonical deltas |
| Real-PostgreSQL queue/consultation/documentation proof | 72 passed; 20-way reservation/start contention, owner/non-owner isolation, canonical versions, stale conflict, signature lock, audit, privacy, and cleanup all passed |
| Generated empty bootstrap verification | Passed; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 53 passed across 54 Markdown files and 193 relative links; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, with migration evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and coarse facts only. All Sprint 8 database evidence ran in the isolated `avtelehealthsp820260827` Compose project; the existing default PostgreSQL volume remained stopped and untouched during the proof.

## 3. Clinical-record, concurrency, privacy, and UX results

The real API/PostgreSQL and browser proofs demonstrated that:

- a telehealth consultation does not create a second chart or note store; every successful draft save appends a canonical `clinical_notes` SOAP version on the one appointment-linked encounter;
- consultation ownership and every active relationship are revalidated and row-locked in the same transaction as the canonical version append, preventing a state/ownership change between authorization and write;
- only the owning physician can save; an independently authenticated physician receives opaque 404 and appends no note;
- the first draft requires expected version zero, the next successful change links version two to version one through `supersedes_note_id`, and a stale writer receives 409 without overwrite or append;
- a canonical locking signature blocks another draft through the existing chart-signature boundary and database serialization trigger;
- empty SOAP content is rejected, author and time are server-derived, and the client cannot supply patient, encounter, appointment, note, author, or timestamp identifiers;
- workspace reload returns only the current bounded draft and no note identifiers, supersession links, history, signatures, diagnosis, orders, or other chart domains;
- every draft attempt records `acl.patients.demo.view`, `acl.encounters.auth.view`, and `acl.encounters.auth.write` against the opaque consultation resource, while responses remain private and non-cacheable; and
- the React editor starts blank, has no template/default or autosave, retains typed text on conflict, requires deliberate replacement of dirty text on reload, and uses no local or session storage.

The workspace still identifies the surrounding consultation projection as read-only because Sprint 8 authorizes only the separate explicit documentation-draft action. The draft itself is visibly synthetic, incomplete, unsigned, non-final, and not patient-visible.

## 4. Defects and boundary refinements found by the evidence gate

The first integrated permission check used the narrower-looking `encounters:auth_a write` label, but the established canonical encounter writer and the gold physician role grant `encounters:auth write`. The endpoint, Decision 0011, runtime proof, and audit assertion were aligned to that existing canonical permission instead of adding a competing access label.

Before final database proof, the repository initially resolved consultation ownership and invoked the canonical note writer in separate transactions. It was tightened so the complete active relationship is locked and the canonical append occurs inside one transaction. The finalized binary then passed all 72 real-database checks.

One local evidence invocation used Windows PowerShell 5.1, which lacks the harness's `ForEach-Object -Parallel` parameter set and mishandled its Unicode mask assertion. The database fixture was reset and the unchanged proof was rerun under its intended PowerShell 7 runtime, passing 72/72. A prospective-identity proof also observed a legitimate request created by another concurrently running suite; its isolated rerun passed all 11 checks. These were harness-orchestration issues, not accepted application failures.

## 5. Graph and change-impact review

The deterministic code-only Graphify index was rebuilt to 6,742 nodes, 15,988 edges, and 348 communities, and its portability check passed. `review-delta` considered eight Sprint 8 source files, identified 67 changed nodes and 80 impacted nodes across two tracked graph files, and highlighted the shared canonical `EncounterRepository` as the primary dependency hub. Because most telehealth files are new and untracked relative to the repository's current commit, its test-gap hints did not recognize their colocated unit, browser, and runtime proofs. Those hints were treated as navigation prompts, not correctness conclusions; the direct backend, frontend, browser, runtime, migration, authorization, OpenAPI, and safety results above cover the changed boundaries.

## 6. Negative assertions and exclusions

Source/runtime/browser inspection found no clinical template/default, background autosave, offline persistence, generated clinical text, diagnosis or problem-list mutation, medication reconciliation, order/referral, prescription or pharmacy path, disposition, signature/finalization action, AVS, completion, billing/claim/payment mutation, patient draft view, prior-note history, general chart navigation, media transport, recording, transcription, payer call, or external vendor path in this slice.

This evidence does **not** authorize or claim:

- that the unsigned draft is complete, clinically reconciled, signed, final, coded, billable, patient-visible, or appropriate for any real patient;
- diagnosis, treatment, orders, medication changes, prescribing, pharmacy delivery, disposition, follow-up, AVS, completion, claims, billing, payment, or another downstream workflow;
- real consent, identity proofing, clinician licensure verification, minors/proxies/guardians, real audio/video, recording, transcription, or an external integration;
- production enablement, deployment, real people, real PHI, or patient care; or
- completion of independent clinical-safety, security/privacy, data, accessibility, legal, operational, or program-owner review.

## 7. Open review gates

Before signing/finalization, diagnosis/coding, orders, medication reconciliation, prescribing, completion, or live-care work, the following remain required:

1. independent clinical/legal review of the SOAP field semantics, incomplete-chart communication, signature/finalization boundary, amendment policy, state/location/consent/licensure gates, and emergency/interrupted-visit disposition;
2. independent security/privacy review of the atomic ownership binding, write authorization, PHI audit behavior, cache controls, browser exposure, logging/telemetry, conflict responses, enumeration resistance, and break-glass policy;
3. independent data review of canonical `clinical_notes` reuse, transaction locks/order, concurrent writer behavior, signature serialization, encounter versioning, query plans, retention, and recovery;
4. independent accessibility/manual workflow review with supported assistive technology, realistic long clinical text, interrupted saves, conflicts, timeouts, and mobile hardware;
5. program-owner review of this packet and another bounded decision before signature/finalization, diagnosis/coding, order, reconciliation, prescription, pharmacy, disposition, AVS, completion, claim, billing, real media, or external integration work; and
6. formal legal/compliance, clinical governance, credentialing, payer, and vendor gates before any production or patient-care enablement.

Until those reviews are recorded, Sprint 8 remains a disabled synthetic development slice and every production and patient-care gate remains closed.
