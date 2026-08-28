# Sprint 18 protected synthetic prospective member-insurance details evidence

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0021](../decisions/0021-approved-sprint-18-prospective-member-insurance-details.md)  
Scope: Disabled, synthetic-only, applicant-owned minimum member/group/subscriber confirmation after a synthetic practice-plan precheck; purpose-protected raw payload and mask-only receipt, with no real insurance/PHI, card/OCR, government identifier, member matching, eligibility, benefits, exact network, canonical coverage, financial action, identity proofing, patient promotion/linkage, consent, request, queue, appointment, encounter, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Aggregate and immutable evidence | V0295 constrains `PracticeNetworkPrecheckRecorded -> MemberInsuranceDetailsRecorded`, one applicant-bound receipt, review/safety/purpose/precheck provenance, selected-plan snapshot, relationship/priority/masks, purpose-isolated protection metadata, semantic idempotency, 27 hard-false consequences, an explicit non-null database snapshot guard, and append-only receipt/event evidence. |
| Validation and protection policy | The member-details policy accepts only normalized `SYN-` identifiers and `Self/Spouse/Parent/Other`, rebinds self identity from the applicant, requires complete adult-bounded non-self subscriber identity, fixes priority to `Primary`, and requires both confirmations. ASP.NET Core Data Protection purpose `AvenChart.Telehealth.ProspectiveMemberInsuranceDetails.v1` protects the raw normalized payload before SQL persistence and fixed-time comparison validates exact replay. |
| HTTP contract | Idempotent `POST /api/telehealth/v1/applicants/{applicantId}/member-insurance-details` is branded-host and applicant-access-key protected, typed, private/no-store, opaque on ownership failure, conditional-input documented, and mask-only on success. |
| Applicant UX | The prospective entry exposes the form only after practice-plan precheck, labels every value synthetic, conditionally reveals non-self subscriber fields, preserves the 911 action, retains one in-memory command identity across ambiguous retry, clears raw inputs on success, renders only masks, and persists no insurance/subscriber values. |
| Runtime and governance | Readiness requires 35 tables; Decision 0021, the [Sprint 18 plan](sprint-18-prospective-member-insurance-details.md), safeguard TH-SG-023, CI runtime invocation, migration/OpenAPI/auth/runtime/live proofs, backlog authorization, runbook, and planning validator are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused prospective member-details backend tests | 30 passed, 0 failed, covering identifier/name/date normalization, relationship conditionality, self rebinding, masks, protected storage, exact/mismatched replay, and protection tamper failure |
| Full backend tests | 243 passed, 0 failed, 0 skipped |
| ASP.NET Core Release build and formatting | Passed with 0 warnings, 0 errors, and no formatting drift |
| Focused frontend API/component tests | 2 files and 29 tests passed, including typed transport, conditional subscriber fields, exact retry identity, masks, raw-value clearing, terminal minimization, emergency action, and no insurance persistence |
| Full frontend tests | 53 files and 258 tests passed |
| Frontend lint, TypeScript, and production build | Passed |
| Frontend bundle budget | Passed at 246,395 of 256,000 initial bytes; 137 JavaScript chunks checked; prospective entry chunk 33.26 kB |
| Cross-browser telehealth accessibility/recovery | 52 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the prospective journey now continues through protected member-detail confirmation |
| Full migration and recovery rehearsal | 251 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 |
| Telehealth migration/schema regression | 56 passed; V0282–V0295, all 35 telehealth tables, explicit upstream-row presence checks, database guards, and append-only behavior passed |
| Telehealth authorization proof | 48 passed, including applicant access-key non-substitution and all earlier role/resource controls |
| Telehealth OpenAPI proof | 27 passed, including the typed applicant-only member-details command, conditional subscriber input, required idempotency, bounded failures, mask-only response, and explicit false consequences |
| Telehealth runtime-safety proof | 19 top-level checks passed; the member-details path has purpose protection but no coverage gateway, canonical insurance mutation, or outbound integration path, and 35-table synthetic readiness remained healthy |
| Live prospective member-details proof | 13 checks passed: Georgia/California/Florida provenance, access/SYN/conditional/stale rejection, cross-applicant provenance rejection, self/non-self paths, exact replay, same-mask changed-content and second-command conflict, ciphertext-at-rest/no-plaintext, tamper failure, 12-way one-winner contention, mask-only response, coarse resume, 27 hard-false consequences, append-only rejection, and zero canonical/downstream delta |
| Earlier telehealth live and contention proofs | Applicant identity, staff review, prospective safety, visit purpose, practice-network precheck, and the 20-caller full queue/consultation lifecycle proof all passed unchanged |
| Generated empty bootstrap verification | Regenerated and verified; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | Passed with Decision 0021 and all 23 safeguards; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-prospective-member-insurance-details.json`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers, masks, and bounded facts only; they do not contain the protected payload or submitted raw insurance/subscriber values.

After evidence capture, the exact labeled Sprint 18 API/PostgreSQL containers, network, isolated volume, and disposable synthetic dataset were removed. That data is intentionally not recoverable. No `avenchart-sprint18` Docker resource remains. The pre-existing default PostgreSQL service was restarted against its untouched `avenchart_avenchart-postgres` volume and verified healthy and unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Insurance, protection, ownership, and UX results

- capture is reachable only by the applicant access-key owner after current no-candidate staff review, one `TelehealthEligible` universal safety evaluation, one controlled visit-purpose classification, and one immutable practice-plan precheck;
- member/group identifiers must be 6–32-character `SYN-` demonstration values; relationship is exactly `Self`, `Spouse`, `Parent`, or `Other`; self identity is rebound from the applicant, while non-self identity is conditionally required and remains protected;
- the raw normalized payload is protected before repository insertion and SQL receives only ciphertext, last-four masks, relationship, selected-plan/provenance facts, protection metadata, idempotency identity, and hard-false flags;
- exact retry unprotects and fixed-time compares the complete semantic payload; changed content—even with the same last-four mask—fails, and an unprotectable payload fails closed;
- a direct-SQL adversarial receipt using another applicant's review decision is rejected by the hardened snapshot trigger, preventing nullable-comparison semantics from weakening upstream provenance;
- 12 concurrent first writers produce one receipt and event; second semantic commands fail; both evidence types reject update and deletion;
- responses contain masks only and applicant resume stays coarse; raw identifiers, subscriber identity/date of birth, ciphertext, protection purpose, access key, clinical answers, staff evidence, and fingerprints remain absent;
- every matching, eligibility, benefit, exact-network, identity/patient, coverage/financial, request/queue, clinical, prescribing, billing/claim, communication, integration, and external-call consequence is explicitly false; and
- recording changes only the applicant aggregate plus one receipt and event; canonical insurance, patient, portal, intake, coverage, request, queue, appointment, encounter, prescription, claim, financial, messaging, and integration rows remain unchanged.

## 4. Boundary refinements found by the evidence gate

The first PowerShell validation fixture attempted to combine two hashtables containing the same subscriber keys. PowerShell rejects duplicate-key addition before the HTTP request; the fixture now clones and assigns those fields, and the product validation path is exercised as intended.

The initial database snapshot trigger used the final query's `FOUND` state plus nullable field comparisons. A deliberately mismatched review identifier could therefore make some comparisons evaluate to SQL `NULL` rather than `TRUE`. V0295 now requires the applicant, review, safety, purpose, and precheck row identifiers to be explicitly non-null before comparing provenance. The new adversarial live assertion failed against the weak shape and passes against the hardened trigger.

An attempted convenience command used the nonexistent `npm run typecheck` script and therefore performed no check. The repository's canonical `npx tsc -b --pretty false` command passed, followed by the full production build.

The first focused telehealth migration command used the old parameter spelling `-SkipBaseRecovery`; PowerShell rejected it before any assertions. The current `-SkipBaseRehearsal` command then passed all 56 checks. Neither command failure is counted as product evidence.

The migration rehearsal deliberately injected process failures at three checkpoints and deliberately corrupted/extended the migration ledger. Recovery succeeded at all checkpoints, while checksum drift and the unexpected migration were rejected as required.

Graphify rebuilt the deterministic code-only index and passed its portability check. The new feature, test, and migration paths remain untracked in this working tree, so focused delta review reported them as test-gap hints rather than pretending to model internal relationships; direct source validation and the focused, full, PostgreSQL, contract, and cross-browser evidence above remain authoritative for this increment.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- route registration disappears when the feature is disabled;
- no raw payload is written outside the purpose-protected field, and a payload that cannot be unprotected is never accepted as replay evidence;
- the database receipt is immutable and no destructive migration or evidence rollback is permitted; correction requires a separately reviewed forward migration;
- disabling/removing the route and form leaves existing synthetic evidence inert;
- stop conditions include non-`SYN-` acceptance, conditional-identity bypass, raw/ciphertext disclosure, missing upstream provenance, replay overwrite, any matching/eligibility/exact-network/canonical-coverage implication, canonical/downstream row creation, browser persistence, external action, or earlier safeguard regression; and
- every result remains synthetic, non-clinical, non-production, and unusable for coverage, payment, or patient-care decisions.

## 6. Open review gates

Independent payer/eligibility, identity, licensed clinical/medical-director, security/privacy, data, accessibility, interoperability, legal/compliance, operational, and program-owner packet reviews remain open. Durable production Data Protection key custody, backup, rotation, and disaster recovery are not supplied by this slice. No real member data, X12 270/271 transaction, payer or provider-directory query, member match, eligibility/benefits decision, exact physician participation decision, canonical coverage record, estimate/payment, identity proofing, patient promotion, consent, request/queue entry, or care action has been approved or implemented. Until those reviews and separately bounded decisions are recorded, Sprint 18 remains a disabled synthetic protected-receipt slice and every production, eligibility, exact-network, financial, patient-promotion, downstream, and patient-care gate remains closed.
