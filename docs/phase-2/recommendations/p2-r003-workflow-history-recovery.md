# P2-R003 — Make scheduling, communication, recall, therapy, billing, and follow-up workflows durable and recoverable

- **Status:** Proposed — target policy approved by `P2-D016`; implementation is not authorized
- **Linked findings:** `P2-03-F019` through `P2-03-F024`, `P2-05-F009`, `P2-08-F001` through `P2-08-F004`
- **Priority band:** Blocker
- **Size:** XL
- **Difficulty:** High
- **Confidence:** High engineering and approved workflow target; detailed operations acceptance evidence pending
- **Proposed owner:** Clinical workflow and operations leads
- **Decision owner:** AvenChart program owner
- **Specialist approval needed:** Scheduling, clinical operations, communications, HIM, finance, pharmacy, therapy

## Problem and evidence

Unify appointment conflict/lifecycle/version rules, message ownership/content history, reminder/referral/recall delivery outcomes, therapy attendance and encounter linkage, billing mutation provenance, and queue recovery into durable state machines with attributable actors and idempotent retries.

The linked workflow findings establish time-of-check/time-of-use scheduling behavior, legacy message mutations outside governed history, hard-deleted recall/appointment evidence, therapy snapshot and cross-transaction failures, controlled-inventory attestation weakness, billing/EOB provenance gaps, and UI stale-action paths. The common problem is workflow state that can change without a single attributable, recoverable aggregate boundary.

## Target state

Each supported workflow uses an approved state and exception model with durable actor/resource/prior-current/outcome evidence, serialized or versioned transitions, idempotent retry, observable recovery, and non-destructive correction where the record is clinically or financially material.

## Expected value

Prevent double booking, lost exceptions, contradictory communication closure, disappearing recall evidence, divergent therapy snapshots, orphan encounters, and untraceable financial/workflow mutations.

## Options considered

Prefer focused aggregate boundaries and transaction ownership over a new service architecture. Define workflow terminal-state, overbooking, reminder delivery, referral acknowledgement, recall retention, and therapy correction policies first. Use staged event/history additions, replayable migration, idempotency keys, and reversible status adapters.

## Dependencies and sequence

`P2-R001` supplies actor, facility, purpose, and audit foundations; `P2-R002` supplies patient/lifecycle and laboratory rules; `P2-R004` supplies constraints/migrations; and `P2-R007` supplies fault/recovery evidence. Schedule and communication rules precede UI migration in `P2-R005`; the laboratory aggregate and workflow outcome vocabulary precede external transport decisions in `P2-R006`.

## Acceptance criteria

Two-actor and fault-injection tests cover every listed race and partial failure; retries are idempotent; history identifies actor, reason, resource, prior/current state, and outcome; queue age/failure/ack metrics are observable; recovery and rollback are rehearsed; scheduling, communications, therapy, finance, and HIM owners approve the resulting semantics.

## Scope and affected contracts

- Appointment/recurrence/conflict/status APIs and modern schedule, Flow Board, and portal appointment UI.
- Staff/portal messages, reminders, referrals, recalls, therapy group/session/attendance/encounter APIs and UI.
- Controlled inventory, billing, claims, payments, EOB/remittance, collections, workflow audit, queues, and recovery models.
- Event/audit/history schemas, constraints, transaction boundaries, idempotency keys, worker metrics, migrations, restore scripts, and operational runbooks.

## Delivery risk and rollback

Visible schedule state, duplicate events, lost follow-up evidence, and unbalanced financial entries are the principal risks. Use additive transition/event tables, canonical idempotency keys, read-model reconciliation, feature-gated routes, append-only correction rather than destructive rollback, worker lease/retry observability, and synthetic partial-failure injection. Recovery must identify and repair partial state rather than retry blindly.

## Size and difficulty rationale

XL breadth covers repeated clinical and financial workflows, but the cohesive boundary is state, ownership, evidence, atomicity, idempotency, and recovery. Difficulty is High because policy and exception rules vary by workflow; shared conventions reduce duplication without forcing a new workflow platform.

## Phase 3 change packets

1. **R003-A — Scheduling aggregate:** conflict/override rule, expected version, recurrence exceptions, terminal correction/deletion policy, schedule/Flow Board safety.
2. **R003-B — Communication and follow-up:** governed message model, reminder/referral/recall lifecycle, local-vs-external outcome vocabulary, retention, and recovery.
3. **R003-C — Therapy integrity:** attendance/complete concurrency boundary, participant correction, and atomic/idempotent encounter linkage.
4. **R003-D — Controlled and financial integrity:** independent attestation, discrepancy closure, internal ledger/provenance/reversal, and EOB/ERA staging contract.
5. **R003-E — Operational recovery:** worker outcome metrics, reconciliation runbooks, fault injection, recovery, and rollback exercises.

## Decision record

- **Decision:** Pending acceptance as a Phase 3 recommendation.
- **Decided by:** AvenChart program owner.
- **Date:** Not set.
- **Rationale and conditions:** `P2-D016` approves workflow and financial target policies. Acceptance requires named workflow owners, transition/exception matrices, migration/rollback ownership, external-boundary decisions, and the evidence above.
