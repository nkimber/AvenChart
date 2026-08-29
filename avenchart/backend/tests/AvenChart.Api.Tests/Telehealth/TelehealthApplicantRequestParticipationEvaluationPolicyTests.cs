// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestParticipationEvaluationPolicyTests
{
    [Fact]
    public void Normalizes_the_exact_server_owned_evaluation_command()
    {
        var normalized = TelehealthApplicantRequestParticipationEvaluationPolicy.Normalize(Valid() with
        {
            EvaluationSnapshotFingerprint = new string('A', 64)
        });

        Assert.Equal(10, normalized.ExpectedRequestVersion);
        Assert.Equal(new string('a', 64), normalized.EvaluationSnapshotFingerprint);
        Assert.True(normalized.SyntheticDataConfirmed);
        Assert.True(normalized.ExactTupleScopeAcknowledged);
        Assert.True(normalized.NoCoverageGuaranteeAcknowledged);
        Assert.True(normalized.RealVerificationStillRequiredAcknowledged);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, false, false)]
    public void Rejects_missing_acknowledgments(
        bool synthetic,
        bool exactTuple,
        bool noGuarantee,
        bool realVerification)
    {
        Assert.Equal(
            "telehealth_applicant_request_participation_evaluation_acknowledgments_required",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestParticipationEvaluationPolicy.Normalize(Valid() with
                {
                    SyntheticDataConfirmed = synthetic,
                    ExactTupleScopeAcknowledged = exactTuple,
                    NoCoverageGuaranteeAcknowledged = noGuarantee,
                    RealVerificationStillRequiredAcknowledged = realVerification
                })).Code);
    }

    [Theory]
    [InlineData(0, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "telehealth_applicant_request_participation_evaluation_version_invalid")]
    [InlineData(10, "short", "telehealth_applicant_request_participation_evaluation_snapshot_invalid")]
    [InlineData(10, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz",
        "telehealth_applicant_request_participation_evaluation_snapshot_invalid")]
    public void Rejects_invalid_version_or_snapshot(int version, string fingerprint, string expectedCode)
    {
        Assert.Equal(
            expectedCode,
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestParticipationEvaluationPolicy.Normalize(Valid() with
                {
                    ExpectedRequestVersion = version,
                    EvaluationSnapshotFingerprint = fingerprint
                })).Code);
    }

    [Theory]
    [InlineData("GA", 101, "18888101", "syn-practitioner-role-ga-101", "syn-org-affiliation-harbor-ga")]
    [InlineData("CA", 104, "18888104", "syn-practitioner-role-ca-104", "syn-org-affiliation-harbor-ca")]
    [InlineData("FL", 107, "18888107", "syn-practitioner-role-fl-107", "syn-org-affiliation-harbor-fl")]
    public void Resolves_only_the_fixed_exact_synthetic_tuple(
        string state,
        int staffId,
        string npi,
        string practitionerRoleReference,
        string affiliationReference)
    {
        var rule = TelehealthApplicantRequestParticipationEvaluationPolicy.ResolveRule(state);

        Assert.Equal(staffId, rule.ExpectedStaffId);
        Assert.Equal(npi, rule.ExpectedSyntheticNpi);
        Assert.Equal(practitionerRoleReference, rule.PractitionerRoleReference);
        Assert.Equal(affiliationReference, rule.OrganizationAffiliationReference);
        Assert.Equal("NON_PRODUCTION", rule.SourceMode);
        Assert.Equal("HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0", rule.CompatibilityTarget);
        Assert.Equal("SyntheticExactParticipationMatched", rule.BusinessOutcome);
        Assert.True(rule.SyntheticBillingEntityInNetwork);
        Assert.True(rule.SyntheticRenderingProviderInNetwork);
        Assert.True(rule.SyntheticPlanNetworkMatched);
        Assert.True(rule.SyntheticServiceLocationMatched);
        Assert.True(rule.SyntheticNewPatientsAccepted);
        Assert.True(rule.SyntheticExactNetworkMatched);
    }

    [Fact]
    public void Rejects_a_state_outside_the_approved_launch_set()
    {
        Assert.Equal(
            "telehealth_applicant_request_participation_context_state_unsupported",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestParticipationEvaluationPolicy.ResolveRule("NY")).Code);
    }

    [Fact]
    public void Snapshot_is_stable_masked_and_bound_to_context_and_network_provenance()
    {
        var first = Snapshot();
        var replay = Snapshot();
        var changedContext = Snapshot(participationContextConfirmationId:
            Guid.Parse("50000000-0000-4000-8000-000000000099"));
        var changedContextFingerprint = Snapshot(contextFingerprint: new string('b', 64));

        Assert.Equal(first, replay);
        Assert.Equal("Synthetic provider ••••8101", first.MaskedProviderReference);
        Assert.Equal("Synthetic billing provider ••••8800", first.MaskedBillingProviderReference);
        Assert.NotEqual(first.Fingerprint, changedContext.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changedContextFingerprint.Fingerprint);
        Assert.DoesNotContain("18888101", first.ToString(), StringComparison.Ordinal);
        Assert.Equal(new DateTimeOffset(2026, 8, 29, 12, 9, 0, TimeSpan.Zero), first.ResultValidThrough);
    }

    [Fact]
    public void Constants_preserve_a_synthetic_only_same_status_advance()
    {
        Assert.Equal(10, TelehealthApplicantRequestParticipationEvaluationPolicy.EntryRequestVersion);
        Assert.Equal(11, TelehealthApplicantRequestParticipationEvaluationPolicy.ResultingRequestVersion);
        Assert.Equal(
            "BILLING_ENTITY_RENDERING_PROVIDER_NETWORK_LOCATION_SERVICE_MODALITY_NEW_PATIENT",
            TelehealthApplicantRequestParticipationEvaluationPolicy.EvaluationScope);
        Assert.True(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.Verification));
    }

    private static EvaluateTelehealthApplicantRequestParticipation Valid() => new(
        10,
        new string('a', 64),
        true,
        true,
        true,
        true);

    private static TelehealthApplicantRequestParticipationEvaluationSnapshot Snapshot(
        Guid? participationContextConfirmationId = null,
        string? contextFingerprint = null)
    {
        var rule = TelehealthApplicantRequestParticipationEvaluationPolicy.ResolveRule("GA");
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");
        return TelehealthApplicantRequestParticipationEvaluationPolicy.Snapshot(
            Guid.Parse("50000000-0000-4000-8000-000000000001"),
            Guid.Parse("50000000-0000-4000-8000-000000000002"),
            Guid.Parse("50000000-0000-4000-8000-000000000003"),
            Guid.Parse("50000000-0000-4000-8000-000000000004"),
            Guid.Parse("50000000-0000-4000-8000-000000000005"),
            participationContextConfirmationId ?? Guid.Parse("50000000-0000-4000-8000-000000000006"),
            10,
            "synthetic-patient-50",
            "avenchart-synthetic-practice",
            10,
            "AvenChart Synthetic Practice",
            "harbor-mutual-hd",
            "Harbor Mutual",
            "High Deductible",
            "GA",
            "migraine",
            new DateOnly(2026, 8, 29),
            101,
            "Alex Walker",
            "18888101",
            contextFingerprint ?? new string('a', 64),
            now.AddMinutes(-1),
            now.AddMinutes(9),
            now.AddMinutes(20),
            rule);
    }
}
