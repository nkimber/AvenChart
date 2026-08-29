# Decision 0047: Sprint 44 applicant request intake snapshot confirmation

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-28

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired prospective applicant whose source-bound request is exactly `Intake` version 4 after the Sprint 43 `TelehealthEligible` synthetic complaint outcome to review a minimized request-time projection, choose one controlled symptom-duration range, and affirm that the already collected applicant information remains the intended source for this request.

The transaction derives a fixed synthetic complaint summary from the server-owned `migraine` or `sleep` category, appends one generic request intake snapshot and one protected applicant receipt, advances only the request to `Verification` version 5, and appends one request event. It does not change the applicant or patient, create canonical coverage, establish legal or clinical consent, verify current eligibility or exact network participation, create an operational-review work item, accept the request, contact the patient, or enter any queue.

## 2. Data-minimization and meaning boundary

The applicant is shown only coarse receipt states, the current state code, masked callback number, server-owned category, controlled duration choices, clinical-publication status, and explicit outstanding gates. No raw insurance identifier, source fingerprint, clinical answer, fired rule, reason code, detailed medication/allergy/history item, another patient record, or staff-only evidence is returned.

The request-level complaint summary is server-derived as `Synthetic migraine intake demonstration` or `Synthetic sleep intake demonstration`; the client cannot submit free text. The only new complaint datum is one of `less-than-day`, `1-3-days`, `4-14-days`, or `more-than-14-days`. This engineering boundary is consistent with purpose-limited collection and access concepts in the [HHS Minimum Necessary Requirement guidance](https://www.hhs.gov/hipaa/for-professionals/privacy/guidance/minimum-necessary-requirement/index.html), but that guidance does not constitute legal approval of this workflow and the treatment exception described there is not used to weaken product minimization.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and configured practice/facility isolated.
2. The server rebinds the unchanged `SyntheticRequestCreated` version 26 applicant, portal-disabled unmerged patient shell, request creation, current location/callback, passing universal assessment, passing complaint assessment, server-owned category, prior promotion/review chain, registration confirmation, notice acknowledgment, insurance handoff, communication/access readiness, device preparation, clinical-information summary, pre-request readiness, and zero-downstream state.
3. The request must be exactly `Intake` version 4 with `TelehealthEligible`, one complaint receipt, one universal receipt, two generic triage assessments, one location, one location confirmation, no prior intake snapshot, and no downstream record or appointment.
4. The complaint fixture remains `UNAPPROVED_SYNTHETIC`; medical-director approval, clinical golden-case approval, and production publication remain false. Advancing to `Verification` is workflow demonstration only and is not a clinical approval claim.
5. The command accepts only expected request version, opaque server snapshot, exact supported state, controlled symptom duration, current-location and callback confirmations, prior-information review, insurance-limitations acknowledgment, pending-consent acknowledgment, pending-verification acknowledgment, complaint-result acknowledgment, and synthetic-data confirmation.
6. The command accepts no patient identifier, complaint category override, complaint narrative, diagnosis, treatment request, outcome, rule, reason, priority, coverage result, network result, legal-consent claim, practice decision, or queue instruction.
7. All confirmations are explicit `true` values with no defaults. Missing, false, malformed, stale, foreign, expired, conflicting, or changed-source submissions fail closed.
8. The projection fingerprint binds every governing source receipt, request/category/outcome/version, current state and callback mask, clinical protocol/publication state, and the earliest applicable database-clock expiry.
9. The transaction is request-version checked, snapshot-bound, semantically idempotent, first-writer safe, database-clock constrained, private/no-store, and applicant-correlated.
10. Exactly one generic `telehealth_intake_snapshots` row, one applicant request-intake receipt, one request transition, and one request event are created. Earlier evidence remains immutable.
11. Exact replay returns the original projection. Changed-content reuse, another command after success, stale version, expired or foreign access, source/patient/protocol/publication drift, and concurrent duplicate writers fail closed.
12. The UI provides no default duration, no free text, explicit correction/stop guidance, stable retry, focus recovery, 320-pixel reflow, keyboard operation, and no intake-result or source-fingerprint persistence in browser storage.
13. No patient confirmation row, demonstration legal acknowledgment, canonical coverage, coverage selection or verification, operational-review task, clinical-review task, contact, doctor search, patient or clinician queue, queue position, appointment, encounter, consent, media, care, prescribing, financial action, claim, integration, or external communication is created.
14. Migration, state-machine, policy, replay/contention, access isolation, expiry/stale/source/protocol/publication drift denial, immutable evidence, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–43.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION`, version 1. |
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/intake`. |
| Entry state | Exact unexpired `SyntheticRequestCreated` applicant and source-linked request at `Intake` version 4 with one passing Sprint 43 complaint assessment. |
| Input | Expected request version, opaque snapshot, exact supported state, one controlled symptom duration, and eight explicit confirmations. |
| Mutation | One generic request intake snapshot, one immutable applicant intake receipt, one request transition, and one request event. |
| Result | Request is `Verification` version 5 for synthetic workflow demonstration only; the applicant and patient remain unchanged. |
| Outstanding gates | Clinical publication, legal/clinician consent, canonical coverage, current eligibility, exact network, financial route, practice operational authorization, queueing, appointment, encounter, and care all remain unavailable. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; free-text clinical intake; a medically published protocol; diagnosis; clinical review or override; canonical patient/coverage mutation; legal or clinician consent; eligibility, benefits, exact network, estimate, or financial verification; staff operational action; patient contact; practice acceptance; queue insertion or position; doctor assignment; appointment; encounter; media; care; prescribing; pharmacy transmission; billing/claim; FHIR or X12 serialization; integration; external calls; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can view or confirm the snapshot; if the client can submit free text, category, outcome, source identity, clinical rule, insurance result, consent claim, or workflow state; if omitted/false confirmations can pass; if stale or changed evidence can be snapshotted; if more than one intake or applicant receipt can be created; if the patient/applicant or earlier evidence changes; if clinical publication is implied; or if any downstream consequence appears. Rollback removes the route/UI and forward-disables the applicant intake-confirmation path without rewriting immutable evidence.

## 7. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to the disabled synthetic, applicant-owned request-intake snapshot slice above.

## References

- [Actors, permissions, and journeys](../02-actors-and-journeys.md)
- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Patient onboarding and identity](../04-patient-onboarding-and-identity.md)
- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [Security, privacy, consent, and audit](../16-security-privacy-consent-and-audit.md)
- [Decision 0046](0046-approved-sprint-43-applicant-request-complaint-triage.md)
- [Sprint 44 plan](../backlog/sprint-44-applicant-request-intake-snapshot-confirmation.md)
