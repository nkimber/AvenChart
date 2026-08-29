# Sprint 53 plan: applicant request queue status

Status: Implemented under [TH-DEC-0056](../decisions/0056-approved-sprint-53-applicant-request-queue-status.md); automated verification in progress, independent approvals remain open

## Goal

Let the owning new-patient applicant see an honest, private, continuously refreshed status after submitting for practice review and after the practice accepts the request into the synthetic queue. Expose only `OperationalReview` and `Queued`, use approximate requests-ahead rather than an assigned position, promise no wait time, and create no clinical or external consequence.

## Delivery boundary

- Add applicant-key-private GET `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/queue-status` with no mutation or idempotency header.
- Rebind the exact applicant, portal-disabled patient shell, request, passing triage, Sprint 51 submission, and, while queued, Sprint 52 authorization, appointment, and one `Ready` queue entry.
- Return only the server-owned `Reviewing` or `InQueue` projection, approximate same-practice/facility requests-ahead, timestamps, refresh direction, safety actions, limitations, and explicit false downstream flags.
- Reject any reserved, connecting, consultation, wrap-up, completed, cancelled, redirected, duplicate, drifted, expired, or foreign state. Clinician assignment remains a later gate.
- Add visible-page HTTP polling with abortable requests, server-directed interval, hidden-page pause, last-known-state recovery, manual keyboard retry, no focus stealing, polite live updates, and no queue-status browser persistence.
- Extend backend/UI tests, authorization, OpenAPI, runtime safety, live GA/CA/FL proof, four-engine browser/accessibility evidence, planning validation, and Graphify review.

## State and evidence contract

```text
SyntheticRequestCreated v26 (unchanged)
  + request OperationalReview v12
  + exact Sprint 51 submission
  -> applicant sees Reviewing

SyntheticRequestCreated v26 (unchanged)
  + request Queued v13+
  + exact Sprint 52 authorization
  + one unassigned appointment
  + one Ready queue entry
  -> applicant sees InQueue
  -> optional approximate requests-ahead snapshot
```

Every read leaves the database unchanged. `exactQueuePositionAssigned=false`, `waitEstimateAvailable=false`, `realtimeAvailable=false`, `renderingPhysicianAssigned=false`, `renderingPhysicianIdentityDisclosed=false`, `coverageVerified=false`, `consentCreated=false`, `careAuthorized=false`, `integrationEnabled=false`, and `externalCallPerformed=false`.

## Acceptance matrix

| Area | Required evidence |
|---|---|
| Policy | Only operational review and ready-queue states; exact policy/version; server-owned language; bounded five-second refresh. |
| Access | Missing, foreign, staff, portal, expired, wrong-host, and cross-practice/facility access fail closed. |
| Provenance | Exact applicant/request/patient-shell/submission and conditional authorization/appointment/queue chain; duplicates and drift fail closed. |
| Approximation | Nonnegative same-practice/facility requests-ahead only for queued requests; no exact position, priority, or wait promise. |
| Consequences | No mutation, assignment, identity disclosure, coverage, consent, encounter, care, prescribing, claim, integration, or external call. |
| UI/accessibility | Poll while visible, pause while hidden, abort stale work, preserve last status, stable manual retry/focus, polite live updates, 320-pixel reflow, automated serious-or-critical WCAG check, no status persistence. |
| Regression | Backend, frontend, browser, route/accessibility, migrations/recovery, runtime, authorization, OpenAPI, queue, planning, Graphify, bootstrap, and cleanup. |

## Gate preserved

Sprint 54 must separately authorize clinician reservation/assignment for applicant-originated requests before this projection can expose any physician-preparing or later lifecycle state. Connection/video, consultation, consent, encounter, care, real coverage and financial routing, prescribing, claims, integrations, completion, cancellation, independent review, and production remain open.
