# Phase 2 — Iteration 02: current implementation reassessment

- **Status:** Recorded; not converged
- **Reviewed on:** 2026-08-23
- **Fixed Phase 1 baseline:** `phase-1-experimental^{}` = `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- **Implementation target reviewed:** `af0f321f6eb215384dff7c1dd882d39ea973be1a` (`codex/ef-data-access-modernization`)
- **Assessment boundary:** Read-only product review. Workbench and assessment records may change; application, database, deployment, test, and runtime implementation did not change in this iteration.
- **Coverage:** `COV-001`, `COV-002`, `COV-008`, `COV-010`, `COV-011`, and `COV-014`–`COV-019`

## Purpose

Re-evaluate the current implementation after Phase 3-oriented changes rather than assuming that the original Phase 2 findings remain true or have been resolved. This record is an iteration over the Phase 1 assessment, not a replacement for the original finding register, scorecard, specialist decisions, or exit gate.

## Methods and limits

The review compared the current target with the fixed Phase 1 baseline; traced host composition, identity, browser-session, FHIR, persistence, UI-storage, and verification paths; inspected the current CI configuration; and performed focused synthetic checks.

| Check | Result | Limit |
| --- | --- | --- |
| `dotnet build avenchart/AvenChart.slnx --configuration Release --no-restore` | Passed with 0 warnings and 0 errors | Build success does not establish workflow or production correctness. |
| `dotnet format avenchart/AvenChart.slnx --verify-no-changes --no-restore` | Passed | Formatting is not an architectural-quality proxy. |
| `npm test -- --run` in `avenchart-ui` | 205/206 passed; `GovernedReportExecution.test.tsx` failed only in the full suite and passed alone | The root cause of the suite interaction was not isolated in this iteration. |
| `npm run lint` in `avenchart-ui` | Passed | Lint does not exercise runtime behavior. |
| `npm run build` in `avenchart-ui` | Inconclusive: existing `dist/assets` Windows file lock (`EPERM`) | The failure was environmental; it is not recorded as a product defect. |
| Production configuration exercise | Current host started in `Production` with `IdentityProvider:Mode=local` | Synthetic local process only; it proves the fail-open configuration condition, not a deployed breach. |
| SMART discovery probe | `/api/fhir/R4/.well-known/smart-configuration` returned 404 | Does not replace formal interoperability or profile validation. |

## Material improvements corroborated

| Area | Current evidence | Effect on the original assessment |
| --- | --- | --- |
| API composition | `Program.cs` is 671 lines and maps 29 focused endpoint modules; the former roughly 8,900-line host hotspot was decomposed. | `P2-01-F001` is materially improved; retain a future maintainability check rather than carrying the original host-file condition forward unchanged. |
| C# governance | The formatter passes and is enforced in the pull-request verification workflow. | `P2-02-F002` is materially improved. Dense legacy code still requires normal review, but the absent formatting gate is no longer current. |
| FHIR R4 core mechanics | Read-only endpoints now use `application/fhir+json`, `OperationOutcome`, search links, and a core FHIR validator for synthetic external laboratory intake. | Narrows the representation/MIME/error portions of `P2-07-F002`; it does not prove the approved US Core/SMART target. |
| Persistence boundary | EF Core is registered against PostgreSQL through `AvenChartDbContext` for incremental entity-backed slices, while Npgsql remains used for projections, locking, integration, and bulk-oriented work. The reviewed SQL paths are parameterized; inspected structural interpolation uses private allow-lists, fixed fragments, or quoted identifiers. | Supports the approved hybrid EF/SQL posture. No evidence supports a blanket ORM conversion or a new general SQL-injection finding. |
| Verification and operations | Pull-request CI now builds/tests/formats C# and tests/lints/builds the modern UI. A separate scheduled runtime-evidence workflow exercises synthetic migration, backup/restore, FHIR laboratory intake, test OIDC, persistence, and browser checks. | Narrows `P2-09-F002`; runtime and browser evidence are still not part of the normal pull-request decision. |

## Residual findings and new candidates

| Classification | Condition | Evidence and required next step |
| --- | --- | --- |
| Residual `P2-05-F001` — High, production blocker | Production options reject `test-oidc` but permit `local`; the default mode remains local and the local staff/portal adapters are selected for that mode. | Add a production-only `oidc` startup invariant and a synthetic production-configuration test. Security/privacy and identity owners must validate the deployment contract. |
| New candidate under the identity/browser trust boundary — High candidate | Global CORS always includes six development `localhost` origins with credentials, while browser OIDC uses cookie sessions. Unsafe browser requests have an allowed-origin/CSRF check, but protected reads remain exposed to the CORS policy. | Restrict production CORS to explicit approved origins and perform an independent HTTPS browser security exercise before treating severity or exploitability as final. Do not create a canonical finding until that validation is complete. |
| Residual `P2-07-F002` — High, production blocker | The approved FHIR target is FHIR R4, US Core 9.0.0, and SMART App Launch. The current code contains no SMART discovery or scope-processing surface, and the standard discovery URL returned 404. | Select/version US Core profiles; implement SMART discovery, authorization/scope rules, and validator-backed synthetic contract tests. Interoperability, laboratory, HIM, and security review remain required. |
| Residual `P2-02-F001` — Medium | Broad repositories remain responsible for multiple workflow, mapping, transaction, and persistence concerns; `PatientPortalRepository` alone is 6,448 lines. The modern UI `api.ts` is 10,321 lines with 419 exported async functions. | Incrementally extract cohesive workflow slices at existing seams. Do not use line count alone or prescribe a blanket repository, CQRS, or EF rewrite. |
| Residual `P2-09-F002` — Medium | The full modern UI suite is not presently stable: one test fails in the full run but passes alone. Risk-shaped runtime/browser suites run on a schedule rather than as a required pull-request tier. | Isolate the full-suite interaction, then define the minimum affected-path pull-request and release evidence tier with named ownership. |

## Counterevidence and exclusions

- A recent-patient browser-storage key is not included as a current disclosure finding: it is not cleared by the clinician cleanup routine, but its writer has no supported UI call site. It should be removed or covered if that feature is connected later.
- The review did not find a new broad parameterized-SQL/ORM security or performance defect. The count of SQL statements, EF models, files, tests, or lines is not used as a quality score.
- The CORS condition is source-correlated and requires the stated independent browser exercise. No deployed exploitation or disclosure is claimed.
- The assessment cannot establish clinical safety, legal compliance, accessibility conformance, certification, or production readiness.

## Recommendation and gate effect

Iteration 2 does not create a new recommendation family. It refines the delivery order within existing proposals:

1. `P2-R001` must include production-only identity-mode and browser-origin fences.
2. `P2-R007` must include full-suite isolation and a risk-tiered runtime/browser gate.
3. `P2-R006` remains the required large interoperability packet for US Core/SMART and the profiled laboratory contract.
4. `P2-R004` and the existing architectural work retain incremental persistence and repository-boundary improvement; a blanket EF migration is not accepted.

The [Phase 2 exit gate](../phase-2-exit-gate.md) remains open. This iteration is **not converged** because it identifies material residual and candidate improvements, including two high-priority trust/interoperability areas. The next iteration follows delivery evidence for the affected packets and must independently recheck the high conditions before changing their disposition.
