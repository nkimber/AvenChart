// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthApplicantPracticeReviewClaimPolicy
{
    public const string PolicyKey = "SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PENDING_PRACTICE_REVIEW_SHORT_LEASE_RECEIPT";
    public const int LeaseSeconds = 120;

    public static void RequireAcknowledgments(
        bool noDecisionAcknowledged,
        bool noPatientContactAcknowledged,
        bool noRequestOrCareQueueAcknowledged)
    {
        if (!noDecisionAcknowledged
            || !noPatientContactAcknowledged
            || !noRequestOrCareQueueAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_practice_review_claim_acknowledgments_required",
                "Acknowledge every synthetic review-claim limitation before continuing.");
        }
    }
}
