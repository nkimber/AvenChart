# Sprint 29 synthetic clinical-information-inventory evidence

Status: Bounded automated evidence passing; independent medication-safety, allergy-safety, patient-registration/HIM, privacy/security, accessibility, data, legal, clinical, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0032](../decisions/0032-approved-sprint-29-synthetic-clinical-information-inventory.md)  
Scope: Disabled, synthetic-only, applicant-owned recording of coarse patient-reported medication, allergy/intolerance, and other-health-history inventory states after device preparation; no detailed clinical information, reconciliation, verified “no known” assertion, clinician task, completed intake, eligibility, patient mutation, request, queue, care, prescribing, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Full server-side provenance | Every read and write rebinds the applicant-key owner, practice/facility, successful promotion, portal-disabled unmerged patient shell, registration receipt, insurance-handoff receipt, communication/access receipt, passing device-preparation receipt, passing safety location, and verified callback source. Missing, stale, expired, cross-applicant, patient-drifted, portal-enabled, merged, canonical-insurance, or receipt-drifted state fails closed. |
| Minimized inventory contract | The route accepts exactly one of `PatientReportsNone`, `ItemsToReview`, or `Unsure` for each of medications, allergies/intolerances, and other health history, plus three mandatory acknowledgments. Medication, substance, reaction, diagnosis, symptom, procedure, dose, identifier, date, narrative, attachment, and free text cannot cross the route. |
| Provisional patient report only | `PatientReportsNone` is not represented as a reconciled or clinically verified “no known” assertion. Medication, allergy, and history reconciliation remain explicitly false. |
| Informational routing only | The server derives `DetailedCollectionRequired` when any category has items, otherwise `AssistedReviewRequired` when any category is unsure, otherwise `PendingClinicianReconciliation`. No task, review queue, priority, recommendation, clinical eligibility, or operational authority is created. |
| Atomic append-only evidence | V0306 constrains one receipt per applicant, exact practice-scoped replay, bounded values, all acknowledgments, source snapshot, policy/expiry, and every no-consequence flag. Receipt, `SyntheticDevicePreparationRecorded -> SyntheticClinicalInformationInventoryRecorded`, and the applicant event commit in one transaction and reject update/delete. |
| Replay and contention | Exact retry returns the first immutable result only after provenance is revalidated again inside the transaction. Changed-key reuse, stale version/fingerprint, source drift, a second semantic command, and concurrent first writers fail closed with at most one receipt and one event. |
| Patient UX | The promoted-applicant flow presents three distinct radio groups and provisional language, requires all acknowledgments, preserves the exact command after an ambiguous failure, offers stable retry, shows an informational route, and stores no result in browser storage. |
| Consequence boundary | Applicant source fields, canonical clinical lists, patient and insurance rows, clinician tasks, portal, completed intake, consent, acceptance, financial records, request, queue, appointment, encounter, care, prescribing, claim, integration, and external-call consequences remain unchanged or false. |
| Runtime and governance | The inventory requires 46 telehealth tables. Decision 0032, the [Sprint 29 plan](sprint-29-synthetic-clinical-information-inventory.md), safeguard TH-SG-034, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, CI invocation, and planning validation are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused clinical-information-inventory policy tests | 15 passed, covering exact category normalization, deterministic route priority, all mandatory acknowledgments, snapshot behavior, invalid inputs, and every hard-false reconciliation/review/intake/care consequence |
| Full backend tests | 366 passed, 0 failed, 0 skipped |
| Full frontend tests | 53 files and 278 tests passed |
| Frontend lint, TypeScript, production build, and bundle budget | Passed; 137 JavaScript chunks checked and the initial bundle was 246,399 of 256,000 bytes |
| Cross-browser telehealth accessibility/recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the promoted-applicant journey covers three separate categories, provisional wording, absence of free text, all acknowledgments, server-route display, explicit false consequences, exact ambiguous retry, storage minimization, focus, reflow, and automated WCAG checks |
| Full migration and recovery rehearsal | 262 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of V0306 checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 86 checks passed; V0282–V0306, all 46 telehealth tables, all 39 append-only triggers, policy/expiry/no-consequence constraints, full provenance guards, and append-only behavior passed |
| Telehealth authorization proof | 74 checks passed, including absent applicant key and portal-session substitution for both inventory operations, patient/staff/physician separation, purpose of use, and practice/facility isolation |
| Telehealth OpenAPI proof | 40 checks passed, including applicant-only GET/POST security, private reads, required idempotency, exact controlled input, server-derived route, explicit false reconciliation/review/care output, and clinical-detail minimization |
| Telehealth runtime-safety proof | 30 top-level checks passed; 46-table synthetic readiness was healthy and no clinical reconciliation, review task, intake/eligibility, patient mutation, request, queue, care, prescribing, integration, or outbound path was introduced |
| Live synthetic clinical-information-inventory proof | 12 checks passed for Georgia, California, and Florida: minimized read; ownership/provenance/stale/fingerprint/drift rejection; exact replay and changed/second-command rejection; eight-way contention; route priority and three-state parity; append-only evidence; and zero patient/canonical-clinical/downstream delta |
| Shared queue/lifecycle stress regression | 134 checks passed, including one winner among 20 concurrent reserve-next and consultation-start callers, authorization/video/consultation/documentation/pharmacy/prescription/disposition boundaries, audit evidence, and restoration of mutable fixtures |
| Planning/backlog validator | Validator v2.6 passed 74 checks with Decision 0032 and all 34 safeguards; 117 Markdown files and 380 relative links were clean, and all three controlled negative mutations were rejected |
| Generated bootstrap | Deterministic regeneration and verification passed with unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2`; the empty-database bootstrap plus V0001–V0306 produced the complete current schema |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact validation passed. Review-delta identified `Program.cs` and `App.tsx` as the relevant shared hubs. The committed code-only index excludes the still-uncommitted telehealth feature tree, so its reported test gaps are an indexing limitation addressed directly by the 366 backend tests, 278 frontend tests, live PostgreSQL proof, and 56 browser journeys above. |

Machine-readable live results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-clinical-information-inventory.json`. The result contains deterministic synthetic IDs and the three coarse category states only; it contains no canonical patient ID, medication or substance, dose, reaction, diagnosis, symptom, procedure, narrative, date, attachment, clinical identifier, protected insurance value, FHIR resource, or external-provider response.

The authoritative final live run used a newly created disposable `avenchart_test_telehealth_s29` database and an exact rebuilt API container. Readiness independently reported 262 applied migrations, V0306 as the latest packaged migration, and 46 of 46 telehealth tables before the application proof. The final database inspection found three inventory receipts, zero receipts with any consequential flag true, and the append-only trigger enabled. The normal database remained unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`. The exact disposable API container and database were then removed, and both exact-name absence checks passed.

## 3. Proven boundaries

- only a current `SyntheticDevicePreparationRecorded` applicant with exact promotion, patient, registration, insurance-handoff, communication/access, passing device-preparation, safety-location, callback, practice, and facility provenance can read or record the inventory;
- only `PatientReportsNone`, `ItemsToReview`, or `Unsure` for each of three distinct categories, three mandatory acknowledgments, and explicit false consequence facts cross the route;
- `PatientReportsNone` remains a provisional report and is never represented as a reconciled “no known” medication, allergy, or history assertion;
- one successful command, including concurrent first writers, produces one receipt, one event, and one monotonic state transition; and
- applicant source, canonical medication/allergy/problem/history, patient, insurance, clinician task, portal, intake, eligibility, consent, acceptance, financial, request, queue, appointment, encounter, care, prescribing, claim, integration, and external-action records remain unchanged.

## 4. Evidence-gate observations

The first live minimization proof reported one failed resume/reload assertion because its test regex matched prohibited clinical words appearing in the permitted explanatory limitations. The proof was corrected to compare exact JSON property names; the unchanged API then passed all 12 checks. This was a test-precision correction, not an API-contract expansion.

The first broad browser invocation lacked the isolated API URL and used the suite's default portal fixture, so authenticated established-patient cases correctly failed to reach the intended runtime. The exact CI-scoped 56-test command was rerun against `http://127.0.0.1:5002` with the seeded `MOD-PAT-0012` synthetic portal fixture and passed serially across all four browser projects. Environment-configuration attempts are not counted as final evidence.

## 5. Rollback and stop evidence

- base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- missing, stale, expired, cross-applicant, portal-enabled, merged, patient-drifted, canonical-insurance, or provenance-incomplete state cannot record an inventory;
- immutable inventory evidence is not destructively rolled back; correction requires a separately reviewed forward migration/workflow;
- disabling/removing the routes and patient panel leaves the synthetic patient shell portal-disabled and without a clinician task, intake completion, eligibility, request, queue, care, or prescribing capability; and
- stop conditions include any detailed clinical fact or free text crossing the route, `PatientReportsNone` becoming a verified “no known” assertion, informational routing creating authority, canonical clinical or patient mutation, provenance divergence, history overwrite, or any earlier safeguard regression.

## 6. Open review gates

Independent medication-safety, allergy-safety, patient-registration/HIM, privacy/security, accessibility, data, legal/regulatory, licensed clinical/medical-director, interoperability, operations/support, and program-owner packet reviews remain open. Detailed clinical-history collection, medication/allergy/problem reconciliation, clinician review, remaining intake, clinician disclosure/consent, practice acceptance, request/queue entry, appointment, care, prescribing, billing/claim, pharmacy transmission, and every production action require later separately bounded decisions.
