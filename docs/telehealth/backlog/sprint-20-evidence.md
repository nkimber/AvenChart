# Sprint 20 synthetic practice-network determination evidence

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0023](../decisions/0023-approved-sprint-20-synthetic-practice-network-determination.md)  
Scope: Disabled, synthetic-only, applicant-owned practice/facility/service network determination after fresh synthetic eligibility; no real insurance/PHI, member or rendering-physician data in the directory inquiry, FHIR resource or bundle, provider-directory or payer call, aggregate exact-network confirmation, canonical coverage, financial amount, identity proofing, patient promotion, request, queue, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Aggregate and immutable evidence | V0297 constrains `SyntheticEligibilityRecorded -> SyntheticPracticeNetworkRecorded`, binds the complete review/safety/purpose/precheck/member-receipt/eligibility provenance chain, requires a fresh eligibility result, records one immutable determination/event, separates eligibility from directory transport, plan-network match, practice affiliation, service inclusion, new-patient acceptance, and business outcome, and enforces mapping/freshness/no-consequence invariants. |
| Standards-shaped port | `ITelehealthProspectivePracticeNetworkGateway` accepts only the configured practice/facility, selected plan, current state, server date, service category, and check time. The deterministic `NON_PRODUCTION` adapter publishes compatibility metadata for `HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0`, returns in-network/accepting, out-of-network, or unable-to-determine fixtures, creates opaque trace tokens, serializes no FHIR resource or bundle, and performs no external call. |
| Privacy and replay | The directory inquiry contains no member, group, subscriber, patient, physician, or NPI value. Exact persisted replay returns before adapter invocation; changed-key, stale, second, expired-upstream, cross-applicant, and concurrent commands fail closed. |
| HTTP contract | Idempotent `POST /api/telehealth/v1/applicants/{applicantId}/practice-network-determination` accepts only expected version and explicit synthetic-data confirmation. It is branded-host/access-key protected, typed, private/no-store, minimized, preserves eligibility separately, and exposes explicit false FHIR, live-directory, rendering-physician, aggregate exact-network, canonical-coverage, financial, patient, request, queue, clinical, integration, and external-call consequences. |
| Applicant UX | The prospective entry exposes the check only after eligibility, preserves the 911 action, retains one in-memory command identity across ambiguous retry, displays eligibility and directory facts separately, states that rendering-physician participation remains unchecked, and persists no member, eligibility, directory result, or trace value in browser storage. |
| Runtime and governance | Readiness requires 37 tables. Decision 0023, the [Sprint 20 plan](sprint-20-synthetic-practice-network-determination.md), safeguard TH-SG-025, CI runtime invocation, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, runbook, and planning validator v2.1 are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused practice-network backend tests | 8 adapter tests passed, covering all three normalized outcomes, configuration, supported states, effective-window rejection, and cancellation; the combined telehealth gateway/policy/runtime-safety filter passed 57 tests |
| Full backend tests | 260 passed, 0 failed, 0 skipped |
| ASP.NET Core Release build and formatting | Passed with 0 warnings, 0 errors, and no formatting drift in production or test projects |
| Focused frontend API/component tests | 2 files and 33 tests passed, including typed transport, minimal request, confirmation, stable retry, separate eligibility/network display, limitations, emergency action, and no result persistence |
| Full frontend tests | 53 files and 262 tests passed with file parallelism disabled |
| Frontend lint, TypeScript, and production build | Passed |
| Frontend bundle budget | Passed at 246,395 of 256,000 initial bytes; 137 JavaScript chunks checked; prospective entry chunk 42.07 kB |
| Cross-browser telehealth accessibility/recovery | 52 passed in 5.7 minutes across desktop Chromium, mobile Chromium, Firefox, and WebKit; the prospective journey now continues through practice-network determination, retry, status separation, minimization, focus recovery, emergency action, reflow, and WCAG checks |
| Full migration and recovery rehearsal | 253 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 62 checks passed; V0282–V0297, all 37 telehealth tables, all 30 append-only trigger inventory entries, complete upstream provenance, Plan-Net metadata, mapping/freshness/no-consequence guards, and append-only behavior passed |
| Telehealth authorization proof | 52 checks passed, including missing applicant access-key and portal-session substitution rejection for the new route |
| Telehealth OpenAPI proof | 29 checks passed, including applicant-only typed minimal input, required idempotency, bounded failures, separate eligibility/network fields, and explicit false consequences |
| Telehealth runtime-safety proof | 21 top-level checks passed; 37-table synthetic readiness was healthy and no member disclosure, FHIR serializer, outbound directory route, rendering-physician claim, or canonical coverage mutation path was present |
| Live prospective practice-network proof | 15 checks passed: Georgia/California/Florida provenance, three deterministic outcomes, eligibility/network separation, cross-applicant provenance rejection, exact replay, changed/stale/second conflict, 12-way one-winner contention, append-only evidence, minimized/coarse resume, no member/physician/raw/FHIR columns, and zero canonical/downstream delta |
| Generated empty bootstrap verification | Verified against the migration-derived generator output; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | v2.1 passed 65 checks with Decision 0023 and all 25 safeguards; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-prospective-practice-network.json`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers, normalized statuses, opaque trace tokens, standards-compatibility metadata, and bounded facts only; they contain no member/subscriber payload, physician identifier, FHIR resource or bundle, payer response, or external directory response.

After evidence capture, the exact labeled Sprint 20 API/PostgreSQL containers, network, isolated volume, and disposable synthetic dataset were removed. That data is intentionally not recoverable. No `avenchart-sprint20` Docker resource remains. The pre-existing default PostgreSQL service was restarted against its untouched `avenchart_avenchart-postgres` volume and verified healthy and unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Network, standards, ownership, and UX results

- the check is reachable only by the applicant access-key owner after current no-candidate staff review, one `TelehealthEligible` safety evaluation, one controlled visit purpose, one plan precheck, one protected member-detail receipt, and one fresh synthetic eligibility result;
- practice, facility, plan, state, date of service, service category, dataset, compatibility, trace, and result facts cannot be supplied by the client;
- the adapter inquiry deliberately excludes member, group, subscriber, patient, rendering-physician, and NPI values, keeping practice/facility/service participation separate from individual-member evidence and eventual clinician participation;
- server-owned fixtures express an in-network practice accepting new patients, an out-of-network practice, and an unavailable/unknown result without silently promoting unknown to participation;
- the compatibility target is metadata, not a conformance claim: no FHIR endpoint discovery, authentication, search parameter, resource profile validation, Bundle, OrganizationAffiliation, HealthcareService, Network resource, or external API call exists;
- exact retry returns immutable evidence before adapter invocation; 12 concurrent first writers produce one result and event; second semantic commands fail; both evidence types reject update and deletion;
- public output contains only applicant/result/version/state, prior normalized eligibility display facts, selected-plan/practice/service display facts, adapter/compatibility/dataset metadata, normalized directory statuses, opaque references and trace tokens, freshness, direction, and limitations; and
- every rendering-physician, aggregate exact-network, canonical-coverage, price/payment, identity/patient, consent, practice-acceptance, request/queue, clinical, prescribing, billing/claim, communication, integration, FHIR, live-directory, and external-call capability remains false with zero canonical/downstream row delta.

## 4. Evidence-gate observations

The first isolated schema run exposed a PostgreSQL 63-byte identifier limit: the proposed append-only trigger name was silently truncated and escaped the aggregate trigger inventory. V0297 was corrected before sealing by shortening the identifier, the disposable database was destroyed and recreated from zero, and the complete 253-migration plus 62-check schema evidence passed on the corrected migration. No applied shared or production database was edited.

The first authorization attempt intentionally used the fresh schema-only database and reached the new anonymous-access checks, then stopped because deterministic portal/staff identities were absent. The approved synthetic gold fixture was loaded, all 253 migrations reapplied, and the full 52-check authorization matrix passed. This was an evidence-environment sequencing correction, not a product-code bypass.

The migration rehearsal deliberately injected process failures at three checkpoints and deliberately corrupted and extended the migration ledger. Recovery succeeded at every checkpoint; checksum drift and the unexpected migration were rejected as required.

Graphify rebuilt and portably validated the deterministic code-only index. The new feature, tests, and migration are still untracked in this working tree, so focused delta review reported zero changed graph nodes and surfaced test-gap hints. Direct source review plus the 260 backend tests, 262 frontend tests, PostgreSQL, API, authorization, migration, live, and 52-test browser evidence above is authoritative for this increment.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- route registration disappears when the feature is disabled;
- unavailable, stale, mismatched, expired, or cross-applicant upstream evidence cannot reach or persist a directory result;
- immutable evidence is not destructively rolled back; correction requires a separately reviewed forward migration;
- disabling/removing the route and panel leaves existing synthetic evidence inert;
- stop conditions include client influence over authoritative inquiry/result facts, member or physician disclosure to the adapter, FHIR/external transport, collapsed eligibility/network semantics, practice participation presented as physician participation or coverage/payment assurance, unknown silently passing, cross-applicant evidence, replay overwrite, browser persistence, canonical/downstream mutation, or earlier safeguard regression; and
- every result remains synthetic, non-clinical, non-production, and unusable for coverage, payment, exact network, clinician matching, or patient-care decisions.

## 6. Open review gates

Independent payer/network, provider-directory vendor, standards-licensing, identity, licensed clinical/medical-director, security/privacy, data, accessibility, interoperability, legal/compliance, operational, and program-owner packet reviews remain open. No live Plan-Net endpoint discovery or query, FHIR conformance validation, real practice or facility participation, rendering-physician participation, canonical coverage, estimate/payment, identity proofing, patient promotion, consent, request/queue entry, or care action has been approved or implemented. Until those reviews and separately bounded decisions are recorded, Sprint 20 remains a disabled synthetic adapter-contract slice and every production, rendering-physician, aggregate exact-network, coverage, financial, patient-promotion, downstream, and patient-care gate remains closed.
