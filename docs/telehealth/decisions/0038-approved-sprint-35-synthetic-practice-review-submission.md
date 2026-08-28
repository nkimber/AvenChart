# Decision 0038: Sprint 35 synthetic practice-review submission

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed Decision 0037 to submit one immutable practice-intake review work item. The work item is scoped to the branded practice and facility, carries only references to the prior applicant-owned receipts plus their server-derived route, and gives the practice an operational object for later review.

This is the first authorized operational consequence in the prospective-patient path: `staffReviewCreated=true`. It is not practice acceptance, a telehealth request, a clinical-review task, a patient or clinician queue entry, an appointment, an encounter, consent, coverage, or care authorization. The patient-facing state is `PendingPracticeReview`, never a queue position or a claim that a doctor is being searched.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticPreRequestReadinessAcknowledged` aggregate with exact immutable provenance.
2. Every read and write rebinds the applicant, successful promotion, portal-disabled unmerged patient shell, the Decision 0037 readiness receipt, its exact snapshot fingerprint and route, and the still-valid underlying source chain. Expiry, cross-applicant provenance, patient drift, portal enablement, merge state, source drift, or canonical insurance/medication/prescription/allergy/problem data fails closed, including exact replay inside the transaction.
3. The applicant command contains only expected aggregate version, the server snapshot fingerprint, and four booleans. It accepts no identity, contact, payer, device, clinical or free-text value; no priority, reviewer, practice decision, queue choice, appointment, or care instruction.
4. Four acknowledgments are mandatory: the submitted information remains patient reported; practice review may request more information or decline; submission creates no telehealth request or patient/clinician queue entry; and urgent or worsening symptoms require immediate appropriate care rather than waiting for review.
5. The server derives the review route from the immutable Decision 0037 receipt. The applicant cannot choose or override `AdditionalClinicalInformationRequired`, `AssistedPreRequestSupportRequired`, or `PendingPracticePreRequestReview`.
6. One practice-review case, one applicant submission receipt, one applicant event, and the `SyntheticPreRequestReadinessAcknowledged -> SyntheticPracticeReviewSubmitted` transition commit atomically. The case has no clinical priority, assignment, acceptance, due-time promise, doctor identity, or queue-position semantics.
7. Exact retry converges only after transactional provenance revalidation. Changed-key reuse, stale version/fingerprint, a second semantic command, contention, or any current provenance failure is rejected with at most one case, receipt, and event.
8. `staffReviewCreated=true`; `clinicianReviewCreated=false`, `practiceAccepted=false`, `patientRecordChanged=false`, `telehealthRequestCreated=false`, `patientCareQueueEntered=false`, `clinicianQueueEntered=false`, `appointmentCreated=false`, `encounterCreated=false`, `careAuthorized=false`, `prescribingEnabled=false`, `billingEnabled=false`, `claimCreated=false`, `integrationEnabled=false`, and `externalCallPerformed=false` are stored and returned explicitly.
9. No source receipt, applicant source field, canonical clinical or patient data, canonical insurance, financial record, telehealth request, patient/clinician queue record, appointment, encounter, consent, prescribing, billing/claim, integration, or external-call record is created or changed.
10. Unit, live PostgreSQL, API minimization, authorization, accessibility/recovery, migration/bootstrap, runtime, planning, Graphify, and full regression evidence is required without weakening Sprints 1–34.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION`, version 1. |
| Entry state | `SyntheticPreRequestReadinessAcknowledged`. |
| Server snapshot | Exact readiness acknowledgment ID, fingerprint, route, applicant/patient shell, practice/facility, expiry, and SHA-256 fingerprint. |
| Required acknowledgments | Patient-reported limitation; review may request information or decline; no telehealth request or care queue; worsening-symptom direction. |
| Work item | One immutable `PendingPracticeReview` case with no priority, assignment, acceptance, deadline promise, or care authority. |
| Resulting status | `SyntheticPracticeReviewSubmitted`. |
| Data consequence | One case, one receipt, and one applicant event only; every clinical, patient, financial, queue, integration, and external consequence remains false except the explicit staff-review work item. |

## 4. Standards and state boundary

The local work item is not a FHIR `Task`, `ServiceRequest`, `Appointment`, `QuestionnaireResponse`, `CoverageEligibilityRequest`, `CoverageEligibilityResponse`, or `Claim`; no US Core profile, USCDI export, X12 transaction, payer inquiry, pharmacy message, or external communication is created. A later interoperability decision may define a projection without replacing the internal aggregate.

The submission is state-neutral and does not reduce Georgia, California, or Florida identity, consent, history, examination, record, prescribing, coverage, standard-of-care, emergency-direction, or professional-licensure obligations in the controlling specifications.

## 5. Explicit exclusions

This decision does not authorize real people or PHI; patient edits or free text; identity or coverage assurance; interpreter or accommodation fulfillment; technology readiness; clinical reconciliation or completed intake; clinician disclosure or legal consent; staff review actions; clinician review; practice acceptance or decline; patient contact; telehealth request creation; patient or clinician queue entry; queue position or wait estimate; appointment; encounter; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 6. Stop conditions and rollback

Stop if source values or protected detail cross the route; if the applicant can choose the review route, priority, reviewer, decision, or queue; if the work item is represented as acceptance, a telehealth request, a doctor search, a queue position, an appointment, or care; if any canonical, clinical, financial, patient/clinician queue, integration, or external row changes; if provenance can diverge; if replay bypasses current validation; or if an earlier safeguard regresses. Rollback disables or removes the applicant routes and panel. Immutable case, receipt, and event evidence is not deleted as rollback; a correction or withdrawal requires a separately governed forward workflow.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic applicant-owned practice-review submission with exactly one staff-review work item and permanently false acceptance, telehealth-request, patient/clinician-queue, care, financial, integration, and external consequences.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Practice configuration and queue operations specification](../07-practice-configuration-and-queue-operations.md)
- [API, events, and integration contracts](../15-api-events-and-integration-contracts.md)
- [Decision 0037](0037-approved-sprint-34-synthetic-pre-request-readiness-acknowledgment.md)
- [Sprint 35 plan](../backlog/sprint-35-synthetic-practice-review-submission.md)
