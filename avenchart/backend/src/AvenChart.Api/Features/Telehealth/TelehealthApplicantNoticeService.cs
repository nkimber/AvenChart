// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantNoticeService(
    TelehealthApplicantNoticeRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantNoticeResponse> GetAsync(
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
            TelehealthApplicantNoticePolicy.ForState(context.CurrentLocationStateCode),
            context.AcknowledgmentId is not null,
            context.AcknowledgedAt);
    }

    public async Task<TelehealthApplicantNoticeResponse> AcknowledgeAsync(
        HttpContext httpContext,
        Guid applicantId,
        AcknowledgeTelehealthApplicantNoticeRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var accessKeyHash = TelehealthProspectiveApplicantPolicy.Hash(key);
        var current = await repository.GetAuthorizedAsync(
            _options.PracticeId, _options.FacilityId, applicantId,
            accessKeyHash, cancellationToken);
        var notice = TelehealthApplicantNoticePolicy.ForState(current.CurrentLocationStateCode);
        var normalized = TelehealthApplicantNoticePolicy.Normalize(request, notice);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-telehealth-notice-acknowledgment-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.NoticeKey,
            normalized.NoticeVersion,
            normalized.CurrentLocationStateCode,
            normalized.CurrentLocationConfirmed,
            normalized.ModeOfCareAcknowledged,
            normalized.PrivacyLimitationsAcknowledged,
            normalized.EmergencyInstructionsAcknowledged,
            normalized.InPersonOptionAcknowledged,
            normalized.ClinicianReconfirmationRequiredAcknowledged,
            normalized.SyntheticDataConfirmed);
        var recorded = await repository.AcknowledgeAsync(
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
            notice,
            true,
            recorded.AcknowledgedAt);
    }

    private static TelehealthApplicantNoticeResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        TelehealthApplicantNoticeDefinition notice,
        bool acknowledged,
        DateTimeOffset? acknowledgedAt) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            NoticeKey: notice.NoticeKey,
            NoticeVersion: notice.NoticeVersion,
            CurrentLocationStateCode: notice.StateCode,
            Title: notice.Title,
            Summary: notice.Summary,
            SourceTitle: notice.SourceTitle,
            SourceUrl: notice.SourceUrl,
            Disclosures: notice.Disclosures,
            DeferredRequirements: notice.DeferredRequirements,
            PolicyKey: TelehealthApplicantNoticePolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantNoticePolicy.PolicyVersion,
            LegalReviewStatus: TelehealthApplicantNoticePolicy.LegalReviewStatus,
            Acknowledged: acknowledged,
            AcknowledgedAt: acknowledgedAt,
            LegalConsentEstablished: false,
            ClinicianConsentDocumented: false,
            ClinicianReconfirmationRequired: true,
            PortalAccountCreated: false,
            IntakeCompleted: false,
            PracticeAccepted: false,
            InsuranceCreated: false,
            RequestCreated: false,
            QueueEnabled: false,
            CareEnabled: false,
            Direction: acknowledged
                ? "The synthetic state-notice acknowledgment was recorded. A clinician must still provide required disclosures and obtain/document any legally effective consent before care."
                : "Review the synthetic state notice and confirm every item. This does not establish legal consent or contact a clinician.",
            Limitations:
            [
                "Synthetic demonstration only; this fixture has not passed independent state legal or clinical review.",
                "This acknowledgment is not a signature, final informed consent, practice acceptance, or authorization for care.",
                "No portal, complete intake, insurance/coverage record, request, queue entry, appointment, encounter, or care capability is created."
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
