// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestQueueStatusPolicyTests
{
    private static readonly DateTimeOffset UpdatedAt = new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TelehealthRequestStatus.OperationalReview, true)]
    [InlineData(TelehealthRequestStatus.Queued, true)]
    [InlineData(TelehealthRequestStatus.Reserved, false)]
    [InlineData(TelehealthRequestStatus.InConsultation, false)]
    [InlineData(TelehealthRequestStatus.Verification, false)]
    [InlineData(TelehealthRequestStatus.Redirected, false)]
    public void VisibleStatusesAreExplicitlyBounded(TelehealthRequestStatus status, bool expected)
    {
        Assert.Equal(expected, TelehealthApplicantRequestQueueStatusPolicy.IsVisibleStatus(status));
    }

    [Fact]
    public void OperationalReviewDoesNotImplyPracticeAcceptanceOrQueueEntry()
    {
        var result = Create(TelehealthRequestStatus.OperationalReview, 12, null);

        Assert.Equal("Reviewing", result.Phase);
        Assert.False(result.PracticeAccepted);
        Assert.False(result.DoctorSearchStarted);
        Assert.False(result.RenderingPhysicianAssigned);
        Assert.Null(result.ApproximateRequestsAhead);
        Assert.False(result.PositionIsApproximate);
        AssertClosedConsequences(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void QueuedCountIsApproximateAndNeverAnAssignedPositionOrWaitPromise(int requestsAhead)
    {
        var result = Create(TelehealthRequestStatus.Queued, 13, requestsAhead);

        Assert.Equal("InQueue", result.Phase);
        Assert.True(result.PracticeAccepted);
        Assert.True(result.DoctorSearchStarted);
        Assert.Equal(requestsAhead, result.ApproximateRequestsAhead);
        Assert.True(result.PositionIsApproximate);
        Assert.False(result.ExactQueuePositionAssigned);
        Assert.False(result.WaitEstimateAvailable);
        Assert.False(result.RealtimeAvailable);
        Assert.False(result.RenderingPhysicianAssigned);
        AssertClosedConsequences(result);
    }

    private static TelehealthApplicantRequestQueueStatusResponse Create(
        TelehealthRequestStatus status,
        int version,
        int? requestsAhead) =>
        TelehealthApplicantRequestQueueStatusPolicy.Create(new(
            Guid.Parse("53000000-0000-4000-8000-000000000053"),
            status,
            version,
            UpdatedAt,
            UpdatedAt.AddSeconds(1),
            requestsAhead));

    private static void AssertClosedConsequences(TelehealthApplicantRequestQueueStatusResponse result)
    {
        Assert.Equal("SYNTHETIC_APPLICANT_REQUEST_QUEUE_STATUS", result.PolicyKey);
        Assert.Equal(1, result.PolicyVersion);
        Assert.Equal("NON_PRODUCTION", result.SourceMode);
        Assert.False(result.RenderingPhysicianIdentityDisclosed);
        Assert.False(result.CoverageVerified);
        Assert.False(result.ConsentCreated);
        Assert.False(result.CareAuthorized);
        Assert.False(result.IntegrationEnabled);
        Assert.False(result.ExternalCallPerformed);
        Assert.Contains(result.SafetyActions, action => action.Contains("911", StringComparison.Ordinal));
    }
}
