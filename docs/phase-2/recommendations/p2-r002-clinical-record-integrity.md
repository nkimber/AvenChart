# P2-R002 — Make patient and encounter clinical-record integrity coherent

- **Status:** Proposed — target policy approved by `P2-D016`; implementation is not authorized
- **Linked findings:** `P2-03-F001` through `P2-03-F018`, `P2-03-F025` through `P2-03-F029`, `P2-04-F002`, `P2-05-F008`
- **Priority band:** Blocker
- **Size:** XL
- **Difficulty:** Exceptional
- **Confidence:** High engineering and approved target policy; detailed clinical acceptance evidence pending
- **Proposed owner:** Clinical data-integrity and application architecture leads
- **Decision owner:** AvenChart program owner
- **Specialist approval needed:** Clinical informatics, HIM, pharmacy, laboratory, patient identity

## Problem and evidence

Make duplicate registration, merges, merged-source access, deceased/retired lifecycle, encounter edits, signatures, vitals, prescriptions, orders, results, corrections, and clinical-list mutations share explicit identity, version, lifecycle, content-attestation, correction, and recovery contracts.

The linked `P2-03-*` findings show server-side duplicate and merge evidence gaps, non-atomic/non-versioned record mutations, incorrect post-merge/lifecycle actionability, signatures not tied to clinical content, unsafe post-lock writes/deletes, incomplete medication history, and laboratory/result/follow-up integrity gaps. They are grouped here because the shared cause is absence of a coherent clinical aggregate and correction boundary.

## Target state

Every supported clinical mutation identifies the current patient/encounter/laboratory aggregate, validates lifecycle and expected version, preserves the content and actor evidence required by its clinical policy, and uses a governed correction/forward-recovery path instead of destructive replacement.

## Expected value

Prevent wrong-patient writes, stale overwrites, contradictory signatures/snapshots, unsafe lifecycle actions, and irreversible loss of clinical history.

## Options considered

Do nothing leaves the validated blockers. A focused aggregate/version/event approach is preferred over a broad rewrite. Sequence identity/merge and lifecycle invariants first, then encounter/signature and result/order boundaries, then clinical-list and portal release corrections. Preserve compatibility adapters only while evidence proves equivalence.

## Dependencies and sequence

Require `P2-R004` bootstrap/constraint decisions and `P2-R001` protected-resource policy before irreversible data-contract changes. Deliver identity/lifecycle invariants first, encounter integrity next, clinical-list/prescription evidence after that, and the laboratory aggregate before external intake in `P2-R006`. `P2-R005` supplies the modern conflict/retry interaction; `P2-R007` owns concurrency, migration, and recovery proof.

## Acceptance criteria

Synthetic concurrent and sequential stale-write tests reject or reconcile every affected mutation; merge preview hashes/versions are revalidated at execution; merged/deceased/retired actions follow approved policy; signatures identify content/version; corrections retain before/after evidence; migration, rollback, audit continuity, and qualified clinical/HIM/pharmacy/laboratory sign-off are demonstrated.

## Scope and affected contracts

- Patient registration, duplicate review, demographic/contact, merge, identity/lifecycle, encounter, SOAP, forms, vitals, clinical lists, prescriptions, orders, specimens, reports, results, corrections, critical-result, and portal-lab APIs.
- DTO expected-version/error contracts, optimistic concurrency, database constraints, audit/event tables, history projections, migrations, recovery scripts, and backed-up data.
- Modern clinician mutation forms and patient shell, plus modern portal result views. FHIR/laboratory mapping coordinates with `P2-R006`; authorization coordinates with `P2-R001`.

## Delivery risk and rollback

Risks include rejecting valid legacy client writes, introducing conflict dialogs during care, incorrectly blocking exceptional documentation, and losing history through migration. Use additive version/event data, compatibility readers, feature-gated API requirements, migration checkpoints, reconciliation reports, and synthetic fault injection. When new clinical evidence exists, recover forward with a governed correction; never roll back by deleting that evidence.

## Size and difficulty rationale

XL scope crosses patient identity, encounter documentation, clinical evidence, laboratories, portal behavior, DTOs, migrations, and audit. Difficulty is Exceptional because content semantics, correction policy, concurrency, recovery, and clinical acceptance must align. EF Core and parameterized SQL remain complementary tools; no blanket persistence rewrite is implied.

## Phase 3 change packets

1. **R002-A — Patient identity and lifecycle:** duplicate gate, atomic demographics, expected versions, merge revalidation, and merged/retired/deceased invariant.
2. **R002-B — Encounter integrity:** expected versions, content-bound signatures, lock serialization, amendment path, vital validation, and provenance.
3. **R002-C — Clinical-list and prescription evidence:** granular permission, actor/reason/prior-current history, non-destructive correction, and recovery.
4. **R002-D — Laboratory clinical aggregate:** order/specimen/report/result identity, correction/review/critical follow-up, portal status context, migration, and reconciliation.

## Decision record

- **Decision:** Pending acceptance as a Phase 3 recommendation.
- **Decided by:** AvenChart program owner.
- **Date:** Not set.
- **Rationale and conditions:** `P2-D016` supplies the target policy. Acceptance requires named clinical/data owners, detailed exception/timing rules, migration/forward-recovery plan, specialist checklist completion, and the acceptance evidence above.
