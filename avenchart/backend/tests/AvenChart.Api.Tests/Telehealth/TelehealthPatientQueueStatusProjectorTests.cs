// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthPatientQueueStatusProjectorTests
{
    private static readonly DateTimeOffset UpdatedAt = new(2026, 8, 27, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SnapshotAt = UpdatedAt.AddSeconds(1);

    [Theory]
    [InlineData(0, "You are next by the current queue order")]
    [InlineData(1, "Approximately 1 request is ahead")]
    [InlineData(3, "Approximately 3 requests are ahead")]
    public void QueuedPositionIsApproximateAndNeverAWaitPromise(int requestsAhead, string expectedDetail)
    {
        var result = Create(TelehealthRequestStatus.Queued, requestsAhead);

        Assert.Equal("InQueue", result.Phase);
        Assert.Equal(requestsAhead, result.ApproximateRequestsAhead);
        Assert.True(result.PositionIsApproximate);
        Assert.False(result.WaitEstimateAvailable);
        Assert.Contains(expectedDetail, result.Detail, StringComparison.Ordinal);
        Assert.Contains("not available", result.WaitEstimateMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.RealtimeAvailable);
    }

    [Fact]
    public void MissingQueueEvidenceFailsClosedToPositionUnavailable()
    {
        var result = Create(TelehealthRequestStatus.Queued, null);

        Assert.Null(result.ApproximateRequestsAhead);
        Assert.False(result.PositionIsApproximate);
        Assert.Contains("temporarily unavailable", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(TelehealthRequestStatus.OperationalReview, "Reviewing", "Reviewing your request")]
    [InlineData(TelehealthRequestStatus.Reserved, "PhysicianPreparing", "A physician is getting ready")]
    [InlineData(TelehealthRequestStatus.Connecting, "ConnectionRoom", "connection room is ready")]
    [InlineData(TelehealthRequestStatus.InConsultation, "Consultation", "consultation has started")]
    [InlineData(TelehealthRequestStatus.Closed, "SyntheticLifecycleClosed", "lifecycle has closed")]
    [InlineData(TelehealthRequestStatus.Redirected, "Redirected", "cannot enter the telehealth queue")]
    [InlineData(TelehealthRequestStatus.EmergencyRedirected, "EmergencyRedirected", "Call 911 now")]
    [InlineData(TelehealthRequestStatus.InPersonRecommended, "InPersonRecommended", "In-person evaluation")]
    [InlineData(TelehealthRequestStatus.ClinicalReview, "ClinicalReview", "Clinical review is required")]
    [InlineData(TelehealthRequestStatus.SafetyScreening, "Reviewing", "Continue the safety questions")]
    public void NonQueuedStatesExposeCalmBoundedContent(
        TelehealthRequestStatus status,
        string expectedPhase,
        string expectedHeadline)
    {
        var result = Create(status, 99);

        Assert.Equal(expectedPhase, result.Phase);
        Assert.Contains(expectedHeadline, result.Headline, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.ApproximateRequestsAhead);
        Assert.False(result.PositionIsApproximate);
        Assert.Contains(result.SafetyActions, action => action.Contains("911", StringComparison.Ordinal));
    }

    private static TelehealthPatientQueueStatusResponse Create(
        TelehealthRequestStatus status,
        int? requestsAhead) =>
        TelehealthPatientQueueStatusProjector.Create(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            status,
            7,
            UpdatedAt,
            SnapshotAt,
            requestsAhead);
}
