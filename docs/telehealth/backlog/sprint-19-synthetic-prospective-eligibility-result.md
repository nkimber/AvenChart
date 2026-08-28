# Sprint 19: synthetic prospective eligibility result

Status: Approved for bounded implementation by [TH-DEC-0022](../decisions/0022-approved-sprint-19-synthetic-prospective-eligibility-result.md)  
Scope: Applicant-triggered deterministic non-production member eligibility/benefit-information result after protected member details; normalized ASC X12N 270/271 Version 5010-shaped metadata only, with no proprietary transaction serialization, payer/clearinghouse contact, real member matching, exact network, canonical coverage, financial amounts, patient promotion, request, queue, care, downstream action, external integration, or production use

## 1. Outcome

Exercise the first prospective eligibility adapter seam without pretending to conduct an interchange. Unprotect and bind the existing synthetic details server-side, produce one deterministic normalized result, record immutable evidence at `SyntheticEligibilityRecorded`, and stop before exact network, canonical coverage, financial, patient, request/queue, or care gates.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP19-001` | Add one append-only prospective eligibility result and constrained `MemberInsuranceDetailsRecorded -> SyntheticEligibilityRecorded` event with complete upstream provenance, standard/dataset/trace/freshness metadata, normalized statuses, and hard-false downstream consequences. |
| `TH-SP19-002` | Add a deterministic `ITelehealthProspectiveEligibilityGateway` port and synthetic adapter with effective-dated matched-active, matched-inactive, subscriber-not-found, and unavailable fixtures while keeping transport and business outcomes separate. |
| `TH-SP19-003` | Add one applicant-owned idempotent private/no-store command that accepts only version plus synthetic acknowledgment, unprotects details server-side, fails closed on unreadable evidence, and returns no raw or protected values. |
| `TH-SP19-004` | Extend prospective entry with an accessible eligibility explanation/confirmation, stable retry, persistent emergency action, normalized non-financial result, explicit exact-network/payment limitation, and no insurance/result persistence. |
| `TH-SP19-005` | Keep applicant resume coarse and every exact-network, canonical-coverage, financial, identity/patient, consent, acceptance, request/queue, clinical, downstream, integration, and external consequence false. |
| `TH-SP19-006` | Prove all fixture outcomes, adapter/standard metadata, protected-payload failure, source/access/version isolation, exact replay, contention, append-only evidence, response/resume minimization, zero canonical/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. Normalized contract

| Field | Rule |
|---|---|
| Command | `expectedVersion` and `syntheticDataConfirmed=true` only. |
| Date/service | Server date in UTC; fixed normalized service category `ProfessionalTelehealthConsultation`. |
| Compatibility | `ASC_X12N_270_271_005010X279A1`; no serialized X12 content. |
| Transport | `SimulatedAccepted` or `SimulatedUnavailable`, independently recorded from the business result. |
| Member match | `Matched`, `NotMatched`, or `Unknown`. |
| Eligibility | `Active`, `Inactive`, or `Unknown`. |
| Benefit information | `Reported`, `NotReported`, or `Unknown`; no deductible, copay, coinsurance, price, estimate, or patient-responsibility amount in this slice. |
| Business outcome | `EligibleBenefitsReported`, `CoverageInactive`, `SubscriberNotFound`, or `UnableToDetermine`. |
| Trace/freshness | Opaque synthetic inquiry/response trace tokens, dataset/evidence version, checked time, and short expiry; no member-derived token. |

The gateway may inspect the purpose-protected synthetic payload in memory. It must not persist, log, return, or serialize the payload or raw fields, and its output must contain no raw transaction.

## 4. Deterministic fixture matrix

| Selected plan and member ID | Transport | Match | Eligibility | Benefits | Business outcome |
|---|---|---|---|---|---|
| Harbor Mutual / `SYN-HM-1001` | `SimulatedAccepted` | `Matched` | `Active` | `Reported` | `EligibleBenefitsReported` |
| Blue Valley Health / `SYN-BV-2002` | `SimulatedAccepted` | `Matched` | `Inactive` | `NotReported` | `CoverageInactive` |
| Pine State Choice / `SYN-PS-3003` | `SimulatedAccepted` | `NotMatched` | `Unknown` | `NotReported` | `SubscriberNotFound` |
| Every other valid synthetic plan/member combination | `SimulatedUnavailable` | `Unknown` | `Unknown` | `Unknown` | `UnableToDetermine` |

No outcome establishes exact network or guarantees coverage/payment. The application must preserve the selected practice-precheck result separately.

## 5. Exit boundary

Sprint 19 ends at a normalized non-production eligibility result. Real X12 generation/parsing, TA1/999 acknowledgment handling, trading-partner transport/security, real payer matching, canonical coverage creation, exact practice/rendering-physician network confirmation, cost sharing/estimate/self-pay, financial acknowledgment, identity proofing, patient promotion/linkage, consent, practice acceptance, request creation, and queue entry remain unavailable and separately gated.
