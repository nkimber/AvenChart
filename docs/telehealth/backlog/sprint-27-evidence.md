# Sprint 27 synthetic communication/access readiness evidence

Status: Bounded automated evidence passing; independent language-access, disability-access, patient-registration, privacy/security, accessibility, data, legal, clinical, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0030](../decisions/0030-approved-sprint-27-synthetic-communication-access-readiness.md)  
Scope: Disabled, synthetic-only, applicant-owned recording of bounded communication preferences and five readiness acknowledgments after synthetic insurance handoff confirmation; no interpreter or accommodation arrangement, technology-readiness conclusion, patient mutation, completed intake, consent, acceptance, request, queue, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Full server-side provenance | Every read and write rebinds the applicant-key owner, practice/facility, successful promotion, portal-disabled unmerged patient shell, registration receipt, insurance-handoff receipt, passing safety location, and verified callback source. Missing, stale, expired, cross-applicant, patient-drifted, portal-enabled, merged, canonical-insurance, or receipt-drifted state fails closed. |
| Minimized server-owned context | The read projection exposes only the current two-letter state, callback last four, the `English`/`Spanish` allowlist, policy/version, and explicit limitations. The browser cannot author location, callback, patient fields, free-text support details, or alternative languages. |
| Explicit readiness acknowledgments | The command requires current location, callback, safe/private communication, disconnection/emergency plan, and synthetic-only use confirmations. Unsafe/private false and any partial confirmation are rejected without a successful receipt. |
| Preferences are not fulfillment | Interpreter and accessibility-support booleans record preferences only. Interpreter assignment, accommodation arrangement, communication arrangement, support-request creation, and technology readiness remain explicitly false. |
| Atomic append-only evidence | V0304 constrains one receipt per applicant, exact practice-scoped replay, bounded values, five affirmations, source snapshot, policy/expiry, and every no-consequence flag. Receipt, `SyntheticInsuranceDetailsConfirmed -> SyntheticCommunicationAccessReadinessRecorded`, and the applicant event commit in one transaction and reject update/delete. |
| Replay and contention | Exact retry returns the first immutable result. Changed-key reuse, stale version/fingerprint, a second semantic command, and concurrent first writers fail closed. Eight simultaneous California commands produced one receipt and one event. |
| Patient UX | The promoted-applicant flow shows the masked callback and server-owned state, labels language/support selections as preferences, blocks submission until all five confirmations are true, preserves the exact command after an ambiguous failure, focuses status, supports 320-pixel reflow, and stores no readiness data in browser storage. |
| Consequence boundary | Applicant source fields, patient and insurance rows, support/communication records, portal, completed intake, consent, acceptance, financial records, request, queue, appointment, encounter, care, prescribing, claim, integration, and external-call consequences remain unchanged or false. |
| Runtime and governance | Readiness requires 44 tables. Decision 0030, the [Sprint 27 plan](sprint-27-synthetic-communication-access-readiness.md), safeguard TH-SG-032, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, CI invocation, and planning validation are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused communication/access policy tests | 14 passed, covering bounded language normalization, five mandatory affirmations, deterministic snapshot behavior, invalid input, and hard-false service/care consequences |
| Full backend tests | 338 passed, 0 failed, 0 skipped |
| Full frontend tests | 53 files and 276 tests passed |
| Frontend lint, TypeScript, production build, and bundle budget | Passed; 137 JavaScript chunks checked and the initial bundle was 246,395 of 256,000 bytes |
| Cross-browser telehealth accessibility/recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the promoted-applicant journey covers notice, registration, insurance handoff, and communication readiness with masking, explicit limitations, disabled submit, all acknowledgments, exact ambiguous retry, focus, reflow, and automated WCAG checks |
| Full migration and recovery rehearsal | 260 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of V0304 checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 82 checks passed; V0282–V0304, all 44 telehealth tables, all 37 append-only triggers, policy/expiry/no-consequence constraints, full provenance guards, and append-only behavior passed |
| Telehealth authorization proof | 70 checks passed, including absent applicant key, portal-session substitution, patient/staff/physician separation, purpose of use, and practice/facility isolation |
| Telehealth OpenAPI proof | 38 checks passed, including applicant-only GET/POST security, private reads, required idempotency, bounded typed input, explicit no-arrangement output, and identifier minimization |
| Telehealth runtime-safety proof | 28 top-level checks passed; 44-table synthetic readiness was healthy and no interpreter/accommodation fulfillment, patient mutation, care, downstream, integration, or outbound path was introduced |
| Live synthetic communication/access-readiness proof | 12 checks passed for Georgia, California, and Florida: minimized read; ownership/partial/unsafe/stale/fingerprint/drift rejection; exact replay and changed/second-command rejection; eight-way contention; three-state parity; append-only evidence; provenance; and zero patient/downstream delta |
| Shared queue/lifecycle stress regression | 134 checks passed, including one winner among 20 concurrent reserve-next callers, authorization/video/consultation/documentation/pharmacy/prescription/disposition boundaries, audit evidence, and restoration of mutable fixtures |
| Planning/backlog validator | Validator v2.4 passed 72 checks with Decision 0030 and all 32 safeguards; 111 Markdown files and 357 relative links were clean, and all three controlled negative mutations were rejected |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact validation passed. Review-delta surfaced the shared `Program.cs` and `api.ts` hubs. The index does not include the still-uncommitted Sprint 27 files, so its reported test gaps are an indexing limitation addressed directly by the policy, API, live PostgreSQL, frontend, and browser evidence above. |

Machine-readable live results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-communication-access-readiness.json`. The result contains deterministic synthetic IDs, masks, bounded preferences, and false consequence facts only; it contains no canonical patient ID, raw callback number, street address, protected insurance value, subscriber identity, clinical history, or external-provider response.

The authoritative live run used a newly created disposable `avenchart_test_telehealth_s27` database and an exact rebuilt API container. Readiness independently reported 260 applied migrations, V0304 as the latest packaged migration, and 44 of 44 telehealth tables before the application proof. The normal database remained unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`. The isolated database remained healthy with its patient trigger enabled through the final evidence check and was then removed with its exact API container.

## 3. Proven boundaries

- only a current `SyntheticInsuranceDetailsConfirmed` applicant with exact promotion, patient, registration, insurance-handoff, safety-location, callback, practice, and facility provenance can read or record readiness;
- only state, callback last four, two allowlisted language options, two boolean preferences, and explicit limitations cross the route;
- all five acknowledgments are independent and mandatory, including safe/private communication and the disconnection/emergency plan;
- interpreter and accessibility selections do not create a request or claim that any service, accommodation, communication setup, or technology check is complete;
- one successful command, including concurrent first writers, produces one receipt, one event, and one monotonic state transition; and
- applicant source, patient, insurance, support, communication, portal, intake, consent, acceptance, financial, request, queue, appointment, encounter, care, prescribing, claim, integration, and external-action records remain unchanged.

## 4. Evidence-gate observations

The first isolated queue-stress run stopped after early checks because the deliberately minimal database did not yet contain the established-patient synthetic insurance fixture. Adding the two known synthetic insurance rows to the disposable database allowed the unchanged 134-check harness to pass. This was test-fixture setup, not a product-code change.

The first full browser attempt had no API on its default port, and the next attempt used the isolated database without the suite's default portal patient. The final run explicitly bound both UI and direct test traffic to the isolated API and used its existing `MOD-PAT-0012` synthetic portal fixture; all 56 tests passed. Failed environment-start attempts are not counted as final evidence.

## 5. Rollback and stop evidence

- base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- missing, stale, expired, cross-applicant, portal-enabled, merged, patient-drifted, canonical-insurance, unsafe/private-false, or provenance-incomplete state cannot record readiness;
- immutable readiness evidence is not destructively rolled back; correction requires a separately reviewed forward migration/workflow;
- disabling/removing the routes and patient panel leaves the synthetic patient shell portal-disabled and without a communication arrangement or care capability; and
- stop conditions include browser-authored location/callback/patient/free-text fields, raw contact/patient/insurance disclosure, preferences represented as fulfillment, technology/intake/consent/acceptance/request/queue/care consequences, provenance divergence, history overwrite, or any earlier safeguard regression.

## 6. Open review gates

Independent language-access/interpreter-service, disability-access/accommodation, patient-registration, privacy/security, accessibility, data, legal/regulatory, licensed clinical/medical-director, interoperability, operations/support, and program-owner packet reviews remain open. Real interpreter or accommodation workflows, translation/localization, technology preflight, remaining demographics/history, completed intake, clinician disclosure/consent, practice acceptance, request/queue entry, appointment, communication/video, care, prescribing, billing/claim, pharmacy transmission, and every production action require later separately bounded decisions.
