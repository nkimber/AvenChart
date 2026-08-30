# Decision 0065: Sprint 62 synthetic closure-status projection

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-30

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the existing authenticated patient and applicant request-status projections to show an exact `Closed` synthetic lifecycle state after the governed Sprint 61 closure transaction. The projection supplies honest terminal status only; it does not make a clinical, legal, financial, appointment, or delivery assertion.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, and limited to the configured branded practice, facility, GA/CA/FL, and active adult patient shell.
2. The applicant projection is available only to the exact unexpired applicant access key and only after full existing lineage validation. A `Closed` state must prove the removed queue, released reservation, ended synthetic session, in-progress appointment, closed consultation/request, return of the existing shift to `Active`, governed encounter lock, and both closure events.
3. The terminal projection says that the synthetic lifecycle closed and explicitly says the appointment and encounter remain incomplete. It never reports care completion, a signed record, patient delivery or AVS, prescription delivery, bill, claim, integration, external action, physician identity, or real network confirmation.
4. `Closed` is terminal for browser polling and exposes no connection-room, queue-refresh, or action control. Existing emergency guidance remains visible.
5. The same neutral content is used by the established-patient status endpoint. No additional patient data is exposed.

## 3. Explicit exclusions

This decision does not authorize appointment or encounter completion, clinical completion, legal signing, patient notification, after-visit summary, prescription delivery, billing, claim creation or submission, payer, clearinghouse, pharmacy, media, integration, external action, real patient care, or production enablement.

## 4. Stop conditions and rollback

Stop if a closure status can be read without the exact lineage, reports completion or downstream work, exposes physician identity or prescription information, or continues terminal polling/actions. Rollback removes `Closed` from the applicant-visible status projection and leaves the underlying closed lifecycle unchanged and auditable.

## 5. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic closure-status projection above.

## References

- [Decision 0064](0064-approved-sprint-61-synthetic-visit-closure.md)
- [Sprint 62 plan](../backlog/sprint-62-synthetic-closure-status.md)
