# Decision 0001: G0 telehealth development baseline

Status: Approved for development planning  
Decision date: 2026-08-26  
Decision owner: Project owner  
Supersedes: None

## Decision

The project owner approved the immediate-telehealth master specification as the product and architecture baseline for backlog creation and foundation planning.

The following decisions are approved:

1. The initial-release boundary in the master specification is confirmed.
2. The project owner is the single accountable decision owner for product decisions and the resolution of open decisions.
3. The technical architecture in specification 13 is approved.
4. The recommended first vertical slice is approved: practice-branded entry through established-patient location/emergency screening, deterministic synthetic triage, administrative authorization, queueing, and atomic clinician reservation.
5. A complete requirement-linked backlog and Sprint 1 plan will be created.
6. The specified engineering safeguards are approved as required controls.
7. The provisional clinical framework is approved for engine and workflow design: five outcomes, emergency-before-insurance, unknown-to-review, clinician-only review authority, candidate pathway structure, and deterministic versioned protocols.
8. Accessible low-fidelity wireframes will define the initial patient, administrator, and physician flows.

## Interpretation

This approval authorizes planning and design. It does not claim that the project owner personally substitutes for licensed medical, legal, privacy, security, billing, accessibility, or credentialing review required before real patient care. Exact clinical content, state legal wording, vendors, payer rules, credentials, and production evidence remain governed by gates `G2` through `G4`.

The repository's pre-existing Phase 2 exit gate remains a separate control. At the time of this decision it states that implementation authorization is pending and prohibits application, database, deployment, test, and runtime changes. This decision does not silently close that gate or accept its 39 High findings. Active CI/test scaffolding and feature code require either:

- explicit Phase 2 gate closure with its required evidence; or
- an explicit, scoped program-owner override identifying affected findings, accepted residual risk, compensating controls, owner, purpose, and review/expiry date.

Documentation, backlog preparation, wireframes, and proposed safeguard contracts may proceed without representing the feature as implementation-authorized.

## Consequences

- The master specification and its 329 requirements are the authoritative telehealth product baseline.
- The machine-readable backlog must map every requirement exactly once as a primary delivery responsibility and may add secondary relationships.
- Sprint 1 must remain a disabled, synthetic-only foundation and may start only after the repository implementation gate permits code changes.
- A change to a confirmed release decision requires a superseding decision record and impact review across requirements, backlog, tests, wireframes, and rollout gates.

## References

- [Master specification](../README.md)
- [Product scope](../01-product-scope.md)
- [Technical architecture](../13-technical-architecture.md)
- [Testing and traceability](../19-testing-acceptance-and-traceability.md)
- [Rollout and approval gates](../20-rollout-metrics-risks-and-approvals.md)
- [Phase 2 exit gate](../../phase-2/phase-2-exit-gate.md)

