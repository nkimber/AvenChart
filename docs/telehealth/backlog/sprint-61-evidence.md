# Sprint 61 evidence: synthetic visit closure

Status: Implemented and automated verification complete; all clinical, legal, billing, claims, integration, accessibility, security, operational, and production approvals remain open

Decision: [TH-DEC-0064](../decisions/0064-approved-sprint-61-synthetic-visit-closure.md)

Plan: [Sprint 61 synthetic visit closure](sprint-61-synthetic-visit-closure.md)

## Implemented boundary

- The exact owning physician may close only an encounter-locked, unfinished synthetic consultation/request and return the existing physician shift to availability.
- The serializable transaction requires the current `MediaEnded`/`WrapUp` lineage, ended session, released reservation, in-progress appointment, active adult patient shell, and governed encounter lock.
- The consultation and request become `Closed`; the appointment remains in progress. Append-only events and semantic replay provide the trace and retry outcome.
- The private control appears only after the current physician workspace records the encounter lock. No encounter completion, patient delivery, billing, claims, pharmacy transmission, integration, or external action is created.

## Automated evidence

- Backend suite: 769 tests passed.
- Focused UI, TypeScript build, runtime safety, OpenAPI, and authorization checks passed.
- The additive closure migration was applied through the standard local migrator; migration/recovery, planning, and Graphify portability checks passed.

## Open gates

Clinical completion, appointment fulfillment, patient-facing completion/status delivery, AVS, legal signing, coding, billing, claim generation/submission, payer/clearinghouse transport, pharmacy delivery, independent review, and production remain open.
