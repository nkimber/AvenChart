# P2-04-F006 — EOB import consumes an unreserved payment-session sequence value

- Status: validated condition
- Domain(s): 04, 07, 09, 10
- Coverage item(s): `COV-007`, `COV-009`, `COV-014`
- Severity: medium
- Production blocker: no by itself
- Reach: repeated after every successful two-row EOB import
- Confidence: high for the baseline schema contract; deployed-live state unverified
- Reviewer: `phase2_data`
- Independent verifier: separate `phase2_verifier` follow-up
- Specialist validation: PostgreSQL runtime and billing-operations review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The EOB importer requests one value from `payment_sessions_id_seq`, then explicitly inserts two payment sessions using that value and its local successor. The database sequence advances only once, so the next ordinary sequence-backed payment-session insert receives the already consumed second ID.

## Evidence

- `BillingRepository.cs:2339-2364` calls `nextval('payment_sessions_id_seq')` once and then uses `sessionId = nextSessionId++` for two inserts.
- `BillingRepository.cs:3135-3148` executes the scalar sequence request exactly once.
- `V0245__remaining_global_identity_sequences.sql:9-15` creates the ordinary increment-by-one sequence owned by `payment_sessions.id`.
- Ordinary adjudication and payment paths request one sequence value per session at `BillingRepository.cs:1843-1847,2021-2026`.

## Consequence

The first ordinary payment/adjudication write after an EOB batch receives the already inserted second ID and fails its transaction on the payment-session primary key.

## Cause and reach

A single sequence value is treated as the start of a locally reservable block without reserving the second value from PostgreSQL.

## Risk calibration

The failure is deterministic and affects a financial workflow, but the ordinary transaction rolls back, the collision is immediately visible, and the failed nontransactional `nextval` advances the sequence so a later retry normally succeeds. Medium severity and non-blocker status are appropriate.

## Uncertainty and counterevidence

No live schema query was available. A pre-existing sequence with an unusual increment or an out-of-band operational repair could change the exact outcome because the migration uses `CREATE SEQUENCE IF NOT EXISTS`; no such mechanism appears in the reviewed baseline.

## Validation record

The data specialist found the allocator path and a separate verifier independently traced the primary-key collision and retry behavior.

## Disposition

Validated source/schema condition. It remains distinct from `P2-04-F005` because the allocator defect would survive replacement of the fixture payload with real remittance data. No implementation recommendation is made.
