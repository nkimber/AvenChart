# Professional claims and financial integration

## 1. Scope and separation

This workflow prepares and, after a separately certified live adapter exists, transmits a professional claim for the physician consultation. It is distinct from eligibility/network verification, patient estimates/payments, the prescription message, and the pharmacy's drug claim.

The initial engineering release uses a deterministic clearinghouse stub. “Generated,” “sent,” “acknowledged,” “accepted,” “adjudicated,” and “paid” are distinct states.

## 2. Claim-ready encounter data

| Area | Required data |
|---|---|
| Patient/subscriber | Canonical patient identity, demographics, address, subscriber/member/group/relationship, coverage order and snapshot |
| Payer | Payer ID/routing, plan/product/network evidence reference, date-of-service eligibility evidence |
| Provider | Billing legal entity, TIN, billing NPI, rendering physician/NPI, taxonomy, state authority, referring provider where required |
| Service location | Patient physical location and state, practice/facility/billing location, modality, CMS place of service determined from facts |
| Clinical | Signed encounter, diagnoses (ICD-10-CM as configured/current), service/procedure codes (CPT/HCPCS under valid licenses/policy), pointers, units, dates/times, documentation support |
| Financial | Charge, fee schedule/version, estimated responsibility, prior authorization/reference when applicable, coordination of benefits |
| Claim control | Claim ID, frequency/type, original claim link for replacement/void, submitter/receiver, rule-set version, correlation/idempotency keys |

CMS POS 10 applies when telehealth is provided and the patient is in their home; POS 02 applies when the telehealth patient is somewhere other than home. The actual confirmed service location drives the value. Modifiers, covered codes, documentation, and payer-specific combinations are effective-dated rules and require billing approval; they are never globally hardcoded from the word “telehealth.”

## 3. Claim workflow

```text
EncounterSigned
  -> ClaimDraft
  -> CodingReview
  -> ScrubFailed | ReadyToSubmit
  -> HumanApproved
  -> Queued/Sent
  -> 999/277CA transport and claim acceptance outcomes
  -> PayerPending
  -> 835 remittance/adjudication
  -> Reconciled | DenialFollowUp | Corrected/Replacement/Void workflow
```

- Scrubbing validates completeness, code/rule combinations, entity identifiers, service/date/location, duplicates, and signed-document support.
- A human billing role reviews or approves submission under practice policy. Autonomous submission is out of scope.
- A 999 acknowledgment indicates syntactic transaction handling, not claim payment. A 277CA provides claim acknowledgment status, not final adjudication. An 835 provides remittance information that must be reconciled to the claim and financial ledger.
- 276/277 supports claim-status inquiry/response. 278 is an optional prior-authorization seam. Each is a separate transaction and status model.

## 4. Standards and canonical adapter

`IProfessionalClaimGateway` maps a versioned canonical claim to HIPAA-adopted ASC X12 standards, including 837 Professional (005010X222A1 as applicable through certified tooling/trading-partner rules), 999/277CA acknowledgments, 276/277 status, and 835 remittance. A standards/licensing-compliant translator or clearinghouse SDK should perform production EDI serialization/validation; bespoke string concatenation is not acceptable.

Trading-partner configuration contains submitter/receiver identifiers, envelopes, endpoints, certificates/keys, test/production mode, acknowledgments, batching, retry windows, contact/escalation, payer rules, and certification evidence. Payloads remain encrypted/restricted and do not enter general logs.

## 5. Requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| TEL-CLM-001 | A claim MUST originate from a signed/finalized encounter and retain references to the exact clinical, coverage, location, provider, fee, and payer-rule versions used. | Historical reconstruction test. |
| TEL-CLM-002 | The system MUST validate billing/rendering NPI, TIN/entity, taxonomy, payer/product, subscriber, date, service, diagnosis pointers, charge, units, and actual service location before ready status. | Claim fixture/scrub tests. |
| TEL-CLM-003 | POS 10 versus POS 02 MUST derive from the physician-confirmed patient location facts; home address alone MUST NOT decide POS. | Location/POS tests. |
| TEL-CLM-004 | CPT/HCPCS, ICD-10-CM, modifiers, coverage and documentation rules MUST be licensed/configured/versioned/effective-dated and approved; no universal payer assumption is allowed. | Multi-payer/date tests. |
| TEL-CLM-005 | Coding assistance MUST NOT assert unperformed care or autonomously select/upcode a billing level; authorized human confirmation is required. | Coding safety tests. |
| TEL-CLM-006 | Claim generation, human approval, transmission, transport acknowledgment, claim acceptance, adjudication, and payment MUST be separate attributable states. | State/role tests. |
| TEL-CLM-007 | Initial production policy MUST require explicit billing-role approval before submission or resubmission. | Authorization/workflow tests. |
| TEL-CLM-008 | Gateway commands MUST be idempotent; a retransmission, replacement, or void MUST preserve the original control numbers and relationship required by the trading partner. | Duplicate/correction tests. |
| TEL-CLM-009 | 999, 277CA, 276/277, and 835 responses MUST be parsed as their actual business meaning and correlated without treating receipt as payment. | EDI fixture contract tests. |
| TEL-CLM-010 | Rejections, denials, missing acknowledgments, and unmatched remittances MUST create owned work items with deadlines, evidence, retry/correction semantics, and escalation. | Revenue-cycle recovery tests. |
| TEL-CLM-011 | The professional claim MUST NOT be transmitted to a pharmacy or e-prescribing endpoint, and the platform MUST NOT claim to submit the pharmacy's drug claim. | Destination allowlist/negative tests. |
| TEL-CLM-012 | A claim correction MUST create a new version and appropriate replacement/void workflow; submitted payloads and acknowledgments are immutable. | Correction history tests. |
| TEL-CLM-013 | Clearinghouse/payer payloads, credentials, certificates, and identifiers MUST be protected, masked, destination-allowlisted, and excluded from ordinary logs/analytics. | Security/data-flow review. |
| TEL-CLM-014 | The deterministic stub MUST exercise accepted, rejected, duplicate, delayed, partial, missing-acknowledgment, denial, adjustment and remittance-mismatch scenarios and be impossible to mistake for live delivery. | Stub certification suite. |
| TEL-CLM-015 | Production submission MUST be blocked until trading-partner enrollment, code-set licensing, companion-guide mapping, end-to-end certification, BAA/security review, and reconciliation controls are approved. | Integration go-live gate. |
| TEL-CLM-016 | Patient-facing claim status MUST use plain, accurate categories and must not disclose internal codes without explanation or promise payer payment. | Portal content tests. |

## 6. Financial reconciliation

The platform links remittance claim/service lines to the submitted claim version and ledger using control numbers plus governed matching. Ambiguity is manual; it is never posted to a plausible patient automatically. Adjustments, contractual amounts, patient responsibility, payments, reversals, refunds, and write-offs retain source and approval. The existing AvenChart billing simulation may support UX/domain prototyping but cannot be represented as live payer adjudication.

## 7. Stub contract

The stub accepts only synthetic test identifiers and returns structured canonical responses plus realistic correlation/control values. It can delay/reorder/duplicate acknowledgments and emit corrupt payloads for resilience testing. Every artifact and UI response carries environment, adapter name, and `NON_PRODUCTION`; production configuration refuses it.

