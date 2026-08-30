# Decision 0062: Sprint 59 synthetic final clinical-review affirmation

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-30

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the exact physician who owns an unfinished synthetic telehealth consultation to record an immutable, versioned final clinical-review affirmation. The record binds the current complete SOAP draft, current safety-disposition draft, and, when one exists, the immutable signed synthetic prescription order.

This is clinician-authored evidence that the physician reviewed the listed synthetic draft versions. It is not a legal signature, final encounter record, diagnosis, treatment authorization, patient instruction, after-visit summary, claim, billing record, visit completion, or clinician release.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, and limited to the configured branded practice, facility, GA/CA/FL, and active adult patient shell.
2. Only the exact owning physician may read or append the review; another clinician, staff role, facility, stale consultation, or locked encounter fails closed.
3. The review requires a current SOAP version with every structural section present, a current safety-disposition version, and affirmative physician acknowledgments of review, adequacy responsibility, no automatic claim or delivery, and synthetic-only effect.
4. It snapshots source version identifiers and optional signed-order identifier. A changed source requires a new review version; prior records are immutable.
5. It provides no automated sufficiency, diagnosis, treatment, coding, coverage, or payment determination. The physician retains responsibility for the review.
6. Idempotent retry returns the exact record. Conflicting reuse of an idempotency key fails closed.
7. The completion-prerequisites projection may report whether its current source versions have a matching review record, but signing, completion, delivery, downstream creation, billing, and claims remain disabled.
8. No canonical encounter signature, AVS, message, appointment/consultation/shift transition, clinician release, outbox/inbox, integration, external call, financial record, claim, or patient-facing delivery is created.

## 3. Explicit exclusions

This decision does not authorize a legally effective signature, encounter finalization, code selection, coding review, billing, professional claim generation or submission, payer or clearinghouse communication, pharmacy communication, AVS, patient delivery, visit completion, clinician release, media, real patient care, or production enablement.

## 4. Stop conditions and rollback

Stop if a non-owner can access the review; incomplete or stale source versions can be affirmed; a review is mutable; a review is represented as a legal signature, claim readiness, delivery, or visit completion; or any downstream, external, financial, or lifecycle state changes. Rollback removes this review endpoint and its evidence tables while preserving the existing drafts and signed synthetic prescription evidence.

## 5. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic final-clinical-review boundary above.

## References

- [Consultation and clinical documentation](../09-consultation-documentation-and-follow-up.md)
- [Professional claims and financial integration](../12-claims-and-financial-integration.md)
- [Decision 0061](0061-approved-sprint-58-synthetic-prescription-signing.md)
- [Sprint 59 plan](../backlog/sprint-59-synthetic-final-clinical-review.md)
