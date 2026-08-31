# Phase 2 — Iteration 03: generated-code quality and maintainability

- **Status:** Recorded; not converged
- **Reviewed on:** 2026-08-31
- **Fixed Phase 1 baseline:** `phase-1-experimental^{}` = `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- **Implementation target reviewed:** `10fbf3940259c3176c7d86ff72de273e84093adf` (`main`, `chore(graph): refresh code index`)
- **Assessment boundary:** Source inspection plus local static verification. This iteration records a narrow quality-policy implementation that leaves product behavior, database behavior, deployment topology, dependencies, and lockfiles unchanged.
- **Coverage:** `COV-001`, `COV-008`, `COV-009`, `COV-011`, `COV-012`, `COV-014`, `COV-015`, `COV-017`, and `COV-018`

## Purpose

Assess the generated code for readable ownership, repetition, and mechanically enforceable hygiene; establish proportionate quality guardrails; and distinguish proven conditions from refactoring preferences. The review does not treat file length, code generation, parameterized SQL, EF adoption, or a green static check as a quality score.

## Method and current evidence

Graphify was used only for code navigation, then conclusions were checked against source. The code-only graph is not evidence of security, clinical safety, accessibility, performance, or production readiness. Read-only specialists independently reviewed architecture, data/persistence, modern UI, transport, and quality operations.

| Check | Result | Limit |
| --- | --- | --- |
| `dotnet build avenchart/AvenChart.slnx -c Release --no-restore` | Passed with 0 warnings and 0 errors | Does not prove workflow or runtime correctness. |
| `dotnet format avenchart/AvenChart.slnx --verify-no-changes --no-restore` | Initially identified whitespace drift in three Telehealth files; passed after a formatting-only repair | Formatter agreement is not an architecture assessment. |
| `dotnet format analyzers avenchart/AvenChart.slnx --verify-no-changes --no-restore` | Passed before the explicit quality-policy change and is retained as a CI check | A clean analyzer baseline does not prove that every useful rule family is enabled. |
| `npm run lint` in `avenchart-ui` | Passed | Syntactic lint is not browser behavior or accessibility conformance. |
| `npm run lint` in `avenchart/frontend` | Passed; Babel reported a deoptimized styling note for the large reference `App.tsx` | The reference UI is excluded from supported production scope. |
| `tsc -b --pretty false` in each UI | Passed | Compiler success is not a UI workflow test. |
| Focused transport tests | `transport.test.ts` 5/5 and selected `api.test.ts` 2/2 passed | These are focused unit tests, not an end-to-end IdP/browser exercise. |

## Current strengths and delivered guardrails

- The existing pull-request workflow builds, tests, formats C#, and lints/builds both UIs. This iteration adds explicit C# analyzer verification to that workflow.
- The repository now declares a small C# formatting contract in `.editorconfig` and makes the SDK analyzer/code-style setting explicit through `avenchart/Directory.Build.props`. The policy deliberately avoids `AnalysisMode=All`, blanket warnings-as-errors, and a bulk rewrite.
- A reporting probe exposed three existing `IDE0060` unused-parameter diagnostics and the SDK's documentation-file prerequisite for `IDE0005`. They are recorded as baseline information rather than immediately promoted to build warnings; a future rule family needs an owner, remediation scope, and ratchet decision.
- Both UIs expose a named `npm run typecheck` command. Their builds already run TypeScript checks; the named command makes the independent check discoverable without lengthening the default pull-request path unnecessarily.
- The three formatter-only Telehealth repairs restore the existing gate without changing product logic.

## Reconciled observations

| Classification | Observation | Disposition and next evidence |
| --- | --- | --- |
| Existing `P2-02-F002` | The Phase 1 baseline lacked a consistent C# formatting/analyzer policy. Current CI now enforces formatting; this iteration repaired three current whitespace regressions and adds explicit analyzer verification and small conventions. | Materially improved; retain the residual policy review. Add rule families only from a reporting baseline owned by maintainers. |
| Existing `P2-02-F001` | Current Telehealth request-step persistence repeats transaction, applicant-lock, access, idempotency, event, and projection templates. `LoadApplicantAsync`, `LoadContextAsync`, and `InsertRequestEventAsync` recur across many repositories. | Corroborates the existing repository-ownership finding. Build an obligation matrix and run synthetic tests before extracting any helper; a generic repository or blanket EF conversion is not supported. |
| Existing `P2-03-F007` and `P2-08-F006` | Portal workflows have several locally implemented async-state/retry/error patterns. | Candidate broadening only. Compare representative loading, cancellation, retry, and error behavior before selecting a shared abstraction. |
| Existing `P2-01-F001` | `avenchart-ui/src/api.ts` remains a large compatibility client used by many routes. Its `fetch` identifier is an alias for the governed `apiFetch` transport rather than a direct browser bypass. | Human-traceability opportunity only; no security finding. Measure change locality before any split. |
| Existing `P2-05-F005` candidate | Static inspection suggests clinician BFF logout may omit the staff-session header required for automatic CSRF proof insertion and could receive a safe 403 instead of clearing cookies. | Unvalidated. Reproduce using only the disposable Docker/test-IdP environment, with valid-CSRF and portal-signout counterexamples, before assigning a finding or severity. |
| Existing `P2-04-F001` | Empty-database bootstrap is stronger than the Phase 1 baseline, but source inspection cannot prove a sole schema authority or full-shape drift detection. | No new finding. Retain database recovery and full-shape validation work. |

## Deliberate non-actions

- Do not adopt EF Core more broadly as a general remedy. The sampled hybrid boundary uses EF for bounded entity state/concurrency and parameterized SQL where PostgreSQL locking, idempotency, queues, projections, or cross-table transactions are clearer.
- Do not add Prettier or type-aware ESLint merely to add tools. Pilot those only if incremental findings are actionable and their duration fits an agreed CI tier.
- Do not mechanically split large files, normalize every duplicate, or silence every React Hooks suppression. New suppressions should carry a local rationale and focused test; current suppressions need a separate representative review.
- Do not claim accessibility conformance, security compliance, performance, or production readiness from this iteration.

## Recommendation and gate effect

This iteration does not create a new canonical finding or recommendation family. It strengthens the evidence and delivery path for existing `P2-R004` (incremental structural/persistence boundaries) and `P2-R007` (risk-tiered verification and release operations).

The Phase 2 exit gate remains open. Iteration 03 is not converged because it retains material structural work, a narrow unvalidated logout candidate, schema-authority/recovery evidence needs, and the existing specialist/runtime gates. A subsequent iteration should record the required runtime reproduction, a scoped persistence obligation matrix, and measured effects of any accepted boundary change.
