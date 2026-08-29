# Sprint 53 evidence: applicant request queue status

Status: Automated verification passed; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0056](../decisions/0056-approved-sprint-53-applicant-request-queue-status.md)

Plan: [Sprint 53 applicant request queue status](sprint-53-applicant-request-queue-status.md)

## Implemented boundary

- Applicant-access-key-private GET projection for exact `OperationalReview` and `Queued` applicant-originated requests.
- Exact applicant, portal-disabled patient shell, eligible request, Sprint 51 submission, and conditional Sprint 52 authorization/appointment/queue provenance checks.
- Approximate same-practice/facility requests-ahead only for one `Ready` queue entry; exact position, priority, wait promise, realtime delivery, clinician assignment/identity, coverage, consent, care, integration, and external action remain false.
- Visible-page authoritative HTTP polling with hidden-page pause, abortable work, last-confirmed-state recovery, keyboard retry, polite live updates, emergency guidance, no focus theft, and no queue-status persistence.

## Automated evidence

- Backend formatting and release compilation passed with zero warnings and zero errors; the complete backend suite passed `753/753` tests, including `10/10` applicant-request queue-status policy tests.
- The production UI passed lint, TypeScript compilation, the `246,436/256,000`-byte initial-bundle budget, the 138-JavaScript-chunk ceiling, and `317/317` Vitest tests. The queue-status component and transport suites cover first load, hidden-page pause, request cancellation, last-confirmed-state recovery, credential isolation, and manual retry.
- The reference frontend passed lint and production build. Its existing large-source and large-chunk informational warnings remain outside this slice.
- The required Playwright telehealth matrix passed `84/84` tests across desktop Chromium, mobile Chromium, Firefox, and WebKit. The applicant status journey passed with keyboard activation and retained focus, reflow at 320 CSS pixels, serious-or-critical automated WCAG checks, deterministic failure recovery, no exact position or wait promise, and no request/status persistence.
- Existing applicant browser fixtures were extended with the queue-status `409` boundary so earlier Draft and prerequisite journeys continue to prove that queue semantics remain absent. The representative authorization accessibility fixture now derives valid dates from the run date rather than a date that could expire.
- The required route-smoke matrix passed all 15 applicable tests with nine intentional project skips, and the representative accessibility matrix passed `10/10` tests.
- A clean isolated PostgreSQL rehearsal verified all `283` packaged migrations, including empty-schema bootstrap, populated-schema replay, interruptions after migrations 1, 64, and 127, recovery, checksum drift rejection, and unexpected-ledger rejection.
- Telehealth readiness reported `71/71` required tables and `283/283` migrations, with `V0327__telehealth_applicant_request_queue_authorization` as the latest expected migration. Sprint 53 is a read-only projection and adds no schema migration.
- Telehealth migration resilience passed `157/157` checks; runtime safety passed `54/54`; authorization passed `151/151`; and the OpenAPI contract passed `82/82`.
- The clean applicant queue-status proof passed `4/4` checks after the inherited queue-authorization proof passed `9/9`. It proved private preauthorization review state; GA, CA, and FL queued state with approximate requests-ahead values 0, 1, and 2; credential substitution denial; an exact minimized response; and repeated-read database fingerprints with no mutation.
- The clean-state telehealth concurrency and lifecycle proof passed `134/134` checks with exactly one winner from 20 concurrent reservation callers.
- Planning validator v3.20.0 passed `98/98` checks across 189 Markdown files, 644 relative links, and 58 safeguards. All three controlled negative mutations were rejected.

## Open gates

Clinician reservation/assignment and identity, connection/video, consultation, consent, encounter, care, real coverage and financial routing, prescribing, claims, integrations, completion, cancellation, independent review, and every production gate remain open.
