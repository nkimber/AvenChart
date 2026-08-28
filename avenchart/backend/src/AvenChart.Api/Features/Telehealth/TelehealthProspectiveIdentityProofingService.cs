// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectiveIdentityProofingService(
    TelehealthProspectiveIdentityProofingRepository repository,
    ITelehealthProspectiveIdentityProofingGateway gateway,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProspectiveIdentityProofingResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthProspectiveIdentityProofingRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "Expected applicant version must be positive.");
        }
        if (!request.PrivacyNoticeAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_identity_proofing_notice_required",
                "Acknowledge the synthetic identity-proofing privacy notice before continuing.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_identity_proofing_synthetic_acknowledgment_required",
                "Confirm that this is a NON_PRODUCTION synthetic identity-proofing exercise.");
        }

        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-synthetic-identity-proofing-v1",
            applicantId,
            request.ExpectedVersion,
            request.PrivacyNoticeAcknowledged,
            request.SyntheticDataConfirmed,
            SyntheticTelehealthProspectiveIdentityProofingGateway.PrivacyNoticeKey,
            SyntheticTelehealthProspectiveIdentityProofingGateway.PrivacyNoticeVersion);
        var result = await repository.RecordAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            request.ExpectedVersion,
            SyntheticTelehealthProspectiveIdentityProofingGateway.PrivacyNoticeKey,
            SyntheticTelehealthProspectiveIdentityProofingGateway.PrivacyNoticeVersion,
            request.PrivacyNoticeAcknowledged,
            semanticKey,
            fingerprint,
            ResolveAsync,
            cancellationToken);
        return ToResponse(result);
    }

    private async ValueTask<TelehealthProspectiveIdentityProofingAdapterResult> ResolveAsync(
        TelehealthProspectiveIdentityProofingCandidate candidate,
        CancellationToken cancellationToken)
    {
        var checkedAt = candidate.DatabaseNow.ToUniversalTime();
        var result = await gateway.CheckAsync(
            new(
                candidate.ApplicantId,
                _options.PracticeId,
                _options.FacilityId,
                candidate.CurrentLocationStateCode,
                SyntheticTelehealthProspectiveIdentityProofingGateway.ProofingProfile,
                SyntheticTelehealthProspectiveIdentityProofingGateway.PrivacyNoticeKey,
                SyntheticTelehealthProspectiveIdentityProofingGateway.PrivacyNoticeVersion,
                $"syn-evidence-{candidate.ApplicantId:N}",
                checkedAt),
            cancellationToken);
        RequireAdapterContract(result, candidate.ApplicantId, checkedAt);
        return result;
    }

    private static void RequireAdapterContract(
        TelehealthProspectiveIdentityProofingAdapterResult result,
        Guid applicantId,
        DateTimeOffset checkedAt)
    {
        var valid = result.AdapterMode == SyntheticTelehealthProspectiveIdentityProofingGateway.AdapterMode
            && result.CompatibilityTarget == SyntheticTelehealthProspectiveIdentityProofingGateway.CompatibilityTarget
            && result.PracticeStatementKey == SyntheticTelehealthProspectiveIdentityProofingGateway.PracticeStatementKey
            && result.PracticeStatementVersion == SyntheticTelehealthProspectiveIdentityProofingGateway.PracticeStatementVersion
            && result.DatasetKey == SyntheticTelehealthProspectiveIdentityProofingGateway.DatasetKey
            && result.DatasetVersion == SyntheticTelehealthProspectiveIdentityProofingGateway.DatasetVersion
            && result.DatasetEffectiveFrom == SyntheticTelehealthProspectiveIdentityProofingGateway.DatasetEffectiveFrom
            && result.DatasetEffectiveThrough == SyntheticTelehealthProspectiveIdentityProofingGateway.DatasetEffectiveThrough
            && result.SourceLastUpdatedAt == SyntheticTelehealthProspectiveIdentityProofingGateway.SourceLastUpdatedAt
            && result.RequestTraceToken != Guid.Empty
            && result.ResponseTraceToken != Guid.Empty
            && result.RequestTraceToken != result.ResponseTraceToken
            && result.ProofingMethod == SyntheticTelehealthProspectiveIdentityProofingGateway.ProofingProfile
            && result.TransportOutcome == "SimulatedCompleted"
            && result.EvidenceCollectionStatus == "FixtureReferenceAccepted"
            && result.EvidenceValidationStatus == "ValidatedFixture"
            && result.AttributeValidationStatus == "ValidatedFixture"
            && result.ApplicantVerificationStatus == "VerifiedFixture"
            && result.FraudCheckStatus == "NoIndicatorFixture"
            && result.BusinessOutcome == "SyntheticProofingPassed"
            && result.ProofingSessionReference.StartsWith("syn-proof-session-", StringComparison.Ordinal)
            && result.ProofingSessionReference.Length == 50
            && result.EvidencePackageReference == $"syn-evidence-{applicantId:N}"
            && result.CheckedAt == checkedAt
            && result.ExpiresAt == checkedAt.AddMinutes(15);
        if (!valid)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_identity_proofing_adapter_contract_invalid",
                "The bounded synthetic identity-proofing adapter returned an invalid result.");
        }
    }

    private static TelehealthProspectiveIdentityProofingResponse ToResponse(
        TelehealthProspectiveIdentityProofingRecord result)
    {
        var adapter = result.AdapterResult;
        return new(
            result.IdentityProofingResultId,
            result.ApplicantId,
            result.ApplicantVersion,
            result.ApplicantStatus,
            result.CurrentLocationStateCode,
            result.PlanKey,
            result.PrivacyNoticeKey,
            result.PrivacyNoticeVersion,
            result.PrivacyNoticeAcknowledged,
            adapter.AdapterMode,
            adapter.CompatibilityTarget,
            adapter.PracticeStatementKey,
            adapter.PracticeStatementVersion,
            adapter.DatasetKey,
            adapter.DatasetVersion,
            adapter.DatasetEffectiveFrom,
            adapter.DatasetEffectiveThrough,
            adapter.SourceLastUpdatedAt,
            adapter.RequestTraceToken,
            adapter.ResponseTraceToken,
            adapter.ProofingMethod,
            adapter.TransportOutcome,
            adapter.EvidenceCollectionStatus,
            adapter.EvidenceValidationStatus,
            adapter.AttributeValidationStatus,
            adapter.ApplicantVerificationStatus,
            adapter.FraudCheckStatus,
            adapter.BusinessOutcome,
            adapter.ProofingSessionReference,
            adapter.EvidencePackageReference,
            adapter.CheckedAt,
            adapter.ExpiresAt,
            result.RecordedAt,
            AssuranceLevelAchieved: "None",
            IdentityEvidenceCollected: false,
            GovernmentIdentifierCollected: false,
            BiometricDataCollected: false,
            AuthoritativeSourceQueried: false,
            ProofingNotificationSent: false,
            RedressCaseCreated: false,
            AuthenticatorBound: false,
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
            Direction: "The synthetic proofing-process fixture was recorded. No identity assurance level or real identity was established; patient promotion, consent, practice acceptance, request, queue, and care remain unavailable.",
            Limitations:
            [
                "NON_PRODUCTION fixture only. No identity provider, evidence issuer, authoritative source, government system, or external service was contacted.",
                "NIST SP 800-63A-4 concepts are represented only as normalized process metadata; this is not an IAL1, IAL2, IAL3, certification, validation, or conformance claim.",
                "No document, government identifier, image, video, biometric, raw evidence, notification, redress case, authenticator, patient, chart, portal account, request, queue entry, or care capability was created."
            ]);
    }

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
