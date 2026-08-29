# Sprint 54 evidence: applicant request clinician reservation

Status: Automated verification passed; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0057](../decisions/0057-approved-sprint-54-applicant-request-clinician-reservation.md)

Plan: [Sprint 54 applicant request clinician reservation](sprint-54-applicant-request-clinician-reservation.md)

## Implemented boundary

- The existing clinician queue now returns an applicant-originated request only to the authenticated physician whose staff identifier exactly matches the current synthetic rendering candidate carried through the applicant participation and queue-authorization chain. Established-patient queue behavior is unchanged.
- The existing leased `reserve-next` transaction rebinds the applicant, portal-disabled patient shell, request, practice, facility, candidate, current Sprint 52 authorization, one `Ready` queue entry, and one unassigned same-patient/facility appointment. Database time must remain earlier than the authorization's `result_valid_through`.
- One transaction creates the generic active reservation, moves the queue and request to `Reserved`, increments the request version, assigns the appointment to the reservation owner, and appends the existing request event. Idempotent replay returns the same reservation.
- Lease expiry retains the expired reservation and event evidence, clears the appointment assignment, returns the request and queue to `Queued`/`Ready`, and requires the same exact current candidate before a later reservation can win.
- The applicant-owned status projection now permits `Reserved` as `PhysicianPreparing`. It confirms only that the exact synthetic candidate owns an active lease; physician identity, real network confirmation, exact position, wait estimate, coverage, consent, encounter, and care authority remain absent.
- The clinician UI labels applicant-originated work and the synthetic candidate match without exposing applicant credentials, member or policy identifiers, NPI, real credentialing, or a real-network guarantee.
- No connection grant, media session, consultation, chart workspace, consent, encounter, diagnosis, treatment, prescription, claim, integration, message, or external action is created by this slice.

## Automated evidence

- Backend formatting and release compilation passed with zero warnings and zero errors; the complete backend suite passed `754/754` tests.
- The production UI passed lint, TypeScript compilation, the `246,436/256,000`-byte initial-bundle budget, the 138-JavaScript-chunk ceiling, and `319/319` Vitest tests. An unchanged administrator focus test that missed its effect while six heavy jobs ran concurrently passed `12/12` in isolation and the subsequent sequential full suite passed cleanly.
- The reference frontend passed lint and production build. Its existing large-source and large-chunk informational warnings remain outside this slice.
- The complete Playwright telehealth matrix passed `84/84` tests across desktop Chromium, mobile Chromium, Firefox, and WebKit. It covers applicant-origin queue status, clinician applicant labels, deterministic recovery, keyboard operation, stable focus, 320-pixel reflow, serious-or-critical automated WCAG checks, and no sensitive browser persistence.
- The required route-smoke matrix passed all 15 applicable tests with nine intentional project skips. The representative accessibility matrix passed `10/10` tests against the same API build in a UTC Linux runtime, including dynamic authorization state on desktop and mobile Chromium.
- Fresh isolated PostgreSQL databases repeatedly applied all `283` packaged migrations from an empty schema. Telehealth readiness reported `71/71` required tables and `283/283` migrations, with `V0327__telehealth_applicant_request_queue_authorization` still the latest migration; Sprint 54 adds no schema migration.
- Telehealth migration resilience passed `157/157` checks; runtime safety passed `54/54`; authorization passed `151/151`; and the OpenAPI contract passed `83/83`, including explicit applicant-origin queue and reservation fields without protected-source or provider-credential payloads.
- The clean GA/CA/FL applicant clinician-reservation proof passed `9/9` checks after the inherited queue-authorization proof passed `9/9`. It proved exact-candidate-only visibility, unmatched-physician exclusion, one winner and 19 conflicts from 20 callers, atomic request/queue/appointment/event binding, fresh participation, no encounter, stable replay, minimized `PhysicianPreparing` status, and exact-candidate-only lease-expiry recovery.
- The clean established-patient telehealth concurrency and lifecycle proof passed `134/134` checks with exactly one winner from 20 callers, confirming that the shared reservation transaction retained its prior behavior.
- Planning validator v3.21.0 passed `99/99` checks across 192 Markdown files, 659 relative links, and 59 safeguards. All three controlled negative mutations were rejected.

## Open gates

Connection/video, consultation, chart workspace for this applicant path, clinician-obtained consent, encounter, care, real coverage and financial routing, prescribing, claims, integrations, completion, cancellation, independent review, and every production gate remain open.
