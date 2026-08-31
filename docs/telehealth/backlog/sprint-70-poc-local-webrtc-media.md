# Sprint 70 plan: POC local browser WebRTC media

Status: Implemented and staging-verified under [TH-DEC-0073](../decisions/0073-approved-poc-local-webrtc-media.md)

## Goal

Demonstrate a real, local-only browser audio/video connection for the exact patient/applicant and physician waiting-room participants without converting the synthetic POC into a vendor media, recording, care-delivery, or production feature.

## Delivery boundary

- Reuse existing short-lived connection grants and verify them on every transient signaling exchange.
- Relay only offer, answer, and candidate JSON in process memory; never persist or log signaling or media.
- Require explicit browser media permission and offer an accessible, visible local/remote preview plus an end control.
- Keep relay activation off by default and limited to non-production local POC configuration.

## Acceptance matrix

| Area | Required evidence |
| --- | --- |
| Authorization | Exact current grant, credential, role, waiting-room session, request state, and expiry are required for every relay call. |
| Privacy | No SDP/ICE persistence/logging, recording, transcription, media upload, browser storage, or external destination. |
| Transport | Only native peer-to-peer WebRTC offer/answer/candidate exchange; no vendor SDK, TURN/STUN, or signaling persistence. |
| UX | User-initiated device permission, local/remote preview, error recovery, visibility-aware polling, keyboard controls, and track cleanup. |
| Consequence | No clinical, documentation, prescription, billing, claim, appointment/encounter completion, notification, or production consequence. |
| Regression | Backend/UI/API/runtime/staging/Graphify evidence passes. |

## Gate preserved

Production media architecture, TURN capacity, vendor selection and agreements, recording/retention policy, privacy/security review, emergency escalation, accessibility certification, clinical workflow approval, and every production gate remain separately governed work.

## Implementation evidence

- The relay is an in-process, expiry-cleaned, role-separated offer/answer/candidate buffer; the API reauthorizes the exact issued connection grant on every read and write.
- Native browser media is available only when the staging-only flag is true. The physician UI requires a connected peer before it enables the existing synthetic consultation-start action.
- Candidate-before-description ordering is handled locally: early remote ICE candidates are held only in browser memory until the matching remote SDP is applied, then flushed in received order.
- Backend regression (800 tests), UI regression (343 tests), runtime-safety, planning, live OpenAPI, Docker staging health, and bundle checks passed for this slice.
