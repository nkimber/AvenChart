# Decision 0014: Sprint 11 synthetic safety-disposition draft authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit only the physician who owns a current synthetic consultation in unfinished wrap-up to record and revise one structured, physician-authored safety-disposition draft. The draft captures the physician's selected disposition, whether the available evaluation was adequate, follow-up owner/timeframe, patient instructions, warning signs/escalation instructions, communication state, and the additional explicit facts required for emergency, urgent, or interrupted outcomes.

The draft is append-only, versioned, attributed, auditable, and linked to the existing encounter and opaque consultation. It is not signed, finalized, delivered to the patient, promoted to an AVS, used for a claim, or treated as evidence that an emergency handoff, transfer, referral, test, prescription, or follow-up occurred.

## 2. Authorized disposition vocabulary

The exact physician-selected codes are:

```text
TreatedTelehealth
NoTreatmentNeeded
TestingOrReferralRequired
UrgentInPerson
EmergencyTransferRecommended
TechnicalAbort
PatientLeft
ClinicianUnableToComplete
```

The application never chooses a disposition or supplies clinical instructions. Every saved draft requires nonblank physician-authored next-step and warning/escalation text, an explicit follow-up owner and timeframe, a communication method/state, and a statement about evaluation adequacy. `UrgentInPerson` and `EmergencyTransferRecommended` require current location/callback reconfirmation. `EmergencyTransferRecommended` also requires an explicit emergency-instruction acknowledgment and one non-claiming handoff state. Interrupted outcomes require a contact/safety-attempt summary. Outcomes that represent completed evaluation require `AdequateEvaluationCompleted = true`.

## 3. Required controls

1. The route remains disabled by default, rejected in Production, synthetic-only, physician-only, treatment-purpose/facility scoped, no-store/private, and correlated to the opaque consultation PHI-audit resource.
2. The server rebinds the current consultation, request, released reservation, wrap-up shift, ended synthetic room, in-progress appointment, open unsigned encounter, adult active patient, physician, practice, and facility in the recording transaction.
3. Input accepts only the expected draft version, exact disposition code, structured acknowledgments/statuses, and bounded physician-authored text. Patient/request/appointment/encounter/actor/time/signature/delivery identifiers cannot be supplied by the client.
4. Exact idempotent replay returns the original version. Changed key reuse, stale version, invalid code, missing common requirement, missing conditional requirement, non-owner, administrator, signed encounter, stale lifecycle, or concurrent writer fails without partial change.
5. Every save appends an immutable snapshot and event with server actor/time and `legal_effect = false`; prior versions cannot be changed or deleted.
6. Emergency handoff states are factual and non-claiming: `RecommendedOnly`, `PatientCalling`, `PracticeCalling`, `Connected`, or `UnableToConfirm`. `Connected` remains an entered physician draft fact and is not external verification.
7. The UI presents this as an unsigned, undelivered safety draft, requires all consequential confirmations visibly, preserves content and the semantic command key after an ambiguous failure, focuses errors, supports keyboard/screen readers and 320 px reflow, and uses no browser persistence.
8. Saving does not change consultation/request/shift/appointment/encounter state; release the physician; sign/finalize documentation; deliver instructions; create an AVS, diagnosis, order, referral, medication, prescription, message, task, bill, claim, notification, outbox/inbox, or external call; or infer that care, communication, transfer, testing, referral, or follow-up occurred.
9. Complete unit, API, authorization, PostgreSQL concurrency/idempotency/rollback/audit/privacy, migration/recovery, accessibility/failure-recovery, planning, Graphify, and full regression evidence is required without weakening Sprints 1–10.

## 4. Explicit exclusions

This decision does not authorize automated clinical advice; templated medical instructions; diagnosis/coding; orders/referrals; patient delivery; emergency-service integration; signed/final documentation; encounter/request/appointment completion; clinician release; AVS; medication reconciliation or prescribing; billing/claims; notifications; real media; real people/PHI; production enablement; or patient care.

## 5. Stop conditions and rollback

Stop if a non-owner can read/write the draft; invalid or incomplete emergency/interrupted data is accepted; clinical text is generated or defaulted; any entered fact is represented as externally verified; stale/concurrent commands partially change history; text enters ordinary logs or browser storage; a downstream clinical/financial/lifecycle/external action occurs; or an earlier safeguard regresses. Rollback disables/removes the route and UI while retaining additive schema and immutable synthetic evidence.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the bounded disabled synthetic draft above. It does not substitute for independent clinical, legal, privacy/security, accessibility, data, operational, or production review.

## References

- [Consultation, documentation, and follow-up](../09-consultation-documentation-and-follow-up.md)
- [Workflow state machines](../03-workflows-and-state-machines.md)
- [Decision 0013](0013-approved-sprint-10-synthetic-pharmacy-choice.md)
- [Sprint 11 plan](../backlog/sprint-11-synthetic-safety-disposition-draft.md)
