# Sprint 7 read-only consultation workspace evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0010](../decisions/0010-approved-sprint-07-read-only-consultation-workspace.md)  
Scope: Disabled, synthetic-only, owning-physician access to a bounded current consultation projection; no general chart navigation or clinical/financial mutation

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP7-001` | Established-patient request insertion now requires an active, unmerged patient whose date of birth is within the inclusive 18–120-year boundary | Real PostgreSQL proofs temporarily exercised ages 17 and 121; both returned the opaque 404 boundary and persisted zero requests; the adult fixture was restored |
| `TH-SP7-002` | `TelehealthConsultationRepository.GetWorkspaceAsync` uses a repeatable-read transaction and an explicit projection that binds consultation, request, reservation, shift, room, appointment, encounter, physician, practice, facility, active adult patient, and latest intake | Owner success, non-owner 404, current-state predicates, bounded active lists, allowlisted response, excluded-field assertions, and no clinical-output delta |
| `TH-SP7-003` | The service requires physician role, configured facility, active staff identity, and consultation ownership; the endpoint composes `patients:demo view` and `encounters:auth view` access filters | 75 focused backend tests, 20-check authorization matrix, temporary second physician proof, and two permission-scoped audit results |
| `TH-SP7-004` | The endpoint sets the opaque consultation as the PHI audit resource and all API responses use `no-store`, `no-cache`, `private`, `max-age=0`, `Pragma: no-cache`, and `Expires: 0` | Live header assertions and two authorized 200 audit rows correlated only to the opaque consultation identifier |
| `TH-SP7-005` | The physician route loads the projection only after a successful start, renders identity/callback/current visit and three bounded lists, supplies explicit empty/verification wording, and supports reload without browser persistence | 19 focused frontend tests; first-load 503 and keyboard retry; DOM/storage negative assertions; 44 WCAG/reflow journeys on Chromium, Firefox, and WebKit |
| `TH-SP7-006` | Typed contracts/OpenAPI, safety/authorization/queue scripts, safeguard `TH-SG-012`, planning validation, and unchanged 242-migration/23-table boundaries close the bounded evidence loop | Complete automated results below |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 128 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 75 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 48 files, 225 tests passed |
| Focused telehealth frontend tests | 5 files, 19 tests passed |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility | 44 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Cross-browser stale-action recovery | 4 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Full 242-migration empty/populated/interruption/recovery rehearsal | Passed, including checkpoints 1, 64, and 127, idempotent replay, checksum-drift rejection, and unexpected-ledger rejection |
| Telehealth migration/schema regression | 26 passed; all 23 telehealth tables and V0282–V0286 invariants remained intact; Sprint 7 added no migration |
| Telehealth authorization proof | 20 passed, including absent staff identity and administrator workspace denial |
| Telehealth OpenAPI proof | 11 passed, including typed read-only workspace authentication, scope headers, 403/404 outcomes, and no body/idempotency input |
| Telehealth runtime-safety proof | 9 passed, including the bounded allowlist source boundary and 23/23 readiness tables |
| Prospective identity regression | 11 passed, including ten-way contention and zero canonical deltas |
| Real-PostgreSQL queue/consultation/workspace proof | 64 passed; one winner from each 20-way reservation/start race; age 17/121 rejected; owner success; non-owner 404; excluded fields absent; private no-store headers; both PHI permissions audited |
| Generated empty bootstrap verification | Passed; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 52 passed across 51 Markdown files and 180 relative links; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,741 nodes, 15,981 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, with migration evidence under `avenchart/artifacts/migration-resilience/`. They contain synthetic identifiers and coarse facts only. The existing default PostgreSQL volume was not mutated after a pre-existing V0200 checksum mismatch was detected; all Sprint 7 database evidence ran in the isolated `avtelehealthsp720260827` project.

## 3. Access, minimization, and audit results

The real API/PostgreSQL proof demonstrated that:

- age 17 and age 121 patient fixtures cannot create an established-patient request, while the normal adult journey remains successful;
- only the staff identity that owns the active consultation encounter can retrieve the projection; another authenticated physician receives the same 404 boundary as missing or out-of-scope work;
- the response includes only an opaque consultation identifier, current patient identity/callback facts, current visit facts, and up to 20 active allergies, medications, and problems;
- canonical patient, encounter, appointment, and request identifiers plus address, email, insurance, financial, employer, guardian, care-team, document, message, laboratory, prior-note, comment, credential, and inactive-list data are absent;
- documentation, prescribing, claims, and completion flags remain false, with no note, signature, prescription, billing, or claim mutation;
- successful access writes both `acl.encounters.auth.view` and `acl.patients.demo.view` audit decisions correlated to the opaque consultation; and
- the response is private and non-cacheable, while the frontend stores neither the projection nor room credential in session or local storage.

The React flow deliberately treats an empty list as “no active entry returned,” not as a negative history. It prompts verbal verification and keeps general chart navigation, diagnosis, documentation, order, reconciliation, prescription, claim, and completion actions absent.

## 4. Defects found and corrected by the evidence gate

The first live non-owner check found that the repository attempted to commit its repeatable-read transaction before disposing a no-row data reader. That converted the intended opaque 404 into an unhandled 500. The reader scope now closes before commit, and the independent second-physician journey returns 404.

The first cache assertion also showed that the global API PHI-cache middleware replaced the endpoint's `private` directive with its own strong `no-store, no-cache, max-age=0` value. The global boundary now includes `private` for every API response; the live response proves all four directives plus `Pragma` and `Expires` protections.

The runtime proof was repeated after both corrections and completed all 64 checks with zero failures. Its temporary non-owner physician identity, age fixtures, coverage mutation, reservation access, and busy-shift state were restored or removed in `finally` evidence checks.

## 5. Graph and change-impact review

The deterministic code-only Graphify index was rebuilt and its portability check passed. Because most telehealth files are new and untracked relative to the repository's current commit, `review-delta` associated only the tracked `Program.cs` change with a historical graph node and reported 80 transitive nodes in two files. Its test-gap hints therefore did not recognize the new colocated tests. They were treated as navigation prompts, not correctness conclusions. Direct backend, frontend, browser, runtime, migration, authorization, OpenAPI, and safety results above cover the actual changed boundaries.

## 6. Negative assertions and exclusions

Source/runtime inspection found no general chart repository or patient-chart response reuse, chart search/navigation, document/message/lab/prior-note query, clinical-output mutation, media transport, recording, transcription, payer/pharmacy call, or external vendor path in this slice.

This evidence does **not** authorize or claim:

- a complete or clinically reconciled patient chart, a confirmed negative history, diagnosis, documentation, order, signature, prescription, AVS, disposition, claim, billing, payment, or completed encounter;
- real consent, identity proofing, clinician licensure verification, minors/proxies/guardians, real audio/video, recording, transcription, or an external integration;
- that synthetic projection data is safe for clinical reliance without physician verification;
- production enablement, deployment, real people, real PHI, or patient care; or
- completion of independent clinical-safety, security/privacy, data, accessibility, legal, operational, or program-owner review.

## 7. Open review gates

Before a documentation, medication-reconciliation, prescribing, completion, or live-care slice, the following remain required:

1. independent clinical/legal review of the exact workspace allowlist, allergy/medication/problem semantics, age/location/consent/licensure gates, emergency fallback, and clinician-verification wording;
2. independent security/privacy review of composed access filters, owner binding, PHI audit behavior, cache controls, browser exposure, logs/telemetry, enumeration resistance, and break-glass policy;
3. independent data review of the repeatable-read projection, active-list definitions, ordering/bounds, patient identity linkage, encounter ownership, transaction behavior, and query plans;
4. independent accessibility/manual workflow review with supported assistive technology and realistic hardware/failure states;
5. program-owner review of this packet and another bounded decision before chart mutation, documentation, diagnosis, medication reconciliation, prescription, pharmacy, claim, billing, completion, real media, or external integration work; and
6. formal legal/compliance, clinical governance, credentialing, payer, and vendor gates before any production or patient-care enablement.

Until those reviews are recorded, Sprint 7 remains a disabled synthetic development slice and every production and patient-care gate remains closed.
