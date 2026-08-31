# Sprint 68 plan: POC synthetic post-visit receipt

Status: Implemented and verified under [TH-DEC-0071](../decisions/0071-approved-poc-synthetic-post-visit-receipt.md)

## Goal

Exercise the patient-facing, immutable post-visit projection seam after an existing synthetic lifecycle closure without representing a real after-visit summary or completed care.

## Delivery boundary

- Atomically create one source-bound receipt when the physician's existing governed synthetic closure succeeds.
- Permit a minimized no-store read only for the authenticated established patient or exact applicant access-key owner of that closed request.
- Show only the fixed POC lifecycle statement, source timestamps/versions, and clear non-completion boundaries.
- Keep clinical content, physician identity, prescriptions, pharmacy choice, care instructions, AVS download/print, notification, billing, claims, external delivery, appointment/encounter completion, and production disabled.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Atomicity | Receipt creation commits with the only allowed synthetic closure transition, or neither commits. |
| Ownership | Exact practice/facility/request and patient or applicant-access-key owner only. |
| Truthfulness | Content says `NON_PRODUCTION`; lifecycle closure does not mean appointment, encounter, clinical, legal, financial, or claim completion. |
| Privacy | No clinician, clinical, medication, pharmacy, billing, insurance, or claim content. |
| Delivery boundary | No notification, download, print artifact, outbox, external destination, or change to lifecycle state. |
| Regression | Migration, authorization, API, UI, runtime safety, staging, and Graphify evidence pass. |

## Gate preserved

The real signed-record AVS, accessible download/print, patient notification, care instructions, follow-up, prescriptions and pharmacy delivery, appointment/encounter completion, billing, claims, integrations, and production remain separately governed work.
