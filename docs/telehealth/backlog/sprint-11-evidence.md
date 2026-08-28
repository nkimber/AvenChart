# Sprint 11 synthetic safety-disposition draft evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0014](../decisions/0014-approved-sprint-11-synthetic-safety-disposition-draft.md)  
Scope: Disabled, synthetic-only, physician-owned structured safety-disposition draft during unfinished wrap-up; no signing, finalization, patient delivery, AVS, downstream clinical/financial/communication/integration action, lifecycle completion, external call, or patient care

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP11-001` | V0289 adds append-only disposition snapshots and immutable recording/revision events with `legal_effect=false`, exact vocabularies, conditional safety constraints, versioning, and replay identity | Migration/source checks, live constraints/triggers, destructive-mutation rejection, and full recovery rehearsal |
| `TH-SP11-002` | `TelehealthSafetyDispositionRules` accepts only eight physician-selected outcomes and enforces common text/follow-up/communication facts plus adequate-evaluation, urgent/emergency, and interrupted-outcome conditions | Eight focused rules tests, API 400 cases, and live zero-evidence assertions |
| `TH-SP11-003` | The repository rebinds the opaque consultation to the current owning physician, practice/facility, adult patient, released reservation, ended room, wrap-up request/shift, in-progress appointment, and open unsigned encounter | Owner/non-owner and administrator denial, signed-encounter denial, PHI audit, and live PostgreSQL proof |
| `TH-SP11-004` | Recording accepts only expected version, structured physician facts, bounded authored text, semantic idempotency, and synthetic acknowledgment; every success appends one snapshot/event with server actor/time | Twenty-way exact replay, changed-key reuse, stale writer, version-two emergency revision, append-only, and request-schema allowlist evidence |
| `TH-SP11-005` | The WrapUp-only physician UI has no clinical defaults, reveals consequential conditions, requires explicit synthetic confirmation, retains form content and command identity through ambiguous failure, focuses errors, and stores no clinical content in browser storage | Component tests plus four-project Playwright keyboard, reflow, axe, recovery, privacy, and exact-payload assertions |
| `TH-SP11-006` | Typed contracts, OpenAPI, private/no-store response handling, opaque consultation-correlated PHI audit, runtime safety, migration/bootstrap, safeguard `TH-SG-016`, planning validation, runbook, and full regressions close the bounded evidence loop | Complete automated results below |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 157 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 104 passed, 0 failed, 0 skipped, including eight disposition-rule cases |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 50 files, 237 tests passed; focused panel/API run passed 19 tests |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility and recovery | 48 passed across desktop/mobile Chromium, Firefox, and WebKit; 44 accessibility journeys plus 4 stale-action recovery journeys |
| Full 245-migration empty/populated/interruption/recovery rehearsal | Passed 29 scenarios, including checkpoints 1, 64, and 127, bootstrap catch-up, idempotent replay, checksum-drift rejection, and unexpected-ledger rejection |
| Telehealth migration/schema regression | 37 passed; V0282–V0289, all 28 telehealth tables, 21 append-only triggers, disposition conditional/replay constraints, and all earlier controls passed |
| Telehealth authorization proof | 32 passed, including absent-identity and administrator GET/PUT disposition denial |
| Telehealth OpenAPI proof | 17 passed, including scoped GET/PUT, typed conditional request allowlist, idempotency, bounded failures, and no finalization/downstream identifiers |
| Telehealth runtime-safety proof | 12 top-level checks passed; its 10 focused runtime-policy tests also passed, including no advice, outbound, lifecycle, or downstream disposition path |
| Prospective identity regression | 11 passed, including contention and zero canonical deltas |
| Real-PostgreSQL end-to-end concurrency proof | 113 passed; 20-way reservation/start/wrap-up/pharmacy/disposition replay, conditional 400s, owner/non-owner, version revision, signature lock, append-only, audit/cache/privacy, zero downstream delta, and cleanup passed |
| Seeded API readiness | Passed after 245 migrations; all 28 telehealth tables ready |
| Generated empty bootstrap verification | Passed unchanged; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 56 passed across 63 Markdown files and 204 relative links; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and coarse facts only. Database evidence ran in the isolated `avtelehealthsp1120260827` Compose project while the existing default PostgreSQL volume remained stopped and untouched.

After evidence capture, the exact labeled API container, Compose database container, network, and isolated volume were removed. The pre-existing default PostgreSQL service was restarted and verified unchanged at 237 migrations, 1,000 synthetic patients, and latest migration `V0281__index_flow_board_appointments_by_date`.

## 3. Safety, ownership, concurrency, privacy, and UX results

The evidence demonstrates that:

- the application presents no disposition or clinical-text default and never chooses an outcome or writes instructions for the physician;
- completed-evaluation outcomes require adequate-evaluation confirmation, urgent/emergency outcomes require current location and callback reconfirmation, emergency requires an instruction acknowledgment plus factual handoff state, interrupted outcomes require a contact/safety-attempt summary, and communication method/state must agree;
- only the physician who owns the current unsigned unfinished wrap-up can read or append the draft; an administrator is forbidden, another physician receives opaque not-found, and a canonical locking signature removes eligibility;
- twenty concurrent exact commands converge on one version/event, exact replay returns that version, and changed-key reuse or stale versions append nothing;
- a valid emergency revision creates version two and `DraftRevised` while retaining immutable version one, `legal_effect=false`, and every unfinished consultation/request/shift/appointment state;
- search/draft responses are private/no-store and record patient-view, encounter-view, and—on PUT—encounter-write permissions against the opaque consultation resource only;
- recording creates no prescription, medication, signature, billing, claim, message, portal-mailbox, integration outbox/inbox, lifecycle, notification, task, delivery, or external-call delta; and
- the browser preserves the exact semantic key and payload across a retriable error while keeping the draft and patient projection out of session/local storage.

## 4. Defects and boundary refinements found by the evidence gate

The first browser locator for “Disposition” also matched the named safety-disposition region because the region's accessible name includes that word. The product semantics were valid; the proof was tightened to scope every form locator to the region and use exact combobox names. The complete four-project browser run then passed.

The migration proof initially named four conceptual constraints rather than V0289's exact portable identifiers. The inventory was corrected to the live constraint names before the authoritative run; all ten selected disposition constraints and both append-only triggers now pass against PostgreSQL.

The program owner authorized generated-bootstrap changes. Regeneration was unnecessary: direct verification and the complete 245-migration recovery rehearsal proved the committed bootstrap current and byte-identical.

## 5. Graph and change-impact review

The deterministic code-only Graphify index was rebuilt to 6,742 nodes, 15,988 edges, and 348 communities, and its portability check passed. A direct query did not surface the new Sprint 11 panel, repository, or migration, and `review-delta` reported zero changed or impacted nodes for the six principal files because the entire telehealth feature remains new and untracked relative to the current commit. The query result was saved under `.graphify/memory/`. Generic missing-test hints were treated only as navigation prompts; the direct unit, API, browser, real-database, authorization, OpenAPI, runtime-safety, migration, and recovery evidence above covers those exact boundaries.

## 6. Negative assertions and exclusions

Sprint 11 does not authorize or claim diagnosis, coding, an order or referral, medication reconciliation or prescribing, a signed/final disposition, patient instruction delivery, AVS, emergency dispatch or verified transfer, completed communication or follow-up beyond the physician's entered draft fact, encounter/request/appointment completion, clinician release, billing, claims, real media, production enablement, real people, real PHI, or patient care.

## 7. Open review gates

Independent clinical-safety, security/privacy, data, accessibility, interoperability, legal/compliance, operational, and program-owner review remain open. Until those reviews are recorded, Sprint 11 remains a disabled synthetic development slice and every production and patient-care gate remains closed.
