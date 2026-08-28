# Decision 0024: Sprint 21 synthetic identity-proofing process

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of an unexpired synthetic prospective applicant in `SyntheticPracticeNetworkRecorded` to acknowledge a synthetic privacy notice and request one deterministic, non-production identity-proofing process fixture. The server permits the step only when the bound eligibility result is active with reported benefits and the bound practice/facility/service directory fixture is in network and accepting new patients. It sends only opaque applicant/evidence references plus server-owned scope and notice metadata to one internal adapter and records one immutable normalized result at `SyntheticIdentityProofingRecorded`.

The adapter separately represents transport, evidence-reference collection, evidence validation, attribute validation, applicant verification, fraud review, and business outcome. It uses NIST SP 800-63A-4 process concepts as compatibility metadata only. The result always records `assuranceLevelAchieved=None` and `identityProofed=false`.

This is synthetic adapter-contract and process-shape evidence only. It is not real identity proofing, an IAL1/IAL2/IAL3 result, a Digital Identity Acceptance Statement, a certification, an identity-provider or authoritative-source call, a patient record, an account, consent to telehealth care, practice acceptance, a request, a queue entry, or care authorization.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, branded-host/practice/facility scoped, applicant-access-key protected, private/no-store, and unavailable outside a fresh positive eligibility plus positive practice-network fixture.
2. The command accepts only expected applicant version, acknowledgment of the fixed synthetic privacy notice, and explicit synthetic confirmation. The browser cannot supply names, dates of birth, addresses, identifiers, evidence, proofing method, assurance level, validation/verification result, fraud result, trace, freshness, or consequence facts.
3. The server rebinds the complete review, safety, purpose, plan-precheck, protected member-receipt, eligibility, and practice-network provenance chain. Missing, expired, stale, cross-applicant, mismatched, inactive, unknown, out-of-network, or unavailable evidence fails closed.
4. The adapter is exactly `NON_PRODUCTION`, deterministic, effective-dated, and receives only opaque applicant/evidence references plus configured practice, facility, state, proofing profile, notice version, and server time. It receives no legal name, birth date, contact value, address, insurance value, government identifier, image, video, biometric, or raw evidence.
5. Compatibility target `NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY` is metadata, not conformance. No fair/strong/superior evidence, core/government identifier, authoritative-source validation, evidence ownership verification, biometric/PAD, fraud program, notification, redress case, or authenticator binding occurs.
6. The fixture status words always include `Fixture`; `SyntheticProofingPassed` means only that the deterministic process exercise completed. It cannot set `identityProofed`, an assurance level, patient promotion, portal identity, authorization, or any downstream capability.
7. Public output contains only applicant/result/version/state, selected-plan key, fixed notice/adapter/practice-statement/dataset metadata, opaque traces and references, normalized fixture statuses, freshness, fixed next action, explicit false consequences, and limitations. It returns no applicant demographic, contact, insurance, evidence, document, government identifier, biometric, or authoritative response.
8. Exactly one immutable result and event are appended. Exact replay converges before adapter invocation; changed-key reuse, stale/second commands, expired evidence, and concurrent first writers fail closed with one winner.
9. The applicant remains prospective. Identity evidence, government identifier, biometric, authoritative query, notification, redress, authenticator, real identity proofing, canonical patient/chart/account, intake completion, consent, practice acceptance, coverage/financial, request/queue, clinical, prescribing, billing/claim, communication, integration, and external-call flags remain false.
10. Unit, adapter-contract, API, authorization, live PostgreSQL fixture/replay/contention/append-only/no-delta, public minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–20.

## 3. Standards and jurisdiction baseline

NIST SP 800-63A-4 treats identity proofing as more than a single match: it specifies documented practice statements, evidence and attribute collection, evidence validation, applicant verification, fraud management, privacy/security controls, notification, redress, and authenticator binding. IAL2 additionally requires defined evidence strengths, at least one government identifier among core attributes, validation against authoritative or credible sources, approved ownership-verification paths, and proofing notification. Because this slice performs none of those real operations, it must not claim any IAL.

HIPAA's Security Rule requires procedures to verify that a person or entity seeking access to electronic protected health information is the one claimed. This slice creates no patient account or ePHI authorization and cannot be used to satisfy that production access-control obligation.

California requires telehealth consent before delivery and preserves ordinary confidentiality and professional standards. Florida applies the prevailing in-person professional standard and medical-record duties to telehealth. Georgia requires appropriate examination capability, an available patient history, and documentation of evaluation, treatment, and practitioner identity. The synthetic proofing fixture does not start care, satisfy consent, establish a patient relationship, or replace any clinical requirement in those states.

## 4. Explicit exclusions

This decision does not authorize real people or data; documents; government identifiers; SSNs; images; video; biometrics; liveness/PAD; authoritative, issuing, credit, government, carrier, device, fraud, or death-record sources; knowledge-based verification; identity-provider connectivity; webhooks; real notifications; redress adjudication; authenticator enrollment; IAL/AAL/FAL claims; patient matching or promotion; portal accounts; telehealth consent; practice acceptance; rendering-clinician verification; request/queue creation; care; prescribing; billing/claims; external integration; or production enablement.

## 5. Stop conditions and rollback

Stop if the client can assert proofing outcomes or assurance; raw identity, contact, insurance, document, biometric, or government-identifier data reaches the adapter or public response; an unknown/failed/expired upstream result passes; another applicant's evidence is accepted; fixture statuses are presented as real identity proofing; replay invokes the adapter or overwrites history; an account/canonical/downstream row or external action occurs; or an earlier safeguard regresses. Rollback disables/removes the route and panel; additive append-only evidence remains inert and requires a separately reviewed forward migration for correction.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic process fixture. It does not substitute for identity-vendor, legal, privacy/security, fraud, accessibility, data, operational, interoperability, licensed-clinical, or production review.

## References

- [NIST SP 800-63A-4 identity-proofing requirements](https://pages.nist.gov/800-63-4/sp800-63a/ial-general/)
- [NIST SP 800-63A-4 IAL requirements](https://pages.nist.gov/800-63-4/sp800-63a/ial/)
- [45 CFR 164.312 technical safeguards](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.312)
- [California Business and Professions Code § 2290.5](https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=BPC&sectionNum=2290.5)
- [Florida Statutes § 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&Search_String=health+care+provider&SubMenu=1&URL=0400-0499%2F0456%2FSections%2F0456.47.html&mode=View+Statutes)
- [Georgia Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3)
- [Decision 0023](0023-approved-sprint-20-synthetic-practice-network-determination.md)
- [Sprint 21 plan](../backlog/sprint-21-synthetic-identity-proofing-process.md)
