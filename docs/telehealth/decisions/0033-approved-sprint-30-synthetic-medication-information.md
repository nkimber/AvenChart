# Decision 0033: Sprint 30 synthetic patient-reported medication information

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed Decision 0032 clinical-information inventory to record one immutable medication-information receipt. When the prior medication category is `ItemsToReview`, the applicant may select zero or more entries from one server-owned local synthetic ingredient catalog, assign only `Taking`, `NotTaking`, or `Unsure` to each selected entry, and indicate whether additional or unlisted items exist. `ItemsToReview` requires at least one selection or the additional/unlisted indicator. Prior `PatientReportsNone` and `Unsure` states accept no selected medication and remain provisional.

This receipt is patient-reported intake evidence only. It is not a canonical medication list, MedicationStatement, MedicationRequest, prescription, medication reconciliation, interaction check, adherence assessment, medication history, clinician review, or prescribing input. The catalog is deliberately incomplete, carries local synthetic keys only, and makes no RxNorm mapping claim.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticClinicalInformationInventoryRecorded` aggregate with exact prior provenance.
2. Every read/write rebinds the applicant, promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access, passing device preparation, clinical-information inventory, passing safety location, verified callback source, and practice/facility. Broken or cross-applicant provenance, patient drift, portal enablement, merge state, canonical insurance, canonical medication data, or prior receipt drift fails closed, including exact replay revalidation inside the transaction.
3. The server owns one effective `LOCAL_SYNTHETIC_ONLY` catalog version containing generic ingredient display names and opaque local keys. No client-supplied display, RxCUI, NDC, SNOMED CT code, medication identifier, strength, form, dose, route, frequency, timing, indication, prescriber, pharmacy, date, note, attachment, or free text is accepted.
4. A selected item accepts only one reported-use state: `Taking`, `NotTaking`, or `Unsure`. Selection keys are unique, normalized in server catalog order, bounded to the catalog size, and persisted with the exact server-owned display, catalog version, coding-system label, and `rxNormMapped=false`.
5. Prior `ItemsToReview` requires at least one selected catalog item or `additionalOrUnlistedItemsReported=true`. Prior `PatientReportsNone` and `Unsure` require zero selected items and the additional/unlisted flag false. No branch may upgrade a provisional report into verified clinical evidence.
6. All four acknowledgments are mandatory: patient-reported information may be incomplete; the local catalog is synthetic and incomplete; no dose or directions are captured; and clinician reconciliation remains required before care or prescribing.
7. The server derives exactly one informational route: `AdditionalMedicationCollectionRequired` when additional/unlisted items are reported; otherwise `ClinicianMedicationReviewRequired` for `ItemsToReview`; `AssistedMedicationReviewRequired` for `Unsure`; or `PendingClinicianConfirmationOfNone` for `PatientReportsNone`. It creates no work item, priority, recommendation, or clinical authority.
8. `medicationStatementCreated=false`, `medicationRequestCreated=false`, `medicationListReconciled=false`, `interactionCheckPerformed=false`, `clinicianReviewCreated=false`, `clinicalIntakeCompleted=false`, `clinicalEligibilityEstablished=false`, `patientRecordChanged=false`, `requestCreated=false`, `queueEntered=false`, `careAuthorized=false`, and `prescribingEnabled=false` remain explicit.
9. The parent receipt, zero or more bounded child items, `SyntheticClinicalInformationInventoryRecorded -> SyntheticMedicationInformationRecorded` transition, and applicant event commit in one PostgreSQL transaction. Database constraints, provenance triggers, deferred item-count/branch checks, and append-only triggers independently enforce the contract.
10. Exact retry converges only after transactional provenance revalidation. Changed-key reuse, stale version/fingerprint, expired applicant, missing or altered provenance, patient/portal/canonical-insurance/canonical-medication drift, invalid/duplicate/over-limit catalog selections, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt/item set/event.
11. No applicant source field, canonical medication/prescription/clinical table, patient, insurance, financial, request, queue, appointment, encounter, care, prescribing, billing/claim, integration, or external-call record is created or changed.
12. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–29.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_MEDICATION_INFORMATION`, version 1. |
| Entry state | `SyntheticClinicalInformationInventoryRecorded`. |
| Server snapshot | Prior inventory and its medication state, passing device preparation, patient shell, practice/facility, and SHA-256 fingerprint. |
| Catalog | Server-owned local synthetic version 1; opaque local key and ingredient display only; no RxNorm mapping claim. |
| Item state | `Taking`, `NotTaking`, or `Unsure`; no dose, route, frequency, timing, indication, or note. |
| Additional-item signal | Boolean; when true, route requires later separately authorized collection. |
| Required acknowledgments | Patient-reported/incomplete; catalog synthetic/incomplete; no dose/directions; clinician reconciliation required. |
| Informational route | `AdditionalMedicationCollectionRequired`, `ClinicianMedicationReviewRequired`, `AssistedMedicationReviewRequired`, or `PendingClinicianConfirmationOfNone`; no task or authority is created. |
| Resulting status | `SyntheticMedicationInformationRecorded`. |
| Data consequence | Immutable applicant receipt/items only; no canonical medication, patient, request, queue, or downstream record is changed. |

## 4. Standards alignment and limits

HL7 FHIR R4 [MedicationStatement](https://hl7.org/fhir/R4/medicationstatement.html) describes a medication-use report whose source may be the patient and whose details can be incomplete; it requires a status and medication when such a resource is actually created, while dosage is optional. It also distinguishes a medication statement from a medication request or administration. The [USCDI Version 6](https://www.healthit.gov/isp/sites/isp/files/2025-07/USCDI-Version-6-July-2025.pdf) treats medications as a clinical data class, and the National Library of Medicine describes [RxNorm](https://www.nlm.nih.gov/research/umls/rxnorm/index.html) as normalized names and identifiers for clinical drugs.

Sprint 30 uses those distinctions only to retain patient source, medication identity, reported-use status, and missing-detail limitations as separate concepts. It creates no FHIR resource, USCDI export, RxNorm/NDC/SNOMED CT coding, dose, directions, medication order, interaction checking, or interoperability payload. A real terminology search/mapping service, clinical reconciliation workflow, history/correction lifecycle, and medication-safety review remain future governed work.

## 5. Explicit exclusions

This decision does not authorize real people or PHI; arbitrary medication text; strengths, forms, doses, routes, frequencies, timing, indications, prescribers, pharmacies, dates, notes, attachments, or external identifiers; canonical medication statements/lists; medication history import; medication reconciliation; interaction/contraindication checking; adherence assessment; clinician task assignment; clinical advice; condition-specific triage; FHIR serialization; complete intake; legal or clinician consent; practice acceptance; request/queue entry; appointment; encounter; communication/video; care; prescribing; pharmacy transmission; billing/claim; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if client-supplied medication display/coding or any dose/directions/free text crosses the route; if the local catalog is represented as complete or RxNorm-mapped; if a patient report is represented as reconciled; if routing creates a task or clinical/operational authority; if any canonical medication, prescription, patient, request, or downstream row changes; if parent/item/state/event provenance can diverge; if retry bypasses current provenance; if history is overwritten; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable receipt evidence is not deleted as rollback; correction requires a separately reviewed forward workflow.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic patient-reported medication-information receipt with permanently false reconciliation, clinical review, intake, eligibility, care, and prescribing consequences. It does not substitute for licensed clinical/medical-director, pharmacy/medication-safety, patient-registration/HIM, privacy/security, accessibility, data, legal, terminology, interoperability, operations, or production review.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Clinical eligibility, triage, and safety specification](../05-clinical-triage-and-safety.md)
- [Prescribing and pharmacy specification](../11-prescribing-and-pharmacy.md)
- [Data model and retention specification](../14-data-model-and-retention.md)
- [Decision 0032](0032-approved-sprint-29-synthetic-clinical-information-inventory.md)
- [Sprint 30 plan](../backlog/sprint-30-synthetic-medication-information.md)
