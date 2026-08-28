# Decision 0030: Sprint 27 synthetic communication/access readiness

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed the Decision 0029 insurance handoff confirmation to record one immutable communication/access-readiness receipt. The applicant reconfirms the server-owned current-location state and masked callback number, confirms they can communicate safely and privately enough to continue, acknowledges the disconnection/emergency plan, and selects a bounded synthetic spoken-language preference plus interpreter and accessibility-support indicators.

This checkpoint records preferences and acknowledgments only. It does not arrange an interpreter or accommodation, establish technology readiness, update the patient chart, complete intake or consent, accept the patient, create a request/queue entry, authorize care, or contact anyone.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticInsuranceDetailsConfirmed` aggregate with one current insurance-handoff receipt and exact prior provenance.
2. The server rebinds the applicant, promotion, portal-disabled unmerged patient shell, registration receipt, insurance handoff, original passing safety evaluation, and verified callback source on every read/write. Broken or cross-applicant provenance, patient drift, portal enablement, merge state, canonical insurance, or prior receipt drift fails closed.
3. The read projection returns only the prior confirmed state code, a last-four callback mask, an allowlisted language catalog, the policy/version, and explicit limitations. It returns no raw phone, street address, patient identifier, protected insurance value, subscriber identity, proofing evidence, staff rationale, clinical history, or duplicate candidate information.
4. The browser cannot author a location, callback number, free-text support detail, or patient field. It may select only `English` or `Spanish`, two boolean need indicators, and five true affirmations: location, callback, safe/private communication, disconnection/emergency plan, and synthetic-only use.
5. `safeAndPrivateToCommunicateConfirmed` must be true. If it is not, the UI directs the applicant to pause and find a safer/private setting without persisting a successful receipt or implying an emergency assessment.
6. Interpreter/accessibility indicators are preference flags only. `interpreterAssigned=false`, `accessibilityAccommodationArranged=false`, `communicationArrangementCompleted=false`, and `supportRequestCreated=false` remain explicit; no multi-party media or outbound message is enabled.
7. The receipt, `SyntheticInsuranceDetailsConfirmed -> SyntheticCommunicationAccessReadinessRecorded` transition, and applicant event commit in one PostgreSQL transaction. Database constraints and a provenance trigger independently verify the prior chain, bounded values, affirmations, applicant/patient equality, portal-disabled state, and no-consequence flags.
8. Exact retry converges. Changed-key reuse, stale version/fingerprint, expired applicant, missing or altered provenance, patient/portal/canonical-coverage drift, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt and event.
9. No applicant source field, patient, insurance, financial, request, queue, appointment, encounter, clinical, prescribing, billing/claim, communication, integration, or external-call record is created or changed. No complete intake, legal/clinician consent, practice acceptance, technology readiness, or care authorization is inferred.
10. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–26.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_COMMUNICATION_ACCESS_READINESS`, version 1. |
| Entry state | `SyntheticInsuranceDetailsConfirmed`. |
| Server snapshot | Passing safety-evaluation state code; verified callback last-four mask; SHA-256 snapshot fingerprint. |
| Applicant selection | Spoken-language preference `English` or `Spanish`; interpreter requested yes/no; accessibility support requested yes/no. |
| Required affirmations | Location, callback, safe/private communication, disconnection/emergency plan, and synthetic-only use. |
| Resulting status | `SyntheticCommunicationAccessReadinessRecorded`. |
| Data consequence | Immutable applicant receipt only; no applicant source, patient, insurance, support-request, communication, or downstream field is changed. |

## 4. Explicit exclusions

This decision does not authorize real people or data; emergency-contact capture; a clinical safety reassessment; disability diagnosis or free-text accessibility detail; interpreter/accommodation fulfillment; language-specific legal or clinical content; translation; multi-party video; outbound communication; portal access; patient-chart mutation; complete demographics/history/intake; allergy/medication collection; identity assurance; legal or clinician consent; technology readiness; practice acceptance; rendering-physician participation; canonical coverage; financial action; request/queue entry; appointment; encounter; care; prescribing; billing/claim; integration; or production enablement.

## 5. Stop conditions and rollback

Stop if the browser can author location/callback/patient fields or free text; if raw contact/location, patient, insurance, proofing, or candidate data is disclosed; if unsafe/private false can succeed; if an interpreter, accommodation, technology, consent, intake, acceptance, request, queue, or care consequence is represented as complete; if receipt/state/event provenance can diverge; if retry overwrites history; if any applicant source, patient, insurance, communication, financial, or downstream record changes; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable readiness evidence is not deleted as rollback; correction requires a separately reviewed forward workflow.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic communication/access-readiness receipt. It does not substitute for accessibility, interpreter/language-service, privacy/security, patient-registration, legal, clinical, data, operational, interoperability, or production review.

## References

- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Clinical triage and safety specification](../05-clinical-triage-and-safety.md)
- [Actors and journeys specification](../02-actors-and-journeys.md)
- [Decision 0029](0029-approved-sprint-26-synthetic-insurance-handoff-confirmation.md)
- [Sprint 27 plan](../backlog/sprint-27-synthetic-communication-access-readiness.md)
