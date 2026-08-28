# Sprint 24 state-specific synthetic telehealth-notice acknowledgment evidence

Status: Bounded automated evidence passing; independent Georgia, California, and Florida legal/regulatory, licensed clinical, identity, patient-matching, security/privacy, accessibility, data, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0027](../decisions/0027-approved-sprint-24-state-specific-telehealth-notice-acknowledgment.md)  
Scope: Disabled, synthetic-only, applicant-owned retrieval and immutable acknowledgment of one server-selected Georgia, California, or Florida telehealth notice after successful synthetic patient-shell promotion; no final/legal clinician consent, portal, completed intake, practice acceptance, coverage, request, queue, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| State-specific policy | `TelehealthApplicantNoticePolicy.cs` owns the versioned three-state catalog, exact state-to-notice mapping, patient-readable disclosure/deferred-item lists, official-source metadata, source retrieval date, and explicit pending-independent-review/non-consent boundary. |
| Promotion and location provenance | Retrieval and acknowledgment require the access-key owner, branded practice, configured facility, unexpired `SyntheticPatientPromoted` aggregate, one successful promotion, its exact active portal-disabled patient shell, and the passing safety screen's current-location state. The client cannot choose a different jurisdiction. |
| Explicit comprehension | The command requires seven independent affirmative acknowledgments: current location; telehealth mode and limitations; privacy limitations; emergency instructions; availability of in-person care; later clinician reconfirmation; and synthetic-only use. |
| Atomic append-only receipt | V0301 adds a constrained acknowledgment table whose provenance trigger independently validates the successful promotion, patient shell, safety location, practice/facility, server notice, and prior aggregate version. The receipt, aggregate transition, and event commit in one transaction and cannot be changed or deleted. |
| Replay and contention | Exact persisted retry returns the immutable first receipt. Changed-key reuse, stale version, missing or blocked promotion, altered location or notice, expired applicant, portal-enabled or missing patient, a second semantic command, and concurrent first writers fail closed with at most one receipt and event. |
| HTTP and privacy | Applicant-key protected GET/POST routes are private/no-store and return notice content/source plus a coarse acknowledgment state without canonical or legacy patient identifiers, demographics, member values, proofing evidence, or staff rationale. |
| Patient UX | The post-promotion section shows the server-selected state notice, official source, non-final warning, disclosure and deferred-control lists, seven independently labeled checkboxes, disabled submission until complete, stable ambiguous retry, an acknowledged result, and accessible recovery/reflow behavior. |
| Consequence boundary | Every response and persisted result keeps `legalConsentEstablished=false` and `clinicianConsentDocumented=false`. The slice creates no portal/account/session, chart content, completed intake, practice acceptance, insurance/coverage, financial record, request, queue, appointment, encounter, care, prescribing, claim, communication, integration, or external call. |
| Runtime and governance | Readiness requires 41 tables. Decision 0027, the [Sprint 24 plan](sprint-24-state-specific-telehealth-notice-acknowledgment.md), safeguard TH-SG-029, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, CI invocation, and the planning validator are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused state-notice backend tests | 15 passed, covering all three state mappings, policy/source metadata, state/notice rebinding, seven explicit acknowledgments, bounded text, unsupported-state rejection, and fingerprints |
| Full backend tests | 298 passed, 0 failed, 0 skipped |
| ASP.NET Core Release build and formatting | Passed with 0 warnings, 0 errors, and no formatting drift |
| Focused frontend API/component tests | 2 files and 38 tests passed, including typed applicant-only transport, server notice/source rendering, seven independent acknowledgments, non-consent labeling, exact retry, no persistence, and recovery |
| Full frontend tests | 53 files and 271 tests passed with file parallelism disabled |
| Frontend lint, TypeScript, production build, and bundle budget | Passed; 137 JavaScript chunks checked and the initial bundle was 246,395 of 256,000 bytes |
| Cross-browser telehealth accessibility/recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the promoted-applicant notice journey covers source display, non-consent language, all seven acknowledgments, stable retry, focus, reflow, and WCAG checks |
| Full migration and recovery rehearsal | 257 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of V0301 checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 74 checks passed; V0282–V0301, all 41 telehealth tables, all 34 append-only triggers, constrained state/notice/promotion/patient provenance, seven affirmations, replay, no-consent/no-downstream facts, and append-only behavior passed |
| Telehealth authorization proof | 64 checks passed, including absent applicant key, portal-session substitution, patient/staff/physician separation, purpose of use, and practice/facility isolation |
| Telehealth OpenAPI proof | 35 checks passed, including applicant-only GET/POST security, private reads, required idempotency, typed state-bound input, bounded failures, and legally nonfinal minimized output |
| Telehealth runtime-safety proof | 25 top-level checks passed; 41-table synthetic readiness was healthy and no portal, care, downstream, integration, or outbound path was introduced |
| Live prerequisite promotion proof | 13 checks passed through the fully API-created, administrator-authorized, duplicate-rechecked synthetic patient-shell promotion chain |
| Live state-notice acknowledgment proof | 13 checks passed: all three state/source selections; non-consent and seven-affirmation enforcement; ownership/promotion/location/notice/patient rejection; exact replay; changed-key/second-command rejection; eight-way contention; Florida boundary wording; minimized resume and public schemas; append-only evidence; and zero downstream delta |
| Generated empty bootstrap verification | Verified against migration-derived output; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 69 checks passed with Decision 0027 and all 29 safeguards; 102 Markdown files and 319 relative links were clean, and all three controlled negative mutations returned nonzero |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed |

Machine-readable live results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-notice-acknowledgment.json`. The evidence uses deterministic synthetic identifiers and normalized statuses only; it contains no canonical patient identifier, raw demographics, member value, identity-proofing evidence, payer response, or external-provider response.

The authoritative live run used the repository's deterministic gold fixture in a disposable `avenchart_test_sprint24` database and the exact rebuilt API image. After evidence capture, the isolated Sprint 24 API container and database were removed. The normal PostgreSQL service was left healthy on its existing volume and verified unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Proven boundaries

- the server selects `GA_TELEHEALTH_NOTICE_V1`, `CA_TELEHEALTH_NOTICE_V1`, or `FL_TELEHEALTH_NOTICE_V1` solely from the immutable passing safety-screen location and rejects a client state mismatch;
- the Georgia and California fixtures identify later clinician/provider disclosures and consent duties, while the Florida fixture preserves applicable standard-of-care, records, confidentiality, provider-registration, and patient-location boundaries without inventing a separate statutory consent requirement;
- the pre-clinician acknowledgment never claims legal sufficiency, a provider-patient relationship, a clinician signature, or clinician-documented consent;
- all seven acknowledgments are explicit and independently constrained, and exact replay cannot overwrite their first immutable provenance;
- one successful acknowledgment, including concurrent first writers, produces one receipt, one event, and one monotonic applicant-state transition; and
- all portal, completed-intake, practice-acceptance, coverage, financial, request, queue, appointment, encounter, care, prescribing, claim, communication, integration, and external-action tables remain unchanged.

## 4. Evidence-gate observations

The fresh isolated-database rehearsal first exposed a test-environment assumption: a newly empty database had no configured synthetic facility. The evidence database was therefore loaded through the repository's deterministic gold-dataset seeder before all current migrations were applied. No production or user data was used.

The runtime checks also caught one brittle source assertion that searched for a literal resulting-status value even though the repository references the policy constant. The assertion was corrected to validate the authoritative policy boundary. A service response construction was then changed to named arguments so the state/source/non-consent mapping remains readable and resistant to positional-field drift.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- a missing, blocked, stale, expired, altered-location, or portal-enabled promotion cannot retrieve or acknowledge a notice;
- immutable acknowledgment evidence is not destructively rolled back; correction requires an independently reviewed forward migration;
- disabling/removing the routes and panel leaves the minimal synthetic patient shell portal-disabled and without care capability; and
- stop conditions include client-selected jurisdiction, final-consent labeling, provenance divergence, history overwrite, identifier disclosure, portal/downstream mutation, or any earlier safeguard regression.

## 6. Open review gates

Independent Georgia, California, and Florida legal/regulatory review must approve the exact patient wording and determine where and how legally effective consent is later obtained and documented. Licensed clinical/medical-director, identity/fraud, patient-matching, security/privacy, accessibility, data, interoperability, payer/network, operational, and program-owner packet reviews also remain open. Real identity assurance, portal enrollment/authentication, remaining demographics/history, completed intake, clinician disclosure and consent, practice acceptance, rendering-clinician participation, canonical insurance/coverage, estimate/payment, request/queue entry, appointment, encounter, communication/video, care, prescribing, billing/claim, pharmacy transmission, and every production action require later separately bounded decisions.

## References

- [California Business and Professions Code § 2290.5](https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=BPC&sectionNum=2290.5.)
- [Georgia Composite Medical Board Rule 360-3-.07](https://rules.sos.ga.gov/gac/360-3-.07)
- [Florida Statutes § 456.47](https://leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL=0400-0499/0456/Sections/0456.47.html)
