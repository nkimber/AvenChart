# Decision 0073: POC local browser WebRTC media

Status: approved for the non-production POC only

## Decision

Permit a strictly local-browser WebRTC proof of concept between the two holders of existing short-lived synthetic waiting-room grants. The feature is opt-in and remains disabled unless `Telehealth:LocalWebRtcPocEnabled` is set in a non-production environment.

## Boundary

- Browser media starts only after a deliberate camera/microphone action from each participant. The server never receives media.
- The existing patient/applicant and physician grants remain the only authorization material. Every signaling read and write verifies the exact issued grant, its one-way credential hash, role, expiry, `Connecting` request, and waiting-room session.
- SDP and ICE candidates live only in process memory for the short grant lifetime. They are not written to a database, audit payload, log, browser storage, transcript, recording, or external service.
- The relay accepts only compact JSON `offer`, `answer`, and `candidate` messages, exposes only the other participant's messages, bounds message size/count, and fails closed when disabled, expired, revoked, or out of lifecycle.
- There is no TURN/STUN vendor, recording, transcription, screen share, attachment, chat replacement, notification, clinical documentation, consultation completion, prescription transmission, billing, claim, external integration, or production activation.

## Verification

The slice requires grant/role/expiry relay tests, API contract and runtime-safety coverage, browser type/UI tests, loopback staging verification in a secure localhost context, and Graphify review. A process restart intentionally clears the relay and requires both participants to reconnect while their grants are valid.
