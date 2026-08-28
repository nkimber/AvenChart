// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record NormalizedTelehealthProspectiveVisitPurpose(
    int ExpectedVersion,
    string PurposeCategory,
    string PurposeDisplayLabel);

public static class TelehealthProspectiveVisitPurposePolicy
{
    public static NormalizedTelehealthProspectiveVisitPurpose Normalize(
        RecordTelehealthProspectiveVisitPurposeRequest request)
    {
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "ExpectedVersion must be positive.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_synthetic_confirmation_required",
                "Confirm that the visit purpose is fictional synthetic demonstration data.");
        }

        var category = request.PurposeCategory?.Trim().ToLowerInvariant() ?? string.Empty;
        var displayLabel = category switch
        {
            "migraine" => "Headache or known migraine pattern",
            "sleep" => "Sleep difficulty",
            _ => throw TelehealthProblem.BadRequest(
                "telehealth_applicant_visit_purpose_invalid",
                "Choose one of the supported synthetic visit-purpose categories.")
        };

        return new NormalizedTelehealthProspectiveVisitPurpose(
            request.ExpectedVersion,
            category,
            displayLabel);
    }
}
