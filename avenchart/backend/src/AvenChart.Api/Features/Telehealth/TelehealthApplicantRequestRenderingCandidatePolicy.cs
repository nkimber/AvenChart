// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record SyntheticTelehealthRenderingCandidate(
    string StateCode,
    int StaffId,
    string ExpectedSyntheticNpi,
    string PractitionerReference,
    string StateAuthorityReference,
    string ServiceCategory,
    string Modality,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveThrough);

public sealed record TelehealthApplicantRequestRenderingCandidateSnapshot(
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    string CandidateDisplayName,
    string MaskedProviderReference,
    string CandidatePurpose,
    DateTimeOffset PracticeNetworkCheckedAt,
    DateTimeOffset PracticeNetworkExpiresAt,
    DateTimeOffset ContextExpiresAt,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestRenderingCandidateCommand(
    int ExpectedRequestVersion,
    string CandidateSnapshotFingerprint,
    bool SyntheticDataConfirmed,
    bool CandidateOnlyScopeAcknowledged,
    bool NoAssignmentAcknowledged,
    bool NetworkCheckStillRequiredAcknowledged);

public static class TelehealthApplicantRequestRenderingCandidatePolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_RENDERING_CANDIDATE_SELECTION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_RENDERING_CANDIDATE_SELECTION";
    public const string CandidatePurpose = "NETWORK_EVALUATION_ONLY";
    public const string CatalogKey = "avenchart-synthetic-rendering-candidate-roster-2026-08";
    public const int CatalogVersion = 1;
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string RequestStatus = "Verification";
    public const int EntryRequestVersion = 8;
    public const int ResultingRequestVersion = 9;

    private static readonly DateTimeOffset CatalogEffectiveFrom =
        DateTimeOffset.Parse("2026-08-29T00:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset CatalogEffectiveThrough =
        DateTimeOffset.Parse("2026-10-31T23:59:59Z", CultureInfo.InvariantCulture);

    public static NormalizedTelehealthApplicantRequestRenderingCandidateCommand Normalize(
        SelectTelehealthApplicantRequestRenderingCandidate request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_rendering_candidate_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.CandidateSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_rendering_candidate_snapshot_invalid",
                "Reload the rendering-candidate step before continuing.");
        }

        if (!request.SyntheticDataConfirmed
            || !request.CandidateOnlyScopeAcknowledged
            || !request.NoAssignmentAcknowledged
            || !request.NetworkCheckStillRequiredAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_rendering_candidate_acknowledgments_required",
                "Confirm the synthetic, candidate-only, no-assignment, and network-check-required statements before continuing.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            request.SyntheticDataConfirmed,
            request.CandidateOnlyScopeAcknowledged,
            request.NoAssignmentAcknowledged,
            request.NetworkCheckStillRequiredAcknowledged);
    }

    public static SyntheticTelehealthRenderingCandidate ResolveCandidate(string stateCode) => stateCode switch
    {
        "GA" => Create("GA", 101, "18888101", "syn-practitioner-ga-101", "syn-authority-ga-101"),
        "CA" => Create("CA", 104, "18888104", "syn-practitioner-ca-104", "syn-authority-ca-104"),
        "FL" => Create("FL", 107, "18888107", "syn-practitioner-fl-107", "syn-authority-fl-107"),
        _ => throw TelehealthProblem.Conflict(
            "telehealth_applicant_request_rendering_candidate_state_unsupported",
            "No bounded synthetic rendering candidate exists for this state.")
    };

    public static TelehealthApplicantRequestRenderingCandidateSnapshot Snapshot(
        Guid applicantId,
        Guid requestId,
        Guid eligibilityVerificationId,
        Guid practiceNetworkVerificationId,
        int requestVersion,
        string canonicalPatientId,
        string practiceId,
        int facilityId,
        string practiceDisplayName,
        string planKey,
        string payerDisplayName,
        string productDisplayName,
        string networkReference,
        string currentLocationStateCode,
        string purposeCategory,
        int candidateStaffId,
        string candidateDisplayName,
        string candidateNpi,
        SyntheticTelehealthRenderingCandidate candidate,
        DateTimeOffset practiceNetworkCheckedAt,
        DateTimeOffset practiceNetworkExpiresAt,
        DateTimeOffset applicantExpiresAt)
    {
        var contextExpiresAt = new[]
        {
            practiceNetworkExpiresAt,
            applicantExpiresAt,
            candidate.EffectiveThrough
        }.Min();
        var maskedProviderReference = $"Synthetic provider ••••{candidateNpi[^4..]}";
        return new(
            practiceDisplayName,
            payerDisplayName,
            productDisplayName,
            currentLocationStateCode,
            purposeCategory,
            candidateDisplayName,
            maskedProviderReference,
            CandidatePurpose,
            practiceNetworkCheckedAt,
            practiceNetworkExpiresAt,
            contextExpiresAt,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-rendering-candidate-context-v1",
                applicantId,
                requestId,
                eligibilityVerificationId,
                practiceNetworkVerificationId,
                requestVersion,
                canonicalPatientId,
                practiceId,
                facilityId,
                practiceDisplayName,
                planKey,
                payerDisplayName,
                productDisplayName,
                networkReference,
                currentLocationStateCode,
                purposeCategory,
                candidateStaffId,
                candidateDisplayName,
                candidateNpi,
                candidate.PractitionerReference,
                candidate.StateAuthorityReference,
                candidate.ServiceCategory,
                candidate.Modality,
                CatalogKey,
                CatalogVersion,
                candidate.EffectiveFrom.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                candidate.EffectiveThrough.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                practiceNetworkCheckedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                practiceNetworkExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                contextExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
    }

    private static SyntheticTelehealthRenderingCandidate Create(
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
            SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceCategory,
            "RealTimeAudioVideo",
            CatalogEffectiveFrom,
            CatalogEffectiveThrough);
}
