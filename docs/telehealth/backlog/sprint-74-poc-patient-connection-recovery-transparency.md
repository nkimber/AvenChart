# Sprint 74 plan: POC patient connection-recovery transparency

Status: Implemented and staging-verified under [TH-DEC-0077](../decisions/0077-approved-poc-patient-connection-recovery-transparency.md)

## Goal

Make a completed pre-consultation synthetic connection recovery clear and safe for both established and prospective patients.

## Delivery boundary

- Detect only an authoritative poll transition from `Connecting` to `Queued`.
- Clear local waiting-room and preflight state before rendering the resumed queue status.
- Display one neutral no-reason recovery message in the existing page session.
- Do not add persistence, notification delivery, queue mutation, clinician identity, clinical information, media recovery, integration, or production capability.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Scope | Both established and applicant flows use the same transition predicate and neutral message. |
| State | Only `Connecting` to `Queued` clears stale local connection material. |
| Transparency | The notice confirms resumed queue status without a clinician identity, reason, or clinical inference. |
| Safety | No ended grant, preflight evidence, or local command material remains rendered after recovery. |
| Consequence | No patient contact, persistence, care, financial, integration, external, or production effect. |
| Regression | Frontend bundle/tests, planning/runtime/staging/Graphify evidence passes. |

## Gate preserved

Production notifications, patient-specific explanation policy, media reconnection, operational escalation, clinical ownership, and all release gates remain separately governed work.
