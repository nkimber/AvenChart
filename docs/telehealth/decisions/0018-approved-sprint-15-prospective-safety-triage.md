# Decision 0018: Sprint 15 synthetic prospective-applicant safety triage

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a contact-verified, staff-reviewed, no-candidate synthetic prospective applicant to submit one emergency-first universal safety screen. The server evaluates all answers with the existing versioned deterministic protocol and advances the applicant to exactly one prospective terminal safety state: `SafetyScreenPassed`, `SafetyClinicalReviewRequired`, `SafetyInPersonRequired`, or `SafetyEmergencyRedirect`.

`SafetyScreenPassed` means only that this synthetic applicant may continue to a later separately authorized prospective-intake step. It is not complete clinical triage, telehealth eligibility, a diagnosis, practice acceptance, a promise of care, or queue authorization.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, practice/facility/host scoped, access-key protected, private/no-store, and unavailable to `ManualReviewRequired` or any applicant not in `IdentityReviewApproved`.
2. Emergency screening occurs before complaint, insurance, payment, patient promotion, request creation, or queueing. The page retains immediate 911/emergency-department direction independent of form submission.
3. Current physical location is explicitly confirmed and limited to the configured Georgia, California, or Florida synthetic service area. Residence is not silently treated as current location.
4. Every safety question is explicitly answered yes or no. Missing/unknown values cannot deserialize or normalize to a passing answer. `Unsure=true` fails closed to clinical review.
5. The existing immutable protocol priority remains `Emergency > UrgentInPerson > InPersonRequired > ClinicalReview > TelehealthEligible`; the final value is represented to the applicant as a bounded safety disposition, never as permission to receive care.
6. The repository rebinds practice, facility, applicant access hash, current status/version, no-candidate review decision, and protocol provenance in one transaction. A reason-free deterministic result cannot be overridden by the applicant or staff.
7. Exactly one append-only evaluation and one append-only applicant event are recorded. Exact retry converges; changed-key reuse, stale writers, a second evaluation, and concurrent first writers fail closed with one winner.
8. Emergency, urgent/in-person, clinical-review, and pass responses use fixed non-diagnostic directions and state that no clinician has reviewed the answers.
9. Every identity-proofing, patient/chart/linkage, portal, complete-intake, consent, coverage, request, queue, appointment, encounter, care, prescribing, billing, claim, communication, notification, integration, and external-call capability remains false.
10. Unit, API, authorization/access-key, live PostgreSQL constraints/contention/no-delta, public-response minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–14.

## 3. Explicit exclusions

This decision does not authorize identity proofing, possible-match resolution, patient promotion/linkage, complaint or free-text collection, diagnosis, complete clinical triage, insurance or network verification, consent, practice acceptance, appointment/request creation, queue entry, clinician review, medical advice beyond fixed emergency/in-person direction, care, prescribing, billing, claims, external integration, production enablement, real people, or real PHI.

The future prospective-intake and promotion boundaries remain separate and atomic. A passing universal safety screen cannot substitute for identity, condition-specific clinical, consent, coverage/financial, operational, licensure, or clinician-availability gates.

## 4. Stop conditions and rollback

Stop if a missing answer can pass; emergency loses precedence; an unreviewed/manual-review/cross-practice applicant can submit; a client can choose an outcome; a response implies care eligibility; any canonical/downstream row or external action is created; clinical answers enter browser storage or ordinary logs; or an earlier safeguard regresses. Rollback disables/removes the route and form; additive append-only evidence remains inert and requires a separately reviewed forward migration for schema correction.

## 5. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic universal safety screen. It does not substitute for independent clinical, identity, legal, privacy/security, accessibility, data, operational, interoperability, or production review.

## References

- [Clinical triage and safety](../05-clinical-triage-and-safety.md)
- [Patient onboarding and identity](../04-patient-onboarding-and-identity.md)
- [Decision 0017](0017-approved-sprint-14-synthetic-applicant-identity-review.md)
- [Sprint 15 plan](../backlog/sprint-15-prospective-safety-triage.md)
