// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantNoticePolicyTests
{
    [Theory]
    [InlineData("GA", "GA_TELEHEALTH_NOTICE_V1", "Georgia Composite Medical Board Rule 360-3-.07")]
    [InlineData("CA", "CA_TELEHEALTH_NOTICE_V1", "California Business and Professions Code § 2290.5")]
    [InlineData("FL", "FL_TELEHEALTH_NOTICE_V1", "Florida Statutes § 456.47")]
    public void ForState_UsesExactServerNotice(
        string state,
        string noticeKey,
        string sourceTitle)
    {
        var notice = TelehealthApplicantNoticePolicy.ForState(state);

        Assert.Equal(state, notice.StateCode);
        Assert.Equal(noticeKey, notice.NoticeKey);
        Assert.Equal(1, notice.NoticeVersion);
        Assert.Equal(sourceTitle, notice.SourceTitle);
        Assert.StartsWith("https://", notice.SourceUrl, StringComparison.Ordinal);
        Assert.NotEmpty(notice.Disclosures);
        Assert.NotEmpty(notice.DeferredRequirements);
    }

    [Fact]
    public void Contract_IsExplicitlySyntheticPendingReviewAndNonConsent()
    {
        Assert.Equal("SYNTHETIC_TELEHEALTH_NOTICE_ACKNOWLEDGMENT", TelehealthApplicantNoticePolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantNoticePolicy.PolicyVersion);
        Assert.Equal("SyntheticTelehealthNoticeAcknowledged", TelehealthApplicantNoticePolicy.ResultingStatus);
        Assert.Equal("STATE_NOTICE_FIXTURE_AND_PATIENT_ACKNOWLEDGMENTS_ONLY", TelehealthApplicantNoticePolicy.EvidenceType);
        Assert.Equal("PendingIndependentReview", TelehealthApplicantNoticePolicy.LegalReviewStatus);
    }

    [Fact]
    public void Normalize_RequiresExactServerNoticeAndAllAffirmations()
    {
        var notice = TelehealthApplicantNoticePolicy.ForState("CA");
        var normalized = TelehealthApplicantNoticePolicy.Normalize(Valid(notice), notice);

        Assert.Equal("CA", normalized.CurrentLocationStateCode);
        Assert.Equal("CA_TELEHEALTH_NOTICE_V1", normalized.NoticeKey);
        Assert.True(normalized.ClinicianReconfirmationRequiredAcknowledged);
    }

    [Fact]
    public void Normalize_RejectsChangedLocation()
    {
        var notice = TelehealthApplicantNoticePolicy.ForState("GA");
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantNoticePolicy.Normalize(
                Valid(notice) with { CurrentLocationStateCode = "FL" }, notice));

        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("telehealth_applicant_notice_location_changed", problem.Code);
    }

    [Fact]
    public void Normalize_RejectsStaleNotice()
    {
        var notice = TelehealthApplicantNoticePolicy.ForState("FL");
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantNoticePolicy.Normalize(
                Valid(notice) with { NoticeVersion = 2 }, notice));

        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("telehealth_applicant_notice_version_conflict", problem.Code);
    }

    [Theory]
    [InlineData("location")]
    [InlineData("mode")]
    [InlineData("privacy")]
    [InlineData("emergency")]
    [InlineData("inPerson")]
    [InlineData("clinician")]
    [InlineData("synthetic")]
    public void Normalize_RejectsEachMissingAffirmation(string field)
    {
        var notice = TelehealthApplicantNoticePolicy.ForState("GA");
        var request = field switch
        {
            "location" => Valid(notice) with { CurrentLocationConfirmed = false },
            "mode" => Valid(notice) with { ModeOfCareAcknowledged = false },
            "privacy" => Valid(notice) with { PrivacyLimitationsAcknowledged = false },
            "emergency" => Valid(notice) with { EmergencyInstructionsAcknowledged = false },
            "inPerson" => Valid(notice) with { InPersonOptionAcknowledged = false },
            "clinician" => Valid(notice) with { ClinicianReconfirmationRequiredAcknowledged = false },
            _ => Valid(notice) with { SyntheticDataConfirmed = false }
        };

        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantNoticePolicy.Normalize(request, notice));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_notice_acknowledgments_required", problem.Code);
    }

    [Fact]
    public void UnsupportedState_FailsClosed()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantNoticePolicy.ForState("NY"));

        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("telehealth_applicant_notice_state_unsupported", problem.Code);
    }

    private static AcknowledgeTelehealthApplicantNoticeRequest Valid(
        TelehealthApplicantNoticeDefinition notice) => new(
            12,
            notice.NoticeKey,
            notice.NoticeVersion,
            notice.StateCode,
            true,
            true,
            true,
            true,
            true,
            true,
            true);
}
