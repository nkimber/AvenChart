# Decision 0029: Sprint 26 synthetic insurance handoff confirmation

Status: Approved — active for the exact disabled synthetic slice below  
Approved date: 2026-08-28  
Decision owner: AvenChart program owner  
Implementation owner: Codex delivery agent under AvenChart program-owner direction  
Review/expiry: 2026-10-31, or immediately when superseded by a later gate decision

## 1. Authorized outcome

Permit the access-key owner of a synthetic applicant who completed the Decision 0028 minimum registration-details confirmation to review a server-owned, masked handoff of the insurance information and synthetic evidence already collected before promotion, then record one immutable no-edit confirmation.

This checkpoint confirms only the selected payer/product, masked member and optional group identifiers, subscriber relationship, coverage priority, and acknowledgment of the recorded eligibility and practice-level network fixture limitations. `InsuranceDetailsConfirmed` means the applicant recognized those copied inputs; it does not mean an insurer confirmed coverage.

## 2. Required controls

1. The feature remains disabled by default, rejected in Production, synthetic-only, private/no-store, audited, applicant-key protected, practice/facility scoped, and limited to an unexpired `SyntheticMinimumRegistrationDetailsConfirmed` aggregate with one successful promotion, one portal-disabled unmerged patient shell, and one current registration-details receipt.
2. The server rebinds the applicant, promotion, patient shell, registration receipt, protected member-details receipt, eligibility result, and practice-network result on every read/write. Broken or cross-applicant provenance, patient drift, portal enablement, merge state, or canonical insurance presence fails closed.
3. Initial confirmation requires current positive synthetic eligibility and practice-level network fixtures. A confirmed receipt may later be read with evidence labeled expired; it never converts stale evidence into current evidence.
4. The response shows payer/product, masks derived only from stored last-four values, subscriber relationship, priority, normalized outcomes, evidence timestamps/freshness, and the explicit fact that no rendering physician was checked. It returns no raw member/group value, subscriber identity, patient identifier, protected payload, trace token, proofing evidence, staff rationale, or duplicate candidate information.
5. The browser cannot submit edits or replacement insurance values. It may echo only the server snapshot fingerprint/version and five affirmations: payer/product, masked member details, subscriber relationship/priority, evidence limitations, and synthetic-only use.
6. If anything is wrong, the UI directs the applicant to stop and restart/contact the practice. This slice does not patch data, create or update `insurance_records`, or start a correction workflow.
7. The receipt, `SyntheticMinimumRegistrationDetailsConfirmed -> SyntheticInsuranceDetailsConfirmed` transition, and applicant event commit in one PostgreSQL transaction. Database constraints and a provenance trigger independently verify the entire handoff chain, evidence freshness at confirmation, portal-disabled patient, no canonical coverage, and no-consequence flags.
8. Exact retry converges. Changed-key reuse, stale version/fingerprint, expired evidence/applicant, missing or altered provenance, canonical coverage, portal enablement, a second semantic command, partial failure, and concurrent first writers fail closed with at most one receipt and event.
9. `coverageVerified=false`, `exactNetworkConfirmed=false`, `renderingPhysicianNetworkChecked=false`, and `canonicalCoverageCreated=false` are permanent for this slice. No patient mutation, portal/account/session, complete intake, consent, practice acceptance, financial record, request, queue, appointment, encounter, care, prescribing, billing/claim, communication, integration, or external call is created.
10. Unit, API, authorization, live PostgreSQL replay/concurrency/append-only/no-delta, minimization, accessibility/recovery, migration/bootstrap, planning, Graphify, and full regression evidence is required without weakening Sprints 1–25.

## 3. Normalized contract

| Field | Rule |
|---|---|
| Policy | `SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION`, version 1. |
| Entry state | `SyntheticMinimumRegistrationDetailsConfirmed`. |
| Server projection | Payer/product; masked member/group last four; subscriber relationship; primary priority; normalized eligibility and practice-network outcomes/timestamps/freshness; rendering-physician-check false; SHA-256 snapshot fingerprint. |
| Resulting status | `SyntheticInsuranceDetailsConfirmed`. |
| Applicant affirmations | Payer/product, masked member/group, subscriber relationship/priority, limitations, and synthetic data. |
| Data consequence | Immutable handoff receipt only; no applicant, patient, or canonical insurance field is changed. |

## 4. Explicit exclusions

This decision does not authorize real people or data; payer, clearinghouse, directory, pharmacy, or identity-provider communication; X12 or FHIR exchange; canonical coverage; member matching beyond the prior fixture; a coverage, benefits, payment, patient-responsibility, or price guarantee; rendering-physician participation; exact aggregate network confirmation; edits/corrections; identity assurance; portal access; complete demographics/history/intake; legal or clinician consent; practice acceptance; request/queue entry; appointment; encounter; communication/video; care; prescribing; billing/claim; or production enablement.

## 5. Stop conditions and rollback

Stop if raw member/group/subscriber data or patient identifiers are disclosed; if the browser can author replacement insurance values; if stale, non-positive, cross-applicant, or broken evidence can be confirmed; if canonical coverage or a portal-enabled/merged patient can pass; if fixture outcomes are represented as insurer confirmation or a rendering-physician network result; if receipt/state/event provenance can diverge; if retry overwrites history; if any patient, insurance, financial, or downstream record changes; or if an earlier safeguard regresses. Rollback disables/removes the routes and panel. Immutable confirmation evidence is not deleted as rollback; correction requires a separately reviewed forward migration and workflow.

## 6. Approval record

The program owner directed complete implementation of the approved telehealth goal, approved all decisions, and authorized uninterrupted work. This record applies that standing authority only to one disabled synthetic, no-edit insurance handoff confirmation. It does not substitute for payer, insurance, patient-registration, privacy/security, legal, clinical, accessibility, data, operations, interoperability, or production review.

## References

- [Insurance, eligibility, network, and pricing specification](../08-insurance-eligibility-network-and-pricing.md)
- [Patient onboarding and identity specification](../04-patient-onboarding-and-identity.md)
- [Decision 0028](0028-approved-sprint-25-minimum-registration-details-confirmation.md)
- [Sprint 26 plan](../backlog/sprint-26-synthetic-insurance-handoff-confirmation.md)
