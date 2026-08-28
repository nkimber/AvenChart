# Sprint 26 synthetic insurance handoff confirmation evidence

Status: Bounded automated evidence passing; independent patient-registration, payer/network, privacy/security, accessibility, data, legal, clinical, interoperability, operational, and program-owner reviews pending  
Decision: [TH-DEC-0029](../decisions/0029-approved-sprint-26-synthetic-insurance-handoff-confirmation.md)  
Scope: Disabled, synthetic-only, applicant-owned review and immutable no-edit confirmation of masked payer/member inputs and recorded eligibility/practice-network fixture limitations after minimum registration-details confirmation; no insurer confirmation, canonical coverage, rendering-physician conclusion, patient mutation, financial action, complete intake, consent, acceptance, request, queue, care, downstream action, external integration, or production use

## 1. Implementation trace

| Boundary | Evidence |
|---|---|
| Server-owned snapshot | `TelehealthApplicantInsuranceHandoffPolicy.cs` creates one deterministic SHA-256 snapshot from the stored payer/product, last-four masks, subscriber relationship/priority, normalized eligibility and practice-network outcomes, evidence timestamps, and rendering-physician false. The browser cannot author replacement insurance values. |
| Full provenance and drift rejection | Every read/write rebinds the applicant-key owner, practice/facility, successful promotion, current registration receipt, portal-disabled unmerged patient shell, protected member-details receipt, positive eligibility result, and positive practice-network result. Missing, stale, expired, cross-applicant, patient-drifted, portal-enabled, merged, or canonical-coverage state fails closed. |
| Masked bounded projection | The response exposes payer/product, member/group masks, relationship/priority, normalized outcomes, timestamps/freshness, and explicit limitation flags. It omits raw member/group values, subscriber identity, patient IDs, protected payloads, trace tokens, proofing evidence, duplicate candidates, and staff rationale. |
| Explicit no-edit confirmation | The command contains only aggregate version, snapshot fingerprint, and five true affirmations covering payer/product, masked member details, subscriber relationship/priority, evidence limitations, and synthetic-only use. Partial confirmation and browser-authored replacements are impossible through the contract. |
| Evidence honesty | Initial confirmation requires current positive eligibility and practice-level network fixtures. The interface states that neither fixture is an insurer response, coverage/payment/benefit guarantee, exact network result, or rendering-physician participation check. A recorded receipt may later display expired evidence without making it current. |
| Atomic append-only receipt | V0303 constrains one receipt per applicant, practice-scoped idempotency, masks, source evidence, affirmations, policy, expiry, and every no-consequence flag. A provenance trigger independently verifies the prior aggregate, patient shell, registration, member, eligibility, and network chain. Receipt, state transition, and event commit in one transaction and reject update/delete. |
| Replay and contention | Exact persisted retry converges on the first immutable result. Changed-key reuse, stale version/fingerprint, a second semantic command, and concurrent first writers fail closed with at most one receipt/event. Eight simultaneous California requests produced one receipt and one event. |
| Patient UX | The post-registration screen labels masked fields and sources, reports evidence freshness, keeps coverage and rendering-physician limitations prominent, provides correction stop direction, disables submit until all five confirmations are complete, preserves the exact command after an ambiguous failure, focuses status, supports reflow, and persists no insurance detail in browser storage. |
| Consequence boundary | Persisted and returned facts keep coverage verification, exact network, rendering-physician check, canonical coverage, patient mutation, portal, completed intake, legal consent, practice acceptance, financial records, request, queue, appointment, encounter, care, prescribing, claim, communication, integration, and external-call consequences false. |
| Runtime and governance | Readiness requires 43 tables. Decision 0029, the [Sprint 26 plan](sprint-26-synthetic-insurance-handoff-confirmation.md), safeguard TH-SG-031, migration/OpenAPI/authorization/runtime/live proofs, backlog authorization, CI invocation, and planning validation are synchronized. |

## 2. Automated results

| Evidence | Result |
|---|---|
| Focused insurance-handoff backend tests | 13 passed, covering exact input normalization, all five affirmations, deterministic masking/fingerprinting, invalid version/fingerprint, and bounded no-consequence output |
| Full backend tests | 324 passed, 0 failed, 0 skipped |
| ASP.NET Core formatting/build | Passed with no formatting drift; the complete backend test build emitted no warnings or errors |
| Focused frontend API/component tests | 2 files and 42 tests passed, including applicant-only transport, exact seven-key command input, masks and evidence labels, five confirmations, coverage/rendering limitations, retry-stable idempotency, focus, false consequences, and no browser persistence |
| Full frontend tests | 53 files and 275 tests passed |
| Frontend lint, TypeScript, production build, and bundle budget | Passed; 137 JavaScript chunks checked and the initial bundle was 246,395 of 256,000 bytes |
| Cross-browser telehealth accessibility/recovery | 56 passed across desktop Chromium, mobile Chromium, Firefox, and WebKit; the promoted-applicant journey now covers state notice, copied registration details, and masked insurance handoff, including minimization, disabled submit, all affirmations, exact ambiguous retry, focus, reflow, and automated WCAG checks |
| Full migration and recovery rehearsal | 259 migrations and 29 empty/populated/interruption/recovery/drift/runtime scenarios passed, including recovery after checkpoints 1, 64, and 127 and rejection of V0303 checksum drift and an unexpected ledger entry |
| Telehealth migration/schema regression | 80 checks passed; V0282–V0303, all 43 telehealth tables, all 36 append-only triggers, mask/evidence/policy/expiry/no-consequence constraints, full provenance guard, and append-only behavior passed |
| Telehealth authorization proof | 68 checks passed, including absent applicant key, portal-session substitution, patient/staff/physician separation, purpose of use, and practice/facility isolation |
| Telehealth OpenAPI proof | 37 checks passed, including applicant-only GET/POST security, private reads, required idempotency, exact snapshot-only typed input, bounded failures, masked evidence output, explicit false consequences, and hidden-identifier exclusion |
| Telehealth runtime-safety proof | 27 top-level checks passed; 43-table synthetic readiness was healthy and no canonical coverage, patient mutation, portal, care, downstream, integration, or outbound path was introduced |
| Live prerequisite promotion proof | 13 checks passed through the fully API-created, administrator-authorized, duplicate-rechecked synthetic patient-shell promotion chain |
| Live prerequisite state-notice proof | 13 checks passed for all three state notices, explicit non-consent, replay/contention, append-only provenance, minimization, and zero downstream delta |
| Live prerequisite registration-details proof | 12 checks passed for masking, no-edit confirmation, replay/contention, promotion/notice/patient provenance, minimization, append-only evidence, and zero patient/downstream delta |
| Live synthetic insurance-handoff proof | 12 checks passed: masked bounded read; ownership/partial/stale/fingerprint/canonical-coverage/portal rejection; Georgia exact replay and changed/second-command rejection; eight-way California contention; Florida policy parity; minimized confirmed resume; three-state database provenance; append-only evidence; sensitive-column exclusion; and zero patient/insurance/downstream delta |
| Shared queue/lifecycle stress regression | 134 checks passed, including one winner among 20 concurrent reserve-next callers, authorization/video/consultation/documentation/pharmacy/prescription/disposition boundaries, audit evidence, and restoration of all mutable fixtures |
| Generated empty bootstrap verification | Verified against generated output; packaged bootstrap SHA-256 `6a1a6ca3de61608654921edb843d48a4b07dcc8899d3e6ca4056cf8b838745a2` |
| Planning/backlog validator | 71 checks passed with Decision 0029 and all 31 safeguards; 108 Markdown files and 345 relative links were clean, and all three controlled negative mutations were rejected |
| Graphify code index | Refreshed to 6,742 nodes, 15,988 edges, and 348 communities; portable-artifact check passed. The committed-code-only delta view reported zero nodes for the still-uncommitted telehealth paths, so direct source, unit, API, live database, and browser evidence remains the authoritative coverage for this workspace. |

Machine-readable live results are under `avenchart/artifacts/telehealth/`, including `latest-telehealth-applicant-insurance-handoff-confirmation.json`. The result contains deterministic synthetic IDs, masks, normalized outcomes, and false consequence facts only; it contains no canonical patient ID, raw member/group value, subscriber identity, protected payload, payer response, or external-provider response.

The authoritative live run began from the repository's deterministic gold fixture in a newly recreated disposable `avenchart_test_sprint26` database and the exact rebuilt API/migrator image. Readiness independently reported 259 expected/applied migrations, V0303 as the latest packaged migration, and 43 of 43 telehealth tables before the application proofs ran. After evidence capture, the isolated Sprint 26 API container and database were removed. The normal PostgreSQL service was left healthy on its existing volume and verified unchanged at 237 migrations, 1,000 synthetic patients, and latest applied migration `V0281__index_flow_board_appointments_by_date`.

## 3. Proven boundaries

- only a current `SyntheticMinimumRegistrationDetailsConfirmed` applicant with exact promotion, registration, patient, member, eligibility, network, practice, and facility provenance can read or confirm the handoff;
- only masks and normalized evidence are projected; raw member/group values, subscriber identity, patient identity, and protected source payloads never cross the route;
- the browser submits no replacement insurance values, and every affirmation is explicit and independently enforced;
- eligibility and practice-level network fixtures remain labeled synthetic, time-bounded, non-guaranteeing, and not specific to a rendering physician;
- one successful command, including concurrent first writers, produces one receipt, one event, and one monotonic `SyntheticInsuranceDetailsConfirmed` transition; and
- patient and insurance rows plus all portal, intake, consent, practice-acceptance, financial, request, queue, appointment, encounter, care, prescribing, claim, communication, integration, and external-action tables remain unchanged.

## 4. Evidence-gate observations

The first draft of the live proof referenced a historical `patients.pid` column instead of the current `patients.legacy_pid` column. That evidence-harness query was corrected before the pristine evidence run; no application or database behavior was changed to make the proof pass.

The first shared queue-stress invocation targeted the intentionally untouched default database because the older harness hard-coded `avenchart`. It failed immediately before making any telehealth mutation. The harness now accepts a validated `avenchart` or `avenchart_test_*` database name; rerunning it against the exact API-bound `avenchart_test_sprint26` database passed all 134 checks. No failed preliminary run is counted as final evidence.

The live proof deliberately introduced canonical coverage and portal enablement in isolated fixtures and verified that the handoff failed closed. It restored each fixture before success; final hashes and row counts proved that confirmation itself added only the three expected immutable state-specific receipts/events.

## 5. Rollback and stop evidence

- committed base and Development configuration remain disabled, and Production enablement remains startup-rejected;
- missing, stale, expired, cross-applicant, portal-enabled, merged, patient-drifted, canonical-coverage, non-positive, or provenance-incomplete state cannot retrieve or confirm the handoff;
- immutable confirmation evidence is not destructively rolled back; correction requires a separately reviewed forward migration/workflow;
- disabling/removing the routes and patient panel leaves the synthetic patient shell portal-disabled and without canonical insurance or care capability; and
- stop conditions include client-authored insurance values, raw identifier/subscriber/patient disclosure, stale/non-positive evidence represented as current, rendering-physician or coverage-guarantee language, patient/insurance/downstream mutation, provenance divergence, history overwrite, or any earlier safeguard regression.

## 6. Open review gates

Independent patient-registration, payer/network, privacy/security, accessibility, data, legal/regulatory, licensed clinical/medical-director, interoperability, operations/support, and program-owner packet reviews remain open. The [insurance, eligibility, network, and pricing specification](../08-insurance-eligibility-network-and-pricing.md) and [patient onboarding specification](../04-patient-onboarding-and-identity.md) remain controlling for later work. Real insurer eligibility/benefits, rendering-clinician participation, canonical coverage, corrections, identity assurance, portal enrollment/authentication, remaining demographics/history, completed intake, clinician disclosure/consent, practice acceptance, estimates/payment, request/queue entry, appointment, encounter, communication/video, care, prescribing, billing/claim, pharmacy transmission, and every production action require later separately bounded decisions.
