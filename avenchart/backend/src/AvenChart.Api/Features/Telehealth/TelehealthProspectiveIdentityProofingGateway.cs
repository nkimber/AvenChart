// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectiveIdentityProofingInquiry(
    Guid ApplicantReference,
    string PracticeId,
    int FacilityId,
    string CurrentLocationStateCode,
    string ProofingProfile,
    string PrivacyNoticeKey,
    int PrivacyNoticeVersion,
    string EvidencePackageReference,
    DateTimeOffset CheckedAt);

public sealed record TelehealthProspectiveIdentityProofingAdapterResult(
    string AdapterMode,
    string CompatibilityTarget,
    string PracticeStatementKey,
    int PracticeStatementVersion,
    string DatasetKey,
    int DatasetVersion,
    DateTimeOffset DatasetEffectiveFrom,
    DateTimeOffset DatasetEffectiveThrough,
    DateTimeOffset SourceLastUpdatedAt,
    Guid RequestTraceToken,
    Guid ResponseTraceToken,
    string ProofingMethod,
    string TransportOutcome,
    string EvidenceCollectionStatus,
    string EvidenceValidationStatus,
    string AttributeValidationStatus,
    string ApplicantVerificationStatus,
    string FraudCheckStatus,
    string BusinessOutcome,
    string ProofingSessionReference,
    string EvidencePackageReference,
    DateTimeOffset CheckedAt,
    DateTimeOffset ExpiresAt);

public interface ITelehealthProspectiveIdentityProofingGateway
{
    ValueTask<TelehealthProspectiveIdentityProofingAdapterResult> CheckAsync(
        TelehealthProspectiveIdentityProofingInquiry inquiry,
        CancellationToken cancellationToken);
}

public sealed class SyntheticTelehealthProspectiveIdentityProofingGateway
    : ITelehealthProspectiveIdentityProofingGateway
{
    public const string AdapterMode = "NON_PRODUCTION";
    public const string CompatibilityTarget = "NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY";
    public const string ProofingProfile = "SYNTHETIC_REMOTE_UNATTENDED_NON_BIOMETRIC";
    public const string PrivacyNoticeKey = "SYNTHETIC_IDENTITY_PROOFING_NOTICE";
    public const int PrivacyNoticeVersion = 1;
    public const string PracticeStatementKey = "SYNTHETIC_IDENTITY_PRACTICE_STATEMENT";
    public const int PracticeStatementVersion = 1;
    public const string DatasetKey = "avenchart-synthetic-identity-proofing-2026-08";
    public const int DatasetVersion = 1;
    public const string PracticeId = "avenchart-synthetic-practice";
    public const int FacilityId = 10;

    public static readonly DateTimeOffset DatasetEffectiveFrom =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset DatasetEffectiveThrough =
        new(2026, 10, 31, 23, 59, 59, TimeSpan.Zero);
    public static readonly DateTimeOffset SourceLastUpdatedAt =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);

    public ValueTask<TelehealthProspectiveIdentityProofingAdapterResult> CheckAsync(
        TelehealthProspectiveIdentityProofingInquiry inquiry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireSupportedInquiry(inquiry);

        return ValueTask.FromResult(new TelehealthProspectiveIdentityProofingAdapterResult(
            AdapterMode,
            CompatibilityTarget,
            PracticeStatementKey,
            PracticeStatementVersion,
            DatasetKey,
            DatasetVersion,
            DatasetEffectiveFrom,
            DatasetEffectiveThrough,
            SourceLastUpdatedAt,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ProofingProfile,
            "SimulatedCompleted",
            "FixtureReferenceAccepted",
            "ValidatedFixture",
            "ValidatedFixture",
            "VerifiedFixture",
            "NoIndicatorFixture",
            "SyntheticProofingPassed",
            $"syn-proof-session-{Guid.NewGuid():N}",
            inquiry.EvidencePackageReference,
            inquiry.CheckedAt,
            inquiry.CheckedAt.AddMinutes(15)));
    }

    private static void RequireSupportedInquiry(TelehealthProspectiveIdentityProofingInquiry inquiry)
    {
        var valid = inquiry.ApplicantReference != Guid.Empty
            && inquiry.PracticeId == PracticeId
            && inquiry.FacilityId == FacilityId
            && inquiry.CurrentLocationStateCode is "GA" or "CA" or "FL"
            && inquiry.ProofingProfile == ProofingProfile
            && inquiry.PrivacyNoticeKey == PrivacyNoticeKey
            && inquiry.PrivacyNoticeVersion == PrivacyNoticeVersion
            && inquiry.EvidencePackageReference == $"syn-evidence-{inquiry.ApplicantReference:N}"
            && inquiry.CheckedAt >= DatasetEffectiveFrom
            && inquiry.CheckedAt <= DatasetEffectiveThrough;
        if (!valid)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_identity_proofing_dataset_unavailable",
                "The bounded synthetic identity-proofing fixture is unavailable for this inquiry.");
        }
    }
}
