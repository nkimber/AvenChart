// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantPracticeReviewPacketService(
    TelehealthApplicantPracticeReviewPacketRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantPracticeReviewPacketResponse> GetAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsAdministratorRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_administrator_role_required",
                "An authorized practice administrator is required for this view.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
        if (string.Equals(session.Role, "frontdesk", StringComparison.OrdinalIgnoreCase)
            && session.StaffId is null)
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "An authenticated front-desk identity must be bound to an active staff record.");
        }

        var record = await repository.GetAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            session.Username,
            caseId,
            cancellationToken) ?? throw TelehealthProblem.ApplicantNotFound();
        TelehealthApplicantPracticeReviewPacketPolicy.RequireAllowed(record);

        var sections = TelehealthApplicantPracticeReviewInboxPolicy.Sections(
                record.InterpreterRequested,
                record.AccessibilitySupportRequested,
                record.ClinicalInformationSummaryRoute)
            .Select(section => new TelehealthApplicantPracticeReviewInboxSectionResponse(
                section.SectionKey,
                section.ReceiptState,
                section.OutstandingRoute))
            .ToArray();

        return new(
            record.PracticeReviewCaseId,
            record.ApplicantVersion,
            record.ApplicantStatus,
            record.CaseStatus,
            TelehealthApplicantPracticeReviewPacketPolicy.PolicyKey,
            TelehealthApplicantPracticeReviewPacketPolicy.PolicyVersion,
            _options.PracticeDisplayName,
            record.DatabaseNow,
            record.AssignmentExpiresAt,
            record.LegalFirstName,
            record.LegalLastName,
            record.DateOfBirth,
            TelehealthProspectiveApplicantPolicy.MaskEmail(record.Email),
            TelehealthProspectiveApplicantPolicy.MaskPhone(record.Phone),
            record.ResidenceStateCode,
            record.PostalCode,
            record.PurposeCategory,
            record.PurposeDisplayLabel,
            record.SafetyOutcome,
            record.ReviewRoute,
            record.SubmittedAt,
            sections,
            new(
                ReceiptRecorded: true,
                record.RegistrationConfirmedAt,
                IdentityAssuranceEstablished: false,
                PatientRecordChanged: false),
            new(
                record.PayerDisplayName,
                record.ProductDisplayName,
                TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask(record.MemberIdLast4),
                record.GroupNumberLast4 is null
                    ? null
                    : TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask(record.GroupNumberLast4),
                record.SubscriberRelationship,
                record.CoveragePriority,
                record.EligibilityBusinessOutcome,
                record.EligibilityCheckedAt,
                record.EligibilityExpiresAt,
                record.EligibilityExpiresAt > record.DatabaseNow,
                record.PracticeNetworkBusinessOutcome,
                record.PracticeNetworkCheckedAt,
                record.PracticeNetworkExpiresAt,
                record.PracticeNetworkExpiresAt > record.DatabaseNow,
                record.RenderingPhysicianNetworkChecked,
                record.InsuranceConfirmedAt,
                CoverageVerified: false,
                ExactNetworkConfirmed: false,
                CanonicalCoverageCreated: false),
            new(
                record.PreferredSpokenLanguage,
                record.InterpreterRequested,
                record.AccessibilitySupportRequested,
                record.SafePrivateCommunicationConfirmed,
                record.CommunicationRecordedAt,
                InterpreterAssigned: false,
                AccessibilityAccommodationArranged: false,
                CommunicationArrangementCompleted: false),
            new(
                record.BrowserSupported,
                record.CameraAvailable,
                record.MicrophoneAvailable,
                record.SpeakerAvailable,
                record.NetworkQuality,
                record.DeviceRecordedAt,
                TechnologyReady: false,
                WaitingRoomCreated: false,
                MediaSessionCreated: false),
            record.ClinicalInformationSummaryRoute,
            record.ClinicalSummaryConfirmedAt,
            StaffReviewWorkItemExists: true,
            StaffActionTaken: true,
            Assigned: true,
            AssignedToCurrentUser: true,
            PriorityAssigned: false,
            PracticeAccepted: false,
            PracticeDeclined: false,
            PatientContacted: false,
            ClinicianReviewCreated: false,
            TelehealthRequestCreated: false,
            PatientCareQueueEntered: false,
            ClinicianQueueEntered: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Limitations:
            [
                "Synthetic operational evidence only; this packet is not a patient chart, clinical review, coverage guarantee, or rendering-clinician network determination.",
                "The short review claim is not extended by reading this packet and may expire while the packet is open.",
                "No decision, patient contact, request, care queue, appointment, encounter, prescribing, financial, integration, or external action is available."
            ]);
    }
}
