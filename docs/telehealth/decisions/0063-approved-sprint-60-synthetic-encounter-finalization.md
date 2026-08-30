# Decision 0063: Sprint 60 synthetic encounter finalization

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-30

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the exact physician who owns an unfinished synthetic telehealth consultation to invoke the existing governed encounter-lock mechanism after a current, complete SOAP draft, current safety-disposition draft, and current source-bound final clinical review have been confirmed. The lock makes the existing encounter snapshot immutable through the governed amendment mechanism.

This is a NON_PRODUCTION synthetic encounter lock. It is not a legally effective signature, clinical finalization, diagnosis, treatment authorization, patient instruction, after-visit summary, billing record, claim, visit completion, or clinician release.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, and limited to the configured branded practice, facility, GA/CA/FL, and active adult patient shell.
2. Only the exact owning physician may finalize the exact `MediaEnded`/`WrapUp` consultation with an ended session, released reservation, unfinished appointment, and `WrapUp` physician shift. All other actors and stale or locked sources fail closed.
3. The existing encounter lock is reached only through a governed transaction that locks the consultation lineage and rechecks ownership, every SOAP section, current safety disposition, and a final review matching the current SOAP version, disposition version, and current signed-prescription order identifier (including no order).
4. The command requires source-review and synthetic-only affirmations plus exact current documentation, disposition, and final-review versions. Changed evidence requires reload; it cannot be silently finalized.
5. The response explicitly reports an immutable encounter lock with `legalEffect`, completion, patient delivery, billing, claim, and external-destination effects all false.
6. No new telehealth clinical, financial, patient-facing, workflow, integration, or external record is created by this command. Ordinary draft updates thereafter require the existing governed amendment workflow.
7. The physician remains in unfinished wrap-up and unavailable for new work. A distinct approved slice must govern visit closure and release.

## 3. Explicit exclusions

This decision does not authorize legal signing, clinical or encounter completion, clinician release, appointment fulfillment, patient delivery or AVS, coding, billing, claim generation or submission, payer or clearinghouse communication, pharmacy communication, media, outbox/inbox work, real patient care, or production enablement.

## 4. Stop conditions and rollback

Stop if finalization can use stale or mismatched source evidence; a non-owner can lock an encounter; a lock produces completion, availability, delivery, financial, claim, integration, or external effect; or an immutable source can be changed outside amendment governance. Rollback removes the finalization endpoint and leaves existing immutable encounter-signature evidence intact for controlled operational recovery.

## 5. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic encounter-finalization boundary above.

## References

- [Consultation and clinical documentation](../09-consultation-documentation-and-follow-up.md)
- [Professional claims and financial integration](../12-claims-and-financial-integration.md)
- [Decision 0062](0062-approved-sprint-59-synthetic-final-clinical-review.md)
- [Sprint 60 plan](../backlog/sprint-60-synthetic-encounter-finalization.md)
