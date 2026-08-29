// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record SyntheticTelehealthParticipationContext(
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
    string AuthorityKind,
    string AuthorityFixtureStatus,
    string RoleFixtureStatus,
    string AffiliationFixtureStatus,
    string ContractFixtureStatus,
    string ServiceCategory,
    string Modality,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveThrough);

public sealed record TelehealthApplicantRequestParticipationContextSnapshot(
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    string CandidateDisplayName,
    string MaskedProviderReference,
    string MaskedBillingProviderReference,
    string ContextPurpose,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveThrough,
    DateTimeOffset ContextExpiresAt,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestParticipationContextCommand(
    int ExpectedRequestVersion,
    string ContextSnapshotFingerprint,
    bool SyntheticDataConfirmed,
    bool NpiNotCredentialAcknowledged,
    bool RealAuthorityNotVerifiedAcknowledged,
    bool ExactParticipationStillRequiredAcknowledged);

public static class TelehealthApplicantRequestParticipationContextPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_CONTEXT";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_PARTICIPATION_CONTEXT";
    public const string ContextPurpose = "PARTICIPATION_EVALUATION_PREREQUISITES_ONLY";
    public const string CatalogKey = "avenchart-synthetic-participation-context-2026-08";
    public const int CatalogVersion = 1;
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string RequestStatus = "Verification";
    public const int EntryRequestVersion = 9;
    public const int ResultingRequestVersion = 10;

    private static readonly DateTimeOffset CatalogEffectiveFrom =
        DateTimeOffset.Parse("2026-08-29T00:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset CatalogEffectiveThrough =
        DateTimeOffset.Parse("2026-10-31T23:59:59Z", CultureInfo.InvariantCulture);

    public static NormalizedTelehealthApplicantRequestParticipationContextCommand Normalize(
        ConfirmTelehealthApplicantRequestParticipationContext request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_participation_context_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.ContextSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_participation_context_snapshot_invalid",
                "Reload the participation-context step before continuing.");
        }

        if (!request.SyntheticDataConfirmed
            || !request.NpiNotCredentialAcknowledged
            || !request.RealAuthorityNotVerifiedAcknowledged
            || !request.ExactParticipationStillRequiredAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_participation_context_acknowledgments_required",
                "Confirm the synthetic, NPI limitation, real-authority, and exact-participation statements before continuing.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            request.SyntheticDataConfirmed,
            request.NpiNotCredentialAcknowledged,
            request.RealAuthorityNotVerifiedAcknowledged,
            request.ExactParticipationStillRequiredAcknowledged);
    }

    public static SyntheticTelehealthParticipationContext ResolveContext(string stateCode) => stateCode switch
    {
        "GA" => Create("GA", 101, "18888101", "syn-practitioner-ga-101", "syn-authority-ga-101"),
        "CA" => Create("CA", 104, "18888104", "syn-practitioner-ca-104", "syn-authority-ca-104"),
        "FL" => Create("FL", 107, "18888107", "syn-practitioner-fl-107", "syn-authority-fl-107"),
        _ => throw TelehealthProblem.Conflict(
            "telehealth_applicant_request_participation_context_state_unsupported",
            "No bounded synthetic participation context exists for this state.")
    };

    public static TelehealthApplicantRequestParticipationContextSnapshot Snapshot(
        Guid applicantId,
        Guid requestId,
        Guid eligibilityVerificationId,
        Guid practiceNetworkVerificationId,
        Guid candidateSelectionId,
        int requestVersion,
        string canonicalPatientId,
        string practiceId,
        int facilityId,
        string practiceDisplayName,
        string planKey,
        string payerDisplayName,
        string productDisplayName,
        string networkReference,
        string organizationReference,
        string locationReference,
        string serviceReference,
        string currentLocationStateCode,
        string purposeCategory,
        DateOnly dateOfService,
        int candidateStaffId,
        string candidateDisplayName,
        string candidateNpi,
        SyntheticTelehealthParticipationContext context,
        DateTimeOffset candidateSelectedAt,
        DateTimeOffset candidateContextExpiresAt,
        DateTimeOffset applicantExpiresAt)
    {
        var contextExpiresAt = new[]
        {
            candidateContextExpiresAt,
            applicantExpiresAt,
            context.EffectiveThrough
        }.Min();
        return new(
            practiceDisplayName,
            payerDisplayName,
            productDisplayName,
            currentLocationStateCode,
            purposeCategory,
            candidateDisplayName,
            $"Synthetic provider ••••{candidateNpi[^4..]}",
            "Synthetic billing provider ••••8800",
            ContextPurpose,
            context.EffectiveFrom,
            context.EffectiveThrough,
            contextExpiresAt,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-participation-context-v1",
                applicantId,
                requestId,
                eligibilityVerificationId,
                practiceNetworkVerificationId,
                candidateSelectionId,
                requestVersion,
                canonicalPatientId,
                practiceId,
                facilityId,
                practiceDisplayName,
                planKey,
                payerDisplayName,
                productDisplayName,
                networkReference,
                organizationReference,
                locationReference,
                serviceReference,
                currentLocationStateCode,
                purposeCategory,
                dateOfService.ToString("O", CultureInfo.InvariantCulture),
                candidateStaffId,
                candidateDisplayName,
                candidateNpi,
                context.PractitionerReference,
                context.StateAuthorityReference,
                context.BillingOrganizationReference,
                context.BillingProviderReference,
                context.PractitionerRoleReference,
                context.OrganizationAffiliationReference,
                context.ContractReference,
                context.AuthorityKind,
                context.AuthorityFixtureStatus,
                context.RoleFixtureStatus,
                context.AffiliationFixtureStatus,
                context.ContractFixtureStatus,
                context.ServiceCategory,
                context.Modality,
                CatalogKey,
                CatalogVersion,
                context.EffectiveFrom.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                context.EffectiveThrough.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                candidateSelectedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                candidateContextExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                contextExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
    }

    private static SyntheticTelehealthParticipationContext Create(
        string stateCode,
        int staffId,
        string expectedSyntheticNpi,
        string practitionerReference,
        string stateAuthorityReference) => new(
            stateCode,
            staffId,
            expectedSyntheticNpi,
            practitionerReference,
            stateAuthorityReference,
            "syn-org-avenchart-practice",
            "syn-billing-provider-avenchart-8800",
            $"syn-practitioner-role-{stateCode.ToLowerInvariant()}-{staffId}",
            $"syn-org-affiliation-harbor-{stateCode.ToLowerInvariant()}",
            $"syn-contract-harbor-telehealth-{stateCode.ToLowerInvariant()}",
            "PHYSICIAN_PRACTICE_AUTHORITY",
            "SYNTHETIC_ACTIVE",
            "SYNTHETIC_ACTIVE",
            "SYNTHETIC_ACTIVE",
            "SYNTHETIC_ACTIVE",
            SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceCategory,
            "RealTimeAudioVideo",
            CatalogEffectiveFrom,
            CatalogEffectiveThrough);
}
