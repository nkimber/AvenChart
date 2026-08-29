// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record SyntheticTelehealthParticipationEvaluationRule(
    string StateCode,
    int ExpectedStaffId,
    string ExpectedSyntheticNpi,
    string PractitionerReference,
    string StateAuthorityReference,
    string BillingOrganizationReference,
    string BillingProviderReference,
    string PractitionerRoleReference,
    string OrganizationAffiliationReference,
    string ContractReference,
    string NetworkReference,
    string OrganizationReference,
    string LocationReference,
    string ServiceReference,
    string ServiceCategory,
    string Modality,
    string SourceMode,
    string CompatibilityTarget,
    string EvaluationScope,
    string BusinessOutcome,
    bool SyntheticBillingEntityInNetwork,
    bool SyntheticRenderingProviderInNetwork,
    bool SyntheticPlanNetworkMatched,
    bool SyntheticServiceLocationMatched,
    bool SyntheticNewPatientsAccepted,
    bool SyntheticExactNetworkMatched,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveThrough);

public sealed record TelehealthApplicantRequestParticipationEvaluationSnapshot(
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    DateOnly DateOfService,
    string CandidateDisplayName,
    string MaskedProviderReference,
    string MaskedBillingProviderReference,
    string ServiceCategory,
    string Modality,
    string EvaluationScope,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveThrough,
    DateTimeOffset ResultValidThrough,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestParticipationEvaluationCommand(
    int ExpectedRequestVersion,
    string EvaluationSnapshotFingerprint,
    bool SyntheticDataConfirmed,
    bool ExactTupleScopeAcknowledged,
    bool NoCoverageGuaranteeAcknowledged,
    bool RealVerificationStillRequiredAcknowledged);

public static class TelehealthApplicantRequestParticipationEvaluationPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_EVALUATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_PARTICIPATION_EVALUATION";
    public const string CatalogKey = "avenchart-synthetic-participation-evaluation-2026-08";
    public const int CatalogVersion = 1;
    public const string SourceMode = "NON_PRODUCTION";
    public const string CompatibilityTarget = "HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0";
    public const string EvaluationScope =
        "BILLING_ENTITY_RENDERING_PROVIDER_NETWORK_LOCATION_SERVICE_MODALITY_NEW_PATIENT";
    public const string BusinessOutcome = "SyntheticExactParticipationMatched";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string RequestStatus = "Verification";
    public const int EntryRequestVersion = 10;
    public const int ResultingRequestVersion = 11;

    private static readonly DateTimeOffset CatalogEffectiveFrom =
        DateTimeOffset.Parse("2026-08-29T00:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset CatalogEffectiveThrough =
        DateTimeOffset.Parse("2026-10-31T23:59:59Z", CultureInfo.InvariantCulture);

    public static NormalizedTelehealthApplicantRequestParticipationEvaluationCommand Normalize(
        EvaluateTelehealthApplicantRequestParticipation request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_participation_evaluation_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.EvaluationSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_participation_evaluation_snapshot_invalid",
                "Reload the participation-evaluation step before continuing.");
        }

        if (!request.SyntheticDataConfirmed
            || !request.ExactTupleScopeAcknowledged
            || !request.NoCoverageGuaranteeAcknowledged
            || !request.RealVerificationStillRequiredAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_participation_evaluation_acknowledgments_required",
                "Confirm the synthetic, exact-tuple, no-guarantee, and real-verification statements before continuing.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            request.SyntheticDataConfirmed,
            request.ExactTupleScopeAcknowledged,
            request.NoCoverageGuaranteeAcknowledged,
            request.RealVerificationStillRequiredAcknowledged);
    }

    public static SyntheticTelehealthParticipationEvaluationRule ResolveRule(string stateCode)
    {
        var context = TelehealthApplicantRequestParticipationContextPolicy.ResolveContext(stateCode);
        return new(
            context.StateCode,
            context.ExpectedStaffId,
            context.ExpectedSyntheticNpi,
            context.PractitionerReference,
            context.StateAuthorityReference,
            context.BillingOrganizationReference,
            context.BillingProviderReference,
            context.PractitionerRoleReference,
            context.OrganizationAffiliationReference,
            context.ContractReference,
            "syn-network-harbor-mutual-hd",
            "syn-org-avenchart-practice",
            "syn-location-main-telehealth",
            "syn-service-professional-telehealth",
            context.ServiceCategory,
            context.Modality,
            SourceMode,
            CompatibilityTarget,
            EvaluationScope,
            BusinessOutcome,
            true,
            true,
            true,
            true,
            true,
            true,
            CatalogEffectiveFrom,
            CatalogEffectiveThrough);
    }

    public static TelehealthApplicantRequestParticipationEvaluationSnapshot Snapshot(
        Guid applicantId,
        Guid requestId,
        Guid eligibilityVerificationId,
        Guid practiceNetworkVerificationId,
        Guid candidateSelectionId,
        Guid participationContextConfirmationId,
        int requestVersion,
        string canonicalPatientId,
        string practiceId,
        int facilityId,
        string practiceDisplayName,
        string planKey,
        string payerDisplayName,
        string productDisplayName,
        string currentLocationStateCode,
        string purposeCategory,
        DateOnly dateOfService,
        int candidateStaffId,
        string candidateDisplayName,
        string candidateNpi,
        string participationContextSnapshotFingerprint,
        DateTimeOffset contextConfirmedAt,
        DateTimeOffset contextExpiresAt,
        DateTimeOffset applicantExpiresAt,
        SyntheticTelehealthParticipationEvaluationRule rule)
    {
        var resultValidThrough = new[]
        {
            contextExpiresAt,
            applicantExpiresAt,
            rule.EffectiveThrough
        }.Min();
        return new(
            practiceDisplayName,
            payerDisplayName,
            productDisplayName,
            currentLocationStateCode,
            purposeCategory,
            dateOfService,
            candidateDisplayName,
            $"Synthetic provider ••••{candidateNpi[^4..]}",
            "Synthetic billing provider ••••8800",
            rule.ServiceCategory,
            rule.Modality,
            rule.EvaluationScope,
            rule.EffectiveFrom,
            rule.EffectiveThrough,
            resultValidThrough,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-participation-evaluation-v1",
                applicantId,
                requestId,
                eligibilityVerificationId,
                practiceNetworkVerificationId,
                candidateSelectionId,
                participationContextConfirmationId,
                requestVersion,
                canonicalPatientId,
                practiceId,
                facilityId,
                practiceDisplayName,
                planKey,
                payerDisplayName,
                productDisplayName,
                currentLocationStateCode,
                purposeCategory,
                dateOfService.ToString("O", CultureInfo.InvariantCulture),
                candidateStaffId,
                candidateDisplayName,
                candidateNpi,
                participationContextSnapshotFingerprint,
                contextConfirmedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                contextExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                rule.PractitionerReference,
                rule.StateAuthorityReference,
                rule.BillingOrganizationReference,
                rule.BillingProviderReference,
                rule.PractitionerRoleReference,
                rule.OrganizationAffiliationReference,
                rule.ContractReference,
                rule.NetworkReference,
                rule.OrganizationReference,
                rule.LocationReference,
                rule.ServiceReference,
                rule.ServiceCategory,
                rule.Modality,
                rule.SourceMode,
                rule.CompatibilityTarget,
                rule.EvaluationScope,
                rule.BusinessOutcome,
                rule.SyntheticBillingEntityInNetwork,
                rule.SyntheticRenderingProviderInNetwork,
                rule.SyntheticPlanNetworkMatched,
                rule.SyntheticServiceLocationMatched,
                rule.SyntheticNewPatientsAccepted,
                rule.SyntheticExactNetworkMatched,
                CatalogKey,
                CatalogVersion,
                rule.EffectiveFrom.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                rule.EffectiveThrough.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                resultValidThrough.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
    }
}
