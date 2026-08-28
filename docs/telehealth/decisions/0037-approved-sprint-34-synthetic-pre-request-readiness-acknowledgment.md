# Decision 0037: Sprint 34 synthetic pre-request readiness acknowledgment

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed Decision 0036 to review a server-derived five-section pre-request readiness projection and record one immutable acknowledgment. The projection reports only that the earlier registration, insurance, communication/access, device-preparation, and clinical-information receipts exist, plus bounded unresolved route codes.

This acknowledgment is a patient-facing checkpoint before any future practice review. It does not declare the applicant ready, complete intake, establish identity or coverage, verify a rendering clinician, create a staff or clinician task, accept the applicant, create a telehealth request, enter a queue, authorize care, or start an integration.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticClinicalInformationSummaryConfirmed` aggregate with exact immutable provenance.
2. Every read and write rebinds the applicant, successful promotion, portal-disabled unmerged patient shell, registration confirmation, insurance handoff, communication/access receipt, passing device-preparation receipt, clinical-information inventory, and clinical-information summary confirmation. Missing or cross-applicant provenance, expiration, patient drift, portal enablement, merge state, canonical insurance/medication/prescription/allergy/problem data, or source drift fails closed, including exact replay inside the transaction.
3. The response contains exactly five stable section keys: `Registration`, `Insurance`, `CommunicationAccess`, `DevicePreparation`, and `ClinicalInformation`. Friendly section labels remain client-owned. No name, birth date, contact, address, language, callback digits, payer/member value, device capability, medication/allergy/history detail, diagnosis, narrative, attachment, or free text crosses the route.
4. Each section exposes only a server-owned coarse receipt state and an unresolved route. The overall route is `AdditionalClinicalInformationRequired` when the prior clinical summary requires more collection; otherwise `AssistedPreRequestSupportRequired` when communication/access assistance or an uncertain clinical branch remains; otherwise `PendingPracticePreRequestReview`.
5. Four acknowledgments are mandatory: the five prior sections were reviewed; unresolved steps remain; no request or queue entry is created; and corrections require a separately authorized workflow.
6. The command accepts no edits, identifiers, values, clinical answers, override, route choice, task choice, practice decision, request content, or free text. Its SHA-256 snapshot binds the exact prior receipt identifiers, fingerprints, bounded support signals, and route codes.
7. `identityAssuranceEstablished=false`, `coverageGuaranteed=false`, `renderingClinicianNetworkVerified=false`, `interpreterOrAccommodationArranged=false`, `technologyReady=false`, `clinicalInformationReconciled=false`, `clinicalIntakeCompleted=false`, `clinicalEligibilityEstablished=false`, `legalConsentEstablished=false`, `staffReviewCreated=false`, `clinicianReviewCreated=false`, `practiceAccepted=false`, `patientRecordChanged=false`, `requestCreated=false`, `queueEntered=false`, `appointmentCreated=false`, `encounterCreated=false`, `careAuthorized=false`, `prescribingEnabled=false`, `billingEnabled=false`, `claimCreated=false`, `integrationEnabled=false`, and `externalCallPerformed=false` remain explicit.
8. The receipt, `SyntheticClinicalInformationSummaryConfirmed -> SyntheticPreRequestReadinessAcknowledged` transition, and applicant event commit in one PostgreSQL transaction. Database constraints, provenance guards, and append-only triggers independently enforce the boundary.
9. Exact retry converges only after transactional provenance revalidation. Changed-key reuse, stale version/fingerprint, a second semantic command, contention, or any current provenance failure is rejected with at most one receipt and event.
10. No source receipt, applicant source field, canonical clinical or patient data, insurance, financial, task, request, queue, appointment, encounter, consent, care, prescribing, billing/claim, integration, or external-call record is created or changed. Unit, live PostgreSQL, API minimization, authorization, accessibility/recovery, migration/bootstrap, runtime, planning, Graphify, and full regression evidence is required without weakening Sprints 1–33.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS`, version 1. |
| Entry state | `SyntheticClinicalInformationSummaryConfirmed`. |
| Server snapshot | Exact five prior receipt identifiers/fingerprints, bounded communication-support signals, clinical summary route, patient shell, practice/facility, and SHA-256 fingerprint. |
| Sections | Five stable keys with server-owned coarse receipt state and unresolved route only. |
| Required acknowledgments | Prior sections reviewed; unresolved steps remain; no request/queue created; corrections require a later governed workflow. |
| Overall route | One of three informational routes; no task, decision, acceptance, request, or authority is created. |
| Resulting status | `SyntheticPreRequestReadinessAcknowledged`. |
| Data consequence | One immutable applicant receipt/event only; every source, clinical, patient, operational, financial, and external consequence remains false. |

## 4. Standards and state boundary

This local checkpoint creates no FHIR `QuestionnaireResponse`, `Task`, `ServiceRequest`, `Appointment`, `CoverageEligibilityRequest`, `CoverageEligibilityResponse`, or `Claim`; no US Core profile; no USCDI export; and no X12 transaction. The receipt is not an interoperability payload, insurer response, practice decision, consent, eligibility result, or request submission.

The checkpoint is state-neutral and does not reduce Georgia, California, or Florida identity, consent, history, examination, record, prescribing, coverage, or standard-of-care obligations already captured in the controlling telehealth specifications.

## 5. Explicit exclusions

This decision does not authorize real people or PHI; patient edits or free text; identity proofing; insurance or network verification; interpreter or accommodation fulfillment; technology readiness; clinical verification or reconciliation; completed intake; clinician disclosure or legal consent; staff or clinician review queues; practice acceptance; telehealth request creation; queue entry; appointment; encounter; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 6. Stop conditions and rollback

Stop if source values or clinical detail cross the route; if a route is client controlled; if the acknowledgment is represented as complete, eligible, accepted, submitted, queued, or ready for care; if any task, request, queue, canonical, financial, or external row changes; if provenance can diverge; if replay bypasses current validation; or if an earlier safeguard regresses. Rollback disables or removes the two routes and panel. Immutable evidence is not deleted as rollback; correction requires a separately governed forward workflow.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic applicant-owned pre-request readiness acknowledgment with permanently false completion, eligibility, acceptance, request, queue, care, financial, integration, and external consequences.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Practice configuration and queue operations specification](../07-practice-configuration-and-queue-operations.md)
- [API, events, and integration contracts](../15-api-events-and-integration-contracts.md)
- [Decision 0036](0036-approved-sprint-33-synthetic-clinical-information-summary-confirmation.md)
- [Sprint 34 plan](../backlog/sprint-34-synthetic-pre-request-readiness-acknowledgment.md)
