# Telehealth implementation backlog

Status: Exact disabled synthetic Sprints 1–77 active only within Decisions 0003 and 0005–0080
Decision baseline: [Decision 0001](../decisions/0001-g0-development-baseline.md)  
Machine-readable source: [backlog.json](backlog.json)  
First iteration: [Sprint 1 foundation plan](sprint-01-foundation.md)
Structural evidence: [Planning-artifact validation report](validation-report.md)
Sprint evidence: [Sprint 1 implementation and verification index](sprint-01-evidence.md)  
Synthetic operations: [Sprint 1 runbook](sprint-01-runbook.md) and [release manifest](sprint-01-release-manifest.json)
Current increment: [Sprint 77 POC applicant pre-authorization request withdrawal](sprint-77-poc-applicant-pre-authorization-request-withdrawal.md); implementation and automated evidence passed

## 1. Backlog contract

The backlog contains 20 epics and 60 primary stories. Each of the 329 `TEL-*` requirements is assigned exactly once through an inclusive requirement range in `backlog.json`. A requirement may be referenced secondarily by other stories, but its primary story owns implementation coordination and evidence completeness.

Every delivery story must acquire, before `done`:

- a named engineering owner;
- accepted design/ADR where required;
- implementation and configuration links;
- automated test identifiers;
- manual/clinical/legal/security/accessibility evidence where required;
- migration, rollback, telemetry, runbook and support evidence when applicable; and
- an updated traceability row matching specification 19.

Statuses are `planned`, `ready`, `in_progress`, `blocked`, `verification`, and `done`. `blocked` requires a named dependency and next action. Priority is clinical/data/security correctness first, then the dependency order below.

## 2. Epic map

| Epic | Scope | Requirements | Delivery gate | Depends on |
|---|---|---|---|---|
| `TH-E01` | Product shell and feature boundaries | `TEL-PROD-001..024` | G1–G4 | E13, E16 |
| `TH-E02` | Actors and authorization relationships | `TEL-ACT-001..018` | G1–G2 | E16 |
| `TH-E03` | Request, appointment, queue and consultation state machines | `TEL-WF-001..015` | G1–G2 | E13–E16 |
| `TH-E04` | Consumer identity and new-patient promotion | `TEL-IDN-001..016` | G1–G3 | E02, E14–E16 |
| `TH-E05` | Clinical protocol and triage engine | `TEL-TRI-001..016` | G2 | E03, E14, E15, E19 |
| `TH-E06` | State and clinical governance | `TEL-REG-001..014` | G2–G4 | E05, E16 |
| `TH-E07` | Practice branding, configuration, operations and matching | `TEL-PRA-001..016` | G1–G2 | E02, E03, E13–E16 |
| `TH-E08` | Eligibility, exact network participation and estimates | `TEL-INS-001..016` | G2–G3 | E04, E07, E13–E16 |
| `TH-E09` | Consultation, charting, AVS and follow-up | `TEL-CON-001..016` | G2 | E03–E07, E10 |
| `TH-E10` | Video, waiting room, realtime and communication | `TEL-VID-001..016` | G2–G3 | E02, E03, E07, E13–E16 |
| `TH-E11` | Pharmacy choice and non-controlled e-prescribing | `TEL-RX-001..016` | G2–G3 | E09, E13–E16 |
| `TH-E12` | Professional claims and reconciliation | `TEL-CLM-001..016` | G2–G3 | E08, E09, E13–E16 |
| `TH-E13` | Feature architecture and adapter boundaries | `TEL-ARC-001..014` | G1 | None |
| `TH-E14` | Data model, provenance, migration and retention | `TEL-DAT-001..014` | G1 | E13 |
| `TH-E15` | HTTP API, events and interoperability contracts | `TEL-API-001..014` | G1–G3 | E13, E14, E16 |
| `TH-E16` | Security, privacy, consent and audit foundation | `TEL-SEC-001..016` | G1–G4 | E13, Phase 2 trust boundary |
| `TH-E17` | Patient/staff/physician UX and accessibility | `TEL-UX-001..020` | G1–G4 | E01–E10, E16 |
| `TH-E18` | Reliability, observability and operations | `TEL-NFR-001..020` | G1–G4 | E13–E16 |
| `TH-E19` | Test infrastructure and traceability | `TEL-TST-001..016` | G1–G4 | E13–E16 |
| `TH-E20` | Pilot, metrics, risk and approvals | `TEL-ROL-001..016` | G1–G5 | All epics |

## 3. Dependency sequence

```text
Phase 2 trust/resource boundary authorization
  -> E13 architecture + E16 security + E19 test foundation
  -> E14 data + E15 API/events
  -> E03 workflow + E02 actors + E07 practice/queue + E17 UX shell
  -> First vertical slice: brand -> established patient -> safety/triage -> admin -> queue -> reserve
  -> E04 new patient + E08 financial gates + E10 video
  -> E09 consultation
  -> E11 prescribing + E12 claims
  -> E18 production hardening + E20 pilot/expansion gates
```

Clinical governance (`E05/E06`) begins in parallel with the foundation so approved executable protocol fixtures are ready before the triage slice reaches G2. Vendor procurement can proceed in parallel but cannot change canonical adapter contracts without review.

## 4. Ready criteria

A story becomes `ready` only when:

1. its requirement range and dependencies are understood;
2. required decisions and content owners are identified;
3. acceptance cases include success, denial, stale/concurrent, outage/recovery, audit and accessibility behavior as applicable;
4. database/API/event and rollback impact is identified;
5. synthetic test data and safe destinations are available; and
6. no open upstream gate forbids the change.

## 5. Done criteria

The definition of done in [specification 19](../19-testing-acceptance-and-traceability.md) is normative. In addition, a story cannot close if it introduces a new `MUST`, endpoint, event, data field, state, clinical rule, vendor assumption or patient-facing assertion without updating the controlling specification and traceability source.

## 6. Current authorization status

- Telehealth G0 product baseline: **approved** by Decision 0001.
- Backlog and wireframe preparation: **authorized**.
- Planning-artifact validator and existing-CI invocation: **authorized and active under Decision 0002**.
- Decisions 0003 and 0005–0069 authorize only their exact disabled synthetic Sprint 1–66 application/database/feature-test/runtime paths through 2026-10-31.
- All implementation outside those decisions remains **blocked by the existing Phase 2 exit gate** until explicit closure or another scoped override.
- Real patient care: separately blocked until G4 regardless of implementation authorization.

[Decision 0002](../decisions/0002-proposed-scoped-verification-authorization.md) continues to govern the planning validator and existing-CI invocation.

[Decision 0003](../decisions/0003-proposed-sprint-01-synthetic-foundation.md) authorizes the complete disabled, synthetic Sprint 1 vertical slice only on its listed paths and with its stop conditions. It does not authorize production enablement, real patient care, or any live integration.

[Decisions 0005–0046](../decisions/0046-approved-sprint-43-applicant-request-complaint-triage.md) add only the bounded synthetic increments documented for Sprints 2–43. Decisions 0038–0042 govern practice-review submission, inbox, claimant, packet, and positive operational authorization. Decision 0043 permits the access-key owner to separately create exactly one source-linked `Draft` request after that authorization. Decision 0044 permits the same owner to bind the exact prior supported current-location state and masked callback route to that request and advance it only to `LocationConfirmed` version 2. Decision 0045 permits one request-time universal safety assessment using the immutable non-production four-answer fixture. Decision 0046 permits one fixed migraine or sleep coded complaint-triage assessment only after an exact universal pass. It records ordered rule evidence and maps the six bounded outcomes, but all content remains `UNAPPROVED_SYNTHETIC`; medical-director approval, approved clinical golden cases, and production publication remain explicitly false. No clinical-review work item, intake snapshot, contact, doctor search, patient/clinician care-queue entry, queue position, appointment, encounter, consent, care, prescribing, billing, claim, integration, or external action is created.

[Decision 0047](../decisions/0047-approved-sprint-44-applicant-request-intake-snapshot-confirmation.md) permits one request-bound intake snapshot and pending `Verification` version 5 transition. [Decision 0048](../decisions/0048-approved-sprint-45-applicant-request-insurance-source-confirmation.md) permits one masked primary-source confirmation and pending version 6 transition without reusing historical results. [Decision 0049](../decisions/0049-approved-sprint-46-applicant-request-eligibility-verification.md) permits one fresh bounded non-production eligibility result and pending version 7 transition. [Decision 0050](../decisions/0050-approved-sprint-47-applicant-request-practice-network-verification.md) permits one fresh practice/facility/service network result and pending version 8 transition after current positive eligibility. [Decision 0051](../decisions/0051-approved-sprint-48-applicant-request-rendering-candidate-selection.md) permits one server-owned state-specific synthetic candidate binding and pending version 9 transition. [Decision 0052](../decisions/0052-approved-sprint-49-applicant-request-participation-context.md) permits one server-owned effective-dated synthetic prerequisite context and pending version 10 transition. [Decision 0053](../decisions/0053-approved-sprint-50-applicant-request-participation-evaluation.md) permits one exact server-owned non-production participation tuple evaluation, including new-patient acceptance, and pending version 11 transition. [Decision 0054](../decisions/0054-approved-sprint-51-applicant-request-operational-review-submission.md) permits the applicant to submit that exact current chain for practice operational review and advances only the request to `OperationalReview` version 12. [Decision 0055](../decisions/0055-approved-sprint-52-applicant-request-queue-authorization.md) permits a configured-practice administrator to accept that exact applicant-originated request into the disabled synthetic clinician queue, atomically creating one unassigned appointment and one ready queue entry and advancing the request to `Queued` version 13. [Decision 0056](../decisions/0056-approved-sprint-53-applicant-request-queue-status.md) permits only the access-key owner to read applicant request queue status through authoritative polling and approximate ordering. [Decision 0057](../decisions/0057-approved-sprint-54-applicant-request-clinician-reservation.md) permits only the exact current synthetic rendering candidate to reserve that request and expose a minimized physician-preparing state. [Decision 0058](../decisions/0058-approved-sprint-55-applicant-request-connection-room.md) permits only the access-key owner of that exactly reserved request to run a coarse local device check and create a private non-production waiting-room session/grant, move the request to `Connecting`, and expose waiting-room true with media and communication false. Protected source data remains server-side; no media, X12, FHIR, NCPDP, or external destination is used, and consultation, consent, encounter, care, financial routing, and production remain closed.

[Decision 0059](../decisions/0059-approved-sprint-56-applicant-consultation-start.md) permits only the exact reservation-owning physician to enter that capture-disabled room and start the bounded synthetic consultation lifecycle after both grants, fresh location, full affirmative checklist, appointment/patient ownership, and the still-current exact candidate authorization are rebound. It creates one synthetic encounter and exposes only the existing bounded chart workspace and unsigned SOAP draft. The applicant alternative never creates or claims real coverage, legal consent, treatment, prescribing, billing, claims, integration, external action, completion, or production readiness.

[Decision 0060](../decisions/0060-approved-sprint-57-applicant-wrap-up-planning.md) permits only the exact consultation-owning physician to end the applicant-originated synthetic lifecycle into unfinished `WrapUp` and reuse the existing unsigned SOAP, neutral pharmacy-destination, non-controlled prescription-preparation, safety-disposition, and structural completion-prerequisite planning tools. Applicant polling gains only a minimized terminal `WrapUp` projection. Signing, canonical prescribing, transmission, patient delivery, completion/release, billing, claims, integrations, external action, and production remain closed.

[Decision 0061](../decisions/0061-approved-sprint-58-synthetic-prescription-signing.md) permits only the exact consultation-owning physician to run a conservative zero-active-medication/zero-active-allergy safety gate and atomically create one immutable signed synthetic prescription plus an uncertified, prepared-only NCPDP SCRIPT 2023011 `NewRx` seam. It has no legal effect and contacts no pharmacy, network, drug-knowledge service, payer, or other external destination. Transmission, patient delivery, completion/release, billing, claims, and production remain closed.

[Decision 0062](../decisions/0062-approved-sprint-59-synthetic-final-clinical-review.md) permits only the exact consultation-owning physician to append one immutable source-bound synthetic final clinical-review record before a separately governed encounter lock. It is not a legal signature, completion, patient delivery, billing, claim, integration, or external action.

[Decision 0063](../decisions/0063-approved-sprint-60-synthetic-encounter-finalization.md) permits only the exact consultation-owning physician to create the existing governed synthetic encounter lock after exact final-review and source verification. It is not a legal signature, completed visit, patient delivery, billing, claim, integration, or external action.

[Decision 0064](../decisions/0064-approved-sprint-61-synthetic-visit-closure.md) permits only the exact consultation-owning physician to close the synthetic consultation/request lifecycle after that encounter lock and return the shift to `Active`. The appointment stays in progress; encounter completion, patient delivery, billing, claims, integrations, and external action remain closed.

[Decision 0065](../decisions/0065-approved-sprint-62-synthetic-closure-status.md) permits the established-patient and exact applicant owner to read a neutral terminal `Closed` lifecycle projection after full lineage validation. The projection says the appointment and encounter remain incomplete and exposes no care-completion, prescription, billing, claim, integration, or external assertion.

[Decision 0066](../decisions/0066-approved-sprint-63-synthetic-idle-shift-end.md) permits the exact physician to end only an idle `Active` synthetic shift after server proof that no active reservation, active consultation, or wrap-up work remains. It creates no patient, queue, appointment, encounter, clinical, financial, media, integration, external, or production consequence.

[Decision 0067](../decisions/0067-approved-poc-synthetic-consultation-transcript.md) permits only the active synthetic request owner and exact consultation-owning physician to append/read an immutable, plain-text, confirmed-synthetic POC transcript during `InConsultation`. It uses visible-page HTTP polling only and creates no realtime delivery, media, recording, transcription, attachment, notification, patient delivery, clinical, financial, integration, external, or production consequence.

[Decision 0068](../decisions/0068-approved-poc-synthetic-patient-request-cancellation.md) permits only the exact authenticated patient owner to cancel an incomplete synthetic request before practice queue authorization with a current version, semantic idempotency, explicit confirmation, and an append-only event. It cannot cancel an appointment, reservation, connection, consultation, prescription, billing item, claim, integration, notification, external action, or production behavior.

[Decision 0069](../decisions/0069-approved-poc-synthetic-request-history.md) permits only the exact authenticated owner to read a minimized synthetic request lifecycle history from the existing append-only event ledger. It exposes only version, resulting status, neutral message, and timestamp; it exposes no actor, raw action, clinical, financial, delivery, integration, external, or production information.

[Decision 0070](../decisions/0070-approved-poc-synthetic-professional-claim-preparation.md) permits the exact wrap-up physician to persist a durable, source-bound `PreparedOnly` synthetic professional-claim receipt with no claim transaction, payer, clearinghouse, pharmacy, or external contact.

[Decision 0071](../decisions/0071-approved-poc-synthetic-post-visit-receipt.md) permits the exact patient or applicant owner to read an immutable, non-clinical synthetic closure receipt that records no delivery, completion, financial, clinical, or external effect.

[Decision 0072](../decisions/0072-approved-poc-synthetic-after-visit-plan-preview.md) permits the exact patient or applicant owner to read an immutable, physician-authored synthetic post-closure plan preview derived from the existing disposition/final-review evidence. It is not a delivered AVS, medical advice, notification, document, completion, financial, integration, or external action.
