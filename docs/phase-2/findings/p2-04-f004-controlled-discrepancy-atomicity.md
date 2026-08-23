# P2-04-F004 — Controlled-count correction is not atomic with discrepancy closure

- Status: validated condition
- Domain(s): 03, 04, 07, 09, 10
- Coverage item(s): `COV-007`, `COV-008`, `COV-009`, `COV-014`
- Severity: high
- Production blocker: yes
- Reach: repeated for every controlled-count discrepancy correction
- Confidence: high static confidence
- Reviewers: `phase2_data`, `phase2_security_privacy`
- Independent verifier: separate `phase2_verifier` pass
- Specialist validation: controlled-inventory operations, pharmacy/legal policy, and database-concurrency review outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Correcting an investigating controlled-count discrepancy first commits a custody movement through its own connection and transaction, then separately attempts to mark the discrepancy corrected. The discrepancy is not first claimed or locked through both writes.

## Evidence

- `InventoryRepository.cs:475-478` reads the discrepancy and variance without a shared correction transaction.
- `InventoryRepository.cs:479` calls `CreateControlledCustodyMovementAsync`, whose lot, custody-event, and inventory-transaction writes commit independently at `InventoryRepository.cs:249-382`.
- `InventoryRepository.cs:480` opens a second connection for the discrepancy update; its explicit conflict error says the correction may already have posted.
- `V0061__inventory_controlled_count_sessions.sql:31-44` retains a correction event ID but does not make one custody event unique to one discrepancy.
- The movement idempotency key is unique, but separate callers or a UI retry can supply different keys for the same discrepancy.

## Consequence

A fault between commits leaves controlled stock and custody evidence corrected while the discrepancy remains investigating. Two concurrent corrections with distinct keys can both change quantity while only one closes the discrepancy.

## Cause and reach

The correction workflow spans two transaction owners without a discrepancy-level claim, lock, or uniqueness invariant. The underlying custody movement transaction is internally strong; the defect is in composing it with discrepancy state.

## Risk calibration

The result can overcorrect controlled quantity and leave durable custody evidence inconsistent with reconciliation state. Repair requires another governed compensating movement, supporting high severity and future-production blocker status.

## Uncertainty and counterevidence

Identical-key replay is idempotent, custody movements lock stock rows, prevent negative balances, and append immutable events. A disposable PostgreSQL fault and two-session interleaving remain outstanding.

## Validation record

The data pass established the split transaction and the independent verifier reproduced the distinct-key schedule. The suspected concurrent active-count creation race was separately falsified because the common controlled-location row is locked before the active-count check.

## Disposition

Validated source-level condition and future-production blocker. No implementation recommendation is made.
