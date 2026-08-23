# P2-09-F001 — The Phase 1 functional-coverage estimate has no reproducible capability denominator

- Status: validated
- Domain(s): 09, 12
- Coverage item(s): `COV-014`, `COV-018`
- Severity: medium
- Production blocker: no
- Reach: systemic
- Confidence: high
- Reviewer: `phase2_quality_operations`
- Independent verifier: `phase2_verifier`
- Specialist validation: none for the evidence-governance condition; domain specialists for any future capability classification
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The workbench and repository preserve an approximately 86% Phase 1 functional-coverage estimate, but no versioned capability inventory, numerator, denominator, partial-credit rule, comparison method, or independent oracle derives it.

## Evidence

- Commit `ce59034cd5fb6497510849df650acc66da9504a3` introduced the estimate through five lines of public-history prose and related styling. It added no derivation or comparison artifact.
- `NOTICE.md:19-20` pins the OpenEMR reference to release `v8_1_0` and commit `28dc4f9ba3f3d4de8324980699a072cdaf098927`; that supplies a stable upstream reference but not a capability model or equivalence rule.
- Phase 1 closure commit `7c90679` preserved the value as `functionalCoverageEstimate: 86` and `functionalCoverageQualifier: 'approximately'` at `tools/generate-history-data.mjs:140-141`; `public-history/app.js:26` renders the value.
- Targeted history and repository searches found no OpenEMR-to-AvenChart capability ledger, numerator, denominator, scoring rule, acceptance threshold, or independent oracle.
- Current `README.md:128` and `public-history/index.html:111-112,299-300` correctly narrow the claim to an approximate historical observation and state that functional breadth does not establish quality or readiness.
- Full methods, commands, results, and limits are preserved in [EXT-S001 Packet 3](../external-feedback/ext-s001-packet-3-independent-evidence-modernization-claims.md).

Expected evidence for a reproducible coverage claim is a versioned unit of comparison, population, scoring method, evidence threshold, result, and provenance that another reviewer can replay.

## Consequence

The value cannot support planning, residual-scope sizing, trend comparison, or a modernization decision with known confidence. Readers may assign more precision to a percentage than its provenance supports even when surrounding text qualifies it. This finding does not prove a defect in any specific workflow or convert the remaining 14% into a defect count.

## Cause and reach

Phase 1 prioritized autonomous functional generation and recorded a directional estimate before the Phase 2 evidence contract existed. The condition affects the project-level breadth claim. It does not establish that the estimate is wrong; it establishes that another reviewer cannot reproduce it.

## Risk calibration

- Impact: misleading prioritization, false precision, or an unsupported comparison claim
- Likelihood or preconditions: the percentage is used outside its explicit historical qualifier or treated as a reproducible baseline
- Detectability: high when provenance is requested; low for a casual reader presented with a precise percentage
- Reversibility: high through later evidence reconstruction or retirement of the numeric claim
- Severity rationale: medium because the number is prominent and cross-cutting, moderated by explicit public qualification and the absence of a production-readiness claim

## Uncertainty and counterevidence

The estimate may have been based on an informal inventory that is not preserved. Current documentation consistently calls it approximate and historical, warns against production use, and states that functional breadth does not establish quality. A future versioned ledger and independent replay could corroborate, revise, or replace the value.

## Validation record

- Independent method: separate commit-history trace, generated-data trace, repository search, and public-claim review
- Result: `corroborated`
- Reviewer agreement or dispute: agreement that the finding concerns evidence provenance, not demonstrated functional failure
- Specialist conclusion or outstanding need: domain specialists would be needed to classify and weight capabilities in any replacement measure

## Disposition

Validated from `EXT-S001-C05`. No Phase 3 recommendation is accepted. The numeric estimate remains historical context and must not be used as an independently audited measure until its evidence can be reconstructed.
