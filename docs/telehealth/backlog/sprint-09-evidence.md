# Sprint 9 consultation wrap-up handoff evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0012](../decisions/0012-approved-sprint-09-consultation-wrap-up-handoff.md)  
Scope: Disabled, synthetic-only, owner-scoped handoff from active consultation into unfinished physician-owned wrap-up; no disposition, signing, completion, clinician release, downstream clinical/financial action, real media, or patient care

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP9-001` | V0287 adds request/shift `WrapUp`, consultation `MediaEnded`, server timing evidence, and a database governor that permits only `Started -> MediaEnded` with version +1 | Migration/source assertions, live constraint/function/trigger checks, rejected arbitrary update/delete/rollback, and full 243-migration recovery rehearsal |
| `TH-SP9-002` | The opaque wrap-up POST requires physician role, facility, treatment purpose, staff identity, chart view/write permissions, current ownership, expected consultation version, semantic idempotency, and three affirmative acknowledgments | Service tests, 24-check authorization matrix, non-owner 404, administrator/absent-identity denials, missing-ack 400, stale/changed-content 409, and exact replay |
| `TH-SP9-003` | One transaction locks the full relationship and changes only consultation/request/shift plus one event for each aggregate | Twenty concurrent exact commands all returned 200 through one transition/event pair; appointment stayed in progress, encounter stayed open, room stayed ended/released, and reserve-next remained blocked |
| `TH-SP9-004` | The owner workspace now recognizes the exact active and wrap-up state triples, exposes consultation lifecycle version/time, and preserves canonical unsigned-draft access | Live owner/non-owner tests, current projection reload, version-three draft append during wrap-up, existing optimistic conflict/signature lock, and no downstream delta |
| `TH-SP9-005` | Patient and physician interfaces distinguish unfinished wrap-up from completed care and require explicit consequential acknowledgments | Stable-key conflict/retry, retained draft text, continued save, honest patient state, keyboard/reflow/axe checks across four browser projects, and forbidden-identifier/storage assertions |
| `TH-SP9-006` | Typed contracts, OpenAPI, PHI audit, cache controls, runtime safety, migration/bootstrap, safeguard `TH-SG-014`, planning validation, runbook, and regressions close the bounded evidence loop | Complete automated results below |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 138 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 85 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 48 files, 227 tests passed |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility | 44 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Cross-browser stale-action recovery | 4 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Full 243-migration empty/populated/interruption/recovery rehearsal | Passed 29 scenarios, including checkpoints 1, 64, and 127, bootstrap catch-up, idempotent replay, checksum-drift rejection, and unexpected-ledger rejection |
| Telehealth migration/schema regression | 29 passed; V0282–V0287, all 23 telehealth tables, wrap-up timing/governor, append-only evidence, and active/busy/wrap-up uniqueness passed |
| Telehealth authorization proof | 24 passed, including absent identity and administrator wrap-up denial |
| Telehealth OpenAPI proof | 13 passed, including scoped typed wrap-up POST, affirmative body, idempotency, bounded failures, and no canonical identifiers |
| Telehealth runtime-safety proof | 10 top-level checks passed; its 9 focused runtime-policy tests also passed, including the unfinished/no-disposition/no-downstream boundary |
| Prospective identity regression | 11 passed, including contention and zero canonical deltas |
| Real-PostgreSQL queue/consultation/documentation/wrap-up proof | 85 passed; 20-way reservation/start/wrap-up contention, exact replay, stale/conflict, ownership, atomicity, continued draft, unavailability, audit/cache/privacy, and cleanup all passed |
| Generated empty bootstrap verification | Passed; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 54 passed across 57 Markdown files and 184 relative links; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, with migration evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and coarse facts only. Database evidence ran in the isolated `avtelehealthsp920260827` Compose project while the existing default PostgreSQL volume remained stopped and untouched.

## 3. Lifecycle, concurrency, privacy, and UX results

The live API/PostgreSQL and browser proofs demonstrated that:

- only the owning physician can submit the wrap-up command, and the server rebinds the consultation, request, released reservation, shift, ended simulator session, appointment, encounter, physician, facility, practice, and adult patient before changing state;
- twenty exact concurrent commands converge on one `MediaEnded/WrapUp/WrapUp` transition, one consultation event, and one request event, while changed-content or stale commands produce no partial delta;
- consultation-start facts remain immutable, events remain append-only, the appointment remains in progress, the encounter stays open, the room stays released/ended, and the physician cannot reserve new work;
- the owner can reload the bounded workspace and append another canonical unsigned SOAP version during wrap-up; the existing signature lock still prevents further draft writes;
- the response exposes the opaque consultation ID and lifecycle/version facts but no patient, request, shift, appointment, encounter, disposition, or other canonical key;
- each route is private/no-store and records the required patient-view, encounter-view, and encounter-write permissions against the opaque consultation audit resource; and
- the physician action preserves all three acknowledgments and its stable idempotency key after a conflict, and the patient sees a not-complete state without clinician, encounter, draft, or connection identity.

## 4. Defects and boundary refinements found by the evidence gate

The packaged migration runner previously passed `--build` to an image-only Compose service, so it could silently reuse a stale migrator image. It now explicitly rebuilds the API/migrator image before execution and surfaces build failure.

The first generated-bootstrap recovery rehearsal found that V0287 added a timing constraint already present in the generated current schema. V0287 now performs an idempotent constraint replacement; the bootstrap was regenerated and the complete 243-migration empty/populated/interruption/recovery rehearsal passed.

One local queue proof was invoked with Windows PowerShell 5.1 even though its concurrency and UTF-8 assertions require PowerShell 7. After reset it ran under `pwsh`. A subsequent run reached the normal global 120-request development limiter before the final 20-way wrap-up burst; the isolated evidence host was restarted with the repository-approved local upper-bound configuration, and the unchanged proof passed 85/85. A response privacy assertion was also narrowed to JSON property names so the permitted explanatory word “disposition” in a limitation did not look like a forbidden `disposition` field.

## 5. Graph and change-impact review

The deterministic code-only Graphify index was rebuilt to 6,742 nodes, 15,988 edges, and 348 communities, and its portability check passed. `review-delta` received the five principal Sprint 9 backend, frontend, and migration files. Because the entire telehealth feature remains new and untracked relative to the repository's current commit, it reported zero changed/impacted nodes and generic missing-test hints. Those hints were treated only as navigation prompts; the direct unit, API, browser, real-database, authorization, OpenAPI, runtime-safety, migration, and recovery results above cover the changed boundaries.

## 6. Negative assertions and exclusions

Source/runtime/browser inspection found no final disposition, emergency/in-person instruction generation, follow-up plan, signing/finalization action, clinician release, encounter/appointment completion, AVS, diagnosis/coding, order/referral, medication reconciliation, prescription/pharmacy, claim, billing/payment, patient draft access, real media, recording, transcription, notification, or external vendor path in this slice.

This evidence does **not** authorize or claim:

- that a real session ended, clinical work was completed, the patient was treated, or any final safety/disposition decision occurred;
- signed/final documentation, patient delivery, diagnosis, orders, medication changes, prescribing, pharmacy transmission, AVS, completion, billing, claims, payment, or clinician availability;
- real consent, identity proofing, clinician licensure verification, minors/proxies/guardians, audio/video, recording, transcription, or external integration;
- production enablement, deployment, real people, real PHI, or patient care; or
- completion of independent clinical-safety, security/privacy, data, accessibility, legal, operational, or program-owner review.

## 7. Open review gates

Before any final disposition, signing, completion, clinician release, patient delivery, or downstream work, the following remain required:

1. independent clinical/legal review of session-end semantics, interrupted/abandoned/emergency/in-person outcomes, required safety/follow-up communication, signature/finalization, state/location/consent/licensure, and patient delivery;
2. independent security/privacy review of command ownership, PHI audit, cache/log/browser boundaries, enumeration resistance, idempotency evidence, and break-glass policy;
3. independent data review of the governed context mutation, lock order, event/version invariants, replay semantics, query plans, recovery, retention, and coexistence with future completion states;
4. independent accessibility/manual workflow review using supported assistive technology, realistic long notes, interrupted commands, conflicts, timeouts, and mobile hardware;
5. program-owner review of this packet and another bounded decision before disposition, signing/finalization, clinician release, AVS, diagnosis/coding, order, reconciliation, prescription/pharmacy, completion, claim/billing, real media, or external integration work; and
6. formal legal/compliance, clinical governance, credentialing, payer, and vendor gates before any production or patient-care enablement.

Until those reviews are recorded, Sprint 9 remains a disabled synthetic development slice and every production and patient-care gate remains closed.
