# P2-R006 — Make API, FHIR, integration, report, artifact, and configuration contracts governed

- **Status:** Proposed — target policy approved by `P2-D016`; implementation is not authorized
- **Linked findings:** `P2-01-F001`, `P2-05-F002`, `P2-05-F003`, `P2-05-F009`, `P2-05-F011`, `P2-07-F001` through `P2-07-F005`, `P2-10-F001`
- **Priority band:** Foundation
- **Size:** XL
- **Difficulty:** High
- **Confidence:** High static/runtime and approved FHIR/laboratory target; implementation, transport-adapter, and production evidence pending
- **Proposed owner:** API/interoperability and security architecture lead
- **Decision owner:** AvenChart program owner
- **Specialist approval needed:** FHIR/interoperability, security/privacy, HIM, operations, configuration governance

## Problem and evidence

Define versioned API/error/security metadata, implement a standards-conformant FHIR R4/SMART contract, expose an external laboratory-result API validated with synthetic laboratory messages, bind integration identity to authenticated sources and payload content, make report exports follow one governed scope/purpose/recipient/artifact/download lifecycle, and enforce configuration approvals where policy requires separation of duties.

The linked API/FHIR/integration/report findings establish incomplete generated OpenAPI metadata, non-conformant FHIR JSON/MIME/error behavior, unbound replay identity, no autonomous adopted transport, no external laboratory clinical-apply contract, direct report-export governance bypass, artifact lifecycle gaps, and configuration workflow that may be advisory. The adopted target makes FHIR R4/SMART and synthetic external laboratory behavior required; it does not claim a current partner integration or certification.

## Target state

Externally visible APIs publish versioned, secure, testable contracts; FHIR R4/SMART conforms to selected profiles; laboratory messages are authenticated, profile-bound, idempotent, clinically reconciled, and proven with synthetic data; and exports/artifacts/configuration follow an accountable governed lifecycle.

## Expected value

Make external contracts testable, prevent misleading interoperability claims and report disclosure, preserve idempotent provenance, and align configuration state with approved governance.

## Options considered

Compare compatibility-only local boundaries, a governed internal contract, and the required external FHIR/laboratory profiles. Acceptance requires selected FHIR/SMART profiles and validators, synthetic inbound laboratory create/correct/duplicate/replay/reconcile scenarios, negative multi-facility/purpose authorization tests, payload-bound identity tests, report artifact retention/download/recovery evidence, and an owner-approved configuration separation policy. Real partner certification remains a later evidence step; the API and synthetic contract are required now.

## Acceptance criteria

Selected FHIR/SMART profiles validate the published contract; synthetic inbound laboratory create, correction, duplicate, replay, and reconciliation scenarios pass; negative multi-facility/purpose and payload-bound identity scenarios fail closed; report artifact retention/download/recovery is proven; and the configuration separation policy has accountable approval. Real partner certification remains a later evidence step; the API and synthetic contract are required now.

## Dependencies and sequence

Define API error/version compatibility and selected FHIR/laboratory profiles in `R006-A` before public behavior changes. `R001` must establish source/resource/facility/purpose authorization; `R002-D` must establish the laboratory clinical aggregate; `R004` must support required migrations; and `R007` must provide contract/validator/recovery evidence. Only then introduce synthetic laboratory intake and any adopted dispatch behavior. Governed report/configuration work may proceed after the authorization and retention policy is accepted.

## Scope and affected contracts

- OpenAPI publication, API versioning, standardized errors, authorization metadata, and generated-client contract verification.
- FHIR R4 resources, JSON/MIME/error conventions, CapabilityStatement, SMART on FHIR authorization, profile/validator test fixtures, read/search lifecycle representation, and public interoperability documentation.
- External laboratory-result intake: authenticated source identity, FHIR R4 ServiceRequest/Specimen/DiagnosticReport/Observation mapping, correction/replay/idempotency/reconciliation behavior, and synthetic-laboratory harness. A real laboratory connection is not yet authorized or required.
- Outbox/inbox identity and payload binding, governed report definition/scope/purpose/recipient/artifact/download lifecycle, and configuration change governance. SAML and HL7 v2 partner adapters remain later decisions unless a named partner requires them.

## Delivery risk and rollback

External contracts can accidentally disclose PHI, break clients, accept ambiguous results, or create duplicate clinical evidence. Version published endpoints, require profile validation before state changes, preserve immutable inbound payload provenance where policy permits, stage compatibility adapters with expiry dates, and use idempotency/reconciliation before retry. Rollback must stop or quarantine intake safely, preserve the source evidence and audit trail, and avoid silently reverting a corrected result or report artifact.

## Size and difficulty rationale

This is Extra Large because it spans public contracts, patient identity, resource authorization, clinical laboratory semantics, asynchronous integration, reports, configuration, security, privacy, and operations. Difficulty is High: FHIR/SMART correctness and external-lab behavior must be validated against standards and synthetic exchanges, not a superficial JSON shape.

## Phase 3 change packets

1. **R006-A — API and OpenAPI contract foundation:** response/error/security metadata, versioning, contract tests, and explicit compatibility policy.
2. **R006-B — FHIR R4 and SMART read contract:** selected R4 profiles, content negotiation, OperationOutcome, CapabilityStatement, lifecycle representation, authorization, and validator evidence.
3. **R006-C — Synthetic external laboratory intake:** profile-bound source authentication, order/specimen/report/result association, correction/critical/replay handling, idempotency, reconciliation, and synthetic-lab tests.
4. **R006-D — Integration transport and evidence:** autonomous dispatch design where adopted, lease/retry/dead-letter semantics, semantic idempotency, provenance, and recovery proof.
5. **R006-E — Governed reports and configuration:** retire or isolate compatibility exports, enforce scope/purpose/recipient/artifact lifecycle, and apply the approved change-governance policy.

## Decision record

- **Decision:** Pending acceptance as a Phase 3 recommendation.
- **Decided by:** AvenChart program owner.
- **Date:** Not set.
- **Rationale and conditions:** `P2-D016` approves FHIR R4/SMART and a synthetic external-laboratory contract as targets. Acceptance requires selected profiles, source/auth model, named interoperability/security/HIM owners, compatibility and rollback policy, and the acceptance evidence above.
