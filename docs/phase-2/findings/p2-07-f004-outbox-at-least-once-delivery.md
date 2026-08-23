# P2-07-F004 — Outbox delivery is at-least-once across the transport boundary

- Status: validated condition
- Domain(s): 07, 10, 11
- Coverage item(s): `COV-010`, `COV-016`, `COV-017`
- Severity: medium
- Production blocker: unknown pending external delivery and partner-idempotency policy
- Reach: repeated across future outbox destinations
- Confidence: high static; fault injection outstanding
- Reviewer: `phase2_data`, `phase2_quality_operations`
- Independent verifier: `phase2_verifier`
- Specialist validation: integration operations/SRE and interoperability outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The outbox claim commits before transport delivery and completion commits afterward. A process failure after external delivery but before local completion, or a delivery longer than the five-minute lease, can permit redelivery.

## Evidence

- `DispatchAsync` claims, calls `DeliverAsync`, then completes in separate operations (`IntegrationRepository.cs:102-117`).
- The claim transaction commits before transport invocation (`IntegrationRepository.cs:258-290`).
- Expired `dispatching` leases are changed to `retry-scheduled` and can be claimed again (`IntegrationRepository.cs:293-326`).
- `IIntegrationTransport` has no durable acknowledgement or destination deduplication contract (`IIntegrationTransport.cs:8-13`).
- The only registered implementation is local deterministic transport (`Program.cs:42`; `LocalDeterministicIntegrationTransport.cs:8-61`).

## Consequence

An external destination can receive duplicate effects after a crash, timeout, or lease overlap unless it deduplicates by a stable event identity. No external duplicate was observed in this local-only implementation.

## Cause and reach

The design deliberately uses at-least-once local state transitions but does not define the partner acknowledgement, idempotency, or exactly-once policy needed at an external boundary.

## Risk calibration

Medium is appropriate while no external adapter exists. The consequence becomes production-blocking only if an external destination relies on exactly-once effects or lacks event-idempotency.

## Uncertainty and counterevidence

Claims are transactional, compare-and-set completion is guarded, leases recover abandoned work, retries are bounded, quarantine is explicit, and recovery is reasoned and actor-attributed. Migration comments explicitly limit this to a local contract.

## Validation record

Static state-machine review and independent verification corroborated the at-least-once mechanics. Crash-after-delivery, lease overlap, and partner deduplication were not tested.

## Disposition

Validated integration-readiness condition with conditional production impact. No Phase 3 implementation recommendation is accepted.
