# Sprint 19 synthetic prospective eligibility result evidence

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0022](../decisions/0022-approved-sprint-19-synthetic-prospective-eligibility-result.md)  
Scope: Disabled, synthetic-only, applicant-owned normalized eligibility and benefit-information result after protected member-detail receipt; no real insurance/PHI, X12 transaction, payer/clearinghouse communication, exact network, canonical coverage, financial amount, identity proofing, patient promotion, request, queue, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Aggregate and immutable evidence | V0296 constrains `MemberInsuranceDetailsRecorded -> SyntheticEligibilityRecorded`, binds complete review/safety/purpose/precheck/member-receipt provenance, records one immutable result/event, separates transport, member-match, eligibility, benefit-information, and business outcomes, enforces freshness/mapping/no-consequence invariants, and rejects update or deletion. |
| Standards-shaped port | `ITelehealthProspectiveEligibilityGateway` fixes the current date of service and service category server-side. The deterministic `NON_PRODUCTION` adapter advertises `ASC_X12N_270_271_005010X279A1`, returns matched-active, matched-inactive, subscriber-not-found, or unavailable fixtures, generates opaque trace tokens, serializes no X12, and performs no external call. |
| Protected input and replay | The service unprotects the Sprint 18 payload only in server memory, verifies it against the immutable receipt, and fails closed on tamper or provenance mismatch. An exact persisted replay returns before payload unprotection or adapter invocation; changed-key, stale, second, and concurrent commands fail closed. |
| HTTP contract | Idempotent `POST /api/telehealth/v1/applicants/{applicantId}/eligibility` accepts only expected version and explicit synthetic-data confirmation. It is branded-host/access-key protected, typed, private/no-store, minimized, and exposes explicit false exact-network, canonical-coverage, financial, patient, request, queue, clinical, integration, and external-call consequences. |
| Applicant UX | The prospective entry exposes eligibility only after protected member details, preserves the 911 action, retains one in-memory command identity across ambiguous retry, displays separate normalized outcomes and limitations, and persists no member, result, trace, or eligibility value in browser storage. |
| Runtime and governance | Readiness requires 36 tables. Decision 0022, the [Sprint 19 plan](sprint-19-synthetic-prospective-eligibility-result.md), safeguard TH-SG-024, CI runtime invocation, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, runbook, and planning validator v2.0 are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused prospective eligibility backend tests | 39 passed, 0 failed, covering all four adapter outcomes, contract metadata, mapping, protected input, exact replay, and validation boundaries |
| Full backend tests | 252 passed, 0 failed, 0 skipped |
| ASP.NET Core Release build and formatting | Passed with 0 warnings, 0 errors, and no formatting drift |
| Focused frontend API/component tests | 2 files and 31 tests passed, including typed transport, confirmation, stable retry, normalized outcomes, limitations, emergency action, and no result persistence |
| Full frontend tests | 53 files and 260 tests passed with file parallelism disabled; four unrelated clinician-page tests that timed out or observed incomplete mocked loads under the initial parallel run passed 7/7 in isolation before the deterministic full rerun |
| Frontend lint, TypeScript, and production build | Passed |
| Frontend bundle budget | Passed at 246,395 of 256,000 initial bytes; 137 JavaScript chunks checked; prospective entry chunk 37.59 kB |
| Cross-browser telehealth accessibility/recovery | 52 passed in 7.2 minutes across desktop Chromium, mobile Chromium, Firefox, and WebKit; the prospective journey now continues through normalized eligibility result and retry |
| Full migration and recovery rehearsal | 252 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 |
| Telehealth migration/schema regression | 59 checks passed; V0282–V0296, all 36 telehealth tables, complete upstream provenance, mapping/freshness/no-consequence guards, and append-only behavior passed |
| Telehealth authorization proof | 50 checks passed, including missing applicant access-key and portal-session substitution rejection |
| Telehealth OpenAPI proof | 28 checks passed, including typed minimal input/output, required idempotency, bounded failures, separate normalized outcomes, and explicit false consequences |
| Telehealth runtime-safety proof | 20 top-level checks passed; 36-table synthetic readiness was healthy and no X12 serializer, outbound payer/clearinghouse route, or canonical coverage mutation path was present |
| Live prospective eligibility proof | 16 checks passed: Georgia/California/Florida provenance, four deterministic outcomes, protected-payload failure, cross-applicant provenance rejection, exact replay, changed/stale/second conflict, 12-way one-winner contention, append-only evidence, minimized/coarse resume, no raw columns, and zero canonical/downstream delta |
| Earlier telehealth live and contention proofs | Applicant identity, staff review, prospective safety, visit purpose, practice-network precheck, protected member receipt, and the 20-caller full queue/consultation lifecycle proof all passed unchanged |
| Generated empty bootstrap verification | Verified against the migration-derived generator output; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | v2.0 passed 64 checks with Decision 0022 and all 24 safeguards; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-prospective-eligibility.json`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers, masks, normalized statuses, opaque trace tokens, and bounded facts only; they contain no protected payload, submitted raw member/subscriber values, X12 transaction, or external response.

After evidence capture, the exact labeled Sprint 19 API/PostgreSQL containers, network, isolated volume, and disposable synthetic dataset were removed. That data is intentionally not recoverable. No `avenchart-sprint19` Docker resource remains. The pre-existing default PostgreSQL service was restarted against its untouched `avenchart_avenchart-postgres` volume and verified healthy and unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Eligibility, standards, ownership, and UX results

- eligibility is reachable only by the applicant access-key owner after current no-candidate staff review, one `TelehealthEligible` safety evaluation, one controlled visit purpose, one practice-plan precheck, and one protected member-detail receipt;
- payer/plan/member/subscriber facts, practice, provider, date of service, service category, trace, adapter metadata, and result facts cannot be supplied by the client;
- server-owned fixtures separately express transport acceptance, member matching, eligibility, benefit-information reporting, and business outcome, so an unavailable transport or unknown match cannot silently become eligible;
- the compatibility target is metadata, not an implementation-guide claim: no X12 segment, interchange, TA1/999, transport security, trading-partner routing, or payer call exists;
- exact retry returns immutable evidence without unprotecting the payload or invoking the adapter, while a new command must unprotect and rebind the receipt before evaluation;
- 12 concurrent first writers produce one result and event; second semantic commands fail; both evidence types reject update and deletion;
- public output contains only applicant/result/version/state, selected-plan display data, member/group masks, synthetic adapter/standard/dataset metadata, opaque trace tokens, normalized non-financial statuses, freshness, next action, and limitations; and
- every exact-network, canonical-coverage, price/payment, identity/patient, consent, practice-acceptance, request/queue, clinical, prescribing, billing/claim, communication, integration, and external-call capability remains false with zero canonical/downstream row delta.

## 4. Evidence-gate observations

The initial full frontend run used the repository's default file parallelism and produced four failures in unrelated clinician schedule, report, patient-shell, and therapy-group tests. Each failure showed timeout or an incomplete mocked asynchronous load. All four files passed 7/7 immediately with one worker, and the entire 53-file / 260-test suite then passed with file parallelism disabled. No product code was changed to mask the resource-contention signal.

The migration rehearsal deliberately injected process failures at three checkpoints and deliberately corrupted and extended the migration ledger. Recovery succeeded at every checkpoint; checksum drift and the unexpected migration were rejected as required.

Graphify rebuilt and portably validated the deterministic code-only index. The new feature, tests, and migration are still untracked in this working tree, so focused delta review reported zero changed graph nodes and test-gap hints. Direct source review plus the unit, PostgreSQL, API, authorization, migration, and browser evidence above is authoritative for this increment.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- route registration disappears when the feature is disabled;
- unreadable or mismatched protected input cannot reach the adapter, and no raw payload or transaction content is added to result/event storage;
- immutable evidence is not destructively rolled back; correction requires a separately reviewed forward migration;
- disabling/removing the route and panel leaves existing synthetic evidence inert;
- stop conditions include client influence over authoritative inquiry/result facts, raw/protected/X12 leakage, collapsed transport/business semantics, active eligibility presented as network/payment assurance, unknown silently passing, cross-applicant evidence, replay overwrite, browser persistence, canonical/downstream mutation, external action, or earlier safeguard regression; and
- every result remains synthetic, non-clinical, non-production, and unusable for coverage, payment, network, or patient-care decisions.

## 6. Open review gates

Independent payer/eligibility, standards-licensing, identity, licensed clinical/medical-director, security/privacy, data, accessibility, interoperability, legal/compliance, operational, and program-owner packet reviews remain open. Durable production Data Protection key custody, backup, rotation, and disaster recovery are not supplied by this slice. No real member data, X12 270/271 transaction, payer/clearinghouse query, real member match, canonical coverage, exact physician participation, estimate/payment, identity proofing, patient promotion, consent, request/queue entry, or care action has been approved or implemented. Until those reviews and separately bounded decisions are recorded, Sprint 19 remains a disabled synthetic adapter-contract slice and every production, exact-network, financial, patient-promotion, downstream, and patient-care gate remains closed.
