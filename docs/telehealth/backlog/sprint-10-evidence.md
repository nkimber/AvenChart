# Sprint 10 synthetic pharmacy-choice evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0013](../decisions/0013-approved-sprint-10-synthetic-pharmacy-choice.md)  
Scope: Disabled, synthetic-only, owner-scoped neutral pharmacy search and patient-confirmed unsigned destination draft during unfinished wrap-up; no medication, prescription, signature, transmission, lifecycle completion, financial action, external call, or patient care

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP10-001` | V0288 adds append-only patient preference evidence, versioned destination snapshots, and immutable destination events without changing any prescription or consultation lifecycle table | Migration/source assertions, live constraints/triggers, rejected destructive mutation, and full migration recovery rehearsal |
| `TH-SP10-002` | A deterministic `NON_PRODUCTION` directory provides six invented GA/CA/FL destinations with explicit source/version, nullable business identifiers, structured addresses, synthetic-only routing capability, bounded neutral filtering, and acknowledged approximate postal distance | Adapter unit tests, runtime-safety proof, live API result assertions, and no-outbound checks |
| `TH-SP10-003` | Search and recording rebind the opaque consultation to the current owning physician, facility, practice, adult patient, released room, unfinished wrap-up, in-progress appointment, and open encounter | Service validation tests, authorization matrix, non-owner 404 evidence, PHI audit, and live PostgreSQL proof |
| `TH-SP10-004` | Destination recording accepts only current dataset keys plus expected version, semantic idempotency, patient confirmation, and synthetic acknowledgment; the server snapshots provenance and appends history transactionally | Concurrent exact replay, changed-key reuse, stale writer, versioned replacement, append-only event, and client-payload allowlist evidence |
| `TH-SP10-005` | The WrapUp-only physician interface distinguishes chart preference from search/distance, explains entered-origin use, requires explicit patient confirmation, preserves a retry-stable command key, restores error focus, and stores no pharmacy or patient facts in browser storage | Component tests and four-project Playwright keyboard, reflow, axe, recovery, privacy, and payload assertions |
| `TH-SP10-006` | Typed contracts, OpenAPI, no-store responses, opaque consultation-correlated PHI audit, runtime safety, migration/bootstrap, safeguard `TH-SG-015`, planning validation, runbook, and regressions close the bounded evidence loop | Complete automated results below |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 149 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 96 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 49 files, 232 tests passed |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility and recovery | 48 passed across desktop/mobile Chromium, Firefox, and WebKit; 44 accessibility journeys plus 4 stale-action recovery journeys |
| Full 244-migration empty/populated/interruption/recovery rehearsal | Passed 29 scenarios, including checkpoints 1, 64, and 127, bootstrap catch-up, idempotent replay, checksum-drift rejection, and unexpected-ledger rejection |
| Telehealth migration/schema regression | 33 passed; V0282–V0288, all 26 telehealth tables, 19 append-only triggers, pharmacy snapshot/replay/confirmation/routing constraints, and all earlier lifecycle controls passed |
| Telehealth authorization proof | 28 passed, including missing-identity and administrator pharmacy-route denial |
| Telehealth OpenAPI proof | 15 passed, including physician-scoped search/PUT, typed request allowlist, idempotency, bounded failures, and no prescription input payload |
| Telehealth runtime-safety proof | 11 top-level checks passed; its 10 focused runtime-policy tests also passed, including mandatory `NON_PRODUCTION` directory mode and no outbound/downstream pharmacy path |
| Prospective identity regression | 11 passed, including contention and zero canonical deltas |
| Real-PostgreSQL queue/consultation/documentation/wrap-up/pharmacy proof | 98 passed; 20-way reservation/start/wrap-up/destination replay, owner/non-owner, neutral search, provenance, preference, version replacement, stale conflict, append-only, audit/cache/privacy, zero downstream delta, and cleanup all passed |
| Seeded API readiness | Passed after 244 migrations; all 26 telehealth tables ready |
| Generated empty bootstrap verification | Passed unchanged; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 55 passed across 60 Markdown files and 194 relative links; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, with migration evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and coarse facts only. Database evidence ran in the isolated `avtelehealthsp1020260827` Compose project while the existing default PostgreSQL volume remained stopped and untouched.

## 3. Ownership, concurrency, privacy, and UX results

The live API/PostgreSQL and browser proofs demonstrated that:

- search and destination recording remain available only to the owning physician while the same synthetic consultation/request/shift is in unfinished wrap-up, and another physician receives opaque not-found;
- the server rebinds the consultation, request, released reservation, ended synthetic room, wrap-up shift, in-progress appointment, open encounter, adult patient, practice, and selected facility before reading a preference or appending a choice;
- directory results come only from the six-entry `NON_PRODUCTION` dataset version `2026.08.27.1`, use stable neutral filtering, distinguish active chart preference from proximity, and calculate approximate distance only after an entered-origin acknowledgment;
- twenty concurrent exact commands converge on one destination version/event and exact replay, while changed-key reuse and stale writers append nothing;
- a patient-confirmed replacement creates version two plus `DestinationChanged` while retaining version one, immutable source/version/address/routing provenance, and the unfinished lifecycle;
- GET and PUT responses are private/no-store and record patient-view, encounter-view, and—on PUT—encounter-write audit permissions against only the opaque consultation resource;
- the client sends only expected version, directory key, patient confirmation, and synthetic acknowledgment, keeps the same idempotency key through a retriable error, retains the selected destination/confirmation, and restores focus to the alert; and
- browser storage contains neither the workspace patient projection, the pharmacy destination, nor the connection credential.

## 4. Defects and boundary refinements found by the evidence gate

PostgreSQL truncated the original append-only trigger identifier for destination versions because it exceeded the 63-byte identifier limit. The behavior remained enforced, but the convention-based inventory could not see it. V0288 now uses the shorter portable identifier `trg_telehealth_pharmacy_choice_versions_append_only`; the live 19-trigger inventory and complete clean-database rehearsal pass.

The initial OpenAPI proof searched the entire PUT operation for the word “prescription.” Its bounded response intentionally says `prescriptionCreated: false`, producing a false failure. The proof now resolves and inspects the request component schema itself, asserts the four permitted inputs, and rejects patient/encounter/request/medication/prescription/claim input fields.

One prospective-identity proof was accidentally run concurrently with the authorization proof against the same mutable evidence database and correctly detected the other command's request-row delta. It was rerun alone and passed all 11 assertions. Database mutation proofs must remain serialized when they use before/after global counts.

The program owner had authorized generated-bootstrap changes. Regeneration was unnecessary: direct verification and the full recovery rehearsal proved the committed bootstrap is current and byte-identical.

## 5. Graph and change-impact review

The deterministic code-only Graphify index was rebuilt to 6,742 nodes, 15,988 edges, and 348 communities, and its portability check passed. `review-delta` received the five principal Sprint 10 backend, frontend, and migration files. Because the telehealth feature remains new and untracked relative to the repository's current commit, it reported zero changed/impacted nodes and generic missing-test hints. Those hints were treated only as navigation prompts; the direct unit, API, browser, real-database, authorization, OpenAPI, runtime-safety, migration, and recovery evidence above covers the changed boundaries.

## 6. Negative assertions and exclusions

Directory search and destination recording:

- expose no patient, encounter, request, appointment, actor, coordinate, medication, drug, claim, or external routing secret;
- produce no medication, prescription, signature, billing, claim, appointment/encounter completion, consultation/request/shift transition, media, message, AVS, task, or external-call delta;
- cannot be used by an administrator, another physician, a cross-facility identity, or a stale/non-current consultation;
- cannot treat a chart preference as payer preference, endorsement, fill likelihood, dispense status, or proof of electronic reachability;
- require explicit acknowledgment before using an entered postal origin and never read home/current location automatically; and
- retain exact immutable directory provenance and all prior destination versions.

This evidence does **not** authorize or claim a medication or prescribing decision, drug/allergy/interaction/formulary/benefit safety check, signed or unsigned prescription, NCPDP SCRIPT transaction, pharmacy acknowledgment, dispense/pickup/payment status, patient self-service choice, manual/unlisted resolution, precise/current-location lookup, disposition, AVS, completion, clinician release, billing, claim, production enablement, real people, real PHI, or patient care.

## 7. Open review gates

This automated packet cannot close independent clinical-safety, security/privacy, data, accessibility, interoperability, pharmacy/e-prescribing, legal/compliance, operational, or program-owner review. No prescription draft, medication decision, safety checking, signature, NCPDP SCRIPT transaction, pharmacy acknowledgment, patient delivery, disposition, clinician release, completion, billing, claim, production enablement, or patient care is authorized by Sprint 10.

Until those reviews are recorded, Sprint 10 remains a disabled synthetic development slice and every production and patient-care gate remains closed.
