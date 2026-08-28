# Decision 0034: Sprint 31 synthetic patient-reported allergy/intolerance information

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed Decision 0033 medication information to record one immutable allergy/intolerance-information receipt. When the prior allergy/intolerance inventory is `ItemsToReview`, the applicant may select zero or more entries from one server-owned local synthetic substance catalog and indicate whether additional or unlisted substances exist. `ItemsToReview` requires at least one selection or the additional/unlisted indicator. Prior `PatientReportsNone` and `Unsure` states accept no selected substance and remain provisional.

This receipt is patient-reported intake preparation only. It is not an allergy or intolerance diagnosis, clinical assessment, canonical allergy list, FHIR AllergyIntolerance, verified “no known allergy” assertion, reconciliation, reaction assessment, criticality assessment, contraindication check, alert, clinician review, or prescribing input. The catalog is deliberately incomplete, carries local synthetic keys only, and makes no SNOMED CT, RxNorm, or other external terminology mapping claim.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticMedicationInformationRecorded` aggregate with exact prior provenance.
2. Every read/write rebinds the applicant, promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access, passing device preparation, clinical-information inventory, medication-information receipt and items, passing safety location, verified callback source, and practice/facility. Broken or cross-applicant provenance, patient drift, portal enablement, merge state, canonical insurance, canonical medication data, canonical allergy data, or prior receipt drift fails closed, including exact replay revalidation inside the transaction.
3. The server owns one effective `LOCAL_SYNTHETIC_ONLY` catalog version containing exactly six local substance displays and opaque local keys: Amoxicillin and Ibuprofen (`Medication`), Peanut and Shellfish (`Food`), and Latex and Bee venom (`Environment`). No client-supplied display, category, SNOMED CT code, RxCUI, NDC, UNII, substance identifier, reaction, manifestation, type, clinical status, verification status, criticality, severity, onset, date, note, attachment, or free text is accepted.
4. Selection keys are unique, normalized in server catalog order, bounded to the catalog size, and persisted with the exact server-owned display, local category, catalog version, coding-system label, `snomedCtMapped=false`, and `rxNormMapped=false`.
5. Prior `ItemsToReview` requires at least one selected catalog substance or `additionalOrUnlistedItemsReported=true`. Prior `PatientReportsNone` and `Unsure` require zero selected substances and the additional/unlisted flag false. No branch may upgrade a provisional report into a verified clinical assertion or “no known allergy” record.
6. All four acknowledgments are mandatory: patient-reported information may be incomplete; the local catalog is synthetic and incomplete; no reaction, manifestation, severity, criticality, type, or timing is captured; and clinician verification/reconciliation remains required before care or prescribing.
7. The server derives exactly one informational route: `AdditionalAllergyCollectionRequired` when additional/unlisted substances are reported; otherwise `ClinicianAllergyReviewRequired` for `ItemsToReview`; `AssistedAllergyReviewRequired` for `Unsure`; or `PendingClinicianConfirmationOfPatientReportedNone` for `PatientReportsNone`. It creates no work item, priority, recommendation, alert, contraindication, or clinical authority.
8. `allergyIntoleranceCreated=false`, `allergyListReconciled=false`, `reactionAssessed=false`, `criticalityAssessed=false`, `contraindicationCheckPerformed=false`, `clinicianReviewCreated=false`, `clinicalIntakeCompleted=false`, `clinicalEligibilityEstablished=false`, `patientRecordChanged=false`, `requestCreated=false`, `queueEntered=false`, `careAuthorized=false`, and `prescribingEnabled=false` remain explicit.
9. The parent receipt, zero or more bounded child items, `SyntheticMedicationInformationRecorded -> SyntheticAllergyInformationRecorded` transition, and applicant event commit in one PostgreSQL transaction. Database constraints, provenance triggers, deferred item-count/branch checks, and append-only triggers independently enforce the contract.
10. Exact retry converges only after transactional provenance revalidation. Changed-key reuse, stale version/fingerprint, expired applicant, missing or altered provenance, patient/portal/canonical-insurance/canonical-medication/canonical-allergy drift, invalid/duplicate/over-limit catalog selections, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt/item set/event.
11. No applicant source field, canonical allergy/medication/prescription/clinical table, patient, insurance, financial, request, queue, appointment, encounter, care, prescribing, billing/claim, integration, alert, or external-call record is created or changed.
12. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–30.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_ALLERGY_INFORMATION`, version 1. |
| Entry state | `SyntheticMedicationInformationRecorded`. |
| Server snapshot | Prior inventory and allergy/intolerance state, exact medication receipt/item fingerprint, patient shell, practice/facility, and SHA-256 fingerprint. |
| Catalog | Server-owned local synthetic version 1; six opaque local keys, displays, and local categories only; no external terminology mapping claim. |
| Item | Substance selection only; no type, status, verification, reaction, severity, criticality, timing, or note. |
| Additional-item signal | Boolean; when true, route requires later separately authorized collection. |
| Required acknowledgments | Patient-reported/incomplete; catalog synthetic/incomplete; no reaction/criticality detail; clinician verification/reconciliation required. |
| Informational route | `AdditionalAllergyCollectionRequired`, `ClinicianAllergyReviewRequired`, `AssistedAllergyReviewRequired`, or `PendingClinicianConfirmationOfPatientReportedNone`; no task or authority is created. |
| Resulting status | `SyntheticAllergyInformationRecorded`. |
| Data consequence | Immutable applicant receipt/items only; no canonical allergy, medication, patient, request, queue, alert, or downstream record is changed. |

## 4. Standards alignment and limits

HL7 FHIR R4 [AllergyIntolerance](https://hl7.org/fhir/R4/allergyintolerance.html) is a record of a clinical assessment of an individual’s propensity for a harmful response to a substance and separates the causative substance, reporter, verification status, type, category, criticality, and reaction manifestations. It cautions that allergy versus intolerance may be difficult to determine and distinguishes reaction severity from criticality. The current [US Core AllergyIntolerance Profile](https://hl7.org/fhir/us/core/StructureDefinition-us-core-allergyintolerance.html) requires an identifying code and patient for an actual interoperable resource and gives separate guidance for “patient not asked” and “no known allergy” assertions.

Sprint 31 uses those distinctions only to prevent a patient selection from being misrepresented. It creates no FHIR resource, USCDI export, clinical/verification status, confirmed negation, SNOMED CT/RxNorm/NDC/UNII code, reaction, severity, criticality, alert, contraindication check, or interoperability payload. A real terminology search/mapping service, reaction and history collection, clinician verification/reconciliation, correction lifecycle, and medication-safety review remain future governed work.

Current state rules do not reduce the ordinary clinical-record boundary: [Georgia Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3) requires patient history availability, records, and the same standard of care; [California Business and Professions Code section 2290.5 guidance](https://www.dhcs.ca.gov/providers-partners/telehealth-frequently-asked-questions/) requires telehealth consent and documentation; and [Florida Statutes section 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499%2F0456%2FSections%2F0456.47.html) applies the prevailing in-person professional standard and medical-record documentation. This synthetic checkpoint is state-neutral and does not claim to satisfy a clinician’s history, assessment, record, consent, or standard-of-care obligations.

## 5. Explicit exclusions

This decision does not authorize real people or PHI; arbitrary substance text; reactions, manifestations, type, clinical status, verification status, criticality, severity, onset, dates, notes, attachments, or external identifiers; canonical allergy/intolerance records or “no known allergy” assertions; allergy history import; allergy reconciliation; contraindication/interaction checking; alerts; clinician task assignment; clinical advice; condition-specific triage; FHIR serialization; complete intake; legal or clinician consent; practice acceptance; request/queue entry; appointment; encounter; communication/video; care; prescribing; pharmacy transmission; billing/claim; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if client-supplied substance display/category/coding or any reaction/severity/criticality/free text crosses the route; if the local catalog is represented as complete or terminology mapped; if a patient report is represented as verified, reconciled, or “no known allergy”; if routing creates an alert, task, contraindication, or clinical/operational authority; if any canonical allergy, medication, prescription, patient, request, or downstream row changes; if parent/item/state/event provenance can diverge; if retry bypasses current provenance; if history is overwritten; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable receipt evidence is not deleted as rollback; correction requires a separately reviewed forward workflow.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic patient-reported allergy/intolerance-information receipt with permanently false clinical assessment, reconciliation, reaction/criticality assessment, contraindication check, clinical review, intake, eligibility, care, and prescribing consequences. It does not substitute for licensed clinical/medical-director, allergy/medication-safety, patient-registration/HIM, privacy/security, accessibility, data, legal, terminology, interoperability, operations, or production review.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Clinical eligibility, triage, and safety specification](../05-clinical-triage-and-safety.md)
- [Prescribing and pharmacy specification](../11-prescribing-and-pharmacy.md)
- [Data model and retention specification](../14-data-model-and-retention.md)
- [Decision 0033](0033-approved-sprint-30-synthetic-medication-information.md)
- [Sprint 31 plan](../backlog/sprint-31-synthetic-allergy-information.md)
