# Sprint 21: synthetic identity-proofing process

Status: Approved for bounded implementation by [TH-DEC-0024](../decisions/0024-approved-sprint-21-synthetic-identity-proofing-process.md)  
Scope: Applicant-triggered deterministic NON_PRODUCTION identity-proofing process fixture after fresh positive eligibility and practice-network evidence; NIST SP 800-63A-4 concepts only, with no real identity evidence, government identifier, biometric, authoritative source, IAL claim, patient promotion, request, queue, care, external integration, or production use

## 1. Outcome

Exercise the prospective identity-provider adapter seam without pretending to prove a person. Bind the complete upstream chain server-side; send only opaque synthetic references and fixed process metadata to the adapter; normalize the distinct proofing stages; record immutable fixture evidence at `SyntheticIdentityProofingRecorded`; and stop before real proofing, patient promotion/linkage, account/authenticator, consent, practice acceptance, request/queue, or care gates.

## 2. Stories

| ID | Story |
|---|---|
| `TH-SP21-001` | Add one append-only identity-proofing process result and constrained `SyntheticPracticeNetworkRecorded -> SyntheticIdentityProofingRecorded` event with complete upstream provenance, positive/fresh eligibility and network prerequisites, fixed notice metadata, process-stage statuses, and hard-false identity/downstream consequences. |
| `TH-SP21-002` | Add deterministic `ITelehealthProspectiveIdentityProofingGateway` and NON_PRODUCTION adapter ports that accept only opaque references and server-owned context, model NIST SP 800-63A-4 process concepts, and cannot claim an IAL. |
| `TH-SP21-003` | Add one applicant-owned idempotent private/no-store command accepting only version plus two acknowledgments; reject inactive/unknown eligibility and out-of-network/unknown/expired directory evidence; return no demographic, contact, insurance, document, biometric, or raw evidence. |
| `TH-SP21-004` | Extend prospective entry with accessible privacy and synthetic acknowledgments, stable retry, persistent emergency action, fixture-vs-real explanation, normalized process-stage display, and no result/reference persistence. |
| `TH-SP21-005` | Keep applicant resume coarse and keep assurance level, evidence/government/biometric collection, authoritative query, notification, redress, authenticator, identity proofing, patient/account, consent, acceptance, request/queue, clinical, downstream, integration, and external consequences false. |
| `TH-SP21-006` | Prove adapter minimization, fixed metadata, positive-prerequisite gating, provenance, replay-before-adapter, contention, append-only evidence, response/resume minimization, zero canonical/downstream delta, accessibility, migration, Graphify, and full regression. |

## 3. Normalized contract

| Field | Rule |
|---|---|
| Command | `expectedVersion`, `privacyNoticeAcknowledged=true`, and `syntheticDataConfirmed=true` only. |
| Inquiry | Opaque applicant/evidence references, configured practice/facility/state, fixed proofing profile and notice, and server time; no raw person, insurance, evidence, or device data. |
| Compatibility | `NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY`; no conformance, certification, DIAS, or IAL claim. |
| Method | `SYNTHETIC_REMOTE_UNATTENDED_NON_BIOMETRIC`; a label for the exercise, not a performed proofing type. |
| Transport | `SimulatedCompleted`. |
| Evidence collection | `FixtureReferenceAccepted`; no identity evidence was actually collected. |
| Evidence and attribute validation | `ValidatedFixture`; no issuing or authoritative source was contacted. |
| Applicant verification | `VerifiedFixture`; no evidence ownership or personhood was established. |
| Fraud review | `NoIndicatorFixture`; no fraud program, death-record, device, carrier, or third-party check ran. |
| Business outcome | `SyntheticProofingPassed`; `assuranceLevelAchieved=None` and `identityProofed=false` remain invariant. |
| Freshness | Opaque request/response/session/evidence references plus fixed effective dataset and a 15-minute result window. |

## 4. Entry gate

The exercise is available only when all existing applicant ownership, no-candidate review, telehealth-eligible universal safety, controlled visit-purpose, protected synthetic insurance-receipt, and freshness rules pass, and the exact bound normalized upstream evidence says:

- member eligibility `Active`;
- benefit information `Reported`;
- eligibility business outcome `EligibleBenefitsReported`; and
- practice/facility/service network `PracticeInNetworkAcceptingNewPatients` with checked/in-network/accepting flags true.

Unknown, inactive, subscriber-not-found, unavailable, out-of-network, stale, expired, mismatched, or cross-applicant evidence never enters the adapter.

## 5. Exit boundary

Sprint 21 ends at normalized synthetic process evidence. Real vendor selection and due diligence, practice statement/DIAS, privacy and fraud risk assessment, evidence strength and core-attribute policy, government-identifier handling, authoritative/issuing sources, non-biometric/biometric pathways, PAD/injection controls, notification/redress, webhook verification, authenticator binding, real identity assurance, patient matching/promotion, portal identity, consent, practice acceptance, rendering-clinician verification, request creation, and queue entry remain unavailable and separately gated.
