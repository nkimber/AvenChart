# Sprint 56 evidence: applicant clinician connection and consultation start

Status: Implemented and automated verification complete; independent clinical, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0059](../decisions/0059-approved-sprint-56-applicant-consultation-start.md)

Plan: [Sprint 56 applicant clinician connection and consultation start](sprint-56-applicant-consultation-start.md)

## Implemented boundary

- The existing physician connection endpoint now has live applicant-path evidence proving the exact reservation owner receives the separate physician-role grant for the same capture-disabled synthetic waiting room.
- Consultation start keeps the established-patient coverage gate and adds a fail-closed applicant alternative requiring the unexpired exact queue authorization, applicant/patient/request/practice/facility binding, reservation-owning candidate, and every synthetic/no-real-coverage/no-care/no-downstream flag.
- The locked start query now also rebinds appointment patient/facility/provider ownership and active, unmerged, living adult patient state for both patient paths.
- The existing exact version/idempotency/location/affirmative-checklist/two-current-grant transaction creates one synthetic consultation context and encounter while atomically closing the queue, reservation, appointment, shift, session, and grant states.
- New-patient start limitations explicitly state that the financial evidence is synthetic and is neither real coverage verification nor a payment guarantee.
- The physician receives only the existing bounded chart/visit projection and explicit unsigned SOAP draft. Applicant status advances without protected identifiers or real coverage/consent claims.

## Automated evidence

- Live GA/CA/FL applicant proof: every inherited applicant gate passed, exact queue authorization passed 9 checks, clinician reservation passed 9 checks, connection-room preparation passed 7 checks, and consultation start passed 7 checks. Twenty concurrent start commands produced one winner and 19 opaque bounded failures; replay returned the same consultation; one transaction created the sole synthetic encounter and closed the queue, reservation, session, and grants without changing prescriptions, billing, claims, or integration outbox state.
- Applicant status and physician workspace: the exact owner received only the bounded chart/visit projection and empty unsigned SOAP draft, while applicant polling advanced to a minimized consultation phase without physician identity, chart facts, credentials, encounter identifiers, insurance identifiers, real coverage, or legal-consent claims.
- Established-patient regression: 134 queue/concurrency/lifecycle checks passed on a clean isolated database, including both 20-caller single-winner boundaries and the existing downstream synthetic draft lifecycle.
- API boundaries: 152 authorization checks, 85 OpenAPI checks, and 55 runtime-safety checks passed. The live database contained all 71 required telehealth tables.
- Migration/recovery: all 29 migration-resilience scenarios passed across the 283-migration catalog, including empty and populated migration, interruption recovery, idempotent replay, checksum drift rejection, and unexpected-ledger rejection. Operational readiness passed all 6 service, database, dataset, ledger, and recovery checks after a clean synthetic reseed.
- Backend: 758 tests passed in Release configuration with zero failures.
- Primary UI: 321 tests across 54 files passed; lint and production build passed; the bundle gate accepted the 246,436-byte initial bundle against the 256,000-byte limit and checked 138 JavaScript chunks.
- Browser/accessibility: the complete 72-case telehealth matrix exercised desktop/mobile Chromium, Firefox, and WebKit. Seventy-one passed in the parallel run; one unrelated promoted-applicant retry timing miss passed immediately when rerun serially. The physician workspace case, including the applicant-specific synthetic financial warning, passed on every project. The route-smoke gate passed 15 applicable cases with 9 intentional project skips, and the general accessibility gate passed all 10 desktop/mobile Chromium cases.
- Reference frontend: lint and production build passed.
- Planning: validator v3.23.0 and all three controlled negative mutations passed; exact document, link, safeguard, and check counts are recorded in the planning validation report.
- Graph: the committed-code index must be refreshed, portability-checked, and delta-reviewed after the feature commit; that evidence is committed separately under the repository Graphify policy.

## Open gates

Real media/communication, legal consent, real coverage and financial clearance, diagnosis/treatment, signing, prescribing, claims, integrations, applicant wrap-up/downstream planning, patient delivery, completion, cancellation, independent review, and every production gate remain open.
