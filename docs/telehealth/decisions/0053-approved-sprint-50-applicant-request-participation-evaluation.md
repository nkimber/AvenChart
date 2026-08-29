# Decision 0053: Sprint 50 applicant request participation evaluation

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired `SyntheticRequestCreated` version 26 applicant whose source-bound request is exactly pending `Verification` version 10 after the immutable Sprint 49 participation-context confirmation to request one server-owned, effective-dated synthetic exact participation evaluation.

The transaction appends one immutable evaluation, advances only the request from `Verification` version 10 to `Verification` version 11, and appends one request event. It may establish that the fixed synthetic billing entity, rendering provider, network, practice, location, professional telehealth service, real-time audio/video modality, state, date of service, and new-patient acceptance tuple matches the approved non-production catalog. It does not evaluate or assert real state practice authority, licensure, credentialing, payer data, provider-directory data, contracting, rendering-provider participation, assignment, availability, coverage, benefits, price, appointment, consent, or care.

## 2. Evaluation meaning

The policy `SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_EVALUATION` version 1 evaluates only the immutable server-owned Sprint 49 context against the fixed `avenchart-synthetic-participation-evaluation-2026-08` catalog. The compatibility target is `HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0`, reflecting Plan-Net PractitionerRole and OrganizationAffiliation relationships among practitioners, organizations, networks, services, and locations. This slice does not serialize FHIR or contact a payer, directory, licensing board, credentialing source, clinician, or other external destination.

`syntheticExactNetworkMatched=true` means only that every field in the fixed non-production tuple matched the synthetic catalog during its effective period. `realStateAuthorityVerified`, `realCredentialingVerified`, `renderingPhysicianNetworkChecked`, and `exactNetworkConfirmed` remain false. An NPI remains an identifier rather than proof of licensure, credentialing, participation, or availability.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and practice/facility isolated.
2. The server rebinds the unchanged applicant, portal-disabled unmerged patient shell, exact request, exact immutable Sprint 49 context, every referenced prior result, and current configured staff record under transaction locks.
3. The request must be exactly pending `Verification` version 10; exactly one participation context and no prior participation evaluation or downstream consequence may exist.
4. The projection is no-edit and minimized. It returns no full NPI, staff identifier, TIN, canonical patient identifier, member data, raw internal reference, contract identifier, license number, price, queue, or assignment data.
5. The command accepts only expected request version, opaque evaluation snapshot fingerprint, and four explicit true values: synthetic-data confirmation, exact-tuple scope acknowledgment, no-coverage-guarantee acknowledgment, and real-verification-still-required acknowledgment.
6. The server owns every evaluated field, outcome, source mode, compatibility target, effective date, and result-valid-through time. The client cannot choose or modify a provider, billing entity, authority, role, affiliation, payer, product, network, contract, service, location, modality, new-patient status, outcome, or date of service.
7. Missing, false, malformed, stale, foreign, expired, changed-provenance, roster-drift, matrix-mismatch, or unsupported-state submissions fail closed before evidence is written.
8. Exactly one evaluation, one request-only same-status version advance, and one request event are committed atomically. Exact replay returns the original result; changed-key reuse, a second command, and concurrent duplicate writers fail closed.
9. Evidence is append-only, database-clock constrained, snapshot-bound, private/no-store, and applicant-correlated.
10. The UI starts all four acknowledgments unchecked, preserves a stable retry identity only for unchanged content, restores focus after errors and success, reflows at 320 pixels, and persists no evaluation evidence.
11. No real authority verification, credentialing verification, real rendering-provider participation verification, clinician assignment, exact real network, canonical coverage, coverage selection/verification, financial route, operational work, acceptance, contact, doctor search, queue, position, appointment, encounter, consent, care, prescription, claim, integration, or external call is created.
12. Migration, policy, access isolation, replay/contention, expiry/stale/provenance denial, minimization, append-only evidence, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–49.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/participation-evaluation`. |
| Entry | One immutable Sprint 49 context with exact current positive eligibility, practice-network, and candidate provenance; request pending `Verification` version 10. |
| Input | Expected version, opaque snapshot, and four acknowledgments; no evaluated reference or outcome input. |
| Exact synthetic tuple | Billing entity, rendering provider, payer product/network, practice/facility, state, location, service, modality, new-patient acceptance, date of service, and effective period. |
| Mutation | One immutable evaluation, request `Verification` version 10 to version 11, and one event. |
| Output | Candidate and masked provider/billing references, payer/product, state, date, service/modality, effective period, synthetic component matches and business outcome, plus explicit false real-verification and downstream flags. |
| Outstanding gates | Real state authority and credential verification, real payer/directory participation, clinician assignment and availability, canonical coverage, financial, operational, consent, queue, appointment, encounter, and care. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; real provider, license, credential, payer, contract, billing, or network data; license lookup; FHIR serialization; payer, directory, licensing-board, credentialing, pharmacy, clearinghouse, or other connectivity; real state-practice-authority verification; real credentialing verification; real rendering-provider or billing-entity participation verification; exact real network confirmation; coverage/payment/price guarantees; canonical coverage; operational review; self-pay; consent; staff action; contact; practice acceptance; queueing; doctor assignment; appointment; encounter; media; care; prescribing; billing or claims; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can view or request the evaluation; if a full NPI, staff identifier, TIN, member value, internal provider/contract/authority reference, or license number is disclosed; if the client can supply an evaluated field or outcome; if stale or mismatched context authorizes the command; if multiple evaluations can exist; if the synthetic match is represented as real authority, credentialing, payer verification, or exact real network confirmation; or if any downstream consequence appears. Rollback removes the route and UI and forward-disables the evaluation path without rewriting immutable evidence.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic participation-evaluation boundary above.

## References

- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [Decision 0052](0052-approved-sprint-49-applicant-request-participation-context.md)
- [HL7 Da Vinci PDex Plan-Net implementation guidance 1.2](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/implementation.html)
- [HL7 Da Vinci PDex Plan-Net PractitionerRole 1.2](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/StructureDefinition-plannet-PractitionerRole.html)
- [HL7 Da Vinci PDex Plan-Net OrganizationAffiliation 1.2](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/StructureDefinition-plannet-OrganizationAffiliation.html)
- [CMS NPI files and NPI limitations](https://download.cms.gov/nppes/NPI_Files.html)
- [Medical Board of California telehealth guidance](https://www.mbc.ca.gov/Resources/Medical-Resources/telehealth.aspx)
- [Georgia Composite Medical Board rules](https://rules.sos.ga.gov/gac/360-3)
- [Florida Statutes section 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499/0456/Sections/0456.47.html)
- [Sprint 50 plan](../backlog/sprint-50-applicant-request-participation-evaluation.md)
