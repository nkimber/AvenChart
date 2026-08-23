# Phase 2 calibration report

## Decision summary

The four-slice calibration is accepted. The review contract produced materially consistent execution traces, independent discovery of the highest-consequence conditions, severity differences within the permitted range, explicit counterevidence, and reliable specialist escalation. Independent verifiers narrowed over-broad claims without dismissing material risk.

The method is ready to scale to the full Phase 2 coverage matrix. This is an assessment-launch decision only. Production use remains prohibited, no scorecard domain is complete, no candidate finding is a legal/compliance or clinical-safety conclusion, and no Phase 3 product change is authorized.

## Calibration results

| Pilot | Independent agreement | Verifier result | Principal calibration value | Specialist work still open |
| --- | --- | --- | --- | --- |
| A — Identity and PHI | Strong agreement on development-only staff identity, unscoped patient access, broad PHI response, and audit-resource gap | Core conditions corroborated; HTTPS narrowed by secure Azure ingress; response-status fidelity remains unproved | Demonstrated that deployment countercontrols and audit pipeline timing must be checked before severity is fixed | Security/privacy, clinical operations, legal/compliance, deployment operations |
| B — Encounter lifecycle | Strong agreement on signature/content binding, inconsistent concurrency, and fragmented provenance; each specialist added distinct conditions | High/systemic clusters corroborated; stale-write severity narrowed pending clinical reproduction | Demonstrated that similar “versioned” features can have materially different content, locking, and client-state guarantees | Clinical informatics, legal/compliance, database/operations, coding/terminology |
| C — Critical lab result | Strong independent agreement on vocabulary mismatch, hidden backlog, closed-loop boundary, and evidence deletion | All five clusters corroborated; consequence and application-boundary adequacy remain specialist-dependent | A green test suite coexisted with a deterministic UI-to-database vocabulary mismatch because the proof seeded the downstream spelling directly | Practicing clinician, clinical operations, records management, database/operations |
| D — Accessibility and recovery | Independent agreement on the systemic evidence gap and repeated dynamic-status weakness | Gate, sign-in, and skip-target conditions reproduced; appointment error narrowed because the exception path has a live toast | Demonstrated that source search must be confirmed in the rendered document and that negative states need focus/live-region inspection | Accessibility specialist, assistive-technology users, users with disabilities |

## Acceptance measures

- Both reviewers in every pilot identified the material execution path and system boundaries.
- No blocker or high condition found by one reviewer was dismissed without recorded evidence.
- Severity differences were within one level or explicitly retained for focused evidence and program-owner decision.
- Verifiers split or narrowed claims when countercontrols applied instead of forcing uniform severity.
- Factual claims have stable citations, reproducible commands, and visible fact/inference separation.
- Symptoms were reconciled to common causes without losing distinct affected workflows.
- Clinical, security/privacy, legal/compliance, accessibility, coding, and operations validation needs were consistently recognized.
- Reviewers used the approved coverage, finding, severity, confidence, and output structures without inventing an incompatible schema.
- The coordinator synthesized each packet without needing raw shell logs or re-performing the entire review.
- The evidence volume was substantial but manageable with the default limit of three concurrent specialists.

## Contract changes demonstrated by the pilot

The accepted assessment skill and operating manual now require reviewers to:

1. trace representative values through the real UI or integration entry contract, validation, normalization, persistence, downstream filters, rendering, audit, and recovery;
2. identify the exact content/version covered by signatures, acknowledgements, approvals, and other state proofs, including relevant mutation interleavings;
3. evaluate audit evidence using protected resource, actor, executed outcome, event timing, retention behavior, and privacy tradeoff;
4. induce asynchronous failure states and inspect active focus, status announcements, bypass targets, and recovery rather than relying on initial-page axe results;
5. compare test fixtures with values actually produced by the UI or integration contract.

The second accessibility pass also corrected an over-broad first-pass inference: a global skip link exists, but its login target is absent and its authenticated portal target is weak. The retained record states the reproduced reach rather than the original broad wording.

## Coverage inventory reconciliation

- COV-002 paths were corrected from provisional `Persistence/*` locators to the actual `Security/*`, `Data/Auth*`, `Data/PhiAudit*`, filter, catalog, and migration paths.
- COV-006 was narrowed to the local laboratory/procedure lifecycle.
- COV-019 was added for external laboratory-result ingestion, terminology normalization, duplicate/correction handling, notification, and reconciliation because Pilot C found no supported inbound path to assume inside COV-006.
- The reference interface remains a separate coverage item because the automated accessibility gate does not exercise it.
- Pilot evidence samples broad rows but does not mark those rows complete; full row assessment and scorecard reconciliation remain ahead.

## Evidence limitations carried into the full assessment

- No database-changing calibration experiment was run, so concurrency, audit-result timing, deletion recovery, and some correction scenarios remain focused Level 2/3 evidence tasks.
- Builds and focused tests passed, but the complete synthetic runtime, migration replay, backup/restore, browser matrix, and operational failure exercises were not run as one clean-baseline campaign.
- Production topology and identity-provider configuration are not approved.
- No qualified clinician, security/privacy specialist, legal/compliance specialist, accessibility specialist, coding specialist, or production database operator has yet accepted the domain consequences.
- The scorecard remains unscored; pilot candidates are not a complete findings register.

## Full-assessment operating recommendation

Launch Phase 2 in bounded read-only batches of no more than three specialists. Begin with cross-cutting foundations and safety-critical evidence that can change the interpretation of many later rows:

1. API/identity/authorization/audit boundaries and production configuration;
2. data model, EF/SQL boundary, PostgreSQL schema, migrations, retention, backup, and recovery;
3. testing/runtime evidence plus representative clinical workflows, starting with the corroborated pilot conditions;
4. remaining clinical, administrative, integration, frontend/reference, accessibility, deployment, dependency, and documentation rows;
5. independent validation, scorecard synthesis, canonical findings, recommendations, and the dependency-aware Phase 3 roadmap.

Specialist availability should be scheduled alongside the applicable batches. Missing specialists do not stop technical evidence collection, but affected conclusions cannot advance beyond `needs-specialist-validation`.
