# Telehealth implementation backlog

Status: Exact disabled synthetic Sprints 1–44 active only within Decisions 0003 and 0005–0047
Decision baseline: [Decision 0001](../decisions/0001-g0-development-baseline.md)  
Machine-readable source: [backlog.json](backlog.json)  
First iteration: [Sprint 1 foundation plan](sprint-01-foundation.md)
Structural evidence: [Planning-artifact validation report](validation-report.md)
Sprint evidence: [Sprint 1 implementation and verification index](sprint-01-evidence.md)  
Synthetic operations: [Sprint 1 runbook](sprint-01-runbook.md) and [release manifest](sprint-01-release-manifest.json)
Current increment: [Sprint 44 applicant request intake snapshot confirmation](sprint-44-applicant-request-intake-snapshot-confirmation.md); bounded automated implementation evidence is recorded in the [Sprint 44 evidence packet](sprint-44-evidence.md)

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
- Decisions 0003 and 0005–0046 authorize only their exact disabled synthetic Sprint 1–43 application/database/feature-test/runtime paths through 2026-10-31.
- All implementation outside those decisions remains **blocked by the existing Phase 2 exit gate** until explicit closure or another scoped override.
- Real patient care: separately blocked until G4 regardless of implementation authorization.

[Decision 0002](../decisions/0002-proposed-scoped-verification-authorization.md) continues to govern the planning validator and existing-CI invocation.

[Decision 0003](../decisions/0003-proposed-sprint-01-synthetic-foundation.md) authorizes the complete disabled, synthetic Sprint 1 vertical slice only on its listed paths and with its stop conditions. It does not authorize production enablement, real patient care, or any live integration.

[Decisions 0005–0046](../decisions/0046-approved-sprint-43-applicant-request-complaint-triage.md) add only the bounded synthetic increments documented for Sprints 2–43. Decisions 0038–0042 govern practice-review submission, inbox, claimant, packet, and positive operational authorization. Decision 0043 permits the access-key owner to separately create exactly one source-linked `Draft` request after that authorization. Decision 0044 permits the same owner to bind the exact prior supported current-location state and masked callback route to that request and advance it only to `LocationConfirmed` version 2. Decision 0045 permits one request-time universal safety assessment using the immutable non-production four-answer fixture. Decision 0046 permits one fixed migraine or sleep coded complaint-triage assessment only after an exact universal pass. It records ordered rule evidence and maps the six bounded outcomes, but all content remains `UNAPPROVED_SYNTHETIC`; medical-director approval, approved clinical golden cases, and production publication remain explicitly false. No clinical-review work item, intake snapshot, contact, doctor search, patient/clinician care-queue entry, queue position, appointment, encounter, consent, care, prescribing, billing, claim, integration, or external action is created.
