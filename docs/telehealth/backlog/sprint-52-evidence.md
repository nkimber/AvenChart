# Sprint 52 evidence: applicant request queue authorization

Status: Automated verification passed; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0055](../decisions/0055-approved-sprint-52-applicant-request-queue-authorization.md)

Plan: [Sprint 52 applicant request queue authorization](sprint-52-applicant-request-queue-authorization.md)

## Implemented boundary

- Staff-private GET/POST applicant-request queue-authorization route with a minimized, no-edit, evidence-bound packet.
- Exact applicant, portal-disabled patient shell, request, Sprints 40–51 evidence chain, current candidate staff record, and operational-review submission rebound under transaction locks.
- Four acknowledgments append one immutable authorization and event, create one unassigned appointment and one ready queue entry, and advance only the request from `OperationalReview` version 12 to `Queued` version 13.
- Applicant-origin discriminators appear in the staff operational-review and clinician queue projections; the generic established-patient authorization path rejects applicant-originated requests.
- Only bounded synthetic practice acceptance, patient/clinician queue admission, doctor-search state, and appointment creation become true. Real authority, credentialing, clinician assignment, exact real network, canonical coverage, financial routing, patient queue status, position, encounter, consent, care, prescribing, claims, integration, and external action remain false.

## Automated evidence

- Backend formatting and release compilation passed with zero warnings and zero errors; the complete backend suite passed `743/743` tests, including `13/13` applicant-request queue-authorization policy tests.
- The production UI passed lint, TypeScript compilation, the `246,399/256,000`-byte initial-bundle budget, the 137-JavaScript-chunk ceiling, and `312/312` Vitest tests; the focused administrator and telehealth transport suites passed `62/62` tests.
- The reference frontend passed lint and production build. Its existing large-source and large-chunk informational warnings remain outside this slice.
- The required Playwright telehealth matrix passed `80/80` tests across desktop Chromium, mobile Chromium, Firefox, and WebKit. The queue-authorization journey passed in all four projects with keyboard operation, reflow, stable retry identity, focus recovery, no sensitive browser persistence, and serious-or-critical automated WCAG checks.
- The required route-smoke matrix passed all 15 applicable tests with nine intentional project skips, and the representative accessibility matrix passed `10/10` tests. Existing duplicate-key console warnings in unrelated demo navigation remain outside this slice and did not produce test failures.
- A clean isolated PostgreSQL rehearsal verified all `283` packaged migrations, including empty-schema bootstrap, populated-schema replay, interruptions after migrations 1, 64, and 127, recovery, checksum drift rejection, and unexpected-ledger rejection.
- Telehealth readiness reported `71/71` required tables and `283/283` migrations, with `V0327__telehealth_applicant_request_queue_authorization` as the latest expected migration.
- Telehealth migration resilience passed `157/157` checks; runtime safety passed `53/53`; authorization passed `148/148`; and the OpenAPI contract passed `80/80`.
- The live applicant queue-authorization proof passed `8/8` checks across GA, CA, and FL. It proved minimized no-edit projection, administrator/facility isolation, exact immutable provenance, generic-route denial, missing/foreign/stale/roster-drift failure, first-writer contention, exact replay, changed-key rejection, append-only evidence and events, atomic appointment/queue/request creation, honest consequence flags, and applicant-origin projections.
- The clean-state telehealth concurrency and lifecycle proof passed `134/134` checks with exactly one winner from 20 concurrent reservation callers.
- Planning validator v3.19.0 passed `97/97` checks across 186 Markdown files, 638 relative links, and 57 safeguards. All three controlled negative mutations were rejected.

## Open gates

Synthetic queue admission is not clinician assignment, encounter, consent, or authorization for care. Real authority, credentialing, rendering-provider participation, canonical coverage and financial routing, applicant queue-status access, prescribing, claims, integrations, independent review, and every production gate remain open.
