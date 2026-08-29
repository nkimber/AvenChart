# Decision 0054: Sprint 51 applicant request operational-review submission

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired `SyntheticRequestCreated` version 26 applicant whose source-bound request is exactly pending `Verification` version 11 after the immutable Sprint 50 participation evaluation to submit that request for practice operational review.

The transaction appends one immutable submission, advances only the request from `Verification` version 11 to `OperationalReview` version 12, and appends one request event. The existing practice-scoped administrator operational-review projection may then list the request. This status means only that the bounded non-production automated evidence is ready for staff review. It is not practice acceptance, coverage verification, a financial route, patient contact, a care queue, an appointment, an encounter, consent, or care authorization.

## 2. Submission meaning

The policy `SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION` version 1 rebinds the exact current applicant, portal-disabled patient shell, request, intake, insurance-source, eligibility, practice-network, rendering-candidate, participation-context, and participation-evaluation chain. It accepts no clinical, insurance, provider, network, operational, or disposition value from the client.

`syntheticAutomatedChecksComplete=true` means only that the approved non-production chain was present, internally consistent, unexpired, and unchanged at submission time. `coverageVerified`, `exactNetworkConfirmed`, `practiceAccepted`, `patientCareQueueEntered`, `clinicianQueueEntered`, `appointmentCreated`, `encounterCreated`, and `careAuthorized` remain false. No payer, provider directory, licensing board, credentialing source, clinician, pharmacy, clearinghouse, or other external destination is contacted.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and practice/facility isolated.
2. The server rebinds the applicant, portal-disabled unmerged patient shell, exact request, all request-stage evidence from Sprints 40–50, and the current staff record referenced by the participation evaluation under transaction locks.
3. The request must be exactly pending `Verification` version 11; exactly one current participation evaluation and no prior operational-review submission, canonical coverage, financial record, queue, reservation, video, consultation, appointment, encounter, consent, care, claim, or external consequence may exist.
4. The projection is no-edit and minimized. It returns no full NPI, staff identifier, TIN, canonical patient identifier, member data, internal provider/contract/authority reference, price, queue, appointment, or clinical detail.
5. The command accepts only expected request version, opaque submission snapshot fingerprint, and four explicit true values: synthetic-evidence acknowledgment, no-coverage-guarantee acknowledgment, practice-review-pending acknowledgment, and no-care-relationship acknowledgment.
6. Missing, false, malformed, stale, foreign, expired, changed-provenance, staff-roster drift, or unsupported-state submissions fail closed before evidence is written.
7. Exactly one submission, one request status/version advance, and one request event are committed atomically. Exact replay returns the original result; changed-key reuse, a second command, and concurrent duplicate writers fail closed.
8. Evidence is append-only, database-clock constrained, snapshot-bound, private/no-store, and applicant-correlated.
9. The existing administrator operational-review list may project the request only by configured practice/facility scope. This slice adds no staff claim, assignment, priority, decision, exception, or patient-contact action.
10. The UI starts all four acknowledgments unchecked, preserves a stable retry identity only for unchanged content, restores focus after errors and success, reflows at 320 pixels, and persists no submission evidence.
11. No canonical coverage, coverage selection/verification, estimate, financial acknowledgment/route, staff assignment, practice acceptance, contact, doctor search, queue, position, appointment, encounter, consent, care, prescription, claim, integration, or external call is created.
12. Migration, policy, access isolation, replay/contention, expiry/stale/provenance denial, minimization, append-only evidence, administrator tenant isolation, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–50.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/operational-review-submission`. |
| Entry | One current immutable Sprint 50 evaluation and complete exact prior provenance; request pending `Verification` version 11. |
| Input | Expected version, opaque snapshot, and four acknowledgments; no evaluated or operational value input. |
| Mutation | One immutable submission, request `Verification` version 11 to `OperationalReview` version 12, and one event. |
| Output | Minimized practice/payer/product/state/purpose/date/service/modality and masked candidate summary, synthetic prerequisite statuses, submission state, explicit false real-coverage/acceptance/downstream flags, and honest direction. |
| Administrator effect | The request becomes visible in the existing practice/facility-scoped operational-review projection. No administrator action is performed. |
| Outstanding gates | Real authority, credentialing and participation; canonical coverage and financial route; staff review/acceptance; consent; patient contact; queue authorization; appointment; encounter; and care. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; real provider, license, credential, payer, contract, billing, or network data; FHIR or X12 serialization; payer, directory, licensing-board, credentialing, pharmacy, clearinghouse, or other connectivity; real state-practice-authority verification; real rendering-provider participation; exact real network; coverage/payment/price guarantees; canonical coverage; estimate or self-pay; staff claim/assignment; staff decision; patient contact; practice acceptance; queueing; doctor assignment; appointment; encounter; media; consent; care; prescribing; billing or claims; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can view or submit; if protected member/provider/internal identifiers are disclosed; if the client can supply an evaluated, operational, or disposition field; if stale evidence authorizes the command; if multiple submissions can exist; if the request appears accepted or queued; if the administrator can view another practice/facility; or if any downstream consequence appears. Rollback removes the route and UI and forward-disables the submission path without rewriting immutable evidence.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic operational-review-submission boundary above.

## References

- [Workflows and state machines](../03-workflows-and-state-machines.md)
- [Practice configuration and queue operations](../07-practice-configuration-and-queue-operations.md)
- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [Decision 0053](0053-approved-sprint-50-applicant-request-participation-evaluation.md)
- [HL7 Da Vinci PDex Plan-Net implementation guidance 1.2](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/implementation.html)
- [Medical Board of California telehealth guidance](https://www.mbc.ca.gov/Resources/Medical-Resources/telehealth.aspx)
- [Georgia Composite Medical Board Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3)
- [Florida Statutes section 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499/0456/Sections/0456.47.html)
- [Sprint 51 plan](../backlog/sprint-51-applicant-request-operational-review-submission.md)
