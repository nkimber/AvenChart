// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthApplicantPracticeReviewAuthorizationPolicy
{
    public const string PolicyKey = "SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "CURRENT_CLAIMANT_MINIMIZED_PACKET_REVIEW_ONLY";
    public const string Decision = "AuthorizedForSyntheticRequestCreation";
    public const string RationaleCode = "OperationalPrerequisitesReviewed";
    public const string ResultingApplicantStatus = "SyntheticPracticeReviewAuthorized";

    public static void RequireCommand(
        string? decision,
        string? rationaleCode,
        bool noClinicalEligibilityAcknowledged,
        bool noCoverageGuaranteeAcknowledged,
        bool noRequestOrQueueAcknowledged)
    {
        if (!string.Equals(decision?.Trim(), Decision, StringComparison.Ordinal)
            || !string.Equals(rationaleCode?.Trim(), RationaleCode, StringComparison.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_practice_review_authorization_decision_invalid",
                "Only the controlled synthetic request-creation authorization is available.");
        }
        if (!noClinicalEligibilityAcknowledged
            || !noCoverageGuaranteeAcknowledged
            || !noRequestOrQueueAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_practice_review_authorization_acknowledgments_required",
                "Acknowledge every operational-authorization limitation before continuing.");
        }
    }
}
