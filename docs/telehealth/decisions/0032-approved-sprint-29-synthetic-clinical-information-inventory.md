# Decision 0032: Sprint 29 synthetic clinical-information inventory

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed Decision 0031 device preparation to record one immutable, coarse inventory of whether medications, allergies/intolerances, and other health-history items still need detailed collection or assisted review. Each category accepts only `PatientReportsNone`, `ItemsToReview`, or `Unsure`. The server derives a routing label but creates no work item and confers no clinical authority.

This checkpoint is deliberately not a medication statement, allergy/intolerance assertion, condition/problem record, clinical history, reconciliation, triage result, intake completion, practice acceptance, or request for care. `PatientReportsNone` is a provisional patient report only and must not be represented as “no known” clinical evidence.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticDevicePreparationRecorded` aggregate with exact prior provenance.
2. Every read/write rebinds the applicant, promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access receipt, passing device-preparation receipt, original passing safety location, and verified callback source. Broken or cross-applicant provenance, patient drift, portal enablement, merge state, canonical insurance, or prior receipt drift fails closed.
3. The command accepts exactly three category states: `PatientReportsNone`, `ItemsToReview`, or `Unsure` for medications, allergies/intolerances, and other health history. It accepts no medication, substance, reaction, diagnosis, symptom, procedure, dose, identifier, date, narrative, attachment, or free text.
4. All three acknowledgments are mandatory: the inventory is patient-reported and may be incomplete; no detailed clinical information is captured here; and clinician reconciliation remains required before care or prescribing.
5. The server derives exactly one informational route: `DetailedCollectionRequired` if any category is `ItemsToReview`; otherwise `AssistedReviewRequired` if any category is `Unsure`; otherwise `PendingClinicianReconciliation`. The route does not create a reviewer task, priority, denial, clinical recommendation, or eligibility result.
6. `medicationListReconciled=false`, `allergyListReconciled=false`, `healthHistoryReconciled=false`, `clinicalIntakeCompleted=false`, `clinicalEligibilityEstablished=false`, `clinicianReviewCreated=false`, `patientRecordChanged=false`, `requestCreated=false`, `queueEntered=false`, `careAuthorized=false`, and `prescribingEnabled=false` remain explicit.
7. The receipt, `SyntheticDevicePreparationRecorded -> SyntheticClinicalInformationInventoryRecorded` transition, and applicant event commit in one PostgreSQL transaction. Database constraints and a provenance trigger independently verify the complete prior chain, bounded values, acknowledgments, applicant/patient equality, portal-disabled state, and no-consequence flags.
8. Exact retry converges. Changed-key reuse, stale version/fingerprint, expired applicant, missing or altered provenance, patient/portal/canonical-insurance drift, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt and event.
9. No applicant source field, canonical medication/allergy/problem/history table, patient, insurance, support, communication, financial, request, queue, appointment, encounter, clinical, prescribing, billing/claim, integration, or external-call record is created or changed.
10. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–28.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY`, version 1. |
| Entry state | `SyntheticDevicePreparationRecorded`. |
| Server snapshot | Prior device-preparation and communication/access receipts, passing location, verified callback source, practice/facility, patient shell, and SHA-256 fingerprint. |
| Category values | `PatientReportsNone`, `ItemsToReview`, or `Unsure` for medications, allergies/intolerances, and other health history. |
| Required acknowledgments | Patient-reported/incomplete; no detailed information captured; clinician reconciliation required. |
| Informational route | `DetailedCollectionRequired`, `AssistedReviewRequired`, or `PendingClinicianReconciliation`; no task or authority is created. |
| Resulting status | `SyntheticClinicalInformationInventoryRecorded`. |
| Data consequence | Immutable applicant receipt only; no canonical clinical, patient, request, queue, or downstream record is changed. |

## 4. Standards alignment and limits

The [USCDI Version 6](https://www.healthit.gov/isp/sites/isp/files/2025-07/USCDI-Version-6-July-2025.pdf) separates medications, allergies/intolerances, and problems/health concerns as distinct clinical data classes. HL7 FHIR R4 likewise separates [MedicationStatement](https://hl7.org/fhir/R4/medicationstatement.html), [AllergyIntolerance](https://hl7.org/fhir/R4/allergyintolerance.html), and [Condition](https://hl7.org/fhir/R4/condition.html), identifies a patient as a possible medication-information source, and distinguishes allergy information that was not asked/reviewed from a clinically meaningful “none identified” assertion.

Sprint 29 uses those distinctions only to avoid collapsing three safety-relevant categories or upgrading a coarse patient report into canonical evidence. It creates no FHIR resource, USCDI export, vocabulary-coded clinical statement, medication reconciliation, allergy verification, diagnosis, or interoperability payload.

## 5. Explicit exclusions

This decision does not authorize real people or PHI; medication/substance/condition names; dosage or reaction details; symptoms or diagnoses; clinical advice; condition-specific triage; emergency or in-person disposition; clinician review assignment; medication/allergy/history reconciliation; canonical chart mutation; FHIR serialization; complete intake; legal or clinician consent; practice acceptance; request/queue entry; appointment; encounter; communication/video; care; prescribing; pharmacy transmission; billing/claim; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if a detailed clinical fact or free text can cross the route; if `PatientReportsNone` is represented as a verified “no known” assertion; if routing creates a task or clinical/operational authority; if a canonical medication, allergy, problem, history, patient, request, or downstream row changes; if receipt/state/event provenance can diverge; if retry overwrites history; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable inventory evidence is not deleted as rollback; correction requires a separately reviewed forward workflow.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic coarse clinical-information inventory with permanently false reconciliation, intake, eligibility, care, and prescribing consequences. It does not substitute for licensed clinical/medical-director, patient-registration/HIM, medication-safety, allergy-safety, privacy/security, accessibility, data, legal, interoperability, operations, or production review.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Clinical eligibility, triage, and safety specification](../05-clinical-triage-and-safety.md)
- [Data model and retention specification](../14-data-model-and-retention.md)
- [Decision 0031](0031-approved-sprint-28-synthetic-device-preparation.md)
- [Sprint 29 plan](../backlog/sprint-29-synthetic-clinical-information-inventory.md)
