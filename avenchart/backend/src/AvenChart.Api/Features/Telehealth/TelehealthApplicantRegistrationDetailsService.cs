// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRegistrationDetailsService(
    TelehealthApplicantRegistrationDetailsRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRegistrationDetailsResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var context = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        return ToResponse(
            context.ApplicantId,
            context.ApplicantVersion,
            context.ApplicantStatus,
            TelehealthApplicantRegistrationDetailsRepository.Snapshot(context),
            context.ConfirmationId is not null,
            context.ConfirmedAt);
    }

    public async Task<TelehealthApplicantRegistrationDetailsResponse> ConfirmAsync(
        HttpContext httpContext,
        Guid applicantId,
        ConfirmTelehealthApplicantRegistrationDetailsRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var accessKeyHash = TelehealthProspectiveApplicantPolicy.Hash(key);
        var current = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            cancellationToken);
        var snapshot = TelehealthApplicantRegistrationDetailsRepository.Snapshot(current);
        var normalized = TelehealthApplicantRegistrationDetailsPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-minimum-registration-details-confirmation-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.DetailsFingerprint,
            normalized.LegalNameAndBirthDateConfirmed,
            normalized.ContactChannelsConfirmed,
            normalized.ResidenceRegionConfirmed,
            normalized.NoCorrectionsNeededConfirmed,
            normalized.SyntheticDataConfirmed);
        var recorded = await repository.ConfirmAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            normalized,
            semanticKey,
            fingerprint,
            cancellationToken);
        return ToResponse(
            recorded.ApplicantId,
            recorded.ApplicantVersion,
            recorded.ApplicantStatus,
            snapshot,
            true,
            recorded.ConfirmedAt);
    }

    private static TelehealthApplicantRegistrationDetailsResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        TelehealthApplicantRegistrationDetailsSnapshot snapshot,
        bool confirmed,
        DateTimeOffset? confirmedAt) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            LegalFirstName: snapshot.LegalFirstName,
            LegalLastName: snapshot.LegalLastName,
            DateOfBirth: snapshot.DateOfBirth,
            MaskedEmail: snapshot.MaskedEmail,
            MaskedPhone: snapshot.MaskedPhone,
            ResidenceStateCode: snapshot.ResidenceStateCode,
            PostalCode: snapshot.PostalCode,
            DetailsFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantRegistrationDetailsPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRegistrationDetailsPolicy.PolicyVersion,
            Confirmed: confirmed,
            ConfirmedAt: confirmedAt,
            IdentityAssuranceEstablished: false,
            PatientRecordChanged: false,
            CorrectionCompleted: false,
            IntakeCompleted: false,
            LegalConsentEstablished: false,
            PracticeAccepted: false,
            InsuranceConfirmed: false,
            CoverageCreated: false,
            RequestCreated: false,
            QueueEnabled: false,
            CareEnabled: false,
            Direction: confirmed
                ? "The no-edit minimum registration-details confirmation was recorded. Complete demographics, history, consent, insurance confirmation, practice acceptance, and care gates remain unavailable."
                : "Review the copied minimum registration details. If anything is wrong, do not confirm; restart this synthetic intake or contact the practice.",
            Limitations:
            [
                "Synthetic demonstration only; no real identity assurance or patient authentication was established.",
                "This review contains no street address, complete demographics, allergies, medications, history, or insurance confirmation.",
                "Confirmation cannot edit the applicant or patient shell and creates no portal, request, queue entry, appointment, encounter, or care capability."
            ]);

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
