# Decision 0046: Sprint 43 applicant request complaint triage

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired prospective applicant whose source-bound request is exactly `SafetyScreening` version 3 after a passing Sprint 42 universal assessment to answer one fixed complaint-specific synthetic question set for the server-owned `migraine` or `sleep` category. A deterministic evaluator records all fired rules in priority order and advances the request exactly once to version 4.

The possible synthetic outcomes are `Emergency`, `UrgentInPerson`, `InPersonRequired`, `ClinicalReview`, `TelehealthEligible`, and `Unsupported`. They map respectively to `EmergencyRedirected`, `InPersonRecommended`, `InPersonRecommended`, `ClinicalReview`, `Intake`, and `Unsupported`. `Intake` means only that this non-production fixture can demonstrate the next workflow state; it is not medical approval, practice acceptance, care authorization, diagnosis, or a guarantee that video care is appropriate.

## 2. Clinical-publication boundary

The migraine and sleep fixtures are `UNAPPROVED_SYNTHETIC` engineering content derived only from the candidate concepts already documented in the triage specification. They are not a published clinical protocol. Their identifiers, hashes, engine version, typed answers, ordered fired rules, reason codes, and source evidence are retained solely for deterministic test replay.

Production publication remains fail-closed until a separate governance decision names a licensed medical director, records their approval of the exact protocol checksum and patient-facing content package, links an approved machine-readable golden-case pack and independent under-triage review, supplies effective/review/retirement dates, and authorizes the exact production scope. Program-owner approval of this engineering slice does not substitute for clinical approval.

Official evidence sources are navigation inputs for a future clinical owner, not approval of these rules: the [ACR Headache Appropriateness Criteria](https://acsearch.acr.org/docs/69482/Narrative) describes headache red flags, and the [American Academy of Sleep Medicine practice-guideline catalog](https://aasm.org/clinical-resources/practice-standards/practice-guidelines/) identifies clinician-developed insomnia and sleep-disorder guidance. A licensed clinical owner must select, interpret, validate, and approve the production rule set.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and configured practice/facility isolated.
2. The server rebinds the unchanged `SyntheticRequestCreated` version 26 applicant, portal-disabled unmerged patient shell, request creation, current location/callback, passing request universal assessment, server-owned complaint category, prior prospective evidence, and zero-downstream state.
3. The command accepts only expected request version, opaque server snapshot, exact supported state, current-location/callback/synthetic confirmations, and the exact category-specific coded answer set. It accepts no patient identifier, category override, complaint narrative, diagnosis, priority, note, clinician instruction, outcome, rule, reason, or override.
4. Each answer is exactly `Yes`, `No`, or `NotSure`. Missing or malformed answers fail validation; `NotSure`, inconsistent eligibility assertions, or an unrecognized category route to review or fail closed and never default to eligible.
5. Evaluation is deterministic and priority ordered. Every fired rule is stored in evaluation order; the first highest-severity rule determines the outcome. The same engine, protocol checksum, and answer checksum must replay bit-for-bit.
6. The migraine fixture evaluates synthetic sudden/worst onset and neurologic/vision warning signals before urgent physical-evaluation signals, higher-risk review signals, and the known-similar-pattern candidate. The sleep fixture evaluates self-harm warning signals before urgent behavioral/withdrawal/dangerous-somnolence signals, breathing-disorder physical-evaluation signals, higher-risk review signals, controlled-sedative unsupported scope, and the uncomplicated-sleep candidate.
7. Patient-facing output exposes only the complaint category, protocol/governance status, outcome, public disposition, safe direction, timestamps, and consequence flags. It does not return submitted answers, answer checksum, fired-rule evidence, or reason-code detail.
8. The request-owned location and universal-safety evidence must remain exact, source-matched, supported, and fresh. Changed, stale, conflicting, or superseded evidence fails closed.
9. The transaction is request-version checked, snapshot-bound, semantically idempotent, first-writer safe, database-clock constrained, private/no-store, and applicant-correlated.
10. The transaction inserts or verifies one exact synthetic protocol fixture, appends one generic assessment and one protected complaint-assessment receipt, advances only the request from version 3 to version 4, and appends one request event. Earlier evidence remains immutable.
11. Exact replay returns the original projection. Changed-content reuse, another command after success, stale version, expired or foreign access, category/source/patient/protocol drift, and concurrent duplicate writers fail closed.
12. The UI provides immediate 911/988 direction where relevant, explicit `Yes`/`No`/`Not sure` controls with no defaults, stable retry, focus recovery, category-specific questions, honest outcome text, 320-pixel reflow, and no answer/result persistence in browser storage.
13. No clinical-review work item, contact, doctor search, care-queue entry, queue position, appointment, encounter, consent, media, care, prescribing, financial action, integration, or external communication is created.
14. Migration, state-machine, policy, deterministic/golden fixture, replay/contention, access isolation, expiry/stale/source/protocol drift denial, immutable evidence, outcome priority, publication-gate, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–42.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE`, version 1. |
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/complaint-triage`. |
| Entry state | Exact unexpired `SyntheticRequestCreated` applicant and source-linked request at `SafetyScreening` version 3 with a passing immutable Sprint 42 universal assessment. |
| Input | Expected request version, context snapshot, exact supported state, location/callback/synthetic confirmations, and exactly one matching migraine or sleep coded answer object. |
| Mutation | One exact synthetic protocol fixture if absent, one immutable generic assessment, one immutable complaint-assessment receipt containing ordered rule evidence, one request transition, and one request event. |
| Pass consequence | Request is `Intake` version 4 for synthetic workflow demonstration only; no intake snapshot, practice acceptance, or care authority exists. |
| Protective consequence | Request is `EmergencyRedirected`, `InPersonRecommended`, `Unsupported`, or `ClinicalReview` version 4 with outcome-specific direction and no downstream service, work item, or external action. |
| Publication gate | `UNAPPROVED_SYNTHETIC`; medical-director approval, golden-case approval, and production publication are all false. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; a medically published protocol; clinical validation or medical-director approval; production publication; diagnosis; free-text clinical intake; generative or opaque clinical logic; clinical override; reviewer assignment or action; administrator clearance; patient contact; practice acceptance; queue insertion or position; doctor assignment; appointment; encounter; consent; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; emergency dispatch; external calls; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can evaluate a request; if the client can select the category, outcome, rule, reason, or clinical narrative; if missing/unknown content can become eligible; if a synthetic pass is represented as approved real-world clinical eligibility; if changed/stale location, universal assessment, protocol, category, or source evidence can pass; if the stored fired-rule order is not reproducible; if more than one complaint assessment or receipt can be created; if earlier evidence changes; if the production-publication flags can become true; if a clinical-review work item or any downstream consequence appears; or if an earlier safeguard regresses. Rollback removes the route/UI and forward-disables the complaint-assessment path without rewriting immutable evidence.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, applicant-owned complaint-triage slice above. The required future clinical approval is deliberately not inferred from that program authority.

## References

- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Clinical triage and safety](../05-clinical-triage-and-safety.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Testing, acceptance, and traceability](../19-testing-acceptance-and-traceability.md)
- [Decision 0045](0045-approved-sprint-42-applicant-request-universal-safety-assessment.md)
- [Sprint 43 plan](../backlog/sprint-43-applicant-request-complaint-triage.md)
