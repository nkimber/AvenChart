# Sprint 28 synthetic device-preparation evidence

Status: Bounded automated evidence passing; independent media/device, patient-registration, privacy/security, accessibility, data, legal, clinical, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0031](../decisions/0031-approved-sprint-28-synthetic-device-preparation.md)  
Scope: Disabled, synthetic-only, applicant-owned recording of a coarse local browser/camera/microphone/speaker preparation result after communication/access readiness; no device identifiers, retained media, technology-readiness conclusion, waiting room, communication, patient mutation, complete intake, consent, acceptance, request, queue, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Full server-side provenance | Every read and write rebinds the applicant-key owner, practice/facility, successful promotion, portal-disabled unmerged patient shell, registration receipt, insurance-handoff receipt, communication/access receipt, passing safety location, and verified callback source. Missing, stale, expired, cross-applicant, patient-drifted, portal-enabled, merged, canonical-insurance, or receipt-drifted state fails closed. |
| Minimized server-owned context | The route exposes only policy/version, the allowed coarse result vocabulary, source fingerprint/version, and explicit limitations. Device labels or identifiers, user agent, IP/ICE/SDP/codec data, resolution, bandwidth, hardware detail, media, recordings, transcripts, and free text do not cross the route. |
| Local track-safe preflight | The patient explicitly starts the existing local preflight. Every acquired camera and microphone track is stopped immediately through the shared cleanup path; no media is transmitted, stored, recorded, or attached to a room. |
| Passing results only | Browser, camera, microphone, and speaker must all report true; network quality must be `Unknown` or `Good`; and all three client-report/no-guarantee/rerun acknowledgments must be true. Failed, denied, missing, partial, `Limited`, or unrecognized results cannot advance. |
| Preparation is not readiness | Technology readiness, waiting-room creation, media-session creation, communication start, support arrangement, request creation, queue entry, and care authorization remain explicitly false. |
| Atomic append-only evidence | V0305 constrains one receipt per applicant, exact practice-scoped replay, bounded coarse values, all acknowledgments, source snapshot, policy/expiry, and every no-consequence flag. Receipt, `SyntheticCommunicationAccessReadinessRecorded -> SyntheticDevicePreparationRecorded`, and the applicant event commit in one transaction and reject update/delete. |
| Replay and contention | Exact retry returns the first immutable result. Changed-key reuse, stale version/fingerprint, a second semantic command, and concurrent first writers fail closed with at most one receipt and one event. |
| Patient UX | The promoted-applicant flow explains permissions and limitations, runs the local check only on explicit action, reports coarse results only, blocks failed or limited checks, requires all acknowledgments, preserves the exact command after an ambiguous failure, supports retry/alternate-device guidance, and stores no result in browser storage. |
| Consequence boundary | Applicant source fields, patient and insurance rows, device/media/support/communication records, portal, completed intake, consent, acceptance, financial records, request, queue, appointment, encounter, care, prescribing, claim, integration, and external-call consequences remain unchanged or false. |
| Runtime and governance | Device preparation requires 45 tables. Decision 0031, the [Sprint 28 plan](sprint-28-synthetic-device-preparation.md), safeguard TH-SG-033, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, CI invocation, and planning validation are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused device-preparation policy tests | 13 passed, covering normalized coarse results, all mandatory affirmations, deterministic snapshot behavior, invalid/limited input, and hard-false media/readiness/care consequences |
| Full backend tests | 351 passed, 0 failed, 0 skipped |
| Full frontend tests | 53 files and 277 tests passed |
| Frontend lint, TypeScript, production build, and bundle budget | Passed; 137 JavaScript chunks checked and the initial bundle was 246,399 of 256,000 bytes |
| Cross-browser telehealth accessibility/recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the promoted-applicant journey covers local track cleanup, coarse-only display, failure/limited blocking, all acknowledgments, exact ambiguous retry, storage minimization, focus, reflow, and automated WCAG checks |
| Full migration and recovery rehearsal | 261 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of V0305 checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 84 checks passed; V0282–V0305, all 45 telehealth tables, all 38 append-only triggers, policy/expiry/no-consequence constraints, full provenance guards, and append-only behavior passed |
| Telehealth authorization proof | 72 checks passed, including absent applicant key and portal-session substitution for both device-preparation operations, patient/staff/physician separation, purpose of use, and practice/facility isolation |
| Telehealth OpenAPI proof | 39 checks passed, including applicant-only GET/POST security, private reads, required idempotency, exact coarse typed input, explicit false readiness/media output, and identifier minimization |
| Telehealth runtime-safety proof | 29 top-level checks passed; 45-table synthetic readiness was healthy and no media, technology readiness, communication, patient mutation, care, downstream, integration, or outbound path was introduced |
| Live synthetic device-preparation proof | 12 checks passed for Georgia, California, and Florida: minimized read; ownership/partial/limited/stale/fingerprint/drift rejection; exact replay and changed/second-command rejection; eight-way contention; three-state parity; append-only evidence; provenance; and zero patient/downstream delta |
| Shared queue/lifecycle stress regression | 134 checks passed, including one winner among 20 concurrent reserve-next and consultation-start callers, authorization/video/consultation/documentation/pharmacy/prescription/disposition boundaries, audit evidence, and restoration of mutable fixtures |
| Planning/backlog validator | Validator v2.5 passed 73 checks with Decision 0031 and all 33 safeguards; 114 Markdown files and 368 relative links were clean, and all three controlled negative mutations were rejected |
| Generated bootstrap | Deterministic regeneration and verification passed with unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2`; the empty-database bootstrap plus V0001–V0305 produced the complete current schema |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact validation passed. Review-delta surfaced the shared `Program.cs` and `App.tsx` hubs. The committed code-only index does not include the still-uncommitted Sprint 28 files, so its reported test gaps are an indexing limitation addressed directly by the 351 backend tests, 277 frontend tests, live PostgreSQL proof, and 56 browser journeys above. |

Machine-readable live results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-device-preparation.json`. The result contains deterministic synthetic IDs and coarse false/true preparation facts only; it contains no canonical patient ID, device label or identifier, user agent, IP/ICE/SDP/codec data, precise network metric, media, recording, transcript, contact value, protected insurance value, clinical history, or external-provider response.

The authoritative live run used a newly created disposable `avenchart_test_telehealth_s28` database and an exact rebuilt API container. Readiness independently reported 261 applied migrations, V0305 as the latest packaged migration, and 45 of 45 telehealth tables before the application proof. The final database inspection found three preparation receipts, zero receipts with any consequential flag true, and the patient trigger enabled. The normal database remained unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`. The isolated database and its exact API container were then removed; both exact-name absence checks passed.

## 3. Proven boundaries

- only a current `SyntheticCommunicationAccessReadinessRecorded` applicant with exact promotion, patient, registration, insurance-handoff, communication/access, safety-location, callback, practice, and facility provenance can read or record preparation;
- only four coarse true capability booleans, `Unknown` or `Good` network quality, three mandatory acknowledgments, and explicit false consequence facts cross the route;
- every temporary media track is stopped immediately and no device identifier or media content is retained or transmitted;
- one successful command, including concurrent first writers, produces one receipt, one event, and one monotonic state transition; and
- applicant source, patient, insurance, device, media, support, communication, portal, intake, consent, acceptance, financial, request, queue, appointment, encounter, care, prescribing, claim, integration, and external-action records remain unchanged.

## 4. Evidence-gate observations

The first OpenAPI sealing run reported one failed minimization assertion because its test regex matched the substring `phone` inside the permitted capability name `microphoneAvailable`. The proof was corrected to compare exact property names against the prohibited set; the unchanged API schema then passed all 39 checks. This was a test precision correction, not an API-contract expansion.

The first full browser attempt used the suite's default `MOD-PAT-0004` portal identity, which was intentionally absent from the minimal disposable database. The API correctly rejected the login. The final unchanged 56-test run explicitly selected the seeded `MOD-PAT-0012` synthetic portal fixture and passed across all four browser projects. Failed environment-configuration attempts are not counted as final evidence.

The authorization and queue suites required two known synthetic portal accounts and the two established-patient insurance rows used by the shared harness. Those exact fixtures were copied read-only from the normal synthetic database into the disposable database; the normal database was not changed.

## 5. Rollback and stop evidence

- base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- missing, stale, expired, cross-applicant, portal-enabled, merged, patient-drifted, canonical-insurance, failed/limited/partial, or provenance-incomplete state cannot record preparation;
- immutable preparation evidence is not destructively rolled back; correction requires a separately reviewed forward migration/workflow;
- disabling/removing the routes and patient panel leaves the synthetic patient shell portal-disabled and without technology readiness, a room, communication, or care capability; and
- stop conditions include retained tracks, device/media/network identifiers crossing the route, failed or limited preflight advancing, preparation represented as readiness, any room/media/communication/request/queue/care consequence, provenance divergence, history overwrite, or any earlier safeguard regression.

## 6. Open review gates

Independent media/WebRTC/device, language/access, disability-access, patient-registration, privacy/security, accessibility, data, legal/regulatory, licensed clinical/medical-director, interoperability, operations/support, and program-owner packet reviews remain open. Real media/signaling/communications, interpreter or accommodation workflows, remaining demographics/history, completed intake, clinician disclosure/consent, practice acceptance, request/queue entry, appointment, care, prescribing, billing/claim, pharmacy transmission, and every production action require later separately bounded decisions.
