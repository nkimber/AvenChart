# Decision 0027: Sprint 24 state-specific synthetic telehealth-notice acknowledgment

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the owner of a successfully promoted synthetic applicant to retrieve one server-selected Georgia, California, or Florida telehealth-notice fixture and record one immutable acknowledgment. The notice is selected from the current physical-location state already captured by the passing safety screen and is bound to the exact applicant, promotion, portal-disabled patient shell, practice, facility, notice key/version, official-source reference, and current aggregate version.

This is a preparation and comprehension checkpoint only. It is not legally effective informed consent, clinician-obtained consent, a signature, practice acceptance, or authorization to request or receive care.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticPatientPromoted` aggregate with exactly one successful promotion and one portal-disabled synthetic patient shell.
2. The server selects the notice by the passing safety screen's current-location state. The browser may echo only the exact notice key/version and must reconfirm the unchanged state; a different state fails closed and requires a fresh safety/location process.
3. The catalog distinguishes the jurisdictions without claiming legal sufficiency: California states that the initiating provider must inform the patient, obtain verbal or written telehealth consent, and document it; Georgia defers provider identity, credentials, emergency contact, follow-up, and emergency instructions to the later clinician gate; Florida states the same-standard-of-care, records, confidentiality, provider-registration, and patient-location boundaries without inventing a separate statutory-consent rule.
4. The patient must affirm telehealth mode/limitations, privacy limitations, emergency instructions, in-person options, later clinician reconfirmation, current location, and synthetic-only use. All acknowledgments are affirmative and independently constrained.
5. The acknowledgment, applicant state/version transition, and aggregate event commit in one PostgreSQL transaction. Database constraints and a provenance trigger independently bind the immutable receipt to the successful promotion, patient shell, safety location, and server notice.
6. Exact retry converges. Changed-key reuse, stale version, expired applicant, missing or blocked promotion, changed location, changed notice, portal-enabled or missing patient, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt and event.
7. Responses are minimized: no canonical/legacy patient identifier, raw demographics, member value, proofing evidence, or staff rationale is returned. The applicant sees notice content/metadata and a coarse acknowledged status only.
8. `legalConsentEstablished=false` and `clinicianConsentDocumented=false` are permanent for this slice. No portal account/session/external mapping, chart content, completed intake, practice acceptance, canonical insurance/coverage, financial record, request, queue, appointment, encounter, care, prescribing, billing/claim, communication, integration, or external call is created.
9. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–23.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_TELEHEALTH_NOTICE_ACKNOWLEDGMENT`, version 1. |
| Source state | The passing universal safety screen's `current_location_state_code`; `GA`, `CA`, or `FL`. |
| Notice | Server-selected `GA_TELEHEALTH_NOTICE_V1`, `CA_TELEHEALTH_NOTICE_V1`, or `FL_TELEHEALTH_NOTICE_V1`. |
| Resulting status | `SyntheticTelehealthNoticeAcknowledged`. |
| Applicant affirmations | Location, mode/limitations, privacy, emergency instructions, in-person option, later clinician reconfirmation, and synthetic data. |
| Consent consequence | None; `legalConsentEstablished=false`, `clinicianConsentDocumented=false`. |

## 4. Jurisdiction boundary

The official sources establish materially different boundaries. California Business and Professions Code § 2290.5 requires the provider initiating telehealth to inform the patient, obtain verbal or written consent, and document it before delivery. Georgia Rule 360-3-.07 requires later provider/licensure, history/examination, records, provider-identity/credential/emergency-contact, and follow-up/emergency-instruction controls. Florida Statutes § 456.47 defines telehealth, applies the prevailing in-person standard of practice, governs records/confidentiality and out-of-state registration, and locates the act where the patient is located. The product therefore must not treat this pre-clinician acknowledgment as final consent in any state.

## 5. Explicit exclusions

This decision does not authorize real people or data; legal-consent sufficiency; clinician consent; electronic signature; identity assurance above `None`; patient authentication or portal access; existing-patient linkage/merge; remaining intake; practice acceptance; insurance/coverage promotion; estimate/payment; request/queue entry; appointment; encounter; clinician assignment; communication/video; care; prescribing; pharmacy transmission; billing/claim; external integration; or production enablement.

## 6. Stop conditions and rollback

Stop if the client selects a jurisdiction different from server evidence; if the receipt is labeled final consent; if a missing, blocked, or portal-enabled patient can proceed; if notice/provenance and aggregate state can diverge; if retry overwrites history; if a patient identifier or hidden evidence is disclosed; if any portal/downstream record appears; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable acknowledgment evidence is not deleted as rollback; correction requires a forward migration and independent legal/clinical review.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic state-notice acknowledgment with permanently false legal-consent and clinical consequences. It does not substitute for Georgia, California, or Florida counsel; licensed clinical governance; privacy/security, accessibility, data, operations, identity, patient-matching, interoperability, payer, or production review.

## References

- [California Business and Professions Code § 2290.5](https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=BPC&sectionNum=2290.5.)
- [Georgia Composite Medical Board Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3-.07)
- [Florida Statutes § 456.47](https://leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499/0456/Sections/0456.47.html)
- [Decision 0026](0026-approved-sprint-23-atomic-synthetic-patient-promotion.md)
- [Sprint 24 plan](../backlog/sprint-24-state-specific-telehealth-notice-acknowledgment.md)
