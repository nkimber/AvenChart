# Decision 0039: Sprint 36 read-only practice-review inbox

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit an authenticated administrator or front-desk staff member with the existing patient-demographics view permission, healthcare-operations purpose, and configured facility context to read a private practice/facility-scoped inbox of the immutable `PendingPracticeReview` work items created under Decision 0038.

The inbox is operational awareness only. It does not authorize assignment, priority, a response-time promise, accept/decline, a clinical decision, patient contact, a telehealth request, a patient or clinician care queue, queue position, appointment, encounter, care, prescribing, billing, claim, integration, or external action.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, authenticated, administrator/front-desk role restricted, healthcare-operations purpose bound, `patients.demo.view` permission gated, and configured practice/facility isolated.
2. The repository selects only `PendingPracticeReview` cases whose applicant remains `SyntheticPracticeReviewSubmitted` and whose immutable case, submission, readiness receipt, promoted portal-disabled unmerged patient shell, purpose, and safety provenance all agree.
3. Results are ordered by submission time and opaque case ID, limited to 100, and use the database clock. A different practice or facility returns no candidate information.
4. The response may contain only opaque case ID, applicant version/status, legal first and last name, birth date, masked email and phone, residence state and postal code, controlled `migraine` or `sleep` purpose, passing safety outcome, the three allowed server review routes, five stable coarse section keys/states/routes, submission time, and explicit limitations/capability flags.
5. No raw email or phone, access key/hash, patient ID, applicant ID, source receipt ID/fingerprint, payer/member/group data, detailed clinical value, medication/allergy/history choice, device value, narrative, free text, clinician identity, possible-match identity, canonical identifier, or financial value may cross the route.
6. The endpoint is GET-only, private/no-store, PHI-audited with an opaque queue resource, and has no idempotency or mutation contract.
7. Every item returns `staffReviewWorkItemExists=true`; `staffActionTaken=false`, `assigned=false`, `priorityAssigned=false`, `practiceAccepted=false`, `practiceDeclined=false`, `patientContacted=false`, `clinicianReviewCreated=false`, `telehealthRequestCreated=false`, `patientCareQueueEntered=false`, `clinicianQueueEntered=false`, `appointmentCreated=false`, `encounterCreated=false`, `careAuthorized=false`, `prescribingEnabled=false`, `billingEnabled=false`, `claimCreated=false`, `integrationEnabled=false`, and `externalCallPerformed=false`.
8. Repeated reads and refreshes create only the existing PHI access-audit evidence; they do not mutate applicant, case, submission, source, patient, clinical, insurance, financial, request, queue, appointment, encounter, integration, or external-action state.
9. The UI provides independent loading, empty, failure/retry, auto-refresh, manual refresh, stable item identity, 320-pixel reflow, keyboard operation, and explicit no-action/no-queue language without browser persistence.
10. Unit, API minimization, authorization, live PostgreSQL zero-mutation, audit, accessibility/recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–35.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX`, version 1. |
| Endpoint | GET `/api/telehealth/v1/admin/applicant-practice-review`; no write counterpart. |
| Entry state | Immutable `PendingPracticeReview` case plus `SyntheticPracticeReviewSubmitted` applicant. |
| Ordering | `submitted_at`, then opaque `practice_review_case_id`; maximum 100. |
| Identity | Legal name and birth date plus masked email/phone and coarse residence region; no patient/applicant identifier. |
| Intake context | Controlled purpose, passing safety outcome, five coarse receipt sections, and server-owned review route only. |
| Capability | Read-only operational awareness; every action and downstream capability false. |

## 4. Explicit exclusions

This decision does not authorize real people or PHI; source-detail or clinical-detail disclosure; patient-chart navigation; identity or coverage assurance; clinician disclosure; assignment; priority; SLA or wait estimate; accept/decline; staff notes; patient communication; request creation; patient or clinician care queue entry; appointment; encounter; consent; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 5. Stop conditions and rollback

Stop if cross-practice/facility or non-operations access succeeds; if raw contact, identifiers, source details, clinical details, payer/member values, free text, clinician identity, priority, assignment, decision controls, queue semantics, or response-time promises appear; if a read mutates durable product state beyond normal PHI access auditing; or if any earlier safeguard regresses. Rollback removes the read route and UI panel; immutable Sprint 35 case/submission evidence remains untouched.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to a disabled synthetic, read-only staff inbox for previously created practice-review work items, with no review decision or downstream consequence.

## References

- [Practice configuration and queue operations specification](../07-practice-configuration-and-queue-operations.md)
- [Security, privacy, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Decision 0038](0038-approved-sprint-35-synthetic-practice-review-submission.md)
- [Sprint 36 plan](../backlog/sprint-36-read-only-practice-review-inbox.md)
