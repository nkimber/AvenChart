# Decision 0031: Sprint 28 synthetic device preparation

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed Decision 0030 communication/access readiness to run the existing local browser device check and record one immutable coarse device-preparation receipt. The browser may temporarily request camera and microphone access, must stop every acquired track immediately, and may submit only four capability booleans plus `Unknown` or `Good` network quality and three explicit limitation acknowledgments.

This checkpoint records a client-reported preparation result only. It does not establish technology readiness, create a waiting room or media session, reserve a clinician, start communication, complete intake or consent, accept the patient, create a request or queue entry, authorize care, or contact anyone.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticCommunicationAccessReadinessRecorded` aggregate with exact prior provenance.
2. Every read/write rebinds the applicant, promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access receipt, original passing safety location, and verified callback source. Broken or cross-applicant provenance, patient drift, portal enablement, merge state, canonical insurance, or prior receipt drift fails closed.
3. The browser preflight uses secure-context, `RTCPeerConnection`, camera, microphone, and speaker capability checks. Temporary media tracks are stopped in a `finally` path. No media is transmitted, recorded, retained, or attached to a room.
4. The command accepts only `browserSupported`, `cameraAvailable`, `microphoneAvailable`, and `speakerAvailable` as true; network quality `Unknown` or `Good`; and three true acknowledgments: the result is client-reported, is not a readiness guarantee, and must be rerun before a real consultation.
5. Unsupported browser/media/speaker, denied permission, missing tracks, `Limited` or unknown vocabulary, partial acknowledgments, or browser-check failure produces no successful receipt or state transition. The UI provides retry and alternate-device guidance without claiming a clinical or operational denial.
6. No device label, device/group identifier, user-agent, IP address, ICE candidate, SDP, codec, resolution, bandwidth metric, hardware detail, media sample, recording, transcript, or free text crosses the route or is stored.
7. The receipt, `SyntheticCommunicationAccessReadinessRecorded -> SyntheticDevicePreparationRecorded` transition, and applicant event commit in one PostgreSQL transaction. Database constraints and a provenance trigger independently verify the complete prior chain, bounded values, acknowledgments, applicant/patient equality, portal-disabled state, and no-consequence flags.
8. Exact retry converges. Changed-key reuse, stale version/fingerprint, expired applicant, missing or altered provenance, patient/portal/canonical-insurance drift, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt and event.
9. `technologyReady=false`, `waitingRoomCreated=false`, `mediaSessionCreated=false`, `communicationStarted=false`, `supportArrangementCompleted=false`, `requestCreated=false`, `queueEntered=false`, and `careAuthorized=false` remain explicit. No applicant source field, patient, insurance, support, communication, financial, request, queue, appointment, encounter, clinical, prescribing, billing/claim, integration, or external-call record is created or changed.
10. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–27.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_DEVICE_PREPARATION`, version 1. |
| Entry state | `SyntheticCommunicationAccessReadinessRecorded`. |
| Server snapshot | Prior communication/access receipt, passing location, verified callback source, practice/facility, and SHA-256 fingerprint. |
| Client-reported result | Browser, camera, microphone, and speaker true; network quality `Unknown` or `Good`. |
| Required acknowledgments | Client-reported result; no readiness guarantee; rerun before consultation. |
| Resulting status | `SyntheticDevicePreparationRecorded`. |
| Data consequence | Immutable applicant receipt only; no source, patient, device-detail, room, media, communication, request, queue, or downstream record is changed. |

## 4. Explicit exclusions

This decision does not authorize real people or PHI; device fingerprinting; device labels/identifiers; IP/network diagnostics; media transport, storage, recording, transcription, or analysis; a WebRTC signaling service; a waiting room or grant; interpreter/accommodation fulfillment; technology readiness; remote support; portal access; patient-chart mutation; complete demographics/history/intake; allergy/medication collection; identity assurance; legal or clinician consent; practice acceptance; rendering-physician participation; canonical coverage; financial action; request/queue entry; appointment; encounter; care; prescribing; billing/claim; integration; or production enablement.

## 5. Stop conditions and rollback

Stop if temporary tracks are not always stopped; if media or a device/network identifier can leave the browser; if failed or limited preflight can advance; if a receipt is represented as technology readiness or communication availability; if a room, grant, media, support, intake, consent, acceptance, request, queue, or care consequence appears; if receipt/state/event provenance can diverge; if retry overwrites history; if any applicant source, patient, insurance, communication, financial, or downstream record changes; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable preparation evidence is not deleted as rollback; correction requires a separately reviewed forward workflow.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic client-reported device-preparation receipt. It does not substitute for accessibility, language/access service, privacy/security, legal, clinical, patient-registration, data, operational, media/vendor, interoperability, or production review.

## References

- [Video, realtime, and communications specification](../10-video-realtime-and-communications.md)
- [Security, privacy, consent, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Decision 0030](0030-approved-sprint-27-synthetic-communication-access-readiness.md)
- [Sprint 28 plan](../backlog/sprint-28-synthetic-device-preparation.md)
