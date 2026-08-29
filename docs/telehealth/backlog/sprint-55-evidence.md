# Sprint 55 evidence: applicant request connection room

Status: Implemented and automated verification complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0058](../decisions/0058-approved-sprint-55-applicant-request-connection-room.md)

Plan: [Sprint 55 applicant request connection room](sprint-55-applicant-request-connection-room.md)

## Implemented boundary

- An applicant-key-only POST route prepares the existing synthetic connection room for the exact source-linked request. It accepts only an exact request version, idempotency key, and coarse browser/camera/microphone/speaker/network capability evidence.
- The server derives a domain-separated participant subject hash from the applicant identifier and stored access-key hash. The transaction rebinds the unexpired applicant, portal-disabled patient shell, request, current queue authorization, exact candidate-owned active reservation and shift, `Reserved` queue entry, appointment, practice, and facility.
- The existing transaction creates one `NON_PRODUCTION` `WaitingRoom` session with media transport, recording, and transcription false; one preflight record; one short-lived patient-role grant with only its credential hash persisted; the `Reserved -> Connecting` request transition; the appointment `Arrived` transition; and append-only request/video events.
- The browser runs the device check only after an applicant action, stops temporary media tracks in `finally`, sends no device IDs/names/labels, browser fingerprint, IP address, or media, and neither renders nor persists the returned credential.
- Applicant status accepts `Connecting` only after exact current session/grant/reservation/appointment/event provenance. It exposes `ConnectionRoom`, waiting-room true, and media/communication false without identifiers or credentials.
- No signaling, media, communication, consultation, chart access, consent, encounter, diagnosis, treatment, prescription, claim, integration, message, or external action is created by this slice.

## Automated evidence

- Live GA/CA/FL applicant proof: all inherited applicant gates passed, queue authorization passed 9 checks, exact clinician reservation passed 9 checks, and connection-room preparation passed 7 checks. Twenty concurrent unchanged commands converged on one session/grant result; changed replay conflicted; the plaintext credential was absent from persisted evidence; and no consultation or encounter was created.
- Established-patient regression: 134 queue/concurrency/lifecycle checks passed on a separate clean database, including the 20-caller single-winner reservation boundary.
- API boundaries: 152 authorization checks, 85 OpenAPI checks, and 55 runtime-safety checks passed. Runtime readiness found all 71 required telehealth tables.
- Migration/recovery: all 29 migration-resilience scenarios passed across the 283-migration catalog, including interruption recovery, idempotent replay, checksum drift rejection, and unexpected-ledger rejection.
- Backend: 757 tests passed; Release build and formatting verification completed with zero errors.
- Primary UI: 321 tests across 54 files passed; lint and production build passed; the bundle gate accepted the 246,436-byte initial bundle against the 256,000-byte limit and checked 138 JavaScript chunks.
- Browser/accessibility: the complete 88-case telehealth Playwright matrix covered desktop/mobile Chromium, Firefox, and WebKit. Eight-worker execution passed 87 cases; its one unrelated established-patient readiness timing miss passed immediately when rerun serially. The new applicant test passed on every project and proved track shutdown, unchanged retry, credential non-retention, WCAG behavior, and 320-pixel reflow. The dedicated route-smoke gate passed 15 applicable cases with 9 intentional project skips. The general accessibility gate passed all 10 desktop/mobile Chromium cases.
- Cross-platform regression found during the accessibility gate: authorization date-only/date-time inputs are now parsed invariantly and normalized to UTC before PostgreSQL `timestamptz` writes; the backend suite and both affected accessibility cases passed after the correction.
- Reference frontend: lint and production build passed.
- Planning: validator v3.22.0 passed 100 checks across 195 Markdown files, 671 relative links, and 60 active safeguards. All three controlled negative mutations were rejected.
- Graph: the committed-code index must be refreshed, portability-checked, and delta-reviewed after the feature commit; that evidence is committed separately under the repository Graphify policy.

## Open gates

Media/signaling, clinician connection, communication, consultation, chart workspace for the applicant path, clinician-obtained consent, encounter, care, real coverage and financial routing, prescribing, claims, integrations, completion, cancellation, independent review, and every production gate remain open.
