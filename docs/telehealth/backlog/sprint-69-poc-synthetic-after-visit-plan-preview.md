# Sprint 69 plan: POC synthetic after-visit plan preview

Status: Implemented and verified under [TH-DEC-0072](../decisions/0072-approved-poc-synthetic-after-visit-plan-preview.md)

## Goal

Make the existing physician-authored synthetic disposition available to the exact patient owner after governed synthetic closure, while clearly separating the result from an actual after-visit summary, delivered clinical instruction, or completed care.

## Delivery boundary

- Atomically create one immutable preview with the existing synthetic closure after the governed encounter lock, current disposition, and final-review evidence are present.
- Permit a minimized no-store read only for the authenticated established patient or exact applicant access-key owner of the closed request.
- Show only synthetic disposition/follow-up fields already authored by the physician, their source versions/timestamp, and explicit non-production/non-delivery boundaries.
- Keep clinician identity, diagnosis, medications, prescriptions, pharmacy, billing, claims, insurance, notification, download/print, outbox, external delivery, appointment/encounter completion, legal effect, and production disabled.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Atomicity | Preview creation commits with the permitted synthetic closure transition, or neither commits. |
| Source binding | The preview checks locked encounter, closed lifecycle, disposition/final-review versions, and its one-way fingerprint. |
| Ownership | Exact practice/facility/request and patient or applicant-access-key owner only. |
| Truthfulness | Content says `NON_PRODUCTION` and never implies medical advice, AVS delivery, appointment/encounter completion, legal effect, or external action. |
| Delivery boundary | No notification, download, print artifact, outbox, external destination, or lifecycle-state change. |
| Regression | Migration, authorization, API, UI, runtime safety, staging, and Graphify evidence pass. |

## Gate preserved

A real signed-record AVS, clinical instructions, accessible download/print, notification, amendments/re-notification, follow-up work queues, appointment/encounter completion, prescription/pharmacy delivery, billing, claims, integrations, and production remain separately governed work.

## Verification record

- Backend build and regression suite: passed (794 tests).
- Telehealth UI type check and regression suite: passed (340 tests); focused affected tests: passed (68 tests).
- Telehealth migration-resilience suite: passed (292 migrations).
- Loopback staging rebuild, OpenAPI contract validation, and runtime-safety validation: passed.
- Planning-artifact validation, Graphify refresh, portability check, and changed-file review: passed.
