# Sprint 62 evidence: synthetic closure-status projection

Status: Implemented and automated verification complete; all clinical, legal, billing, claims, integration, accessibility, security, operational, and production approvals remain open

Decision: [TH-DEC-0065](../decisions/0065-approved-sprint-62-synthetic-closure-status.md)

Plan: [Sprint 62 synthetic closure-status projection](sprint-62-synthetic-closure-status.md)

## Implemented boundary

- Existing patient and applicant owner-scoped status projections can represent a `Closed` synthetic lifecycle.
- Applicant source validation requires the closed consultation/request lineage, governing encounter lock, closure events, active returned shift, and still-in-progress appointment.
- Terminal content is clear that the appointment and encounter remain incomplete. It presents no physician identity, prescription, billing, claim, delivery, integration, external action, or care-completion claim.
- Browser polling and connection controls stop at terminal closure while emergency guidance remains available.

## Automated evidence

- Backend projector/policy and full backend suite passed.
- Focused patient-status UI/polling tests and TypeScript build passed.
- Runtime safety, OpenAPI, authorization, planning, migration/resilience, and Graphify portability checks passed.

## Open gates

Appointment and encounter completion, clinical completion, legal signing, AVS, patient delivery, prescription delivery, coding, billing, claims, payer/clearinghouse transport, pharmacy delivery, integrations, independent review, and production remain open.
