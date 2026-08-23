# EXT-S001 Packet 3 — Independent evidence and modernization claims

## Packet

- Source challenges: `EXT-S001-C05`, `EXT-S001-C06`, `EXT-S001-C07`
- Status: evidence complete and independently verified
- Baseline tag: `phase-1-experimental`
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Review date: 2026-08-21
- Reviewer: `phase2_quality_operations`
- Independent verifier: `phase2_verifier`
- Evidence level: Level 0 repository, history, evidence-governance, and workflow inspection; targeted Level 1 build and unit checks
- Product worktree: `avenchart/` and `avenchart-ui/` had no diff from the fixed baseline when the packet completed
- Assessment worktree: Phase 2 documentation and workbench changes were present and are not treated as baseline product evidence
- Tool environment: Git 2.53.0.windows.1; .NET SDK 10.0.400; Node.js 24.13.1; npm 11.8.0; PowerShell 7.6.4
- Runtime limit: no PostgreSQL or browser workflow was started; database, end-to-end, accessibility, recovery, and performance suites were inventoried rather than replayed

## Scope and coverage

| Concern | Packet question |
| --- | --- |
| `COV-014` | Does the repository provide an independent, reproducible oracle for the Phase 1 functional-coverage estimate and risk-shaped verification for important behavior? |
| Assessment governance | Did Phase 1 delay feedback, and do Phase 2 calibration, packet sizing, counterevidence, verification, specialist gates, and stopping controls now limit the spread of a mistaken assumption? |
| Cross-domain synthesis | Are prototype generation activity, assessment, remediation, specialist validation, operationalization, residual risk, and ongoing ownership represented as different claims and costs? |

Domains 09 Testing and quality controls, 10 Observability and operations, and 12 Documentation and developer experience are primary. Domains 03, 05, 07, and 08 are supporting lenses only where the calibration evidence demonstrates a verification limitation; this packet does not promote those pilot observations into new clinical, security, API, or accessibility findings.

## Evidence questions

1. What versioned numerator, denominator, capability inventory, comparison method, and independent oracle produce the approximately 86% Phase 1 estimate?
2. Which checks are present, which are enforced by the default pull-request workflow, and which high-risk paths remain manually invoked or unrepresented?
3. Do green checks coexist with a deterministic defect that they bypassed, and what can be concluded without attributing causation to an agent identity?
4. Was delayed evaluation accidental or declared, and which present controls supply earlier challenge and stopping points?
5. Does the project claim that commits, elapsed time, additions, or initial generation cost establish production readiness or total modernization economics?
6. What cost categories can the Phase 2 records express, which are actually populated, and which remain unknown?

## Exclusions and limits

- This packet does not infer test independence, competence, or causation from commit authorship, agent identity, prompt history, or the presence of generated code.
- It does not treat passing tests as proof of correctness or an omitted CI job as proof that a workflow is defective.
- It does not equate the absence of mutation-testing tooling with a defect. The repository script named `test:mutation-proofs` exercises data-mutation workflows; it is not a mutation-testing score.
- It does not convert the Phase 1 86% estimate into a defect count, safety claim, or statement about OpenEMR equivalence.
- It does not estimate labor, token, cloud, specialist, remediation, certification, or ownership cost without actual records and accepted assumptions.
- The local file `avenchart/artifacts/latest-avenchart-smoke-test.json` is excluded from canonical evidence. It is ignored and untracked, so its reported 156 passed and 51 failed checks cannot be tied to the fixed baseline or independently reproduced from repository history.
- No product code, CI configuration, test, deployment, data, or recommendation was changed.

## Results

### Challenge outcomes

| Challenge | Outcome | Reconciliation |
| --- | --- | --- |
| `EXT-S001-C05` | `corroborated`, with causal narrowing | The 86% estimate is not reproducible from a versioned capability ledger or independent oracle, and calibration proves that green checks can bypass the real entry path. The repository does not establish that common agent authorship caused the blind spot or that all verification is correlated. |
| `EXT-S001-C06` | `partially corroborated` | Phase 1 intentionally placed generation before evaluation, so early architectural and risk feedback was absent by design. The number or cost of propagated mistakes is not measurable from the repository. Accepted Phase 2 calibration, bounded packets, counterevidence, independent verification, specialist gates, and stop/narrow rules directly address the prospective concern. |
| `EXT-S001-C07` | `partially corroborated` | Source activity and elapsed time do not establish production readiness or total modernization economics. Current public material already says so, and the recommendation schema separates size, complexity, coordination, migration, validation, and risk. Actual cost and ownership records are not yet populated, so a total-economics conclusion remains unavailable. |

### Functional-coverage provenance

The approximately 86% value is a historical observation, not a reproducible measurement:

- `NOTICE.md:19-20` pins the OpenEMR reference to release `v8_1_0` and commit `28dc4f9ba3f3d4de8324980699a072cdaf098927`. This is a strong provenance control for choosing a future comparison baseline, but it does not define the capability denominator or equivalence rule.
- Commit `ce59034cd5fb6497510849df650acc66da9504a3` introduced the estimate through five lines of public-history prose and related styling. It added no capability inventory, comparison dataset, denominator, calculation, acceptance rule, or independent review record.
- Phase 1 closure commit `7c90679` preserved the value as `functionalCoverageEstimate: 86` in `tools/generate-history-data.mjs:140`, with the qualifier `approximately` at `:141`. `public-history/app.js:26` renders that constant.
- History searches found no versioned OpenEMR-to-AvenChart capability ledger, numerator, denominator, scoring rule, or independent oracle that derives the value.
- Current `README.md:128` and `public-history/index.html:111-112,299-300` correctly narrow the claim: it is approximate, historical, and not evidence of quality, safety, security, privacy, accessibility, interoperability, certification, or production readiness.

The qualification prevents an over-broad readiness claim, but it does not make the number auditable. A reader cannot reproduce the estimate, determine how partial capabilities were scored, or distinguish implemented routes from successfully exercised workflows.

### Verification inventory and default gate

The repository contains meaningful verification breadth:

- one production C# project;
- 31 modern UI unit/component test files;
- 19 Playwright end-to-end specifications;
- 42 `Test-*.ps1` workflow, API, database, recovery, quality, and UI scripts: 36 under `avenchart/` and six under `avenchart-ui/`;
- modern UI scripts for workflow, mutation-workflow, accessibility, quality, route-smoke, and repeatability checks;
- deterministic synthetic data, migration checks, and guarded operations scripts.

The default workflow at `.github/workflows/verify.yml` enforces a narrower subset:

1. restore and Release build for the single API project;
2. install and build for the reference frontend;
3. install, `npm test`, and build for the modern UI; and
4. public-history regeneration and diff.

It does not invoke a .NET test project because none exists. It also does not invoke the 42 PowerShell suites, the 19 Playwright specifications, either frontend lint command, C# formatting, an explicit analyzer or warnings-as-errors policy, the accessibility scripts, database-backed workflows, migration/recovery checks, or backup/restore rehearsal. The compiler's default diagnostics remain a real build control. Repository evidence does not establish an external required check, branch-protection rule, scheduled job, or release gate that supplies the omitted controls elsewhere; their existence is therefore unknown rather than assumed absent.

Targeted Level 1 replay on 2026-08-21 produced:

```text
dotnet build avenchart/AvenChart.slnx --configuration Release --no-restore
Build succeeded. 0 warnings, 0 errors.

npm test -- --run   (working directory: avenchart-ui)
31 files passed; 178 tests passed.
```

These are material strengths and prove that the default checked subset is currently green. They do not exercise the omitted runtime and browser surfaces.

### Independent calibration evidence

The strongest evidence is a falsified verification assumption rather than a failed test count:

- Pilot C ran 79 focused tests; the independent pass ran the focused shell/queue tests and both production builds. All executed checks passed.
- Both reviewers and the verifier traced the real laboratory entry path and found that the supported UI writes `C`, persistence retains `C`, and the acknowledgement queue recognizes `critical`, `panic`, `hh`, and `ll` but not `C`.
- The existing workflow proof inserted `critical` directly, bypassing the upstream value. The green proof therefore did not cover the real cross-layer contract it appeared to represent.
- Pilots A, B, and D likewise preserved strengths and green focused checks while independent reviewers found material identity/PHI, encounter-lifecycle, and accessibility conditions. Those pilot conditions retain their own specialist and runtime limits.

This corroborates the risk of correlated verification blind spots and the need for independent, real-entry-path evidence. It does not establish why the blind spot arose, that all agent-authored tests are dependent, or that independently authored tests would necessarily have caught it.

### Feedback-loop controls

Phase 1 deliberately tested unattended generation before evaluation. The workbench records that design rather than presenting it as a mature delivery process. It therefore did not provide early human architectural, clinical, security, data, accessibility, or operational correction while the application was being generated.

Phase 2 now introduces explicit feedback boundaries:

- four calibration slices reviewed independently before the full assessment was authorized;
- bounded, non-overlapping packets with no more than three concurrent specialists initially;
- a fixed Phase 1 product baseline and read-only product boundary;
- a common evidence and finding schema, counterevidence, confidence, and unknowns;
- independent verification for blocker, high, systemic, clinical-safety, and disputed conditions;
- specialist gates for clinical, security/privacy, accessibility, legal/compliance, certification/interoperability, and high-risk operations conclusions;
- stop, narrow, and escalation rules; and
- visible scorecard, decision, finding, recommendation, and Phase 3 entry gates.

The criticism remains useful as a historical limitation and future-process guardrail. No repository evidence quantifies how many Phase 1 assumptions propagated, how much rework resulted, or whether a different feedback cadence would have been more economical.

### Modernization and cost accounting

The current records correctly distinguish activity from readiness:

| Category | Current evidence |
| --- | --- |
| Generation activity | 773 retained application-source check-ins over 63 active days, with additions/deletions and area counts explicitly described as activity rather than quality |
| Functional breadth | Approximately 86%, explicitly historical and unaudited; no reproducible capability denominator |
| Assessment | Phase 2 packets, findings, scorecard, validation needs, unknowns, and decisions are being recorded |
| Remediation | Not started; product changes remain Phase 3 work |
| Specialist validation | Required by category and still outstanding where named; no time or monetary ledger |
| Operationalization | Readiness, recovery, deployment, certification, and production-use gates remain open; no actual total-cost record |
| Residual risk | Findings and unknowns can be recorded, but no accepted production-risk position exists |
| Ongoing ownership | No measured maintenance, support, incident, upgrade, or human-oversight cost record |

The Phase 2 recommendation template separates breadth, technical difficulty, coordination, migration, specialist involvement, validation cost, delivery risk, rollback, alternatives, and acceptance evidence. That is an adequate schema for later decision-making, but a schema is not cost data. No conclusion about total modernization economics can be made until those categories are populated against an accepted target use and time horizon.

This is retained as a program-level opportunity rather than a canonical defect finding. The project makes no total-cost or economic-superiority claim, and precise remediation estimates would be premature before Phase 2 identifies and sequences the work. If the program owner makes modernization economics an explicit decision output, the record will need estimated and actual units, assumptions, time horizon, variance, and ownership for each category above.

## Candidate finding 1 — The Phase 1 functional-coverage estimate has no reproducible capability denominator

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

### Condition

The workbench and repository preserve an approximately 86% Phase 1 functional-coverage estimate, but no versioned capability inventory, numerator, denominator, partial-credit rule, comparison method, or independent oracle derives it.

### Evidence

- Commit `ce59034cd5fb6497510849df650acc66da9504a3` introduced the estimate as prose and styling only.
- `tools/generate-history-data.mjs:140-141` stores `86` and `approximately` as constants; `public-history/app.js:26` renders the value.
- Targeted history and repository searches found no derivation artifact or OpenEMR-to-AvenChart capability ledger.
- Current public text correctly says the estimate is historical and does not establish quality or readiness.

Expected evidence for a reproducible coverage claim is a versioned unit of comparison, population, scoring method, evidence threshold, result, and provenance that another reviewer can replay.

### Consequence

The value cannot support planning, residual-scope sizing, trend comparison, or a modernization decision with known confidence. Readers may assign more precision to a percentage than its provenance supports even when surrounding text qualifies it.

### Cause and reach

Phase 1 prioritized autonomous functional generation and recorded a directional estimate before the Phase 2 evidence contract existed. The condition affects the project-level breadth claim rather than proving a defect in any particular workflow.

### Risk calibration

- Impact: misleading prioritization, false precision, or an unsupported comparison claim
- Likelihood or preconditions: the percentage is used outside its explicit historical qualifier or treated as a reproducible baseline
- Detectability: high when provenance is requested; low for a casual reader presented with a precise percentage
- Reversibility: high through later evidence reconstruction or retirement of the numeric claim
- Severity rationale: medium because the number is prominent and cross-cutting, moderated by explicit public qualification and the absence of a production-readiness claim

### Uncertainty and counterevidence

The estimate may have been based on an informal inventory that is not preserved. Current documentation consistently calls it approximate and historical, warns against production use, and states that functional breadth does not establish quality. A future versioned ledger and independent replay could corroborate, revise, or replace the value.

### Validation record

- Independent method: separate commit-history trace, generated-data trace, repository search, and public-claim review
- Result: `corroborated`
- Reviewer agreement or dispute: agreement that the finding concerns evidence provenance, not demonstrated functional failure
- Specialist conclusion or outstanding need: domain specialists would be needed to classify and weight capabilities in any replacement measure

### Disposition

Assigned canonical ID `P2-09-F001`. No Phase 3 recommendation is accepted. The finding does not invalidate Phase 1 or convert 14% into a defect estimate.

## Candidate finding 2 — The default verification gate omits risk-shaped runtime and browser suites

- Status: validated
- Domain(s): 09, 10
- Coverage item(s): `COV-014`, `COV-017`
- Severity: medium
- Production blocker: no by itself
- Reach: systemic
- Confidence: high
- Reviewer: `phase2_quality_operations`
- Independent verifier: `phase2_verifier`
- Specialist validation: none for the gate inventory; accessibility, recovery, clinical, security/privacy, and certification/interoperability adequacy retain their normal specialist dependencies
- Baseline commit: `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21

### Condition

The pull-request workflow builds the API and both frontends, runs the modern UI unit suite, and verifies generated public history. It does not run the repository's API/database workflow scripts, Playwright end-to-end specs, accessibility and recovery suites, lint, C# formatting or a separate analyzer-policy gate, or backup/restore rehearsal. The API has no automated .NET test project in the solution.

### Evidence

- `.github/workflows/verify.yml:21-24,45-50,61-64` defines the enforced checks.
- The repository contains 31 modern UI unit/component files, 19 Playwright specifications, and 42 `Test-*.ps1` scripts.
- `avenchart-ui/package.json:10-22` exposes lint, end-to-end, workflow, accessibility, quality, route-smoke, and repeatability commands; the workflow invokes only `npm test` and build.
- Searches found one C# project and no `Microsoft.NET.Test.Sdk`, xUnit, NUnit, MSTest, Stryker, `dotnet test`, Playwright, PowerShell test, lint, format, or separate analyzer invocation in the default workflow. The normal C# build and its default diagnostics do run.
- A current Release build passed with zero warnings/errors, and all 178 modern UI tests passed.
- Pilot C demonstrates why the distinction matters: green focused checks bypassed a deterministic real-entry-path vocabulary mismatch.

Expected result is not that every check run on every change. It is a documented, reproducible, risk-shaped gate model that identifies which suites protect which merge or release decision, with freshness and failure ownership.

### Consequence

A change can pass the visible default gate without exercising backend behavior, database invariants, real browser workflows, accessibility failure recovery, migration/recovery, or backup/restore. Existing deep tests can drift or fail without affecting the default repository result. The evidence does not show that such a regression has merged or reached production.

### Cause and reach

Phase 1 accumulated extensive workflow scripts around a lightweight build-and-unit workflow. The omitted surfaces span clinical, security, data, API, UI, accessibility, and operations behavior, making the verification-governance condition systemic even though individual test suites may be strong.

### Risk calibration

- Impact: undetected regression, stale verification, false confidence, or late discovery during manual assessment or release work
- Likelihood or preconditions: a change affects behavior outside compilation and modern UI unit coverage, and no external required gate runs the applicable suite
- Detectability: low at pull-request time; higher during manual workflow, specialist review, or runtime rehearsal
- Reversibility: code regressions may be easy or difficult depending on data and deployment impact; the gate structure itself is readily changeable
- Severity rationale: medium because the gap spans material surfaces, moderated by clean builds, a green 178-test UI suite, extensive available scripts, synthetic data, and no evidence of production use

### Uncertainty and counterevidence

The repository has much more verification than the default workflow executes. Some PowerShell suites require containers, synthetic data, platform-specific tooling, or longer runtimes and may be intentionally manual. Branch protection, scheduled jobs, private release gates, freshness policy, runtime stability, and per-suite cost are unknown. A risk-based tiering exercise could show that only a subset belongs on pull requests.

### Validation record

- Independent method: separate workflow trace, project/test inventory, package-script comparison, focused test replay, and calibration cross-check
- Result: `corroborated`
- Reviewer agreement or dispute: agreement after narrowing from “tests are unreliable” to an unenforced and incompletely independent evidence model
- Specialist conclusion or outstanding need: database/operations and affected domain specialists must approve the eventual gate tiers and release evidence

### Disposition

Assigned canonical ID `P2-09-F002`. No Phase 3 recommendation is accepted. The finding does not require every deep suite to run on every pull request and does not prescribe a CI vendor or test framework.

## Material strengths and counterevidence

- Phase 1 preserved a broad set of focused workflow scripts, deterministic synthetic data, database migration checks, modern UI tests, and browser scenarios.
- The current Release build and all 178 modern UI tests are green.
- Public documentation prominently prohibits production clinical use and distinguishes functional breadth and source activity from quality.
- Calibration used independent reviewers and verifiers to find conditions that existing proofs missed and then strengthened the assessment contract.
- Phase 2 requires real-entry-path tracing, counterevidence, confidence, unknowns, and specialist validation rather than treating test count as a score.
- The accepted recommendation template separates value, effort, difficulty, dependencies, risk, alternatives, and proof; Phase 3 cannot start until recommendations and ordering are approved.

## Unknowns and next evidence

1. Build a versioned capability ledger and independently sample its scoring before using a functional-coverage percentage for planning or comparison.
2. Classify each existing suite by risk protected, environment, duration, stability, owner, required frequency, and retained evidence; do not assume all belong on every pull request.
3. Trace representative clinical, identity, API, data, and accessibility values from their real entry path through downstream behavior and compare fixtures with production vocabulary.
4. Establish whether required checks, schedules, or release gates exist outside the public workflow.
5. Define a target modernization decision and time horizon, then record generation, assessment, remediation, specialist, operationalization, residual-risk, and ownership costs separately.
6. Keep the ignored local smoke artifact excluded unless a reproducible invocation, environment, baseline, and retained result are established.

## Coverage and scorecard impact

- `COV-014` advances to `In review` with material independently verified evidence; it remains incomplete because runtime, browser, accessibility, mutation sensitivity, and risk-tier execution are not broadly replayed.
- `COV-017` and `COV-018` gain limited gate, provenance, and documentation evidence; no row is complete.
- Domain 09 receives two validated medium findings plus material verification strengths and remains in assessment.
- Domain 10 receives gate and recovery-evidence limits only; operational readiness remains unassessed.
- Domain 12 gains a validated coverage-provenance condition and evidence that current qualification is materially accurate.
- No final rating, production-readiness, clinical-safety, compliance, modernization-economics, or Phase 3 authorization follows.

## Integrity statement

Packet 3 changed only Phase 2 assessment records and the workbench. Product paths remain identical to the fixed Phase 1 baseline. The two findings describe evidence governance and enforced verification scope; they do not reinterpret omitted checks as known product defects or the historical 86% estimate as a measured failure rate.
