// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectiveMemberInsuranceDetailsService(
    TelehealthProspectiveMemberInsuranceDetailsRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProspectiveMemberInsuranceDetailsResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthProspectiveMemberInsuranceDetailsRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthProspectiveMemberInsuranceDetailsPolicy.Normalize(
            request,
            DateOnly.FromDateTime(DateTime.UtcNow));
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-member-insurance-details-v1",
            applicantId,
            normalized.ExpectedVersion,
            TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask(normalized.MemberId),
            normalized.GroupNumber is null ? "none" : TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask(normalized.GroupNumber),
            normalized.SubscriberRelationship,
            normalized.DetailsConfirmed,
            normalized.SyntheticDataConfirmed);
        var result = await repository.RecordAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            fingerprint,
            cancellationToken);
        return ToResponse(result);
    }

    private static TelehealthProspectiveMemberInsuranceDetailsResponse ToResponse(
        TelehealthProspectiveMemberInsuranceDetailsRecord result) => new(
            DetailsId: result.DetailsId,
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            PlanKey: result.PlanKey,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            PracticeNetworkStatus: result.PracticeNetworkStatus,
            MemberIdMask: TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask(result.MemberIdLast4),
            GroupNumberMask: result.GroupNumberLast4 is null
                ? null
                : TelehealthProspectiveMemberInsuranceDetailsPolicy.Mask(result.GroupNumberLast4),
            SubscriberRelationship: result.SubscriberRelationship,
            CoveragePriority: result.CoveragePriority,
            ProtectionScheme: result.ProtectionScheme,
            ProtectionVersion: result.ProtectionVersion,
            RecordedAt: result.RecordedAt,
            MemberMatched: false,
            MemberEligibilityChecked: false,
            MemberBenefitsChecked: false,
            RenderingPhysicianNetworkChecked: false,
            CoverageVerified: false,
            ExactNetworkConfirmed: false,
            IdentityProofed: false,
            CanonicalPatientCreated: false,
            ChartLinked: false,
            PortalAccountCreated: false,
            ProspectiveIntakeCompleted: false,
            ConsentCreated: false,
            PracticeAccepted: false,
            CoverageRecordCreated: false,
            EstimateCreated: false,
            FinancialAcknowledgmentCreated: false,
            RequestCreated: false,
            QueueEnabled: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            CareEnabled: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            CommunicationEnabled: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: "The protected synthetic member-details receipt was recorded. Eligibility, benefits, and exact practice-and-physician network verification remain required.",
            Limitations:
            [
                "Only SYN-prefixed demonstration identifiers were accepted; this receipt is not a payer, clearinghouse, or X12 271 response.",
                "The raw normalized values are purpose-protected and are not returned. A mask is not member matching, active coverage, benefits, or exact network evidence.",
                "No canonical insurance, coverage, patient, chart, portal, consent, request, queue, appointment, encounter, estimate, payment, prescribing, billing, claim, communication, integration, external action, or care capability was created."
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
