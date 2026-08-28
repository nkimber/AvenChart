# Sprint 17 synthetic prospective practice-network precheck evidence

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0020](../decisions/0020-approved-sprint-17-prospective-practice-network-precheck.md)  
Scope: Disabled, synthetic-only, applicant-owned selection of one versioned practice-plan fixture after a recorded visit purpose; no member eligibility or benefits, physician participation, exact network confirmation, coverage, price, payment, identity proofing, patient promotion/linkage, intake completion, consent, request, queue, appointment, encounter, care, prescription, claim, communication, external payer call, production use, or real PHI

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Aggregate and immutable evidence | V0294 constrains `VisitPurposeRecorded -> PracticeNetworkPrecheckRecorded`, one applicant-bound precheck, exact plan/outcome mappings, review/safety/purpose provenance, semantic idempotency, a catalog effective window, 26 hard-false downstream flags, a database snapshot guard, and append-only precheck/event evidence. |
| Server-owned fixture policy | `SyntheticTelehealthProspectivePracticeNetworkCatalog`, repository, and service expose only three exact `NON_PRODUCTION` fixtures, rebind host/practice/facility/access/version/review/safety/purpose state under the applicant lock, and do not call `ITelehealthCoverageGateway` or any payer, eligibility, benefit, or network adapter. |
| HTTP contract | `GET /api/telehealth/v1/applicants/{applicantId}/practice-network-precheck/options` and idempotent `POST /api/telehealth/v1/applicants/{applicantId}/practice-network-precheck` are applicant-access-key protected, typed, private/no-store, opaque on ownership failure, and bounded to documented failure responses. |
| Applicant UX | The prospective entry loads the catalog only after purpose recording, presents accessible exact-plan radios, distinguishes practice-level fixtures from member eligibility and exact physician participation, preserves the 911 action, provides retry-safe submission and focus recovery, and stores no selected plan in browser persistence. |
| Runtime and governance | Readiness requires 34 tables; Decision 0020, Sprint 17 plan, safeguard TH-SG-022, CI runtime invocation, migration/OpenAPI/auth/runtime/live proofs, backlog authorization, runbook, and planning validator are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused prospective practice-network backend tests | 15 passed, 0 failed, covering the exact catalog, plan/outcome mappings, effective window, expiry, and invalid selection behavior |
| Full backend tests | 213 passed, 0 failed, 0 skipped |
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Focused frontend API/component tests | 2 files and 27 tests passed, including catalog transport, accessible choices, exact fixture distinctions, retry identity, focus recovery, terminal rendering, persistent emergency action, and no plan persistence |
| Full frontend tests | 53 files and 256 tests passed |
| Frontend lint, TypeScript, and production build | Passed |
| Frontend bundle budget | Passed at 246,395 of 256,000 initial bytes; 137 JavaScript chunks checked; prospective entry chunk 26.47 kB |
| Cross-browser telehealth accessibility/recovery | 52 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the prospective journey now continues through the practice-network precheck |
| Full migration and recovery rehearsal | 250 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 |
| Telehealth migration/schema regression | 53 passed; V0282–V0294, all 34 telehealth tables, prior controls, database guards, and append-only behavior passed |
| Telehealth authorization proof | 46 passed, including applicant access-key boundaries and all earlier role/resource controls |
| Telehealth OpenAPI proof | 26 passed, including both typed applicant-only precheck endpoints, required command idempotency, bounded failures, and minimal contracts |
| Telehealth runtime-safety proof | 18 top-level checks passed; the prospective path has no coverage-gateway or outbound integration source path and 34-table synthetic readiness remained healthy |
| Live prospective practice-network proof | 13 checks passed: Georgia/California/Florida provenance, exact options, access/arbitrary/stale rejection, all three outcomes, exact replay, changed/second-command conflict, 12-way one-winner contention, minimized response/resume, 26 hard-false consequences, append-only rejection, and zero canonical/downstream delta |
| Generated empty bootstrap verification | Regenerated and verified; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | Passed with Decision 0020 and all 22 safeguards; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-prospective-practice-network-precheck.json`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and bounded facts only.

After evidence capture, the exact labeled Sprint 17 API/PostgreSQL containers, network, isolated volume, and disposable synthetic dataset were removed. That data is intentionally not recoverable. No `avenchart-sprint17` Docker resource remains. The pre-existing default PostgreSQL service was restarted against its untouched `avenchart_avenchart-postgres` volume and verified healthy and unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Insurance, ownership, privacy, and UX results

- discovery is reachable only by the applicant access-key owner after current no-candidate staff review, one `TelehealthEligible` universal safety evaluation, and one controlled visit-purpose classification;
- the catalog contains exactly `Harbor Mutual / High Deductible`, `Blue Valley Health / Standard`, and `Pine State Choice / Choice`, mapped respectively to `PracticeNetworkConfirmedFixture`, `NetworkUnknown`, and `PracticeOutOfNetworkFixture`;
- all options are marked `NON_PRODUCTION`, catalog key `avenchart-synthetic-prospective-practice-network-2026-08`, version 1, effective 2026-08-27 through 2026-10-31;
- the result concerns only a synthetic practice-plan fixture: it does not establish member identity, active coverage, benefits, service coverage, the exact billing/rendering physician's participation, exact network status, cost, or payment responsibility;
- exact retry converges; arbitrary, changed, stale, second, and losing concurrent commands fail; 12 concurrent first writers produce one precheck and event; and both evidence types reject destructive mutation;
- every eligibility, benefit, exact-network, patient/chart/portal, intake/consent, coverage, financial, request/queue, appointment/encounter, care, prescribing, billing/claim, communication, integration, and external-action capability is explicitly false;
- recording changes only the applicant aggregate plus one precheck and one event; canonical patient, insurance, portal, intake, coverage, request, queue, appointment, encounter, prescription, claim, and financial rows remain unchanged; and
- the browser keeps only the applicant ID/access key plus one in-memory ambiguous command identity for explicit retry; it persists neither catalog content nor the selected plan.

## 4. Boundary refinements found by the evidence gate

The first live minimization assertion used the coarse word `member`, which also appears in the required limitation text `Member eligibility remains unavailable`. The test was narrowed to actual forbidden member/coverage identifiers and the complete authoritative proof passed without a product-code change.

The first focused browser run selected every `role=status` element after the flow gained both the purpose result and the network-loading message. The test-only locator was constrained to the purpose result's exact text; the final 52-test cross-browser run passed without a UI behavior change.

An attempted full frontend command included Vitest's removed `--minWorkers` option and exited before collecting tests. The supported deterministic `--maxWorkers=1` command then ran the full 53-file, 256-test suite successfully; the pre-collection CLI error is not treated as product or test evidence.

The migration rehearsal deliberately injected process failures at three checkpoints and deliberately corrupted/extended the migration ledger. Recovery succeeded at all checkpoints, while checksum drift and the unexpected migration were rejected as required.

Graphify rebuilt the committed deterministic code-only index and passed its portability check. The focused delta review surfaced the tracked `Program.cs` integration boundary and its broad dependency history. The new feature, test, and migration files remain untracked in this working tree, so the index reported them as test-gap hints instead of pretending to model their internal relationships; direct source validation and the focused, full, PostgreSQL, contract, and cross-browser evidence above remain authoritative for this increment.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- route registration disappears when the feature is disabled;
- the bounded catalog expires on 2026-10-31 and its database evidence cannot be rewritten or deleted;
- no destructive migration or evidence rollback is permitted; correction requires a separately reviewed forward migration;
- stop conditions include arbitrary-plan acceptance, stale/duplicate overwrite, any eligibility/benefit/exact-network implication, coverage-gateway or external call, canonical/downstream row creation, browser persistence, or earlier safeguard regression; and
- every result remains synthetic, non-clinical, non-production, and unusable for coverage or patient-care decisions.

## 6. Open review gates

Independent payer/network subject-matter, identity, licensed clinical/medical-director, security/privacy, data, accessibility, interoperability, legal/compliance, operational, and program-owner packet reviews remain open. No X12 270/271 transaction, payer directory, provider-directory query, member eligibility decision, exact physician participation decision, coverage record, price estimate, payment, patient promotion, request/queue entry, or care action has been approved or implemented. Until those reviews and separately bounded decisions are recorded, Sprint 17 remains a disabled synthetic discovery slice and every production, eligibility, exact-network, financial, patient-promotion, downstream, and patient-care gate remains closed.
