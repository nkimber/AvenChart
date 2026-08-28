# Decision 0019: Sprint 16 synthetic prospective visit-purpose classification

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-27  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of an unexpired synthetic prospective applicant in `SafetyScreenPassed` to classify the visit purpose as exactly `migraine` or `sleep`. The system records one append-only purpose selection and advances the prospective aggregate to `VisitPurposeRecorded`.

This is navigation and intake classification only. It is not a diagnosis, complaint-specific triage, clinical eligibility, medical-director protocol approval, practice acceptance, a request for care, or a promise that the concern can be treated through telehealth.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, branded-host/practice/facility scoped, applicant-access-key protected, private/no-store, and unavailable from every state except `SafetyScreenPassed`.
2. The repository transaction rebinds the current applicant/version, no-candidate staff approval, passing universal safety evaluation and protocol provenance before recording a purpose.
3. The request accepts only an exact controlled category and synthetic confirmation. It accepts no free text, symptom details, diagnosis, medication, insurance, payment, patient ID, clinician choice, or client-selected clinical outcome.
4. `migraine` and `sleep` are demonstration navigation labels, not published clinical protocols. They must not be described as automatically eligible, treatable, diagnosed, or medically approved.
5. Exactly one immutable purpose record and one applicant event are appended. Exact retry converges; changed-content key reuse, stale writers, second submissions, and concurrent first writers fail closed with one winner.
6. Public resume and command responses expose only the category, fixed display label, aggregate state, fixed next action, and explicit limitations. They expose no raw safety answers, fingerprints, review actor/reason, possible candidate, access credential, or canonical identifier.
7. Every identity-proofing, patient/chart/linkage, portal, complete-intake, complaint-specific triage, clinical eligibility, consent, coverage, request, queue, appointment, encounter, care, prescribing, billing, claim, communication, notification, integration, and external-call capability remains false.
8. Unit, API, access-key, live PostgreSQL replay/contention/append-only/no-delta, public minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–15.

## 3. Explicit exclusions

This decision does not authorize free-text or detailed symptom collection; publication or execution of a headache, migraine, insomnia, or sleep protocol; medical-director approval; identity proofing; patient promotion/linkage; insurance/network verification; consent; practice acceptance; request/queue creation; clinician review; diagnosis; advice; care; prescribing; billing; claims; external integration; production enablement; real people; or real PHI.

A separately approved, licensed-clinical-owner protocol and evidence package is required before either category can produce a complaint-specific clinical outcome. The category alone can never substitute for the universal safety screen or any later clinical gate.

## 4. Stop conditions and rollback

Stop if an unsafe/nonpassing applicant can submit; the client can send an arbitrary category or free text; the response implies clinical eligibility; a second selection overwrites history; any downstream row/external action is created; purpose data enters browser persistence or ordinary logs; or an earlier safeguard regresses. Rollback disables/removes the route and form; additive append-only evidence remains inert and requires a separately reviewed forward migration for correction.

## 5. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to this bounded disabled synthetic visit-purpose classification. It does not substitute for licensed clinical-owner, identity, legal, privacy/security, accessibility, data, operational, interoperability, or production review.

## References

- [Clinical triage and safety](../05-clinical-triage-and-safety.md)
- [Decision 0018](0018-approved-sprint-15-prospective-safety-triage.md)
- [Sprint 16 plan](../backlog/sprint-16-prospective-visit-purpose.md)
