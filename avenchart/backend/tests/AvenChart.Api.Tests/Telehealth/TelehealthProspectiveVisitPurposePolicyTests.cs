// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthProspectiveVisitPurposePolicyTests
{
    [Theory]
    [InlineData("migraine", "migraine", "Headache or known migraine pattern")]
    [InlineData(" MIGRAINE ", "migraine", "Headache or known migraine pattern")]
    [InlineData("sleep", "sleep", "Sleep difficulty")]
    [InlineData(" SLEEP ", "sleep", "Sleep difficulty")]
    public void Normalize_allows_only_controlled_navigation_categories(
        string supplied,
        string category,
        string label)
    {
        var normalized = TelehealthProspectiveVisitPurposePolicy.Normalize(
            new RecordTelehealthProspectiveVisitPurposeRequest(4, supplied, true));

        Assert.Equal(4, normalized.ExpectedVersion);
        Assert.Equal(category, normalized.PurposeCategory);
        Assert.Equal(label, normalized.PurposeDisplayLabel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("cancer")]
    [InlineData("migraine with free text")]
    [InlineData("sore-throat")]
    public void Normalize_rejects_unknown_or_free_text_categories(string supplied)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveVisitPurposePolicy.Normalize(
                new RecordTelehealthProspectiveVisitPurposeRequest(4, supplied, true)));

        Assert.Equal("telehealth_applicant_visit_purpose_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_requires_positive_expected_version()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveVisitPurposePolicy.Normalize(
                new RecordTelehealthProspectiveVisitPurposeRequest(0, "migraine", true)));

        Assert.Equal("telehealth_applicant_version_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_requires_synthetic_confirmation()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveVisitPurposePolicy.Normalize(
                new RecordTelehealthProspectiveVisitPurposeRequest(4, "sleep", false)));

        Assert.Equal("telehealth_synthetic_confirmation_required", problem.Code);
    }
}
