# Decision 0064: Sprint 61 synthetic visit closure

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-30

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the exact physician who owns a locked, unfinished synthetic telehealth encounter to close only its synthetic consultation and request lifecycle and return that physician's existing shift from `WrapUp` to `Active`. This is a NON_PRODUCTION operational release, not clinical visit completion.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, and limited to the configured branded practice, facility, GA/CA/FL, and active adult patient shell.
2. Only the exact owner may close an exact `MediaEnded` consultation with a `WrapUp` request and shift, ended session, released reservation, unfinished appointment, and existing governed encounter lock. All stale, foreign, incomplete, or unlocked sources fail closed.
3. The physician must confirm review of the governed lock, confirm the synthetic-only effect, and submit the exact current consultation version.
4. One serializable transaction atomically advances consultation `MediaEnded` to `Closed`, request `WrapUp` to `Closed`, records both append-only events, and returns the existing physician shift to `Active`. Semantic replay returns the original closure result; a reused key with different content is rejected.
5. The appointment remains in progress. The response and private UI must state that encounter completion, patient delivery, billing, claims, pharmacy transmission, integration, and external action are all false.
6. A failure leaves the source lifecycle and physician availability unchanged. The closure control is not offered until the governed encounter-lock command has succeeded in the current physician workspace.

## 3. Explicit exclusions

This decision does not authorize clinical or encounter completion, legal signing, appointment fulfillment, patient instruction or AVS, patient notification, coding, billing, claim creation or submission, payer or clearinghouse communication, pharmacy communication, media, outbox/inbox work, real patient care, or production enablement.

## 4. Stop conditions and rollback

Stop if closure can run without the encounter lock; changes the appointment; releases a non-owner; fails to preserve atomic/replay behavior; or creates delivery, financial, claim, integration, or external effect. Rollback removes the closure endpoint and leaves the locked, unfinished source safely in physician wrap-up for controlled recovery.

## 5. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic lifecycle closure above.

## References

- [Consultation and clinical documentation](../09-consultation-documentation-and-follow-up.md)
- [Professional claims and financial integration](../12-claims-and-financial-integration.md)
- [Decision 0063](0063-approved-sprint-60-synthetic-encounter-finalization.md)
- [Sprint 61 plan](../backlog/sprint-61-synthetic-visit-closure.md)
