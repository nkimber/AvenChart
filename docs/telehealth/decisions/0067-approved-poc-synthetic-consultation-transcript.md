# Decision 0067: POC synthetic consultation transcript

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-30

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## Authorized outcome

Permit a request owner and the exact assigned physician to exchange plain-text **synthetic demonstration** messages only while the same synthetic consultation is active. The POC supports a visible patient–clinician communication experience without introducing a real messaging or media service.

## Required controls

1. Telehealth remains disabled by default, rejected in Production, synthetic-only, and restricted to the configured branded practice/facility and GA/CA/FL runtime boundary.
2. A patient can read or append messages only for their own active `InConsultation` request. A physician can read or append messages only for the active consultation they own at the configured facility.
3. Every append requires an explicit synthetic-data confirmation. Messages are 1–1000 printable characters, append-only, scoped to one consultation, and have `legal_effect=false`.
4. The UI labels the transcript as POC-only and synthetic, makes no care assertion, and includes emergency guidance. It uses ordinary short HTTP polling while visible; polling pauses with the page hidden.
5. Realtime delivery, WebRTC, audio/video transport, recording, transcription, attachments, notifications, patient delivery, clinical documentation, prescriptions, billing, claims, integrations, and external transport remain disabled.

## Stop conditions and rollback

Stop if a user can access a transcript outside its request/consultation ownership, append after wrap-up, bypass synthetic confirmation, or cause any external delivery. Rollback removes the transcript endpoints and UI panel; already appended synthetic evidence remains immutable for the POC audit trail.

## Approval record

The program owner authorized continued POC development using best judgment and approved current POC decisions. This record applies that authority only to this bounded non-production synthetic transcript slice.

## References

- [Workflow state machines](../03-workflows-and-state-machines.md)
- [Video, waiting room and communication](../10-video-realtime-and-communications.md)
- [Sprint 64 plan](../backlog/sprint-64-poc-synthetic-consultation-transcript.md)
