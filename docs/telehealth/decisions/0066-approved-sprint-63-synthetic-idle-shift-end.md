# Decision 0066: Sprint 63 synthetic idle-shift end

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-30

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the exact physician who owns an idle `Active` synthetic telehealth shift to end that shift explicitly. This is an operational availability change only; it is not visit, appointment, encounter, clinical, financial, claim, media, or external completion.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, and limited to the configured branded practice, facility, GA/CA/FL, and active adult patient shell.
2. The command requires the exact owner, practice, facility, active shift identifier and current shift version. It fails closed for foreign, stale, ended, busy, wrap-up, or missing shifts.
3. The database transaction must prove that the shift has no active reservation and no active or wrap-up consultation context before it changes `Active` to `Ended`.
4. The command requires explicit confirmation that no active work is held and that the effect is synthetic-only. It is semantic-idempotent: exact retry returns the same end result; conflicting key reuse fails.
5. The action records an end time and immutable command provenance. The physician UI clears the ended shift and permits a new shift to be started only through the existing start-shift flow.
6. The endpoint and UI explicitly state that no patient request, queue entry, appointment, encounter, documentation, prescription, pharmacy transmission, delivery, billing, claim, integration, external destination, or real-care action is changed.

## 3. Explicit exclusions

This decision does not authorize reservation cancellation, consultation termination, appointment fulfillment, encounter completion, legal signing, patient notification, AVS, prescription delivery, billing, claim creation or submission, payer, clearinghouse, pharmacy, media, outbox/inbox work, real patient care, or production enablement.

## 4. Stop conditions and rollback

Stop if a shift can end with active work; if an owner, practice, facility, or version check is bypassed; if retry behavior is not stable; or if any patient, clinical, financial, integration, or external state changes. Rollback removes the end-shift endpoint and UI control while leaving already ended shifts auditable.

## 5. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic idle-shift end slice above.

## References

- [Workflow state machines](../03-workflows-and-state-machines.md)
- [Decision 0064](0064-approved-sprint-61-synthetic-visit-closure.md)
- [Sprint 63 plan](../backlog/sprint-63-synthetic-idle-shift-end.md)
