# Decision 0026: Sprint 23 atomic synthetic patient promotion

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit an authorized administrator for the configured practice and facility to execute one atomic synthetic promotion after `SyntheticPromotionAuthorized`. The transaction must recheck the complete immutable evidence chain and current patient duplicates under the same registration advisory lock used by the canonical patient workspace.

When no current possible patient match exists, the transaction may create exactly one canonical patient shell from the already collected synthetic applicant demographics, link it immutably to the applicant, and move the prospective aggregate to `SyntheticPatientPromoted`. When a possible current patient match exists, it must create no patient and move the aggregate to `SyntheticPromotionBlockedPossibleMatch` with a minimized immutable result. This decision does not authorize automatic linkage to an existing patient.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, permission-filtered, and limited to an active administrator in the configured practice/facility. Front-desk staff may authorize the prior governance decision but may not execute canonical creation in this slice.
2. Execution requires the exact applicant and one immutable `AuthorizedForSyntheticPromotion` decision at the current version, a complete same-applicant upstream chain, unexpired applicant/process evidence, `assuranceLevelAchieved=None`, `identityProofed=false`, and every real-evidence/source/biometric/authenticator flag false.
3. The command accepts only expected version, an explicit `canonicalPatientCreationAcknowledged=true`, an explicit `noPortalNoCareAcknowledged=true`, and a normalized reason. It cannot accept patient demographics, identifiers, duplicate outcomes, assurance, resulting state, or consequence flags.
4. The repository acquires the canonical patient-registration advisory transaction lock, locks the applicant, returns exact replay before state mutation, and repeats the facility-scoped name/date-of-birth, date-of-birth/email, and date-of-birth/phone duplicate query inside the transaction.
5. A possible match is a fail-closed outcome: no canonical patient is inserted, no existing patient is linked or disclosed, and no client receives candidate identity or count. Resolution requires a separately approved human matching/linkage workflow.
6. A no-match outcome creates one patient with a deterministic synthetic telehealth public/canonical identifier, one legacy sequence value, applicant legal name/date of birth/email/phone/state/postal code, the configured facility, `portal_enabled=false`, no provider, no consent assumptions, no communication preferences, and a synthetic promotion purpose. Fields not collected remain null.
7. The patient insert, immutable promotion record, applicant state/version change, and aggregate event are one PostgreSQL transaction. Database constraints and a provenance trigger independently bind the patient shell to the applicant and authorization decision.
8. Exact retry converges. Changed-key reuse, stale/expired/mismatched evidence, denial, a second semantic command, identifier collision, partial failure, and concurrent first writers fail closed with at most one patient, promotion record, and event.
9. The response is minimized. The applicant sees only a coarse promoted-or-blocked status. The staff result may say whether a patient shell was created but must not return possible matches, a legacy PID, or hidden proofing/member evidence.
10. No portal account/session/external identity, authenticator, chart content, complete intake, consent, practice acceptance, insurance/coverage, financial record, request, queue, appointment, encounter, care, prescribing, billing/claim, communication, integration, or external call is created.
11. Unit, API, authorization, live PostgreSQL duplicate/race/replay/atomicity/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–22.

## 3. Normalized transaction contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION`, version 1. |
| Allowed command | `PromoteAuthorizedSyntheticApplicant`. |
| Success outcome/status | `SyntheticPatientCreated` / `SyntheticPatientPromoted`. |
| Duplicate outcome/status | `BlockedPossiblePatientMatch` / `SyntheticPromotionBlockedPossibleMatch`. |
| Required acknowledgments | `canonicalPatientCreationAcknowledged=true`; `noPortalNoCareAcknowledged=true`. |
| Patient identity | Deterministic `TH-PAT-<applicant-guid-n>`; portal disabled. |
| Existing-patient behavior | Never link, merge, disclose, or mutate. |

## 4. Explicit exclusions

This decision does not authorize real people or data; assurance above `None`; automatic or manual existing-patient linkage; merge; portal enrollment; authenticator binding; external identity mapping; intake completion; consent; practice acceptance; canonical insurance/coverage; estimate/payment; request/queue entry; appointment; encounter; clinician assignment; video; care; prescribing; pharmacy transmission; billing; claim; communication; external integration; or production enablement.

## 5. Stop conditions and rollback

Stop if an existing patient is linked or disclosed; if duplicate recheck is outside the patient-registration transaction lock; if a blocked outcome creates a patient; if patient creation and promotion evidence can diverge; if the client can author demographics, identifiers, duplicate results, or consequences; if retry overwrites history; if a portal/downstream record appears; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Canonical patient and append-only promotion evidence are not deleted as rollback; correction requires an independently reviewed patient-safety workflow and forward migration.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled, deterministic synthetic canonical-patient shell transaction. It does not substitute for identity, patient-matching, legal, privacy/security, accessibility, data, operations, payer, licensed-clinical, interoperability, or production review.

## References

- [2025 ONC SAFER Guide: Patient Identification](https://healthit.gov/resources/2025-safer-guide-patient-identification/)
- [ONC patient identity and patient record matching](https://healthit.gov/standards-and-technology/patient-identity-and-patient-record-matching/)
- [NIST SP 800-63A-4 identity-proofing requirements](https://pages.nist.gov/800-63-4/sp800-63a/ial-general/)
- [45 CFR 164.312 technical safeguards](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.312)
- [Decision 0025](0025-approved-sprint-22-synthetic-promotion-authorization.md)
- [Sprint 23 plan](../backlog/sprint-23-atomic-synthetic-patient-promotion.md)
