# Decision 0042: Sprint 39 synthetic practice-review authorization

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the authenticated administrator or front-desk staff member who owns the current unexpired Sprint 37 claim and reviewed the Sprint 38 packet to record one immutable, positive-only operational authorization for a separately gated future synthetic telehealth-request creation step.

This is a practice-intake governance decision over synthetic evidence. It is not a clinical eligibility determination, coverage guarantee, practice acceptance, patient contact, telehealth request, patient or clinician queue entry, appointment, encounter, consent, care authorization, prescription, financial action, integration, or external communication.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, authenticated, administrator/front-desk role restricted, healthcare-operations purpose bound, `patients.demo.write` permission gated, and configured practice/facility isolated.
2. The server requires the active unexpired Sprint 37 claim to belong to the current actor and atomically revalidates the exact case, Sprint 38 packet policy version, submission, readiness, promotion, portal-disabled unmerged patient shell and copied fields, source receipts, purpose, safety, expiry, and zero-downstream provenance.
3. The only decision is `AuthorizedForSyntheticRequestCreation`; the only rationale is `OperationalPrerequisitesReviewed`. Denial, deferral, correction, priority, notes, free text, patient contact, and clinical disposition are excluded.
4. Three independent acknowledgments are mandatory: this is not clinical eligibility; synthetic eligibility/practice-entity network evidence is not a coverage guarantee and rendering-physician network remains unchecked; and no request, queue, appointment, encounter, consent, or care authority is created.
5. The command is case-scoped, version checked, semantically idempotent, current-claimant bound, first-writer safe, database-clock constrained, private/no-store, and case-correlated in the PHI audit. Actor, role, staff, practice, facility, applicant, patient, claim, and source relationships are server derived.
6. The transaction advances the prospective applicant from `SyntheticPracticeReviewSubmitted` to `SyntheticPracticeReviewAuthorized`, increments its version once, appends one immutable authorization and one aggregate event, and leaves the immutable submitted case and short-claim history intact.
7. Exact replay by the same actor and command returns the original result. Changed-content replay, stale version, expired/foreign claim, drift, prior authorization, and concurrent duplicate writers fail closed. After success the item leaves the pending inbox because its applicant state is no longer pending.
8. Every authorization records `requestCreationAuthorized=true` while `practiceAccepted=false`, `patientContacted=false`, `clinicianReviewCreated=false`, `telehealthRequestCreated=false`, `patientCareQueueEntered=false`, `clinicianQueueEntered=false`, `appointmentCreated=false`, `encounterCreated=false`, `consentCreated=false`, `careAuthorized=false`, `prescribingEnabled=false`, `billingEnabled=false`, `claimCreated=false`, `integrationEnabled=false`, and `externalCallPerformed=false`.
9. The UI places the authorization form inside the current claimant's open packet, keeps the limitations adjacent, requires every acknowledgment, provides stable retry and post-success refresh, supports keyboard/focus and 320-pixel reflow, and stores nothing in the browser.
10. Migration, policy, authorization, replay/contention, claimant isolation, expiry/drift denial, append-only enforcement, audit, zero-downstream state, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–38.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION`, version 1. |
| Endpoint | POST `/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}/authorization`. |
| Entry state | Exact unexpired pending case and `SyntheticPracticeReviewSubmitted` applicant with a current actor-owned claim and packet policy version 1. |
| Decision | `AuthorizedForSyntheticRequestCreation` with rationale `OperationalPrerequisitesReviewed`; no negative or free-text path. |
| Mutation | One applicant version/status transition plus immutable decision/event evidence; case and claim receipts remain unchanged. |
| Consequence | Authorization for one later separately gated synthetic step only; no request or downstream capability exists. |

## 4. Explicit exclusions

This decision does not authorize real people or PHI; another claimant; patient-chart navigation; raw identity, contact, insurance, or clinical source detail; identity or coverage assurance; rendering-clinician network verification; priority; accept/decline/deferral; correction workflow; notes or free text; patient communication; request creation; patient or clinician care queue entry; queue position; appointment; encounter; consent; media; clinical review; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 5. Stop conditions and rollback

Stop if a non-claimant or expired claimant can authorize; if denial, free text, chart/source detail, or another claimant identity is exposed; if the case or claim receipt changes; if a request, queue, contact, appointment, encounter, consent, care, financial, integration, or external consequence appears; if replay/contention creates more than one authorization; or if any earlier safeguard regresses. Rollback removes the authorization route/UI and forward-disables the new decision path without rewriting immutable evidence.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, current-claimant, positive-only authorization above.

## References

- [Actors and journeys specification](../02-actors-and-journeys.md)
- [Practice configuration and queue operations specification](../07-practice-configuration-and-queue-operations.md)
- [Security, privacy, consent, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Decision 0041](0041-approved-sprint-38-claimant-bound-practice-review-packet.md)
- [Sprint 39 plan](../backlog/sprint-39-synthetic-practice-review-authorization.md)
