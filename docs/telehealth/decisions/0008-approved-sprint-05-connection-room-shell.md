# Decision 0008: Sprint 5 connection-room shell authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Add a provider-neutral, synthetic-only connection-room boundary after a physician has atomically reserved an eligible request:

```text
Reserved request + active reservation
  -> participant runs explicit device preflight
  -> server revalidates patient or reservation-owning physician
  -> deterministic NON_PRODUCTION video adapter prepares an opaque session
  -> one participant/session/role-scoped, short-lived grant is issued
  -> request moves to Connecting
  -> private synthetic waiting-room status
```

The slice may demonstrate WebRTC browser capability and device permission checks, but it does not transport media or clinically start a consultation.

## 2. Authorized implementation surfaces

Changes may use the existing telehealth paths plus:

```text
avenchart/database/migrations/V0285__telehealth_connection_room_shell.sql
docs/telehealth/decisions/0008-approved-sprint-05-connection-room-shell.md
docs/telehealth/backlog/sprint-05-connection-room-shell.md
docs/telehealth/backlog/sprint-05-evidence.md
```

The smallest telehealth backend, frontend, OpenAPI, health, migration, authorization, runtime-evidence, planning-validation, CI, and generated-bootstrap edits needed to connect and prove the slice are authorized.

## 3. Required controls

1. The feature remains disabled by default, simulator-only, and rejected in Production.
2. A session can be prepared only for a current `Reserved` or `Connecting` request with an active, unexpired reservation in the configured practice and facility.
3. A patient grant requires the current patient portal identity to own the request. A physician grant requires the authenticated physician to own the active reservation and shift.
4. Grants are random/cryptographic, short-lived, single participant/session/role scoped, returned only in a response body, stored only as SHA-256, and never placed in a URL or log.
5. Exact command replay is idempotent during the active simulator process; changed-content reuse fails closed. A simulator restart invalidates prior ephemeral credentials.
6. Device preflight is user initiated, releases test media tracks immediately, and records only coarse capability outcomes—never device labels, IP addresses, media, or chart content.
7. Session/provider payloads use opaque identifiers and exclude names, symptoms, coverage, diagnoses, medication, prescription, and claim data.
8. Recording, transcription, summarization, persistent media, face recognition, and vendor training are represented as disabled and have no implementation path.
9. A provider event or browser connection cannot start a clinical encounter or change state beyond the server-owned `Reserved -> Connecting` command.
10. Patient waiting-room status reveals no other patient or physician identity. Physician context remains limited to the request already reserved to that physician.
11. Session, preflight, grant, and lifecycle evidence is practice/request bound, reconstructable, and protected by database constraints and append-only events.
12. API, authorization, concurrency, migration/recovery, privacy, accessibility, failure-recovery, and full regression evidence must pass without weakening prior gates.

## 4. Explicit exclusions

This decision does not authorize:

- a live managed video provider, vendor SDK, signaling server, TURN service, media transport, webhook, SignalR domain transition, recording, transcription, chat, attachment, or notification;
- consultation start, encounter creation, chart access expansion, clinical notes, diagnosis, disposition, AVS, prescribing, pharmacy, claim, or payment work;
- audio-only fallback, additional participants, interpreters, observers, invisible staff join, or patient-to-patient presence;
- a production provider selection, BAA conclusion, security/accessibility certification, device compatibility claim, or provider uptime claim;
- production enablement, deployment, real people, real PHI, patient care, or closure of an independent review gate.

## 5. Stop conditions and rollback

Stop if a participant can obtain another participant's grant, a grant or media-derived datum is persisted/logged in plaintext, an expired/non-owner reservation can create a session, another patient's presence is disclosed, the browser or adapter can start an encounter, any media/vendor destination is contacted, Production accepts the simulator, or prior evidence regresses. Rollback disables/removes the routes and UI; additive evidence remains dormant for governed cleanup.

## 6. Approval record

The program owner directed Codex to implement the complete approved telehealth plan, approved all current decisions, authorized modification of the generated bootstrap, and authorized uninterrupted long-running work while unavailable. This record applies that authority only to the bounded non-production slice above. It does not broaden authority to production, external vendors, real patient care, or self-certification of independent reviews.

## References

- [Decision 0003](0003-proposed-sprint-01-synthetic-foundation.md)
- [Decision 0006](0006-approved-sprint-03-patient-queue-transparency.md)
- [Workflow state machines](../03-workflows-and-state-machines.md)
- [Video and realtime specification](../10-video-realtime-and-communications.md)
- [Sprint 5 plan](../backlog/sprint-05-connection-room-shell.md)

