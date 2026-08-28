// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestLocationPolicyTests
{
    private static readonly string Snapshot = new('a', 64);

    [Fact]
    public void Normalize_accepts_supported_exact_shape_and_canonicalizes_state_and_snapshot()
    {
        var normalized = TelehealthApplicantRequestLocationPolicy.Normalize(Valid(
            stateCode: " ga ",
            snapshot: Snapshot.ToUpperInvariant()));

        Assert.Equal(1, normalized.ExpectedRequestVersion);
        Assert.Equal("GA", normalized.CurrentLocationStateCode);
        Assert.Equal(Snapshot, normalized.ContextSnapshotFingerprint);
        Assert.True(normalized.CurrentLocationConfirmed);
        Assert.True(normalized.CallbackNumberConfirmed);
        Assert.True(normalized.ChangedLocationRequiresRestartAcknowledged);
        Assert.True(normalized.UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged);
    }

    [Theory]
    [InlineData("GA")]
    [InlineData("CA")]
    [InlineData("FL")]
    public void Normalize_accepts_each_approved_state(string stateCode)
    {
        Assert.Equal(
            stateCode,
            TelehealthApplicantRequestLocationPolicy.Normalize(Valid(stateCode: stateCode))
                .CurrentLocationStateCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_rejects_nonpositive_request_version(int version)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestLocationPolicy.Normalize(Valid(version: version)));

        Assert.Equal("telehealth_applicant_request_location_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Normalize_rejects_invalid_snapshot(string snapshot)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestLocationPolicy.Normalize(Valid(snapshot: snapshot)));

        Assert.Equal("telehealth_applicant_request_location_snapshot_invalid", problem.Code);
    }

    [Theory]
    [InlineData("NY")]
    [InlineData("Georgia")]
    [InlineData("")]
    public void Normalize_rejects_unsupported_state(string stateCode)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestLocationPolicy.Normalize(Valid(stateCode: stateCode)));

        Assert.Equal("telehealth_applicant_request_location_state_unsupported", problem.Code);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Normalize_requires_every_confirmation(
        bool location,
        bool callback,
        bool changedLocation,
        bool urgent)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestLocationPolicy.Normalize(new(
                1, Snapshot, "GA", location, callback, changedLocation, urgent)));

        Assert.Equal("telehealth_applicant_request_location_acknowledgments_required", problem.Code);
    }

    [Fact]
    public void Snapshot_is_deterministic_masked_and_source_bound()
    {
        var requestId = Guid.Parse("41000000-0000-4000-8000-000000000041");
        var creationId = Guid.Parse("42000000-0000-4000-8000-000000000042");
        var readinessId = Guid.Parse("43000000-0000-4000-8000-000000000043");

        var first = TelehealthApplicantRequestLocationPolicy.Snapshot(
            requestId, creationId, readinessId, 1, "GA", "0123");
        var replay = TelehealthApplicantRequestLocationPolicy.Snapshot(
            requestId, creationId, readinessId, 1, "GA", "0123");
        var changed = TelehealthApplicantRequestLocationPolicy.Snapshot(
            requestId, creationId, readinessId, 1, "CA", "0123");

        Assert.Equal(first, replay);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
        Assert.Equal("***-***-0123", first.MaskedCallbackPhone);
        Assert.DoesNotContain("0123", first.Fingerprint, StringComparison.Ordinal);
    }

    private static ConfirmTelehealthApplicantRequestLocation Valid(
        int version = 1,
        string? snapshot = null,
        string stateCode = "GA") => new(
            version,
            snapshot ?? Snapshot,
            stateCode,
            CurrentLocationConfirmed: true,
            CallbackNumberConfirmed: true,
            ChangedLocationRequiresRestartAcknowledged: true,
            UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true);
}
