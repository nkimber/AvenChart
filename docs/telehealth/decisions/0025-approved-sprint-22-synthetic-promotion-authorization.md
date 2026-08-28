# Decision 0025: Sprint 22 synthetic promotion authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit an authorized administrator for the configured practice and facility to review the complete, unexpired synthetic prospective-applicant chain after `SyntheticIdentityProofingRecorded` and append one `AuthorizedForSyntheticPromotion` or `DeniedForSyntheticPromotion` decision. Authorization moves only the prospective aggregate to `SyntheticPromotionAuthorized`; denial moves it to `SyntheticPromotionDenied`.

The reviewer must explicitly acknowledge that the upstream fixture achieved assurance level `None`, did not prove identity, and uses synthetic data. This decision is a human governance checkpoint for a separately approved future synthetic promotion exercise only. It is not patient creation, patient matching, practice acceptance, identity proofing, an account, consent, a request, a queue entry, or authorization for care.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, branded-practice/facility scoped, private/no-store, audited, permission-filtered, and limited to an authorized administrator or active bound front-desk staff identity.
2. The review queue includes only unexpired applicants currently at `SyntheticIdentityProofingRecorded` whose complete server-bound review, safety, purpose, plan, member-receipt, eligibility, practice-network, and identity-process chain is present and consistent.
3. The queue is PHI-minimized to review-relevant synthetic details: applicant identity and masked contact, location, selected plan, normalized eligibility/network/process outcomes, assurance `None`, timestamps, and version. It exposes no raw insurance value, proofing evidence, opaque provider reference, government identifier, biometric, possible matching patient, canonical identifier, or vendor payload.
4. The command accepts only expected version, one enumerated decision, a normalized reason, explicit acknowledgment of assurance `None` and no identity proofing, and explicit synthetic-data confirmation. The browser cannot supply or override evidence or consequences.
5. Authorization is allowed only when the immutable proofing fixture says `SyntheticProofingPassed`, `assuranceLevelAchieved=None`, `identityProofed=false`, all proofing evidence/source/biometric/authenticator flags are false, and the fixture is unexpired. Denial remains available for a valid queued item.
6. Exactly one immutable decision and aggregate event are appended. Exact retry converges; changed-key reuse, stale/second commands, missing or expired evidence, facility mismatch, and concurrent first writers fail closed with one winner.
7. The database independently binds every upstream identifier and normalized snapshot to the same applicant and enforces the reviewer acknowledgments, resulting status, actor attribution, policy metadata, and hard-false downstream consequences.
8. The applicant remains prospective. Real identity proofing, patient/chart/account, intake completion, consent, practice acceptance, coverage/financial, request/queue, appointment/encounter/care, prescribing, billing/claim, communication, integration, and external-call consequences remain false.
9. The interface must distinguish this governance decision from the earlier contact/duplicate review, prominently state its synthetic-only meaning, support keyboard and screen-reader operation, preserve the exact command and retry key after an ambiguous failure, and provide independent refresh/recovery.
10. Unit, API, authorization, live PostgreSQL fixture/replay/contention/append-only/no-delta, response minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–21.

## 3. Normalized decision contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION`, version 1. |
| Evidence type | `COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY`. |
| Allowed decisions | `AuthorizedForSyntheticPromotion` or `DeniedForSyntheticPromotion`. |
| Resulting statuses | `SyntheticPromotionAuthorized` or `SyntheticPromotionDenied`. |
| Required acknowledgments | `noneAssuranceAcknowledged=true` and `syntheticDataConfirmed=true`. |
| Identity premise | `assuranceLevelAchieved=None`; `identityProofed=false`. |
| Meaning | Permission for a later separately gated synthetic promotion exercise only. |

## 4. Explicit exclusions

This decision does not authorize real people or data; patient matching or merge; a canonical patient, chart, portal identity, authenticator, intake completion, telehealth consent, practice acceptance, coverage/financial record, request, queue, appointment, encounter, clinician assignment, video, care, prescribing, pharmacy transmission, billing, claim, communication, external integration, real identity provider, IAL/AAL/FAL claim, or production enablement.

## 5. Stop conditions and rollback

Stop if authorization creates or links a patient or downstream record; if the client can assert evidence or assurance; if incomplete, failed, unknown, stale, expired, cross-applicant, or mismatched evidence reaches the queue or can be authorized; if raw insurance or proofing evidence is disclosed; if actor attribution or acknowledgments can be bypassed; if retry overwrites history; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel; additive append-only decisions remain inert and require a separately reviewed forward migration for correction.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic governance checkpoint. It does not substitute for legal, privacy/security, identity, fraud, accessibility, data, operations, payer, licensed-clinical, interoperability, or production review.

## References

- [NIST SP 800-63A-4 identity-proofing requirements](https://pages.nist.gov/800-63-4/sp800-63a/ial-general/)
- [45 CFR 164.312 technical safeguards](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.312)
- [California Business and Professions Code § 2290.5](https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=BPC&sectionNum=2290.5)
- [Florida Statutes § 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&Search_String=health+care+provider&SubMenu=1&URL=0400-0499%2F0456%2FSections%2F0456.47.html&mode=View+Statutes)
- [Georgia Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3)
- [Decision 0024](0024-approved-sprint-21-synthetic-identity-proofing-process.md)
- [Sprint 22 plan](../backlog/sprint-22-synthetic-promotion-authorization.md)
