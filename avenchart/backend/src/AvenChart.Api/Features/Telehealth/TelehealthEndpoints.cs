// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Infrastructure;
using Microsoft.Extensions.Options;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthEndpoints
{
    public const string IdempotencyHeader = "X-Idempotency-Key";
    public const string ApplicantAccessHeader = "X-AvenChart-Telehealth-Applicant-Key";

    public static WebApplication MapTelehealthEndpoints(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<IOptions<TelehealthOptions>>().Value.Enabled)
        {
            return app;
        }

        var root = app.MapGroup("/api/telehealth/v1")
            .WithTags("Telehealth (synthetic only)");

        root.MapGet("/context", GetContextAsync)
            .WithName("GetTelehealthPracticeContext")
            .Produces<TelehealthPracticeContextResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var applicants = root.MapGroup("/applicants");
        applicants.MapPost("", CreateProspectiveApplicantAsync)
            .WithName("CreateTelehealthProspectiveApplicant")
            .WithDescription("Creates an isolated synthetic prospective applicant. It does not create or link a canonical patient, chart, portal account, visit request, or queue entry.")
            .Accepts<CreateTelehealthProspectiveApplicantRequest>("application/json")
            .Produces<TelehealthProspectiveApplicantResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
        applicants.MapGet("/{applicantId:guid}", GetProspectiveApplicantAsync)
            .WithName("GetTelehealthProspectiveApplicant")
            .WithDescription("Returns only the access-key owner's coarse synthetic applicant state and never returns duplicate candidate data.")
            .Produces<TelehealthProspectiveApplicantResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        applicants.MapPost("/{applicantId:guid}/contact-verification", VerifyProspectiveApplicantContactAsync)
            .WithName("VerifyTelehealthProspectiveApplicantContact")
            .WithDescription("Verifies control of a synthetic demonstration contact only, then returns a privacy-safe duplicate disposition. This is not identity proofing.")
            .Accepts<VerifyTelehealthProspectiveApplicantContactRequest>("application/json")
            .Produces<TelehealthProspectiveApplicantResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/safety-triage", EvaluateProspectiveSafetyTriageAsync)
            .WithName("EvaluateTelehealthProspectiveSafetyTriage")
            .WithDescription("Evaluates one emergency-first synthetic universal safety screen for a no-candidate staff-reviewed prospective applicant. A passing result permits only a later intake step and never creates a patient, request, appointment, or queue entry.")
            .Accepts<EvaluateTelehealthProspectiveSafetyTriageRequest>("application/json")
            .Produces<TelehealthProspectiveSafetyTriageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/visit-purpose", RecordProspectiveVisitPurposeAsync)
            .WithName("RecordTelehealthProspectiveVisitPurpose")
            .WithDescription("Records one controlled synthetic migraine-or-sleep navigation category after a passing universal safety screen. It does not diagnose, determine clinical eligibility, or create a patient, request, appointment, or queue entry.")
            .Accepts<RecordTelehealthProspectiveVisitPurposeRequest>("application/json")
            .Produces<TelehealthProspectiveVisitPurposeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/practice-network-precheck/options", GetProspectivePracticeNetworkOptionsAsync)
            .WithName("GetTelehealthProspectivePracticeNetworkOptions")
            .WithDescription("Returns a private versioned NON_PRODUCTION plan catalog for one eligible synthetic applicant. It does not verify member eligibility, benefits, a rendering physician, exact network status, coverage, or payment.")
            .Produces<TelehealthProspectivePracticeNetworkOptionsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/practice-network-precheck", RecordProspectivePracticeNetworkPrecheckAsync)
            .WithName("RecordTelehealthProspectivePracticeNetworkPrecheck")
            .WithDescription("Records one immutable synthetic practice-level plan fixture after visit-purpose classification. It does not perform a payer call, member eligibility, benefits, physician participation, exact network confirmation, coverage, pricing, or care.")
            .Accepts<RecordTelehealthProspectivePracticeNetworkPrecheckRequest>("application/json")
            .Produces<TelehealthProspectivePracticeNetworkPrecheckResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/member-insurance-details", RecordProspectiveMemberInsuranceDetailsAsync)
            .WithName("RecordTelehealthProspectiveMemberInsuranceDetails")
            .WithDescription("Records one protected mask-only receipt for SYN-prefixed demonstration member/group/subscriber details. It does not match a member, create canonical coverage, contact a payer, verify eligibility or benefits, confirm exact network status, price care, or enable care.")
            .Accepts<RecordTelehealthProspectiveMemberInsuranceDetailsRequest>("application/json")
            .Produces<TelehealthProspectiveMemberInsuranceDetailsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/eligibility", RecordProspectiveEligibilityAsync)
            .WithName("RecordTelehealthProspectiveEligibility")
            .WithDescription("Records one normalized NON_PRODUCTION eligibility fixture derived from protected synthetic member details. It creates no X12 transaction, payer call, exact network confirmation, canonical coverage, financial amount, patient, request, queue entry, or care capability.")
            .Accepts<RecordTelehealthProspectiveEligibilityRequest>("application/json")
            .Produces<TelehealthProspectiveEligibilityResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/practice-network-determination", RecordProspectivePracticeNetworkAsync)
            .WithName("RecordTelehealthProspectivePracticeNetwork")
            .WithDescription("Records one normalized NON_PRODUCTION practice/facility/service network fixture after fresh synthetic eligibility. It creates no FHIR resource or directory call, checks no rendering physician, and creates no canonical coverage, financial amount, patient, request, queue entry, or care capability.")
            .Accepts<RecordTelehealthProspectivePracticeNetworkRequest>("application/json")
            .Produces<TelehealthProspectivePracticeNetworkResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/identity-proofing", RecordProspectiveIdentityProofingAsync)
            .WithName("RecordTelehealthProspectiveIdentityProofing")
            .WithDescription("Records one normalized NON_PRODUCTION identity-proofing process fixture after fresh active eligibility and a positive practice-network fixture. It collects no real evidence, government identifier, image, video, or biometric; claims no identity assurance level; contacts no external source; and creates no patient, request, queue entry, or care capability.")
            .Accepts<RecordTelehealthProspectiveIdentityProofingRequest>("application/json")
            .Produces<TelehealthProspectiveIdentityProofingResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/telehealth-notice", GetApplicantTelehealthNoticeAsync)
            .WithName("GetTelehealthApplicantNotice")
            .WithDescription("Returns the applicant owner's server-selected synthetic GA, CA, or FL telehealth-notice fixture after successful patient-shell promotion. It returns no patient identifier and is not legally effective consent.")
            .Produces<TelehealthApplicantNoticeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/telehealth-notice/acknowledgment", AcknowledgeApplicantTelehealthNoticeAsync)
            .WithName("AcknowledgeTelehealthApplicantNotice")
            .WithDescription("Records one immutable synthetic state-notice acknowledgment bound to the successful promotion and safety-location evidence. It is not clinician-obtained or legally effective consent and creates no portal, request, queue, or care capability.")
            .Accepts<AcknowledgeTelehealthApplicantNoticeRequest>("application/json")
            .Produces<TelehealthApplicantNoticeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/registration-details", GetApplicantRegistrationDetailsAsync)
            .WithName("GetTelehealthApplicantRegistrationDetails")
            .WithDescription("Returns the applicant owner's exact minimum registration details copied into the portal-disabled synthetic patient shell after notice acknowledgment. Contacts remain masked; no patient identifier, street address, complete intake, identity assurance, insurance confirmation, request, queue, or care capability is returned.")
            .Produces<TelehealthApplicantRegistrationDetailsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/registration-details/confirmation", ConfirmApplicantRegistrationDetailsAsync)
            .WithName("ConfirmTelehealthApplicantRegistrationDetails")
            .WithDescription("Records one immutable no-edit confirmation of the exact server snapshot. It does not establish identity assurance, edit the patient, complete intake, confirm insurance, or create a request, queue entry, or care capability.")
            .Accepts<ConfirmTelehealthApplicantRegistrationDetailsRequest>("application/json")
            .Produces<TelehealthApplicantRegistrationDetailsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/insurance-handoff", GetApplicantInsuranceHandoffAsync)
            .WithName("GetTelehealthApplicantInsuranceHandoff")
            .WithDescription("Returns an applicant-owned, masked, synthetic insurance-details handoff after minimum registration details are confirmed. It returns no raw member value or patient identifier and does not verify coverage, payment, benefits, exact network participation, or a rendering physician.")
            .Produces<TelehealthApplicantInsuranceHandoffResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/insurance-handoff/confirmation", ConfirmApplicantInsuranceHandoffAsync)
            .WithName("ConfirmTelehealthApplicantInsuranceHandoff")
            .WithDescription("Records one immutable no-edit confirmation of the masked synthetic insurance handoff and its limitations. It creates no canonical coverage, financial record, request, queue entry, or care capability.")
            .Accepts<ConfirmTelehealthApplicantInsuranceHandoffRequest>("application/json")
            .Produces<TelehealthApplicantInsuranceHandoffResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/communication-access-readiness", GetApplicantCommunicationAccessReadinessAsync)
            .WithName("GetTelehealthApplicantCommunicationAccessReadiness")
            .WithDescription("Returns the applicant owner's masked callback/location context and bounded synthetic language/support preference catalog after insurance confirmation. It returns no raw contact, patient, insurance, proofing, or clinical data and does not arrange support or create care capability.")
            .Produces<TelehealthApplicantCommunicationAccessReadinessResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/communication-access-readiness", RecordApplicantCommunicationAccessReadinessAsync)
            .WithName("RecordTelehealthApplicantCommunicationAccessReadiness")
            .WithDescription("Records one immutable synthetic communication/access preference receipt. It does not arrange an interpreter or accommodation, establish technology readiness, complete intake or consent, or create a request, queue entry, communication, or care capability.")
            .Accepts<RecordTelehealthApplicantCommunicationAccessReadinessRequest>("application/json")
            .Produces<TelehealthApplicantCommunicationAccessReadinessResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/device-preparation", GetApplicantDevicePreparationAsync)
            .WithName("GetTelehealthApplicantDevicePreparation")
            .WithDescription("Returns the applicant owner's bounded synthetic device-preparation policy after communication/access readiness. It returns no device identifier, media, patient, contact, insurance, or clinical data and does not establish technology readiness or create a room, request, queue entry, or care capability.")
            .Produces<TelehealthApplicantDevicePreparationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/device-preparation", RecordApplicantDevicePreparationAsync)
            .WithName("RecordTelehealthApplicantDevicePreparation")
            .WithDescription("Records one immutable coarse client-reported synthetic device-preparation receipt. It does not certify a device, establish technology readiness, create a waiting room or media session, complete intake or consent, or create a request, queue entry, communication, or care capability.")
            .Accepts<RecordTelehealthApplicantDevicePreparationRequest>("application/json")
            .Produces<TelehealthApplicantDevicePreparationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/clinical-information-inventory", GetApplicantClinicalInformationInventoryAsync)
            .WithName("GetTelehealthApplicantClinicalInformationInventory")
            .WithDescription("Returns the applicant owner's bounded synthetic three-category clinical-information inventory after device preparation. It returns no clinical details or canonical chart content and creates no review, request, queue entry, prescribing, or care capability.")
            .Produces<TelehealthApplicantClinicalInformationInventoryResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/clinical-information-inventory", RecordApplicantClinicalInformationInventoryAsync)
            .WithName("RecordTelehealthApplicantClinicalInformationInventory")
            .WithDescription("Records one immutable coarse patient-reported synthetic inventory receipt for medications, allergies or intolerances, and other health history. It collects no clinical details, does not reconcile a chart, and creates no review, request, queue entry, prescribing, or care capability.")
            .Accepts<RecordTelehealthApplicantClinicalInformationInventoryRequest>("application/json")
            .Produces<TelehealthApplicantClinicalInformationInventoryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/medication-information", GetApplicantMedicationInformationAsync)
            .WithName("GetTelehealthApplicantMedicationInformation")
            .WithDescription("Returns the applicant owner's bounded patient-reported synthetic medication-information context and fixed local ingredient catalog after the coarse clinical-information inventory. It returns no canonical medication list and creates no clinical review, request, queue entry, prescribing, or care capability.")
            .Produces<TelehealthApplicantMedicationInformationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/medication-information", RecordApplicantMedicationInformationAsync)
            .WithName("RecordTelehealthApplicantMedicationInformation")
            .WithDescription("Records one immutable patient-reported synthetic medication-information receipt from a fixed incomplete local ingredient catalog. It captures no dose or directions, performs no reconciliation or interaction check, and creates no review, request, queue entry, prescribing, or care capability.")
            .Accepts<RecordTelehealthApplicantMedicationInformationRequest>("application/json")
            .Produces<TelehealthApplicantMedicationInformationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/allergy-information", GetApplicantAllergyInformationAsync)
            .WithName("GetTelehealthApplicantAllergyInformation")
            .WithDescription("Returns the applicant owner's bounded patient-reported synthetic allergy or intolerance context and fixed local substance catalog after medication information. It returns no canonical allergy list or confirmed negation and creates no alert, safety check, clinical review, request, queue entry, prescribing, or care capability.")
            .Produces<TelehealthApplicantAllergyInformationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/allergy-information", RecordApplicantAllergyInformationAsync)
            .WithName("RecordTelehealthApplicantAllergyInformation")
            .WithDescription("Records one immutable patient-reported synthetic allergy or intolerance receipt from a fixed incomplete local substance catalog. It captures no reaction, severity, criticality, type, status, or free text, performs no reconciliation or contraindication check, and creates no alert, review, request, queue entry, prescribing, or care capability.")
            .Accepts<RecordTelehealthApplicantAllergyInformationRequest>("application/json")
            .Produces<TelehealthApplicantAllergyInformationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/health-history-information", GetApplicantHealthHistoryInformationAsync)
            .WithName("GetTelehealthApplicantHealthHistoryInformation")
            .WithDescription("Returns the applicant owner's bounded patient-reported synthetic health-history-topic context and fixed local topic catalog after allergy information. Topics are not diagnoses, findings, assessments, or canonical history and create no triage change, clinical review, request, queue entry, prescribing, or care capability.")
            .Produces<TelehealthApplicantHealthHistoryInformationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/health-history-information", RecordApplicantHealthHistoryInformationAsync)
            .WithName("RecordTelehealthApplicantHealthHistoryInformation")
            .WithDescription("Records one immutable patient-reported synthetic health-history-topic receipt from a fixed incomplete local catalog. It captures no diagnosis, finding, assessment, status, timing, detail, or free text and creates no canonical history, risk evaluation, triage change, review, request, queue entry, prescribing, or care capability.")
            .Accepts<RecordTelehealthApplicantHealthHistoryInformationRequest>("application/json")
            .Produces<TelehealthApplicantHealthHistoryInformationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/clinical-information-summary", GetApplicantClinicalInformationSummaryAsync)
            .WithName("GetTelehealthApplicantClinicalInformationSummary")
            .WithDescription("Returns the applicant owner's server-derived summary of the prior coarse medication, allergy/intolerance, and health-history receipts. It exposes no clinical detail and creates no reconciliation, intake completion, review task, request, queue entry, prescribing, or care capability.")
            .Produces<TelehealthApplicantClinicalInformationSummaryResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/clinical-information-summary", ConfirmApplicantClinicalInformationSummaryAsync)
            .WithName("ConfirmTelehealthApplicantClinicalInformationSummary")
            .WithDescription("Records one immutable no-edit confirmation of a server-derived synthetic clinical-information summary. Confirmation is not verification, reconciliation, intake completion, eligibility, practice acceptance, a request, a queue entry, prescribing, or care authority.")
            .Accepts<ConfirmTelehealthApplicantClinicalInformationSummaryRequest>("application/json")
            .Produces<TelehealthApplicantClinicalInformationSummaryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/pre-request-readiness", GetApplicantPreRequestReadinessAsync)
            .WithName("GetTelehealthApplicantPreRequestReadiness")
            .WithDescription("Returns five coarse server-derived synthetic onboarding section states and unresolved routes. It exposes no source values and creates no completion, eligibility, review task, acceptance, request, queue entry, or care capability.")
            .Produces<TelehealthApplicantPreRequestReadinessResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/pre-request-readiness", AcknowledgeApplicantPreRequestReadinessAsync)
            .WithName("AcknowledgeTelehealthApplicantPreRequestReadiness")
            .WithDescription("Records one immutable acknowledgment of a minimized synthetic pre-request readiness projection. It is not completed intake, eligibility, consent, practice acceptance, request submission, queue entry, or care authority.")
            .Accepts<AcknowledgeTelehealthApplicantPreRequestReadinessRequest>("application/json")
            .Produces<TelehealthApplicantPreRequestReadinessResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/practice-review-submission", GetApplicantPracticeReviewSubmissionAsync)
            .WithName("GetTelehealthApplicantPracticeReviewSubmission")
            .WithDescription("Returns one minimized applicant-owned synthetic practice-review submission projection. It exposes no source values and creates no practice decision, telehealth request, patient or clinician queue entry, appointment, encounter, or care capability.")
            .Produces<TelehealthApplicantPracticeReviewResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/practice-review-submission", SubmitApplicantPracticeReviewAsync)
            .WithName("SubmitTelehealthApplicantPracticeReview")
            .WithDescription("Creates one synthetic practice-intake review work item. It is not practice acceptance, a telehealth request, a doctor search, a patient or clinician queue entry, an appointment, encounter, or care authority.")
            .Accepts<SubmitTelehealthApplicantPracticeReviewRequest>("application/json")
            .Produces<TelehealthApplicantPracticeReviewResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/telehealth-request", GetApplicantTelehealthRequestAsync)
            .WithName("GetTelehealthApplicantRequestCreation")
            .WithDescription("Returns the applicant owner's private synthetic Draft-request creation state. It exposes no patient identifier or source detail and creates no queue, doctor search, appointment, encounter, consent, care, prescribing, financial, integration, or external capability.")
            .Produces<TelehealthApplicantRequestCreationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/telehealth-request", CreateApplicantTelehealthRequestAsync)
            .WithName("CreateTelehealthApplicantRequest")
            .WithDescription("Creates exactly one authorization-gated synthetic Draft telehealth request. It does not start a doctor search or create a patient or clinician queue entry, queue position, appointment, encounter, consent, care, prescribing, financial, integration, or external action.")
            .Accepts<CreateTelehealthApplicantRequest>("application/json")
            .Produces<TelehealthApplicantRequestCreationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/telehealth-request/location", GetApplicantTelehealthRequestLocationAsync)
            .WithName("GetTelehealthApplicantRequestLocation")
            .WithDescription("Returns the applicant owner's private masked request-time location and callback confirmation state. It creates no triage result, clinical review, contact, queue, appointment, encounter, consent, care, financial, integration, or external capability.")
            .Produces<TelehealthApplicantRequestLocationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/telehealth-request/location", ConfirmApplicantTelehealthRequestLocationAsync)
            .WithName("ConfirmTelehealthApplicantRequestLocation")
            .WithDescription("Binds the exact supported location and masked callback context to one applicant-created Draft request and advances it only to LocationConfirmed. It does not perform triage or create any downstream care workflow.")
            .Accepts<ConfirmTelehealthApplicantRequestLocation>("application/json")
            .Produces<TelehealthApplicantRequestLocationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapGet("/{applicantId:guid}/telehealth-request/safety", GetApplicantTelehealthRequestSafetyAsync)
            .WithName("GetTelehealthApplicantRequestUniversalSafety")
            .WithDescription("Returns the applicant owner's private request-time universal safety-screen state. It returns no submitted answers and creates no review work item, contact, queue, appointment, encounter, care, financial, integration, or external capability.")
            .Produces<TelehealthApplicantRequestUniversalSafetyResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);
        applicants.MapPost("/{applicantId:guid}/telehealth-request/safety", AssessApplicantTelehealthRequestSafetyAsync)
            .WithName("AssessTelehealthApplicantRequestUniversalSafety")
            .WithDescription("Evaluates the four explicit synthetic universal-safety answers against the immutable non-production fixture. A pass advances only to complaint-specific safety screening; it is not clinical eligibility and creates no downstream care workflow.")
            .Accepts<EvaluateTelehealthApplicantRequestUniversalSafety>("application/json")
            .Produces<TelehealthApplicantRequestUniversalSafetyResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);

        var patient = root.MapGroup("/patient");
        patient.MapGet("/requests", ListPatientRequestsAsync)
            .WithName("ListPatientTelehealthRequests")
            .Produces<TelehealthRequestListResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        patient.MapGet("/requests/{requestId:guid}/status", GetPatientQueueStatusAsync)
            .WithName("GetPatientTelehealthQueueStatus")
            .WithDescription("Returns the authenticated request owner's authoritative synthetic status and an approximate same-practice/facility queue count when available. It never promises a wait time or exposes another patient or clinician.")
            .Produces<TelehealthPatientQueueStatusResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        patient.MapPost("/requests", CreateRequestAsync)
            .WithName("CreatePatientTelehealthRequest")
            .Accepts<CreateTelehealthRequest>("application/json")
            .Produces<TelehealthRequestResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
        patient.MapPost("/requests/{requestId:guid}/location", ConfirmLocationAsync)
            .WithName("ConfirmPatientTelehealthLocation")
            .Accepts<ConfirmTelehealthLocationRequest>("application/json")
            .Produces<TelehealthRequestResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);
        patient.MapPost("/requests/{requestId:guid}/triage", EvaluateTriageAsync)
            .WithName("EvaluatePatientTelehealthTriage")
            .Accepts<EvaluateTelehealthTriageRequest>("application/json")
            .Produces<TelehealthRequestResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);
        patient.MapGet("/requests/{requestId:guid}/readiness", GetPatientReadinessAsync)
            .WithName("GetPatientTelehealthReadiness")
            .Produces<TelehealthPatientReadinessResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        patient.MapPost("/requests/{requestId:guid}/readiness", CompleteReadinessAsync)
            .WithName("CompletePatientTelehealthReadiness")
            .Accepts<CompleteTelehealthReadinessRequest>("application/json")
            .Produces<TelehealthRequestResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        patient.MapPost("/requests/{requestId:guid}/coverage/verify", VerifyCoverageAsync)
            .WithName("VerifyPatientTelehealthCoverage")
            .Accepts<VerifyTelehealthCoverageRequest>("application/json")
            .Produces<TelehealthRequestResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);
        patient.MapPost("/requests/{requestId:guid}/connection-grants", PreparePatientConnectionAsync)
            .WithName("PreparePatientTelehealthConnection")
            .WithDescription("Runs a coarse synthetic device preflight and issues a short-lived participant-scoped NON_PRODUCTION waiting-room grant. No media or consultation is started.")
            .Accepts<PrepareTelehealthConnectionRequest>("application/json")
            .Produces<TelehealthConnectionGrantResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var admin = root.MapGroup("/admin");
        admin.MapGet("/applicant-practice-review", ListApplicantPracticeReviewInboxAsync)
            .WithName("ListTelehealthApplicantPracticeReviewInbox")
            .WithDescription("Returns a private minimized read-only practice/facility inbox of pending synthetic practice-review work items. It exposes no source details and offers no decision, assignment, request, queue, appointment, encounter, care, financial, integration, or external capability.")
            .Produces<TelehealthApplicantPracticeReviewInboxResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));
        admin.MapPost("/applicant-practice-review/{practiceReviewCaseId:guid}/claim", ClaimApplicantPracticeReviewAsync)
            .WithName("ClaimTelehealthApplicantPracticeReview")
            .WithDescription("Creates one immutable 120-second synthetic staff review claim. It prevents duplicate operational work only and creates no priority, decision, contact, request, care queue, appointment, encounter, care, financial, integration, or external capability.")
            .Accepts<ClaimTelehealthApplicantPracticeReviewRequest>("application/json")
            .Produces<TelehealthApplicantPracticeReviewClaimResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
        admin.MapGet("/applicant-practice-review/{practiceReviewCaseId:guid}", GetApplicantPracticeReviewPacketAsync)
            .WithName("GetTelehealthApplicantPracticeReviewPacket")
            .WithDescription("Returns one private minimized synthetic operational packet only to the current unexpired review claimant. It exposes no patient chart, clinical selections, raw identifiers, decision, contact, request, care queue, appointment, encounter, financial, integration, or external capability and does not extend the claim.")
            .Produces<TelehealthApplicantPracticeReviewPacketResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));
        admin.MapPost("/applicant-practice-review/{practiceReviewCaseId:guid}/authorization", AuthorizeApplicantPracticeReviewAsync)
            .WithName("AuthorizeTelehealthApplicantPracticeReview")
            .WithDescription("Records one immutable positive-only operational authorization for a separately gated future synthetic request-creation step. It creates no practice acceptance, patient contact, request, queue, appointment, encounter, consent, care, financial, integration, or external capability.")
            .Accepts<AuthorizeTelehealthApplicantPracticeReviewRequest>("application/json")
            .Produces<TelehealthApplicantPracticeReviewAuthorizationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
        admin.MapGet("/applicant-identity-review", ListApplicantIdentityReviewAsync)
            .WithName("ListTelehealthApplicantIdentityReview")
            .WithDescription("Returns a PHI-minimized configured-practice queue of contact-verified synthetic prospective applicants. It exposes no possible matching patient or canonical identifier and performs no identity proofing.")
            .Produces<TelehealthApplicantIdentityReviewQueueResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));
        admin.MapPut("/applicants/{applicantId:guid}/identity-review-decision", RecordApplicantIdentityReviewAsync)
            .WithName("RecordTelehealthApplicantIdentityReview")
            .WithDescription("Appends one deterministic synthetic review decision based only on contact control and duplicate disposition. The applicant remains prospective; no patient, chart, portal, request, or queue entry is created.")
            .Accepts<RecordTelehealthApplicantIdentityReviewRequest>("application/json")
            .Produces<TelehealthApplicantIdentityReviewDecisionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
        admin.MapGet("/applicant-promotion-authorization", ListApplicantPromotionAuthorizationAsync)
            .WithName("ListTelehealthApplicantPromotionAuthorization")
            .WithDescription("Returns a private PHI-minimized staff queue of synthetic applicants with a complete unexpired process chain. Assurance remains None; no patient or downstream capability is created.")
            .Produces<TelehealthApplicantPromotionAuthorizationQueueResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));
        admin.MapPut("/applicants/{applicantId:guid}/promotion-authorization-decision", RecordApplicantPromotionAuthorizationAsync)
            .WithName("RecordTelehealthApplicantPromotionAuthorization")
            .WithDescription("Appends one staff authorization or denial for a separately gated future synthetic promotion exercise. It does not prove identity or create a patient, chart, account, request, queue entry, or care capability.")
            .Accepts<RecordTelehealthApplicantPromotionAuthorizationRequest>("application/json")
            .Produces<TelehealthApplicantPromotionAuthorizationDecisionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
        admin.MapGet("/applicant-synthetic-promotion", ListApplicantSyntheticPromotionAsync)
            .WithName("ListTelehealthApplicantSyntheticPromotion")
            .WithDescription("Returns a private minimized administrator queue of authorized synthetic applicants awaiting an atomic duplicate-rechecked patient-shell promotion. It exposes no candidate or canonical patient identifier.")
            .Produces<TelehealthApplicantSyntheticPromotionQueueResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));
        admin.MapPut("/applicants/{applicantId:guid}/synthetic-promotion", ExecuteApplicantSyntheticPromotionAsync)
            .WithName("ExecuteTelehealthApplicantSyntheticPromotion")
            .WithDescription("Atomically rechecks current duplicates and either creates one portal-disabled synthetic patient shell or records a privacy-safe duplicate block. It creates no portal, coverage, request, queue, or care capability.")
            .Accepts<ExecuteTelehealthApplicantSyntheticPromotionRequest>("application/json")
            .Produces<TelehealthApplicantSyntheticPromotionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
        admin.MapGet("/operational-review", ListOperationalReviewAsync)
            .WithName("ListTelehealthOperationalReview")
            .Produces<TelehealthOperationalReviewResponse>()
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "view"));
        admin.MapPost("/requests/{requestId:guid}/authorize", AuthorizeToQueueAsync)
            .WithName("AuthorizeTelehealthRequestToQueue")
            .Accepts<AuthorizeTelehealthRequest>("application/json")
            .Produces<TelehealthRequestResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        var clinician = root.MapGroup("/clinician");
        clinician.MapGet("/queue", ListClinicianQueueAsync)
            .WithName("ListTelehealthClinicianQueue")
            .Produces<TelehealthQueueResponse>()
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "view"));
        clinician.MapPost("/shifts", StartShiftAsync)
            .WithName("StartTelehealthClinicianShift")
            .Produces<TelehealthShiftResponse>(StatusCodes.Status201Created)
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
        clinician.MapPost("/reservations/reserve-next", ReserveNextAsync)
            .WithName("ReserveNextTelehealthRequest")
            .Produces<TelehealthReservationResponse>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
        clinician.MapPost("/reservations/{reservationId:guid}/connection-grants", PreparePhysicianConnectionAsync)
            .WithName("PreparePhysicianTelehealthConnection")
            .WithDescription("Issues a short-lived NON_PRODUCTION waiting-room grant only to the physician who owns the active reservation. No media or encounter is started.")
            .Accepts<PrepareTelehealthConnectionRequest>("application/json")
            .Produces<TelehealthConnectionGrantResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
        clinician.MapPost("/reservations/{reservationId:guid}/consultations/start", StartConsultationAsync)
            .WithName("StartTelehealthConsultation")
            .WithDescription("Creates one synthetic appointment-linked AvenChart encounter and opaque consultation context after all start gates pass. It enables only the bounded consultation projection and explicit unsigned SOAP draft; signing, prescription, claim, completion, and real-media capabilities remain disabled.")
            .Accepts<StartTelehealthConsultationRequest>("application/json")
            .Produces<TelehealthConsultationStartResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "write"));
        clinician.MapGet("/consultations/{consultationId:guid}/workspace", GetConsultationWorkspaceAsync)
            .WithName("GetTelehealthConsultationWorkspace")
            .WithDescription("Returns only the owning physician's bounded current synthetic patient/visit/active-list projection. It exposes no patient, encounter, appointment, or request key and enables no mutation.")
            .Produces<TelehealthConsultationWorkspaceResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"));
        clinician.MapPut("/consultations/{consultationId:guid}/documentation/draft", SaveConsultationDocumentationDraftAsync)
            .WithName("SaveTelehealthConsultationDocumentationDraft")
            .WithDescription("Explicitly appends one unsigned SOAP draft version to the owning physician's active synthetic encounter. No autosave, signing, diagnosis, prescription, claim, completion, or patient delivery occurs.")
            .Accepts<TelehealthConsultationDocumentationDraftRequest>("application/json")
            .Produces<TelehealthConsultationDocumentationDraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "write"));
        clinician.MapPost("/consultations/{consultationId:guid}/wrap-up", EnterConsultationWrapUpAsync)
            .WithName("EnterTelehealthConsultationWrapUp")
            .WithDescription("Moves the owning physician's synthetic consultation into unfinished wrap-up while keeping the encounter open, documentation unsigned, and the physician unavailable for new work.")
            .Accepts<EnterTelehealthConsultationWrapUpRequest>("application/json")
            .Produces<TelehealthConsultationWrapUpResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "write"));
        clinician.MapGet("/consultations/{consultationId:guid}/pharmacy-choices", GetConsultationPharmacyChoicesAsync)
            .WithName("GetTelehealthConsultationPharmacyChoices")
            .WithDescription("Returns neutral deterministic NON_PRODUCTION pharmacy directory choices, an associated synthetic chart preference when present, and the current unsigned destination draft only for the physician who owns an unfinished wrap-up.")
            .Produces<TelehealthPharmacyChoiceWorkspaceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"));
        clinician.MapPut("/consultations/{consultationId:guid}/pharmacy-choice", RecordConsultationPharmacyChoiceAsync)
            .WithName("RecordTelehealthConsultationPharmacyChoice")
            .WithDescription("Appends an unsigned patient-confirmed synthetic destination draft. It creates no medication, prescription, signature, transmission, claim, completion, or external call.")
            .Accepts<RecordTelehealthPharmacyChoiceRequest>("application/json")
            .Produces<TelehealthPharmacyChoiceDraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "write"));
        clinician.MapGet("/consultations/{consultationId:guid}/prescription-preparation-draft", GetConsultationPrescriptionPreparationDraftAsync)
            .WithName("GetTelehealthConsultationPrescriptionPreparationDraft")
            .WithDescription("Returns a neutral non-controlled synthetic medication-catalog search and the owning physician's current unsigned, unchecked preparation draft. It is not a prescription or recommendation and enables no signing, transmission, delivery, completion, or external call.")
            .Produces<TelehealthPrescriptionPreparationWorkspaceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("patients", "med", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"));
        clinician.MapPut("/consultations/{consultationId:guid}/prescription-preparation-draft", RecordConsultationPrescriptionPreparationDraftAsync)
            .WithName("RecordTelehealthConsultationPrescriptionPreparationDraft")
            .WithDescription("Appends a physician-authored NON_PRODUCTION preparation draft for one catalog-selected non-controlled medication. It creates no canonical medication or prescription, signature, transmission, AVS, bill, claim, lifecycle transition, or external call.")
            .Accepts<RecordTelehealthPrescriptionPreparationDraftRequest>("application/json")
            .Produces<TelehealthPrescriptionPreparationDraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "write"));
        clinician.MapGet("/consultations/{consultationId:guid}/safety-disposition-draft", GetConsultationSafetyDispositionDraftAsync)
            .WithName("GetTelehealthConsultationSafetyDispositionDraft")
            .WithDescription("Returns only the owning physician's current unsigned, undelivered synthetic safety-disposition draft and bounded physician-selected vocabularies during unfinished wrap-up.")
            .Produces<TelehealthSafetyDispositionWorkspaceResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"));
        clinician.MapGet("/consultations/{consultationId:guid}/completion-prerequisites", GetConsultationCompletionPrerequisitesAsync)
            .WithName("GetTelehealthConsultationCompletionPrerequisites")
            .WithDescription("Returns a minimized, read-only structural-evidence review for the owning physician during unfinished wrap-up. It does not assess clinical sufficiency or enable signing, completion, delivery, prescriptions, claims, or downstream creation.")
            .Produces<TelehealthCompletionPrerequisitesResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"));
        clinician.MapPut("/consultations/{consultationId:guid}/safety-disposition-draft", RecordConsultationSafetyDispositionDraftAsync)
            .WithName("RecordTelehealthConsultationSafetyDispositionDraft")
            .WithDescription("Appends a physician-authored synthetic safety-disposition draft. It does not sign, finalize, deliver, complete, release the physician, or create an order, referral, prescription, bill, claim, message, task, outbox, or external handoff.")
            .Accepts<RecordTelehealthSafetyDispositionDraftRequest>("application/json")
            .Produces<TelehealthSafetyDispositionDraftResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "view"))
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth", "write"));

        return app;
    }

    private static Task<IResult> GetContextAsync(TelehealthService service, HttpContext context) =>
        ExecuteAsync(() => Task.FromResult<IResult>(Results.Ok(service.GetPracticeContext(context.Request.Host))));

    private static Task<IResult> CreateProspectiveApplicantAsync(
        TelehealthProspectiveApplicantService service,
        HttpContext context,
        CreateTelehealthProspectiveApplicantRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await service.CreateAsync(
                context,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken);
            return Results.Created($"/api/telehealth/v1/applicants/{result.ApplicantId}", result);
        });

    private static Task<IResult> GetProspectiveApplicantAsync(
        TelehealthProspectiveApplicantService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.GetAsync(
            context,
            applicantId,
            ReadApplicantAccessKey(context),
            cancellationToken)));

    private static Task<IResult> VerifyProspectiveApplicantContactAsync(
        TelehealthProspectiveApplicantService service,
        HttpContext context,
        Guid applicantId,
        VerifyTelehealthProspectiveApplicantContactRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.VerifyContactAsync(
            context,
            applicantId,
            request,
            ReadApplicantAccessKey(context),
            ReadIdempotencyKey(context),
            cancellationToken)));

    private static Task<IResult> EvaluateProspectiveSafetyTriageAsync(
        TelehealthProspectiveSafetyTriageService service,
        HttpContext context,
        Guid applicantId,
        EvaluateTelehealthProspectiveSafetyTriageRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.EvaluateAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordProspectiveVisitPurposeAsync(
        TelehealthProspectiveVisitPurposeService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthProspectiveVisitPurposeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetProspectivePracticeNetworkOptionsAsync(
        TelehealthProspectivePracticeNetworkPrecheckService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetOptionsAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordProspectivePracticeNetworkPrecheckAsync(
        TelehealthProspectivePracticeNetworkPrecheckService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthProspectivePracticeNetworkPrecheckRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordProspectiveMemberInsuranceDetailsAsync(
        TelehealthProspectiveMemberInsuranceDetailsService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthProspectiveMemberInsuranceDetailsRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordProspectiveEligibilityAsync(
        TelehealthProspectiveEligibilityService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthProspectiveEligibilityRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordProspectivePracticeNetworkAsync(
        TelehealthProspectivePracticeNetworkService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthProspectivePracticeNetworkRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordProspectiveIdentityProofingAsync(
        TelehealthProspectiveIdentityProofingService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthProspectiveIdentityProofingRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantTelehealthNoticeAsync(
        TelehealthApplicantNoticeService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> AcknowledgeApplicantTelehealthNoticeAsync(
        TelehealthApplicantNoticeService service,
        HttpContext context,
        Guid applicantId,
        AcknowledgeTelehealthApplicantNoticeRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.AcknowledgeAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantRegistrationDetailsAsync(
        TelehealthApplicantRegistrationDetailsService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> ConfirmApplicantRegistrationDetailsAsync(
        TelehealthApplicantRegistrationDetailsService service,
        HttpContext context,
        Guid applicantId,
        ConfirmTelehealthApplicantRegistrationDetailsRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.ConfirmAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantInsuranceHandoffAsync(
        TelehealthApplicantInsuranceHandoffService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> ConfirmApplicantInsuranceHandoffAsync(
        TelehealthApplicantInsuranceHandoffService service,
        HttpContext context,
        Guid applicantId,
        ConfirmTelehealthApplicantInsuranceHandoffRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.ConfirmAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantCommunicationAccessReadinessAsync(
        TelehealthApplicantCommunicationAccessService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordApplicantCommunicationAccessReadinessAsync(
        TelehealthApplicantCommunicationAccessService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthApplicantCommunicationAccessReadinessRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantDevicePreparationAsync(
        TelehealthApplicantDevicePreparationService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordApplicantDevicePreparationAsync(
        TelehealthApplicantDevicePreparationService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthApplicantDevicePreparationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantClinicalInformationInventoryAsync(
        TelehealthApplicantClinicalInformationInventoryService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordApplicantClinicalInformationInventoryAsync(
        TelehealthApplicantClinicalInformationInventoryService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthApplicantClinicalInformationInventoryRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantMedicationInformationAsync(
        TelehealthApplicantMedicationInformationService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordApplicantMedicationInformationAsync(
        TelehealthApplicantMedicationInformationService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthApplicantMedicationInformationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantAllergyInformationAsync(
        TelehealthApplicantAllergyInformationService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordApplicantAllergyInformationAsync(
        TelehealthApplicantAllergyInformationService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthApplicantAllergyInformationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantHealthHistoryInformationAsync(
        TelehealthApplicantHealthHistoryInformationService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> RecordApplicantHealthHistoryInformationAsync(
        TelehealthApplicantHealthHistoryInformationService service,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthApplicantHealthHistoryInformationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.RecordAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantClinicalInformationSummaryAsync(
        TelehealthApplicantClinicalInformationSummaryService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> ConfirmApplicantClinicalInformationSummaryAsync(
        TelehealthApplicantClinicalInformationSummaryService service,
        HttpContext context,
        Guid applicantId,
        ConfirmTelehealthApplicantClinicalInformationSummaryRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.ConfirmAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantPreRequestReadinessAsync(
        TelehealthApplicantPreRequestReadinessService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> AcknowledgeApplicantPreRequestReadinessAsync(
        TelehealthApplicantPreRequestReadinessService service,
        HttpContext context,
        Guid applicantId,
        AcknowledgeTelehealthApplicantPreRequestReadinessRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.AcknowledgeAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantPracticeReviewSubmissionAsync(
        TelehealthApplicantPracticeReviewSubmissionService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> SubmitApplicantPracticeReviewAsync(
        TelehealthApplicantPracticeReviewSubmissionService service,
        HttpContext context,
        Guid applicantId,
        SubmitTelehealthApplicantPracticeReviewRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.SubmitAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantTelehealthRequestAsync(
        TelehealthApplicantRequestCreationService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> CreateApplicantTelehealthRequestAsync(
        TelehealthApplicantRequestCreationService service,
        HttpContext context,
        Guid applicantId,
        CreateTelehealthApplicantRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            var result = await service.CreateAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken);
            return Results.Created(
                $"/api/telehealth/v1/applicants/{applicantId:D}/telehealth-request",
                result);
        });

    private static Task<IResult> GetApplicantTelehealthRequestLocationAsync(
        TelehealthApplicantRequestLocationService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> ConfirmApplicantTelehealthRequestLocationAsync(
        TelehealthApplicantRequestLocationService service,
        HttpContext context,
        Guid applicantId,
        ConfirmTelehealthApplicantRequestLocation request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.ConfirmAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> GetApplicantTelehealthRequestSafetyAsync(
        TelehealthApplicantRequestUniversalSafetyService service,
        HttpContext context,
        Guid applicantId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.GetAsync(
                context,
                applicantId,
                ReadApplicantAccessKey(context),
                cancellationToken));
        });

    private static Task<IResult> AssessApplicantTelehealthRequestSafetyAsync(
        TelehealthApplicantRequestUniversalSafetyService service,
        HttpContext context,
        Guid applicantId,
        EvaluateTelehealthApplicantRequestUniversalSafety request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            SetProspectiveApplicantPrivateResponse(context);
            return Results.Ok(await service.AssessAsync(
                context,
                applicantId,
                request,
                ReadApplicantAccessKey(context),
                ReadIdempotencyKey(context),
                cancellationToken));
        });

    private static Task<IResult> ListPatientRequestsAsync(
        TelehealthService service,
        HttpContext context,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.ListPatientRequestsAsync(context, cancellationToken)));

    private static Task<IResult> GetPatientQueueStatusAsync(
        TelehealthService service,
        HttpContext context,
        Guid requestId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.GetPatientQueueStatusAsync(
            context, requestId, cancellationToken)));

    private static Task<IResult> CreateRequestAsync(
        TelehealthService service,
        HttpContext context,
        CreateTelehealthRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await service.CreateRequestAsync(context, request, ReadIdempotencyKey(context), cancellationToken);
            return Results.Created($"/api/telehealth/v1/patient/requests/{result.RequestId}", result);
        });

    private static Task<IResult> ConfirmLocationAsync(
        TelehealthService service,
        HttpContext context,
        Guid requestId,
        ConfirmTelehealthLocationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.ConfirmLocationAsync(
            context, requestId, request, ReadIdempotencyKey(context), cancellationToken)));

    private static Task<IResult> EvaluateTriageAsync(
        TelehealthService service,
        HttpContext context,
        Guid requestId,
        EvaluateTelehealthTriageRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.EvaluateTriageAsync(
            context, requestId, request, ReadIdempotencyKey(context), cancellationToken)));

    private static Task<IResult> GetPatientReadinessAsync(
        TelehealthService service,
        HttpContext context,
        Guid requestId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.GetPatientReadinessAsync(
            context, requestId, cancellationToken)));

    private static Task<IResult> CompleteReadinessAsync(
        TelehealthService service,
        HttpContext context,
        Guid requestId,
        CompleteTelehealthReadinessRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.CompleteReadinessAsync(
            context, requestId, request, ReadIdempotencyKey(context), cancellationToken)));

    private static Task<IResult> VerifyCoverageAsync(
        TelehealthService service,
        HttpContext context,
        Guid requestId,
        VerifyTelehealthCoverageRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.VerifyCoverageAsync(
            context, requestId, request, ReadIdempotencyKey(context), cancellationToken)));

    private static Task<IResult> PreparePatientConnectionAsync(
        TelehealthVideoService service,
        HttpContext context,
        Guid requestId,
        PrepareTelehealthConnectionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Results.Ok(await service.PreparePatientAsync(
            context,
            requestId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));

    private static async Task<IResult> ListOperationalReviewAsync(
        TelehealthService service,
        AuthRepository authRepository,
        HttpContext context,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Ok(await service.ListOperationalReviewAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            cancellationToken)));

    private static async Task<IResult> ListApplicantIdentityReviewAsync(
        TelehealthApplicantIdentityReviewService service,
        AuthRepository authRepository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SetApplicantIdentityReviewPrivateResponse(context, "queue");
        return await ExecuteAsync(async () => Results.Ok(await service.ListAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            cancellationToken)));
    }

    private static async Task<IResult> ListApplicantPracticeReviewInboxAsync(
        TelehealthApplicantPracticeReviewInboxService service,
        AuthRepository authRepository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SetApplicantPracticeReviewInboxPrivateResponse(context);
        return await ExecuteAsync(async () => Results.Ok(await service.ListAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            cancellationToken)));
    }

    private static async Task<IResult> ClaimApplicantPracticeReviewAsync(
        TelehealthApplicantPracticeReviewClaimService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid practiceReviewCaseId,
        ClaimTelehealthApplicantPracticeReviewRequest request,
        CancellationToken cancellationToken)
    {
        SetApplicantPracticeReviewClaimPrivateResponse(context, practiceReviewCaseId);
        return await ExecuteAsync(async () => Results.Ok(await service.ClaimAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            practiceReviewCaseId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> GetApplicantPracticeReviewPacketAsync(
        TelehealthApplicantPracticeReviewPacketService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid practiceReviewCaseId,
        CancellationToken cancellationToken)
    {
        SetApplicantPracticeReviewPacketPrivateResponse(context, practiceReviewCaseId);
        return await ExecuteAsync(async () => Results.Ok(await service.GetAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            practiceReviewCaseId,
            cancellationToken)));
    }

    private static async Task<IResult> AuthorizeApplicantPracticeReviewAsync(
        TelehealthApplicantPracticeReviewAuthorizationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid practiceReviewCaseId,
        AuthorizeTelehealthApplicantPracticeReviewRequest request,
        CancellationToken cancellationToken)
    {
        SetApplicantPracticeReviewAuthorizationPrivateResponse(context, practiceReviewCaseId);
        return await ExecuteAsync(async () => Results.Ok(await service.AuthorizeAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            practiceReviewCaseId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> RecordApplicantIdentityReviewAsync(
        TelehealthApplicantIdentityReviewService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthApplicantIdentityReviewRequest request,
        CancellationToken cancellationToken)
    {
        SetApplicantIdentityReviewPrivateResponse(context, applicantId.ToString("D"));
        return await ExecuteAsync(async () => Results.Ok(await service.RecordAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            applicantId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> ListApplicantPromotionAuthorizationAsync(
        TelehealthApplicantPromotionAuthorizationService service,
        AuthRepository authRepository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SetApplicantPromotionAuthorizationPrivateResponse(context, "queue");
        return await ExecuteAsync(async () => Results.Ok(await service.ListAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            cancellationToken)));
    }

    private static async Task<IResult> RecordApplicantPromotionAuthorizationAsync(
        TelehealthApplicantPromotionAuthorizationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid applicantId,
        RecordTelehealthApplicantPromotionAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        SetApplicantPromotionAuthorizationPrivateResponse(context, applicantId.ToString("D"));
        return await ExecuteAsync(async () => Results.Ok(await service.RecordAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            applicantId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> ListApplicantSyntheticPromotionAsync(
        TelehealthApplicantSyntheticPromotionService service,
        AuthRepository authRepository,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SetApplicantSyntheticPromotionPrivateResponse(context, "queue");
        return await ExecuteAsync(async () => Results.Ok(await service.ListAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            cancellationToken)));
    }

    private static async Task<IResult> ExecuteApplicantSyntheticPromotionAsync(
        TelehealthApplicantSyntheticPromotionService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid applicantId,
        ExecuteTelehealthApplicantSyntheticPromotionRequest request,
        CancellationToken cancellationToken)
    {
        SetApplicantSyntheticPromotionPrivateResponse(context, applicantId.ToString("D"));
        return await ExecuteAsync(async () => Results.Ok(await service.ExecuteAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            applicantId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> AuthorizeToQueueAsync(
        TelehealthService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid requestId,
        AuthorizeTelehealthRequest request,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Ok(await service.AuthorizeToQueueAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            requestId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));

    private static async Task<IResult> ListClinicianQueueAsync(
        TelehealthService service,
        AuthRepository authRepository,
        HttpContext context,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Ok(await service.ListClinicianQueueAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            cancellationToken)));

    private static async Task<IResult> StartShiftAsync(
        TelehealthService service,
        AuthRepository authRepository,
        HttpContext context,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Created(
            "/api/telehealth/v1/clinician/shifts/current",
            await service.StartShiftAsync(
                await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
                RequireStaffAccessContext(context),
                ReadIdempotencyKey(context),
                cancellationToken)));

    private static async Task<IResult> ReserveNextAsync(
        TelehealthService service,
        AuthRepository authRepository,
        HttpContext context,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var result = await service.ReserveNextAsync(
                await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
                RequireStaffAccessContext(context),
                ReadIdempotencyKey(context),
                cancellationToken);
            return result is null ? Results.NoContent() : Results.Ok(result);
        });

    private static async Task<IResult> PreparePhysicianConnectionAsync(
        TelehealthVideoService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid reservationId,
        PrepareTelehealthConnectionRequest request,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Ok(await service.PreparePhysicianAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            reservationId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));

    private static async Task<IResult> StartConsultationAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid reservationId,
        StartTelehealthConsultationRequest request,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => Results.Ok(await service.StartAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            reservationId,
            request,
             ReadIdempotencyKey(context),
             cancellationToken)));

    private static async Task<IResult> GetConsultationWorkspaceAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthConsultation", consultationId.ToString("D"));
        return await ExecuteAsync(async () => Results.Ok(await service.GetWorkspaceAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            cancellationToken)));
    }

    private static async Task<IResult> SaveConsultationDocumentationDraftAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        TelehealthConsultationDocumentationDraftRequest request,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthConsultation", consultationId.ToString("D"));
        return await ExecuteAsync(async () => Results.Ok(await service.SaveDocumentationDraftAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            request,
            cancellationToken)));
    }

    private static async Task<IResult> EnterConsultationWrapUpAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        EnterTelehealthConsultationWrapUpRequest request,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthConsultation", consultationId.ToString("D"));
        return await ExecuteAsync(async () => Results.Ok(await service.EnterWrapUpAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> GetConsultationPharmacyChoicesAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        string? query,
        string? state,
        string? postalCode,
        string? originPostalCode,
        bool? locationSearchAcknowledged,
        int? limit,
        CancellationToken cancellationToken)
    {
        SetConsultationPrivateResponse(context, consultationId);
        return await ExecuteAsync(async () => Results.Ok(await service.GetPharmacyChoicesAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            query,
            state,
            postalCode,
            originPostalCode,
            locationSearchAcknowledged ?? false,
            limit ?? 25,
            cancellationToken)));
    }

    private static async Task<IResult> RecordConsultationPharmacyChoiceAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        RecordTelehealthPharmacyChoiceRequest request,
        CancellationToken cancellationToken)
    {
        SetConsultationPrivateResponse(context, consultationId);
        return await ExecuteAsync(async () => Results.Ok(await service.RecordPharmacyChoiceAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> GetConsultationSafetyDispositionDraftAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        SetConsultationPrivateResponse(context, consultationId);
        return await ExecuteAsync(async () => Results.Ok(await service.GetSafetyDispositionDraftAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            cancellationToken)));
    }

    private static async Task<IResult> GetConsultationPrescriptionPreparationDraftAsync(
        TelehealthPrescriptionService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        string? query,
        CancellationToken cancellationToken)
    {
        SetConsultationPrivateResponse(context, consultationId);
        return await ExecuteAsync(async () => Results.Ok(await service.GetWorkspaceAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            query,
            cancellationToken)));
    }

    private static async Task<IResult> RecordConsultationPrescriptionPreparationDraftAsync(
        TelehealthPrescriptionService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        RecordTelehealthPrescriptionPreparationDraftRequest request,
        CancellationToken cancellationToken)
    {
        SetConsultationPrivateResponse(context, consultationId);
        return await ExecuteAsync(async () => Results.Ok(await service.RecordAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> RecordConsultationSafetyDispositionDraftAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        RecordTelehealthSafetyDispositionDraftRequest request,
        CancellationToken cancellationToken)
    {
        SetConsultationPrivateResponse(context, consultationId);
        return await ExecuteAsync(async () => Results.Ok(await service.RecordSafetyDispositionDraftAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            request,
            ReadIdempotencyKey(context),
            cancellationToken)));
    }

    private static async Task<IResult> GetConsultationCompletionPrerequisitesAsync(
        TelehealthConsultationService service,
        AuthRepository authRepository,
        HttpContext context,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        SetConsultationPrivateResponse(context, consultationId);
        return await ExecuteAsync(async () => Results.Ok(await service.GetCompletionPrerequisitesAsync(
            await GetSessionFromHeaderAsync(authRepository, context, cancellationToken),
            RequireStaffAccessContext(context),
            consultationId,
            cancellationToken)));
    }

    private static void SetConsultationPrivateResponse(HttpContext context, Guid consultationId)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthConsultation", consultationId.ToString("D"));
    }

    private static void SetApplicantIdentityReviewPrivateResponse(HttpContext context, string resourceId)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthApplicantIdentityReview", resourceId);
    }

    private static void SetApplicantPracticeReviewInboxPrivateResponse(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthApplicantPracticeReviewInbox", "queue");
    }

    private static void SetApplicantPracticeReviewClaimPrivateResponse(HttpContext context, Guid caseId)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthApplicantPracticeReviewClaim", caseId.ToString("D"));
    }

    private static void SetApplicantPracticeReviewPacketPrivateResponse(HttpContext context, Guid caseId)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthApplicantPracticeReviewPacket", caseId.ToString("D"));
    }

    private static void SetApplicantPracticeReviewAuthorizationPrivateResponse(HttpContext context, Guid caseId)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthApplicantPracticeReviewAuthorization", caseId.ToString("D"));
    }

    private static void SetApplicantPromotionAuthorizationPrivateResponse(HttpContext context, string resourceId)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthApplicantPromotionAuthorization", resourceId);
    }

    private static void SetApplicantSyntheticPromotionPrivateResponse(HttpContext context, string resourceId)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        PhiAuditResourceContext.Set(context, "TelehealthApplicantSyntheticPromotion", resourceId);
    }

    private static void SetProspectiveApplicantPrivateResponse(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        context.Response.Headers.Vary = ApplicantAccessHeader;
    }

    private static string ReadIdempotencyKey(HttpContext context) =>
        context.Request.Headers[IdempotencyHeader].ToString();

    private static string ReadApplicantAccessKey(HttpContext context) =>
        context.Request.Headers[ApplicantAccessHeader].ToString();

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (TelehealthProblem problem)
        {
            return Results.Problem(
                statusCode: problem.StatusCode,
                title: problem.Title,
                detail: problem.Message,
                extensions: new Dictionary<string, object?> { ["code"] = problem.Code });
        }
    }
}
