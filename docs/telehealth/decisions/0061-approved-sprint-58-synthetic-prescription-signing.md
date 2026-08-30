# Decision 0061: Sprint 58 synthetic prescription safety gate and signing

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the exact physician who owns an unfinished synthetic telehealth consultation to run one conservative medication-safety gate and atomically create one immutable signed synthetic prescription from the current non-controlled preparation draft and current patient-confirmed pharmacy choice.

The same transaction may create an uncertified, prepared-only `NewRx` integration record targeting NCPDP SCRIPT 2023011 and recording SCRIPT 2017071 as a transition mapping only through December 31, 2027. It must not contact a pharmacy, network, vendor, payer, or other external destination. The synthetic signature and prescription have no legal or patient-care effect.

## 2. Safety and ownership rule

The action must rebind the configured practice and facility, exact consultation-owning physician, active adult patient, released reservation, ended session, unfinished appointment and encounter, `MediaEnded` consultation, `WrapUp` request, `WrapUp` shift, unsigned encounter, current draft version, and unchanged current pharmacy-choice version inside one serializable transaction.

Because no approved drug-knowledge service is integrated, the synthetic gate fails closed unless the canonical chart contains zero active medications and zero active allergies and the physician explicitly reconfirms both empty lists, adequate evaluation, and synthetic-only effect. Any active or unconfirmed item requires clinician resolution and creates no prescription.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, non-controlled, and scoped to the configured branded practice, facility, GA/CA/FL, and adult patient shell.
2. Only the exact owning physician may sign; another physician, administrator, missing staff binding, wrong facility, stale consultation, or locked encounter receives fail-closed behavior.
3. The exact current draft and pharmacy-choice versions are required. A changed destination or stale draft requires a revised preparation draft.
4. All four physician attestations are affirmative and server validated. Missing, unknown, active, or unconfirmed medication/allergy information is not normalized to none.
5. The transaction creates one canonical `prescriptions` row, one telehealth order/signature evidence row, and one prescription audit event, with semantic idempotency and conflict detection.
6. The signed prescription content and telehealth evidence are immutable. Retry returns the exact result; another prescription for the consultation is rejected.
7. The prepared integration seam records canonical model version, `NewRx`, target SCRIPT 2023011, the bounded 2017071 transition label, safety ruleset, content hash, and separate transport state.
8. `PreparedOnly` is neither queued nor sent. Certification, external destination contact, pharmacy acknowledgment, dispense, pickup, payment, patient delivery, and legal effect remain false.
9. Request, consultation, shift, appointment, encounter, clinician availability, patient status, billing, claims, messages, AVS, outbox/inbox, and completion remain unchanged.
10. Unit, authorization, OpenAPI, runtime, migration, live PostgreSQL, replay, immutability, browser/accessibility, planning, and Graphify evidence are required.

## 4. Standards decision

CMS permits either NCPDP SCRIPT 2017071 or 2023011 during the transition that ends December 31, 2027 and requires exclusive SCRIPT 2023011 for Part D e-prescribing beginning January 1, 2028. AvenChart therefore targets 2023011 for new adapter work and records 2017071 only as an explicit transition mapping. This decision does not claim certification or implement the proprietary standard message.

## 5. Explicit exclusions

This decision does not authorize a production prescription, certified drug-knowledge adjudication, alert override, controlled substance, EPCS, pharmacy or network connection, standards certification, NCPDP message transmission, acknowledgment, cancel/change/renewal, formulary or real-time benefit request, patient delivery, AVS, dispense/pickup/payment claim, visit completion, clinician release, billing, professional claim, external action, real people or PHI, or production enablement.

## 6. Stop conditions and rollback

Stop if a non-owner can sign; if a non-empty or unconfirmed medication/allergy list passes; if a stale draft or pharmacy version is accepted; if duplicate or mutable signed content is possible; if any external destination is contacted; if `PreparedOnly` is represented as sent; or if lifecycle, patient delivery, financial, integration, or production state changes. Rollback removes the signing endpoint, order table, immutable-prescription trigger, and UI action while preserving prior unsigned preparation drafts.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic safety-gated prescription-signing boundary above.

## References

- [Prescribing and pharmacy](../11-prescribing-and-pharmacy.md)
- [CMS e-prescribing standards](https://www.cms.gov/medicare/regulations-guidance/electronic-prescribing)
- [Decision 0060](0060-approved-sprint-57-applicant-wrap-up-planning.md)
- [Sprint 58 plan](../backlog/sprint-58-synthetic-prescription-signing.md)
