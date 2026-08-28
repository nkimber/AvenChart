# Decision 0028: Sprint 25 synthetic minimum registration-details confirmation

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed the Decision 0027 state-notice acknowledgment to review the exact minimum registration details copied during successful patient-shell promotion and record one immutable confirmation that they are current and need no correction.

This checkpoint confirms only legal name, date of birth, masked verified contact channels, residence state, and postal code. It does not collect or infer a street address, mailing address, sex/gender, race/ethnicity, language, emergency contact, guarantor, allergies, medications, history, communication preference, identity assurance, or insurance confirmation. It never edits the prospective applicant or canonical patient shell.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticTelehealthNoticeAcknowledged` aggregate with one successful promotion, one portal-disabled unmerged patient shell, and one current notice acknowledgment.
2. The server rebinds the applicant, promotion, patient shell, and notice receipt on every read/write. Every copied patient field must exactly match the source applicant; portal enablement, merge state, facility mismatch, missing provenance, or any data drift fails closed.
3. The read response may return the applicant's legal first/last name and date of birth, but email and telephone remain masked. It returns residence state and postal code only, never a street or mailing address, canonical/legacy patient identifier, proofing evidence, member value, staff rationale, or duplicate candidate information.
4. The browser cannot submit edits or replacement values. It may echo only the exact server snapshot fingerprint/version and five affirmative confirmations: legal name/birth date, verified contact channels, residence region, no correction needed, and synthetic-only use.
5. If anything is wrong, the UI directs the applicant to restart/contact the practice; this slice does not silently accept, patch, overwrite, merge, or start a correction workflow.
6. The receipt, `SyntheticTelehealthNoticeAcknowledged -> SyntheticMinimumRegistrationDetailsConfirmed` transition, and event commit in one PostgreSQL transaction. Database constraints and a provenance trigger independently verify the notice, promotion, patient shell, copied minimum fields, portal-disabled/unmerged state, and no-consequence flags.
7. Exact retry converges. Changed-key reuse, stale version/fingerprint, expired applicant, missing or altered notice/promotion/patient, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt and event.
8. `identityAssuranceEstablished=false`, `intakeCompleted=false`, `legalConsentEstablished=false`, and `insuranceConfirmed=false` are permanent for this slice. No patient-field mutation, portal/account/session, chart content, practice acceptance, canonical insurance/coverage, financial record, request, queue, appointment, encounter, care, prescribing, billing/claim, communication, integration, or external call is created.
9. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–24.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION`, version 1. |
| Entry state | `SyntheticTelehealthNoticeAcknowledged`. |
| Server projection | Legal name, date of birth, masked email, masked phone, residence state, postal code, and SHA-256 snapshot fingerprint. |
| Resulting status | `SyntheticMinimumRegistrationDetailsConfirmed`. |
| Applicant affirmations | Name/birth date, contact channels, residence region, no correction needed, and synthetic data. |
| Data consequence | Immutable receipt only; no applicant/patient field is changed. |

## 4. Explicit exclusions

This decision does not authorize real people or data; an identity assurance level; authoritative identity verification; edits or corrections; street/mailing address; complete demographics; clinical history; allergies/medications; legal/clinician consent; patient authentication or portal access; existing-patient linkage/merge; completed intake; practice acceptance; insurance confirmation or coverage promotion; estimate/payment; request/queue entry; appointment; encounter; communication/video; care; prescribing; billing/claim; external integration; or production enablement.

## 5. Stop conditions and rollback

Stop if the client can edit server-held details; if a mismatched applicant/patient snapshot can be confirmed; if full contact values, patient identifiers, street address, hidden evidence, or duplicate candidate data is disclosed; if correction is represented as completed; if receipt/state/event provenance can diverge; if retry overwrites history; if any patient field or downstream record changes; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable confirmation evidence is not deleted as rollback; correction requires a separately reviewed forward migration and workflow.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic, no-edit confirmation of minimum registration details. It does not substitute for identity, patient-registration, privacy/security, legal, clinical, accessibility, data, operations, interoperability, payer, or production review.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [UX content and accessibility specification](../17-ux-content-and-accessibility.md)
- [Decision 0027](0027-approved-sprint-24-state-specific-telehealth-notice-acknowledgment.md)
- [Sprint 25 plan](../backlog/sprint-25-minimum-registration-details-confirmation.md)
