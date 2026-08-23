# COV-012–COV-019 — Residual coverage and Phase 2 readiness reconciliation

## 1. Scope and coverage

- **Primary coverage:** `COV-001`, `COV-008`, `COV-009`, `COV-012`, `COV-013`, `COV-014`, `COV-015`, `COV-016`, `COV-017`, `COV-018`, and `COV-019`
- **Supporting findings:** `P2-03-F007`, `P2-08-F006`, `P2-09-F002`, `P2-04-F001`, `P2-01-F001`, `P2-02-F001`, `P2-02-F002`, and `P2-09-F001`
- **Domains:** 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12
- **Surfaces traced:** API host and middleware; EF Core and SQL registration; schema/migration/seed and recovery scripts; patient portal pages and tests; reference interface; CI and UI verification scripts; local Docker scripts; Azure operations templates and documentation; package manifests, lockfiles, licenses, notices, and public-history/documentation artifacts; external laboratory scope.
- **Exclusions:** production deployment, real patient data, real credentials, destructive resets outside the documented synthetic workflow, formal legal/compliance certification, WCAG certification, clinical-policy approval, and external laboratory integration claims.

## 2. Baseline and methods

- Fixed Phase 1 baseline: `phase-1-experimental^{}` = `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`.
- Working tree was checked before review; application/database/deployment/test implementation remained read-only.
- Static traces used repository search, source inspection, manifest/lockfile inspection, workflow inspection, and documentation reconciliation.
- Initial Level 1 checks passed: .NET Release build with zero warnings/errors, modern UI build/bundle budget, and 31 UI test files with 178 tests.
- The later [runtime-readiness packet](cov-014-019-runtime-readiness.md) supersedes the initial environment limitation: Docker/PostgreSQL, migration/bootstrap, backup/restore, browser gates, FHIR/integration, identity scope, and the existing Azure demo were exercised with synthetic/read-only evidence.

## 3. Material strengths

- The API host registers Problem Details, response compression, health/readiness checks, rate limiting, CORS, request correlation, scoped repositories, EF Core, parameterized SQL, and a hosted report worker in one explicit startup pipeline (`Program.cs:26-43,115-178,181-228,260-369`).
- The hybrid data boundary is documented and evidenced: EF Core is used for ordinary entity/state work while SQL remains explicit for reporting, locks, leases, bulk work, and PostgreSQL-specific behavior. No general “use more EF” defect was established.
- Local startup/reset scripts state synthetic-only intent, preserve database volumes on normal stop, and put destructive reset behind an explicit `-Force` switch (`scripts/README.md`, `Reset-AvenChartDemoData.ps1`).
- Azure operations documentation describes private networking, Key Vault references, managed identity, HTTPS ingress, health probes, migration jobs, revisions, rollback, backup retention, access-code gating, and a disabled-by-default deployment execution setting (`infra/azure/operations/README.md`). These are design controls, not deployed evidence.
- Portal session loading uses patient-bound sessions, cancellation for the shell bootstrap, invalid-session handling, local cleanup on sign-out, appointment request history, and a tested modal focus trap/restoration (`PortalShell.tsx`, `PortalAppointments.tsx`, `PortalShell.test.tsx`, `PortalAppointments.test.tsx`).
- Package manifests, lockfiles, GPL notices, repository metadata, and frontend/backend build workflows are versioned. CI builds both frontends and the backend and regenerates public history.
- The reference interface is explicitly retained as a compatibility/reference surface rather than silently treated as the modern clinician UI. Its scope and production role remain an owner decision.

## 4. Candidate findings and broadenings

### Existing `P2-03-F007` — broaden to residual portal loads

- **Status:** supported broadening
- **Severity/blocker:** retain High / Yes from the canonical finding
- **Reach:** adds portal dashboard message preview, portal records’ parallel document/lab/health/refill loads, and portal thread selection to the existing patient, clinician, billing, inventory, therapy, and communication response-ownership root.
- **Evidence:** `PortalDashboard.tsx:71-82` loads recent messages without cancellation; `PortalMessages.tsx:156-223,340-424` loads inbox/thread state without request identity; `PortalRecords.tsx:292-348` starts several independent loads without shared cancellation or ownership. The patient-bound API/session controls are real counterevidence, but they do not make stale UI state coherent.
- **Validation needed:** deferred-response browser scenarios across portal navigation, thread selection, and records tab loads.

### Existing `P2-08-F006` — broaden to portal failure announcement/recovery

- **Status:** supported broadening; retain Medium / blocker Unknown
- **Reach:** `PortalDashboard`, `PortalMessages`, `PortalRecords`, and `PortalAccount` contain page-specific visual error states with inconsistent `role`, live-region, focus, and retry semantics. `PortalShell` and `PortalAppointments` provide stronger alert/retry patterns.
- **Evidence:** `PortalDashboard.tsx:213-221`, `PortalMessages.tsx:479-497,787-797`, `PortalRecords.tsx:612-711,898-910,997-1000`, and `PortalAccount.tsx:116` include error rendering that is not consistently programmatically announced or paired with retry.
- **Validation needed:** keyboard and screen-reader failure/recovery runs; no formal WCAG conclusion is made.

### COV-017 opportunity — release dependency and provenance evidence

- **Status:** opportunity / evidence gap, not a new canonical defect
- **Observation:** project and package manifests carry license metadata and lockfiles, but no repository-visible SBOM, dependency-vulnerability gate, license-policy report, signed release provenance, or reproducible release artifact record was found.
- **Counterevidence:** clean builds succeed; package locks are committed; GPL and third-party notices exist; CI has read-only permissions and deterministic history generation.
- **Disposition:** retain as a Phase 3 readiness recommendation candidate only after release owners define the required artifact, license, vulnerability, and provenance policy.

### COV-013 and COV-019 — scope dispositions

- The reference interface is excluded from supported production scope by `P2-D014`; assessment and future implementation focus on `avenchart-ui` clinician and patient-portal routes.
- External laboratory-result ingestion is required by `P2-D014`. Synthetic runtime confirmed that the generic inbox does not apply laboratory results and accepts divergent replay as a duplicate; the absent required boundary is recorded as blocker `P2-07-F005`.

## 5. Unknowns and counterevidence

- Empty-database migration bootstrap failed at the seed-owned `facilities` dependency, while seed-first resilience and backup/restore passed. Representative query plans, lock contention, and failover remain unmeasured.
- Azure templates and operations guidance are not proof of a deployed or approved production topology, certificate, gateway, secret, backup, recovery, or monitoring configuration.
- CI does not run the 19 Playwright specifications or the 42 PowerShell workflow/recovery scripts; risk-shaped omissions remain represented by `P2-09-F002`.
- No manual assistive-technology certification or controlled deferred-response browser run was available.
- No external laboratory partner contract, selected FHIR certification profile, SMART identity contract, or inbound clinical apply path was found; FHIR and external lab are required by `P2-D014`.
- No SBOM, dependency vulnerability scan, signed release artifact, or independent license review was found in repository-visible governance; this remains an opportunity until policy is defined.
- Green build/test results establish repeatable local engineering evidence, not production readiness.

## 6. Specialist validation required

- Database/operations: migration bootstrap, backup/restore, failover, query/lock/timeout behavior, worker recovery, and deployment topology.
- Security/privacy/legal/HIM: identity provider, minimum necessary scope, report/artifact retention, browser persistence, audit correlation, and release policy.
- Clinical, scheduling, laboratory, and interoperability owners: lifecycle, result release, follow-up, FHIR, external lab, and portal workflow decisions.
- Accessibility specialist: WCAG 2.2 AA manual keyboard, screen-reader, zoom/reflow, focus, contrast, and failure-announcement validation.
- Release/supply-chain owner: SBOM, dependency/license policy, signed provenance, artifact retention, and clean-checkout release evidence.
- Program owner: reference-interface support status, external laboratory scope, production topology, and risk acceptance or Phase 3 authorization.

## 7. Coverage and scorecard impact

- `COV-012` receives a bounded portal evidence packet and remains **In review**; `P2-03-F007` and `P2-08-F006` are broadened.
- `COV-013` is **Excluded** by approved scope; `COV-019` has evidence complete for the missing required external API and links to `P2-07-F005`.
- `COV-009`, `COV-010`, `COV-014`, `COV-015`, and `COV-017` now have representative runtime evidence complete. `COV-016` has bounded live-demo evidence and remains In review for a current production topology. `COV-001`, `COV-008`, `COV-012`, and `COV-018` remain open where human/specialist or broader evidence is still required.
- No new canonical high or blocker finding is created by this residual packet. The existing blocker set and the two conditional COV-011 findings remain unchanged except for broadened reach.
- Domains 01, 02, 06, 11, and 12 remain unscored; Domains 03, 05, 08, 09, and 10 remain capped by existing findings and unresolved specialist/runtime evidence.

## 8. Recommended next evidence, not fixes

1. Retain the completed migration/bootstrap/restore and runtime-gate results; add representative query-plan, concurrency, failover, and current-baseline production exercises during accepted implementation work.
2. Reconcile the failed/short-circuited Playwright and PowerShell suites, then add deferred-response, forced-failure, reconnect, session-expiry, and accessibility scenarios.
3. Obtain remaining owner decisions for clinical lifecycle exceptions, portal release, report retention, FHIR/SMART profile, external laboratory contract, and specialist acceptance. Identity, facility/patient/purpose scope, and reference-interface support are decided by `P2-D014`.
4. Obtain qualified accessibility, clinical, privacy/HIM, security, interoperability, database/operations, and release/supply-chain sign-off for their applicable conclusions.
5. Produce dependency/license/SBOM/provenance and clean-checkout release evidence.
6. Only after the preceding evidence and decisions are recorded should recommendations be accepted and Phase 3 implementation packets be authorized.

## Readiness disposition

Phase 2 assessment evidence is substantially collected but **not complete for implementation authorization**. Runtime availability is no longer the primary gap; the fixed baseline, governance, findings, and scorecard are ready for human validation and recommendation acceptance. Coding changes remain prohibited until the remaining specialist decisions and program-owner gates are explicitly closed or accepted as residual risk.
