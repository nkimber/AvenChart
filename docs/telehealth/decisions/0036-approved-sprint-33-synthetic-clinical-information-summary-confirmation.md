# Decision 0036: Sprint 33 synthetic clinical-information summary confirmation

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed Decision 0035 health-history topics to review a server-derived summary of the already recorded medication, allergy/intolerance, and other-health-history branches and record one immutable no-edit confirmation.

The summary exposes only the three prior coarse inventory states, bounded selected-item counts, additional-or-unlisted signals, and prior informational routes. It does not copy clinical detail, add an answer, resolve uncertainty, establish a confirmed negative, reconcile a list, complete clinical intake, determine eligibility, create a task, or submit a request.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticHealthHistoryInformationRecorded` aggregate with exact prior provenance.
2. Every read/write rebinds the applicant, successful promotion, portal-disabled unmerged patient shell, clinical-information inventory, medication receipt and exact children, allergy receipt and exact children, health-history receipt and exact children, and all retained upstream identifiers. Broken or cross-applicant provenance, patient drift, portal enablement, merge state, canonical insurance, canonical medication, canonical prescription, canonical allergy, canonical problem, or prior-receipt drift fails closed, including exact replay inside the transaction.
3. The response contains no legal name, birth date, contact, address, member identifier, payer detail, medication or allergy catalog display, health-history topic display, diagnosis, symptom, status, timing, narrative, attachment, or free text. It returns only stable category keys, prior coarse states, counts, booleans, prior route codes, policy/version, snapshot fingerprint, direction, limitations, and hard-false consequence flags. Friendly category labels are client-owned presentation text and are not copied into the wire contract or receipt.
4. The server derives exactly one summary route. Any additional/unlisted signal yields `AdditionalClinicalInformationCollectionRequired`; otherwise any `Unsure` inventory branch yields `AssistedClinicalInformationReviewRequired`; otherwise any `ItemsToReview` branch yields `ClinicianClinicalInformationReviewRequired`; otherwise the result is `PendingClinicianReconciliationOfPatientReportedNone`.
5. All four acknowledgments are mandatory: the summary is patient reported and may be incomplete; it contains no clinical verification or reconciliation; confirmation does not establish intake completion or eligibility; and changes or omissions must be handled through a separately authorized workflow.
6. The command accepts no clinical content, edits, overrides, route choice, correction narrative, or externally supplied count. The server snapshot binds the exact source receipt identifiers, fingerprints, category states, counts, additional signals, and routes.
7. `questionnaireResponseCreated=false`, `medicationListReconciled=false`, `allergyListReconciled=false`, `healthHistoryReconciled=false`, `confirmedNegativeEstablished=false`, `clinicianReviewCreated=false`, `clinicalIntakeCompleted=false`, `clinicalEligibilityEstablished=false`, `clinicalTriageChanged=false`, `patientRecordChanged=false`, `practiceAccepted=false`, `requestCreated=false`, `queueEntered=false`, `careAuthorized=false`, and `prescribingEnabled=false` remain explicit.
8. The summary receipt, `SyntheticHealthHistoryInformationRecorded -> SyntheticClinicalInformationSummaryConfirmed` transition, and applicant event commit in one PostgreSQL transaction. Database constraints, source-provenance guards, and append-only triggers independently enforce the contract.
9. Exact retry converges only after transactional provenance revalidation. Changed-key reuse, stale version/fingerprint, expiration, missing or altered source evidence, patient or canonical-data drift, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt and event.
10. No applicant source field, prior receipt, canonical clinical table, patient, insurance, financial, request, queue, appointment, encounter, consent, care, prescribing, billing/claim, integration, or external-call record is created or changed. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–32.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY`, version 1. |
| Entry state | `SyntheticHealthHistoryInformationRecorded`. |
| Server snapshot | Exact inventory, medication, allergy, and health-history receipt identifiers/fingerprints/states/counts/signals/routes plus patient shell and practice/facility provenance, hashed with SHA-256. |
| Summary categories | `Medications`, `AllergiesOrIntolerances`, and `OtherHealthHistory`; each is server derived and exposes only prior coarse state, count, additional signal, and route. |
| Required acknowledgments | Patient-reported/incomplete; not verified or reconciled; not intake completion or eligibility; corrections require a later governed workflow. |
| Informational route | One of four bounded routes; no task, priority, assessment, or authority is created. |
| Resulting status | `SyntheticClinicalInformationSummaryConfirmed`. |
| Data consequence | Immutable applicant summary receipt only; no source, canonical clinical, patient, request, queue, or downstream record changes. |

## 4. Standards alignment and limits

FHIR R5 [QuestionnaireResponse](https://hl7.org/fhir/R5/questionnaireresponse.html) represents a complete or partial structured set of answers linked to a questionnaire and distinguishes the subject, source, status, and authored context. FHIR R5 [Provenance](https://hl7.org/fhir/R5/provenance.html) records the agents, entities, targets, and activities needed to assess authenticity and reproducibility. USCDI defines standardized health-data classes for interoperable exchange.

Sprint 33 uses only the provenance and patient-source distinctions as prevention boundaries. It creates no FHIR resource, QuestionnaireResponse, Provenance resource, US Core profile instance, USCDI export, canonical list, confirmed negative, clinical attestation, or interoperability payload. The local receipt is not a substitute for a governed clinical form, clinician reconciliation, correction/amendment workflow, terminology service, or exchange implementation.

This checkpoint is state-neutral and does not reduce Georgia, California, or Florida history, consent, record, examination, or standard-of-care obligations already captured in the controlling telehealth specification and Decision 0035.

## 5. Explicit exclusions

This decision does not authorize real people or PHI; new clinical answers or details; patient-entered edits or free text; diagnoses, symptoms, statuses, timing, severity, medication dose/directions, allergy reaction/criticality, health-history findings, confirmed negatives, canonical clinical records, reconciliation, correction, amendment, assessment, risk evaluation, triage change, clinical or administrative task creation, completed intake, legal or clinician consent, practice acceptance, request/queue entry, appointment, encounter, communication/video, care, prescribing, pharmacy transmission, billing/claim, FHIR serialization, integration, or production enablement.

## 6. Stop conditions and rollback

Stop if the summary accepts or exposes clinical detail; if a count or route is client controlled; if confirmation is represented as verified, reconciled, complete, eligible, accepted, or ready for care; if it creates a task, request, queue entry, or canonical record; if prior provenance can diverge; if replay bypasses current provenance; if a source record is overwritten; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable receipt evidence is not deleted as rollback; correction requires a separately reviewed forward workflow.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic applicant-owned no-edit summary confirmation with permanently false clinical, reconciliation, intake, eligibility, acceptance, request, queue, care, and prescribing consequences. It does not substitute for licensed clinical/medical-director, patient-registration/HIM, privacy/security, accessibility, data, legal, terminology, interoperability, operations, or production review.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Clinical eligibility, triage, and safety specification](../05-clinical-triage-and-safety.md)
- [Data model and retention specification](../14-data-model-and-retention.md)
- [Security, privacy, consent, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Decision 0035](0035-approved-sprint-32-synthetic-health-history-topics.md)
- [Sprint 33 plan](../backlog/sprint-33-synthetic-clinical-information-summary-confirmation.md)
