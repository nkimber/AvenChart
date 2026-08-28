// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantPracticeReviewInboxService(
    TelehealthApplicantPracticeReviewInboxRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantPracticeReviewInboxResponse> ListAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
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

        var result = await repository.ListAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            cancellationToken);
        return new(
            TelehealthApplicantPracticeReviewInboxPolicy.PolicyKey,
            TelehealthApplicantPracticeReviewInboxPolicy.PolicyVersion,
            _options.PracticeDisplayName,
            result.DatabaseNow,
            result.Items.Select(item => ToItem(item, session.Username)).ToArray(),
            Limitations());
    }

    private static TelehealthApplicantPracticeReviewInboxItemResponse ToItem(
        TelehealthApplicantPracticeReviewInboxRecord item,
        string currentActorId)
    {
        TelehealthApplicantPracticeReviewInboxPolicy.RequireAllowed(
            item.ApplicantStatus,
            item.CaseStatus,
            item.ReviewRoute,
            item.PurposeCategory,
            item.SafetyOutcome,
            item.ClinicalInformationSummaryRoute);
        var assigned = item.ActiveClaimExpiresAt is not null;
        return new(
            item.PracticeReviewCaseId,
            item.ApplicantVersion,
            item.ApplicantStatus,
            item.CaseStatus,
            item.LegalFirstName,
            item.LegalLastName,
            item.DateOfBirth,
            TelehealthProspectiveApplicantPolicy.MaskEmail(item.Email),
            TelehealthProspectiveApplicantPolicy.MaskPhone(item.Phone),
            item.ResidenceStateCode,
            item.PostalCode,
            item.PurposeCategory,
            item.PurposeDisplayLabel,
            item.SafetyOutcome,
            item.ReviewRoute,
            TelehealthApplicantPracticeReviewInboxPolicy.Sections(
                    item.InterpreterRequested,
                    item.AccessibilitySupportRequested,
                    item.ClinicalInformationSummaryRoute)
                .Select(section => new TelehealthApplicantPracticeReviewInboxSectionResponse(
                    section.SectionKey,
                    section.ReceiptState,
                    section.OutstandingRoute))
                .ToArray(),
            item.SubmittedAt,
            StaffReviewWorkItemExists: true,
            StaffActionTaken: assigned,
            Assigned: assigned,
            AssignedToCurrentUser: assigned
                && string.Equals(item.ActiveClaimActorId, currentActorId, StringComparison.Ordinal),
            AssignmentExpiresAt: item.ActiveClaimExpiresAt,
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
            ExternalCallPerformed: false);
    }

    private static string[] Limitations() =>
    [
        "Synthetic operational awareness; the only available action is a short review claim with no priority or response-time promise.",
        "The work item is not a telehealth request, doctor search, patient or clinician care queue, appointment, encounter, or care authorization.",
        "No patient contact, clinical review, acceptance, prescribing, billing, claim, integration, or external action is available."
    ];
}
