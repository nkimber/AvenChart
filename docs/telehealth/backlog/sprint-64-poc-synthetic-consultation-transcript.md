# Sprint 64 plan: POC synthetic consultation transcript

Status: Approved under [TH-DEC-0067](../decisions/0067-approved-poc-synthetic-consultation-transcript.md)

## Goal

Make the POC’s patient–physician communication portion demonstrable with a tightly bounded, plain-text synthetic transcript during an active synthetic consultation.

## Delivery boundary

- Read and append only for the exact active request owner or consultation-owning physician.
- Enforce an affirmative synthetic-data acknowledgment and short printable message bound.
- Store immutable, source-bound messages with no legal effect.
- Poll only while the page is visible; do not claim realtime delivery.
- Create no media, notification, clinical documentation, prescription, billing, claim, integration, external, patient-care, or production capability.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Ownership | Patient/request and physician/consultation ownership fail closed. |
| Lifecycle | Read/append succeeds only in `InConsultation`; wrap-up and closed work is inaccessible. |
| Content | Explicit synthetic confirmation; 1–1000 printable characters; append-only persistence. |
| UX | Visible POC/emergency warning, confirmation-gated send, short polling, accessible status/error feedback. |
| Consequence | No realtime transport, media, recording, external delivery, clinical effect, financial effect, or production activation. |
| Regression | Backend and UI tests, migration/runtime validation, staging smoke, and Graphify evidence. |

## Gate preserved

This is not WebRTC, chat, notification, emergency triage, clinical documentation, or a real patient-care communication channel. Vendor selection, BAAs, privacy/security review, retention policy, operational escalation, and all production gates remain separate work.
