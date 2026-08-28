# Decision 0005: Sprint 2 established-patient readiness authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-26  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Extend the approved Sprint 1 foundation with one established-patient, synthetic-only readiness path:

```text
TelehealthEligible triage
  -> Intake
  -> server-read current demographics/contact and clinical-summary projection
  -> affirmative confirmation of the exact projection fingerprints
  -> bounded complaint-details snapshot
  -> selection and confirmation of an existing patient-owned coverage record
  -> exact synthetic demonstration acknowledgment
  -> Verification
  -> deterministic NON_PRODUCTION eligibility and exact-network stub evidence
  -> OperationalReview only when both synthetic gates are explicitly confirmed
```

Eligibility and network participation remain separate statuses and evidence. `Unknown`, missing, stale, or mismatched evidence cannot be presented as covered or in network and cannot reach operational review.

## 2. Authorized implementation surfaces

Changes may use the existing Decision 0003 paths plus:

```text
avenchart/database/migrations/V0283__telehealth_established_patient_readiness.sql
docs/telehealth/decisions/0005-approved-sprint-02-established-patient-readiness.md
docs/telehealth/backlog/sprint-02-established-patient-readiness.md
docs/telehealth/backlog/sprint-02-evidence.md
```

The smallest existing telehealth test, runtime-evidence, OpenAPI, frontend route, configuration, planning-validation, and CI composition edits needed to connect and verify the slice are authorized. Existing non-telehealth patient and insurance records remain the system of record and may be read through scoped repository queries; they may not be overwritten by this slice.

## 3. Required controls

1. Feature defaults off and Production startup still rejects enablement.
2. Only authenticated, established, portal-enabled synthetic patients may use the flow.
3. Practice, facility, patient, request, insurance record, version, state, and action scope are enforced server-side.
4. Raw policy/member identifiers are never returned; patient projections use masking and source fingerprints.
5. Patient confirmations, intake, acknowledgment, coverage selection, coverage verification, and request events are append-only.
6. Complaint text is bounded, never logged, and visibly restricted to synthetic data.
7. The acknowledgment is labeled synthetic and non-legal; it is not represented as production telehealth treatment consent.
8. The coverage gateway is a deterministic in-process `NON_PRODUCTION` adapter. It performs no network call and never implies payer acceptance or a guarantee of payment.
9. Eligibility and exact-network statuses, sources, input fingerprints, limitations, verification times, and expirations are stored separately and reconstructably.
10. Only a current `Active` eligibility result plus current `ConfirmedInNetwork` exact-network result may transition `Verification -> OperationalReview`.
11. Unknown or adverse results remain patient-visible in `Verification` with an honest recovery message and are invisible to the administrator authorization queue.
12. Commands require semantic idempotency and optimistic concurrency; PostgreSQL constraints and transactions protect ownership and evidence.
13. The additive migration must pass empty, populated, replay, interruption, recovery, and append-only checks.
14. API, unit, authorization, runtime, UI, accessibility, failure-recovery, and planning evidence must be updated without weakening existing gates.

## 4. Explicit exclusions

This decision does not authorize:

- new-patient/prospective-patient creation, identity proofing, duplicate resolution, chart promotion, or marketplace entry;
- real people, real symptoms, production PHI, a public domain, patient care, production enablement, or deployment;
- live payer, X12, clearinghouse, pharmacy, video, notification, e-prescribing, claim, payment, or price/GFE traffic;
- a claim that a real plan is active, a real provider is in network, a service will be paid, or a quoted patient responsibility is accurate;
- patient edits to canonical demographics, clinical lists, or insurance records;
- legal approval of consent content, clinical approval of a production protocol, G2/G3/G4 closure, or closure of any Phase 2 finding; or
- administrator override of clinical or coverage results.

## 5. Stop conditions and rollback

Stop if a cross-patient coverage record can be selected, an unconfirmed/unknown coverage result reaches staff review, raw identifiers leak into UI/logs, a real destination is contacted, Production accepts the enabled feature, or migration/recovery evidence fails. Rollback disables the feature and leaves append-only evidence dormant. Applied migrations and evidence are corrected only with a separately reviewed forward change.

## 6. Approval record

The program owner previously directed Codex to “implement all of this,” approved all decisions, and on 2026-08-26 stated: “I give you permission to modify the generated bootstrap file. I am about to go to bed and I will not be able to intervene or give human permissions for about 10 hours. I want you to be able to operate during this time and I give you authorization and permission to make whatever changes you need to be able to run this goal as a long running job, uninterrupted.” This record activates only the bounded non-production slice above. It does not broaden authority to real patient care, external integrations, production deployment, or self-certification of independent reviews.

## References

- [Decision 0003](0003-proposed-sprint-01-synthetic-foundation.md)
- [Decision 0004](0004-proposed-bootstrap-schema-reconciliation.md)
- [Workflow state machine](../03-workflows-and-state-machines.md)
- [Insurance and network specification](../08-insurance-eligibility-network-and-pricing.md)
- [Sprint 2 plan](../backlog/sprint-02-established-patient-readiness.md)

