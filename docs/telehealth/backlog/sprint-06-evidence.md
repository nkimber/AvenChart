# Sprint 6 consultation-start handoff evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0009](../decisions/0009-approved-sprint-06-consultation-start-handoff.md)  
Scope: Disabled, synthetic-only handoff from the connection-room shell into one linked existing-system appointment and encounter, without clinical documentation or downstream clinical/financial capability

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP6-001` | Operational authorization creates one same-day scheduled/unassigned AvenChart appointment and reservation assigns the winning physician | request/appointment uniqueness; transactional authorization replay; appointment status/provider assertions; 20-way reservation race |
| `TH-SP6-002` | V0286 and `TelehealthDomain.cs` add `InConsultation`, immutable consultation context/start events, active-or-busy shift uniqueness, and sequential consultation membership within one shift | 242-migration recovery; 26 schema checks; append-only rejection; three consecutive real-database lifecycle runs on one shift |
| `TH-SP6-003` | `TelehealthConsultationService` and `TelehealthConsultationRepository` enforce physician role, treatment purpose, staff/facility ownership, current shift/reservation/request/version, fresh location and coverage, both participant grants, arrived appointment, and all affirmative checks | service tests; authorization proof; incomplete/state/non-physician negative cases; 20-way consultation-start race; exact replay and changed-key conflict |
| `TH-SP6-004` | Consultation start reuses `EncounterRepository.CreateInTransactionAsync` and atomically links the request, appointment, reservation, shift, room, context, and one existing encounter | one winner/one encounter; appointment/provider linkage; released reservation; ended/revoked room; zero note, signature, prescription, billing, and claim deltas; opaque response |
| `TH-SP6-005` | The clinician workspace supplies an explicit synthetic start checklist with retry-stable idempotency; the patient workspace renders a terminal privacy-bounded `InConsultation` projection | 18 focused frontend tests; 44 cross-browser accessibility journeys; stable-key 503 retry; zero terminal polling; no credential, clinician, or encounter-key disclosure |
| `TH-SP6-006` | Typed endpoint/OpenAPI, authorization, runtime policy, 23-table health, V0286, CI scripts, planning controls, runbook, and complete regressions close the bounded evidence loop | all automated results below |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 125 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 72 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 48 files, 224 tests passed |
| Focused telehealth frontend tests | 5 files, 18 tests passed |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility | 44 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Cross-browser stale-action recovery | 4 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Full 242-migration empty/populated/interruption/recovery rehearsal | Passed, including checkpoints 1, 64, and 127, idempotent replay, checksum drift, unexpected-ledger rejection, and V0286 |
| Telehealth migration/schema proof | 26 passed, including 23 tables, immutable consultation evidence, request-scoped uniqueness, sequential shift membership, and active-or-busy shift uniqueness |
| Telehealth authorization proof | 18 passed, including absent/cross-patient boundaries and administrator consultation-start denial |
| Telehealth OpenAPI proof | 10 passed, including physician-only scoped consultation start, affirmative input, idempotency, and bounded failure contracts |
| Telehealth runtime-safety proof | 8 passed, including disabled defaults, Production rejection, no clinical-output/media/payer/pharmacy path, and 23/23 readiness tables |
| Prospective identity regression | 11 passed, including ten-way contention and zero canonical deltas |
| Real-PostgreSQL queue/consultation proof | 55 passed on the final run after two preceding complete passes; 20 reservation callers and 20 start callers each produced one winner; the same shift appended contexts `0 -> 1 -> 2 -> 3` |
| Generated empty bootstrap verification | Passed; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 51 passed across 48 Markdown files and 168 relative links; three controlled negative mutations returned nonzero |

Machine-readable runtime results are written under `avenchart/artifacts/telehealth/` and the full migration result under `avenchart/artifacts/migration-resilience/`. They contain synthetic identifiers and coarse facts only; they are local evidence outputs, not source authority.

## 3. Transaction, access, and privacy results

The real API/PostgreSQL proof demonstrated that:

- operational authorization creates exactly one scheduled/unassigned appointment and reservation assigns it to the one winning physician;
- patient room entry marks that appointment arrived before consultation start;
- incomplete affirmative checks, a state inconsistent with current location evidence, a non-physician identity, stale version/state, expired access, and non-owned work cannot start the lifecycle;
- 20 concurrent start commands produce one success and 19 privacy-bounded not-found/conflict results;
- exact idempotent replay returns the same opaque consultation and request version while changed reuse returns conflict;
- one transaction creates one appointment-linked, physician-owned existing encounter and context, marks the appointment in room and request `InConsultation`, releases the reservation, marks the shift busy, and ends/revokes room access;
- consultation start creates no clinical note, encounter signature, prescription, billing row, or claim; and
- patient and public responses contain no sequential encounter key, clinician identity, credential, policy identifier, or internal coverage key.

The response explicitly reports `legalEffect=false` and every downstream capability flag as false. The UI retains a retry-stable command key after a synthetic 503, preserves the physician's checks, and does not persist the participant credential in session storage or render it in the DOM.

## 4. Migration, recovery, and repeatability

V0286 is additive to the current uncommitted feature line. The packaged migrator provisioned an empty database from the verified bootstrap, applied and replayed all 242 migrations, recovered after three deliberate committed-migration interruptions, rejected checksum drift and an unexpected ledger entry, and exercised populated-state preservation.

The first repeatability attempt exposed an incorrect one-consultation-per-shift uniqueness constraint. That would have prevented a physician from serving a second patient during the same shift. The constraint was removed while request, reservation, session, appointment, and encounter uniqueness remained intact; a non-unique shift/history index was added. A fresh migration/schema proof then passed, and three consecutive full queue/connection/consultation runs reused the same shift and appended exactly one context and encounter per run (`0 -> 1 -> 2 -> 3`). No consultation context or evidence event was deleted or rewritten; direct mutation attempts were rejected by database triggers.

The required Graphify delta review reported 178 changed nodes and capped the transitive result at 100 impacted nodes/104 files. It correctly identified `Program.cs`, `App.tsx`, and the shared frontend transport as integration hubs. Its committed code-only index did not associate the new untracked telehealth tests with the feature files, so its test-gap hints were treated as navigation prompts rather than correctness conclusions. Direct 125-test backend, 224-test frontend, 48-journey browser, 55-check real-database, authorization, OpenAPI, migration, and runtime-safety evidence covers those boundaries.

## 5. Negative assertions and exclusions

Source/runtime inspection found no real audio/video transport, recording, transcription, diagnosis, order, clinical-note, signature, prescription, pharmacy transmission, eligibility/network call, claim submission, billing, payment, or external-vendor path in this slice. No chart-read projection or expanded encounter access is granted by the opaque consultation response.

This evidence does **not** authorize or claim:

- a legally effective telehealth consent, identity proofing, clinician licensure determination, modality determination, or completed clinical encounter;
- chart review, clinical documentation, diagnosis, disposition, AVS, orders, prescribing, pharmacy selection/transmission, claims, billing, payment, or completion;
- actual audio/video communication, a production media provider, payer or pharmacy integration, provider directory, BAA, or vendor certification;
- production enablement, deployment, real people, real PHI, or patient care; or
- completion of independent clinical-safety, security/privacy, data, accessibility, legal, operational, or program-owner review.

## 6. Open review gates

Before any live clinical consultation or chart/documentation slice, the following remain required:

1. independent clinical/legal review of the exact consultation-start boundary, consent, location, licensure, emergency fallback, minors/proxies, modality, and documentation obligations;
2. independent security/privacy review of authorization composition, encounter access, credential lifecycle, audit content, logs, browser exposure, abuse controls, and trust boundaries;
3. independent data review of V0286 cardinalities, encounter/appointment status mapping, transaction recovery, retention, indexes, and future completion behavior;
4. independent accessibility/manual workflow review across supported assistive technology, hardware, failure states, and cognitive load;
5. program-owner review of this packet and another bounded decision before chart-read, documentation, clinical output, completion, real media, payer, pharmacy, or other external integration work; and
6. formal legal/compliance and vendor/procurement gates before any production or patient-care enablement.

Until those reviews are recorded, Sprint 6 remains a disabled synthetic development slice and every production and patient-care gate remains closed.
