# Decision 0022: Sprint 19 synthetic prospective eligibility result

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of an unexpired synthetic prospective applicant in `MemberInsuranceDetailsRecorded` to request one deterministic, non-production member eligibility and benefit-information result for the current date of service. The server unprotects the existing receipt, binds the selected plan and applicant/subscriber facts, invokes one internal adapter shaped around ASC X12N 270/271 Version 5010 inquiry/response semantics, and records one immutable normalized result at `SyntheticEligibilityRecorded`.

This is adapter-contract evidence only. It is not an X12 interchange, payer or clearinghouse communication, canonical coverage, a guarantee of coverage or payment, exact practice/rendering-physician network confirmation, an estimate, patient responsibility, practice acceptance, identity proofing, patient promotion, consent, request/queue creation, or care authorization.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, branded-host/practice/facility scoped, applicant-access-key protected, private/no-store, and unavailable outside `MemberInsuranceDetailsRecorded`.
2. The command accepts only expected applicant version and explicit synthetic confirmation. The client cannot supply or replace payer, plan, member/subscriber, practice/provider, date of service, service category, trace, transport result, business result, benefits, freshness, or consequence flags.
3. The server rebinds the complete review, safety, purpose, practice-plan, and protected member-detail provenance. An unreadable protected payload, mismatched receipt, expired applicant, stale version, missing upstream row, or cross-applicant identifier fails closed.
4. The adapter is exactly `NON_PRODUCTION`, effective-dated, deterministic, and separately represents transport outcome, member-match status, eligibility status, benefit-information status, and response/business outcome. It advertises compatibility target `ASC_X12N_270_271_005010X279A1` but serializes no proprietary X12 transaction and performs no external call.
5. Result fixtures cover matched-active, matched-inactive, subscriber-not-found, and unknown/unavailable behavior. Active eligibility never implies exact network, service coverage, payment, price, or patient responsibility. Unknown or unavailable never silently passes.
6. Raw member/group/subscriber data, protected payload, subscriber identity, transaction text, X12 segments/elements, payer routing identifiers, and financial amounts never appear in the result row, event, idempotency fingerprint, ordinary logs, metrics, URLs, public response, browser persistence, or test artifact.
7. Public output contains only applicant/result/version/state, selected-plan display labels, member/group masks, synthetic adapter/standard/dataset metadata, opaque trace tokens, normalized non-financial statuses, freshness, fixed next action, and explicit limitations.
8. Exactly one immutable result and one event are appended. Exact replay converges; changed key reuse, stale/second commands, and concurrent first writers fail closed with one winner.
9. `memberEligibilityChecked` may become true and `memberMatched`, `memberBenefitsChecked`, and normalized statuses reflect only the deterministic fixture. Exact network, canonical coverage, financial, identity/patient, consent, practice acceptance, request/queue, clinical, prescribing, billing/claim, communication, integration, and external-call capabilities remain false.
10. Unit, adapter-contract, API, authorization, protected-payload failure, live PostgreSQL outcome/replay/contention/append-only/no-delta, public minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–18.

## 3. Standards baseline

The compatibility target follows the current CMS-adopted ASC X12N 270/271 Version 5010 eligibility/benefit inquiry and response baseline. CMS describes the inquiry and response as paired transactions and separately warns that eligibility information is not a reimbursement guarantee. This slice models only a normalized port and deterministic fixtures; implementation-guide content and real trading-partner behavior require licensed standards materials, payer/clearinghouse agreements, security review, and a separate decision.

## 4. Explicit exclusions

This decision does not authorize real insurance/PHI, X12 serialization, TA1/999 generation, payer or clearinghouse connectivity, trading-partner credentials, real member matching, canonical insurance/coverage creation, exact network confirmation, cost sharing or financial amounts, estimates/self-pay, identity proofing, patient promotion/linkage, consent, practice acceptance, request/queue creation, care, prescribing, billing, claims, external integration, production enablement, or real people.

## 5. Stop conditions and rollback

Stop if a client can influence authoritative inquiry/result facts; raw/protected values or transaction content leaks; an unreadable payload proceeds; transport and business outcomes collapse into one status; active eligibility is presented as exact network or payment assurance; an unknown result passes a later gate; another applicant's evidence is accepted; replay overwrites history; any canonical/downstream row or external action occurs; or an earlier safeguard regresses. Rollback disables/removes the route and panel; additive append-only evidence remains inert and requires a separately reviewed forward migration for correction.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic eligibility adapter/result. It does not substitute for payer/trading-partner, standards-licensing, identity, legal, privacy/security, accessibility, data, operational, interoperability, licensed-clinical, or production review.

## References

- [CMS eligibility inquiry and response overview](https://www.cms.gov/priorities/key-initiatives/burden-reduction/administrative-simplification/transactions/health-plan-eligibility-benefit-inquiry-response)
- [CMS adopted standards and operating rules](https://www.cms.gov/priorities/key-initiatives/burden-reduction/administrative-simplification/hipaa/adopted-standards-operating-rules)
- [Decision 0021](0021-approved-sprint-18-prospective-member-insurance-details.md)
- [Sprint 19 plan](../backlog/sprint-19-synthetic-prospective-eligibility-result.md)
