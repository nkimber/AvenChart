# P2-07-F003 — Integration idempotency identities are not content-bound

- Status: validated condition
- Domain(s): 04, 07, 09, 10
- Coverage item(s): `COV-009`, `COV-010`, `COV-014`
- Severity: medium
- Production blocker: unknown pending partner and payload scope
- Reach: repeated across inbound and outbound integration contracts
- Confidence: high static and synthetic runtime
- Reviewer: `phase2_data`
- Independent verifier: `phase2_verifier`
- Specialist validation: integration owner and database specialist outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Reusing an outbox idempotency key or inbox source-message ID with different semantic content returns the existing record as a normal replay without comparing or retaining the divergent request.

## Evidence

- Outbox enqueue conflicts only on `idempotency_key`, performs a no-op update, and returns the existing row without comparing event type, aggregate, destination, or payload (`IntegrationRepository.cs:24-64`).
- `idempotency_key` is nullable, so requests without a key can create multiple logically identical events (`V0003__integration_inbox_outbox.sql:3-20`).
- Inbox uniqueness is only `(source, source_message_id)` (`V0003__integration_inbox_outbox.sql:26-37`). On conflict the repository returns `Duplicate=true` without comparing `message_type` or payload (`IntegrationRepository.cs:168-224`).
- Inbox history supports reconcile/reject but no correction, supersession, schema version, or content digest (`V0232__integration_inbox_reconciliation.sql:1-2`).

## Consequence

A sender that accidentally reuses an identifier for corrected or different content receives a successful duplicate response while the differing content disappears. No clinical or financial loss is claimed because no supported material producer or partner adapter was found.

## Cause and reach

Database uniqueness establishes race-safe identity but assumes that the identity proves semantic equivalence. The generic contract affects every caller that uses these inbox/outbox paths.

## Risk calibration

Medium is appropriate until an external contract demonstrates immutable IDs and a correction rule requiring new IDs. Impact and reversibility could be higher when partner-originated clinical or financial messages are enabled.

## Uncertainty and counterevidence

Exact replays are safely collapsed; inbox callers receive an explicit duplicate flag; and versioned reconciliation decisions are atomic and reasoned. The current local-only boundary may intentionally rely on immutable source IDs.

## Validation record

Static review and independent verification corroborated the behavior. Synthetic runtime then submitted `lab.result.v1` twice with the same source/message ID but different result values: the first receipt returned `201`, the divergent replay returned `200` with `duplicate = true`, and the clinical laboratory-result count did not change. Concurrent replay remains outstanding.

## Disposition

Validated engineering-readiness condition with conditional production impact. No Phase 3 implementation recommendation is accepted.
