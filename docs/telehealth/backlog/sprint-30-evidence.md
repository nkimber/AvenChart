# Sprint 30 synthetic medication-information evidence

Status: Bounded automated evidence passing; independent medication-safety, pharmacy, patient-registration/HIM, privacy/security, accessibility, data, legal, clinical, terminology, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0033](../decisions/0033-approved-sprint-30-synthetic-medication-information.md)  
Scope: Disabled, synthetic-only, applicant-owned recording of bounded patient-reported ingredient selections and coarse use states after a clinical-information inventory; no dose, directions, canonical medication data, reconciliation, interaction check, clinician task, completed intake, eligibility, patient mutation, request, queue, care, prescribing, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Full server-side provenance | Every read and write rebinds the applicant-key owner, practice/facility, successful promotion, portal-disabled unmerged patient shell, registration, insurance handoff, communication/access, passing device preparation, clinical-information inventory, passing safety location, and verified callback source. Missing, stale, expired, cross-applicant, patient-drifted, portal-enabled, merged, canonical-insurance, canonical-medication, or receipt-drifted state fails closed. |
| Fixed minimized catalog | The server owns exactly six generic synthetic ingredient entries under `LOCAL_SYNTHETIC_ONLY`, version 1. Catalog completeness and RxNorm mapping are explicitly false. Client-supplied display, coding, strength, form, dose, directions, route, frequency, timing, indication, prescriber, pharmacy, date, note, attachment, identifier, and free text cannot cross the route. |
| Coarse patient report only | Each selected catalog entry accepts only `Taking`, `NotTaking`, or `Unsure`; selected keys are unique and server ordered. An additional-or-unlisted signal is separate. Prior `ItemsToReview` requires at least one selection or that signal; prior `PatientReportsNone` and `Unsure` accept no selection and remain provisional. |
| Informational routing only | The server derives `AdditionalMedicationCollectionRequired`, `ClinicianMedicationReviewRequired`, `AssistedMedicationReviewRequired`, or `PendingClinicianConfirmationOfNone`. No task, priority, recommendation, reconciliation, interaction check, clinical eligibility, or operational authority is created. |
| Atomic append-only evidence | V0307 constrains one parent receipt per applicant, bounded child items, exact source/catalog/policy facts, all acknowledgments, and every no-consequence flag. Deferred constraints require the parent item count and branch to match the final child set. Parent, children, `SyntheticClinicalInformationInventoryRecorded -> SyntheticMedicationInformationRecorded`, and event commit in one transaction and reject update/delete. |
| Replay and contention | Exact retry returns the first immutable result only after provenance is revalidated inside the transaction. Changed-key reuse, stale version/fingerprint, source or canonical-medication drift, invalid or duplicate selections, a second semantic command, and concurrent first writers fail closed with at most one parent, exact child set, and one event. |
| Patient UX | The promoted-applicant flow exposes only the fixed catalog, coarse use status, additional-item signal, and four required acknowledgments. It provides an explicit load retry, preserves the exact command and selections after an ambiguous write, displays all false consequences, and stores no medication result in browser storage. |
| Consequence boundary | `MedicationStatement`, `MedicationRequest`, reconciliation, interaction checking, clinician review, completed intake, eligibility, patient mutation, request, queue, care, and prescribing remain explicitly false. Canonical medication/prescription/clinical, patient, insurance, financial, operational, integration, and external-call records remain unchanged. |
| Runtime and governance | The stage requires 48 telehealth tables. Decision 0033, the [Sprint 30 plan](sprint-30-synthetic-medication-information.md), safeguard TH-SG-035, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, CI invocation, and planning validation are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused medication-information policy tests | 24 passed, covering the exact six-entry catalog, normalization and ordering, three coarse use states, every prior-inventory branch, route priority, all four acknowledgments, snapshot behavior, invalid/duplicate/over-limit inputs, and every hard-false canonical/reconciliation/interaction/review/intake/care consequence |
| Full backend tests | 390 passed, 0 failed, 0 skipped |
| Full frontend tests | 53 files and 279 tests passed |
| Frontend lint, TypeScript, production build, and bundle budget | Passed; 137 JavaScript chunks checked and the initial bundle was 246,399 of 256,000 bytes |
| Cross-browser telehealth accessibility/recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the promoted-applicant journey covers transient load recovery, fixed-catalog minimization, two coarse selections, an additional-item signal, four acknowledgments, exact ambiguous-submit retry, explicit false consequences, storage minimization, focus, reflow, and automated WCAG checks |
| Full migration and recovery rehearsal | 263 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of V0307 checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 88 checks passed; V0282–V0307, all 48 telehealth tables, all 41 append-only triggers, parent/child and no-consequence controls, prior database safeguards, and append-only behavior passed |
| Telehealth authorization proof | 76 checks passed, including absent applicant key and portal-session substitution for both medication-information operations, patient/staff/physician separation, purpose of use, and practice/facility isolation |
| Telehealth OpenAPI proof | 41 checks passed, including applicant-only GET/POST security, private reads, required idempotency, fixed catalog and coarse item input, explicit false canonical/reconciliation/interaction/review/care output, and exact prohibited-property minimization |
| Telehealth runtime-safety proof | 31 top-level checks passed; 48-table synthetic readiness was healthy, Production remains rejected, and no canonical medication, reconciliation, interaction, review task, intake/eligibility, request, queue, care, prescribing, integration, or outbound path was introduced |
| Live synthetic medication-information proof | 12 checks passed for Georgia, California, and Florida: fixed-catalog minimization; ownership/provenance/stale/fingerprint/canonical-medication drift rejection; exact replay and changed/second-command rejection; eight-way contention; item/additional/none route parity; deferred count and append-only guards; resume/storage privacy; and zero canonical/downstream delta |
| Shared queue/lifecycle stress regression | 134 checks passed, including one winner among 20 concurrent reserve-next and consultation-start callers, authorization/video/consultation/documentation/pharmacy/prescription/disposition boundaries, audit evidence, and restoration of mutable fixtures |
| Planning/backlog validator | Validator v2.7 passed 75 checks with Decision 0033 and all 35 safeguards; 120 Markdown files and 393 relative links were clean, and all three controlled negative mutations were rejected |
| Generated bootstrap | Deterministic regeneration and verification passed with unchanged SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2`; the empty-database bootstrap plus V0001–V0307 produced the complete current schema |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact validation passed. Review-delta inspected seven implementation files, surfaced `Program.cs` and `App.tsx` as shared hubs, and was reconciled with the direct 390 backend tests, 279 frontend tests, live PostgreSQL proof, and 56 browser journeys. |

Machine-readable live results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-medication-information.json`. The result contains deterministic synthetic applicant identifiers, local catalog keys/display names, coarse reported-use states, and the additional-item signal only. It contains no canonical patient ID, external terminology code, strength, form, dose, directions, route, frequency, timing, indication, prescriber, pharmacy, date, note, attachment, free text, protected insurance value, FHIR resource, or external-provider response.

The authoritative final live run used the disposable `avenchart_test_s30_schema` database and exact `avenchart-api-s30-live` container. Readiness independently reported 263 applied migrations, V0307 as the latest packaged migration, and 48 of 48 telehealth tables before the application proof. Final inspection found three medication-information receipts, two bounded child-item rows, zero receipts with any consequential flag true, and all 41 append-only triggers. The normal database remained unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`. The exact disposable API container and database were then removed, and both exact-name absence checks passed.

## 3. Proven boundaries

- only a current `SyntheticClinicalInformationInventoryRecorded` applicant with exact promotion, patient, registration, insurance, communication, device, inventory, safety-location, callback, practice, and facility provenance can read or record the receipt;
- only server-owned catalog keys, `Taking`/`NotTaking`/`Unsure`, the additional-or-unlisted signal, four mandatory acknowledgments, and explicit false consequence facts cross the route;
- the local catalog remains incomplete and unmapped, and no patient report is represented as canonical or reconciled medication evidence;
- one successful command, including concurrent first writers, produces one parent, the exact bounded child set, one event, and one monotonic state transition; and
- applicant source, canonical medication/prescription/clinical, patient, insurance, clinician task, portal, intake, eligibility, consent, acceptance, financial, request, queue, appointment, encounter, care, prescribing, claim, integration, and external-action records remain unchanged.

## 4. Evidence-gate observations

The initial OpenAPI assertion scanned forbidden words as substrings and therefore treated the required “no dose or directions captured” acknowledgment as if dose and directions properties were exposed. The gate now compares exact schema property names; the unchanged API then passed all 41 checks. This was a test-precision correction, not an API-contract expansion.

The first targeted browser run used a label locator that also matched the selected ingredient's status control. Tightening the assertion to the ingredient checkbox resolved the locator ambiguity; the unchanged interaction and data contract then passed.

The isolated live proof needed the established synthetic users and records from the normal database. Its disposable clone exposed 47 historical ledger checksums that differ from the currently packaged migration files. Only the disposable clone's ledger was normalized; the current idempotent V0200 SQL and V0282–V0307 were then applied there. The normal database and its ledger were not changed. The separate clean empty-database 263-migration rehearsal proves the packaged catalog independently; the checksum divergence remains a historical environment fact rather than being concealed by the live fixture setup.

## 5. Rollback and stop evidence

- base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- missing, stale, expired, cross-applicant, portal-enabled, merged, patient-drifted, canonical-insurance, canonical-medication, or provenance-incomplete state cannot record medication information;
- immutable parent/child evidence is not destructively rolled back; correction requires a separately reviewed forward migration and workflow;
- disabling or removing the routes and panel leaves the synthetic patient shell portal-disabled and without a canonical medication, clinician task, intake completion, eligibility, request, queue, care, or prescribing capability; and
- stop conditions include any arbitrary medication, external code, dose, directions, or free text crossing the route; the catalog being represented as complete or mapped; a patient report becoming reconciled; routing creating authority; canonical or downstream mutation; provenance divergence; history overwrite; or an earlier safeguard regression.

## 6. Open review gates

Independent medication-safety/pharmacy, patient-registration/HIM, privacy/security, accessibility, data, legal/regulatory, licensed clinical/medical-director, terminology, interoperability, operations/support, and program-owner packet reviews remain open. Real medication collection and history, terminology mapping, medication reconciliation, interaction/contraindication checking, clinician review, remaining intake, clinician disclosure/consent, practice acceptance, request/queue entry, appointment, care, prescribing, billing/claim, pharmacy transmission, and every production action require later separately bounded decisions.
