// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AvenChart.Api.Features.Telehealth;

public sealed record NormalizedTelehealthProspectiveApplicant(
    string LegalFirstName,
    string LegalLastName,
    DateOnly DateOfBirth,
    string Email,
    string Phone,
    string ResidenceStateCode,
    string PostalCode);

public static partial class TelehealthProspectiveApplicantPolicy
{
    public const string DemonstrationVerificationCode = "246810";
    public const int MaximumVerificationAttempts = 5;
    public static readonly TimeSpan ApplicantLifetime = TimeSpan.FromMinutes(30);

    [GeneratedRegex("^[0-9]{5}$", RegexOptions.CultureInvariant)]
    private static partial Regex PostalCodePattern();

    public static NormalizedTelehealthProspectiveApplicant Normalize(
        CreateTelehealthProspectiveApplicantRequest request,
        IReadOnlyCollection<string> supportedStates,
        DateOnly today)
    {
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_synthetic_acknowledgment_required",
                "Confirm that every value is synthetic demonstration data before continuing.");
        }

        var firstName = NormalizeName(request.LegalFirstName, "Legal first name");
        var lastName = NormalizeName(request.LegalLastName, "Legal last name");
        if (!DateOnly.TryParseExact(
                request.DateOfBirth?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOfBirth))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_birth_date_invalid",
                "Date of birth must use YYYY-MM-DD.");
        }

        var latestAdultBirthDate = today.AddYears(-18);
        if (dateOfBirth > latestAdultBirthDate || dateOfBirth < today.AddYears(-120))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_adult_required",
                "This synthetic new-patient path is limited to adults age 18 through 120.");
        }

        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (email.Length is < 3 or > 254
            || email.Any(char.IsControl)
            || !MailAddress.TryCreate(email, out var parsedEmail)
            || !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_email_invalid",
                "Enter a valid synthetic email address.");
        }

        var phoneDigits = new string((request.Phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (phoneDigits.Length == 11 && phoneDigits[0] == '1')
        {
            phoneDigits = phoneDigits[1..];
        }
        if (phoneDigits.Length != 10 || phoneDigits[0] is '0' or '1')
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_phone_invalid",
                "Enter a valid 10-digit synthetic US phone number.");
        }
        var phone = $"+1{phoneDigits}";

        var state = request.ResidenceStateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!supportedStates.Contains(state, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_state_unsupported",
                "Synthetic residence must be Georgia, California, or Florida.");
        }

        var postalCode = request.PostalCode?.Trim() ?? string.Empty;
        if (!PostalCodePattern().IsMatch(postalCode))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_postal_code_invalid",
                "Enter a five-digit synthetic postal code.");
        }

        return new NormalizedTelehealthProspectiveApplicant(
            firstName, lastName, dateOfBirth, email, phone, state, postalCode);
    }

    public static string RequireAccessKey(string? value)
    {
        var key = value ?? string.Empty;
        if (key.Length is < 43 or > 256 || key.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw TelehealthProblem.ApplicantUnauthorized();
        }
        return key;
    }

    public static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string VerificationHash(Guid applicantId, string verificationCode) =>
        Hash($"telehealth-applicant-contact-v1\u001f{applicantId:N}\u001f{verificationCode}");

    public static bool FixedTimeHashEquals(string expectedHex, string suppliedHex)
    {
        if (expectedHex.Length != 64 || suppliedHex.Length != 64)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedHex),
            Convert.FromHexString(suppliedHex));
    }

    public static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@');
        return separator <= 0 ? "•••" : $"{email[0]}•••{email[separator..]}";
    }

    public static string MaskPhone(string phone) =>
        phone.Length < 4 ? "•••" : $"(***) ***-{phone[^4..]}";

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
                "telehealth_applicant_name_invalid",
                $"{label} must contain 1 to 100 letters, spaces, hyphens, or apostrophes.");
        }
        return normalized;
    }
}
