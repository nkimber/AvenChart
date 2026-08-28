// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthApplicantSyntheticPromotionPolicy
{
    public const string PolicyKey = "SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "AUTHORIZED_SYNTHETIC_APPLICANT_AND_CURRENT_DUPLICATE_RECHECK";
    public const string Command = "PromoteAuthorizedSyntheticApplicant";

    public static string CanonicalPatientId(Guid applicantId) =>
        $"TH-PAT-{applicantId:N}".ToUpperInvariant();

    public static string ResultingStatus(bool possibleMatchDetected) =>
        possibleMatchDetected
            ? "SyntheticPromotionBlockedPossibleMatch"
            : "SyntheticPatientPromoted";

    public static string Outcome(bool possibleMatchDetected) =>
        possibleMatchDetected
            ? "BlockedPossiblePatientMatch"
            : "SyntheticPatientCreated";

    public static string NormalizeReason(string? value)
    {
        var reason = value?.Trim() ?? string.Empty;
        if (reason.Length is < 10 or > 1000
            || reason.Any(character => char.IsControl(character)
                && character is not '\r' and not '\n' and not '\t'))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_synthetic_promotion_reason_invalid",
                "Reason must contain 10 to 1000 characters and no unsupported control characters.");
        }
        return reason;
    }
}
