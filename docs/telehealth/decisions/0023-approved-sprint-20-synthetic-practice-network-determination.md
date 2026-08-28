# Decision 0023: Sprint 20 synthetic practice-network determination

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of an unexpired synthetic prospective applicant in `SyntheticEligibilityRecorded` to request one deterministic, non-production practice-network determination for the selected plan, configured practice/facility, current location state, professional telehealth service, and current date. The server binds the complete eligibility and upstream provenance, invokes one internal directory adapter shaped around HL7 FHIR R4 Da Vinci PDex Plan-Net 1.2.0 concepts, and records one immutable normalized result at `SyntheticPracticeNetworkRecorded`.

This is practice/facility/service adapter-contract evidence only. It is not a live provider-directory lookup, payer confirmation, contract verification, rendering-physician participation check, canonical coverage, payment guarantee, estimate, patient responsibility, practice acceptance, identity proofing, patient promotion, consent, request/queue creation, or care authorization.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, branded-host/practice/facility scoped, applicant-access-key protected, private/no-store, and unavailable outside `SyntheticEligibilityRecorded`.
2. The command accepts only expected applicant version and explicit synthetic confirmation. The client cannot supply or replace plan/network, practice, facility, provider, location, service, date, directory result, freshness, trace, or consequence facts.
3. The server rebinds the complete review, safety, purpose, practice-plan, protected member-receipt, and eligibility-result provenance. Eligibility evidence must remain fresh. Missing, expired, stale, cross-applicant, mismatched, or impossible upstream evidence fails closed.
4. The directory adapter is exactly `NON_PRODUCTION`, effective-dated, deterministic, receives no member/subscriber fields, and separately represents transport availability, plan/network match, practice affiliation, service availability, new-patient acceptance, and business outcome.
5. The adapter advertises compatibility target `HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0` as normalized metadata only. It creates no FHIR resource or bundle, claims no Plan-Net conformance, contacts no endpoint, and stores no third-party directory payload.
6. Fixtures cover practice-in-network/accepting, practice-out-of-network, and unavailable/unknown behavior. An in-network practice result never implies rendering-physician participation, member coverage, payment, price, capacity, appointment availability, or practice acceptance. Unknown never silently passes.
7. Public output contains only applicant/result/version/state, selected-plan display data, prior normalized eligibility statuses, configured practice display, synthetic directory/standard/dataset metadata, opaque traces, normalized non-financial network statuses, freshness, fixed next action, and explicit limitations.
8. Exactly one immutable result and one event are appended. Exact replay converges before adapter invocation; changed key reuse, stale/second commands, and concurrent first writers fail closed with one winner.
9. `practiceNetworkChecked` and `practiceInNetwork` may reflect only the deterministic fixture. `renderingPhysicianNetworkChecked`, aggregate `exactNetworkConfirmed`, canonical coverage, financial, identity/patient, consent, practice acceptance, request/queue, clinical, prescribing, billing/claim, communication, integration, and external-call capabilities remain false.
10. Unit, adapter-contract, API, authorization, live PostgreSQL fixture/replay/contention/append-only/no-delta, public minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–19.

## 3. Standards baseline

CMS requires certain impacted payers to expose public provider-directory APIs containing contracted network-provider information and encourages Plan-Net alignment for applicable implementations. HL7 Plan-Net 1.2.0 is a trial-use FHIR R4 implementation guide representing insurance plans and networks plus practitioner or organization participation, locations, and services. The production design should preserve those relationships and source freshness, but exact payer-specific semantics, contracts, endpoint discovery, validation, and reconciliation require separate interoperability, payer, legal, security, data, and operational approval.

## 4. Explicit exclusions

This decision does not authorize real insurance/PHI, a live provider-directory API, FHIR serialization, payer or clearinghouse connectivity, provider contracts, NPI matching, real practice or physician participation decisions, canonical insurance/coverage, payment/financial amounts, estimates/self-pay, identity proofing, patient promotion/linkage, consent, practice acceptance, request/queue creation, care, prescribing, billing, claims, external integration, production enablement, or real people.

## 5. Stop conditions and rollback

Stop if a client can influence authoritative directory facts; member/subscriber data reaches the directory adapter; transport, affiliation, service, acceptance, and business outcomes collapse; practice affiliation is presented as physician participation or payment assurance; stale/unknown evidence passes; another applicant's evidence is accepted; replay invokes the adapter or overwrites history; any canonical/downstream row or external action occurs; or an earlier safeguard regresses. Rollback disables/removes the route and panel; additive append-only evidence remains inert and requires a separately reviewed forward migration for correction.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic practice-network adapter/result. It does not substitute for payer/directory, identity, legal, privacy/security, accessibility, data, operational, interoperability, licensed-clinical, or production review.

## References

- [CMS Provider Directory API FAQ](https://www.cms.gov/priorities/burden-reduction/overview/interoperability/frequently-asked-questions/provider-directory-api)
- [CMS APIs and relevant standards/implementation guides](https://www.cms.gov/priorities/burden-reduction/overview/interoperability/implementation-guides-standards/application-programming-interfaces-apis-relevant-standards-implementation-guides-igs)
- [HL7 Da Vinci PDex Plan-Net 1.2.0 implementation](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/implementation.html)
- [HL7 Plan-Net Network profile](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/StructureDefinition-plannet-Network.html)
- [Decision 0022](0022-approved-sprint-19-synthetic-prospective-eligibility-result.md)
- [Sprint 20 plan](../backlog/sprint-20-synthetic-practice-network-determination.md)
