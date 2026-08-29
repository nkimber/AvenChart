# Sprint 55 plan: applicant request connection room

Status: Implemented and automated verification complete under [TH-DEC-0058](../decisions/0058-approved-sprint-55-applicant-request-connection-room.md); independent review and every production gate remain open

## Goal

Let the access-key owner of an exactly reserved applicant-originated request run a safe local device check and enter the existing private synthetic waiting-room shell. Reuse the proven connection transaction while keeping media, communication, consultation, consent, encounter, care, integrations, and production closed.

## Delivery boundary

- Add an applicant-key-only idempotent connection-preparation route bound to both applicant and request identifiers.
- Rebind the unexpired applicant, portal-disabled patient shell, exact current queue authorization, candidate-owned active reservation/shift, queue entry, appointment, practice, and facility.
- Derive a domain-separated participant subject hash without storing raw applicant or access-key values.
- Reuse the existing non-production connection transaction to create one waiting-room session, coarse preflight, short-lived patient-role grant, `Reserved -> Connecting` request transition, `Scheduled -> Arrived` appointment transition, and append-only events.
- Run a user-initiated local camera/microphone/speaker/browser check, stop temporary tracks on every path, and send only coarse capability evidence.
- Never render or persist the returned join credential; retain only non-sensitive in-memory waiting-room confirmation.
- Extend applicant-owned status to exact-provenance `Connecting`/`ConnectionRoom` with waiting-room true and media/communication false.
- Add unit, API, browser/accessibility, authorization, OpenAPI, runtime, GA/CA/FL live, concurrency/replay, regression, planning, and Graphify evidence.

## State and evidence contract

```text
SyntheticRequestCreated v26 (unchanged)
  + request Reserved v14+
  + exact current Sprint 52 authorization
  + exact candidate-owned active reservation and shift
  + queue Reserved
  + same scheduled appointment/provider
  + applicant access-key ownership
  + coarse user-initiated local preflight
  -> one NON_PRODUCTION WaitingRoom session
  -> one hashed short-lived patient-role grant
  -> request Connecting v+1
  -> appointment Arrived
  -> applicant sees ConnectionRoom
  -> mediaSessionCreated = false
  -> communicationStarted = false
```

No media bytes, signaling, recording, transcription, chat, consultation, chart access, consent, encounter, diagnosis, treatment, prescription, claim, integration, message, or external action is created.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership | Only the exact applicant access-key owner may prepare the room; staff/portal/foreign/wrong-key/wrong-request attempts fail closed. |
| Provenance/freshness | Exact applicant/patient/request/authorization/candidate/reservation/shift/queue/appointment/practice/facility chain remains current at database time. |
| Atomicity/replay | Session, preflight, grant, request/appointment transitions, and events are all-or-nothing; unchanged replay is stable and changed content conflicts. |
| Concurrency | Concurrent unchanged calls converge on one session/preflight/grant/event set without partial duplicates. |
| Device privacy | User initiated; temporary tracks stop; only coarse evidence sent; no device labels/IDs, browser fingerprint, IP, or media retained. |
| Credential privacy | Plaintext credential is not stored server-side and is neither rendered nor persisted in browser storage. |
| Applicant status | Connecting requires exact live provenance; reports waiting-room true, media/communication false, and no physician identity, exact position, or wait promise. |
| Regression | Backend, frontend, four-engine browser, route/accessibility, runtime, authorization, OpenAPI, migrations/recovery, established-patient lifecycle, planning, Graphify, and cleanup. |

## Gate preserved

Sprint 56 must separately authorize any applicant-originated clinician connection, communication, consultation-start, chart-workspace, or consent boundary. Encounter, care, real coverage and financial routing, prescribing, claims, integrations, completion, cancellation, independent review, and production remain open.
