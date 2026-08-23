# P2-09-F002 — The default verification gate omits risk-shaped runtime and browser suites

- Status: validated
- Domain(s): 09, 10
- Coverage item(s): `COV-007`, `COV-010`, `COV-014`, `COV-017`
- Severity: medium
- Production blocker: no by itself
- Reach: systemic
- Confidence: high
- Reviewer: `phase2_quality_operations`
- Independent verifier: `phase2_verifier`
- Specialist validation: none for the gate inventory; accessibility, recovery, clinical, security/privacy, and certification/interoperability adequacy retain their normal specialist dependencies
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

## Condition

The public pull-request workflow builds the API and both frontends, runs the modern UI unit suite, and verifies generated public history. It does not run the repository's API/database workflow scripts, Playwright end-to-end specs, accessibility and recovery suites, lint, C# formatting or a separate analyzer-policy gate, or backup/restore rehearsal. The API has no automated .NET test project in the solution.

## Evidence

- `.github/workflows/verify.yml:21-24,45-50,61-64` defines the enforced checks.
- The repository contains 31 modern UI unit/component test files, 19 Playwright end-to-end specifications, and 42 `Test-*.ps1` scripts: 36 under `avenchart/` and six under `avenchart-ui/`.
- `avenchart-ui/package.json:10-22` exposes lint, end-to-end, workflow, mutation-workflow, accessibility, quality, route-smoke, and repeatability commands; the workflow invokes only `npm test` and build.
- Searches found one C# project and no .NET test SDK or xUnit, NUnit, or MSTest project. The workflow contains no `dotnet test`, Playwright, PowerShell test, lint, format, or separate analyzer invocation. The normal C# build and its default diagnostics do run.
- A current Release build passed with zero warnings/errors, and all 178 modern UI tests in 31 files passed.
- When the deeper retained gates were run against a fresh PostgreSQL dataset, the broad smoke gate passed 157 of 207 checks and failed 50; the accessibility gate passed six of eight scenarios but two fixture calls returned 400 before scanning; the material-workflow suite passed 54, skipped six, and timed out four cross-browser medication-link scenarios; the isolated mutation suite failed cleanup in its first scenario and did not run the remaining ten.
- Pilot C ran green focused checks while a deterministic UI-to-database laboratory vocabulary mismatch remained undetected because the workflow proof inserted the downstream spelling directly.
- COV-007 found no default-gate scenarios for controlled discrepancy partial commits, dual-user credential attestation, repeated synthetic billing postings, EOB sequence allocation, direct-versus-governed report scope, queued-run source mutation, worker/retention failures, or billing/report UI recovery.
- COV-010 found no default-gate scenarios for OpenAPI response/security metadata, FHIR R4 validation and pagination, merged/lifecycle FHIR projections, changed-payload idempotency collisions, queue progress, lease overlap, or crash-after-transport-success recovery.
- Full methods, commands, results, and limits are preserved in [EXT-S001 Packet 3](../external-feedback/ext-s001-packet-3-independent-evidence-modernization-claims.md).

Expected result is not that every check run on every change. It is a documented, reproducible, risk-shaped gate model that identifies which suites protect which merge or release decision, with freshness and failure ownership.

## Consequence

A change can pass the visible default gate without exercising backend behavior, database invariants, real browser workflows, accessibility failure recovery, migration/recovery, or backup/restore. Existing deep tests can drift or fail without affecting the default repository result. The evidence does not show that such a regression has merged or reached production.

## Cause and reach

Phase 1 accumulated extensive workflow scripts around a lightweight build-and-unit workflow. The omitted surfaces span clinical, security, data, API, UI, accessibility, and operations behavior, making the verification-governance condition systemic even though individual test suites may be strong.

## Risk calibration

- Impact: undetected regression, stale verification, false confidence, or late discovery during manual assessment or release work
- Likelihood or preconditions: a change affects behavior outside compilation and modern UI unit coverage, and no external required gate runs the applicable suite
- Detectability: low at pull-request time; higher during manual workflow, specialist review, or runtime rehearsal
- Reversibility: code regressions may be easy or difficult depending on data and deployment impact; the gate structure itself is readily changeable
- Severity rationale: medium because the gap spans material surfaces, moderated by clean builds, a green 178-test UI suite, extensive available scripts, synthetic data, and no evidence of production use

## Uncertainty and counterevidence

The repository has much more verification than the default workflow executes. Runtime replay now establishes that several deeper suites have drifted or short-circuit before completing, while build/unit/lint/bundle checks remain green. Some PowerShell suites require containers, synthetic data, platform-specific tooling, or longer runtimes and may be intentionally manual. Branch protection, scheduled jobs, private release gates, freshness policy, runtime stability, and per-suite cost are unknown. A risk-based tiering exercise could show that only a subset belongs on pull requests.

## Validation record

- Independent method: separate workflow trace, project/test inventory, package-script comparison, focused test replay, and calibration cross-check
- Result: `corroborated`
- Reviewer agreement or dispute: agreement after narrowing from “tests are unreliable” to an unenforced and incompletely independent evidence model
- Specialist conclusion or outstanding need: database/operations and affected domain specialists must approve the eventual gate tiers and release evidence

## Disposition

Validated from `EXT-S001-C05` and broadened by COV-010. No Phase 3 recommendation is accepted. The finding does not require every deep suite to run on every pull request and does not prescribe a CI vendor or test framework.
