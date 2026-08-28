# Sprint 21 synthetic identity-proofing process evidence

Status: Bounded automated evidence passing; independent identity, clinical, legal, security/privacy, accessibility, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0024](../decisions/0024-approved-sprint-21-synthetic-identity-proofing-process.md)  
Scope: Disabled, synthetic-only, applicant-owned identity-proofing *process fixture* after fresh active eligibility and a positive practice-network result; no real evidence, document, government identifier, image, video, biometric, authoritative-source query, notification, redress case, authenticator, identity assurance level, patient promotion, portal account, consent, request, queue, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Aggregate and immutable evidence | V0298 constrains `SyntheticPracticeNetworkRecorded -> SyntheticIdentityProofingRecorded`, binds the complete applicant/review/safety/purpose/precheck/member-receipt/eligibility/practice-network provenance chain, requires fresh `Active`/`Reported`/`EligibleBenefitsReported` eligibility plus `PracticeInNetworkAcceptingNewPatients`, and records one immutable result/event with database-enforced scope, notice, vocabulary, mapping, reference, freshness, replay, no-consequence, and provenance invariants. |
| Process-shaped port | `ITelehealthProspectiveIdentityProofingGateway` accepts only an opaque applicant reference, server-owned practice/facility/state/profile/notice/time, and an opaque evidence-package reference. The deterministic `NON_PRODUCTION` adapter publishes `NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY`, returns separate transport, evidence-collection, evidence-validation, attribute-validation, applicant-verification, fraud, and business statuses, and performs no external call. |
| No assurance claim | The fixture always returns `assuranceLevelAchieved=None` and `identityProofed=false`. It exercises process-shaped statuses only; it does not satisfy IAL1, IAL2, IAL3, or any NIST conformance claim and cannot promote an applicant or authorize care. |
| Privacy and replay | No name, date of birth, contact value, address, insurance value, document, government identifier, biometric, image/video, or raw evidence reaches the adapter or persistence table. Exact persisted replay returns before adapter invocation; changed-content reuse, stale/expired/upstream-negative, second semantic commands, and concurrent first writers fail closed. |
| HTTP contract | Idempotent `POST /api/telehealth/v1/applicants/{applicantId}/identity-proofing` accepts only expected version plus explicit privacy-notice and synthetic-data acknowledgments. It is branded-host/access-key protected, typed, private/no-store, and returns normalized process facts, opaque references, explicit `None` assurance, and explicit false consequences. |
| Applicant UX | The prospective entry exposes the process exercise only after an immediately observed positive eligibility/network result, preserves the 911 action, retains one in-memory command identity across ambiguous retry, displays normalized process stages without opaque references, makes the non-assurance limitation prominent, and persists no result, trace, session, or evidence reference in browser storage. A resumed coarse status does not infer the hidden eligibility/network result or reopen proofing. |
| Runtime and governance | Readiness requires 38 tables. Decision 0024, the [Sprint 21 plan](sprint-21-synthetic-identity-proofing-process.md), safeguard TH-SG-026, CI runtime invocation, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, runbook, and planning validator v2.1 are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused identity-proofing backend tests | 6 adapter tests passed, covering the normalized process fixture, exact server-owned contract, Georgia/California/Florida, unavailable profiles/effective windows, and cancellation |
| Full backend tests | 266 passed, 0 failed, 0 skipped |
| ASP.NET Core Release build and formatting | Passed with 0 warnings, 0 errors, and no formatting drift |
| Focused frontend API/component tests | 2 files and 34 tests passed, including minimal typed transport, privacy acknowledgments, positive-gate visibility, stable retry, normalized stage display, emergency action, opaque-reference suppression, and no result persistence |
| Full frontend tests | 53 files and 263 tests passed with file parallelism disabled |
| Frontend lint, TypeScript, and production build | Passed |
| Frontend bundle budget | Passed at 246,395 of 256,000 initial bytes; 137 JavaScript chunks checked; prospective entry chunk 47.15 kB |
| Cross-browser telehealth accessibility/recovery | 52 passed in 3.7 minutes across desktop Chromium, mobile Chromium, Firefox, and WebKit; the prospective journey now continues through positive network evidence and identity-proofing acknowledgments, ambiguous retry, normalized status, minimization, focus recovery, emergency action, reflow, and WCAG checks |
| Full migration and recovery rehearsal | 254 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 65 checks passed; V0282–V0298, all 38 telehealth tables, all 31 append-only trigger inventory entries, complete upstream provenance, NIST-concepts-only metadata, normalized mapping/freshness/replay/no-consequence guards, and append-only behavior passed |
| Telehealth authorization proof | 54 checks passed, including missing applicant access key and portal-session substitution rejection for the identity-proofing route |
| Telehealth OpenAPI proof | 30 checks passed, including applicant-only typed minimal input, required idempotency, bounded failures, normalized process fields, `None` assurance, and explicit false consequences |
| Telehealth runtime-safety proof | 22 top-level checks passed; 38-table synthetic readiness was healthy and no raw identity data, identity-provider transport, assurance claim, patient promotion, canonical mutation, downstream, or outbound path was present |
| Live prospective identity-proofing proof | 14 checks passed: Georgia/California/Florida positive provenance, inactive/out-of-network/access/acknowledgment/stale rejection, exact normalized stages, NIST concepts-only/`None` assurance, public and schema minimization, all consequence flags false, exact replay, changed-content and second-command rejection, 12-way one-winner contention, append-only evidence, coarse resume, and zero canonical/downstream delta |
| Generated empty bootstrap verification | Verified against the migration-derived generator output; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | v2.1 passed 66 checks with Decision 0024 and all 26 safeguards; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-prospective-identity-proofing.json`, with repository recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers, normalized statuses, opaque trace/reference tokens, standards-compatibility metadata, and bounded false consequence facts only; they contain no identity document, government identifier, biometric, raw evidence, real applicant identity, payer response, authoritative-source response, or external-provider response.

After evidence capture, the exact labeled Sprint 21 API/PostgreSQL containers, network, isolated volume, and disposable synthetic dataset were removed. That data is intentionally not recoverable. No `avenchart-sprint21` Docker resource remains. The pre-existing default PostgreSQL service was restarted against its untouched `avenchart_avenchart-postgres` volume and verified healthy and unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Identity, standards, state, and UX results

- the process exercise is reachable only by the applicant access-key owner after current no-candidate staff review, one `TelehealthEligible` safety result, one controlled visit purpose, one plan precheck, one protected member-detail receipt, one fresh active/reported eligibility result, and one positive practice/facility/service network result;
- the client cannot supply the applicant reference, practice, facility, state, proofing profile, notice identity/version, evidence reference, compatibility target, dataset, process outcome, or timestamps;
- the adapter inquiry deliberately excludes legal identity attributes and sends only opaque references and server-owned context;
- `NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY` is descriptive compatibility metadata, not certification or conformance: the fixture does not perform the evidence, authoritative-source, government-identifier, ownership, notification, redress, or authenticator-binding work required for a real assurance result;
- public output includes normalized process stages and opaque references, while the applicant UI deliberately withholds the opaque references and browser storage retains only the applicant session credential pair;
- exact retry returns immutable evidence before adapter invocation; 12 concurrent first writers produce one result and event; changed-content reuse and a second semantic command fail; result and event reject update and deletion;
- Georgia, California, and Florida remain bounded state-context inputs only. This slice starts no clinical relationship, encounter, diagnosis, prescription, billing, or care action; state telehealth consent, licensure, standard-of-care, record, prescribing, and emergency obligations remain later gates; and
- every real-evidence, identifier, biometric, source-query, notification, redress, authenticator, assurance, patient/account/chart, consent, practice-acceptance, coverage/financial, request/queue, appointment/encounter, clinical, prescribing, billing/claim, communication, integration, and external-call capability remains false with zero canonical/downstream row delta.

The process model was checked against the official [NIST SP 800-63A-4 general identity-proofing requirements](https://pages.nist.gov/800-63-4/sp800-63a/ial-general/) and [IAL requirements](https://pages.nist.gov/800-63-4/sp800-63a/ial/). Privacy/security design remains bounded by [45 CFR 164.312](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.312). State-specific clinical gates continue to reference [California Business and Professions Code § 2290.5](https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?sectionNum=2290.5.&lawCode=BPC), [Florida Statutes § 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&Search_String=&URL=0400-0499/0456/Sections/0456.47.html), and [Georgia Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3-.07).

## 4. Evidence-gate observations

The first isolated setup applied migrations before loading the synthetic gold fixture. Because the fixture reset cannot drop migration-dependent tables, that disposable project volume was removed and recreated, the fixture was loaded first, and all 254 migrations then applied cleanly. No shared or production database was edited.

The first live proof exposed an Npgsql provider-shape defect: `ExecuteScalarAsync` returned a `DateTime` for a PostgreSQL timestamp while the repository cast directly to `DateTimeOffset`. The repository now uses the typed data reader, and the full 266-test plus live PostgreSQL evidence passed. The same proof also exposed a test-only minimization regex that matched explicit false flag names; it was narrowed to exact raw-field/schema checks without weakening the product boundary.

The runtime-safety source check initially treated the explicit `NON_BIOMETRIC` profile label as if it were biometric payload. It was narrowed to raw biometric/government-identifier field patterns, after which all 22 checks passed. A final live-proof command initially omitted the isolated Compose project label and correctly failed because the default service was stopped; the labeled rerun passed 14/14 and the default database remained untouched.

During the broad browser run, Docker Desktop stopped and then failed restart on inaccessible stale Unix-socket reparse points. Its optional AI/inference settings were disabled, the exact ephemeral runtime socket directories were preserved under timestamped `.stale-*` names, Docker restarted without a factory reset, and the isolated containers resumed against their original volume. The complete 52-test telehealth matrix then passed. No Docker image, project database volume, or repository file was reset as part of that correction.

The migration rehearsal deliberately injected process failures at three checkpoints and deliberately corrupted and extended the migration ledger. Recovery succeeded at every checkpoint; checksum drift and the unexpected migration were rejected as required.

Graphify rebuilt and portably validated the deterministic code-only index. The Sprint 21 feature, tests, and migration are still untracked in this working tree, so focused delta review reported zero changed graph nodes and surfaced test-gap hints. Direct source review plus the 266 backend tests, 263 frontend tests, PostgreSQL, API, authorization, migration, live, and 52-test browser evidence above is authoritative for this increment.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- route registration disappears when the feature is disabled;
- inactive, unreported, stale, mismatched, expired, negative-network, cross-applicant, or otherwise incomplete upstream evidence cannot reach or persist a proofing result;
- immutable evidence is not destructively rolled back; correction requires a separately reviewed forward migration;
- disabling/removing the route and panel leaves existing synthetic evidence inert;
- stop conditions include client influence over authoritative inquiry/result facts, raw identity data, evidence/document/identifier/biometric capture, authoritative-source or identity-provider transport, NIST IAL or conformance claim, hidden upstream state inferred as approval, replay overwrite, browser reference/result persistence, patient/account/chart promotion, canonical/downstream mutation, or earlier safeguard regression; and
- every result remains synthetic, non-clinical, non-production, and unusable for real identity, account enrollment, coverage/payment, exact physician network, queue placement, or patient-care decisions.

## 6. Open review gates

Independent identity-provider and fraud/risk vendor, NIST identity specialist, licensed clinical/medical-director, Georgia/California/Florida legal and regulatory, security/privacy, data, accessibility, interoperability, payer/network, operational, and program-owner packet reviews remain open. No real proofing vendor, authoritative source, evidence issuer, document or biometric capture, IAL assessment, authenticator binding, patient promotion/linkage, portal enrollment, consent, practice acceptance, rendering-physician network confirmation, canonical coverage, estimate/payment, request/queue entry, encounter, or care action has been approved or implemented. Until those reviews and separately bounded decisions are recorded, Sprint 21 remains a disabled synthetic process-contract slice and every production, real-identity, patient-promotion, downstream, and patient-care gate remains closed.
