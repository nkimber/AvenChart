# Decision 0058: Sprint 55 applicant request connection room

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of an applicant-originated request already reserved by its exact Sprint 48 synthetic rendering candidate to run a user-initiated local device-capability check and prepare the existing private synthetic connection room. The existing connection transaction may create one non-production session, one coarse preflight result, and one short-lived patient-role waiting-room grant; move the request from `Reserved` to `Connecting` with one version increment; and mark the exact scheduled appointment `Arrived`.

This is a private local waiting-room shell. It is not WebRTC or another media transport, communication, recording, consent, a consultation, an encounter, diagnosis, treatment, prescribing, billing, or external integration.

## 2. Ownership and provenance rule

The command must bind the applicant identifier and hashed access key to the unexpired `SyntheticRequestCreated` version 26 applicant, its exact portal-disabled canonical patient shell, the source-linked request, current Sprint 52 queue authorization, exact candidate-owned active reservation, one `Reserved` queue entry, and the same scheduled appointment. The authorization and reservation must still be current at database time.

The server derives a domain-separated participant subject hash from the applicant identifier and stored access-key hash. No raw access key or applicant identifier is stored as the session participant subject. Staff, portal, foreign-applicant, wrong-request, expired, unmatched-candidate, stale-version, and drifted-provenance attempts fail closed.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, branded-practice/facility scoped, and access-key owner restricted.
2. The device check is initiated by the applicant. It collects only coarse booleans and a normalized `unknown`, `limited`, or `good` connection indication. It sends no device ID, device name, label, browser fingerprint, IP address, media, or recording.
3. Temporary camera and microphone tracks used by the local check stop in all success and failure paths before the result is returned.
4. The command requires an idempotency key and exact request version. Replay returns the same result; changed content, stale state, or conflicting ownership fails without partial mutation.
5. The transaction rebinds the exact applicant, patient shell, request, queue authorization, reservation owner, active shift, appointment, practice, and facility under database locks.
6. The session is fixed to `NON_PRODUCTION` `WaitingRoom`; media transport, recording, and transcription remain false. The short-lived grant credential is returned once over the private response, while only its hash is stored.
7. The browser must not render or persist the credential. It may retain only non-sensitive in-memory expiry, waiting-room message, and limitations for the current view.
8. The applicant status projection may expose only `Connecting`/`ConnectionRoom`, waiting-room entered true, and media/communication false after exact current session, grant, appointment, reservation, request-event, and video-event provenance validation.
9. The applicant projection discloses no physician identity, credential, provider ID, NPI, applicant ID, patient ID, member/coverage data, exact queue position, or wait promise.
10. The transaction creates no communication channel, consultation, chart workspace, consent, encounter, diagnosis, treatment, prescription, claim, message, integration, or external call.
11. Unit, transport, browser/accessibility, authorization, OpenAPI, runtime, live GA/CA/FL ownership/provenance/concurrency, regression, planning, and Graphify evidence are required.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_CONNECTION_ROOM` version 1 in `NON_PRODUCTION` mode. |
| Applicant command | POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/{requestId}/connection-grants`; applicant access key and idempotency key required. |
| Input | Exact request version plus coarse browser/camera/microphone/speaker/network/synthetic booleans only. |
| Entry state | Exact applicant-owned request `Reserved`; exact active candidate-owned reservation and shift; current queue authorization; same scheduled appointment. |
| Atomic result | One existing-format `WaitingRoom` session, preflight, and active patient-role grant; request `Connecting` v+1; appointment `Arrived`; append-only events. |
| Applicant projection | `Connecting` maps to `ConnectionRoom`; waiting-room entered true; media session and communication false. |
| Secret handling | Credential returned only to the caller, hash stored server-side, never rendered or persisted by the browser. |
| Outstanding gates | Media/signaling, communication, consultation, chart access, clinician-obtained consent, encounter, care, prescribing, claims, integrations, completion, cancellation, and production. |

## 5. Explicit exclusions

This decision does not authorize WebRTC, WebSocket, SignalR, SIP, telephony, chat, recording, transcription, vendor media, physician identity disclosure, connection to a clinician, a consultation, chart access, consent, an encounter, diagnosis, advice, treatment, prescribing, pharmacy transmission, billing, claims, FHIR/X12/NCPDP messages, payer or pharmacy calls, real people or PHI, or production enablement.

## 6. Stop conditions and rollback

Stop if a non-owner can prepare the room; if a request without the exact active candidate reservation can enter `Connecting`; if raw access or grant credentials are stored, rendered, logged, or returned by status; if local tracks remain active; if session/request/appointment/events can partially commit; if status accepts orphaned or drifted evidence; or if media, communication, consultation, consent, encounter, care, financial, integration, external, or production consequence occurs. Rollback removes the applicant command and `Connecting` projection while retaining governed append-only session/grant evidence.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic, applicant-owned waiting-room boundary above.

## References

- [Video, waiting room, and realtime communication](../10-video-realtime-and-communications.md)
- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0057](0057-approved-sprint-54-applicant-request-clinician-reservation.md)
- [Sprint 55 plan](../backlog/sprint-55-applicant-request-connection-room.md)
