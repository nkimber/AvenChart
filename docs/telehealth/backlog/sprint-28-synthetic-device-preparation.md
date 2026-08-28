# Sprint 28: synthetic device preparation

Status: Approved for bounded implementation by [TH-DEC-0031](../decisions/0031-approved-sprint-28-synthetic-device-preparation.md)  
Scope: Applicant-owned immutable receipt for a coarse, local, client-reported browser/camera/microphone/speaker check after communication/access readiness; no device identifiers, media transport, waiting room, technology readiness, complete intake, consent, acceptance, request, queue, care, communication, external integration, or production use

## 1. Outcome

Add the next nonclinical preparation checkpoint by reusing the existing privacy-minimized browser preflight. Temporary media tracks are stopped immediately. Only a passing coarse result and three limitation acknowledgments can be recorded, and the result remains explicitly insufficient to establish technology readiness or start a telehealth session.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP28-001` | Add a server-owned preparation snapshot bound to the complete communication/access chain, with deterministic fingerprinting and no device or network identifiers. |
| `TH-SP28-002` | Add applicant-key protected private/no-store retrieval after communication readiness, returning only policy, allowed coarse result vocabulary, and explicit limitations. |
| `TH-SP28-003` | Add one idempotent atomic command for four true capability booleans, `Unknown` or `Good` network quality, and three mandatory limitation acknowledgments. |
| `TH-SP28-004` | Add append-only receipt/event provenance bound to the promotion, patient shell, registration, insurance, communication/access, safety location, callback, practice/facility, and aggregate version. |
| `TH-SP28-005` | Add an accessible patient panel that runs the local device check, always stops tracks, explains permissions and limitations, blocks failed/limited results, supports retry and alternate-device guidance, preserves an ambiguous API retry, and stores no result in browser storage. |
| `TH-SP28-006` | Prove source/access/version/provenance isolation, bounded vocabulary, track cleanup, failure behavior, all acknowledgments, exact replay, changed replay, contention, append-only evidence, zero source/patient/device/media/communication/downstream delta, migration/bootstrap, and full regression. |

## 3. Entry gate

The exact applicant must be unexpired and `SyntheticCommunicationAccessReadinessRecorded`; the successful promotion, portal-disabled unmerged patient shell, registration, insurance-handoff, communication/access receipt, original passing safety evaluation, and verified callback source must still exist and agree; no canonical insurance may exist; and the stored practice/facility/applicant/patient relationships must remain unchanged.

## 4. Exit boundary

Sprint 28 ends at `SyntheticDevicePreparationRecorded`. The receipt records only client-reported coarse preparation. It is not technology readiness, a device certification, a network-quality guarantee, a waiting room or media session, communication, complete intake, legal consent, practice acceptance, request creation, queueing, or care authorization.

## 5. Verification matrix

| Area | Required evidence |
|---|---|
| Browser policy | Secure-context/WebRTC/media/speaker checks; all acquired tracks stopped; no labels, IDs, samples, user agent, IP, ICE, SDP, codec, resolution, or precise network metrics. |
| Server policy | Four true capabilities, `Unknown`/`Good` only, three true acknowledgments, deterministic source fingerprint, and hard-false readiness/room/media/communication/request/queue/care output. |
| Database | Prior-chain mismatch, failed/limited result, stale/expired/canonical-insurance/portal-enabled rejection, exact replay, changed replay, concurrent one-receipt convergence, append-only evidence, and zero source/patient/media/communication/downstream delta. |
| HTTP | Applicant-only private/no-store reads/writes, required idempotency, typed bounded input/output, bounded failures, and hidden device/contact/patient/insurance identifier exclusion. |
| UI | Loading/error/retry, permission explanation, explicit start, visible stopped-track statement, limitation acknowledgments, disabled submit, failure/alternate-device guidance, confirmed outcome, reflow, focus, and no browser persistence. |
| Regression | Backend/full frontend/lint/build/E2E, migrations/recovery/bootstrap, runtime/authorization/OpenAPI/live safety, planning validator, and Graphify portable review. |
