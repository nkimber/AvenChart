# Video, realtime status, and communication

## 1. Technology choice

WebRTC is the browser media foundation, but raw peer-to-peer APIs are not a complete healthcare video service. The initial production design should use a managed, HIPAA-capable video provider behind `ITelehealthVideoProvider`, subject to security review and a business associate agreement. A local WebRTC-compatible simulator/stub supports development. Selection must consider supported browsers/devices, TURN coverage, regional availability, encryption, accessibility, waiting-room controls, data location, subprocessors, incident terms, deletion, telemetry, and no-recording enforcement.

ASP.NET Core SignalR is appropriate for AvenChart request/queue presence and status notifications. It must not carry video media, be the sole source of truth, or implement domain transitions in hub methods. Clients reconcile notifications with versioned HTTP state.

## 2. Components

```text
AvenChart API/domain
  -> creates VideoSession intent and participant grants
  -> ITelehealthVideoProvider
       -> managed provider adapter (production after certification)
       -> deterministic simulator (development/test only)

Patient/physician clients
  -> short-lived, participant/session-scoped provider token
  -> provider media/signaling/TURN
  -> SignalR for AvenChart status notification
  -> HTTPS API for authoritative state and commands
```

A video provider receives opaque participant and session IDs plus the minimum display/operational data. It does not need the full chart, triage, coverage, prescription, or claim.

## 3. Waiting room and join

1. Server creates a video intent only after reservation.
2. Each participant requests a short-lived, single-session grant after current authentication/authorization checks.
3. Client runs camera, microphone, speaker, bandwidth and browser checks, with keyboard/screen-reader accessible controls and plain recovery guidance.
4. Patient enters a private waiting room; no patient can see or hear another patient.
5. Physician enters the same reserved session, completes clinical start checks, and explicitly starts the consultation.
6. Server records session lifecycle metadata and media quality, not media content.
7. On disconnect, clients retry within policy and offer approved contact/fallback. The encounter remains physician-owned until disposition.

## 4. Security and privacy

- TLS and provider-supported encrypted media are mandatory; provider security claims are verified during procurement.
- Tokens are short-lived, audience/session/role-bound, non-guessable, and never placed in URLs, referrers, logs, analytics, screenshots, or support tickets.
- Session identifiers are random and practice/request bound; provider webhooks require signature, freshness, replay, origin/destination, and event-id validation.
- Recording, transcription, generative summaries, face recognition, background capture, vendor training use, and persistent media are disabled contractually and technically.
- Staff cannot silently join. Participant roster and join/leave status are visible. Additional participant/interpreter support requires explicit approved workflow and consent.
- Browser permissions are requested just in time. The UI clearly indicates camera/microphone status and supports leaving immediately.
- CSP, permissions policy, secure cookies, safe iframe policy, origin allowlists, and cross-site protections apply to branded/custom domains.

## 5. Failure and fallback

| Failure | Required response |
|---|---|
| Permission/device failure before join | Guided retry/device switch; accessible support route; do not start encounter |
| Bandwidth degradation | Adaptive quality; preserve audio; show status without clinical inference |
| Media disconnect | Attempt reconnect; display callback/emergency plan; notify physician; preserve request/encounter state |
| Provider outage | Stop new joins, keep authoritative queue, present honest outage status, execute practice continuity plan |
| Patient loses app connection | Push/SMS minimal notification if consented; polling/relogin restores state |
| SignalR failure | Fall back to bounded polling; do not change business state based on presence alone |
| Video remains inadequate | Physician chooses in-person/emergency/technical-abort or audio fallback only if all governing rules permit |

Audio-only fallback is a distinct modality decision. It records who decided, why, start/end, state/payer/protocol permission versions, patient agreement, exam limitations, and resulting billing rules.

## 6. Written communication

Session text supports connection help and clinician-patient communication. It is not a hidden permanent transcript or a substitute for required charting. The clinician explicitly incorporates clinically relevant messages/attachments into the encounter with provenance before signing. Operational session content follows a counsel-approved short retention/deletion rule and legal hold; audit metadata remains. Attachments are type/size scanned, isolated, and treated as untrusted PHI.

## 7. Video and realtime requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-VID-001 | Production video MUST use an approved HIPAA-capable provider with executed BAA and completed security/privacy/accessibility/continuity review behind a vendor-neutral adapter. | Procurement/certification gate. |
| TEL-VID-002 | The platform MUST NOT record, transcribe, summarize, or persist media; controls must be disabled in provider tenant, tokens, UI, API, and contract. | Configuration inspection and negative tests. |
| TEL-VID-003 | Video grants MUST be short-lived, one-participant/role/session/practice bound, issued only after current authorization, and unusable after cancellation/end/revocation. | Token abuse tests. |
| TEL-VID-004 | Waiting rooms MUST isolate patients and show an accurate participant roster; no staff/third party may join invisibly. | Multi-session isolation tests. |
| TEL-VID-005 | Device preflight and recovery MUST support keyboard, screen reader, captions/provider accessibility where applicable, device switching, and plain-language errors. | Browser/device/accessibility matrix. |
| TEL-VID-006 | A video-provider event MUST NOT change clinical/request state without validated signature/replay protection, state preconditions, and domain-command handling. | Forged/replayed webhook tests. |
| TEL-VID-007 | SignalR messages MUST contain minimum necessary identifiers/status/version, use authorized practice/request groups, and trigger authoritative HTTP reconciliation. | Hub authorization and out-of-order tests. |
| TEL-VID-008 | Polling MUST preserve full status functionality when realtime delivery is unavailable and apply bounded backoff/jitter. | Network degradation test. |
| TEL-VID-009 | Disconnect/reconnect MUST preserve encounter ownership and prompt safe follow-up; it MUST NOT create a duplicate encounter or silently complete a visit. | Chaos/reconnect tests. |
| TEL-VID-010 | Audio-only fallback MUST be deny-by-default and require current state, payer, practice, protocol, physician, and patient approval evidence. | Cross-state/payer fallback tests. |
| TEL-VID-011 | Provider metadata and telemetry MUST exclude chart/diagnosis/coverage/prescription details and must not use patient names where opaque identifiers suffice. | Vendor data-flow/log review. |
| TEL-VID-012 | Webhooks and adapter commands MUST be idempotent and preserve transport versus business/session state. | Duplicate/delayed webhook tests. |
| TEL-VID-013 | Session chat/attachment handling MUST have an approved retention rule, malware/content protections, chart-incorporation provenance, and no unreviewed transcript claim. | Content lifecycle test. |
| TEL-VID-014 | A provider outage MUST activate a documented continuity mode with new-join control, queue preservation, patient notice, clinician disposition ownership, and recovery reconciliation. | Outage exercise. |
| TEL-VID-015 | Production startup MUST fail when the simulator is configured or recording/transcription cannot be proven disabled. | Environment safety test. |
| TEL-VID-016 | Supported browser/device/network baselines and provider capabilities MUST be published, monitored, and re-certified after significant SDK/provider changes. | Compatibility certification report. |

## 8. Provider adapter operations

The port supports `CreateSession`, `IssueParticipantGrant`, `RevokeParticipant`, `EndSession`, `GetSessionStatus`, and validated webhook normalization. It returns opaque provider references and normalized statuses. It never exposes provider secrets to the browser or returns a success that means the consultation clinically started/completed.

