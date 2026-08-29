# Decision 0056: Sprint 53 applicant request queue status

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one applicant-originated request to read and poll a minimized status projection only after the exact Sprint 51 operational-review submission exists. The projection may show `OperationalReview` version 12 or `Queued` version 13 and later queued-version refreshes while the one queue entry remains `Ready`.

The policy `SYNTHETIC_APPLICANT_REQUEST_QUEUE_STATUS` version 1 is read-only. It creates no queue entry, assignment, appointment, coverage, consent, encounter, care, prescription, claim, integration, message, or external action. It does not expose later clinician reservation, connection, consultation, wrap-up, completion, or cancellation states; those remain separately gated.

## 2. Status meaning

`OperationalReview` means the applicant submitted the current synthetic evidence and the configured practice has not accepted the request into its clinician queue. `Queued` means the exact Sprint 52 staff authorization, unassigned appointment, and `Ready` queue entry exist for the same applicant, patient shell, request, practice, and facility.

Only `Queued` may return an approximate count of same-practice, same-facility `Ready` requests ordered ahead at snapshot time. This is not an assigned queue position, clinical priority, or wait-time promise and may change for safety or operational reasons. `renderingPhysicianAssigned`, `renderingPhysicianIdentityDisclosed`, `coverageVerified`, `consentCreated`, `careAuthorized`, `integrationEnabled`, and `externalCallPerformed` remain false.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, practice-branded-host scoped, and applicant-access-key protected.
2. The applicant must remain exactly `SyntheticRequestCreated` version 26 with an unexpired key. Staff and patient-portal sessions cannot substitute for the key; foreign keys receive an opaque not-found response.
3. The server rebinds the portal-disabled unmerged patient shell, applicant-created request, passing triage, exact Sprint 51 submission, and, for `Queued`, the exact Sprint 52 authorization, appointment, and single queue entry.
4. Before staff acceptance, the projection is only `Reviewing`; it exposes no queue count and keeps practice acceptance and doctor search false.
5. After staff acceptance, the projection is only `InQueue`; it may expose an approximate nonnegative requests-ahead count while exact-position-assigned and wait-estimate-available remain false.
6. The response is private/no-store, contains no applicant or canonical patient identifier, access key, clinician identity, provider identifier, NPI, member or payer evidence, diagnosis, medication, prescription, claim, encounter, price, or clinical narrative.
7. The UI polls authoritative HTTP at a server-directed interval while visible, pauses while hidden, aborts obsolete requests, preserves the last confirmed state during transient failure, and offers keyboard-operable manual recovery without stealing focus.
8. A pre-eligible `409` remains silent and retries because the applicant may still be completing the request. Expired sessions stop with restart direction; malformed, stale, foreign, changed-provenance, duplicate, and unsupported states fail closed.
9. No status payload or queue evidence is persisted to browser storage. Status changes use a polite live region, emergency direction remains visible, and the view must reflow at 320 pixels with serious-or-critical automated WCAG checks.
10. Repeated reads must not mutate request, queue, event, authorization, appointment, applicant, or patient evidence and must perform no network call beyond the incoming API response.
11. No realtime transport is authorized. WebRTC, WebSocket, SignalR, vendor video, recording, transcription, FHIR, X12, pharmacy, payer, directory, or clearinghouse integration remains outside this slice.
12. Policy, access isolation, provenance denial, minimization, no-mutation, accessibility, recovery, runtime, OpenAPI, planning, Graphify, full regression, and GA/CA/FL live evidence are required without weakening Sprints 1–52.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Endpoint | GET `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/queue-status`. |
| Entry | Owning unexpired applicant key; applicant exactly `SyntheticRequestCreated` version 26; exact eligible request and Sprint 51 submission. |
| Visible states | Only `OperationalReview` version 12 and `Queued` version 13 or later while its single queue entry is `Ready`. |
| Approximation | Count only same-practice/facility `Ready` requests ordered ahead. Never an assigned position, priority, or wait promise. |
| Transport | Authoritative HTTP polling every five seconds by default; no realtime delivery. |
| Output | Request identity/version/status, plain-language phase, approximation, timestamps, refresh interval, safety direction, limitations, and explicit consequence flags. |
| Mutation | None. |
| Outstanding gates | Clinician reservation/assignment and identity; connection/video; consultation; consent; encounter; care; real coverage and financial route; prescribing; claims; integrations; completion; cancellation. |

## 5. Explicit exclusions

This decision does not authorize clinician reservation or assignment; clinician identity; clinical prioritization; exact queue position; wait-time estimate or availability guarantee; patient contact or messaging; video, WebRTC, recording, or transcription; real authority, credentialing, provider participation, coverage, payment, or price; encounter, consent, diagnosis, treatment, prescribing, billing, or claims; FHIR, X12, pharmacy, payer, directory, clearinghouse, or other connectivity; completion or cancellation; real people or PHI; or production enablement.

## 6. Stop conditions and rollback

Stop if another applicant, practice, facility, staff session, or portal session can read the status; if protected patient, member, payer, provider, clinician, or clinical data is disclosed; if an exact position, wait time, clinical priority, availability, coverage, payment, or treatment is implied; if reading mutates evidence or contacts an external system; if a state beyond `OperationalReview` or `Queued` is exposed; or if polling causes focus loss, inaccessible updates, unbounded retries, sensitive persistence, or hidden-page traffic. Rollback removes the route and applicant status component without altering durable queue evidence.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic applicant-owned status boundary above.

## References

- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Practice configuration and queue operations](../07-practice-configuration-and-queue-operations.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0055](0055-approved-sprint-52-applicant-request-queue-authorization.md)
- [Medical Board of California telehealth guidance](https://www.mbc.ca.gov/Resources/Medical-Resources/telehealth.aspx)
- [Georgia Composite Medical Board Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3)
- [Florida Statutes section 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499/0456/Sections/0456.47.html)
- [Sprint 53 plan](../backlog/sprint-53-applicant-request-queue-status.md)
