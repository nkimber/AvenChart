// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

namespace AvenChart.Api.Features.Telehealth;

public sealed record NormalizedTelehealthProspectiveMemberInsuranceDetails(
    int ExpectedVersion,
    string MemberId,
    string? GroupNumber,
    string SubscriberRelationship,
    string? SubscriberFirstName,
    string? SubscriberLastName,
    DateOnly? SubscriberDateOfBirth,
    bool DetailsConfirmed,
    bool SyntheticDataConfirmed);

public sealed record TelehealthProtectedMemberInsurancePayload(
    string MemberId,
    string? GroupNumber,
    string SubscriberRelationship,
    string SubscriberFirstName,
    string SubscriberLastName,
    DateOnly SubscriberDateOfBirth,
    string CoveragePriority);

public static partial class TelehealthProspectiveMemberInsuranceDetailsPolicy
{
    public const string CoveragePriority = "Primary";

    [GeneratedRegex("^SYN-[A-Z0-9][A-Z0-9-]{0,26}[A-Z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex SyntheticIdentifierPattern();

    public static NormalizedTelehealthProspectiveMemberInsuranceDetails Normalize(
        RecordTelehealthProspectiveMemberInsuranceDetailsRequest request,
        DateOnly today)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "Expected applicant version must be positive.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_member_details_synthetic_acknowledgment_required",
                "Confirm that every insurance value is synthetic demonstration data.");
        }
        if (!request.DetailsConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_member_details_confirmation_required",
                "Review and confirm the synthetic member details before continuing.");
        }

        var memberId = NormalizeIdentifier(request.MemberId, "Member ID", required: true)!;
        var groupNumber = NormalizeIdentifier(request.GroupNumber, "Group number", required: false);
        var relationship = request.SubscriberRelationship?.Trim() ?? string.Empty;
        if (relationship is not ("Self" or "Spouse" or "Parent" or "Other"))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_subscriber_relationship_invalid",
                "Subscriber relationship must be Self, Spouse, Parent, or Other.");
        }

        if (relationship == "Self")
        {
            if (!string.IsNullOrWhiteSpace(request.SubscriberFirstName)
                || !string.IsNullOrWhiteSpace(request.SubscriberLastName)
                || !string.IsNullOrWhiteSpace(request.SubscriberDateOfBirth))
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_self_subscriber_details_forbidden",
                    "Do not re-enter subscriber identity when the synthetic subscriber is the applicant.");
            }

            return new(
                request.ExpectedVersion,
                memberId,
                groupNumber,
                relationship,
                null,
                null,
                null,
                request.DetailsConfirmed,
                request.SyntheticDataConfirmed);
        }

        var firstName = NormalizeName(request.SubscriberFirstName, "Subscriber first name");
        var lastName = NormalizeName(request.SubscriberLastName, "Subscriber last name");
        if (!DateOnly.TryParseExact(
                request.SubscriberDateOfBirth?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOfBirth)
            || dateOfBirth > today.AddYears(-18)
            || dateOfBirth < today.AddYears(-120))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_subscriber_birth_date_invalid",
                "Non-self subscriber date of birth must identify a synthetic adult age 18 through 120 and use YYYY-MM-DD.");
        }

        return new(
            request.ExpectedVersion,
            memberId,
            groupNumber,
            relationship,
            firstName,
            lastName,
            dateOfBirth,
            request.DetailsConfirmed,
            request.SyntheticDataConfirmed);
    }

    public static TelehealthProtectedMemberInsurancePayload ResolveSubscriber(
        NormalizedTelehealthProspectiveMemberInsuranceDetails details,
        string applicantFirstName,
        string applicantLastName,
        DateOnly applicantDateOfBirth) =>
        details.SubscriberRelationship == "Self"
            ? new(
                details.MemberId,
                details.GroupNumber,
                details.SubscriberRelationship,
                applicantFirstName,
                applicantLastName,
                applicantDateOfBirth,
                CoveragePriority)
            : new(
                details.MemberId,
                details.GroupNumber,
                details.SubscriberRelationship,
                details.SubscriberFirstName!,
                details.SubscriberLastName!,
                details.SubscriberDateOfBirth!.Value,
                CoveragePriority);

    public static string Mask(string value) => $"••••{value[^4..]}";

    private static string? NormalizeIdentifier(string? value, string label, bool required)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!required && normalized.Length == 0)
        {
            return null;
        }
        if (!SyntheticIdentifierPattern().IsMatch(normalized))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_synthetic_insurance_identifier_invalid",
                $"{label} must be a 6 to 32 character SYN- demonstration identifier using only letters, numbers, and hyphens.");
        }
        return normalized;
    }

    private static string NormalizeName(string? value, string label)
    {
        var normalized = string.Join(' ', (value ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length is < 1 or > 100
            || normalized.Any(char.IsControl)
            || normalized.Any(character => !char.IsLetter(character) && character is not ' ' and not '-' and not '\''))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_subscriber_name_invalid",
                $"{label} must contain 1 to 100 letters, spaces, hyphens, or apostrophes.");
        }
        return normalized;
    }
}
