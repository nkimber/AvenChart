// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthApplicantPromotionAuthorizationPolicy
{
    public const string PolicyKey = "SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY";

    public static readonly string[] Decisions =
    [
        "AuthorizedForSyntheticPromotion",
        "DeniedForSyntheticPromotion"
    ];

    public static string ResultingStatus(string decision) => decision switch
    {
        "AuthorizedForSyntheticPromotion" => "SyntheticPromotionAuthorized",
        "DeniedForSyntheticPromotion" => "SyntheticPromotionDenied",
        _ => throw TelehealthProblem.BadRequest(
            "telehealth_applicant_promotion_authorization_decision_invalid",
            "Decision must be AuthorizedForSyntheticPromotion or DeniedForSyntheticPromotion.")
    };

    public static string NormalizeReason(string? value)
    {
        var reason = value?.Trim() ?? string.Empty;
        if (reason.Length is < 10 or > 1000
            || reason.Any(character => char.IsControl(character)
                && character is not '\r' and not '\n' and not '\t'))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_promotion_authorization_reason_invalid",
                "Reason must contain 10 to 1000 characters and no unsupported control characters.");
        }
        return reason;
    }
}
