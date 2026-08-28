// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthApplicantIdentityReviewPolicy
{
    public const string PolicyKey = "SYNTHETIC_STAFF_IDENTITY_REVIEW";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "CONTACT_CONTROL_AND_DUPLICATE_DISPOSITION_ONLY";

    public static string AllowedDecision(string duplicateDisposition) => duplicateDisposition switch
    {
        "NoCandidate" => "ApprovedForProspectiveIntake",
        "PossibleMatchManualReview" => "ManualReviewRequired",
        _ => throw TelehealthProblem.Conflict(
            "telehealth_applicant_identity_review_evidence_invalid",
            "The server-held duplicate disposition does not permit a bounded identity-review decision.")
    };

    public static string ResultingStatus(string decision) => decision switch
    {
        "ApprovedForProspectiveIntake" => "IdentityReviewApproved",
        "ManualReviewRequired" => "ManualReviewRequired",
        _ => throw TelehealthProblem.BadRequest(
            "telehealth_applicant_identity_review_decision_invalid",
            "Decision must be ApprovedForProspectiveIntake or ManualReviewRequired.")
    };

    public static string NormalizeReason(string? value)
    {
        var reason = value?.Trim() ?? string.Empty;
        if (reason.Length is < 10 or > 1000
            || reason.Any(character => char.IsControl(character)
                && character is not '\r' and not '\n' and not '\t'))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_identity_review_reason_invalid",
                "Reason must contain 10 to 1000 characters and no unsupported control characters.");
        }
        return reason;
    }
}
