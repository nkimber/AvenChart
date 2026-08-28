# Decision 0021: Sprint 18 protected synthetic prospective member-insurance details

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of an unexpired synthetic prospective applicant in `PracticeNetworkPrecheckRecorded` to confirm one minimum structured member-insurance detail set for the already selected synthetic plan. The command advances the prospective aggregate to `MemberInsuranceDetailsRecorded` and records one immutable receipt whose raw member, group, and subscriber values are stored only inside an opaque ASP.NET Core Data Protection payload.

This is protected demonstration-data capture only. It is not identity proofing, member matching, a canonical insurance or coverage record, eligibility or benefits verification, exact practice/rendering-physician network confirmation, an X12 270/271 transaction, a payer response, price/estimate, payment, practice acceptance, request/queue creation, or care authorization.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, branded-host/practice/facility scoped, applicant-access-key protected, private/no-store, and unavailable from every state except `PracticeNetworkPrecheckRecorded`.
2. The command rebinds the current applicant, version, no-candidate staff approval, passing universal safety evaluation, controlled purpose, and immutable practice-plan precheck. The client cannot select or replace the practice, facility, payer, product, plan result, provenance, or consequence flags.
3. Member and optional group identifiers must be explicit `SYN-`-prefixed demonstration values. Coverage priority is server-fixed to `Primary`. Relationship is exactly `Self`, `Spouse`, `Parent`, or `Other`; self-subscriber name/date of birth are rebound from the applicant and must be omitted, while non-self name/date of birth are conditionally required and adult/valid-date bounded.
4. The client must affirm both synthetic-data use and review of the entered details. Card images/OCR, SSN/government identifiers, policy documents, free text, payer routing identifiers, financial data, and live-person data are rejected or absent.
5. Raw member/group/subscriber content is serialized once and protected with a versioned, purpose-isolated ASP.NET Core Data Protection protector before repository persistence. It is never placed in ordinary logs, events, fingerprints, metrics, URLs, audit descriptions, response bodies, or browser persistence. A payload that cannot be unprotected fails closed.
6. Public results contain only applicant/receipt/version/state, the selected synthetic plan's display labels, member/group masks, relationship, protected-payload scheme/version, confirmation time, fixed next action, and explicit limitations. Subscriber names/date of birth, ciphertext, applicant access credentials, fingerprints, safety answers, staff actors/reasons, and canonical identifiers remain private.
7. Exactly one immutable detail receipt and one applicant event are appended. Exact retry converges by unprotecting and fixed-time comparing the normalized payload; changed-content key reuse, stale writers, second submissions, and concurrent first writers fail closed with one winner.
8. Every member-eligibility, benefits, rendering-physician-network, exact-network, identity-proofing, patient/chart/portal, complete-intake, consent, practice-acceptance, coverage-record, estimate/payment, request, queue, appointment, encounter, care, prescribing, billing, claim, communication, integration, and external-call capability remains false.
9. Future eligibility must use a separately approved, standards-oriented X12 270/271 adapter and separately governed durable protection/key recovery. Future exact network verification remains an independent exact entity/physician/product/service/date check. This receipt cannot satisfy either gate.
10. Unit, API, access-key, payload-protection, live PostgreSQL replay/contention/append-only/no-delta, public minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–17.

## 3. Explicit exclusions

This decision does not authorize real insurance or subscriber data, card upload/OCR, government identifiers, canonical `insurance_records` creation, member matching, eligibility/benefits verification, X12 serialization or payer contact, public-directory inference, exact network confirmation, cost estimation, self-pay election, financial acknowledgment, identity proofing, patient promotion/linkage, consent, practice acceptance, request/queue creation, care, prescribing, billing, claims, external integration, production enablement, real people, or real PHI.

## 4. Stop conditions and rollback

Stop if an applicant outside `PracticeNetworkPrecheckRecorded` can submit; a non-`SYN-` identifier is accepted; self/non-self conditional identity is bypassed; raw or protected payload content appears in a response, ordinary log, event, fingerprint, metric, URL, or browser persistence; an unprotectable payload is treated as valid; a second receipt overwrites history; any eligibility/network/coverage/financial/patient/request/queue/downstream row or external action is created; or an earlier safeguard regresses. Rollback disables/removes the route and form; additive append-only evidence remains inert and requires a separately reviewed forward migration for correction.

## 5. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic protected-detail receipt. It does not substitute for payer/trading-partner, identity, legal, privacy/security, accessibility, data, operational, interoperability, licensed-clinical, or production review.

## References

- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0020](0020-approved-sprint-17-prospective-practice-network-precheck.md)
- [Sprint 18 plan](../backlog/sprint-18-prospective-member-insurance-details.md)
