# Decision 0020: Sprint 17 synthetic prospective practice-network precheck

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of an unexpired synthetic prospective applicant in `VisitPurposeRecorded` to view a versioned non-production plan catalog and record one practice-level network precheck. The precheck selects exactly one server-defined synthetic payer/product fixture, snapshots its deterministic practice-level status, and advances the prospective aggregate to `PracticeNetworkPrecheckRecorded`.

This is plan discovery only. It is not member eligibility, benefits verification, exact network confirmation, rendering-physician participation, a payer or directory response, coverage creation, a price estimate, financial consent, practice acceptance, a request for care, or a promise of coverage or payment. Because no physician is assigned and no subscriber/member identifiers are collected, even a practice-level positive fixture keeps `individualEligibilityChecked`, `renderingPhysicianNetworkChecked`, `coverageVerified`, and `exactNetworkConfirmed` false.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, branded-host/practice/facility scoped, applicant-access-key protected, private/no-store, and unavailable from every state except `VisitPurposeRecorded`.
2. The options projection and command rebind the current applicant, version, no-candidate staff approval, passing universal safety evaluation, and existing purpose provenance. No client-selected practice, facility, payer label, product label, network result, effective date, evidence source, or consequence flag is trusted.
3. The versioned catalog accepts only an opaque server-owned `planKey`. It accepts no member/subscriber name, member ID, group ID, policy number, card image, payer response, free text, physician, price, payment, or coverage outcome.
4. Catalog fixtures cover `PracticeNetworkConfirmedFixture`, `NetworkUnknown`, and `PracticeOutOfNetworkFixture` outcomes for demonstration. All are visibly watermarked `NON_PRODUCTION`; none is represented as individual coverage, exact network, a guarantee, or an X12 271 response.
5. Exactly one immutable precheck and one applicant event are appended. Exact retry converges; changed-content key reuse, stale writers, second submissions, and concurrent first writers fail closed with one winner.
6. Public responses expose only the controlled plan display data, coarse practice-level fixture status, source version/effective window, fixed next action, and explicit limitations. They expose no applicant access credential, raw safety answers, review actor/reason, fingerprints, candidate, canonical patient, member identifier, or transaction payload.
7. Every member-eligibility, benefits, rendering-physician-network, exact-network, identity-proofing, patient/chart/portal, complete-intake, consent, coverage-record, estimate/payment, request, queue, appointment, encounter, care, prescribing, billing, claim, communication, integration, and external-call capability remains false.
8. Future individual eligibility must use a separately approved standards-oriented adapter modeling HIPAA-adopted X12 270/271 semantics; future exact network verification must independently bind payer product/network, billing entity, rendering physician, state/location, service/modality, and date. This precheck cannot satisfy either gate.
9. Unit, API, access-key, live PostgreSQL replay/contention/append-only/no-delta, public minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–16.

## 3. Explicit exclusions

This decision does not authorize subscriber/member data capture, insurance card upload/OCR, member matching, eligibility/benefits verification, X12 serialization or payer contact, public-directory inference, exact practice-and-physician network confirmation, cost estimation, self-pay election, financial acknowledgment, identity proofing, patient promotion/linkage, clinical triage, consent, practice acceptance, request/queue creation, care, prescribing, billing, claims, external integration, production enablement, real people, or real PHI.

## 4. Stop conditions and rollback

Stop if an applicant outside `VisitPurposeRecorded` can use the projection/command; arbitrary payer/product/result content is accepted; any output implies individual eligibility, exact network, coverage, or payment; a second precheck overwrites history; any downstream row/external action is created; plan selection enters browser persistence or ordinary logs; or an earlier safeguard regresses. Rollback disables/removes the routes and form; additive append-only evidence remains inert and requires a separately reviewed forward migration for correction.

## 5. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic plan-discovery precheck. It does not substitute for payer/trading-partner, identity, legal, privacy/security, accessibility, data, operational, interoperability, licensed-clinical, or production review.

## References

- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [CMS adopted standards and eligibility references](../references.md#administrative-transactions-claims-prescribing-and-location-coding)
- [Decision 0019](0019-approved-sprint-16-prospective-visit-purpose.md)
- [Sprint 17 plan](../backlog/sprint-17-prospective-practice-network-precheck.md)
