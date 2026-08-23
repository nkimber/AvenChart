# P2-02-F001 — Representative broad repositories combine persistence with workflow and delivery responsibilities

- Status: validated
- Domain(s): 01, 02, 04, 12
- Coverage item(s): `COV-001`, `COV-008`
- Severity: medium
- Production blocker: no
- Reach: repeated
- Confidence: high
- Reviewer: `phase2_architecture`
- Independent verifier: `phase2_verifier`
- Specialist validation: none
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

Several representative repositories own more than persistence. They combine normalization and business validation, lifecycle decisions, transaction orchestration, SQL, response-model assembly, packaging, cryptography, and workflow history across broad subcapabilities.

## Evidence

- `PatientPortalRepository.cs` has 6,819 physical lines and spans login/session, profile, appointments, clinical summaries, report generation and packaging, documents, messaging, refills, and lifecycle mutations: representative operations `:72-2167`.
- `DocumentRepository.cs` has 5,351 lines and spans routing, OCR, retention, content versions, review, signing, archive, restore, and deletion: representative operations `:71-3555`.
- `BillingRepository.cs` has 4,260 lines and spans statements, delivery, collections, claims, adjudication, payments, reversals, EOB import, and deletion: representative operations `:28-2490`.
- `PatientRepository.cs` has 4,065 lines. Registration validation and normalization share the repository with persistence: `:915-980` and `:3480-3623`.
- The verifier independently traced `DocumentRepository.UpdateMetadataAsync` at `:2469-2682` and reproduced validation, transaction ownership, cross-table patient and encounter checks, mapping, mutation, audit-event construction, and response assembly.
- Reciprocal source references were reproduced between representative `Data` and `Security`, `Infrastructure`, and `Workflows` files. These are source-level dependencies inside one project, not proof of an assembly or runtime cycle.
- The adopted target requires rules not to be hidden in persistence and ownership to be explicit: `docs/phase-2/quality-standard.md:48-49`.
- Full commands, actual results, and limits are preserved in [EXT-S001 Packet 1](../external-feedback/ext-s001-packet-1-architecture-human-traceability.md).

## Consequence

Maintainers changing the sampled patient, document, portal, or billing workflows must reason about rules, persistence details, mapping, and failure behavior in the same broad class. This increases unrelated context required for review. The finding does not infer a performance, transaction, SQL, or behavioral defect from size alone.

## Cause and reach

Repositories are organized around broad product domains, allowing many subcapabilities to accumulate behind one dependency. The condition is reproduced in four repositories and is not asserted for every repository.

## Risk calibration

- Impact: change comprehension, review scope, ownership clarity, and regression exposure
- Likelihood or preconditions: present when modifying the sampled broad workflows
- Detectability: high
- Reversibility: moderate to high; existing behavior and data boundaries would require preservation evidence
- Severity rationale: repeated material maintainability burden without a demonstrated production failure

## Uncertainty and counterevidence

Broad repositories may remain cohesive when the underlying domain is broad. `Persistence/README.md` documents deliberate EF-backed mutation and SQL projection boundaries. The compact 450-line `IntegrationRepository` is a strong counterexample with cohesive inbox/outbox responsibility, validation, idempotency, leases, transactions, and transport abstraction. This packet does not judge whether individual SQL operations should use EF Core.

## Validation record

- Independent method: independent repository responsibility inventory and `DocumentRepository.UpdateMetadataAsync` trace, using `IntegrationRepository` as counterevidence
- Result: `corroborated` at medium severity and repeated reach
- Reviewer agreement or dispute: agreement after narrowing the condition to representative broad repositories rather than every repository
- Specialist conclusion or outstanding need: [Packet 2](../external-feedback/ext-s001-packet-2-ef-core-sql-fitness.md) assessed EF Core and SQL fitness separately and did not support blanket EF adoption

## Disposition

Validated from `EXT-S001-C01`. No implementation recommendation is accepted. This finding does not prescribe a named architecture, a new layer, a blanket repository split, or greater EF adoption.
