# Sprint 20: synthetic practice-network determination

Status: Approved for bounded implementation by [TH-DEC-0023](../decisions/0023-approved-sprint-20-synthetic-practice-network-determination.md)  
Scope: Applicant-triggered deterministic NON_PRODUCTION practice/facility/service network determination after a fresh normalized eligibility result; Plan-Net-shaped metadata only, with no live directory/FHIR call, rendering-physician participation, canonical coverage, financial amount, patient promotion, request, queue, care, downstream action, external integration, or production use

## 1. Outcome

Exercise the first prospective provider-directory adapter seam without pretending to query a payer. Bind the practice, facility, selected plan, state, service, date, and complete upstream evidence server-side; produce one normalized practice-network result; record immutable evidence at `SyntheticPracticeNetworkRecorded`; and stop before rendering-physician network, canonical coverage, financial, patient, request/queue, or care gates.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP20-001` | Add one append-only practice-network determination and constrained `SyntheticEligibilityRecorded -> SyntheticPracticeNetworkRecorded` event with complete upstream/eligibility provenance, directory metadata, normalized statuses, freshness, and hard-false downstream consequences. |
| `TH-SP20-002` | Add a deterministic `ITelehealthProspectivePracticeNetworkGateway` port and synthetic adapter with in-network/accepting, out-of-network, and unavailable fixtures shaped around Plan-Net plan/network/organization-affiliation/location/service concepts. |
| `TH-SP20-003` | Add one applicant-owned idempotent private/no-store command accepting only version plus synthetic acknowledgment, requiring fresh eligibility evidence, sending no member data to the adapter, and returning no protected/raw values. |
| `TH-SP20-004` | Extend prospective entry with accessible network explanation/confirmation, stable retry, persistent emergency action, separate eligibility and practice-network statuses, explicit physician/payment limitations, and no result persistence. |
| `TH-SP20-005` | Keep applicant resume coarse; allow only synthetic practice-network checked/in-network facts while rendering-physician, aggregate exact-network, coverage, financial, identity/patient, consent, acceptance, request/queue, clinical, downstream, integration, and external consequences remain false. |
| `TH-SP20-006` | Prove all fixtures, adapter/standard metadata, eligibility freshness and separation, source/access/version isolation, replay-before-adapter, contention, append-only evidence, response/resume minimization, zero canonical/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. Normalized contract

| Field | Rule |
|---|---|
| Command | `expectedVersion` and `syntheticDataConfirmed=true` only. |
| Inquiry facts | Server-owned practice ID/display, facility, selected plan, current state, current UTC date, and `ProfessionalTelehealthConsultation`; no member/subscriber input. |
| Compatibility | `HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0`; no FHIR resource, bundle, or conformance claim. |
| Transport | `SimulatedAvailable` or `SimulatedUnavailable`, independently recorded from directory/business results. |
| Plan/network match | `Matched` or `Unknown`. |
| Practice affiliation | `InNetwork`, `OutOfNetwork`, or `Unknown`. |
| Service availability | `Included`, `Excluded`, or `Unknown`. |
| New-patient acceptance | `Accepting`, `NotAccepting`, or `Unknown`; this is directory metadata, not operational practice acceptance or appointment availability. |
| Business outcome | `PracticeInNetworkAcceptingNewPatients`, `PracticeOutOfNetwork`, or `UnableToDetermine`. |
| Trace/freshness | Opaque synthetic request/response trace tokens, source/dataset version and effective window, checked time, prior eligibility expiry, and short result expiry. |

## 4. Deterministic fixture matrix

| Selected plan | Transport | Plan/network | Practice affiliation | Service | New patient | Business outcome |
|---|---|---|---|---|---|---|
| Harbor Mutual | `SimulatedAvailable` | `Matched` | `InNetwork` | `Included` | `Accepting` | `PracticeInNetworkAcceptingNewPatients` |
| Blue Valley Health | `SimulatedUnavailable` | `Unknown` | `Unknown` | `Unknown` | `Unknown` | `UnableToDetermine` |
| Pine State Choice | `SimulatedAvailable` | `Matched` | `OutOfNetwork` | `Excluded` | `Unknown` | `PracticeOutOfNetwork` |

Eligibility and network remain independent: active eligibility does not create network participation, and an unavailable or out-of-network directory result does not rewrite the immutable eligibility result. A later rendering clinician must still be checked against the exact product/network/service/location/date before assignment.

## 5. Exit boundary

Sprint 20 ends at normalized synthetic practice/facility/service directory evidence. Real FHIR or payer-directory connectivity, endpoint discovery/authentication, NPI/entity matching, provider contracts, rendering-physician network confirmation, directory reconciliation, canonical coverage, cost sharing/estimate/self-pay, financial acknowledgment, identity proofing, patient promotion/linkage, consent, practice acceptance, request creation, and queue entry remain unavailable and separately gated.
