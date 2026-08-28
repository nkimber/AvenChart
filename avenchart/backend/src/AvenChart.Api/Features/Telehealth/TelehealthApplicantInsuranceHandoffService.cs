// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantInsuranceHandoffService(
    TelehealthApplicantInsuranceHandoffRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantInsuranceHandoffResponse> GetAsync(
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
            TelehealthApplicantInsuranceHandoffRepository.Snapshot(context),
            context.DatabaseNow,
            context.ConfirmationId is not null,
            context.ConfirmedAt);
    }

    public async Task<TelehealthApplicantInsuranceHandoffResponse> ConfirmAsync(
        HttpContext httpContext,
        Guid applicantId,
        ConfirmTelehealthApplicantInsuranceHandoffRequest request,
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
        var snapshot = TelehealthApplicantInsuranceHandoffRepository.Snapshot(current);
        var normalized = TelehealthApplicantInsuranceHandoffPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-insurance-handoff-confirmation-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.InsuranceSnapshotFingerprint,
            normalized.PayerAndProductConfirmed,
            normalized.MaskedMemberDetailsConfirmed,
            normalized.SubscriberRelationshipConfirmed,
            normalized.EvidenceLimitationsAcknowledged,
            normalized.SyntheticDataConfirmed);
        var recorded = await repository.ConfirmAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            normalized,
            semanticKey,
            commandFingerprint,
            cancellationToken);
        return ToResponse(
            recorded.ApplicantId,
            recorded.ApplicantVersion,
            recorded.ApplicantStatus,
            snapshot,
            current.DatabaseNow,
            true,
            recorded.ConfirmedAt);
    }

    private static TelehealthApplicantInsuranceHandoffResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        TelehealthApplicantInsuranceHandoffSnapshot snapshot,
        DateTimeOffset databaseNow,
        bool confirmed,
        DateTimeOffset? confirmedAt) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            PayerDisplayName: snapshot.PayerDisplayName,
            ProductDisplayName: snapshot.ProductDisplayName,
            MemberIdMask: snapshot.MemberIdMask,
            GroupNumberMask: snapshot.GroupNumberMask,
            SubscriberRelationship: snapshot.SubscriberRelationship,
            CoveragePriority: snapshot.CoveragePriority,
            EligibilityBusinessOutcome: snapshot.EligibilityBusinessOutcome,
            EligibilityCheckedAt: snapshot.EligibilityCheckedAt,
            EligibilityExpiresAt: snapshot.EligibilityExpiresAt,
            EligibilityEvidenceCurrent: snapshot.EligibilityExpiresAt > databaseNow,
            PracticeNetworkBusinessOutcome: snapshot.PracticeNetworkBusinessOutcome,
            PracticeNetworkCheckedAt: snapshot.PracticeNetworkCheckedAt,
            PracticeNetworkExpiresAt: snapshot.PracticeNetworkExpiresAt,
            PracticeNetworkEvidenceCurrent: snapshot.PracticeNetworkExpiresAt > databaseNow,
            RenderingPhysicianNetworkChecked: snapshot.RenderingPhysicianNetworkChecked,
            InsuranceSnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantInsuranceHandoffPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantInsuranceHandoffPolicy.PolicyVersion,
            InsuranceDetailsConfirmed: confirmed,
            ConfirmedAt: confirmedAt,
            CoverageVerified: false,
            ExactNetworkConfirmed: false,
            CanonicalCoverageCreated: false,
            PatientRecordChanged: false,
            PortalAccessEnabled: false,
            IntakeCompleted: false,
            LegalConsentEstablished: false,
            PracticeAccepted: false,
            RequestCreated: false,
            QueueEnabled: false,
            CareEnabled: false,
            Direction: confirmed
                ? "The no-edit synthetic insurance-details handoff confirmation was recorded. Rendering-physician participation, coverage, payment, complete intake, consent, practice acceptance, request, queue, and care gates remain closed."
                : "Review the masked synthetic insurance handoff. If any payer, product, member, group, or subscriber relationship detail is wrong, do not confirm; restart this synthetic intake or contact the practice.",
            Limitations:
            [
                "Synthetic demonstration only; no payer, clearinghouse, provider directory, insurer, or rendering physician was contacted.",
                "An eligibility fixture and a practice-level network fixture are not guarantees of coverage, benefits, payment, patient responsibility, or rendering-physician participation.",
                "This confirmation creates no canonical insurance record, patient change, portal access, request, queue entry, appointment, encounter, billing record, claim, or care capability."
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
