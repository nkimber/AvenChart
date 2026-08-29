// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestParticipationContextPolicyTests
{
    [Fact]
    public void Normalizes_the_exact_context_only_command()
    {
        var normalized = TelehealthApplicantRequestParticipationContextPolicy.Normalize(Valid() with
        {
            ContextSnapshotFingerprint = new string('A', 64)
        });

        Assert.Equal(9, normalized.ExpectedRequestVersion);
        Assert.Equal(new string('a', 64), normalized.ContextSnapshotFingerprint);
        Assert.True(normalized.SyntheticDataConfirmed);
        Assert.True(normalized.NpiNotCredentialAcknowledged);
        Assert.True(normalized.RealAuthorityNotVerifiedAcknowledged);
        Assert.True(normalized.ExactParticipationStillRequiredAcknowledged);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, false, false)]
    public void Rejects_missing_acknowledgments(
        bool synthetic,
        bool npiLimitation,
        bool realAuthority,
        bool exactParticipation)
    {
        Assert.Equal(
            "telehealth_applicant_request_participation_context_acknowledgments_required",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestParticipationContextPolicy.Normalize(Valid() with
                {
                    SyntheticDataConfirmed = synthetic,
                    NpiNotCredentialAcknowledged = npiLimitation,
                    RealAuthorityNotVerifiedAcknowledged = realAuthority,
                    ExactParticipationStillRequiredAcknowledged = exactParticipation
                })).Code);
    }

    [Theory]
    [InlineData(0, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "telehealth_applicant_request_participation_context_version_invalid")]
    [InlineData(9, "short", "telehealth_applicant_request_participation_context_snapshot_invalid")]
    [InlineData(9, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz",
        "telehealth_applicant_request_participation_context_snapshot_invalid")]
    public void Rejects_invalid_version_or_snapshot(int version, string fingerprint, string expectedCode)
    {
        Assert.Equal(
            expectedCode,
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestParticipationContextPolicy.Normalize(Valid() with
                {
                    ExpectedRequestVersion = version,
                    ContextSnapshotFingerprint = fingerprint
                })).Code);
    }

    [Theory]
    [InlineData("GA", 101, "18888101", "syn-practitioner-role-ga-101", "syn-org-affiliation-harbor-ga")]
    [InlineData("CA", 104, "18888104", "syn-practitioner-role-ca-104", "syn-org-affiliation-harbor-ca")]
    [InlineData("FL", 107, "18888107", "syn-practitioner-role-fl-107", "syn-org-affiliation-harbor-fl")]
    public void Resolves_only_the_fixed_state_context(
        string state,
        int staffId,
        string npi,
        string practitionerRoleReference,
        string affiliationReference)
    {
        var context = TelehealthApplicantRequestParticipationContextPolicy.ResolveContext(state);

        Assert.Equal(staffId, context.ExpectedStaffId);
        Assert.Equal(npi, context.ExpectedSyntheticNpi);
        Assert.Equal(practitionerRoleReference, context.PractitionerRoleReference);
        Assert.Equal(affiliationReference, context.OrganizationAffiliationReference);
        Assert.Equal("SYNTHETIC_ACTIVE", context.ContractFixtureStatus);
        Assert.Equal("RealTimeAudioVideo", context.Modality);
    }

    [Fact]
    public void Rejects_a_state_outside_the_approved_launch_set()
    {
        Assert.Equal(
            "telehealth_applicant_request_participation_context_state_unsupported",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestParticipationContextPolicy.ResolveContext("NY")).Code);
    }

    [Fact]
    public void Snapshot_is_stable_masked_and_bound_to_candidate_and_network_provenance()
    {
        var first = Snapshot();
        var replay = Snapshot();
        var changedCandidate = Snapshot(candidateSelectionId:
            Guid.Parse("49000000-0000-4000-8000-000000000099"));
        var changedNetwork = Snapshot(networkReference: "syn-network-other");

        Assert.Equal(first, replay);
        Assert.Equal("Synthetic provider ••••8101", first.MaskedProviderReference);
        Assert.Equal("Synthetic billing provider ••••8800", first.MaskedBillingProviderReference);
        Assert.NotEqual(first.Fingerprint, changedCandidate.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changedNetwork.Fingerprint);
        Assert.DoesNotContain("18888101", first.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Constants_preserve_a_context_only_same_status_advance()
    {
        Assert.Equal(
            "PARTICIPATION_EVALUATION_PREREQUISITES_ONLY",
            TelehealthApplicantRequestParticipationContextPolicy.ContextPurpose);
        Assert.Equal(9, TelehealthApplicantRequestParticipationContextPolicy.EntryRequestVersion);
        Assert.Equal(10, TelehealthApplicantRequestParticipationContextPolicy.ResultingRequestVersion);
        Assert.True(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.Verification));
    }

    private static ConfirmTelehealthApplicantRequestParticipationContext Valid() => new(
        9,
        new string('a', 64),
        true,
        true,
        true,
        true);

    private static TelehealthApplicantRequestParticipationContextSnapshot Snapshot(
        Guid? candidateSelectionId = null,
        string networkReference = "syn-network-harbor-mutual-hd")
    {
        var context = TelehealthApplicantRequestParticipationContextPolicy.ResolveContext("GA");
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");
        return TelehealthApplicantRequestParticipationContextPolicy.Snapshot(
            Guid.Parse("49000000-0000-4000-8000-000000000001"),
            Guid.Parse("49000000-0000-4000-8000-000000000002"),
            Guid.Parse("49000000-0000-4000-8000-000000000003"),
            Guid.Parse("49000000-0000-4000-8000-000000000004"),
            candidateSelectionId ?? Guid.Parse("49000000-0000-4000-8000-000000000005"),
            9,
            "synthetic-patient-49",
            "avenchart-synthetic-practice",
            10,
            "AvenChart Synthetic Practice",
            "harbor-mutual-hd",
            "Harbor Mutual",
            "High Deductible",
            networkReference,
            "syn-org-avenchart-practice",
            "syn-location-main-telehealth",
            "syn-service-professional-telehealth",
            "GA",
            "migraine",
            new DateOnly(2026, 8, 29),
            101,
            "Alex Walker",
            "18888101",
            context,
            now.AddMinutes(-1),
            now.AddMinutes(9),
            now.AddMinutes(20));
    }
}
