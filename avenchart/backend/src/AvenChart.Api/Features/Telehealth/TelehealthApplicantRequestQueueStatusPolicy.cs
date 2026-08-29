// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthApplicantRequestQueueStatusPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_QUEUE_STATUS";
    public const int PolicyVersion = 1;
    public const string SourceMode = "NON_PRODUCTION";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;

    public static bool IsVisibleStatus(TelehealthRequestStatus status) => status is
        TelehealthRequestStatus.OperationalReview
        or TelehealthRequestStatus.Queued;

    public static TelehealthApplicantRequestQueueStatusResponse Create(
        TelehealthApplicantRequestQueueStatusRecord record)
    {
        var projected = TelehealthPatientQueueStatusProjector.Create(
            record.RequestId,
            record.RequestStatus,
            record.RequestVersion,
            record.RequestUpdatedAt,
            record.SnapshotAt,
            record.ApproximateRequestsAhead);
        var accepted = record.RequestStatus != TelehealthRequestStatus.OperationalReview;

        return new(
            RequestId: projected.RequestId,
            RequestStatus: projected.RequestStatus,
            RequestVersion: projected.RequestVersion,
            PolicyKey: PolicyKey,
            PolicyVersion: PolicyVersion,
            SourceMode: SourceMode,
            Phase: projected.Phase,
            Headline: projected.Headline,
            Detail: projected.Detail,
            ApproximateRequestsAhead: projected.ApproximateRequestsAhead,
            PositionIsApproximate: projected.PositionIsApproximate,
            ExactQueuePositionAssigned: false,
            WaitEstimateAvailable: false,
            WaitEstimateMessage: projected.WaitEstimateMessage,
            RequestUpdatedAt: projected.RequestUpdatedAt,
            SnapshotAt: projected.SnapshotAt,
            RefreshAfterSeconds: projected.RefreshAfterSeconds,
            RealtimeAvailable: false,
            PracticeAccepted: accepted,
            DoctorSearchStarted: accepted,
            RenderingPhysicianAssigned: false,
            RenderingPhysicianIdentityDisclosed: false,
            CoverageVerified: false,
            ConsentCreated: false,
            CareAuthorized: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            SafetyActions: projected.SafetyActions,
            Limitations: [
                "NON_PRODUCTION synthetic status only. Authoritative HTTP polling is used; realtime delivery is not enabled.",
                "Any requests-ahead count is an approximate same-practice snapshot, not an assigned position or wait-time promise.",
                "This slice does not expose or authorize clinician assignment, connection, consultation, or completion states.",
                "No consent, care authorization, prescription, claim, integration, or external action is created by reading this status."
            ]);
    }
}
