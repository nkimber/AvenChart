# P2-01-F001 — API composition, transport policy, and workflow rules converge in one change hotspot

- Status: validated
- Domain(s): 01, 02, 07, 12
- Coverage item(s): `COV-001`, `COV-010`, `COV-018`
- Severity: medium
- Production blocker: no
- Reach: cross-cutting
- Confidence: high
- Reviewer: `phase2_architecture`
- Independent verifier: `phase2_verifier`
- Specialist validation: none
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

`Program.cs` is not merely large. Its 8,911 physical lines own host composition, middleware, 606 verb mappings, response and error translation, authorization and audit filters, and inline workflow preconditions across unrelated capabilities.

## Evidence

- Host composition and middleware: `avenchart/backend/src/AvenChart.Api/Program.cs:24-350`.
- Route mappings: `Program.cs:352-8751`.
- Encounter and document coordination: `Program.cs:2528-3062`.
- `HasLockingSignatureAsync` appears in 15 handler locations between `Program.cs:2336` and `:3020`.
- Witness identity rules are implemented in handlers at `Program.cs:5826-5849` and `:5886`.
- Static inspection reproduced 606 verb mappings, 27 top-level route groups, 605 named endpoints, 444 inclusive local `catch` clauses, and 779 selected `400`/`404`/`409`/problem result expressions.
- The verifier independently traced encounter-document movement at `Program.cs:2702-2761`. The handler loads two encounter projections, checks membership, enforces same-patient and locking-signature rules, resolves identity, invokes `DocumentRepository`, reloads projections, and translates failures.
- The expected target is explicit responsibility ownership, non-hidden rules, and predictable API errors: `docs/phase-2/quality-standard.md:48-50`.
- Full commands, actual results, and limits are preserved in [EXT-S001 Packet 1](../external-feedback/ext-s001-packet-1-architecture-human-traceability.md).

## Consequence

API changes converge on one file and require reviewers to navigate unrelated capabilities. Repeated workflow checks and local exception mappings enlarge the review surface and create an opportunity for policy drift. The evidence does not establish an escaped defect, measured delivery delay, or production failure.

## Cause and reach

Endpoint behavior accumulated in the host as functional parity expanded. The condition crosses most API capabilities, but handler complexity varies. Named routes, groups, shared filters, separate DTOs, and extracted endpoint modules are material compensating controls.

## Risk calibration

- Impact: maintainability, review accuracy, change isolation, and human comprehension
- Likelihood or preconditions: present when extending affected endpoints or changing boundary policy
- Detectability: high through static inspection
- Reversibility: high; the condition does not imply a data migration
- Severity rationale: material and cross-cutting without demonstrated clinical, security, correctness, or production failure

## Uncertainty and counterevidence

IDE navigation, route names, contiguous capability groups, and centralized permission filters make the file more workable than line count alone suggests. Extracted route modules and compact integration/FHIR handlers prove that Minimal APIs can support cohesive boundaries in the current stack. No timed maintainer study, merge-conflict analysis, or full semantic classification of all routes was performed.

## Validation record

- Independent method: three second-path traces, including encounter-document movement, plus independent structural counts
- Result: `corroborated` at medium severity and cross-cutting reach
- Reviewer agreement or dispute: agreement; both reviewers rejected the stronger claim that Minimal APIs are inherently unsuitable or that controllers are required
- Specialist conclusion or outstanding need: none for this structural condition

## Disposition

Validated from `EXT-S001-C01` and the supported portion of `EXT-S001-C02`. No implementation recommendation is accepted. Later recommendation work must compare the current grouped Minimal API style, cohesive endpoint modules, and other proportionate alternatives without presuming a framework rewrite.
