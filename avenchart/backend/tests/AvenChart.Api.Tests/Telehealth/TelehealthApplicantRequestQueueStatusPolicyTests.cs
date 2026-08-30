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
    [InlineData(TelehealthRequestStatus.Reserved, true)]
    [InlineData(TelehealthRequestStatus.Connecting, true)]
    [InlineData(TelehealthRequestStatus.InConsultation, true)]
    [InlineData(TelehealthRequestStatus.WrapUp, true)]
    [InlineData(TelehealthRequestStatus.Closed, true)]
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
        Assert.False(result.SyntheticRenderingCandidateMatched);
        Assert.False(result.RealRenderingPhysicianNetworkConfirmed);
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
        Assert.False(result.SyntheticRenderingCandidateMatched);
        AssertClosedConsequences(result);
    }

    [Fact]
    public void ReservedDisclosesOnlySyntheticAssignmentWithoutPhysicianIdentityOrCareAuthority()
    {
        var result = Create(TelehealthRequestStatus.Reserved, 14, null);

        Assert.Equal("PhysicianPreparing", result.Phase);
        Assert.True(result.PracticeAccepted);
        Assert.True(result.DoctorSearchStarted);
        Assert.True(result.RenderingPhysicianAssigned);
        Assert.True(result.SyntheticRenderingCandidateMatched);
        Assert.False(result.RenderingPhysicianIdentityDisclosed);
        Assert.False(result.RealRenderingPhysicianNetworkConfirmed);
        Assert.Null(result.ApproximateRequestsAhead);
        Assert.False(result.PositionIsApproximate);
        AssertClosedConsequences(result);
    }

    [Fact]
    public void ConnectingDisclosesOnlyPrivateSyntheticWaitingRoomWithoutMediaOrCommunication()
    {
        var result = Create(TelehealthRequestStatus.Connecting, 15, null);

        Assert.Equal("ConnectionRoom", result.Phase);
        Assert.True(result.RenderingPhysicianAssigned);
        Assert.True(result.SyntheticRenderingCandidateMatched);
        Assert.True(result.ConnectionRoomCreated);
        Assert.True(result.PatientWaitingRoomEntered);
        Assert.False(result.MediaSessionCreated);
        Assert.False(result.CommunicationStarted);
        AssertClosedConsequences(result);
    }

    [Fact]
    public void InConsultationDisclosesOnlyBoundedSyntheticLifecycleWithoutCareOrCoverageClaims()
    {
        var result = Create(TelehealthRequestStatus.InConsultation, 16, null);

        Assert.Equal("Consultation", result.Phase);
        Assert.True(result.RenderingPhysicianAssigned);
        Assert.True(result.SyntheticRenderingCandidateMatched);
        Assert.False(result.RenderingPhysicianIdentityDisclosed);
        Assert.False(result.ConnectionRoomCreated);
        Assert.False(result.PatientWaitingRoomEntered);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("bounded synthetic lifecycle", StringComparison.Ordinal));
        AssertClosedConsequences(result);
    }

    [Fact]
    public void WrapUpDisclosesOnlyUnsignedPlanningStateWithoutCompletionOrPrescriptionClaims()
    {
        var result = Create(TelehealthRequestStatus.WrapUp, 17, null);

        Assert.Equal("WrapUp", result.Phase);
        Assert.True(result.RenderingPhysicianAssigned);
        Assert.True(result.SyntheticRenderingCandidateMatched);
        Assert.False(result.RenderingPhysicianIdentityDisclosed);
        Assert.False(result.ConnectionRoomCreated);
        Assert.False(result.PatientWaitingRoomEntered);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("unsigned planning drafts", StringComparison.Ordinal));
        AssertClosedConsequences(result);
    }

    [Fact]
    public void ClosedDisclosesOnlyLifecycleClosureWithoutEncounterCompletionOrDownstreamClaims()
    {
        var result = Create(TelehealthRequestStatus.Closed, 18, null);

        Assert.Equal("SyntheticLifecycleClosed", result.Phase);
        Assert.True(result.PracticeAccepted);
        Assert.True(result.DoctorSearchStarted);
        Assert.False(result.RenderingPhysicianAssigned);
        Assert.False(result.SyntheticRenderingCandidateMatched);
        Assert.False(result.ConnectionRoomCreated);
        Assert.False(result.PatientWaitingRoomEntered);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("appointment and encounter remain incomplete", StringComparison.Ordinal));
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
        Assert.False(result.RealRenderingPhysicianNetworkConfirmed);
        if (result.RequestStatus != TelehealthRequestStatus.Connecting.ToString())
        {
            Assert.False(result.ConnectionRoomCreated);
            Assert.False(result.PatientWaitingRoomEntered);
        }
        Assert.False(result.MediaSessionCreated);
        Assert.False(result.CommunicationStarted);
        Assert.False(result.CoverageVerified);
        Assert.False(result.ConsentCreated);
        Assert.False(result.CareAuthorized);
        Assert.False(result.IntegrationEnabled);
        Assert.False(result.ExternalCallPerformed);
        Assert.Contains(result.SafetyActions, action => action.Contains("911", StringComparison.Ordinal));
    }
}
