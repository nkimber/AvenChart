# Decision 0041: Sprint 38 claimant-bound practice-review packet

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the authenticated administrator or front-desk staff member who owns the current unexpired Sprint 37 claim to open one private, read-only operational review packet for that exact synthetic practice-review case.

The packet supplies only the minimum masked registration, synthetic insurance and practice-network evidence, communication/access needs, coarse client-reported device preparation, visit-purpose summary, and non-diagnostic clinical routing status needed to prepare for a later operational decision. It is not a chart, clinical review, acceptance, decline, contact, request, queue, appointment, encounter, care, or financial action.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, authenticated, administrator/front-desk role restricted, healthcare-operations purpose bound, `patients.demo.view` permission gated, and configured practice/facility isolated.
2. The server requires an active unexpired Sprint 37 claim owned by the current actor and revalidates the exact case, submission, readiness, promotion, portal-disabled unmerged patient shell and copied fields, purpose, safety, expiry, source receipts, and zero-downstream provenance for every read.
3. The route contains only the opaque practice-review case ID. Practice, facility, actor, clock, claim ownership, and all source relationships are server derived.
4. The response may include legal name and birth date; masked email, phone, member ID, and group number; residence state and postal code; visit-purpose category/label; `TelehealthEligible` safety outcome; payer/product display labels; subscriber relationship and coverage priority; synthetic eligibility and practice-entity-network outcomes with checked/expiry/current state; the explicit false rendering-clinician-network flag; preferred spoken language; interpreter/accessibility requests; safe/private communication confirmation; coarse browser/camera/microphone/speaker/network-quality preparation; the five readiness sections; and the non-diagnostic clinical summary route.
5. The response must not include applicant, patient, promotion, receipt, trace, or source identifiers; raw email, phone, address, member ID, or group number; access secrets; identity evidence; possible-match information; employer/guardian data; clinical selections, item counts, narratives, free text, medication/allergy/history details; device fingerprints; clinician identity; financial amount; or another staff identity.
6. Claim ID is not returned. Only the claim expiry is exposed. Reading the packet does not renew, extend, release, replace, or otherwise mutate the claim; an expired, absent, foreign-actor, stale, drifted, or out-of-scope packet fails closed without disclosing whether another claimant exists.
7. The packet is private/no-store and PHI-audited with the opaque case ID. It returns safe Problem Details and performs no product-state mutation beyond the existing attributable access audit.
8. Every response preserves `staffReviewWorkItemExists=true`, `staffActionTaken=true`, `assigned=true`, and `assignedToCurrentUser=true`, while `priorityAssigned=false`, `practiceAccepted=false`, `practiceDeclined=false`, `patientContacted=false`, `clinicianReviewCreated=false`, `telehealthRequestCreated=false`, `patientCareQueueEntered=false`, `clinicianQueueEntered=false`, `appointmentCreated=false`, `encounterCreated=false`, `careAuthorized=false`, `prescribingEnabled=false`, `billingEnabled=false`, `claimCreated=false`, `integrationEnabled=false`, and `externalCallPerformed=false`.
9. The UI offers the packet only for a claim held by the current user, provides loading/error/retry/claim-expiry recovery, presents limitations adjacent to synthetic insurance/network and device facts, supports keyboard/focus and 320-pixel reflow, and stores nothing in the browser.
10. Policy, authorization, claimant isolation, minimization, expiry/drift denial, audit, zero-product-state mutation, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–37.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET`, version 1. |
| Endpoint | GET `/api/telehealth/v1/admin/applicant-practice-review/{practiceReviewCaseId}`. |
| Entry state | Exact unexpired `PendingPracticeReview` case with all prior provenance valid and an active Sprint 37 claim owned by the current actor. |
| Visibility | Current claimant only; no source IDs, raw identifiers, another claimant identity, clinical detail, or patient-chart navigation. |
| Mutation | None beyond the existing PHI access audit; the claim lease is not extended. |
| Consequence | Operational preparation only; every decision, communication, request, queue, care, financial, integration, and external capability remains false. |

## 4. Explicit exclusions

This decision does not authorize real people or PHI; patient-chart navigation; raw identity, contact, insurance, or clinical source detail; identity or coverage assurance; rendering-clinician network verification; clinician disclosure; priority; SLA or wait estimate; accept/decline; staff notes; patient communication; correction workflow; request creation; patient or clinician care queue entry; appointment; encounter; consent; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 5. Stop conditions and rollback

Stop if a staff member without the active claim can read a packet; if an expired or foreign claim reveals packet or claimant data; if a raw/source identifier, clinical selection, free text, another claimant identity, or patient-chart link is exposed; if a read changes or extends the claim or any product state other than access audit; if decision, contact, request, queue, care, financial, integration, or external consequences appear; or if any earlier safeguard regresses. Rollback removes the packet route/UI and leaves the Sprint 35 case and Sprint 37 immutable claim evidence untouched.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to a disabled synthetic, current-claimant read of one minimized operational practice-review packet.

## References

- [Actors and journeys specification](../02-actors-and-journeys.md)
- [Practice configuration and queue operations specification](../07-practice-configuration-and-queue-operations.md)
- [Security, privacy, consent, and audit specification](../16-security-privacy-consent-and-audit.md)
- [Decision 0040](0040-approved-sprint-37-synthetic-practice-review-claim.md)
- [Sprint 38 plan](../backlog/sprint-38-claimant-bound-practice-review-packet.md)
