# Decision 0035: Sprint 32 synthetic patient-reported health-history topics

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed Decision 0034 allergy/intolerance information to record one immutable health-history-topic receipt. When the prior other-health-history inventory is `ItemsToReview`, the applicant may select zero or more entries from one server-owned local synthetic topic catalog and indicate whether additional or unlisted topics exist. `ItemsToReview` requires at least one selected topic or the additional/unlisted indicator. Prior `PatientReportsNone` and `Unsure` accept no selected topic and remain provisional.

The six topics are prompts for later history collection, not diagnoses, conditions, procedures, observations, pregnancy-status assertions, behavioral-health assessments, substance-use assessments, family-history findings, risk-modifier evaluations, or problem-list entries. The catalog is deliberately incomplete, uses local synthetic keys and broad local categories only, and makes no SNOMED CT, ICD-10-CM, LOINC, FHIR, US Core, or USCDI conformance claim.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticAllergyInformationRecorded` aggregate with exact prior provenance.
2. Every read/write rebinds the applicant, promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access, passing device preparation, clinical-information inventory, medication-information receipt and exact children, allergy-information receipt and exact children, passing safety location, verified callback source, and practice/facility. Broken or cross-applicant provenance, patient drift, portal enablement, merge state, canonical insurance, canonical medication, canonical allergy, canonical problem, or prior-receipt drift fails closed, including exact-replay revalidation inside the transaction.
3. The server owns one effective `LOCAL_SYNTHETIC_ONLY` catalog version containing exactly six opaque topic keys, displays, and local categories: ongoing health conditions (`ConditionOrConcern`), prior surgeries or hospital stays (`ProcedureOrHospitalization`), pregnancy or postpartum information (`HealthStatus`), immune-system or active-cancer-treatment information (`RiskContext`), behavioral-health or substance-use information (`SensitiveHistory`), and family health history (`FamilyHistory`).
4. No client-supplied display, category, diagnosis, condition, procedure, pregnancy status, family relationship, behavioral-health result, substance-use result, clinical status, verification status, severity, onset, abatement, date, code, identifier, note, attachment, or free text is accepted. Selection keys are unique, normalized in server catalog order, and bounded to the catalog size.
5. Each persisted topic retains only its exact server-owned key/display/local category, catalog version, `codingSystem=LOCAL_SYNTHETIC_ONLY`, and `snomedCtMapped=false`, `icd10CmMapped=false`, and `loincMapped=false`.
6. Prior `ItemsToReview` requires a selected topic or `additionalOrUnlistedTopicsReported=true`. Prior `PatientReportsNone` and `Unsure` require zero selections and the additional/unlisted flag false. No branch creates a confirmed no-history assertion.
7. All four acknowledgments are mandatory: patient-reported information may be incomplete; a topic selection is not a diagnosis or clinical finding; no status, timing, severity, or detail is captured; and clinician verification/reconciliation remains required before care.
8. The server derives exactly one informational route: `AdditionalHealthHistoryCollectionRequired`, `ClinicianHealthHistoryReviewRequired`, `AssistedHealthHistoryReviewRequired`, or `PendingClinicianConfirmationOfPatientReportedNone`. It creates no task, priority, recommendation, risk score, triage change, or clinical authority.
9. `conditionCreated=false`, `procedureCreated=false`, `observationCreated=false`, `familyMemberHistoryCreated=false`, `questionnaireResponseCreated=false`, `healthHistoryReconciled=false`, `riskModifierEvaluated=false`, `clinicalTriageChanged=false`, `clinicianReviewCreated=false`, `clinicalIntakeCompleted=false`, `clinicalEligibilityEstablished=false`, `patientRecordChanged=false`, `requestCreated=false`, `queueEntered=false`, `careAuthorized=false`, and `prescribingEnabled=false` remain explicit.
10. The parent receipt, zero or more bounded child topics, `SyntheticAllergyInformationRecorded -> SyntheticHealthHistoryInformationRecorded` transition, and applicant event commit in one PostgreSQL transaction. Database constraints, deferred topic-count/branch checks, provenance guards, and append-only triggers independently enforce the contract.
11. Exact retry converges only after transactional provenance revalidation. Changed-key reuse, stale version/fingerprint, expiration, missing or altered provenance, patient/portal/canonical-data drift, invalid/duplicate/over-limit topic selections, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt/topic set/event.
12. No applicant source field, canonical problem/allergy/medication/prescription/clinical table, patient, insurance, financial, request, queue, appointment, encounter, care, prescribing, billing/claim, integration, alert, or external-call record is created or changed. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–31.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_HEALTH_HISTORY_INFORMATION`, version 1. |
| Entry state | `SyntheticAllergyInformationRecorded`. |
| Server snapshot | Prior inventory other-health-history state, exact medication and allergy receipt/item fingerprints, patient shell, practice/facility, and SHA-256 fingerprint. |
| Catalog | Server-owned local synthetic version 1; six opaque topic keys, displays, and local categories; incomplete and not externally mapped. |
| Topic | Review prompt only; no diagnosis, finding, assessment, status, timing, severity, relationship, or narrative. |
| Additional-topic signal | Boolean; when true, route requires later separately authorized collection. |
| Required acknowledgments | Patient-reported/incomplete; topic is not a diagnosis/finding; no status/timing/detail; clinician verification/reconciliation required. |
| Informational route | One of four bounded routes; no task or authority is created. |
| Resulting status | `SyntheticHealthHistoryInformationRecorded`. |
| Data consequence | Immutable applicant receipt/topics only; no canonical problem, clinical, patient, request, queue, or downstream record changes. |

## 4. Standards alignment and limits

FHIR R4 [Condition](https://hl7.org/fhir/R4/condition.html) is intended for a condition, problem, diagnosis, or health concern that has risen to a level of concern and distinguishes clinical status, verification status, category, severity, code, timing, recorder, and asserter. The current [US Core Condition Problems and Health Concerns Profile](https://hl7.org/fhir/us/core/StructureDefinition-us-core-condition-problems-health-concerns.html) requires a condition code and patient for an interoperable resource and supports status, verification, diagnosis/resolution dates, and provenance. [USCDI v6 Health Status Assessments](https://isp.healthit.gov/uscdi-data-class/health-status-assessments?page=0) separately identifies pregnancy, smoking, functional, disability, mental/cognitive, and health-concern assessment data.

Sprint 32 uses those distinctions only to prevent a broad patient-selected review topic from being misrepresented. It creates no FHIR Condition, Observation, Procedure, FamilyMemberHistory, QuestionnaireResponse, US Core profile instance, USCDI export, problem-list entry, assessment, confirmed negation, external terminology code, or interoperability payload. Real condition/history capture, terminology, status/timing, correction, reconciliation, assessment instruments, sensitive-data policy, and clinician verification remain future governed work.

This checkpoint is state-neutral and does not reduce Georgia, California, or Florida history, assessment, consent, record, or standard-of-care obligations already captured in the controlling telehealth specification and Decision 0034.

## 5. Explicit exclusions

This decision does not authorize real people or PHI; arbitrary topics; diagnoses, conditions, procedures, observations, symptoms, pregnancy status, behavioral-health or substance-use assessment, family relationships/findings, status, verification, severity, onset, abatement, dates, notes, attachments, free text, or external identifiers; canonical problem or health-history records; confirmed no-history assertions; risk-modifier evaluation; clinical triage changes; clinician task assignment; FHIR serialization; completed intake; legal or clinician consent; practice acceptance; request/queue entry; appointment; encounter; communication/video; care; prescribing; pharmacy transmission; billing/claim; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if client-supplied topic display/category/coding or any diagnosis/status/timing/free text crosses the route; if a topic selection is represented as a condition, assessment, problem-list item, confirmed negation, risk modifier, or externally mapped concept; if routing creates a task, triage change, or clinical/operational authority; if any canonical problem, allergy, medication, prescription, patient, request, or downstream row changes; if parent/topic/state/event provenance can diverge; if retry bypasses current provenance; if history is overwritten; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable receipt evidence is not deleted as rollback; correction requires a separately reviewed forward workflow.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic patient-reported health-history-topic receipt with permanently false clinical, assessment, triage, review, intake, eligibility, care, and prescribing consequences. It does not substitute for licensed clinical/medical-director, patient-registration/HIM, privacy/security, sensitive-data, accessibility, data, legal, terminology, interoperability, operations, or production review.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Clinical eligibility, triage, and safety specification](../05-clinical-triage-and-safety.md)
- [Consultation documentation specification](../09-consultation-documentation-and-follow-up.md)
- [Data model and retention specification](../14-data-model-and-retention.md)
- [Decision 0034](0034-approved-sprint-31-synthetic-allergy-information.md)
- [Sprint 32 plan](../backlog/sprint-32-synthetic-health-history-topics.md)
