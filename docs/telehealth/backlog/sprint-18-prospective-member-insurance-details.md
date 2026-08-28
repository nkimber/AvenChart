# Sprint 18: protected synthetic prospective member-insurance details

Status: Approved for bounded implementation by [TH-DEC-0021](../decisions/0021-approved-sprint-18-prospective-member-insurance-details.md)  
Scope: Applicant-owned minimum synthetic member/group/subscriber confirmation after practice-plan discovery; protected raw payload and masked receipt only, with no real insurance/PHI, card/OCR, government identifier, member matching, eligibility/benefits, exact network, canonical coverage, estimate/payment, identity proofing, patient promotion/linkage, consent, request, queue, care, downstream action, external integration, or production use

## 1. Outcome

Model the minimum insurance-detail confirmation needed before a future eligibility adapter without pretending the details were verified. Record one protected immutable receipt at `MemberInsuranceDetailsRecorded`; stop before eligibility, exact network, canonical coverage, financial, patient, request, queue, or care gates.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP18-001` | Add one append-only member-detail receipt and constrained `PracticeNetworkPrecheckRecorded -> MemberInsuranceDetailsRecorded` event with review, safety, purpose, plan-precheck, state, protection, confirmation, and hard-false consequence provenance. |
| `TH-SP18-002` | Add exact server normalization for `SYN-` member/group identifiers, `Self/Spouse/Parent/Other`, conditional subscriber identity, primary priority, dual acknowledgments, masking, and a versioned purpose-isolated payload protector. |
| `TH-SP18-003` | Publish one applicant-owned private/no-store idempotent record route with opaque not-found, bounded Problem Details, protected persistence, minimized receipt response, and no patient/staff-session substitution. |
| `TH-SP18-004` | Extend the prospective entry with accessible member/group/subscriber controls, conditional fields, mask-only confirmation, persistent emergency direction, stable retry/reload, and no insurance-detail persistence. |
| `TH-SP18-005` | Keep applicant resume coarse and every eligibility, benefit, physician/exact-network, coverage, financial, identity/patient, request/queue, care, and external consequence false. |
| `TH-SP18-006` | Prove validation/conditionality, ciphertext-at-rest/no-plaintext, state/access/version isolation, exact replay, protection failure, contention, append-only evidence, response/resume minimization, zero canonical/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. Minimum structured contract

| Field | Rule |
|---|---|
| `memberId` | Required, 6–32 uppercase letters/digits/hyphens after normalization, and must start `SYN-`; returned only as `••••` plus the last four characters. |
| `groupNumber` | Optional, 6–32 uppercase letters/digits/hyphens, must start `SYN-` when present; returned only as a last-four mask. |
| `subscriberRelationship` | Exactly `Self`, `Spouse`, `Parent`, or `Other`. |
| Subscriber legal name/date of birth | Must be absent for `Self`, in which case current applicant identity is rebound; all three are required for non-self and remain only in the protected payload. |
| Coverage priority | Server-fixed `Primary`; no secondary/tertiary coordination or self-pay route is created. |
| Confirmations | `detailsConfirmed` and `syntheticDataConfirmed` must both be explicit `true`. |

The payload is protected with purpose `AvenChart.Telehealth.ProspectiveMemberInsuranceDetails.v1`, records scheme `ASP.NET_CORE_DATA_PROTECTION` and version 1, and is opaque to SQL. It is not an X12 model, eligibility query, coverage artifact, or reusable authentication secret.

## 4. Acceptance evidence

1. Only the configured branded host and correct access-key owner of an unexpired, current `PracticeNetworkPrecheckRecorded` applicant with intact no-candidate review, passing safety, controlled purpose, and exact plan-precheck provenance can record.
2. Non-`SYN-` identifiers, malformed/oversized values, missing confirmations, invalid relationships, self requests containing subscriber identity, and non-self requests missing valid subscriber identity return bounded 400 responses without writes.
3. The repository transaction resolves payer/product/plan/outcome solely from the current precheck, protects the normalized payload before SQL insertion, and writes neither raw identifiers nor subscriber identity to any ordinary column, event, fingerprint, or response.
4. Database evidence stores only ciphertext, last-four masks, relationship, plan/provenance, and protection metadata; at-rest scans find no submitted raw member/group/subscriber values. Unprotectable or mismatched replay content fails closed.
5. Every response keeps `memberEligibilityChecked`, `memberBenefitsChecked`, `renderingPhysicianNetworkChecked`, `coverageVerified`, `exactNetworkConfirmed`, and all identity/patient/financial/request/queue/care/downstream/external flags false.
6. Exact retry returns one immutable receipt; changed content, stale version, second semantic command, and concurrent first writers create no duplicate evidence.
7. Recording changes only the applicant aggregate plus one detail receipt and event; `insurance_records`, patients, portals, intake/coverage evidence, requests, queues, appointments, encounters, prescriptions, claims, messages, tasks/notifications, integration, and external-call evidence remain unchanged.
8. Component and cross-browser tests cover labels/instructions, conditional fields, error summary/focus recovery, mask-only result, ambiguous retry with one command identity, 320 px reflow, serious automated WCAG findings, persistent emergency links, and no insurance values in local/session storage.

## 5. Exit boundary

Sprint 18 ends at a protected synthetic member-detail receipt. Durable production key custody/recovery, real member data, card/OCR, member matching, canonical coverage creation, eligibility/benefits, exact practice/rendering-physician network confirmation, estimates/self-pay, financial acknowledgment, identity proofing, patient promotion/linkage, consent, practice acceptance, request creation, and queue entry remain unavailable and separately gated.
