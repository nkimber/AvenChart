# Sprint 62 plan: synthetic closure-status projection

Status: Implemented under [TH-DEC-0065](../decisions/0065-approved-sprint-62-synthetic-closure-status.md)

## Goal

Show an accurate, protected terminal status to the patient after the governed synthetic lifecycle closure, without representing it as appointment, encounter, clinical, legal, financial, claim, delivery, or integration completion.

## Delivery boundary

- Make `Closed` visible only through the existing owner-scoped status projections.
- Revalidate exact applicant lifecycle provenance, including encounter lock and closure events, before emitting an applicant terminal projection.
- Reuse neutral terminal content for established patients.
- Stop browser polling and remove terminal controls while preserving emergency guidance and no-store private status behavior.
- Expose no clinician identity, prescription, billing, claim, integration, external-action, or clinical-completion information.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership | Exact patient/applicant owner and current branded practice/facility scope only. |
| Provenance | Closed consultation/request, released reservation, ended session, in-progress appointment, active shift, encounter lock, and closure events. |
| Truthfulness | Terminal content explicitly says the appointment and encounter remain incomplete. |
| Privacy | No physician identity, prescription, patient chart, billing, claim, or external data. |
| Browser behavior | `Closed` stops polling and exposes no connection-room or refresh action. |
| Regression | Backend projector/policy, focused UI/polling, runtime safety, OpenAPI, authorization, planning, and Graphify evidence. |

## Gate preserved

Appointment and encounter completion, clinical completion, legal signature, AVS, patient delivery, prescribing delivery, billing, claims, integrations, and production remain separate gated work.
