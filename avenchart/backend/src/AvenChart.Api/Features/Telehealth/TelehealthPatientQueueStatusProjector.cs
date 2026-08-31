// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthPatientQueueStatusProjector
{
    public const int RefreshAfterSeconds = 5;

    private static readonly IReadOnlyList<string> SafetyActions =
    [
        "If symptoms worsen or you are unsure it is safe to wait, contact the practice or seek in-person care.",
        "Call 911 now for an emergency."
    ];

    public static TelehealthPatientQueueStatusResponse Create(
        Guid requestId,
        TelehealthRequestStatus status,
        int requestVersion,
        DateTimeOffset requestUpdatedAt,
        DateTimeOffset snapshotAt,
        int? approximateRequestsAhead)
    {
        var content = ContentFor(status, approximateRequestsAhead);
        return new TelehealthPatientQueueStatusResponse(
            requestId,
            status.ToString(),
            requestVersion,
            content.Phase,
            content.Headline,
            content.Detail,
            status == TelehealthRequestStatus.Queued ? approximateRequestsAhead : null,
            status == TelehealthRequestStatus.Queued && approximateRequestsAhead.HasValue,
            false,
            "A wait-time estimate is not available in this synthetic demonstration.",
            requestUpdatedAt,
            snapshotAt,
            RefreshAfterSeconds,
            false,
            SafetyActions);
    }

    private static StatusContent ContentFor(TelehealthRequestStatus status, int? requestsAhead) => status switch
    {
        TelehealthRequestStatus.Queued when requestsAhead == 0 => new(
            "InQueue",
            "You're in line",
            "You are next by the current queue order. This is approximate and can change for safety or operational reasons."),
        TelehealthRequestStatus.Queued when requestsAhead > 0 => new(
            "InQueue",
            "You're in line",
            $"Approximately {requestsAhead} {(requestsAhead == 1 ? "request is" : "requests are")} ahead. This can change for safety or operational reasons."),
        TelehealthRequestStatus.Queued => new(
            "InQueue",
            "You're in line",
            "Your approximate queue position is temporarily unavailable. Your request remains in line; refresh or keep this page open."),
        TelehealthRequestStatus.Reserved => new(
            "PhysicianPreparing",
            "A physician is getting ready",
            "Keep this page open. You can run the synthetic device check when the connection-room action appears."),
        TelehealthRequestStatus.Connecting => new(
            "ConnectionRoom",
            "Your private connection room is ready",
            "This synthetic room transports no media and does not start a consultation. Keep emergency and callback guidance available while you wait."),
        TelehealthRequestStatus.InConsultation => new(
            "Consultation",
            "Your synthetic consultation has started",
            "This is lifecycle demonstration data only. No real media, diagnosis, prescription, completion, or claim is available."),
        TelehealthRequestStatus.WrapUp => new(
            "WrapUp",
            "Your physician is finishing the synthetic visit record",
            "This visit is not complete. No signed record, after-visit summary, prescription, or claim is available. Follow the practice guidance you received, and use the emergency action below if needed."),
        TelehealthRequestStatus.Closed => new(
            "SyntheticLifecycleClosed",
            "The synthetic visit lifecycle has closed",
            "The physician's synthetic workflow has ended. This does not mean the appointment or encounter is complete and does not create a signed record, after-visit summary, prescription delivery, bill, claim, or external action."),
        TelehealthRequestStatus.Cancelled => new(
            "RequestCancelled",
            "This synthetic request was cancelled",
            "The request is no longer in the queue. If it had already been authorized to the queue, its provisional synthetic appointment was cancelled before any clinician reservation or consultation."),
        TelehealthRequestStatus.Redirected => new(
            "Redirected",
            "This request cannot enter the telehealth queue",
            "Follow the care guidance already shown. Do not wait on this page if symptoms are urgent or worsening."),
        TelehealthRequestStatus.EmergencyRedirected => new(
            "EmergencyRedirected",
            "Call 911 now",
            "This request cannot continue toward telehealth. Call 911 or go to the nearest emergency department; the application did not dispatch help."),
        TelehealthRequestStatus.InPersonRecommended => new(
            "InPersonRecommended",
            "In-person evaluation is recommended",
            "This request cannot continue toward the telehealth queue. Follow the in-person care direction already shown."),
        TelehealthRequestStatus.ClinicalReview => new(
            "ClinicalReview",
            "Clinical review is required",
            "This request cannot continue automatically. A qualified clinical-review workflow is required; no reviewer assignment is promised here."),
        TelehealthRequestStatus.SafetyScreening => new(
            "Reviewing",
            "Continue the safety questions",
            "The universal safety screen passed, but complaint-specific triage is still required before this request can continue."),
        TelehealthRequestStatus.OperationalReview => new(
            "Reviewing",
            "Reviewing your request",
            "Your practice has not placed this request in the physician queue yet."),
        TelehealthRequestStatus.Verification => new(
            "Reviewing",
            "More information is needed",
            "Complete the current verification step before this request can reach practice review."),
        TelehealthRequestStatus.Intake => new(
            "Reviewing",
            "Complete your request details",
            "Complete the current readiness step before this request can reach practice review."),
        TelehealthRequestStatus.LocationConfirmed => new(
            "Reviewing",
            "Complete the safety questions",
            "Complete the synthetic safety questions before this request can continue."),
        _ => new(
            "Reviewing",
            "Finish starting your request",
            "Complete the current request step before practice review can begin.")
    };

    private sealed record StatusContent(string Phase, string Headline, string Detail);
}
