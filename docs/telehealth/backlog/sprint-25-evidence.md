# Sprint 25 synthetic minimum registration-details confirmation evidence

Status: Bounded automated evidence passing; independent identity, patient-registration, privacy/security, accessibility, data, legal, clinical, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0028](../decisions/0028-approved-sprint-25-minimum-registration-details-confirmation.md)  
Scope: Disabled, synthetic-only, applicant-owned review and immutable no-edit confirmation of legal name, date of birth, masked verified contacts, residence state, and postal code copied into a promoted portal-disabled patient shell after state-notice acknowledgment; no correction, identity assurance, complete intake, legal consent, practice acceptance, insurance confirmation, request, queue, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Server-owned snapshot | `TelehealthApplicantRegistrationDetailsPolicy.cs` derives an exact allowlisted snapshot from applicant-held values, masks email/phone, and hashes the unmasked server snapshot into a deterministic SHA-256 fingerprint. The browser cannot author replacement registration values. |
| Provenance and drift rejection | Every read/write rebinds the access-key owner, branded practice, facility, unexpired notice-acknowledged aggregate, successful promotion, exact notice receipt, and active portal-disabled unmerged patient shell. Legal name, birth date, email, phone, state, and postal code must exactly match applicant source values or the operation fails closed. |
| Explicit no-edit confirmation | The command echoes only applicant version, snapshot fingerprint, and five true affirmations covering name/birth date, verified contact channels, residence region, no correction needed, and synthetic-only use. Partial confirmation is rejected. |
| Correction honesty | The patient screen says not to confirm incorrect details and directs the applicant to restart or contact the practice through a separately approved channel. This slice has no edit, patch, overwrite, merge, or correction-completion path. |
| Atomic append-only receipt | V0302 constrains one confirmation per applicant, notice receipt, promotion, and patient shell. A provenance trigger independently checks exact prior version/state and current copied fields. Receipt, aggregate transition, and event commit in one transaction and reject update/delete. |
| Replay and contention | Exact persisted retry returns the first immutable receipt. Changed-key reuse, stale version/fingerprint, expired applicant, missing or altered provenance, portal enablement, patient drift, a second semantic command, and concurrent first writers fail closed with at most one receipt/event. |
| HTTP and privacy | Applicant-key protected GET/POST routes are private/no-store. Output contains only the bounded display fields and false consequence flags; it excludes canonical/legacy patient IDs, street address, raw contacts, portal identity, member values, proofing evidence, staff rationale, and command provenance. |
| Patient UX | The post-notice screen labels every displayed source field, keeps contacts masked, separates five confirmations, disables submission until complete, preserves the exact command after an ambiguous failure, focuses failure/result status, supports reflow, and stores no registration details in browser persistence. |
| Consequence boundary | Persisted and returned facts keep identity assurance, patient mutation, correction completion, completed intake, legal consent, practice acceptance, insurance confirmation, coverage, request, queue, appointment, encounter, care, prescribing, claim, communication, integration, and external-call consequences false. |
| Runtime and governance | Readiness requires 42 tables. Decision 0028, the [Sprint 25 plan](sprint-25-minimum-registration-details-confirmation.md), safeguard TH-SG-030, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, CI invocation, and planning validation are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused minimum-details backend tests | 13 passed, covering normalization, all five affirmations, snapshot masking/fingerprinting, invalid version/fingerprint, and no-edit contract behavior |
| Full backend tests | 311 passed, 0 failed, 0 skipped |
| ASP.NET Core formatting/build | Passed with no formatting drift; the complete backend test build emitted no warnings or errors |
| Focused frontend API/component tests | 2 files and 40 tests passed, including applicant-only transport, exact seven-key no-edit command body, bounded field display, masked contacts, five confirmations, correction direction, retry-stable idempotency, no browser persistence, focus, and false consequences |
| Full frontend tests | 53 files and 273 tests passed |
| Frontend lint, TypeScript, production build, and bundle budget | Passed; 137 JavaScript chunks checked and the initial bundle was 246,395 of 256,000 bytes |
| Cross-browser telehealth accessibility/recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the promoted-applicant journey now covers both state-notice acknowledgment and minimum-details confirmation, including masking, disabled submit, all five affirmations, ambiguous retry, correction language, focus, reflow, and automated WCAG checks |
| Full migration and recovery rehearsal | 258 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of V0302 checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 77 checks passed; V0282–V0302, all 42 telehealth tables, all 35 append-only triggers, snapshot/policy/expiry/no-consequence constraints, notice/promotion/patient provenance guard, and append-only behavior passed |
| Telehealth authorization proof | 66 checks passed, including absent applicant key, portal-session substitution, patient/staff/physician separation, purpose of use, and practice/facility isolation |
| Telehealth OpenAPI proof | 36 checks passed, including applicant-only GET/POST security, private reads, required idempotency, exact no-edit typed input, bounded failures, masked bounded output, and identifier exclusion |
| Telehealth runtime-safety proof | 26 top-level checks passed; 42-table synthetic readiness was healthy and no patient mutation, portal, care, downstream, integration, or outbound path was introduced |
| Live prerequisite promotion proof | 13 checks passed through the fully API-created, administrator-authorized, duplicate-rechecked synthetic patient-shell promotion chain |
| Live prerequisite state-notice proof | 13 checks passed for all three state notices, explicit non-consent, replay/contention, append-only provenance, minimization, and zero downstream delta |
| Live minimum registration-details proof | 12 checks passed: masked bounded read; ownership/partial/stale/fingerprint/portal/drift rejection; Georgia exact replay and changed/second-command rejection; eight-way California contention; Florida no-consequence behavior; minimized resume; three-state database provenance; append-only evidence; sensitive-column exclusion; and zero patient/downstream delta |
| Generated empty bootstrap verification | Verified against generated output; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 70 checks passed with Decision 0028 and all 30 safeguards; 105 Markdown files and 332 relative links were clean |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed. The committed-code-only delta view did not surface the still-uncommitted telehealth files, so direct unit, API, live database, and browser evidence remains the authoritative change coverage for this workspace. |

Machine-readable live results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-registration-details-confirmation.json`. The result contains deterministic synthetic IDs and normalized statuses only; it contains no canonical patient ID, raw contact value, street address, member value, proofing evidence, payer response, or external-provider response.

The authoritative live run used the repository's deterministic gold fixture in a disposable `avenchart_test_sprint25` database and the exact rebuilt API/migrator image. After evidence capture, the isolated Sprint 25 API container and database were removed. The normal PostgreSQL service was left healthy on its existing volume and verified unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Proven boundaries

- only a current `SyntheticTelehealthNoticeAcknowledged` applicant with exact successful-promotion, notice, patient, practice, and facility provenance can read or confirm the snapshot;
- the server exposes legal name and date of birth plus masked contacts and residence region, never the patient ID, raw contacts, or street address;
- the browser submits no patient values and cannot edit or correct applicant/patient data;
- every affirmation is explicit and independently enforced, and exact retry cannot overwrite the first immutable provenance;
- one successful command, including concurrent first writers, produces one receipt, one event, and one monotonic `SyntheticMinimumRegistrationDetailsConfirmed` transition; and
- patient rows and all portal, intake, consent, practice-acceptance, insurance/coverage, financial, request, queue, appointment, encounter, care, prescribing, claim, communication, integration, and external-action tables remain unchanged.

## 4. Evidence-gate observations

The first isolated seed used the prior packaged migrator image and truthfully reported 257 migrations. Rebuilding the API/migrator image and re-running migration applied V0302 as migration 258; readiness then independently reported 258 expected/applied migrations and 42 of 42 telehealth tables. This demonstrates that the packaged migration ledger, rather than source-file presence alone, governs runtime readiness.

The first cross-browser run started the UI without pointing authenticated flows to the isolated API, so all public mocked journeys passed while authenticated journeys remained at login. The final run explicitly bound the development UI to `http://127.0.0.1:5015`; all 56 cases then passed across four browser/device projects. No failed preliminary run is counted as final evidence.

The live proof also deliberately toggled the isolated Florida patient shell to portal-enabled and then introduced a last-name mismatch. Both reads failed closed; the isolated fixture was restored before the successful confirmation, and final before/after row hashes proved the confirmation itself changed no patient record.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- missing, stale, expired, portal-enabled, merged, drifted, or provenance-incomplete patient shells cannot retrieve or confirm details;
- immutable confirmation evidence is not destructively rolled back; correction requires a separately reviewed forward migration/workflow;
- disabling/removing the routes and patient panel leaves the minimal synthetic patient shell portal-disabled and without care capability; and
- stop conditions include client-authored patient values, unmasked contact or identifier disclosure, confirmation despite data drift, correction-completed language, patient/downstream mutation, provenance divergence, history overwrite, or any earlier safeguard regression.

## 6. Open review gates

Independent identity and patient-registration owners must approve the eventual full demographic/correction workflow and patient-authentication boundary. Privacy/security, accessibility, data, legal/regulatory, licensed clinical/medical-director, interoperability, payer/network, operations/support, and program-owner packet reviews also remain open. The [patient onboarding specification](../04-patient-onboarding-and-identity.md) and [UX/accessibility specification](../17-ux-content-and-accessibility.md) remain controlling for later work. Real identity assurance, portal enrollment/authentication, edits/corrections, remaining demographics/address/history, completed intake, clinician disclosure/consent, practice acceptance, rendering-clinician participation, canonical insurance/coverage, estimate/payment, request/queue entry, appointment, encounter, communication/video, care, prescribing, billing/claim, pharmacy transmission, and every production action require later separately bounded decisions.
