# Sprint 58 evidence: synthetic prescription safety gate and signing

Status: Implemented and automated verification complete; independent clinical, pharmacy/e-prescribing, legal, privacy, security, accessibility, interoperability, operational, and production approvals remain open

Decision: [TH-DEC-0061](../decisions/0061-approved-sprint-58-synthetic-prescription-signing.md)

Plan: [Sprint 58 synthetic prescription safety gate and signing](sprint-58-synthetic-prescription-signing.md)

## Implemented boundary

- Exact-owner prescription signing now requires the current draft, unchanged patient-confirmed pharmacy choice, four explicit physician attestations, and zero active canonical medications and allergies in one serializable transaction.
- Success atomically creates one canonical prescription row, immutable telehealth order/signature evidence with a content hash, and one prescription audit event.
- The synthetic adapter records a prepared-only, uncertified `NewRx` target for NCPDP SCRIPT 2023011 and a bounded 2017071 transition label through 2027-12-31.
- No pharmacy, network, drug-knowledge vendor, payer, or external destination is contacted. Legal effect, certification, transmission, acknowledgment, patient delivery, lifecycle completion, billing, and claims remain false or unchanged.

## Automated evidence

- Live new-patient proof: the complete inherited branded-site applicant workflow passed from synthetic promotion through clinical intake, operational review, queue authorization, exact clinician reservation, connection preparation, consultation start, and unfinished wrap-up. The new signing proof then passed all 5 checks for exact-owner authorization, stable replay, the zero-medication/zero-allergy safety gate, atomic canonical/audit/order persistence, prepared-only SCRIPT 2023011 metadata, closed downstream capabilities, and database-enforced immutability.
- Established-patient regression: all 134 queue/concurrency/lifecycle checks passed, including cleanup and repeatability restoration.
- API boundaries: all 154 authorization checks, 86 OpenAPI checks, and 57 runtime-safety checks passed with Decision 0061 included. Runtime readiness found all 71 required telehealth tables, and telehealth remains disabled by default.
- Migration/recovery: all 29 migration-resilience scenarios passed across the 284-migration catalog, including interruption recovery, replay, checksum drift, and unexpected-ledger rejection. The telehealth schema proof passed 161 checks, including the signed-order constraints, append-only order evidence, and canonical-prescription mutation trigger.
- Backend: all 765 tests passed with zero failures.
- Primary UI: all 323 tests across 54 files passed; lint and production build passed; the bundle gate accepted the 246,436-byte initial bundle against the 256,000-byte limit and checked 138 JavaScript chunks.
- Browser/accessibility: all 123 applicable cross-browser route, accessibility, reflow, keyboard, and failure-recovery cases passed across desktop/mobile Chromium, Firefox, and WebKit; 9 project-specific route cases remained intentionally skipped. The physician prescribing workspace passed its focused four-browser rerun after preserving the explicit no-interaction/contraindication-service warning.
- Reference frontend: lint and production build passed.
- Operational readiness: all 6 service, database, dataset, ledger, and recovery checks passed.
- Planning: validator 3.4.0 passed all 82 checks with Decision 0061, Sprint 58, backlog, and safeguard artifacts included.
- Graph: the committed-code index must be refreshed, portability-checked, and delta-reviewed after the feature commit; that evidence is committed separately under the repository Graphify policy.

## Open gates

Approved drug knowledge and interaction/contraindication adjudication, alert override governance, production prescriber authority, controlled substances/EPCS, certified NCPDP mapping and vendor connectivity, transport/business recovery, correction/cancel/change/renewal flows, pharmacy acknowledgment, patient delivery/AVS, final documentation signature, completion/release, billing, claims, independent review, and every production gate remain open.
