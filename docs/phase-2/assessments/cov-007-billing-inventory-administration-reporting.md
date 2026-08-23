# COV-007 assessment — billing, inventory, administration, reporting, and background execution

- Status: in review
- Baseline: `phase-1-experimental` at `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`
- Observed on: 2026-08-21
- Primary reviewers: `phase2_data`, `phase2_quality_operations`, `phase2_frontend_accessibility`
- Independent verification: separate `phase2_verifier` pass plus focused `phase2_security_privacy` validation
- Primary coverage: `COV-007`
- Supporting coverage: `COV-001`, `COV-002`, `COV-003`, `COV-004`, `COV-005`, `COV-008`, `COV-009`, `COV-011`, `COV-014`, `COV-016`, `COV-017`
- Evidence level: source and retained-test trace, clean Release build, complete modern UI unit suite, focused report tests, and independent static verification; PostgreSQL interleavings, fault injection, browser failure workflows, backup/restore, deployment probes, and qualified operating-policy decisions remain outstanding

## Assessment question

Do billing, controlled inventory, administration, reporting, and background execution preserve trustworthy financial, custody, configuration, disclosure, queue, artifact, and recovery state from user action through durable evidence and operational observation?

This is an engineering-readiness assessment. It does not make a financial, legal, clinical, pharmacy, controlled-substance, privacy, certification, or production-use claim. Local-only and synthetic boundaries are treated as counterevidence where they are explicit, but an exposed production-shaped mutation is still assessed as an engineering condition against the adopted future-production target.

## Representative traces

### Billing and financial state

1. Billing lines, claims, payments, statements, EOB import, and claim adjudication are exposed through ordinary `acct:bill:write` endpoints.
2. Patient and encounter relationships and encounter-lock checks exist, but several financial writes do not receive the authenticated actor or a caller version.
3. EOB import accepts only a patient ID and inserts fixed demonstration rows; adjudication accepts only a claim ID and posts fixed monetary outcomes.
4. The EOB importer allocates one payment-session sequence value and manually uses two consecutive IDs.
5. Billing lines, claims, and payment activities have physical delete paths beside a payment void path; financial preimages and durable resource events are not a shared invariant.

### Controlled inventory custody and counts

1. Ordinary custody movement is strong: lot/location locks, quantity checks, related-event validation, idempotency, actor fields, immutable events, and atomic ledger writes are present.
2. Controlled count creation serializes on the controlled-location row; the suspected duplicate active-count race was not reproduced and is not a finding.
3. Count and discrepancy screens ask the initiating user to paste another user's active session UUID as a counter/witness credential.
4. Discrepancy correction commits the compensating custody movement and then separately closes the discrepancy, leaving a split-commit/concurrent-correction boundary.

### Administration and configuration

1. Governed change requests retain baselines, caller versions, row locks, revisions, events, transactions, and rollback.
2. The same administration capability can create, submit, approve, and activate a request; creator/approver separation is not enforced.
3. Direct upsert/status/rollback routes coexist with the request lifecycle across coding catalogs, form layouts/options, modules, API clients, clinical alerts, and practice settings.
4. The registry explicitly says independent-approver policy is pending, so the mechanism is recorded as a policy-dependent medium condition rather than an unqualified security violation.

### Reports, queue, worker, and artifacts

1. Governed reporting has strong purpose, recipient, scope, definition revision, idempotency, queue lease, retry, checksum, lifecycle, and protected-download controls.
2. Direct operational and family CSV exports remain enabled beside governed reporting and query practice-wide data with only a broad report capability.
3. Governed runs pin metadata and as-of labels, but worker execution queries mutable live tables rather than a source-versioned snapshot.
4. The worker is an in-process background service; readiness and deployment verification do not observe worker progress. This remains an operational evidence gap, not a canonical finding in this packet.
5. Artifacts are stored as database text and purged by worker maintenance; encryption, legal hold, backup/restore, and disposition approval remain unresolved. Independent verification retained this as a readiness unknown rather than a finding because deployment topology and approved policy are not established.

## Reproducible checks

| Check | Result |
| --- | --- |
| Resolve the Phase 1 tag and compare `avenchart/`, `avenchart-ui/`, and `infra/` with the baseline | Baseline resolved; product tree remained unchanged during assessment |
| Release API build | Passed with 0 warnings and 0 errors (`dotnet build .\\avenchart\\AvenChart.slnx -c Release --no-restore -v:minimal`) |
| Complete modern UI unit suite | 31 files and 178 tests passed |
| Focused report/API tests | 3 files and 68 tests passed in the specialist pass; the complete suite also passed |
| Modern UI production build and bundle budget | Passed; initial bundle 201,524 of 256,000 bytes, 128 chunks |
| NuGet and production npm advisory checks | No vulnerable packages reported by configured sources; `npm audit --omit=dev --audit-level=low` reported 0 vulnerabilities |
| PostgreSQL concurrency, fault, sequence, report-source mutation, backup/restore, and migration replay | Not run: Docker/PostgreSQL and `psql` were unavailable |
| Browser witness replay, direct-versus-governed export comparison, EOB repeat, poll/collections failure recovery | Not run; source paths are deterministic but runtime behavior remains outstanding |

The green build and UI tests preserve useful route, rendering, and API-client contracts. They do not exercise a second user's affirmative action, repeated financial mutations, sequence state after a two-row import, controlled discrepancy interleavings, report source mutation between enqueue and execution, direct export evidence comparison, worker termination, artifact expiry during worker failure, or billing queue failure recovery.

## Material strengths and counterevidence

- Controlled custody movements lock affected rows, reject invalid quantity/relationship states, use idempotency keys, and atomically append custody and inventory events.
- Purchase receipts, lot ledgers, valuation runs, cost-policy activation, and requisitions retain deliberate transactional boundaries.
- Controlled-count creation serializes by locking the controlled-location row; no active-count duplicate finding is reported.
- Governed report execution has purpose/recipient/scope checks, pinned definition metadata, idempotency, `FOR UPDATE SKIP LOCKED` queue claims, leases, heartbeats, bounded retry, cancellation, checksums, retention deadlines, and protected downloads.
- Administration workflows retain baselines, caller versions, row locks, revisions, reasoned events, and rollback; delegated practice-setting users cannot approve or activate.
- Billing patient-account failures expose Retry; inventory root failures use alert and Retry; report UI discloses local-only limits; administration warns about irreversible destructive actions.
- The UI has substantial automated axe and material inventory/report browser coverage, although those checks do not establish WCAG conformance or dangerous-path semantics.
- The application explicitly says local EOB activity is not clearinghouse adjudication and report execution is not approved production infrastructure. Those disclosures narrow the claim but do not remove production-shaped write or bypass paths.
- Azure PostgreSQL deployment documentation describes private networking and encrypted database/backups. The artifact concern is unresolved lifecycle approval and exercised recovery—not proof of plaintext storage.

## Validated findings

| Finding | Condition | Severity | Reach | Production blocker |
| --- | --- | --- | --- | --- |
| [`P2-04-F004`](../findings/p2-04-f004-controlled-discrepancy-atomicity.md) | Controlled-count correction is not atomic with discrepancy closure | High | Repeated | Yes |
| [`P2-04-F005`](../findings/p2-04-f005-synthetic-billing-outcomes.md) | Billing adjudication and EOB import persist fixed synthetic monetary outcomes | High | Repeated | Yes |
| [`P2-04-F006`](../findings/p2-04-f006-eob-payment-sequence.md) | EOB import consumes an unreserved payment-session sequence value | Medium | Repeated | No |
| [`P2-04-F007`](../findings/p2-04-f007-report-source-snapshot.md) | Governed report metadata is pinned without pinning source row state | High | Cross-cutting | Yes |
| [`P2-05-F010`](../findings/p2-05-f010-controlled-inventory-bearer-attestation.md) | Controlled-inventory attestation uses another user's transferable session bearer | High | Repeated | Yes |
| [`P2-05-F011`](../findings/p2-05-f011-legacy-report-governance-bypass.md) | Direct report exports bypass the governed reporting lifecycle | High | Cross-cutting | Yes |
| [`P2-08-F003`](../findings/p2-08-f003-governed-report-poll-recovery.md) | Governed report status can remain stale after a recoverable poll or lifecycle conflict | Medium | Repeated | No |
| [`P2-08-F004`](../findings/p2-08-f004-collections-queue-failure-state.md) | A failed collections-queue load is indistinguishable from an unavailable queue | Medium | Isolated | Unknown |
| [`P2-10-F001`](../findings/p2-10-f001-administration-approval-boundary.md) | Administration change governance does not enforce independent approval | Medium pending policy | Systemic | Unknown |

High severity reflects the adopted future-production target and durable financial, custody, disclosure, or source-provenance consequences. It does not assert demonstrated financial loss, controlled-substance diversion, unauthorized disclosure, or patient harm in the synthetic Phase 1 experiment.

## Existing findings broadened by this packet

- [`P2-03-F007`](../findings/p2-03-f007-patient-route-response-inversion.md) now includes billing-account and inventory-lot response ownership, extending the same stale-selection cause beyond chart, portal, and therapy views.
- [`P2-03-F010`](../findings/p2-03-f010-encounter-lock-boundary.md) now includes billing adjudication, ordinary payment, and EOB import checks that occur before the financial transaction.
- [`P2-03-F011`](../findings/p2-03-f011-clinical-record-hard-delete.md) now covers physical deletion of billing lines, claims, payment activities, and payment sessions alongside clinical/follow-up records. Financial retention policy remains outstanding.
- [`P2-05-F002`](../findings/p2-05-f002-chart-access-not-resource-scoped.md) now includes direct report-family practice-wide row scope; [`P2-05-F003`](../findings/p2-05-f003-phi-audit-resource-correlation.md) now includes the inability to correlate direct report rows/downloads.
- [`P2-05-F009`](../findings/p2-05-f009-workflow-mutation-provenance.md) now includes billing mutations and fixed `119` / `gold-billing-01` actor attribution.
- [`P2-09-F002`](../findings/p2-09-f002-default-verification-gate.md) now includes the missing controlled discrepancy, dual-user, financial replay, sequence, report-source, worker/retention, and billing-recovery scenarios.

## Narrowed or retained as unknown

- The concurrent active controlled-count creation race was falsified statically because supported creation locks the controlled-location row before checking for an active session. Database-bypass behavior remains an operating-policy question, not a finding.
- The in-process worker and health-probe gap is retained as a production-readiness unknown. Logs, queue expiry, retries, and an operator projection are meaningful controls; no worker failure, timeout, outage, multi-replica, or SLO scenario was run.
- Inline report artifact storage and expiry are retained as a readiness unknown. The code has requester/recipient checks, checksums, events, and purge; approved production storage, key/access policy, legal hold, backup/restore, disposition, and direct expiry enforcement remain unresolved. No unencrypted-storage claim is made.
- Build/container/action mutability is a candidate for `COV-015`/`COV-017`, not a COV-007 finding; advisory checks were green and no compromise or vulnerable package was established.
- Administration self-approval/direct mutation is a validated mechanism with medium policy-dependent severity. An accountable owner must decide which configuration families require separation of duties or a break-glass route.
- Statement batch command growth, report artifact size, queue throughput, and connection/memory use remain measurement questions. No performance severity is inferred from static code shape.
- No external clearinghouse, payer, reminder gateway, or report delivery service was found. The local-only labels are retained as boundary evidence; they do not establish a production integration contract.

## Required specialist decisions and remaining evidence

- Billing/revenue-cycle and finance owners must define the source-of-truth, idempotency, correction, void, retention, and deletion semantics for EOB/adjudication/payment state.
- Controlled-inventory/pharmacy operations and security owners must define dual-control proof, permission/facility scope, second-party intent, content binding, and correction escalation.
- Report/HIM/privacy owners must decide whether direct compatibility exports are permitted, what purpose/recipient/minimum-necessary rules apply, and whether source-as-of semantics require historical snapshots.
- Database specialists must replay discrepancy faults, EOB repetition/sequence behavior, financial deletes, and queued-run source mutation in a disposable PostgreSQL environment.
- Operations/SRE must exercise worker termination, readiness/liveness behavior, leases, retries, queue age, artifact expiry, backup/restore, and two-replica claims.
- Accessibility and representative biller/inventory/admin users must test keyboard, screen-reader, focus, zoom/reflow, error, conflict, and retry behavior for the affected UI paths.
- Configuration owners must decide where creator/approver/activator separation is required and whether direct routes are break-glass, compatibility, or normal operations.

## Coverage and scorecard impact

- `COV-007` advances from **Inventoried** to **In review** with four vertical specialist packets and independent verification. It is not Evidence complete or Complete.
- `COV-008` and `COV-009` gain material hybrid-transaction, sequence, source-snapshot, custody, financial-history, and recovery evidence; live database proof remains outstanding.
- `COV-014` gains concrete missing negative scenarios; the repository-visible gate finding `P2-09-F002` remains the canonical verification-governance root.
- `COV-001`, `COV-016`, and `COV-017` receive supporting worker/probe, deployment-boundary, and build-provenance evidence but remain open for their own rows.
- Domains 04, 05, 07, 08, 09, and 10 remain capped by the validated high/blocking findings; no domain rating improves from this packet.

## Next evidence, not fixes

1. Run two-user synthetic controlled-count and custody tests, including a second user without inventory permission and no action in that user's browser.
2. Fault controlled discrepancy correction after movement commit and run two distinct-key corrections concurrently.
3. Repeat EOB import and adjudication, inspect balances, actor fields, sequence state, and retry behavior.
4. Compare direct and governed report outputs for restricted synthetic facility/provider scopes, including audit, purpose, recipient, artifact, and download evidence.
5. Pause a queued report, mutate an eligible source row, release the worker, and compare preview, metadata, and artifact; repeat across a retry.
6. Exercise worker termination/restart, readiness/liveness, queue age, artifact expiry during outage, restore, and multi-instance claim behavior.
7. Force collections and governed-report poll failures and verify visible recovery, focus, status announcements, and stale-state handling.
8. Obtain accountable finance, pharmacy, HIM/privacy, reporting, operations, accessibility, and configuration-policy decisions before any Phase 3 recommendation is accepted.

`COV-007` remains **In review**. The product tree remains read-only and unchanged from the fixed Phase 1 baseline. No implementation recommendation or Phase 3 change packet is authorized by this assessment.
