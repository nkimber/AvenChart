# Decision 0052: Sprint 49 applicant request participation context

Status: Approved — active for the exact disabled synthetic slice below

Approved date: 2026-08-29

Decision owner: AvenChart program owner

Implementation owner: Codex delivery agent under AvenChart program-owner direction

Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of one unexpired `SyntheticRequestCreated` version 26 applicant whose source-bound request is exactly pending `Verification` version 9 after the immutable Sprint 48 rendering-candidate selection to confirm one server-owned, effective-dated synthetic prerequisite context for a future exact participation evaluation.

The transaction appends one immutable context confirmation, advances only the request from `Verification` version 9 to `Verification` version 10, and appends one request event. It does not evaluate or assert real state practice authority, licensure, credentialing, contracting, rendering-provider participation, exact network, assignment, availability, coverage, appointment, consent, or care.

## 2. Context meaning

The policy `SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_CONTEXT` version 1 binds the selected GA, CA, or FL synthetic practitioner and state-authority references to the already established payer, product, network, practice, facility, service, modality, current patient state, and date of service. It adds fixed synthetic PractitionerRole-shaped, OrganizationAffiliation-shaped, billing-organization, and contract references plus one effective period. These internal references are shaped for future Plan-Net-compatible mapping, but this slice does not serialize FHIR or contact a payer, directory, licensing board, credentialing source, clinician, or other external destination.

`participationEvaluationContextConfirmed=true` means only that a later separately authorized evaluator has a fixed set of synthetic prerequisite references. The NPI remains an identifier rather than proof of licensure or credentialing. `realStateAuthorityVerified`, `realCredentialingVerified`, `renderingPhysicianNetworkChecked`, and `exactNetworkConfirmed` remain false.

## 3. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, applicant-access-key protected, and practice/facility isolated.
2. The server rebinds the unchanged applicant, portal-disabled unmerged patient shell, exact request, exact current positive eligibility and practice-network evidence, exact immutable candidate selection, and current configured staff record under transaction locks.
3. The request must be exactly pending `Verification` version 9; exactly one candidate selection and no prior participation context or downstream consequence may exist.
4. The projection is no-edit and minimized. It returns no full NPI, staff identifier, TIN, canonical patient identifier, member data, contract identifier, license number, price, queue, or assignment data.
5. The command accepts only expected request version, opaque context snapshot fingerprint, and four explicit true values: synthetic-data confirmation, NPI-not-credential acknowledgment, real-authority-not-verified acknowledgment, and exact-participation-still-required acknowledgment.
6. The server owns every prerequisite reference and effective date. The client cannot choose or modify a provider, authority, role, affiliation, billing entity, payer, product, network, contract, service, location, modality, outcome, or date of service.
7. Missing, false, malformed, stale, foreign, expired, changed-provenance, roster-drift, or unsupported-state submissions fail closed before evidence is written.
8. Exactly one context, one request-only same-status version advance, and one request event are committed atomically. Exact replay returns the original result; changed-key reuse, a second command, and concurrent duplicate writers fail closed.
9. Evidence is append-only, database-clock constrained, snapshot-bound, private/no-store, and applicant-correlated.
10. The UI starts all four acknowledgments unchecked, preserves a stable retry identity only for unchanged content, restores focus after errors and success, reflows at 320 pixels, and persists no context evidence.
11. No real authority verification, credentialing verification, participation evaluation, clinician assignment, exact network, canonical coverage, coverage selection/verification, financial route, operational work, acceptance, contact, doctor search, queue, position, appointment, encounter, consent, care, prescription, claim, integration, or external call is created.
12. Migration, policy, access isolation, replay/contention, expiry/stale/provenance denial, minimization, append-only evidence, accessibility, recovery, runtime, OpenAPI, planning, Graphify, and full regression evidence is required without weakening Sprints 1–48.

## 4. Normalized contract

| Field | Rule |
|---|---|
| Endpoint | GET and POST `/api/telehealth/v1/applicants/{applicantId}/telehealth-request/participation-context`. |
| Entry | Exact current positive eligibility and practice-network evidence, one immutable Sprint 48 candidate selection, and request pending `Verification` version 9. |
| Input | Expected version, opaque snapshot, and four acknowledgments; no prerequisite reference or outcome input. |
| Context | Server-owned GA/CA/FL synthetic authority, practitioner-role, organization-affiliation, billing-organization, contract, service, modality, location, network, and effective-date bindings. |
| Mutation | One immutable context confirmation, request `Verification` version 9 to version 10, and one event. |
| Output | Candidate display and masked provider/billing references, jurisdiction, coarse synthetic fixture states, effective dates, and explicit false verification and downstream flags. |
| Outstanding gates | Real state authority and credential verification, exact billing-entity and rendering-provider participation evaluation, assignment/availability, coverage, financial, operational, consent, queue, appointment, encounter, and care. |

## 5. Explicit exclusions

This decision does not authorize real people or PHI; real provider, license, credential, payer, contract, billing, or network data; license lookup; FHIR serialization; payer, directory, licensing-board, credentialing, pharmacy, clearinghouse, or other connectivity; state-practice-authority verification; credentialing verification; rendering-provider or billing-entity participation evaluation; exact network confirmation; coverage/payment/price guarantees; canonical coverage; operational review; self-pay; consent; staff action; contact; practice acceptance; queueing; doctor assignment; appointment; encounter; media; care; prescribing; billing or claims; integration; or production enablement.

## 6. Stop conditions and rollback

Stop if foreign or expired access can view or confirm the context; if a full NPI, staff identifier, TIN, member value, contract identifier, or license number is disclosed; if the client can supply a prerequisite reference or outcome; if stale or non-positive upstream evidence authorizes the command; if multiple contexts can exist; if synthetic fixture presence is represented as real authority, credentialing, or exact network verification; or if any downstream consequence appears. Rollback removes the route and UI and forward-disables the context path without rewriting immutable evidence.

## 7. Approval record

The program owner approved all current decisions and authorized uninterrupted implementation. This record applies that standing authority only to the disabled synthetic participation-context boundary above.

## References

- [Insurance eligibility, network participation, and pricing](../08-insurance-eligibility-network-and-pricing.md)
- [Decision 0051](0051-approved-sprint-48-applicant-request-rendering-candidate-selection.md)
- [HL7 Da Vinci PDex Plan-Net PractitionerRole 1.2](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/StructureDefinition-plannet-PractitionerRole.html)
- [HL7 Da Vinci PDex Plan-Net OrganizationAffiliation 1.2](https://hl7.org/fhir/us/davinci-pdex-plan-net/STU1.2/StructureDefinition-plannet-OrganizationAffiliation.html)
- [CMS NPI files and NPI limitations](https://download.cms.gov/nppes/NPI_Files.html)
- [Sprint 49 plan](../backlog/sprint-49-applicant-request-participation-context.md)
