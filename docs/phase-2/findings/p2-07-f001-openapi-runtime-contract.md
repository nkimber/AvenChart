# P2-07-F001 — Generated OpenAPI does not describe the runtime contract

- Status: validated condition
- Domain(s): 01, 07, 09, 12
- Coverage item(s): `COV-001`, `COV-010`, `COV-014`, `COV-017`
- Severity: medium
- Production blocker: no by itself; conditional on OpenAPI being an external client contract
- Reach: systemic
- Confidence: high
- Reviewer: `phase2_quality_operations`
- Independent verifier: `phase2_verifier`
- Specialist validation: API architecture and contract ownership outstanding
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The development OpenAPI document inventories the API but does not describe the runtime response, error, or authentication contract. The generated document reports every operation as an unsecured, bodyless `200` response.

## Evidence

- OpenAPI is registered at `Program.cs:26` and mapped only in Development at `Program.cs:258-261`.
- Independent inspection found 658 operations, all with only a `200` response, no response content, and no security requirement or security scheme.
- No `.Produces`, `.Accepts`, `WithOpenApi`, or standard security metadata was located.
- Actual routes return materially different contracts: FHIR reads can return `404` (`Program.cs:925-952`), integration enqueue can return `201` or validation `400` (`Program.cs:5573-5591`), recovery can return `200`, `400`, or `409` (`Program.cs:5603-5628`), and authorization can return `401` or `403` (`Program.cs:8837-8872`).

## Consequence

Generated clients, contract tests, and reviewers cannot infer success bodies, authentication requirements, or failure behavior from the machine-readable description. This is a contract-evidence failure, not evidence that runtime authorization is absent.

## Cause and reach

Most handlers return untyped `IResult` branches without explicit response metadata, and custom endpoint filters are not represented as standard OpenAPI security metadata. The mismatch affects the generated description of the API as a whole.

## Risk calibration

Medium severity is appropriate while OpenAPI is development-only and its external support status is undecided. The reach is systemic and the remedy is bounded by explicit contract ownership.

## Uncertainty and counterevidence

Request bodies, parameters, route names, and operation IDs are substantially described. Runtime authorization and error handling execute independently. A qualified owner may decide the document is only a development route inventory, which would reduce the consequence but not remove the documentation mismatch.

## Validation record

Source review and live Development document inspection were independently corroborated. PostgreSQL was unavailable, but the contract mismatch does not depend on database state.

## Disposition

Validated engineering-readiness condition. No Phase 3 implementation recommendation is accepted.
