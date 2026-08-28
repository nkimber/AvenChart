# Sprint 5 connection-room-shell evidence packet

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0008](../decisions/0008-approved-sprint-05-connection-room-shell.md)  
Scope: Disabled, provider-neutral, synthetic-only device preflight, isolated waiting room, and participant-scoped connection grants without media or encounter start

## 1. Implementation trace

| Item | Implementation | Evidence |
|---|---|---|
| `TH-SP5-001` | `TelehealthDomain.cs` and V0285 add only `Reserved -> Connecting`, opaque session/preflight/grant/event aggregates, database isolation constraints, and append-only lifecycle evidence | state-machine tests; 241-migration recovery; 21-table readiness; destructive-mutation rejection |
| `TH-SP5-002` | `ITelehealthVideoProvider` and `SyntheticTelehealthVideoProvider` generate process-ephemeral, participant/session/role-bound credentials through a `NON_PRODUCTION` adapter with no network or media dependency | three adapter tests; runtime source-boundary proof; Production rejection; exact in-process replay |
| `TH-SP5-003` | `TelehealthVideoService` and `TelehealthVideoRepository` issue a patient grant only to the portal owner of a reserved request with a current reservation | absent/cross-patient denial; failed-preflight rejection; lease/state/version checks; hash-only database proof |
| `TH-SP5-004` | The physician route requires physician role, treatment purpose, staff/facility scope, active shift, and ownership of the active reservation | administrator denial; active-reservation-owner success; distinct patient/physician credentials for one opaque session |
| `TH-SP5-005` | `devicePreflight.ts`, patient and physician workspaces, API transport, and telehealth CSS provide explicit camera/microphone checks, immediate track release, coarse outcomes, resilient retry, and private waiting-room wording | 17 focused frontend tests; 40 cross-browser accessibility journeys; four stale-action recovery journeys; credential non-render/non-storage assertions |
| `TH-SP5-006` | Typed endpoints, runtime policy, 21-table health, V0285, OpenAPI/auth/migration/concurrency scripts, planning guardrail, and CI runtime workflow close the bounded evidence loop | all automated results below; full backend/frontend/browser and migration recovery regression |

## 2. Automated results

| Gate | Result |
|---|---|
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 118 passed, 0 failed, 0 skipped |
| Focused telehealth backend tests | 65 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Frontend lint and TypeScript | Passed |
| Full frontend unit/component tests | 48 files, 223 tests passed |
| Focused telehealth frontend tests | 5 files, 17 tests passed |
| Production frontend build and budget | Passed; 246,395 / 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility | 40 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Cross-browser stale-action recovery | 4 passed across desktop/mobile Chromium, Firefox, and WebKit |
| Full 241-migration empty/populated/interruption/recovery rehearsal | Passed, including checkpoints 1, 64, and 127, idempotent replay, checksum drift, unexpected-ledger rejection, and V0285 |
| Telehealth migration/schema proof | 20 passed, including four V0285 tables, append-only/no-delete enforcement, no-capture, role, expiry, idempotency, and isolation constraints |
| Telehealth authorization proof | 17 passed, including absent/cross-patient and non-physician connection denial |
| Telehealth OpenAPI proof | 9 passed, including separate patient/physician auth, typed preflight, idempotency, and conflict contracts |
| Telehealth runtime-safety proof | 7 passed, including disabled defaults, Production rejection, no media/vendor/encounter path, and 21/21 readiness tables |
| Prospective identity regression | 11 passed, including ten-way contention and zero canonical deltas |
| Real-PostgreSQL queue/connection proof | 40 passed twice consecutively; one winner among 20 callers, independent role grants, hash-only storage, capture disabled, zero encounter delta, and post-proof fixture/reservation cleanup |
| Generated empty bootstrap verification | Passed; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 50 passed across 45 Markdown files and 156 relative links; three controlled negative mutations returned nonzero |

Machine-readable runtime results are written under `avenchart/artifacts/telehealth/` and the full migration result under `avenchart/artifacts/migration-resilience/`. They contain synthetic identifiers and coarse facts only; they are local evidence outputs, not source authority.

## 3. Connection and privacy results

The real API/PostgreSQL proof demonstrated that:

- an incomplete device preflight returns validation failure and issues no grant;
- another portal patient receives the same not-found boundary and cannot create or observe the room;
- a patient command moves one request from `Reserved` to `Connecting` exactly once and exact replay returns the same process-ephemeral credential;
- reuse of the command key with changed preflight content returns conflict;
- an administrator cannot use the physician route, while the reservation-owning physician receives a different role-scoped credential for the same opaque session;
- the database contains the SHA-256 of the returned credential, never the plaintext credential;
- session payloads contain opaque identifiers and fixed disabled values for recording, transcription, and media transport;
- exactly one request transition event and two participant video events are appended; and
- the encounter count delta is zero and the patient status projection exposes neither physician identity nor grant material.

The browser preflight is explicitly initiated by the participant. Tests prove all acquired media tracks are stopped immediately. Only browser/camera/microphone/speaker booleans and a coarse network bucket cross the API; device labels, IP addresses, media, names, symptoms, coverage, diagnoses, medication, and claim data do not enter the session-provider payload.

## 4. Migration, recovery, and repeatability

V0285 is additive. The packaged migrator provisioned an empty database from the verified bootstrap, applied and replayed all 241 migrations, recovered after three deliberate committed-migration interruptions, rejected checksum drift and an unexpected ledger entry, and exercised populated-state preservation. Database checks enforce one request/reservation pairing, role values, a maximum five-minute grant, one active participant/role grant, `NON_PRODUCTION`, and disabled capture. Append-only/no-delete triggers reject destructive evidence mutation.

The queue proof initially exposed a harness repeatability defect: its source-coverage mutation was not restored and an older ready request could win a later run. The harness now assigns the current synthetic proof deterministic priority, restores the coverage source in `finally`, and releases its reservation and room access after assertions. Two immediate complete runs both passed all 40 checks. No database row or evidence event is deleted.

The required Graphify delta review reported 63 changed nodes and 80 impacted nodes, primarily through the existing `Program.cs` and `App.tsx` integration hubs. Its graph did not associate the new uncommitted tests with the feature files, so the reported test-gap hints were not treated as correctness conclusions. Direct unit, contract, PostgreSQL, authorization, migration, browser, and runtime-safety proofs cover those surfaces.

## 5. Negative assertions and exclusions

Source/runtime inspection found no managed video SDK, signaling server, TURN service, WebRTC media transport, provider webhook, recording, transcription, summarization, persistent media, face recognition, vendor training, chat, attachment, or notification path. The returned credential is not placed in a URL, log, browser storage, rendered DOM, or durable evidence JSON.

This evidence does **not** authorize or claim:

- actual audio/video communication, a selected production video provider, a BAA, vendor security/accessibility certification, or provider reliability;
- consultation start, encounter creation, expanded chart access, clinical notes, diagnosis, disposition, AVS, prescribing, pharmacy, claims, payment, or any external integration;
- audio-only fallback, interpreters, observers, additional participants, invisible staff presence, or patient-to-patient presence;
- production enablement, deployment, real people, real PHI, or patient care; or
- completion of any independent clinical-safety, security/privacy, data, accessibility, legal, operational, or program-owner review.

## 6. Open review gates

Before any live connection capability or clinical encounter work, the following remain required:

1. independent security/privacy review of credential lifecycle, browser exposure, replay scope, participant isolation, abuse controls, CSP, logs, and provider trust boundaries;
2. independent data review of V0285 constraints, retention, terminal cleanup, recovery, indexes, session/grant expiry, and future encounter linkage;
3. independent accessibility/manual device review across supported hardware, browsers, assistive technology, failure states, consent wording, and cognitive load;
4. clinical/legal/compliance confirmation of modality, patient-location, consent, emergency escalation, and when a consultation legally/clinically begins;
5. vendor/procurement review and BAA before any real media provider is selected or contacted; and
6. program-owner review of this packet plus another explicit decision before consultation, encounter, media, or external-vendor implementation.

Until those reviews are recorded, Sprint 5 remains a disabled synthetic development slice and every production and patient-care gate remains closed.
