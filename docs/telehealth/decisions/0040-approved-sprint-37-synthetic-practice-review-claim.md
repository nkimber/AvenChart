# Decision 0040: Sprint 37 synthetic practice-review claim

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit an authenticated administrator or front-desk staff member with healthcare-operations purpose, the existing patient-demographics write permission, and configured facility context to claim one exact pending synthetic practice-review work item for a short server-timed review lease.

The claim prevents simultaneous staff work only. It does not authorize priority, a response-time promise, staff disposition, clinical review, accept/decline, patient contact, a telehealth request, a patient or clinician care queue, queue position, appointment, encounter, care, prescribing, billing, claim, integration, or external action.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, authenticated, administrator/front-desk role restricted, healthcare-operations purpose bound, `patients.demo.write` permission gated, and configured practice/facility isolated.
2. The server revalidates the exact pending case, submission, readiness, promotion, portal-disabled unmerged patient shell and all copied fields, purpose, safety, expiry, and zero-downstream provenance in the claiming transaction.
3. The command contains only the opaque case ID in the route, expected applicant version, inbox policy version, and three independent no-decision/no-contact/no-request-or-care-queue acknowledgments. The staff identity, practice, facility, clock, lease length, and expiry are server derived.
4. The database locks the exact case before testing active claims. One first writer creates an immutable claim receipt; concurrent or subsequent different claims conflict while the lease is active.
5. Exact semantic replay by the same staff identity and idempotency key returns the original receipt. Reusing a key with different content conflicts. An expired lease may be followed by a new immutable claim receipt; history is never overwritten.
6. Lease duration is 120 seconds. The inbox derives active assignment using the database clock and exposes only `assigned`, `assignedToCurrentUser`, and `assignmentExpiresAt`; another staff member's identity is never returned.
7. Claiming does not reorder the inbox, assign priority, promise response time, or change the applicant, case, patient, request, queue, appointment, encounter, clinical, insurance, financial, integration, or external-action state.
8. Every active claim returns `staffReviewWorkItemExists=true`, `staffActionTaken=true`, `assigned=true`, while `priorityAssigned=false`, `practiceAccepted=false`, `practiceDeclined=false`, `patientContacted=false`, `clinicianReviewCreated=false`, `telehealthRequestCreated=false`, `patientCareQueueEntered=false`, `clinicianQueueEntered=false`, `appointmentCreated=false`, `encounterCreated=false`, `careAuthorized=false`, `prescribingEnabled=false`, `billingEnabled=false`, `claimCreated=false`, `integrationEnabled=false`, and `externalCallPerformed=false`.
9. The endpoint is private/no-store, PHI-audited with the opaque case ID, and requires a semantic idempotency key. It returns safe Problem Details for stale, expired, drifted, absent, unauthorized, and actively claimed cases.
10. The UI provides a single explicit claim action, stable pending/retry/ambiguous-result recovery, active-lease status, another-reviewer privacy, keyboard operation, 320-pixel reflow, and no browser persistence.
11. Migration, policy, authorization, minimization, contention, lease expiry, zero-product-state-mutation outside immutable claim evidence, audit, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–36.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM`, version 1. |
| Endpoint | POST `/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}/claim`. |
| Entry state | Exact unexpired `PendingPracticeReview` case with no active claim and all Sprint 36 provenance valid. |
| Lease | 120 seconds from the database clock; first writer wins; immutable historical receipts. |
| Visibility | The current staff member sees whether the active claim is theirs; all other staff identity remains private. |
| Consequence | Operational duplicate-work prevention only; every decision, request, queue, care, financial, integration, and external capability remains false. |

## 4. Explicit exclusions

This decision does not authorize real people or PHI; source-detail or clinical-detail disclosure; patient-chart navigation; identity or coverage assurance; clinician disclosure; priority; SLA or wait estimate; accept/decline; staff notes; patient communication; request creation; patient or clinician care queue entry; appointment; encounter; consent; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 5. Stop conditions and rollback

Stop if two active claims exist for one case; if a different staff identity receives an active claimant identity or can replay another claimant's receipt; if stale, expired, drifted, out-of-scope, or non-pending provenance can be claimed; if priority, disposition, communication, request, queue, care, financial, integration, or external consequences appear; or if any earlier safeguard regresses. Rollback removes the claim route/UI and leaves immutable claim evidence inert; Sprint 35 case/submission evidence remains untouched.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to a disabled synthetic, time-limited staff claim over a previously created pending practice-review work item.

## References

- [Practice configuration and queue operations specification](../07-practice-configuration-and-queue-operations.md)
- [Security, privacy, consent, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Decision 0039](0039-approved-sprint-36-read-only-practice-review-inbox.md)
- [Sprint 37 plan](../backlog/sprint-37-synthetic-practice-review-claim.md)
