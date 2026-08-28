# Insurance eligibility, network participation, and pricing

## 1. Two independent coverage gates

The product must not collapse “insurance is active” and “this physician/practice is in network” into one answer.

### Gate A: member eligibility and benefits

Determine whether the member/subscriber data matches an active benefit plan for the date of service and whether telehealth/professional services have reported benefit information. The standards-oriented adapter models X12 270 inquiry and 271 response content, including payer trace numbers and benefit limitations.

### Gate B: exact network participation

Determine whether the exact combination participates in the exact plan network:

- payer and product/network identifier, not only a payer display name;
- member coverage and date of service;
- billing legal entity/TIN and billing NPI;
- rendering physician NPI and state authority;
- practice/service/billing location and patient location state;
- intended service/category and modality; and
- participation contract/evidence effective dates.

Provider directories can support this check, but are not universally authoritative for a member's exact product. Practice contract configuration or an approved verification source must be available. A payer logo/card OCR match is not network proof.

## 2. Patient coverage capture

The patient selects an existing coverage record or enters a new one. Capture payer, plan/product/network, member ID, group, subscriber name/date of birth/relationship, coverage priority, payer contact/routing identifiers, and front/back card images when offered. OCR is assistive only: extracted fields are labeled, confidence-scored, and explicitly confirmed by the patient. Raw images and extracted identifiers receive PHI/security controls.

Changes create a versioned coverage candidate. Existing chart coverage is not silently overwritten before validation/review. A consultation retains a snapshot/reference of the coverage used even if the policy changes later.

## 3. Status model

Eligibility and network have separate status/evidence. The combined patient-facing financial route is one of:

| Status | Meaning and behavior |
|---|---|
| `ConfirmedInNetwork` | Active benefit evidence plus exact participation evidence are current; disclose that this is not a guarantee of payment and show estimate inputs |
| `CoverageActiveNetworkPending` | Eligibility appears active, exact network result is not confirmed; practice may hold, manually verify, or offer acknowledged self-pay according to policy |
| `OutOfNetworkOrSelfPay` | Evidence indicates out-of-network/no coverage or patient chooses self-pay; show approved price/GFE and financial terms before acceptance |
| `UnableToVerify` | Timeout, mismatch, missing data, unsupported payer, or ambiguous result; never translate to “covered”; assign manual owner and safe next action |
| `CoverageInactive` | Payer response/manual evidence indicates inactive for service date; offer correction/other coverage/self-pay as policy permits |
| `PatientResponsibilityUnknown` | Network may be confirmed but cost-sharing detail is inadequate; estimate must show ranges/unknowns and assumptions |

## 4. Evidence model

Every verification stores request/response version, source/destination, trace/correlation IDs, semantic query fingerprint, payer/product/member identifiers in protected form, billing/rendering entities, service/date/location, result codes, normalized status, raw payload secure reference, received/verified time, freshness expiry, human verifier/source when manual, and reason/limitations.

Manual verification is not a checkbox. It requires authorized user, source (payer portal/phone/contract roster), reference/contact, date/time, exact entity/product/service scope, result, expiry, and evidence attachment/reference. Changes to clinician, product, service date, practice entity, patient state, or material payer data invalidate affected evidence.

## 5. Estimate and self-pay

The estimate engine is versioned and deterministic. It uses configured cash price or contracted assumptions, anticipated service/code family, known copay/deductible/coinsurance when reliable, and explicit unknowns. It displays:

- practice/provider and service;
- estimate date and expiration;
- expected charge and estimated patient responsibility/range;
- insurance/network evidence status and timestamp;
- assumptions and excluded downstream services/prescriptions/labs;
- statement that eligibility/network information is not a guarantee of payment;
- cancellation/refund/payment policy; and
- self-pay Good Faith Estimate content/process where applicable.

No payment is required in the baseline unless a later payment specification is approved. If preauthorization is needed, it is a separate, visible state; X12 278 is an adapter seam, not assumed in MVP.

## 6. Requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-INS-001 | Eligibility/benefits and exact network participation MUST be separate checks, records, statuses, expirations, and patient disclosures. | Contract/schema/UI tests. |
| TEL-INS-002 | The eligibility adapter MUST model HIPAA-adopted X12 270/271 semantics and preserve payer trace/business responses independently from transport status. | Adapter contract fixtures. |
| TEL-INS-003 | Network confirmation MUST evaluate exact payer product/network, billing entity/TIN/NPI, rendering NPI, state/location, service/modality, and date. | Participation matrix tests. |
| TEL-INS-004 | An `unknown`, timeout, directory-only match, OCR result, payer-name match, or active coverage MUST NOT be represented as confirmed in-network. | Negative semantic tests. |
| TEL-INS-005 | Patients MUST confirm extracted/entered coverage fields and choose the coverage priority or self-pay route; card OCR MUST remain untrusted assistance. | UX and data-provenance tests. |
| TEL-INS-006 | Coverage changes MUST be versioned and reviewed/applied under practice policy; the visit MUST retain the exact coverage snapshot/evidence used. | Historical reconstruction test. |
| TEL-INS-007 | Verification evidence MUST be attributable, protected, time-bounded, reproducible, and invalidated by material input changes. | Evidence/freshness tests. |
| TEL-INS-008 | Manual verification MUST require exact source, scope, reference, result, verifier, timestamp, and expiration. | Manual-review validation test. |
| TEL-INS-009 | The system MUST present a versioned estimate/GFE or an explicit inability to estimate before queue authorization, with assumptions and no-guarantee language. | Price content and acceptance tests. |
| TEL-INS-010 | Self-pay MUST be an explicit patient choice or approved fallback with financial acknowledgment; failure to verify insurance MUST not silently convert the patient. | Financial-route tests. |
| TEL-INS-011 | No hardcoded global payer rule may determine coverage, network, modifier, POS, or patient responsibility; rules MUST be scoped/versioned/effective-dated. | Configuration and multi-payer tests. |
| TEL-INS-012 | Sensitive coverage identifiers and raw transactions MUST be encrypted/access-restricted, masked in UI/logs, and excluded from metrics labels. | Security/log review. |
| TEL-INS-013 | Eligibility/network external calls MUST have timeout, retry/idempotency, circuit-breaker, manual recovery, and patient-facing delay behavior. | Failure-injection tests. |
| TEL-INS-014 | A payer/provider directory source MUST record its authority and limitation; public CMS directory APIs MUST not be treated as universal commercial network truth. | Source-policy review and UI test. |
| TEL-INS-015 | Financial acknowledgments MUST preserve the rendered estimate/content version and affirmative patient action without waiving protections not lawfully waivable. | Legal review and evidence test. |
| TEL-INS-016 | Coverage and network status changes after queuing MUST trigger a governed re-evaluation that preserves queue fairness and informs the patient; they cannot silently change financial responsibility. | In-queue invalidation test. |

## 7. Stub behavior

The deterministic eligibility/network stub accepts synthetic payer/product/member/entity/service fixtures and returns each normalized status, structured X12-like trace metadata, delays, timeouts, duplicate responses, malformed responses, and later status changes. Every stub response is watermarked `NON_PRODUCTION` in API data, UI, audit, and generated artifacts. Production mode refuses the stub by configuration safety policy.

