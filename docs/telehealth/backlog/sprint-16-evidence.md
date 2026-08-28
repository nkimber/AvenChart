# Sprint 16 synthetic prospective visit-purpose evidence

Status: Bounded automated evidence passing; independent reviews and program-owner packet review pending  
Decision: [TH-DEC-0019](../decisions/0019-approved-sprint-16-prospective-visit-purpose.md)  
Scope: Disabled, synthetic-only, applicant-owned controlled migraine/sleep navigation classification after a passing universal safety screen; no free text, complaint-specific protocol, clinical eligibility, identity proofing, patient promotion/linkage, insurance, consent, request, queue, clinician review, care, downstream action, external integration, production use, or real PHI

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Aggregate and immutable evidence | V0293 constrains `SafetyScreenPassed -> VisitPurposeRecorded`, one applicant-bound purpose, the exact two-value category/label mapping, passing safety/review provenance, semantic idempotency, hard-false consequence flags, a database snapshot guard, and append-only purpose/event evidence. |
| Server policy and transaction | `TelehealthProspectiveVisitPurposePolicy`, repository, and service normalize only `migraine` or `sleep`, require synthetic confirmation, rebind host/practice/facility/access/version/review/safety state under the applicant lock, and never call a clinical evaluator or downstream adapter. |
| HTTP contract | `POST /api/telehealth/v1/applicants/{applicantId}/visit-purpose` is applicant-access-key protected, idempotent, typed, private/no-store, opaque on ownership failure, and bounded to 400/401/404/409/410 failures. |
| Applicant UX | The prospective entry presents two semantic radio options, persistent emergency direction, explicit non-diagnosis/non-eligibility content, synthetic confirmation, stable ambiguous retry, focus recovery, and a terminal no-consequence result without storing purpose data. |
| Runtime and governance | Readiness requires 33 tables; Decision 0019, Sprint 16 plan, safeguard TH-SG-021, CI runtime invocation, migration/OpenAPI/auth/runtime/live proofs, backlog authorization, runbook, and planning validator are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused telehealth backend tests | 145 passed, 0 failed, including 10 prospective visit-purpose policy cases |
| Focused telehealth frontend tests | 10 files, 47 tests passed, including controlled input, transport minimization, ambiguous retry, focus recovery, terminal result, and no purpose persistence |
| Live prospective visit-purpose proof | 12 checks passed: prerequisite provenance, arbitrary/access/stale rejection, both fixed categories, exact replay, changed/second-command conflict, 12-way one-winner contention, public minimization, hard-false consequences, append-only rejection, and zero canonical/downstream delta |
| ASP.NET Core Release build | Passed with 0 warnings and 0 errors |
| Full backend tests | 198 passed, 0 failed, 0 skipped |
| C# format verification | Passed with no changes required |
| Full frontend tests | 53 files, 253 tests passed |
| Frontend lint and TypeScript/build | Passed |
| Frontend bundle budget | Passed at 246,395 of 256,000 initial bytes; 137 JavaScript chunks checked |
| Cross-browser telehealth accessibility/recovery | 52 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the safety-to-purpose journey includes persistent 911 action, radio semantics, 320 px reflow, serious automated WCAG checks, same-key retry, focus recovery, exact body assertions, and no purpose storage |
| Full migration and recovery rehearsal | 249 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed |
| Telehealth migration/schema regression | 50 passed; V0282–V0293, all 33 telehealth tables, 26 append-only triggers, and every earlier control passed |
| Telehealth authorization proof | 43 passed, including absent applicant access-key denial for the purpose command and all earlier role/resource boundaries |
| Telehealth OpenAPI proof | 24 passed, including the typed applicant-only purpose command, required idempotency, bounded failures, minimal input, and explicit no-consequence output |
| Telehealth runtime-safety proof | 17 top-level checks passed; no clinical evaluator/downstream/outbound source path and 33-table synthetic readiness remained healthy |
| Prospective identity, identity-review, and safety regressions | 11, 14, and 12 passed respectively, preserving contention, privacy, append-only evidence, priority, and zero canonical deltas |
| Real-PostgreSQL end-to-end concurrency proof | 134 passed, preserving all prior workflow, ownership, exact-replay, append-only, contention, lifecycle, privacy, and zero-downstream controls |
| Generated empty bootstrap verification | Passed unchanged; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | Passed with Decision 0019 and all 21 safeguards; all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable runtime results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-prospective-visit-purpose.json`, with recovery evidence under `avenchart/artifacts/migration-resilience/`. They contain deterministic synthetic identifiers and bounded facts only.

After evidence capture, the exact labeled Sprint 16 API/PostgreSQL containers, network, isolated volume, and disposable synthetic dataset were removed. That data is intentionally not recoverable. No `avenchart-sprint16` Docker resource remains. The pre-existing default PostgreSQL service stayed healthy and was verified unchanged at 237 migrations, 1,000 synthetic patients, and latest migration `V0281__index_flow_board_appointments_by_date`.

## 3. Clinical-safety, ownership, privacy, and UX results

- purpose classification is reachable only after current no-candidate staff approval and one `TelehealthEligible` universal safety evaluation from `synthetic-universal-safety` version 1;
- the server accepts no narrative, symptom, diagnosis, medication, insurance, patient, request, or clinician input—only expected version, one controlled category, and synthetic confirmation;
- `migraine` maps only to `Headache or known migraine pattern`, and `sleep` only to `Sleep difficulty`; neither category runs or implies a complaint-specific clinical protocol, diagnosis, eligibility, or treatment decision;
- exact retry converges; changed, stale, second, and losing concurrent commands fail; 12 concurrent first writers produce one purpose and event; and both evidence types reject destructive mutation;
- every protocol-published, clinical-eligibility, identity-proofing, patient/chart, complete-intake, coverage, request, queue, and care capability is explicitly false;
- recording changes only the applicant aggregate plus one purpose and one event; patient, portal, insurance, intake, coverage, request, queue, appointment, encounter, prescription, claim, and financial rows remain unchanged; and
- the browser retains one ambiguous command identity for explicit retry but persists only applicant ID/access key, never the category or purpose body.

## 4. Boundary refinements found by the evidence gate

The first migration source assertion expected three no-consequence columns on one physical line even though V0293 deliberately wrapped the check constraint. The proof now permits SQL whitespace while retaining the same required columns. The first runtime source assertion expected wording not used by the fixed service limitations; it now binds to the actual statement that the categories are not diagnoses or approved clinical protocols. Both authoritative reruns passed.

The full migration rehearsal initially met a host-port collision because the still-running isolated Sprint 16 database owned port 5433. That first attempt stopped before its test migrator connected. The disposable Sprint 16 evidence stack was then removed, the pre-existing default PostgreSQL container was force-recreated against its unchanged named volume to restore its Compose network attachment, and the complete rehearsal passed. A direct ledger/patient/latest-migration query confirmed the default dataset was unchanged.

Graphify rebuilt the committed code-only index and passed portability. Because the new telehealth feature files are still untracked in this working tree, review-delta reported zero indexed impact and test-gap hints rather than pretending to cover them. Direct source validation plus the focused, full, live PostgreSQL, browser, migration, and contract evidence above remains authoritative for this increment.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- route registration disappears when the feature is disabled;
- no destructive migration or evidence rollback is permitted; a correction requires a separately reviewed forward migration;
- stop conditions include any unsafe-state submission, arbitrary/free-text acceptance, clinical-eligibility implication, overwrite/duplicate purpose, downstream row or external action, browser/log persistence, or earlier safeguard regression; and
- every result remains synthetic, non-clinical, non-production, and unusable for patient care.

## 6. Open review gates

Independent identity, licensed clinical/medical-director, security/privacy, data, accessibility, interoperability, legal/compliance, operational, and program-owner packet review remain open. No headache, migraine, insomnia, or sleep protocol has been approved or implemented. Until those reviews and any separately required protocol decision are recorded, Sprint 16 remains a disabled synthetic navigation slice and every production, identity-proofing, patient-promotion, complaint-specific triage, eligibility, request/queue, downstream, and patient-care gate remains closed.
