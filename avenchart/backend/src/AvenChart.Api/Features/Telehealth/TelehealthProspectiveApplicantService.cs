// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectiveApplicantService(
    TelehealthProspectiveApplicantRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProspectiveApplicantResponse> CreateAsync(
        HttpContext httpContext,
        CreateTelehealthProspectiveApplicantRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthProspectiveApplicantPolicy.Normalize(
            request,
            _options.SupportedStates,
            DateOnly.FromDateTime(DateTime.UtcNow));
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "create-prospective-applicant-v1",
            normalized.LegalFirstName,
            normalized.LegalLastName,
            normalized.DateOfBirth,
            normalized.Email,
            normalized.Phone,
            normalized.ResidenceStateCode,
            normalized.PostalCode,
            request.SyntheticDataConfirmed);
        var applicant = await repository.CreateAsync(
            _options.PracticeId,
            _options.FacilityId,
            normalized,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            semanticKey,
            fingerprint,
            cancellationToken);
        return ToResponse(applicant);
    }

    public async Task<TelehealthProspectiveApplicantResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var applicant = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        return ToResponse(applicant);
    }

    public async Task<TelehealthProspectiveApplicantResponse> VerifyContactAsync(
        HttpContext httpContext,
        Guid applicantId,
        VerifyTelehealthProspectiveApplicantContactRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "ExpectedVersion must be positive.");
        }
        var verificationCode = request.VerificationCode?.Trim() ?? string.Empty;
        if (verificationCode.Length != 6 || verificationCode.Any(character => !char.IsDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_verification_code_invalid",
                "Enter the six-digit synthetic verification code.");
        }
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "verify-prospective-contact-v1",
            applicantId,
            request.ExpectedVersion,
            TelehealthProspectiveApplicantPolicy.Hash(verificationCode));
        var applicant = await repository.VerifyContactAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            request.ExpectedVersion,
            verificationCode,
            semanticKey,
            fingerprint,
            cancellationToken);
        return ToResponse(applicant);
    }

    private TelehealthProspectiveApplicantResponse ToResponse(TelehealthProspectiveApplicantRecord applicant)
    {
        var pending = applicant.Status == "ContactVerificationPending";
        var verified = applicant.Status is "IdentityReviewPending" or "IdentityReviewApproved" or "ManualReviewRequired"
            or "SafetyScreenPassed" or "SafetyClinicalReviewRequired" or "SafetyInPersonRequired"
            or "SafetyEmergencyRedirect" or "VisitPurposeRecorded" or "PracticeNetworkPrecheckRecorded"
            or "MemberInsuranceDetailsRecorded" or "SyntheticEligibilityRecorded"
            or "SyntheticPracticeNetworkRecorded" or "SyntheticIdentityProofingRecorded"
            or "SyntheticPromotionAuthorized" or "SyntheticPromotionDenied"
            or "SyntheticPatientPromoted" or "SyntheticPromotionBlockedPossibleMatch"
            or "SyntheticTelehealthNoticeAcknowledged"
            or "SyntheticMinimumRegistrationDetailsConfirmed"
            or "SyntheticInsuranceDetailsConfirmed"
            or "SyntheticCommunicationAccessReadinessRecorded"
            or "SyntheticDevicePreparationRecorded"
            or "SyntheticClinicalInformationInventoryRecorded"
            or "SyntheticMedicationInformationRecorded"
            or "SyntheticAllergyInformationRecorded"
            or "SyntheticHealthHistoryInformationRecorded"
            or "SyntheticClinicalInformationSummaryConfirmed"
            or "SyntheticPreRequestReadinessAcknowledged"
            or "SyntheticPracticeReviewSubmitted"
            or "SyntheticPracticeReviewAuthorized"
            or "SyntheticRequestCreated";
        var canonicalPatientCreated = applicant.Status is "SyntheticPatientPromoted"
            or "SyntheticTelehealthNoticeAcknowledged"
            or "SyntheticMinimumRegistrationDetailsConfirmed"
            or "SyntheticInsuranceDetailsConfirmed"
            or "SyntheticCommunicationAccessReadinessRecorded"
            or "SyntheticDevicePreparationRecorded"
            or "SyntheticClinicalInformationInventoryRecorded"
            or "SyntheticMedicationInformationRecorded"
            or "SyntheticAllergyInformationRecorded"
            or "SyntheticHealthHistoryInformationRecorded"
            or "SyntheticClinicalInformationSummaryConfirmed"
            or "SyntheticPreRequestReadinessAcknowledged"
            or "SyntheticPracticeReviewSubmitted"
            or "SyntheticPracticeReviewAuthorized"
            or "SyntheticRequestCreated";
        var nextAction = applicant.Status switch
        {
            "ContactVerificationPending" => "Enter the demonstration code to verify control of the synthetic email contact.",
            "IdentityReviewPending" => applicant.DuplicateDisposition == "PossibleMatchManualReview"
                ? "A possible existing record requires authorized manual identity review. No patient record was created or linked."
                : "No exact candidate was found, but approved identity proofing and review are still required. No patient record was created.",
            "IdentityReviewApproved" => "Staff recorded the bounded synthetic review. Identity proofing and all later intake gates are still required; no patient record was created.",
            "ManualReviewRequired" => "The bounded review requires a separate authorized patient-matching workflow. No candidate is disclosed and no patient record was created or linked.",
            "SafetyScreenPassed" => "The universal synthetic safety screen found no stop condition. Complaint-specific triage, identity, consent, coverage, practice acceptance, and every care gate are still required and are not yet available.",
            "SafetyClinicalReviewRequired" => "An uncertain safety answer cannot pass automatically. A separately authorized clinical-review workflow would be required; no request was created.",
            "SafetyInPersonRequired" => "Arrange an in-person medical evaluation. If symptoms become an emergency, call 911. No clinician reviewed these synthetic answers.",
            "SafetyEmergencyRedirect" => "Call 911 now or go to the nearest emergency department. This form did not create a request or contact a clinician.",
            "VisitPurposeRecorded" => "The synthetic visit purpose was classified. Complaint-specific triage, identity, consent, coverage, practice acceptance, and every care gate are still required and are not yet available.",
            "PracticeNetworkPrecheckRecorded" => "The synthetic practice-level plan precheck was recorded. Member eligibility, exact practice-and-physician network confirmation, identity, consent, coverage, financial, request, queue, and care gates remain unavailable.",
            "MemberInsuranceDetailsRecorded" => "The protected synthetic member-details receipt was recorded. Member matching, eligibility, benefits, exact practice-and-physician network confirmation, identity, consent, canonical coverage, financial, request, queue, and care gates remain unavailable.",
            "SyntheticEligibilityRecorded" => "A normalized synthetic member-eligibility result was recorded. It is not a coverage guarantee or exact network confirmation; identity, consent, canonical coverage, financial, request, queue, and care gates remain unavailable.",
            "SyntheticPracticeNetworkRecorded" => "A normalized synthetic practice/facility/service network result was recorded. Rendering-physician participation, canonical coverage, financial, identity, consent, practice acceptance, request, queue, and care gates remain unavailable.",
            "SyntheticIdentityProofingRecorded" => "A normalized synthetic identity-proofing process fixture was recorded. No identity assurance level or real identity was established; patient promotion, consent, practice acceptance, request, queue, and care gates remain unavailable.",
            "SyntheticPromotionAuthorized" => "Authorized staff approved only a separately gated future synthetic promotion exercise. No real identity, patient, chart, portal, consent, practice acceptance, request, queue, or care capability exists; this demonstration stops here.",
            "SyntheticPromotionDenied" => "Authorized staff denied synthetic promotion. No patient, chart, portal, consent, practice acceptance, request, queue, or care capability was created; contact the practice or start a new synthetic applicant session if directed.",
            "SyntheticPatientPromoted" => "A minimal canonical synthetic patient shell was created after a current duplicate recheck. No portal, complete intake, consent, coverage, request, queue, or care capability exists; wait for separate practice instructions.",
            "SyntheticPromotionBlockedPossibleMatch" => "Synthetic promotion was safely blocked because a possible current patient match was detected. No patient was created or linked and no candidate details are available; contact the practice for a separately governed review.",
            "SyntheticTelehealthNoticeAcknowledged" => "The synthetic state-specific telehealth notice was acknowledged. A clinician must still provide required disclosures and obtain and document any legally effective consent before care; portal, complete intake, practice acceptance, request, queue, and care remain unavailable.",
            "SyntheticMinimumRegistrationDetailsConfirmed" => "The minimum copied registration details were confirmed without editing the patient shell. Identity assurance, complete demographics/history, legal consent, insurance confirmation, practice acceptance, request, queue, and care remain unavailable.",
            "SyntheticInsuranceDetailsConfirmed" => "The masked synthetic insurance handoff was confirmed without creating canonical coverage. Rendering-physician participation, coverage/payment guarantees, complete intake, legal consent, practice acceptance, request, queue, and care remain unavailable.",
            "SyntheticCommunicationAccessReadinessRecorded" => "The synthetic communication and access readiness receipt was recorded. No interpreter or accessibility service was arranged, and technical readiness, legal consent, practice acceptance, request, queue, and care remain unavailable.",
            "SyntheticDevicePreparationRecorded" => "The coarse client-reported synthetic device-preparation receipt was recorded. It is not technology readiness or a connection guarantee; media, intake, legal consent, practice acceptance, request, queue, and care remain unavailable.",
            "SyntheticClinicalInformationInventoryRecorded" => "The coarse patient-reported synthetic clinical-information inventory was recorded. No details were collected and no medication, allergy, or health-history list was reconciled; clinical intake, eligibility, review, request, queue, prescribing, and care remain unavailable.",
            "SyntheticMedicationInformationRecorded" => "The bounded patient-reported synthetic medication-information receipt was recorded from an incomplete local ingredient catalog. No dose or directions were collected and no medication list was reconciled; clinical intake, interaction checking, review, request, queue, prescribing, and care remain unavailable.",
            "SyntheticAllergyInformationRecorded" => "The bounded patient-reported synthetic allergy/intolerance-information receipt was recorded from an incomplete local substance catalog. No reaction, severity, criticality, confirmed negation, or canonical allergy record was created; clinical intake, safety checking, review, request, queue, prescribing, and care remain unavailable.",
            "SyntheticHealthHistoryInformationRecorded" => "The bounded patient-reported synthetic health-history-topic receipt was recorded from an incomplete local topic catalog. Topics are not diagnoses, findings, assessments, or canonical history; risk evaluation, triage change, clinical intake, review, request, queue, prescribing, and care remain unavailable.",
            "SyntheticClinicalInformationSummaryConfirmed" => "The no-edit synthetic clinical-information summary was confirmed. It remains patient reported and unreconciled; no clinical intake, eligibility, review task, request, queue, prescribing, or care capability was created.",
            "SyntheticPreRequestReadinessAcknowledged" => "The five-section synthetic pre-request readiness review was acknowledged. Outstanding steps remain; no practice acceptance, request, queue entry, appointment, encounter, prescribing, or care capability was created.",
            "SyntheticPracticeReviewSubmitted" => "The synthetic information was submitted for practice review. One staff review work item exists, but no practice decision, telehealth request, doctor search, patient or clinician queue entry, appointment, encounter, prescribing, or care capability was created.",
            "SyntheticPracticeReviewAuthorized" => "The practice authorized one separately confirmed synthetic request-creation step. No request, doctor search, queue, appointment, encounter, consent, prescribing, or care capability exists until you explicitly continue.",
            "SyntheticRequestCreated" => "One synthetic telehealth request exists. Complete only the separately available request-owned steps; no doctor search or patient or clinician care queue is active.",
            "VerificationLocked" => "The attempt limit was reached. Start a new synthetic applicant session.",
            _ => "This synthetic applicant session expired. Start again."
        };
        return new TelehealthProspectiveApplicantResponse(
            applicant.ApplicantId,
            applicant.Status,
            applicant.Version,
            _options.PracticeDisplayName,
            applicant.ResidenceStateCode,
            TelehealthProspectiveApplicantPolicy.MaskEmail(applicant.Email),
            TelehealthProspectiveApplicantPolicy.MaskPhone(applicant.Phone),
            verified,
            verified ? "ContactControlOnly" : "UnverifiedContact",
            applicant.DuplicateDisposition,
            canonicalPatientCreated,
            pending ? Math.Max(0, applicant.MaximumAttempts - applicant.AttemptCount) : 0,
            applicant.ExpiresAt,
            pending ? TelehealthProspectiveApplicantPolicy.DemonstrationVerificationCode : null,
            nextAction,
            ApplicantLimitations(applicant.Status));
    }

    private static string[] ApplicantLimitations(string status) => status switch
    {
        "SyntheticPatientPromoted" =>
        [
            "Synthetic demonstration only; no message was sent and no real identity was proved.",
            "A minimal synthetic patient shell exists, but no canonical identifier is disclosed here.",
            "No chart content, portal, completed intake, consent, coverage, request, queue, or care capability was created."
        ],
        "SyntheticTelehealthNoticeAcknowledged" =>
        [
            "Synthetic demonstration only; no real identity was proved and no final legal consent was established.",
            "A minimal synthetic patient shell exists, but no canonical identifier is disclosed here.",
            "No chart content, portal, completed intake, practice acceptance, coverage, request, queue, or care capability was created."
        ],
        "SyntheticMinimumRegistrationDetailsConfirmed" =>
        [
            "Synthetic demonstration only; no real identity assurance or patient authentication was established.",
            "Only the copied minimum name, birth date, masked contact, state, and postal details were confirmed; no patient field was edited.",
            "No complete demographics/history, legal consent, insurance confirmation, practice acceptance, request, queue, or care capability was created."
        ],
        "SyntheticInsuranceDetailsConfirmed" =>
        [
            "Synthetic demonstration only; no payer, clearinghouse, provider directory, insurer, or rendering physician was contacted.",
            "Only masked copied insurance details and fixture limitations were confirmed; no canonical coverage or patient field was created or changed.",
            "No portal, complete intake, legal consent, practice acceptance, request, queue, appointment, encounter, billing, claim, or care capability was created."
        ],
        "SyntheticCommunicationAccessReadinessRecorded" =>
        [
            "Synthetic demonstration only; no interpreter, accessibility service, clinician, practice staff member, or external service was contacted.",
            "Only the selected communication preferences and required readiness acknowledgments were recorded; no patient field was created or changed.",
            "No technical readiness, complete intake, legal consent, practice acceptance, request, queue, appointment, encounter, billing, claim, or care capability was created."
        ],
        "SyntheticDevicePreparationRecorded" =>
        [
            "Synthetic demonstration only; no media, device identifier, support service, clinician, practice staff member, or external service was contacted.",
            "Only a coarse client-reported device-preparation result was recorded; no technology-readiness or connection guarantee was established.",
            "No waiting room, media session, complete intake, legal consent, practice acceptance, request, queue, appointment, encounter, billing, claim, or care capability was created."
        ],
        "SyntheticClinicalInformationInventoryRecorded" =>
        [
            "Synthetic demonstration only; no medication, substance, reaction, dose, diagnosis, symptom, procedure, narrative, date, identifier, or free text was collected.",
            "PatientReportsNone is only an unverified patient report and never means a clinician-reconciled no-known finding.",
            "No clinician review, chart reconciliation, clinical intake, eligibility decision, request, queue entry, prescribing, or care capability was created."
        ],
        "SyntheticMedicationInformationRecorded" =>
        [
            "Synthetic demonstration only; the fixed local ingredient catalog is incomplete and has no RxNorm, NDC, or SNOMED CT mapping claim.",
            "Only patient-reported ingredient selections and Taking, NotTaking, or Unsure were recorded; no dose, directions, route, frequency, indication, prescriber, pharmacy, dates, notes, attachments, or free text was collected.",
            "No MedicationStatement, MedicationRequest, canonical reconciliation, interaction check, clinician task, intake, eligibility decision, request, queue entry, prescribing, or care capability was created."
        ],
        "SyntheticAllergyInformationRecorded" =>
        [
            "Synthetic demonstration only; the fixed local substance catalog is incomplete and has no SNOMED CT, RxNorm, NDC, UNII, or other external terminology mapping claim.",
            "Only patient-reported substance selections were recorded; no reaction, manifestation, type, clinical status, verification status, severity, criticality, onset, dates, notes, attachments, or free text was collected.",
            "No AllergyIntolerance resource, canonical allergy list, confirmed no-known-allergy assertion, reconciliation, contraindication check, alert, clinician task, intake, eligibility decision, request, queue entry, prescribing, or care capability was created."
        ],
        "SyntheticHealthHistoryInformationRecorded" =>
        [
            "Synthetic demonstration only; the fixed local topic catalog is incomplete and has no SNOMED CT, ICD-10-CM, LOINC, FHIR, US Core, or USCDI mapping claim.",
            "Only broad patient-selected review topics were recorded; no diagnosis, condition, procedure, observation, pregnancy status, assessment, family-history finding, status, timing, severity, dates, notes, attachments, or free text was collected.",
            "No canonical problem, Condition, Procedure, Observation, FamilyMemberHistory, QuestionnaireResponse, confirmed no-history assertion, reconciliation, risk evaluation, triage change, clinician task, intake, eligibility decision, request, queue entry, prescribing, or care capability was created."
        ],
        "SyntheticClinicalInformationSummaryConfirmed" =>
        [
            "Synthetic demonstration only; the summary contains only prior coarse patient-reported states, bounded counts, additional-item signals, and informational routes.",
            "Confirmation created no QuestionnaireResponse, canonical clinical record, confirmed negative, verification, reconciliation, assessment, or eligibility result.",
            "No clinical-review task, completed intake, practice acceptance, request, queue entry, prescribing, or care capability was created."
        ],
        "SyntheticPreRequestReadinessAcknowledged" =>
        [
            "Synthetic demonstration only; this checkpoint exposes only five coarse receipt states and unresolved route codes.",
            "Acknowledgment established no identity, coverage, rendering-clinician network status, fulfilled support, technology readiness, reconciliation, completed intake, eligibility, consent, or practice acceptance.",
            "No staff or clinician task, request, queue entry, appointment, encounter, care, prescribing, billing, claim, integration, or external action was created."
        ],
        "SyntheticPracticeReviewSubmitted" =>
        [
            "Synthetic demonstration only; the practice work item references earlier bounded receipts and contains no copied source values or clinical details.",
            "Submission is not practice acceptance, a telehealth request, a doctor search, a patient or clinician queue entry, or a response-time promise.",
            "No patient change, appointment, encounter, care, prescribing, billing, claim, integration, or external action was created."
        ],
        "SyntheticPracticeReviewAuthorized" =>
        [
            "Synthetic demonstration only; practice authorization is operational and is not clinical eligibility, exact rendering-clinician network confirmation, a coverage guarantee, or care acceptance.",
            "No request exists until the access-key owner separately confirms the request-creation boundary.",
            "No doctor search, queue entry or position, appointment, encounter, consent, care, prescribing, billing, claim, integration, or external action was created."
        ],
        "SyntheticRequestCreated" =>
        [
            "Synthetic demonstration only; one source-linked request exists and the synthetic patient account remains portal-disabled.",
            "A request status is not clinical eligibility, exact rendering-clinician network confirmation, coverage, acceptance, a doctor search, or a queue entry.",
            "No queue position, appointment, encounter, consent, media, care, prescribing, billing, claim, integration, or external action was created."
        ],
        "SyntheticPromotionBlockedPossibleMatch" =>
        [
            "Synthetic demonstration only; no message was sent and no real identity was proved.",
            "A possible patient match blocked promotion; no patient was created or linked and no candidate is disclosed.",
            "No chart content, portal, completed intake, consent, coverage, request, queue, or care capability was created."
        ],
        _ =>
        [
            "Synthetic demonstration only; no message was sent.",
            "Contact verification is not identity proofing.",
            "No clinician reviewed any universal safety-screen answers.",
            "This applicant is not a patient, has no chart, and cannot enter the telehealth queue."
        ]
    };

    private void RequireConfiguredHost(HostString host)
    {
        if (!_options.BrandedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new TelehealthProblem(
                StatusCodes.Status404NotFound,
                "telehealth_practice_not_found",
                "Telehealth practice was not found",
                "This host is not configured for the synthetic telehealth practice.");
        }
    }
}
