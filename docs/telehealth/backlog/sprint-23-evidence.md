# Sprint 23 atomic synthetic patient-promotion evidence

Status: Bounded automated evidence passing; independent identity, patient-matching, clinical, legal, security/privacy, accessibility, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0026](../decisions/0026-approved-sprint-23-atomic-synthetic-patient-promotion.md)  
Scope: Disabled, synthetic-only, administrator-executed atomic creation of one minimal portal-disabled patient shell after explicit authorization, or a privacy-safe duplicate block; no existing-patient linkage, portal identity, complete intake, consent, practice acceptance, coverage, request, queue, appointment, encounter, care, prescribing, billing, communication, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Atomic aggregate and patient provenance | V0300 constrains `SyntheticPromotionAuthorized -> SyntheticPatientPromoted/SyntheticPromotionBlockedPossibleMatch`, stores one append-only promotion result, binds a created patient shell to the applicant and exact authorization decision, and rejects inconsistent patient/outcome combinations independently of application code. |
| Current patient matching | The repository acquires the canonical patient-registration advisory transaction lock, locks the applicant, validates the entire immutable prospective chain, and repeats the configured facility-scoped name/date-of-birth, date-of-birth/email, and date-of-birth/phone match query in the same PostgreSQL transaction. |
| Fail-closed outcomes | A current possible match creates no patient, reveals no candidate, and records only `BlockedPossiblePatientMatch`. A no-match result creates exactly one deterministic `TH-PAT-<applicant-guid-n>` shell with `portal_enabled=false`, no provider, and no inferred consent or communication preference. Existing patients are never linked or changed. |
| Authorization and command minimization | Only an active administrator in the configured practice/facility may execute the command. The input accepts expected version, `PromoteAuthorizedSyntheticApplicant`, a normalized reason, and two explicit acknowledgments; it cannot supply demographics, identifiers, match results, assurance, outcome, or consequence flags. |
| Replay and concurrency | Exact persisted retry returns the immutable first result. Changed-key reuse, stale/expired/mismatched evidence, denial, a second semantic command, identifier collision, and concurrent first writers fail closed with at most one patient, result, and event. |
| HTTP and applicant privacy | Private/no-store endpoints return minimized authorization candidates and results without legacy PID, candidate identity/count, member values, or proofing evidence. Applicant resume exposes only a coarse promoted-or-blocked state and no canonical identifier. |
| Staff UX | The administrator workspace has a separate accessible execution section with the consequence boundary, both acknowledgments, disabled submit until complete, stable ambiguous retry, polling, and recovery. |
| Runtime and governance | Readiness requires 40 tables. Decision 0026, the [Sprint 23 plan](sprint-23-atomic-synthetic-patient-promotion.md), safeguard TH-SG-028, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, CI invocation, and the planning validator are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused promotion backend tests | 9 passed, covering command and reason normalization, explicit acknowledgments, exact outcomes/statuses, deterministic patient identity, and fingerprints |
| Full backend tests | 283 passed, 0 failed, 0 skipped |
| ASP.NET Core Release build and formatting | Passed with 0 warnings, 0 errors, and no formatting drift |
| Focused frontend API/component tests | 3 files and 37 tests passed, including typed minimal transport, consequence acknowledgments, promoted/blocked outcomes, stable retry, polling, and recovery |
| Full frontend tests | 53 files and 269 tests passed with file parallelism disabled |
| Frontend lint, TypeScript, production build, and bundle budget | Passed; 137 JavaScript chunks checked and the initial bundle remained within budget |
| Cross-browser telehealth accessibility/recovery | 52 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the administrator journey includes promotion execution, explicit acknowledgments, focus/reflow, recovery, and WCAG checks |
| Full migration and recovery rehearsal | 256 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 71 checks passed; V0282–V0300, all 40 telehealth tables, all 33 append-only triggers, constrained provenance, acknowledgments, replay, no-downstream facts, and append-only behavior passed |
| Telehealth authorization proof | 62 checks passed, including absent identity, portal-session substitution, front-desk exclusion in the live proof, physician rejection, purpose-of-use, and practice/facility isolation |
| Telehealth OpenAPI proof | 34 checks passed, including administrator-only permission metadata, typed minimal input, required idempotency, bounded failures, and minimized outputs with no candidate or canonical identifiers |
| Telehealth runtime-safety proof | 24 top-level checks passed; 40-table synthetic readiness was healthy and no portal, care, downstream, integration, or outbound path was introduced |
| Live atomic promotion proof | 13 checks passed: minimized queue; role/scope/denied/acknowledgment/stale rejection; atomic no-match creation; deterministic minimal portal-disabled mapping; privacy-safe duplicate block; exact replay; changed-content and second-command rejection; eight-way one-winner contention; coarse applicant resume; append-only evidence; minimized schema; and zero downstream delta |
| Generated empty bootstrap verification | Verified against migration-derived output; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 68 checks passed with Decision 0026 and all 28 safeguards; 99 Markdown files and 310 relative links were clean, and all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable live results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-synthetic-promotion.json`. The evidence contains deterministic synthetic identifiers and normalized statuses only; it contains no duplicate candidate identity/count, raw member value, government identifier, biometric, identity-proofing evidence, payer response, authoritative-source response, or external-provider response.

The live proof ran against a disposable database cloned from the synthetic gold fixture so the existing patient-match surface was realistic. The default development database's pre-existing migration-checksum drift remained untouched. The isolated ledger was reconciled only inside the disposable database before applying the current packaged migrations; a separate fresh empty-database rehearsal independently applied all 256 migrations from the committed bootstrap and migration catalog.

After evidence capture, the exact Sprint 23 API container and `avenchart_test_sprint23` database were removed. The normal PostgreSQL service was left healthy on its existing volume and verified unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Proven boundaries

- promotion requires the exact current same-applicant chain through an unexpired `AuthorizedForSyntheticPromotion` decision, with assurance `None`, `identityProofed=false`, and every real-evidence, authoritative-source, biometric, and authenticator flag false;
- one successful no-match command, including eight concurrent first writers, produces one patient shell, one immutable promotion result, and one aggregate event;
- a newly present possible match produces no patient, no link or merge, no existing-patient change, and no disclosure that could identify a candidate;
- a created shell is deliberately not a portal account, authenticated identity, completed intake, accepted patient, confirmed coverage, request, queue entry, appointment, encounter, chart, or authorization for care;
- the live evidence observed zero change to portal identity, insurance, request, queue, intake, confirmation, location, coverage verification, appointment, encounter, claim, and prescription records; and
- Georgia, California, and Florida remain bounded state-context inputs only. No state-specific clinical relationship, consent, prescribing, or standard-of-care conclusion is made by this checkpoint.

## 4. Evidence-gate observations

The realistic live rehearsal exposed two migration assumptions before sealing: V0300 initially used obsolete prospective-state names and a stale practice identifier. The migration was corrected to extend the mature state vocabulary and bind the configured synthetic practice. The same live proof found that applicant resume reported a promoted state while retaining the prior “not a patient” flag and limitation. The coarse response now confirms that a patient shell was created while continuing to withhold its identifier and every portal or care capability.

The evidence scripts also caught two brittle assertions: one assumed adjacent SQL formatting for `portal_enabled=false`, and the new random-secret helper assumed PowerShell 7 APIs. The source assertion now checks the actual column/value mapping, and the helper uses a compatible cryptographic generator. The authoritative live suite passes under the CI PowerShell 7 runtime.

Graphify rebuilt and portably validated the deterministic code-only index. The telehealth feature tree remains untracked in this working tree, so focused review of the eight principal Sprint 23 files reported zero changed graph nodes and surfaced generic test-gap hints. Direct source review plus the 283 backend tests, 269 frontend tests, live PostgreSQL proofs, migration/API/authorization/runtime checks, and 52-test browser matrix above is authoritative for this increment.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- a possible current match cannot create a patient or disclose/link/merge an existing one;
- a no-match command cannot create more than one patient shell, promotion record, or event, including under concurrent first writers;
- immutable promotion and patient provenance is not destructively rolled back; correction requires an independently reviewed patient-safety workflow and forward migration;
- disabling/removing the routes and panel leaves synthetic patient shells inert and portal-disabled; and
- stop conditions include duplicate recheck outside the registration lock, partial provenance, candidate disclosure, client-authored demographics/outcomes, portal or downstream mutation, or any earlier safeguard regression.

## 6. Open review gates

Independent identity/fraud and patient-matching review; licensed clinical/medical-director review; Georgia, California, and Florida legal/regulatory review; security/privacy, accessibility, data, interoperability, payer/network, operational, and program-owner packet review remain open. Real identity proofing, authoritative-source validation, duplicate resolution or linkage, portal enrollment/authentication, remaining demographics, complete intake, telehealth consent, practice acceptance, rendering-clinician network confirmation, canonical coverage, estimate/payment, request/queue entry, appointment, encounter, communication/video, prescribing, billing/claim, pharmacy transmission, and every production action require later separately bounded decisions.
