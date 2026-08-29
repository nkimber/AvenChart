# Sprint 1 synthetic telehealth runbook

Scope: local deterministic evidence only under Decisions 0003 and 0005–0054, most recently [Decision 0054](../decisions/0054-approved-sprint-51-applicant-request-operational-review-submission.md). Never use live data, credentials, destinations or a production-like host.

## Preconditions

- PostgreSQL contains the deterministic AvenChart gold dataset and migrations `V0282` through `V0326`.
- ASP.NET Core environment is `Development` or `Testing`.
- `Telehealth:Enabled` is false in committed base and Development settings.
- Only `127.0.0.1`, `localhost` and the configured `.example.test` branded host are used.

## Local activation

Start an isolated API process/container with only `Telehealth__Enabled=true`. Do not edit committed configuration to enable the feature. Readiness must report `details.telehealth.data.enabled=true`, `mode=Synthetic`, and 69 present tables before tests begin.

Run, in order:

```powershell
pwsh -NoProfile -File ./scripts/Test-TelehealthMigrationResilience.ps1 -SkipBaseRehearsal
pwsh -NoProfile -File ./scripts/Test-TelehealthRuntimeSafety.ps1 -ApiBaseUrl http://127.0.0.1:5001
pwsh -NoProfile -File ./scripts/Test-TelehealthOpenApiContract.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthAuthorization.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthProspectiveIdentity.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantIdentityReview.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthProspectiveSafetyTriage.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthProspectiveVisitPurpose.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthProspectivePracticeNetworkPrecheck.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthProspectiveMemberInsuranceDetails.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthProspectiveEligibility.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthProspectivePracticeNetworkDetermination.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthProspectiveIdentityProofing.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantPromotionAuthorization.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRegistrationDetailsConfirmation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantInsuranceHandoffConfirmation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantCommunicationAccessReadiness.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantDevicePreparation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantClinicalInformationInventory.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantMedicationInformation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantAllergyInformation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantHealthHistoryInformation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantClinicalInformationSummary.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantPreRequestReadiness.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantPracticeReviewSubmission.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantPracticeReviewInbox.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantPracticeReviewClaim.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantPracticeReviewPacket.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantPracticeReviewAuthorization.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestCreation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestLocation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestUniversalSafety.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestComplaintTriage.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestIntake.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestInsuranceSource.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestEligibility.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestPracticeNetwork.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestRenderingCandidate.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestParticipationContext.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestParticipationEvaluation.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthApplicantRequestOperationalReviewSubmission.ps1
pwsh -NoProfile -File ./scripts/Test-TelehealthQueueConcurrency.ps1 -CallerCount 20
```

Run the two telehealth Playwright specifications on desktop and mobile Chromium after the enabled synthetic API is ready.

## Expected operating signals

- `/health/ready` is healthy and includes only feature enabled/mode/schema-count data.
- Unknown branded hosts return 404.
- Unsafe/uncertain triage never reaches OperationalReview.
- Only an administrator can queue a clinically eligible request.
- Only an eligible physician with an active facility-scoped shift can reserve.
- Concurrent reserve-next calls result in one winner for one physician/request.
- Prospective applicants use hash-only access/challenge evidence, reveal only a coarse duplicate disposition, and produce zero patient/portal/coverage/request/queue deltas.
- Authorized administrative review advances only contact-verified applicants to a server-derived `IdentityReviewApproved` or `ManualReviewRequired` state, records one append-only decision and event, remains explicitly not identity proofing, and produces zero canonical patient/portal/intake/coverage/request/queue/clinical/financial/prescribing/integration deltas.
- Only the access-key owner of an unexpired, no-candidate `IdentityReviewApproved` applicant can submit the prospective safety screen. Every answer and the supported current location must be explicit; emergency-first priority is immutable; one append-only evaluation/event is recorded; pass remains prospective; and no clinical answer is stored in browser persistence or creates identity, patient, portal, complete-intake, coverage, request, queue, clinical, financial, prescribing, integration, or external action.
- Only the access-key owner of an unexpired `SafetyScreenPassed` applicant can record one controlled visit purpose. Exactly `migraine` or `sleep` maps to a server-owned navigation label; no free text or complaint-specific clinical evaluator runs; one append-only purpose/event is recorded; and protocol, eligibility, identity, patient, complete-intake, coverage, request, queue, care, financial, prescribing, integration, and external consequences stay false.
- Only the access-key owner of an unexpired `VisitPurposeRecorded` applicant can list the versioned catalog and record one practice-plan fixture. Exactly three server-owned NON_PRODUCTION choices distinguish practice-confirmed fixture, unknown, and practice-out-of-network fixture without collecting member or physician identifiers; one append-only precheck/event is recorded; and eligibility, benefits, exact network, coverage, identity, patient, portal, consent, financial, request, queue, appointment, encounter, care, prescribing, billing, claim, communication, integration, and external consequences stay false.
- Only the access-key owner of an unexpired `PracticeNetworkPrecheckRecorded` applicant can confirm one minimum synthetic member-detail set. Identifiers must use the `SYN-` prefix, self/non-self subscriber rules are explicit, the raw normalized payload is purpose-protected before persistence, responses are mask-only, one append-only receipt/event is recorded, and eligibility, benefits, exact network, canonical insurance/coverage, identity, patient, portal, consent, financial, request, queue, appointment, encounter, care, prescribing, billing, claim, communication, integration, and external consequences stay false.
- Only the access-key owner of an unexpired `MemberInsuranceDetailsRecorded` applicant can request one normalized synthetic eligibility result. The server rebinds and unprotects upstream evidence, fixes date/service facts, keeps transport/member-match/eligibility/benefit/business outcomes separate, records one immutable result/event, serializes no X12, performs no external call, and leaves exact-network, canonical-coverage, financial, identity/patient, consent, practice-acceptance, request/queue, care, prescribing, billing/claim, communication, integration, and external consequences false.
- Only the access-key owner of an unexpired `SyntheticEligibilityRecorded` applicant with a fresh result can request one server-bound synthetic practice-network determination. The inquiry contains only the configured practice/facility, selected plan, state, date, and professional telehealth service; three normalized fixture outcomes preserve eligibility separately; compatibility metadata targets HL7 FHIR R4 Da Vinci PDex Plan-Net 1.2.0 without creating FHIR resources or claiming conformance; rendering-physician participation remains unchecked; and exact-network, canonical-coverage, identity/patient, practice-acceptance, financial, request/queue, care, prescribing, billing/claim, communication, integration, and external consequences stay false.
- Only the access-key owner of an unexpired applicant with fresh active eligibility and an accepting practice-network result can run the opaque-reference-only synthetic proofing-process fixture. Assurance remains `None`, identity remains unproved, no raw evidence/identifier/biometric/source request exists, and no patient/account/portal/consent/request/queue/care/downstream consequence occurs.
- Only an active authorized administrator or bound front-desk staff member in the configured practice/facility can authorize or deny later synthetic promotion after reviewing the complete unexpired normalized chain. Both no-assurance and synthetic-data acknowledgments are mandatory; one immutable decision/event is appended; the applicant remains prospective; and every canonical patient, chart, account, portal, intake, consent, acceptance, coverage, request, queue, clinical, downstream, integration, and external consequence stays false.
- Only an active authorized administrator in the configured practice/facility can execute the separately authorized atomic synthetic promotion. The transaction takes the canonical registration lock, rechecks current facility-scoped duplicates, and either creates exactly one deterministic minimal portal-disabled patient shell or records a possible-match block without linking, identifying, or mutating any existing patient. Patient creation/promotion/applicant/event evidence is atomic and replay-safe; chart content, portal/account/external identity, intake completion, consent, acceptance, insurance/coverage/financial, request, queue, appointment, encounter, care, prescribing, claim, communication, integration, and external consequences stay absent.
- Only the access-key owner of an unexpired successfully promoted applicant can retrieve and acknowledge the server-selected notice for the passing safety screen's Georgia, California, or Florida location. Seven acknowledgments are explicit; one immutable receipt/event is bound to the promotion, portal-disabled patient shell, state, notice, practice, and facility; legal and clinician consent remain false; and no portal, completed intake, practice acceptance, coverage, request, queue, care, downstream, integration, or external consequence occurs.
- Only the access-key owner of an unexpired notice-acknowledged applicant can confirm the copied minimum registration display. Name, birth date, masked contacts, state, and postal code are server-bound and no-edit; a correction need stops the step; one immutable confirmation/event is recorded; identity assurance, patient mutation, portal, complete intake, consent, insurance, acceptance, request, queue, and care remain unavailable.
- Only the access-key owner of an unexpired minimum-details-confirmed applicant with fresh positive synthetic eligibility and practice-network evidence can confirm the masked insurance handoff. Raw member/subscriber values and canonical identifiers never return; rendering-physician participation and coverage/payment guarantees remain false; one immutable no-edit confirmation/event is recorded; no canonical coverage, patient mutation, portal, intake, consent, acceptance, request, queue, claim, or care capability is created.
- Only the access-key owner of an unexpired insurance-confirmed applicant can record one bounded communication/access-readiness receipt. Location and masked callback are rebound to server evidence; the spoken-language choice is limited to English or Spanish; all location, callback, safe/private communication, disconnection/emergency-plan, and synthetic acknowledgments are mandatory. Interpreter and accessibility selections remain preferences—not assignments, accommodations, support requests, or communications—and technology readiness, patient mutation, intake, consent, acceptance, request, queue, appointment, encounter, billing, claim, integration, external call, and care remain unavailable.
- Only the access-key owner of an unexpired communication-ready applicant can record one coarse, client-reported synthetic device-preparation receipt after a local browser check. Browser, camera, microphone, and speaker must all pass; the connection indication is only `Unknown` or `Good`; `Limited` and partial results stop the step; and client-reported, no-guarantee, and pre-consultation-recheck acknowledgments are mandatory. The browser stops all temporary media tracks immediately and sends no media, device identifiers, browser/IP details, WebRTC negotiation data, recording, transcript, or precise network measurement. Technology readiness, support, waiting room, media session, communication, patient mutation, intake, consent, acceptance, request, queue, appointment, encounter, billing, claim, integration, external call, and care remain unavailable.
- Only the access-key owner of an unexpired applicant whose coarse clinical inventory has been recorded can record one bounded synthetic medication-information receipt. The exact fixed local catalog is incomplete and unmapped; selected ingredients use only `Taking`, `NotTaking`, or `Unsure`, an additional-or-unlisted signal is separate, and four incompleteness/no-detail/reconciliation acknowledgments are mandatory. No dose, directions, route, frequency, indication, prescriber, pharmacy, date, note, attachment, free text, canonical medication resource, reconciliation, interaction check, clinician task, patient mutation, intake, eligibility, request, queue, prescribing, external call, or care capability is created.
- Only the administrator or bound front-desk staff member who owns the current unexpired short claim may record the positive-only practice-review authorization. The exact minimized packet provenance and packet policy version are revalidated under locks; all three limitation acknowledgments are mandatory; the applicant advances once to `SyntheticPracticeReviewAuthorized`; the submitted case and claim history remain unchanged; and only a separately gated future synthetic request-creation step is authorized. No acceptance, contact, clinical review, request, queue, appointment, encounter, consent, care, prescribing, financial, integration, external call, or production capability is created.
- Only the access-key owner of an unexpired `SyntheticRequestCreated` applicant whose request reached exact `Intake` version 4 through a passing unpublished synthetic complaint assessment may record the no-free-text request intake snapshot. One controlled duration and eight explicit current-source/limitations confirmations are required; the server derives the synthetic summary, appends one generic intake plus one protected receipt/event, and advances only the request to pending `Verification` version 5. The applicant and patient stay unchanged, and no consent, canonical coverage, current eligibility/network confirmation, operational review, acceptance, contact, doctor search, queue, appointment, encounter, care, prescription, financial, integration, external call, or production capability is created.
- Only the access-key owner of an unexpired `SyntheticRequestCreated` applicant whose request reached pending `Verification` version 5 through the exact Sprint 44 intake may confirm the masked primary insurance source. Seven source, future-verification, limitation, and synthetic acknowledgments are required; prior eligibility and practice-network evidence remains historical and non-reusable, the protected payload is referenced without copy or decryption, and only the request advances to pending `Verification` version 6. No current eligibility/network result, rendering-physician check, canonical coverage, financial or operational route, contact, doctor search, queue, appointment, encounter, consent, care, integration, external call, or production capability is created.
- Only the access-key owner of an unexpired `SyntheticRequestCreated` applicant whose request reached pending `Verification` version 6 through the exact Sprint 45 source confirmation may run one fresh request-time eligibility check. The protected synthetic payload is decrypted only in server memory, validated against the masked receipt, and never copied or returned; two acknowledgments are mandatory; the fixed in-process `NON_PRODUCTION` adapter returns separate transport, match, eligibility, benefit-information, and business outcomes; and only the request advances to pending `Verification` version 7. No X12 is serialized, no external destination is contacted, and exact network, canonical coverage/selection, financial or operational work, contact, doctor search, queue, appointment, encounter, consent, care, integration, or production capability is created.
- Only the access-key owner of that request with exact current positive eligibility may run one fresh practice/facility/service network check. Three acknowledgments are mandatory; no member or patient value enters the fixed in-process Plan-Net-shaped adapter; only the request advances to pending `Verification` version 8; and rendering-physician participation, exact network, canonical coverage/selection, financial, operational, queue, care, integration, external, and production consequences remain unavailable.
- Only the access-key owner of that request with exact current positive eligibility and practice-network results may bind the server-owned GA, CA, or FL synthetic rendering candidate for a future exact participation check. Four acknowledgments are mandatory; only a masked provider reference is returned; only the request advances to pending `Verification` version 9; and clinician assignment, availability, licensure, credentialing, rendering-network evaluation, exact network, canonical coverage/selection, financial, operational, queue, care, integration, external, and production consequences remain unavailable.
- Connection grants remain opaque, participant/session/role scoped, hash-only at rest, and replay-stable only inside the active simulator process; media capture remains absent.
- Operational authorization creates one scheduled unassigned appointment; reservation assigns the winning physician; patient waiting-room entry marks Arrived.
- Only the reservation-owning physician with current request/location/coverage/reservation/session/grants and every affirmative start check can create one appointment-linked encounter/context and enter `InConsultation`.
- The start projection exposes no sequential encounter key, and notes, signatures, prescriptions, billing, claims, chart access, completion, and real media remain unavailable.
- Only the owning physician can enter unfinished wrap-up. One transaction changes consultation/request/shift state, retains the open appointment/encounter and draft access, keeps the physician unavailable, and exposes an honest not-complete patient state.
- Only that owning physician can record the unsigned safety-disposition draft. The exact disposition vocabulary and conditional evaluation, location/callback, emergency, communication, and interrupted-contact facts are enforced; exact retries converge; prior versions remain immutable; and no signing, delivery, completion, downstream clinical/financial/communication/integration, or lifecycle action occurs.
- Only that owning physician can read the completion-prerequisites projection. It exposes SOAP presence without text or sufficiency claims, structured disposition state without authored text, optional nonblocking pharmacy state, stable product blockers, and no canonical identifiers; every signing, completion, delivery, and downstream capability remains false, and repeated reads produce no durable or lifecycle delta.

No patient identifier, complaint response, location, token, request ID or free-text intake is permitted in logs, health data or metric labels.

## Stop, rollback and recovery

Immediately stop the enabled synthetic process if a Decision 0003 stop condition occurs. Rollback is the feature flag: restart without `Telehealth__Enabled=true`; route registration disappears while database evidence remains. Never edit an applied migration or delete durable request/audit evidence as rollback. Apply a separately reviewed forward migration for schema correction.

For an expired reservation, the repository uses the database clock to mark the lease expired, returns the queue entry to Ready and appends a system event. For an unavailable database, readiness is unhealthy and no telehealth traffic may proceed. After recovery, rerun migration, runtime-safety, authorization and concurrency evidence before further work.

## Escalation

Record the failing command, UTC time, environment, decision ID and non-PHI error code. Do not attach database rows, headers, tokens, screenshots containing identifiers or request payloads. A clinical-safety, privacy, authorization, data-integrity or critical-accessibility failure blocks the slice until the appropriate independent owner reviews it.
