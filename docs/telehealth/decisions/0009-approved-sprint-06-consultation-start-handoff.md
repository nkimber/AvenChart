# Decision 0009: Sprint 6 consultation-start handoff authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Add a transactionally linked, synthetic-only handoff from the connected request into AvenChart's existing appointment and encounter systems:

```text
operational authorization -> one same-day scheduled telehealth appointment
reservation -> assign the reservation-owning physician
patient waiting-room entry -> appointment Arrived
physician start command + fresh active participant grants + affirmative start checklist
  -> one existing AvenChart encounter linked to that appointment
  -> one telehealth consultation context
  -> request Connecting -> InConsultation
  -> appointment In room
```

This is lifecycle and linkage evidence only. It does not represent a real consultation, real consent, actual video, chart review, documentation, diagnosis, prescribing, billing, or patient care.

## 2. Authorized implementation surfaces

Changes may use the existing telehealth paths plus:

```text
avenchart/database/migrations/V0286__telehealth_consultation_start_handoff.sql
docs/telehealth/decisions/0009-approved-sprint-06-consultation-start-handoff.md
docs/telehealth/backlog/sprint-06-consultation-start-handoff.md
docs/telehealth/backlog/sprint-06-evidence.md
```

The smallest backend, frontend, appointment/encounter reuse, OpenAPI, health, migration, authorization, runtime-evidence, planning-validation, CI, and generated-bootstrap edits needed to connect and prove this slice are authorized.

## 3. Required controls

1. The feature remains disabled by default, synthetic-only, and rejected in Production.
2. Queue authorization creates exactly one deterministic, same-day immediate-telehealth appointment in the existing appointment table, initially scheduled and unassigned; replay creates no duplicate.
3. Atomic reservation assigns only the reservation-owning physician. Pre-consultation recovery removes that assignment only through an evidenced lifecycle transition.
4. Patient waiting-room entry may mark the appointment Arrived; video/session presence alone never starts a consultation.
5. Consultation start requires `Connecting`, current request version, the authenticated reservation-owning physician, active shift and reservation, unexpired patient and physician grants for the same session, and all affirmative synthetic checklist fields.
6. The checklist records only coarse evidence for patient identity discussion, GA/CA/FL physical-location reconfirmation, callback, privacy, telehealth-consent discussion, symptom-change/red-flag check, emergency plan, modality sufficiency, and synthetic-data acknowledgment. It is explicitly not legal consent or identity proofing.
7. Any emergency, concerning symptom change, failed communication, stale location, expired grant/reservation, missing participant, or non-owner attempt fails closed with no encounter or state mutation.
8. One transaction creates exactly one encounter through the existing AvenChart encounter foundation, links it to the telehealth appointment/context, marks the appointment In room, and moves the request only `Connecting -> InConsultation`.
9. Exact command replay returns the same opaque consultation projection. Changed-content reuse and concurrent starts create no second encounter.
10. The public response and UI expose an opaque consultation ID, never the sequential encounter key, and explicitly keep chart access, notes, signing, prescribing, claims, and completion unavailable.
11. Start evidence is request/practice/facility/reservation/session/appointment/encounter bound, append-only or no-delete as appropriate, and reconstructable without storing raw discussion, media, symptoms, or free text.
12. Appointment/request/consultation/encounter linkage, authorization, concurrency, failure recovery, migration/recovery, privacy, accessibility, and full regression evidence must pass without weakening prior gates.

## 4. Explicit exclusions

This decision does not authorize:

- real consent, identity proofing, clinical assessment, actual audio/video, chart review, note entry, diagnosis, orders, disposition, AVS, signature, amendment, or clinical completion;
- medication reconciliation, prescribing, pharmacy lookup/transmission, claims, coding, charges, payment, FHIR export, or any external integration;
- a live video provider, webhook, recording, transcription, chat, attachment, notification, marketplace, or production deployment;
- expanded access through general patient/encounter routes, a representation that a participant is truly present, or a legal/payer/state sufficiency conclusion; or
- real people, real PHI, patient care, or closure of any independent review gate.

## 5. Stop conditions and rollback

Stop if a non-owner or expired reservation can start; a missing/expired participant grant passes; an emergency or negative checklist answer creates an encounter; replay/concurrency creates multiple appointments, contexts, or encounters; a sequential encounter key or participant identity reaches the patient projection; a start bypasses existing encounter linkage; or prior safety evidence regresses. Rollback disables/removes the routes and UI. Additive appointment, encounter, consultation, and event evidence is retained for governed correction and is never destructively deleted.

## 6. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation work. This record applies that authority only to the bounded disabled synthetic slice above. It does not broaden authority to production, real care, external vendors, or self-certification of independent reviews.

## References

- [Decision 0008](0008-approved-sprint-05-connection-room-shell.md)
- [Workflow state machines](../03-workflows-and-state-machines.md)
- [Consultation specification](../09-consultation-documentation-and-follow-up.md)
- [Sprint 6 plan](../backlog/sprint-06-consultation-start-handoff.md)
