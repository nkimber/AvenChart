# Decision 0017: Sprint 14 synthetic prospective-applicant identity review

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit an authorized practice administrator to review a contact-verified synthetic prospective applicant and append exactly one bounded identity-review decision. `ApprovedForProspectiveIntake` is available only when the deterministic duplicate disposition is `NoCandidate`; `ManualReviewRequired` is available only when it is `PossibleMatchManualReview`.

The decision records staff review of contact-control and duplicate-classification evidence only. It is not identity proofing, an NIST identity-assurance level, patient matching, patient creation, chart linkage, portal enrollment, coverage verification, clinical triage, practice acceptance, appointment/request creation, or queue authorization.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, practice/facility scoped, staff-session protected, role and access-permission gated, private/no-store, and PHI audited.
2. The server rebinds the applicant, practice, facility, authenticated administrative principal, optional staff binding, current status/version, contact-verification evidence, and duplicate disposition in one database transaction before recording a decision. A front-desk principal must have a staff binding; the existing dedicated administrator account remains traceable by authenticated actor ID and PHI audit even when it is not a clinical staff record.
3. The review queue exposes only the minimum bounded applicant attributes needed for this synthetic decision and never exposes a possible matching patient's identity, chart, candidate identifier, or comparison details.
4. The decision is deterministic: `NoCandidate` permits only `ApprovedForProspectiveIntake`; `PossibleMatchManualReview` permits only `ManualReviewRequired`. No override, candidate selection, merge, link, or patient creation exists.
5. A reason, expected applicant version, semantic idempotency key, explicit synthetic-data confirmation, fixed policy/evidence provenance, authenticated administrative actor/role, staff binding when present, and database time are required.
6. The applicant aggregate advances to `IdentityReviewApproved` or `ManualReviewRequired`; one append-only decision and one append-only event are recorded. Prior contact and duplicate evidence remains immutable.
7. Exact retry converges on the same decision. Changed-key reuse, stale writers, conflicting outcomes, and a second decision fail closed; concurrent first decisions produce one winner.
8. Responses state that contact control is not identity proofing and return false for identity proofed, canonical patient created, chart linked, prospective intake completed, request created, and queue enabled.
9. The operation creates no patient, chart, portal account, coverage/eligibility, consent, intake, request, queue, encounter, appointment, claim, prescription, task, message, notification, integration, or external call.
10. Unit, API, authorization, live PostgreSQL constraints/contention/no-delta, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–13.

## 3. Explicit exclusions

This decision does not authorize real identity proofing, document or biometric collection, knowledge-based verification, trusted-referee processing, possible-match disclosure, patient matching/linking/merging, canonical patient creation, portal credentials, insurance-network confirmation, clinical triage, consent, request creation, queue entry, care, billing, claim submission, production enablement, real people, or real PHI.

The future promotion boundary remains atomic: all required identity, duplicate, clinical, consent, coverage/financial, and operational acceptance gates must pass before a canonical patient is created or linked and any request is reassociated or queued.

## 4. Stop conditions and rollback

Stop if an unauthorized or cross-facility actor can read or decide; any possible matching patient is exposed; staff can override the deterministic outcome; a decision is represented as identity proofing; any canonical/downstream row or external action is created; clinical content enters browser storage or ordinary logs; or an earlier safeguard regresses. Rollback disables/removes the routes and panel; additive append-only evidence remains inert and requires a separately reviewed forward migration for schema correction.

## 5. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic identity-review decision. It does not substitute for independent identity, clinical, legal, privacy/security, accessibility, data, operational, interoperability, or production review.

## References

- [Patient onboarding and identity](../04-patient-onboarding-and-identity.md)
- [NIST SP 800-63A-4, Identity Proofing](https://pages.nist.gov/800-63-4/sp800-63a.html)
- [Decision 0007](0007-approved-sprint-04-prospective-patient-identity-shell.md)
- [Sprint 14 plan](../backlog/sprint-14-synthetic-applicant-identity-review.md)
