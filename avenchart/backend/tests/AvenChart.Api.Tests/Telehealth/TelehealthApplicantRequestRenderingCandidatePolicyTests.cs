// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestRenderingCandidatePolicyTests
{
    [Fact]
    public void Normalizes_the_exact_candidate_only_command()
    {
        var normalized = TelehealthApplicantRequestRenderingCandidatePolicy.Normalize(Valid() with
        {
            CandidateSnapshotFingerprint = new string('A', 64)
        });

        Assert.Equal(8, normalized.ExpectedRequestVersion);
        Assert.Equal(new string('a', 64), normalized.CandidateSnapshotFingerprint);
        Assert.True(normalized.SyntheticDataConfirmed);
        Assert.True(normalized.CandidateOnlyScopeAcknowledged);
        Assert.True(normalized.NoAssignmentAcknowledged);
        Assert.True(normalized.NetworkCheckStillRequiredAcknowledged);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, false, false)]
    public void Rejects_missing_acknowledgments(
        bool synthetic,
        bool candidateOnly,
        bool noAssignment,
        bool networkStillRequired)
    {
        Assert.Equal(
            "telehealth_applicant_request_rendering_candidate_acknowledgments_required",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestRenderingCandidatePolicy.Normalize(Valid() with
                {
                    SyntheticDataConfirmed = synthetic,
                    CandidateOnlyScopeAcknowledged = candidateOnly,
                    NoAssignmentAcknowledged = noAssignment,
                    NetworkCheckStillRequiredAcknowledged = networkStillRequired
                })).Code);
    }

    [Theory]
    [InlineData(0, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "telehealth_applicant_request_rendering_candidate_version_invalid")]
    [InlineData(8, "short", "telehealth_applicant_request_rendering_candidate_snapshot_invalid")]
    [InlineData(8, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz",
        "telehealth_applicant_request_rendering_candidate_snapshot_invalid")]
    public void Rejects_invalid_version_or_snapshot(int version, string fingerprint, string expectedCode)
    {
        Assert.Equal(
            expectedCode,
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestRenderingCandidatePolicy.Normalize(Valid() with
                {
                    ExpectedRequestVersion = version,
                    CandidateSnapshotFingerprint = fingerprint
                })).Code);
    }

    [Theory]
    [InlineData("GA", 101, "18888101", "syn-practitioner-ga-101")]
    [InlineData("CA", 104, "18888104", "syn-practitioner-ca-104")]
    [InlineData("FL", 107, "18888107", "syn-practitioner-fl-107")]
    public void Resolves_only_the_fixed_state_candidate(
        string state,
        int staffId,
        string npi,
        string practitionerReference)
    {
        var candidate = TelehealthApplicantRequestRenderingCandidatePolicy.ResolveCandidate(state);

        Assert.Equal(staffId, candidate.StaffId);
        Assert.Equal(npi, candidate.ExpectedSyntheticNpi);
        Assert.Equal(practitionerReference, candidate.PractitionerReference);
        Assert.Equal("RealTimeAudioVideo", candidate.Modality);
    }

    [Fact]
    public void Rejects_a_state_outside_the_approved_launch_set()
    {
        Assert.Equal(
            "telehealth_applicant_request_rendering_candidate_state_unsupported",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestRenderingCandidatePolicy.ResolveCandidate("NY")).Code);
    }

    [Fact]
    public void Snapshot_is_stable_masked_and_bound_to_the_candidate_and_network_evidence()
    {
        var first = Snapshot();
        var replay = Snapshot();
        var changedProvider = Snapshot(candidateStaffId: 104, candidateNpi: "18888104");
        var changedNetworkEvidence = Snapshot(practiceNetworkVerificationId:
            Guid.Parse("48000000-0000-4000-8000-000000000099"));

        Assert.Equal(first, replay);
        Assert.Equal("Synthetic provider ••••8101", first.MaskedProviderReference);
        Assert.NotEqual(first.Fingerprint, changedProvider.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changedNetworkEvidence.Fingerprint);
        Assert.DoesNotContain("18888101", first.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Constants_preserve_a_candidate_only_same_status_advance()
    {
        Assert.Equal("NETWORK_EVALUATION_ONLY", TelehealthApplicantRequestRenderingCandidatePolicy.CandidatePurpose);
        Assert.Equal(8, TelehealthApplicantRequestRenderingCandidatePolicy.EntryRequestVersion);
        Assert.Equal(9, TelehealthApplicantRequestRenderingCandidatePolicy.ResultingRequestVersion);
        Assert.True(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.Verification));
    }

    private static SelectTelehealthApplicantRequestRenderingCandidate Valid() => new(
        8,
        new string('a', 64),
        true,
        true,
        true,
        true);

    private static TelehealthApplicantRequestRenderingCandidateSnapshot Snapshot(
        int candidateStaffId = 101,
        string candidateNpi = "18888101",
        Guid? practiceNetworkVerificationId = null)
    {
        var candidate = TelehealthApplicantRequestRenderingCandidatePolicy.ResolveCandidate("GA");
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");
        return TelehealthApplicantRequestRenderingCandidatePolicy.Snapshot(
            Guid.Parse("48000000-0000-4000-8000-000000000001"),
            Guid.Parse("48000000-0000-4000-8000-000000000002"),
            Guid.Parse("48000000-0000-4000-8000-000000000003"),
            practiceNetworkVerificationId ?? Guid.Parse("48000000-0000-4000-8000-000000000004"),
            8,
            "synthetic-patient-48",
            "avenchart-synthetic-practice",
            10,
            "AvenChart Synthetic Practice",
            "harbor-mutual-hd",
            "Harbor Mutual",
            "High Deductible",
            "syn-network-harbor-mutual-hd",
            "GA",
            "migraine",
            candidateStaffId,
            "Alex Walker",
            candidateNpi,
            candidate,
            now.AddMinutes(-2),
            now.AddMinutes(10),
            now.AddMinutes(30));
    }
}
