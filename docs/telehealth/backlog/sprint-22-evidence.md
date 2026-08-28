# Sprint 22 synthetic promotion-authorization evidence

Status: Bounded automated evidence passing; independent identity, clinical, legal, security/privacy, accessibility, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0025](../decisions/0025-approved-sprint-22-synthetic-promotion-authorization.md)  
Scope: Disabled, synthetic-only, staff-governed authorization or denial after a complete unexpired prospective-applicant process chain; the applicant remains prospective and no canonical patient, chart, account, portal identity, intake completion, consent, practice acceptance, coverage/financial record, request, queue, appointment, encounter, care, prescribing, claim, communication, downstream action, external integration, or production use is created

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Aggregate and immutable decision | V0299 constrains `SyntheticIdentityProofingRecorded -> SyntheticPromotionAuthorized/SyntheticPromotionDenied`, binds the decision to the complete same-applicant review/safety/purpose/precheck/member-receipt/eligibility/network/proofing chain, requires current positive upstream evidence, and records one append-only decision/event with database-enforced actor, scope, vocabulary, acknowledgments, replay, provenance, and hard-false consequence invariants. |
| Human governance checkpoint | `TelehealthApplicantPromotionAuthorizationPolicy` accepts only `AuthorizedForSyntheticPromotion` or `DeniedForSyntheticPromotion`, a normalized reason, `noneAssuranceAcknowledged=true`, and `syntheticDataConfirmed=true`. Authorization means permission for a later separately gated synthetic promotion exercise only; it does not create or identify a patient. |
| Staff authorization and minimization | The service and repository require an active administrator or bound front-desk staff member with the promotion-review permission and exact configured practice/facility scope. The queue exposes only normalized review-relevant synthetic facts, masked contact, upstream status/freshness, and decision options; it excludes member values, proofing references/evidence, candidate identities, and canonical identifiers. |
| Replay and concurrency | Exact persisted replay returns the immutable first result. Changed-content key reuse, stale version, expired/missing/cross-chain evidence, a second semantic command, scope mismatch, and concurrent first writers fail closed. Eight simultaneous first writers produce one decision and event. |
| HTTP contract | Private/no-store staff endpoints list queued reviews and append an idempotent decision. The request contains only expected version, an enumerated decision, normalized reason, and the two required acknowledgments; it cannot supply evidence, assurance, resulting state, or consequences. |
| Staff and applicant UX | The administrator workspace has an independent, accessible promotion-review section with evidence limitations, authorize/deny controls, two explicit acknowledgments, stable ambiguous retry, polling, and recovery. The applicant resume endpoint reports only a coarse authorized-or-denied status and never exposes staff rationale or hidden evidence. |
| Runtime and governance | Readiness requires 39 tables. Decision 0025, the [Sprint 22 plan](sprint-22-synthetic-promotion-authorization.md), safeguard TH-SG-027, CI runtime invocation, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, and the planning validator are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused promotion-authorization backend tests | 8 passed, covering decision normalization, acknowledgments, exact outcomes, status mapping, reason bounds, and deterministic fingerprints |
| Full backend tests | 274 passed, 0 failed, 0 skipped |
| ASP.NET Core Release build and formatting | Passed with 0 warnings, 0 errors, and no formatting drift |
| Focused frontend API/component tests | 2 files and 34 tests passed, including typed minimal transport, both decisions, explicit acknowledgments, stable retry, normalized evidence, and independent recovery |
| Full frontend tests | 53 files and 266 tests passed with file parallelism disabled |
| Frontend lint, TypeScript, production build, and bundle budget | Passed |
| Cross-browser telehealth accessibility/recovery | 52 passed in 3.2 minutes across desktop Chromium, mobile Chromium, Firefox, and WebKit; the administrator journey includes promotion review, explicit acknowledgments, decision submission, focus/reflow, and WCAG checks |
| Full migration and recovery rehearsal | 255 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 68 checks passed; V0282–V0299, all 39 telehealth tables, all 32 append-only trigger inventory entries, complete provenance, normalized snapshot, acknowledgment/replay/no-consequence guards, and append-only behavior passed |
| Telehealth authorization proof | 57 checks passed, including absent identity, portal-session substitution, and physician rejection for the staff decision route |
| Telehealth OpenAPI proof | 32 checks passed, including permission metadata, typed minimal input, required idempotency, bounded failures, normalized output, and explicit false consequences |
| Telehealth runtime-safety proof | 23 top-level checks passed; 39-table synthetic readiness was healthy and no patient promotion, canonical mutation, downstream action, or outbound path was present |
| Live promotion-authorization proof | 12 checks passed: queue minimization, role/scope/acknowledgment/stale/expired rejection, authorization and denial, exact replay, changed-content and second-command rejection, eight-way one-winner contention, append-only evidence, coarse applicant resume, and zero canonical/downstream delta |
| Generated empty bootstrap verification | Verified against the migration-derived generator output; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | Passed with Decision 0025 and all 27 safeguards; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-promotion-authorization.json`, with repository recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers, normalized statuses, decision metadata, and bounded false consequence facts only; they contain no raw member value, identity document, government identifier, biometric, proofing evidence, real applicant identity, payer response, authoritative-source response, or external-provider response.

The live proof ran against a disposable database created from the synthetic gold fixture. The default development database had a pre-existing V0200 migration checksum mismatch and correctly failed closed before any Sprint 22 migration was applied. It was not repaired, overwritten, or migrated for this evidence run. After capture, the exact disposable API container and the `avenchart_sprint22` and `avenchart_test_sprint22` databases were removed. The normal PostgreSQL service was recreated against its existing volume and verified healthy and unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Governance, state, and UX results

- only an active authorized administrator or bound front-desk staff member for the configured practice/facility can list or decide eligible applicants;
- an applicant appears only after the exact same aggregate has complete, unexpired evidence through `SyntheticIdentityProofingRecorded`, active/reported eligibility, an accepting practice-network result, `SyntheticProofingPassed`, assurance `None`, `identityProofed=false`, and false real-evidence/source/biometric/authenticator flags;
- the browser cannot supply upstream evidence, applicant identity facts, assurance, resulting status, actor attribution, policy metadata, timestamps, or consequence values;
- `AuthorizedForSyntheticPromotion` is deliberately not patient creation, matching, proofing, practice acceptance, identity enrollment, or authorization for care;
- exact retry returns immutable evidence; eight concurrent first writers produce one decision and event; changed-content reuse, a second command, stale or expired evidence, and update/delete of decision history fail;
- the applicant sees only a coarse authorized-or-denied status and restart option, while the staff view omits raw member/proofing evidence and canonical identifiers;
- Georgia, California, and Florida remain bounded state-context inputs only. This checkpoint starts no clinical relationship, encounter, diagnosis, prescription, billing, or care action; state telehealth consent, licensure, standard-of-care, record, prescribing, and emergency obligations remain later gates; and
- every identity-proofing, patient/account/chart, portal/authenticator, intake/consent/acceptance, coverage/financial, request/queue, appointment/encounter, clinical, prescribing, billing/claim, communication, integration, and external-call consequence remains false with zero canonical/downstream row delta.

The checkpoint was reviewed against the official [NIST SP 800-63A-4 general identity-proofing requirements](https://pages.nist.gov/800-63-4/sp800-63a/ial-general/) without making an assurance or conformance claim. Privacy/security design remains bounded by [45 CFR 164.312](https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C/section-164.312). State-specific clinical gates continue to reference [California Business and Professions Code § 2290.5](https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?sectionNum=2290.5.&lawCode=BPC), [Florida Statutes § 456.47](https://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&Search_String=&URL=0400-0499/0456/Sections/0456.47.html), and [Georgia Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3-.07).

## 4. Evidence-gate observations

The default database’s existing checksum drift was discovered when the normal migrator rejected V0200. The default database was left untouched. An initial disposable database lacked the synthetic gold fixtures required by the live chain; a second isolated gold-seeded database was used for authoritative evidence instead.

The first live run exposed a missing SQL separator and a PostgreSQL timestamp provider-shape mismatch. Adding the separator and reading the timestamp through a typed data reader corrected the repository without loosening the schema boundary. The live minimization check also initially matched the explicit `NON_BIOMETRIC` label; its raw-field pattern was narrowed without weakening product behavior.

The first full browser run exposed one ambiguous checkbox locator after the new acknowledgments were added. The test now targets the exact promotion acknowledgment, and both the focused four-browser rerun and complete 52-test matrix passed. The same review found that the applicant resume service treated the two new terminal prospective statuses as expired; it now returns explicit coarse, privacy-safe authorization/denial messages.

The migration rehearsal deliberately injected process failures at three checkpoints and deliberately corrupted and extended the migration ledger. Recovery succeeded at every checkpoint; checksum drift and the unexpected migration were rejected as required.

Graphify rebuilt and portably validated the deterministic code-only index. The telehealth feature tree remains untracked in this working tree, so focused review of the eight principal Sprint 22 files reported zero changed graph nodes and surfaced generic test-gap hints. Direct source review plus the 274 backend tests, 266 frontend tests, live PostgreSQL proofs, migration/API/authorization/runtime checks, and 52-test browser matrix above is authoritative for this increment.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- route registration disappears when the feature is disabled;
- incomplete, stale, expired, mismatched, cross-applicant, nonpassing, or already-decided evidence cannot reach or persist a promotion decision;
- immutable evidence is not destructively rolled back; correction requires a separately reviewed forward migration;
- disabling/removing the routes and panel leaves existing synthetic evidence inert;
- stop conditions include client influence over evidence, assurance, result, or consequences; raw insurance or proofing disclosure; bypassed actor scope or acknowledgments; replay overwrite; applicant access to rationale; canonical patient/account/chart or downstream mutation; or earlier safeguard regression; and
- every result remains synthetic, non-clinical, non-production, and unusable for real identity, enrollment, coverage/payment, request/queue placement, or patient-care decisions.

## 6. Open review gates

Independent identity/fraud, NIST identity-specialist, licensed clinical/medical-director, Georgia/California/Florida legal and regulatory, security/privacy, data, accessibility, interoperability, payer/network, operational, and program-owner packet reviews remain open. No real proofing vendor, authoritative source, patient promotion/linkage, portal enrollment, authenticator, complete intake, consent, practice acceptance, rendering-physician network confirmation, canonical coverage, estimate/payment, request/queue entry, encounter, or care action has been approved or implemented. Until those reviews and separately bounded decisions are recorded, Sprint 22 remains a disabled synthetic governance checkpoint and every production, real-identity, promotion, downstream, and patient-care gate remains closed.
